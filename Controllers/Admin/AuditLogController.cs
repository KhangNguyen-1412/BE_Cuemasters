using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/audit-logs")]
    [Authorize(Roles = "Admin")]
    public class AuditLogController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditLogController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs(
            [FromQuery] string? entityType,
            [FromQuery] string? entityId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var query = _context.AuditLogs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(log => log.EntityType == entityType);
            if (!string.IsNullOrWhiteSpace(entityId)) query = query.Where(log => log.EntityId == entityId);
            if (from.HasValue) query = query.Where(log => log.CreatedAt >= from.Value);
            if (to.HasValue) query = query.Where(log => log.CreatedAt <= to.Value);

            var logs = await query
                .OrderByDescending(log => log.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(log => new AuditLogResponse
                {
                    Id = log.Id,
                    Action = log.Action.ToString(),
                    EntityType = log.EntityType,
                    EntityId = log.EntityId,
                    ActorUserId = log.ActorUserId,
                    ActorName = log.ActorName,
                    ActorRole = log.ActorRole,
                    PreviousValue = log.PreviousValue,
                    NewValue = log.NewValue,
                    Notes = log.Notes,
                    CreatedAt = log.CreatedAt
                })
                .ToListAsync();

            return Ok(logs);
        }
    }
}
