using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
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

        [HttpPost("{sessionId:guid}/transfer-table")]
        public async Task<IActionResult> TransferTable(Guid sessionId, [FromBody] TransferTableRequest request)
        {
            var result = await _tableSessionService.TransferTableAsync(sessionId, request.NewTableId, request.Reason);
            return result.Success ? Ok(new { Message = result.Message }) : BadRequest(new { Message = result.Message });
        }

        [HttpPost("{sessionId:guid}/extend")]
        public async Task<IActionResult> Extend(Guid sessionId, [FromBody] ExtendSessionRequest request)
        {
            var result = await _tableSessionService.ExtendSessionAsync(sessionId, request.AdditionalMinutes);
            return result.Success ? Ok(new { Message = result.Message }) : BadRequest(new { Message = result.Message });
        }

        [HttpGet("{sessionId:guid}/interim-bill")]
        public async Task<IActionResult> InterimBill(Guid sessionId)
        {
            var bill = await _tableSessionService.GetInterimBillAsync(sessionId);
            return bill == null ? NotFound(new { Message = "Session not found." }) : Ok(bill);
        }
    }
}
