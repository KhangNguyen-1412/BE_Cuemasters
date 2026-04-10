using BilliardsBooking.API.Data;
using BilliardsBooking.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Services
{
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
            var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();

            var now = DateTime.UtcNow;
            var graceCutoff = now - GracePeriod;

            var reservations = await context.Reservations
                .Where(r => r.Status == ReservationStatus.Confirmed &&
                            (r.BookingDate < graceCutoff.Date ||
                             (r.BookingDate == graceCutoff.Date && r.StartTime <= graceCutoff.TimeOfDay)))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            if (reservations.Count == 0) return;

            _logger.LogInformation("Found {Count} no-show reservations to process.", reservations.Count);
            foreach (var reservationId in reservations)
            {
                var result = await reservationService.MarkNoShowAsync(reservationId);
                if (!result.Success)
                {
                    _logger.LogWarning("Failed to mark reservation {ReservationId} as no-show: {Message}", reservationId, result.Message);
                }
            }
        }
    }
}
