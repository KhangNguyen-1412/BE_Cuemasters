using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoachesController : ControllerBase
    {
        private readonly ICoachService _coachService;

        public CoachesController(ICoachService coachService)
        {
            _coachService = coachService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCoaches()
        {
            var coaches = await _coachService.GetAllCoachesAsync();
            return Ok(coaches);
        }

        [HttpGet("{id}/availability")]
        public async Task<IActionResult> GetCoachAvailability(Guid id, [FromQuery] DateTime date)
        {
            if (date == default)
            {
                return BadRequest(new { Message = "A valid date must be provided." });
            }

            var availability = await _coachService.GetCoachAvailabilityAsync(id, date);
            return Ok(availability);
        }
        [HttpPost("{id}/book")]
        [Authorize]
        public async Task<IActionResult> BookCoach(Guid id, [FromBody] BilliardsBooking.API.DTOs.CreateCoachingSessionRequest request)
        {
            var userIdString = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            request.CoachId = id; // Ensure route id matches

            try
            {
                var sessionId = await _coachService.BookCoachSessionAsync(userId, request);
                return Ok(new { Message = "Coach booked successfully.", SessionId = sessionId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}
