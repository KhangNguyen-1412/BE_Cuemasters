using System.Threading.Tasks;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            var result = await _paymentService.CreatePaymentAsync(request);
            if (result == null)
            {
                return BadRequest(new { Message = "Could not create payment. Booking might be invalid or already paid." });
            }

            return Ok(result);
        }
    }
}
