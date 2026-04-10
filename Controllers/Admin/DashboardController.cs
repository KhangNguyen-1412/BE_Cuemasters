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
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<ActionResult<AdminDashboardStatsResponse>> GetStats([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var paymentQuery = _context.Payments.Where(p => p.Status == PaymentStatus.Completed);
            if (from.HasValue) paymentQuery = paymentQuery.Where(p => (p.CompletedAt ?? p.CreatedAt) >= from.Value);
            if (to.HasValue) paymentQuery = paymentQuery.Where(p => (p.CompletedAt ?? p.CreatedAt) <= to.Value);
            var totalRevenue = await paymentQuery.SumAsync(p => p.Amount);

            var activeSessions = await _context.TableSessions.CountAsync(s => s.Status == TableSessionStatus.Active);

            var reservationQuery = _context.Reservations.AsQueryable();
            if (from.HasValue) reservationQuery = reservationQuery.Where(r => r.CreatedAt >= from.Value);
            if (to.HasValue) reservationQuery = reservationQuery.Where(r => r.CreatedAt <= to.Value);

            var walkInQuery = _context.TableSessions.Where(s => s.Type == SessionType.WalkIn);
            if (from.HasValue) walkInQuery = walkInQuery.Where(s => s.CreatedAt >= from.Value);
            if (to.HasValue) walkInQuery = walkInQuery.Where(s => s.CreatedAt <= to.Value);

            var totalBookings = await reservationQuery.CountAsync() + await walkInQuery.CountAsync();
            var availableTables = await _context.Tables.CountAsync(t =>
                t.IsActive &&
                t.Status == TableManualStatus.Available &&
                t.RealTimeStatus != TableRealTimeStatus.Occupied);
            var totalTables = await _context.Tables.CountAsync(t => t.IsActive);
            var activeCoaches = await _context.Coaches.CountAsync(c => c.IsActive);
            var menuItems = await _context.FnBMenuItems.CountAsync();
            var activeMemberships = await _context.UserMemberships.CountAsync(m => m.IsActive);

            var today = DateTime.UtcNow.Date;
            var noShowsToday = await _context.Reservations.CountAsync(r => r.Status == ReservationStatus.NoShow && r.BookingDate.Date == today);
            var forfeitedDepositsToday = await _context.Payments
                .Where(p => p.Type == PaymentType.ForfeitAdjustment &&
                            p.Status == PaymentStatus.Completed &&
                            (p.CompletedAt ?? p.CreatedAt).Date == today)
                .SumAsync(p => p.Amount);

            return Ok(new AdminDashboardStatsResponse
            {
                Revenue = totalRevenue,
                ActiveSessions = activeSessions,
                TotalBookings = totalBookings,
                AvailableTables = availableTables,
                TotalTables = totalTables,
                ActiveCoaches = activeCoaches,
                MenuItems = menuItems,
                ActiveMemberships = activeMemberships,
                NoShowsToday = noShowsToday,
                ForfeitedDepositsToday = forfeitedDepositsToday
            });
        }
    }
}
