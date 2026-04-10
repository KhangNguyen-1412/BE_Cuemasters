using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FloorPlanController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FloorPlanController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("snapshot")]
        public async Task<ActionResult<FloorPlanSnapshotResponse>> GetSnapshot([FromQuery] DateTime date)
        {
            var tables = await _context.Tables
                .Where(t => t.IsActive)
                .Select(t => new TableSnapshotDto
                {
                    TableId = t.Id,
                    TableNumber = t.TableNumber,
                    Type = t.Type.ToString(),
                    RealTimeStatus = t.Status == TableManualStatus.Maintenance
                        ? TableRealTimeStatus.Maintenance.ToString()
                        : (t.RealTimeStatus == TableRealTimeStatus.Occupied ||
                           _context.TableSessions.Any(s => s.TableId == t.Id && s.Status == TableSessionStatus.Active))
                            ? TableRealTimeStatus.Occupied.ToString()
                            : t.RealTimeStatus.ToString(),
                    ActiveSessionId = _context.TableSessions
                        .Where(s => s.TableId == t.Id && s.Status == TableSessionStatus.Active)
                        .Select(s => (Guid?)s.Id)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new FloorPlanSnapshotResponse
            {
                Date = date.Date,
                GeneratedAt = DateTime.UtcNow,
                Tables = tables
            });
        }
    }
}
