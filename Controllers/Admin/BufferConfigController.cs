using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
using BilliardsBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/buffer-configs")]
    [Authorize(Roles = "Admin")]
    public class BufferConfigController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;

        public BufferConfigController(AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _context.BufferConfigs
                .OrderBy(c => c.TableType)
                .ThenBy(c => c.DayOfWeek)
                .Select(c => new BufferConfigResponse
                {
                    Id = c.Id,
                    TableType = c.TableType.ToString(),
                    DayOfWeek = c.DayOfWeek,
                    TimeFrom = c.TimeFrom,
                    TimeTo = c.TimeTo,
                    BufferCount = c.BufferCount,
                    IsActive = c.IsActive
                })
                .ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UpsertBufferConfigRequest request)
        {
            if (!Enum.TryParse<TableType>(request.TableType, true, out var tableType))
            {
                return BadRequest(new { Message = "Invalid table type." });
            }

            var config = new BufferConfig
            {
                TableType = tableType,
                DayOfWeek = request.DayOfWeek,
                TimeFrom = request.TimeFrom,
                TimeTo = request.TimeTo,
                BufferCount = request.BufferCount,
                IsActive = request.IsActive
            };

            _context.BufferConfigs.Add(config);
            await _auditService.LogAsync(
                AuditAction.BufferConfigChanged,
                nameof(BufferConfig),
                config.Id.ToString(),
                newValue: new { config.TableType, config.DayOfWeek, config.TimeFrom, config.TimeTo, config.BufferCount, config.IsActive },
                notes: "Buffer config created.");
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Buffer config created.", Id = config.Id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpsertBufferConfigRequest request)
        {
            if (!Enum.TryParse<TableType>(request.TableType, true, out var tableType))
            {
                return BadRequest(new { Message = "Invalid table type." });
            }

            var config = await _context.BufferConfigs.FindAsync(id);
            if (config == null)
            {
                return NotFound(new { Message = "Buffer config not found." });
            }

            var previous = new { config.TableType, config.DayOfWeek, config.TimeFrom, config.TimeTo, config.BufferCount, config.IsActive };
            config.TableType = tableType;
            config.DayOfWeek = request.DayOfWeek;
            config.TimeFrom = request.TimeFrom;
            config.TimeTo = request.TimeTo;
            config.BufferCount = request.BufferCount;
            config.IsActive = request.IsActive;

            await _auditService.LogAsync(
                AuditAction.BufferConfigChanged,
                nameof(BufferConfig),
                config.Id.ToString(),
                previousValue: previous,
                newValue: new { config.TableType, config.DayOfWeek, config.TimeFrom, config.TimeTo, config.BufferCount, config.IsActive },
                notes: "Buffer config updated.");
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Buffer config updated." });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var config = await _context.BufferConfigs.FindAsync(id);
            if (config == null)
            {
                return NotFound(new { Message = "Buffer config not found." });
            }

            await _auditService.LogAsync(
                AuditAction.BufferConfigChanged,
                nameof(BufferConfig),
                config.Id.ToString(),
                previousValue: new { config.TableType, config.DayOfWeek, config.TimeFrom, config.TimeTo, config.BufferCount, config.IsActive },
                notes: "Buffer config deleted.");
            _context.BufferConfigs.Remove(config);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Buffer config deleted." });
        }
    }
}
