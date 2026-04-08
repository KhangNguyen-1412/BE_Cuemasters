using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
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
        public async Task<ActionResult<AdminAnalyticsResponse>> GetAnalytics([FromQuery] string period = "month", [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var normalizedPeriod = NormalizePeriod(period);
            var revenueByPeriod = await BuildRevenueSeriesAsync(normalizedPeriod, from, to);
            var revenueBySource = await BuildRevenueBySourceAsync(from, to);

            var totalTables = await _context.Tables.CountAsync(t => t.IsActive);
            var heatmap = await BuildOccupancyHeatmapAsync(totalTables);
            var peakHours = BuildPeakHours(heatmap, totalTables);

            return Ok(new AdminAnalyticsResponse
            {
                Period = normalizedPeriod,
                RevenueByPeriod = revenueByPeriod,
                RevenueBySource = revenueBySource,
                OccupancyHeatmap = heatmap,
                PeakHours = peakHours,
                AverageOccupancyRate = heatmap.Count == 0 ? 0 : Math.Round(heatmap.Average(cell => cell.OccupancyRate), 2),
                PeakOccupancyRate = peakHours.Count == 0 ? 0 : peakHours.Max(hour => hour.OccupancyRate)
            });
        }

        private async Task<List<AdminRevenueSourceResponse>> BuildRevenueBySourceAsync(DateTime? from = null, DateTime? to = null)
        {
            var paymentQuery = _context.Payments
                .Where(payment => payment.Status == PaymentStatus.Completed && payment.BookingId.HasValue);

            if (from.HasValue)
            {
                paymentQuery = paymentQuery.Where(payment => payment.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                paymentQuery = paymentQuery.Where(payment => payment.CreatedAt <= to.Value);
            }

            var bookingIds = await paymentQuery
                .Select(payment => payment.BookingId!.Value)
                .Distinct()
                .ToListAsync();

            if (bookingIds.Count == 0)
            {
                return BuildEmptyRevenueBySource();
            }

            var tableRevenue = await _context.Bookings
                .Where(booking => bookingIds.Contains(booking.Id))
                .Select(booking => booking.TotalTableCost)
                .DefaultIfEmpty(0)
                .SumAsync();

            var fnbRevenue = await _context.FnBOrders
                .Where(order => bookingIds.Contains(order.BookingId))
                .Select(order => order.TotalAmount)
                .DefaultIfEmpty(0)
                .SumAsync();

            var coachingRevenue = await _context.CoachingSessions
                .Where(session => session.BookingId.HasValue && bookingIds.Contains(session.BookingId.Value))
                .Select(session => session.Cost)
                .DefaultIfEmpty(0)
                .SumAsync();

            var total = tableRevenue + fnbRevenue + coachingRevenue;

            decimal ToPercentage(decimal amount)
            {
                if (total <= 0)
                {
                    return 0;
                }

                return Math.Round((amount / total) * 100m, 2);
            }

            return new List<AdminRevenueSourceResponse>
            {
                new()
                {
                    Label = "Tiền giờ chơi",
                    Amount = tableRevenue,
                    Percentage = ToPercentage(tableRevenue)
                },
                new()
                {
                    Label = "Dịch vụ F&B",
                    Amount = fnbRevenue,
                    Percentage = ToPercentage(fnbRevenue)
                },
                new()
                {
                    Label = "Huấn luyện viên",
                    Amount = coachingRevenue,
                    Percentage = ToPercentage(coachingRevenue)
                }
            };
        }

        private static List<AdminRevenueSourceResponse> BuildEmptyRevenueBySource()
        {
            return new List<AdminRevenueSourceResponse>
            {
                new() { Label = "Tiền giờ chơi", Amount = 0, Percentage = 0 },
                new() { Label = "Dịch vụ F&B", Amount = 0, Percentage = 0 },
                new() { Label = "Huấn luyện viên", Amount = 0, Percentage = 0 }
            };
        }

        private async Task<List<AdminRevenuePointResponse>> BuildRevenueSeriesAsync(string period, DateTime? from = null, DateTime? to = null)
        {
            var now = DateTime.UtcNow;
            var paymentQuery = _context.Payments.Where(payment => payment.Status == PaymentStatus.Completed);
            if (from.HasValue) paymentQuery = paymentQuery.Where(p => p.CreatedAt >= from.Value);
            if (to.HasValue) paymentQuery = paymentQuery.Where(p => p.CreatedAt <= to.Value);
            var completedPayments = await paymentQuery.Select(payment => new { payment.CreatedAt, payment.Amount }).ToListAsync();

            var bQuery = _context.Bookings.Where(booking => booking.Status != BookingStatus.Cancelled);
            if (from.HasValue) bQuery = bQuery.Where(b => b.CreatedAt >= from.Value);
            if (to.HasValue) bQuery = bQuery.Where(b => b.CreatedAt <= to.Value);
            var bookingStarts = await bQuery.Select(booking => new { StartsAt = booking.BookingDate.Add(booking.StartTime) }).ToListAsync();

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
                        Revenue = completedPayments
                            .Where(payment => payment.CreatedAt.Date == day.Date)
                            .Sum(payment => payment.Amount),
                        BookingCount = bookingStarts.Count(booking => booking.StartsAt.Date == day.Date)
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
                        Revenue = completedPayments
                            .Where(payment => payment.CreatedAt >= bucketStart && payment.CreatedAt < bucketEnd)
                            .Sum(payment => payment.Amount),
                        BookingCount = bookingStarts.Count(booking => booking.StartsAt >= bucketStart && booking.StartsAt < bucketEnd)
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
                    Revenue = completedPayments
                        .Where(payment => payment.CreatedAt >= currentMonth && payment.CreatedAt < nextMonth)
                        .Sum(payment => payment.Amount),
                    BookingCount = bookingStarts.Count(booking => booking.StartsAt >= currentMonth && booking.StartsAt < nextMonth)
                });
            }

            return points;
        }

        private async Task<List<AdminHeatmapCellResponse>> BuildOccupancyHeatmapAsync(int totalTables)
        {
            var analysisStart = DateTime.UtcNow.Date.AddDays(-27);

            var slots = await _context.BookingSlots
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
                    var occupancyRate = totalTables == 0
                        ? 0
                        : Math.Round((decimal)bookingCount / (totalTables * 2m) * 100m, 2);

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
                    var occupancyRate = totalTables == 0
                        ? 0
                        : Math.Round((decimal)bookingCount / (group.Count() * totalTables * 2m) * 100m, 2);

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

        private static string NormalizePeriod(string period)
        {
            return period?.Trim().ToLowerInvariant() switch
            {
                "day" => "day",
                "week" => "week",
                _ => "month"
            };
        }

        private static int ToMondayBasedDayOfWeek(DayOfWeek dayOfWeek)
        {
            return dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
        }
    }
}
