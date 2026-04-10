using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class TableSession
    {
        public Guid Id { get; set; }

        public int TableId { get; set; }
        public BilliardTable? Table { get; set; }

        public Guid? ReservationId { get; set; }
        public Reservation? Reservation { get; set; }

        public Guid? UserId { get; set; }
        public User? User { get; set; }
        public string? GuestName { get; set; }

        public SessionType Type { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        public TableSessionStatus Status { get; set; } = TableSessionStatus.Active;

        public ICollection<FnBOrder> FnBOrders { get; set; } = new List<FnBOrder>();
        public ICollection<CoachingSession> CoachingSessions { get; set; } = new List<CoachingSession>();

        [NotMapped]
        public Guid? InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public byte[]? RowVersion { get; set; }
    }
}
