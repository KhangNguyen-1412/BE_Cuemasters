using BilliardsBooking.API.Data;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TablesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITableService _tableService;

        public TablesController(AppDbContext context, ITableService tableService)
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
            return availability == null
                ? NotFound(new { Message = "Table not found or not available." })
                : Ok(availability);
        }

        [HttpGet]
        public async Task<IActionResult> GetTables()
        {
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
                    ManualStatus = t.Status,
                    RealTimeStatus = t.RealTimeStatus,
                    ActiveSessionId = _context.TableSessions
                        .Where(s => s.TableId == t.Id && s.Status == TableSessionStatus.Active)
                        .Select(s => (Guid?)s.Id)
                        .FirstOrDefault()
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
                Status = t.ManualStatus == TableManualStatus.Maintenance
                    ? "Maintenance"
                    : t.RealTimeStatus == TableRealTimeStatus.Occupied || t.ActiveSessionId != null
                        ? "InUse"
                        : "Available"
            });

            return Ok(result);
        }
    }
}
