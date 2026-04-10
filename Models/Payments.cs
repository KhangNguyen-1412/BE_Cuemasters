using System;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class Payment
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }
        public User? User { get; set; }

        public Guid? BookingId { get; set; }
        public Booking? Booking { get; set; }

        public Guid? ReservationId { get; set; }
        public Reservation? Reservation { get; set; }

        public Guid? InvoiceId { get; set; }
        public Invoice? Invoice { get; set; }

        public Guid? UserMembershipId { get; set; }
        public UserMembership? UserMembership { get; set; }

        public PaymentType Type { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; }
        public PaymentStatus Status { get; set; }

        public string? TransactionId { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
