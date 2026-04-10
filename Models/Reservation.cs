using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class Reservation
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }
        public User? User { get; set; }

        public TableType RequestedTableType { get; set; }

        public DateTime BookingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

        public decimal DepositAmount { get; set; }
        public bool DepositForfeited { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CancelledAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }

        [NotMapped]
        public Guid? TableSessionId { get; set; }
        public TableSession? TableSession { get; set; }

        public ICollection<ReservationSlot> Slots { get; set; } = new List<ReservationSlot>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        public byte[]? RowVersion { get; set; }
    }
}
