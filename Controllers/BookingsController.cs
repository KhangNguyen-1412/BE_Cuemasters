using System;
using System.Security.Claims;
using System.Threading.Tasks;
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
    public class BookingsController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingsController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyBookings([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var result = await _bookingService.GetBookingsForUserAsync(userId, page, pageSize, status);
            return Ok(result);
        }

        [HttpGet("category-availability")]
        public async Task<IActionResult> GetCategoryAvailability([FromQuery] TableType tableType, [FromQuery] DateTime date)
        {
            var result = await _bookingService.GetCategoryAvailabilityAsync(tableType, date);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBooking(Guid id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var booking = await _bookingService.GetBookingByIdAsync(id);
            if (booking == null)
            {
                return NotFound(new { Message = "Booking not found." });
            }

            return Ok(booking);
        }

        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelBooking(Guid id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var result = await _bookingService.CancelBookingAsync(id, userId);

            if (!result.Success)
            {
                return BadRequest(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message });
        }

        [HttpPut("{id}/reschedule")]
        public async Task<IActionResult> RescheduleBooking(Guid id, [FromBody] RescheduleRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var result = await _bookingService.RescheduleBookingAsync(id, userId, request);

            if (!result.Success)
            {
                return BadRequest(new { Message = result.Message });
            }

            return Ok(new { Message = result.Message });
        }

        [HttpPost]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var result = await _bookingService.CreateBookingAsync(userId, request);

            if (!result.Success)
            {
                return Conflict(new { Message = result.Message });
            }

            return Ok(new { Message = "Booking successfully established.", BookingId = result.BookingId });
        }
    }
}
