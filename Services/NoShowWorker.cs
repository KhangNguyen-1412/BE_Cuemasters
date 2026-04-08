using BilliardsBooking.API.Data;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Services
{
    /// <summary>
    /// Background job that runs every 5 minutes to auto-cancel no-show bookings.
    /// A no-show is a Confirmed booking where:
    ///   - BookingDate + StartTime + 15 min grace period &lt; UtcNow
    ///   - CheckedInAt is still null (customer never arrived)
    /// Actions taken:
    ///   - Status → NoShow
    ///   - DepositForfeited → true (owner keeps the deposit)
    ///   - All BookingSlots released (IsActive = false) → frees category capacity
    ///   - SignalR notification sent so the floor plan refreshes availability
    /// NOTE: CoachingSessions are independent entities and are NOT cascaded — a coach
    /// no-show is a separate concern tracked on the session itself.
    /// </summary>
    public class NoShowWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NoShowWorker> _logger;
        private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

        public NoShowWorker(IServiceScopeFactory scopeFactory, ILogger<NoShowWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NoShowWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNoShowsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing no-shows.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }

            _logger.LogInformation("NoShowWorker stopped.");
        }

        private async Task ProcessNoShowsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<TableStatusHub>>();

            var now = DateTime.UtcNow;
            var graceCutoff = now - GracePeriod;

            // Find all confirmed bookings past their grace period with no check-in
            var noShowBookings = await context.Bookings
                .Include(b => b.Slots)
                .Where(b =>
                    b.Status == BookingStatus.Confirmed &&
                    b.CheckedInAt == null &&
                    (b.BookingDate < graceCutoff.Date ||
                     (b.BookingDate == graceCutoff.Date && b.StartTime <= graceCutoff.TimeOfDay)))
                .ToListAsync(cancellationToken);

            if (noShowBookings.Count == 0) return;

            _logger.LogInformation("Found {Count} no-show bookings to process.", noShowBookings.Count);

            foreach (var booking in noShowBookings)
            {
                booking.Status = BookingStatus.NoShow;
                booking.DepositForfeited = true;

                // Release all booking slots → category capacity recovered.
                foreach (var slot in booking.Slots)
                {
                    slot.IsActive = false;
                }

                _logger.LogInformation(
                    "Booking {BookingId} marked as NoShow. Category {Category}, Deposit {Deposit} forfeited.",
                    booking.Id, booking.RequestedTableType, booking.DepositAmount);
            }

            await context.SaveChangesAsync(cancellationToken);

            // SignalR: notify floor plan that category capacity freed.
            var affectedCategories = noShowBookings.Select(b => b.RequestedTableType).Distinct();
            var today = now.Date;
            foreach (var category in affectedCategories)
            {
                await hubContext.Clients
                    .Group($"floorplan-{today:yyyy-MM-dd}")
                    .SendAsync("CategoryCapacityChanged", category.ToString(), today, cancellationToken);
            }
        }
    }
}
