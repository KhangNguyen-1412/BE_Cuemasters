using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembershipsController : ControllerBase
    {
        private readonly IMembershipService _membershipService;

        public MembershipsController(IMembershipService membershipService)
        {
            _membershipService = membershipService;
        }

        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _membershipService.GetPlansAsync();
            return Ok(plans);
        }

        [Authorize]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyMembership()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var membership = await _membershipService.GetUserMembershipAsync(userId);
            if (membership == null)
            {
                return NotFound(new { Message = "Active membership not found." });
            }

            return Ok(membership);
        }

        [Authorize]
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var result = await _membershipService.SubscribeAsync(userId, request);
            if (result == null)
            {
                return BadRequest(new { Message = "Could not subscribe to the membership plan." });
            }

            return Ok(result);
        }

        [Authorize]
        [HttpPut("my/cancel")]
        public async Task<IActionResult> CancelAutoRenew()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var result = await _membershipService.CancelAutoRenewAsync(userId);
            if (!result)
            {
                return BadRequest(new { Message = "Could not cancel auto-renewal. Membership may not exist or is already canceled." });
            }

            return Ok(new { Message = "Auto-renewal cancelled successfully." });
        }
    }
}
