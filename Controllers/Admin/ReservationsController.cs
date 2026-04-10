using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ReservationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IReservationService _reservationService;

        public ReservationsController(AppDbContext context, IReservationService reservationService)
        {
            _context = context;
            _reservationService = reservationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetReservations([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var reservations = await _context.Reservations
                .Include(r => r.TableSession)
                .OrderByDescending(r => r.BookingDate)
                .ThenByDescending(r => r.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReservationResponse
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    RequestedTableType = r.RequestedTableType,
                    BookingDate = r.BookingDate,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    Status = r.Status.ToString(),
                    DepositAmount = r.DepositAmount,
                    DepositForfeited = r.DepositForfeited,
                    CreatedAt = r.CreatedAt,
                    CancelledAt = r.CancelledAt,
                    ConfirmedAt = r.ConfirmedAt,
                    TableSessionId = r.TableSession != null ? r.TableSession.Id : null
                })
                .ToListAsync();

            return Ok(reservations);
        }

        [HttpPost("{reservationId:guid}/refund")]
        public async Task<IActionResult> Refund(Guid reservationId, [FromBody] RefundReservationRequest request)
        {
            var result = await _reservationService.CreateRefundAsync(reservationId, request.Amount, request.Reason);
            return result.Success ? Ok(new { Message = result.Message }) : BadRequest(new { Message = result.Message });
        }
    }
}
