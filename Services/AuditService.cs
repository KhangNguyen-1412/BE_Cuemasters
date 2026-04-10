using System.Text.Json;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Services
{
    public interface IAuditService
    {
        Task LogAsync(
            AuditAction action,
            string entityType,
            string entityId,
            string? actorUserId = null,
            object? previousValue = null,
            object? newValue = null,
            string? notes = null);
    }

    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;

        public AuditService(AppDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(
            AuditAction action,
            string entityType,
            string entityId,
            string? actorUserId = null,
            object? previousValue = null,
            object? newValue = null,
            string? notes = null)
        {
            User? actor = null;
            if (!string.IsNullOrWhiteSpace(actorUserId) && Guid.TryParse(actorUserId, out var actorId))
            {
                actor = await _context.Users.FindAsync(actorId);
            }

            _context.AuditLogs.Add(new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                ActorUserId = actorUserId,
                ActorName = actor?.FullName,
                ActorRole = actor?.Role.ToString(),
                PreviousValue = previousValue == null ? null : JsonSerializer.Serialize(previousValue),
                NewValue = newValue == null ? null : JsonSerializer.Serialize(newValue),
                Notes = notes
            });
        }
    }
}
