using System.Globalization;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AnalyticsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<AdminAnalyticsResponse>> GetAnalytics(
            [FromQuery] string period = "month",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string basis = "service")
        {
            var normalizedPeriod = NormalizePeriod(period);
            var normalizedBasis = NormalizeBasis(basis);

            var invoiceQuery = normalizedBasis == "payment"
                ? QueryByPaymentDate(from, to)
                : QueryByServiceDate(from, to);

            var invoices = await invoiceQuery.ToListAsync();
            var sessions = await _context.TableSessions.Select(s => s.StartedAt).ToListAsync();

            var revenueByPeriod = BuildRevenueSeries(normalizedPeriod, invoices, sessions);
            var revenueBySource = BuildRevenueBySource(invoices);

            var totalTables = await _context.Tables.CountAsync(t => t.IsActive);
            var heatmap = await BuildOccupancyHeatmapAsync(totalTables);
            var peakHours = BuildPeakHours(heatmap, totalTables);

            return Ok(new AdminAnalyticsResponse
            {
                Period = normalizedPeriod,
                Basis = normalizedBasis,
                RevenueByPeriod = revenueByPeriod,
                RevenueBySource = revenueBySource,
                OccupancyHeatmap = heatmap,
                PeakHours = peakHours,
                AverageOccupancyRate = heatmap.Count == 0 ? 0 : Math.Round(heatmap.Average(cell => cell.OccupancyRate), 2),
                PeakOccupancyRate = peakHours.Count == 0 ? 0 : peakHours.Max(hour => hour.OccupancyRate)
            });
        }

        private IQueryable<Models.Invoice> QueryByServiceDate(DateTime? from, DateTime? to)
        {
            var query = _context.Invoices.AsQueryable();
            if (from.HasValue) query = query.Where(i => i.ServiceDate >= from.Value.Date);
            if (to.HasValue) query = query.Where(i => i.ServiceDate <= to.Value.Date);
            return query;
        }

        private IQueryable<Models.Invoice> QueryByPaymentDate(DateTime? from, DateTime? to)
        {
            var query = _context.Invoices.Where(i => i.PaymentCompletedAt != null);
            if (from.HasValue) query = query.Where(i => i.PaymentCompletedAt >= from.Value);
            if (to.HasValue) query = query.Where(i => i.PaymentCompletedAt <= to.Value);
            return query;
        }

        private static List<AdminRevenueSourceResponse> BuildRevenueBySource(List<Models.Invoice> invoices)
        {
            var tableRevenue = invoices.Sum(i => i.TableTimeCost);
            var fnbRevenue = invoices.Sum(i => i.FnBTotal);
            var coachingRevenue = invoices.Sum(i => i.CoachingTotal);
            var total = tableRevenue + fnbRevenue + coachingRevenue;

            decimal ToPercentage(decimal amount) => total <= 0 ? 0 : Math.Round((amount / total) * 100m, 2);

            return new List<AdminRevenueSourceResponse>
            {
                new() { Label = "Tiền giờ chơi", Amount = tableRevenue, Percentage = ToPercentage(tableRevenue) },
                new() { Label = "Dịch vụ F&B", Amount = fnbRevenue, Percentage = ToPercentage(fnbRevenue) },
                new() { Label = "Huấn luyện viên", Amount = coachingRevenue, Percentage = ToPercentage(coachingRevenue) }
            };
        }

        private static List<AdminRevenuePointResponse> BuildRevenueSeries(string period, List<Models.Invoice> invoices, List<DateTime> sessions)
        {
            var now = DateTime.UtcNow;
            var points = new List<AdminRevenuePointResponse>();

            if (period == "week")
            {
                var weekStart = now.Date.AddDays(-6);
                for (var offset = 0; offset < 7; offset++)
                {
                    var day = weekStart.AddDays(offset);
                    points.Add(new AdminRevenuePointResponse
                    {
                        Label = day.ToString("dd MMM", CultureInfo.InvariantCulture),
                        Revenue = invoices.Where(i => i.ServiceDate.Date == day.Date || i.PaymentCompletedAt?.Date == day.Date).Sum(i => i.GrandTotal),
                        BookingCount = sessions.Count(start => start.Date == day.Date)
                    });
                }

                return points;
            }

            if (period == "day")
            {
                var dayStart = now.Date;
                for (var hour = 0; hour < 24; hour += 2)
                {
                    var bucketStart = dayStart.AddHours(hour);
                    var bucketEnd = bucketStart.AddHours(2);
                    points.Add(new AdminRevenuePointResponse
                    {
                        Label = bucketStart.ToString("HH:mm", CultureInfo.InvariantCulture),
                        Revenue = invoices.Where(i =>
                                (i.PaymentCompletedAt >= bucketStart && i.PaymentCompletedAt < bucketEnd) ||
                                (i.ServiceStartedAt >= bucketStart && i.ServiceStartedAt < bucketEnd))
                            .Sum(i => i.GrandTotal),
                        BookingCount = sessions.Count(start => start >= bucketStart && start < bucketEnd)
                    });
                }

                return points;
            }

            var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
            for (var offset = 0; offset < 12; offset++)
            {
                var currentMonth = monthStart.AddMonths(offset);
                var nextMonth = currentMonth.AddMonths(1);
                points.Add(new AdminRevenuePointResponse
                {
                    Label = currentMonth.ToString("MMM yy", CultureInfo.InvariantCulture),
                    Revenue = invoices.Where(i =>
                            (i.PaymentCompletedAt >= currentMonth && i.PaymentCompletedAt < nextMonth) ||
                            (i.ServiceDate >= currentMonth && i.ServiceDate < nextMonth))
                        .Sum(i => i.GrandTotal),
                    BookingCount = sessions.Count(start => start >= currentMonth && start < nextMonth)
                });
            }

            return points;
        }

        private async Task<List<AdminHeatmapCellResponse>> BuildOccupancyHeatmapAsync(int totalTables)
        {
            var analysisStart = DateTime.UtcNow.Date.AddDays(-27);

            var slots = await _context.ReservationSlots
                .Where(slot => slot.IsActive && slot.SlotDate >= analysisStart)
                .Select(slot => new { slot.SlotDate, slot.SlotStart })
                .ToListAsync();

            var groupedCounts = slots
                .GroupBy(slot => new { DayOfWeek = ToMondayBasedDayOfWeek(slot.SlotDate.DayOfWeek), Hour = slot.SlotStart.Hours })
                .ToDictionary(group => (group.Key.DayOfWeek, group.Key.Hour), group => group.Count());

            var heatmap = new List<AdminHeatmapCellResponse>();
            for (var day = 0; day < 7; day++)
            {
                for (var hour = 0; hour < 24; hour++)
                {
                    groupedCounts.TryGetValue((day, hour), out var bookingCount);
                    var occupancyRate = totalTables == 0 ? 0 : Math.Round((decimal)bookingCount / (totalTables * 2m) * 100m, 2);
                    heatmap.Add(new AdminHeatmapCellResponse
                    {
                        DayOfWeek = day,
                        Hour = hour,
                        BookingCount = bookingCount,
                        OccupancyRate = occupancyRate
                    });
                }
            }

            return heatmap;
        }

        private static List<AdminPeakHourResponse> BuildPeakHours(IEnumerable<AdminHeatmapCellResponse> heatmap, int totalTables)
        {
            return heatmap
                .GroupBy(cell => cell.Hour)
                .Select(group =>
                {
                    var bookingCount = group.Sum(cell => cell.BookingCount);
                    var occupancyRate = totalTables == 0 ? 0 : Math.Round((decimal)bookingCount / (group.Count() * totalTables * 2m) * 100m, 2);
                    return new AdminPeakHourResponse
                    {
                        Hour = group.Key,
                        BookingCount = bookingCount,
                        OccupancyRate = occupancyRate
                    };
                })
                .OrderByDescending(hour => hour.BookingCount)
                .ThenBy(hour => hour.Hour)
                .Take(6)
                .ToList();
        }

        private static string NormalizePeriod(string period) => period?.Trim().ToLowerInvariant() switch
        {
            "day" => "day",
            "week" => "week",
            _ => "month"
        };

        private static string NormalizeBasis(string basis) => basis?.Trim().ToLowerInvariant() == "payment" ? "payment" : "service";

        private static int ToMondayBasedDayOfWeek(DayOfWeek dayOfWeek) => dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
    }
}
