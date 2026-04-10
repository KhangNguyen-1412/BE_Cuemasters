using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Staff")]
    public class SessionsController : ControllerBase
    {
        private readonly ITableSessionService _tableSessionService;

        public SessionsController(ITableSessionService tableSessionService)
        {
            _tableSessionService = tableSessionService;
        }

        [HttpGet("{sessionId:guid}/running-total")]
        public async Task<IActionResult> GetRunningTotal(Guid sessionId)
        {
            var total = await _tableSessionService.GetRunningTotalAsync(sessionId);
            return total == null ? NotFound(new { Message = "Session not found." }) : Ok(total);
        }

        [HttpPost("{sessionId:guid}/coaches/{coachingSessionId:guid}/link")]
        public async Task<IActionResult> LinkCoach(Guid sessionId, Guid coachingSessionId)
        {
            var result = await _tableSessionService.LinkCoachEarlyAsync(sessionId, coachingSessionId);
            return result.Success ? Ok(new { Message = result.Message }) : BadRequest(new { Message = result.Message });
        }

        [HttpPost("{sessionId:guid}/fnb")]
        public async Task<IActionResult> AddFnB(Guid sessionId, [FromBody] AddSessionFnBRequest request)
        {
            var result = await _tableSessionService.AddFnBOrderAsync(sessionId, request.Items);
            return result.Success ? Ok(new { Message = result.Message }) : BadRequest(new { Message = result.Message });
        }
    }
}
