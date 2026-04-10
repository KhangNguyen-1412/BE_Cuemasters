using System;
using System.Collections.Generic;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class Invoice
    {
        public Guid Id { get; set; }

        public Guid TableSessionId { get; set; }
        public TableSession? TableSession { get; set; }

        public Guid? UserId { get; set; }
        public User? User { get; set; }
        public string? GuestName { get; set; }

        public int TableId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public TableType TableType { get; set; }

        public DateTime ServiceDate { get; set; }
        public DateTime ServiceStartedAt { get; set; }
        public DateTime ServiceEndedAt { get; set; }
        public DateTime? PaymentCompletedAt { get; set; }
        public decimal SessionDurationHours { get; set; }

        public decimal TableHourlyRateSnapshot { get; set; }
        public decimal TableTimeCost { get; set; }
        public decimal FnBTotal { get; set; }
        public decimal CoachingTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal MembershipDiscountPercent { get; set; }
        public string? MembershipTierSnapshot { get; set; }
        public decimal DepositApplied { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal BalanceDue { get; set; }

        public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
