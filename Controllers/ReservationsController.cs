using System.Security.Claims;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReservationsController : ControllerBase
    {
        private readonly IReservationService _reservationService;

        public ReservationsController(IReservationService reservationService)
        {
            _reservationService = reservationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyReservations([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            return Ok(await _reservationService.GetReservationsForUserAsync(userId, page, pageSize, status));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetReservation(Guid id)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var reservation = await _reservationService.GetReservationResponseAsync(id, userId);
            return reservation == null ? NotFound(new { Message = "Reservation not found." }) : Ok(reservation);
        }

        [HttpGet("category-availability")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategoryAvailability([FromQuery] TableType tableType, [FromQuery] DateTime date)
        {
            return Ok(await _reservationService.GetCategoryAvailabilityAsync(tableType, date));
        }

        [HttpPost]
        public async Task<IActionResult> CreateReservation([FromBody] CreateBookingRequest request)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _reservationService.CreateReservationAsync(userId, request, userId.ToString());
            return result.Success
                ? Ok(new { Message = result.Message, ReservationId = result.ReservationId })
                : Conflict(new { Message = result.Message });
        }

        [HttpPut("{id:guid}/cancel")]
        public async Task<IActionResult> CancelReservation(Guid id)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _reservationService.CancelReservationAsync(id, userId, userId.ToString());
            return result.Success ? Ok(new { Message = result.Message }) : BadRequest(new { Message = result.Message });
        }

        [HttpPut("{id:guid}/reschedule")]
        public async Task<IActionResult> RescheduleReservation(Guid id, [FromBody] RescheduleRequest request)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _reservationService.RescheduleReservationAsync(id, userId, request, userId.ToString());
            return result.Success ? Ok(new { Message = result.Message }) : BadRequest(new { Message = result.Message });
        }

        private bool TryGetUserId(out Guid userId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdString, out userId);
        }
    }
}
