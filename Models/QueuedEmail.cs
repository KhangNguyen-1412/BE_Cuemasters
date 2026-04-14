using System;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class QueuedEmail
    {
        public long Id { get; set; }
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public QueuedEmailStatus Status { get; set; } = QueuedEmailStatus.Pending;
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public DateTime? NextAttemptAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastAttemptAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
