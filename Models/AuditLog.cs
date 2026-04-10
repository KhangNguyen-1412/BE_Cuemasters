using System;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class AuditLog
    {
        public long Id { get; set; }
        public AuditAction Action { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string? ActorUserId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorRole { get; set; }
        public string? PreviousValue { get; set; }
        public string? NewValue { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
