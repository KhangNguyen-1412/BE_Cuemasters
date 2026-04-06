using System;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class Payment
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public User? User { get; set; }

        public Guid? BookingId { get; set; }
        public Booking? Booking { get; set; }

        public Guid? UserMembershipId { get; set; }
        public UserMembership? UserMembership { get; set; }

        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; }
        public PaymentStatus Status { get; set; }

        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
