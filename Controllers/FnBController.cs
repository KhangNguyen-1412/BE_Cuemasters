using System.Threading.Tasks;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FnBController : ControllerBase
    {
        private readonly IFnBService _fnbService;

        public FnBController(IFnBService fnbService)
        {
            _fnbService = fnbService;
        }

        [HttpGet("menu")]
        public async Task<IActionResult> GetMenu()
        {
            var menuItems = await _fnbService.GetMenuItemsAsync();
            return Ok(menuItems);
        }
    }
}
