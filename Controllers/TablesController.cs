using System;
using System.Linq;
using System.Threading.Tasks;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TablesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly BilliardsBooking.API.Services.ITableService _tableService;

        public TablesController(AppDbContext context, BilliardsBooking.API.Services.ITableService tableService)
        {
            _context = context;
            _tableService = tableService;
        }

        [HttpGet("{id}/availability")]
        public async Task<IActionResult> GetTableAvailability(int id, [FromQuery] DateTime date)
        {
            if (date == default)
            {
                return BadRequest(new { Message = "A valid date must be provided." });
            }

            var availability = await _tableService.GetTableAvailabilityAsync(id, date);
            if (availability == null)
            {
                return NotFound(new { Message = "Table not found or not available." });
            }

            return Ok(availability);
        }

        [HttpGet]
        public async Task<IActionResult> GetTables()
        {
            var now = DateTime.UtcNow.TimeOfDay;
            var today = DateTime.UtcNow.Date;

            // Simple active tables retrieval
            var tables = await _context.Tables
                .Where(t => t.IsActive)
                .Select(t => new
                {
                    t.Id,
                    t.TableNumber,
                    t.Type,
                    t.HourlyRate,
                    t.PositionX,
                    t.PositionY,
                    t.Status, // Base status (Maintenance/Available)
                    // We can augment this dynamically based on any active slots right now
                    ActiveBookingsCount = _context.BookingSlots.Count(bs => bs.TableId == t.Id && bs.SlotDate == today && bs.IsActive && bs.SlotStart <= now && bs.SlotStart >= now.Subtract(TimeSpan.FromMinutes(30)))
                })
                .ToListAsync();

            var result = tables.Select(t => new
            {
                t.Id,
                t.TableNumber,
                Type = t.Type.ToString(),
                t.HourlyRate,
                t.PositionX,
                t.PositionY,
                // Override computed status if someone is currently playing
                Status = t.Status == TableManualStatus.Maintenance ? "Maintenance" : (t.ActiveBookingsCount > 0 ? "InUse" : "Available")
            });

            return Ok(result);
        }
    }
}
