using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly BilliardsBooking.API.Services.IBookingService _bookingService;

        public BookingsController(AppDbContext context, BilliardsBooking.API.Services.IBookingService bookingService)
        {
            _context = context;
            _bookingService = bookingService;
        }

        [HttpGet]
        public async Task<ActionResult<AdminBookingListResponse>> GetAllBookings(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] int? tableId = null,
            [FromQuery] string? userId = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            if (page < 1 || pageSize < 1)
            {
                return BadRequest(new { Message = "Page and pageSize must be greater than zero." });
            }

            var query = _context.Bookings
                .Include(booking => booking.User)
                .Include(booking => booking.Table)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<BookingStatus>(status, true, out var parsedStatus))
                {
                    return BadRequest(new { Message = "Invalid booking status filter." });
                }

                query = query.Where(booking => booking.Status == parsedStatus);
            }

            if (tableId.HasValue)
            {
                query = query.Where(booking => booking.TableId == tableId.Value);
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                if (!Guid.TryParse(userId, out var parsedUserId))
                {
                    return BadRequest(new { Message = "Invalid user id filter." });
                }

                query = query.Where(booking => booking.UserId == parsedUserId);
            }

            if (dateFrom.HasValue)
            {
                query = query.Where(booking => booking.BookingDate >= dateFrom.Value.Date);
            }

            if (dateTo.HasValue)
            {
                query = query.Where(booking => booking.BookingDate <= dateTo.Value.Date);
            }

            var totalItems = await query.CountAsync();
            var bookings = await query
                .OrderByDescending(booking => booking.BookingDate)
                .ThenByDescending(booking => booking.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = await BuildBookingResponsesAsync(bookings);

            return Ok(new AdminBookingListResponse
            {
                Items = items,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AdminBookingResponse>> GetBookingById(Guid id)
        {
            var booking = await _context.Bookings
                .Include(item => item.User)
                .Include(item => item.Table)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (booking == null)
            {
                return NotFound(new { Message = "Booking not found." });
            }

            var response = await BuildBookingResponsesAsync(new List<Booking> { booking });
            return Ok(response.First());
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AdminBookingResponse>> UpdateBookingStatus(Guid id, [FromBody] AdminUpdateBookingStatusRequest request)
        {
            if (!Enum.TryParse<BookingStatus>(request.Status, true, out var newStatus))
            {
                return BadRequest(new { Message = "Invalid booking status." });
            }

            var booking = await _context.Bookings
                .Include(item => item.User)
                .Include(item => item.Table)
                .Include(item => item.Slots)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (booking == null)
            {
                return NotFound(new { Message = "Booking not found." });
            }

            if (booking.Status == BookingStatus.Cancelled && newStatus != BookingStatus.Cancelled)
            {
                return BadRequest(new { Message = "Cancelled bookings cannot be reactivated from this endpoint." });
            }

            booking.Status = newStatus;

            if (newStatus == BookingStatus.Cancelled)
            {
                booking.CancelledAt ??= DateTime.UtcNow;

                foreach (var slot in booking.Slots)
                {
                    slot.IsActive = false;
                }
            }

            await _context.SaveChangesAsync();

            var response = await BuildBookingResponsesAsync(new List<Booking> { booking });
            return Ok(response.First());
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteBooking(Guid id)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(item => item.Id == id);
            if (booking == null)
            {
                return NotFound(new { Message = "Booking not found." });
            }

            var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var orderIds = await _context.FnBOrders
                    .Where(order => order.BookingId == id)
                    .Select(order => order.Id)
                    .ToListAsync();

                if (orderIds.Count > 0)
                {
                    var orderItems = await _context.FnBOrderItems
                        .Where(orderItem => orderIds.Contains(orderItem.FnBOrderId))
                        .ToListAsync();

                    _context.FnBOrderItems.RemoveRange(orderItems);

                    var orders = await _context.FnBOrders
                        .Where(order => orderIds.Contains(order.Id))
                        .ToListAsync();

                    _context.FnBOrders.RemoveRange(orders);
                }

                var payments = await _context.Payments
                    .Where(payment => payment.BookingId == id)
                    .ToListAsync();
                _context.Payments.RemoveRange(payments);

                var coachingSessions = await _context.CoachingSessions
                    .Where(session => session.BookingId == id)
                    .ToListAsync();
                _context.CoachingSessions.RemoveRange(coachingSessions);

                _context.Bookings.Remove(booking);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return NoContent();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<List<AdminBookingResponse>> BuildBookingResponsesAsync(List<Booking> bookings)
        {
            if (bookings.Count == 0)
            {
                return new List<AdminBookingResponse>();
            }

            var bookingIds = bookings.Select(booking => booking.Id).ToList();

            var paymentMap = await _context.Payments
                .Where(payment => payment.BookingId.HasValue && bookingIds.Contains(payment.BookingId.Value))
                .OrderByDescending(payment => payment.CreatedAt)
                .ToListAsync();

            var fnbTotals = await _context.FnBOrders
                .Where(order => bookingIds.Contains(order.BookingId))
                .GroupBy(order => order.BookingId)
                .Select(group => new { BookingId = group.Key, Total = group.Sum(order => order.TotalAmount) })
                .ToListAsync();

            var coachingSessions = await _context.CoachingSessions
                .Where(session => session.BookingId.HasValue && bookingIds.Contains(session.BookingId.Value))
                .ToListAsync();

            var coachIds = coachingSessions.Select(session => session.CoachId).Distinct().ToList();

            var coachNames = await _context.Coaches
                .Include(coach => coach.User)
                .Where(coach => coachIds.Contains(coach.Id))
                .ToDictionaryAsync(coach => coach.Id, coach => coach.User != null ? coach.User.FullName : coach.Id.ToString());

            var latestPaymentByBookingId = paymentMap
                .Where(payment => payment.BookingId.HasValue)
                .GroupBy(payment => payment.BookingId!.Value)
                .ToDictionary(group => group.Key, group => group.First());

            var fnbTotalByBookingId = fnbTotals.ToDictionary(item => item.BookingId, item => item.Total);
            var coachingByBookingId = coachingSessions
                .Where(session => session.BookingId.HasValue)
                .ToDictionary(session => session.BookingId!.Value, session => session);

            return bookings.Select(booking =>
            {
                latestPaymentByBookingId.TryGetValue(booking.Id, out var payment);
                fnbTotalByBookingId.TryGetValue(booking.Id, out var fnbTotal);
                coachingByBookingId.TryGetValue(booking.Id, out var coachingSession);

                var coachingTotal = coachingSession?.Cost ?? 0;
                var coachName = coachingSession != null && coachNames.TryGetValue(coachingSession.CoachId, out var foundCoachName)
                    ? foundCoachName
                    : null;

                return new AdminBookingResponse
                {
                    Id = booking.Id.ToString(),
                    UserId = booking.UserId.ToString(),
                    UserFullName = booking.User?.FullName ?? string.Empty,
                    UserEmail = booking.User?.Email ?? string.Empty,
                    TableId = booking.TableId,
                    TableNumber = booking.Table?.TableNumber ?? string.Empty,
                    BookingDate = booking.BookingDate,
                    StartTime = booking.BookingDate.Add(booking.StartTime),
                    EndTime = booking.BookingDate.Add(booking.EndTime),
                    TotalPrice = booking.TotalTableCost + fnbTotal + coachingTotal,
                    DiscountAmount = booking.DiscountAmount,
                    FnBTotal = fnbTotal,
                    CoachingTotal = coachingTotal,
                    PaymentAmount = payment?.Amount ?? 0,
                    PaymentStatus = payment?.Status.ToString(),
                    Status = booking.Status.ToString(),
                    CreatedAt = booking.CreatedAt,
                    CancelledAt = booking.CancelledAt,
                    CoachName = coachName
                };
            }).ToList();
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking(AdminCreateBookingRequest request)
        {
            var (success, message, bookingId) = await _bookingService.CreateBookingAsync(request.UserId, request);
            if (!success)
            {
                return BadRequest(new { Message = message });
            }
            return CreatedAtAction(nameof(GetBookingById), new { id = bookingId }, new { Id = bookingId, Message = message });
        }
    }
}
