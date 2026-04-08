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
            if (from.HasValue) paymentQuery = paymentQuery.Where(p => p.CreatedAt >= from.Value);
            if (to.HasValue) paymentQuery = paymentQuery.Where(p => p.CreatedAt <= to.Value);
            var totalRevenue = await paymentQuery.SumAsync(p => p.Amount);

            var activeSessions = await _context.Bookings
                .Where(b => b.Status == BookingStatus.InProgress || b.Status == BookingStatus.Confirmed)
                .CountAsync();

            var bookingQuery = _context.Bookings.AsQueryable();
            if (from.HasValue) bookingQuery = bookingQuery.Where(b => b.CreatedAt >= from.Value);
            if (to.HasValue) bookingQuery = bookingQuery.Where(b => b.CreatedAt <= to.Value);
            var totalBookings = await bookingQuery.CountAsync();
            var availableTables = await _context.Tables
                .Where(t => t.IsActive && t.Status == TableManualStatus.Available)
                .CountAsync();
            var totalTables = await _context.Tables.CountAsync(t => t.IsActive);
            var activeCoaches = await _context.Coaches.CountAsync(c => c.IsActive);
            var menuItems = await _context.FnBMenuItems.CountAsync();
            var activeMemberships = await _context.UserMemberships.CountAsync(m => m.IsActive);

            // No-show KPIs
            var today = DateTime.UtcNow.Date;
            var noShowsToday = await _context.Bookings
                .Where(b => b.Status == BookingStatus.NoShow && b.BookingDate.Date == today)
                .CountAsync();
            var forfeitedDepositsToday = await _context.Bookings
                .Where(b => b.Status == BookingStatus.NoShow && b.DepositForfeited && b.BookingDate.Date == today)
                .SumAsync(b => b.DepositAmount);

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
