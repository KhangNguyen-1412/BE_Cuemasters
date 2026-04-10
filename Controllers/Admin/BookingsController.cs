using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class BookingsController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly IReservationService _reservationService;
        private readonly AppDbContext _context;

        public BookingsController(
            IBookingService bookingService,
            IReservationService reservationService,
            AppDbContext context)
        {
            _bookingService = bookingService;
            _reservationService = reservationService;
            _context = context;
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

            return Ok(await _bookingService.GetAdminBookingsAsync(page, pageSize, status, tableId, userId, dateFrom, dateTo));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AdminBookingResponse>> GetBookingById(Guid id)
        {
            var booking = await _bookingService.GetAdminBookingByIdAsync(id);
            return booking == null ? NotFound(new { Message = "Booking not found." }) : Ok(booking);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult> UpdateBookingStatus(Guid id, [FromBody] AdminUpdateBookingStatusRequest request)
        {
            if (!Enum.TryParse<BookingStatus>(request.Status, true, out var newStatus))
            {
                return BadRequest(new { Message = "Invalid booking status." });
            }

            var reservation = await _reservationService.GetReservationAsync(id);
            if (reservation == null)
            {
                return BadRequest(new { Message = "Only online reservations support manual status updates from this endpoint." });
            }

            if (newStatus == BookingStatus.Cancelled && reservation.UserId.HasValue)
            {
                var result = await _reservationService.CancelReservationAsync(id, reservation.UserId.Value);
                return result.Success ? Ok(new { Message = result.Message }) : BadRequest(new { Message = result.Message });
            }

            if (newStatus == BookingStatus.NoShow)
            {
                var result = await _reservationService.MarkNoShowAsync(id);
                return result.Success ? Ok(new { Message = result.Message }) : BadRequest(new { Message = result.Message });
            }

            return BadRequest(new { Message = "This endpoint only supports Cancelled and NoShow after the refactor." });
        }

        [HttpDelete("{id:guid}")]
        public IActionResult DeleteBooking(Guid id)
        {
            return BadRequest(new { Message = "Hard deletion is disabled after the reservation/session/invoice refactor." });
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

        [HttpPut("{id:guid}/checkin")]
        public async Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInRequest request)
        {
            var (success, message) = await _bookingService.CheckInAsync(id, request.TableId);
            return success ? Ok(new { Message = message }) : BadRequest(new { Message = message });
        }

        [HttpGet("pending-checkin")]
        public async Task<IActionResult> GetPendingCheckins([FromQuery] DateTime date)
        {
            if (date == default) date = DateTime.UtcNow.Date;
            return Ok(await _bookingService.GetPendingCheckinsAsync(date));
        }

        [HttpGet("upcoming-warnings")]
        public async Task<IActionResult> GetUpcomingWarnings()
        {
            return Ok(await _bookingService.GetUpcomingWarningsAsync());
        }

        [HttpPut("{id:guid}/link-coach-session")]
        public async Task<IActionResult> LinkCoachSession(Guid id, [FromBody] LinkCoachSessionRequest request)
        {
            var (success, message) = await _bookingService.LinkCoachSessionAsync(id, request.CoachingSessionId);
            return success ? Ok(new { Message = message }) : BadRequest(new { Message = message });
        }

        [HttpGet("{id:guid}/available-coach-sessions")]
        public async Task<IActionResult> GetAvailableCoachSessions(Guid id)
        {
            return Ok(await _bookingService.GetLinkableCoachSessionsAsync(id));
        }

        [HttpPut("{id:guid}/checkout")]
        public async Task<IActionResult> CheckOut(Guid id, [FromBody] CheckoutRequest? request)
        {
            var paymentMethod = request?.PaymentMethod ?? "Cash";
            var (success, message, summary) = await _bookingService.CheckOutAsync(id, paymentMethod);
            return success ? Ok(new { Message = message, Summary = summary }) : BadRequest(new { Message = message });
        }
    }
}
