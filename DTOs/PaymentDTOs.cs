using System;

namespace BilliardsBooking.API.DTOs
{
    public class CreatePaymentRequest
    {
        public string BookingId { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty; // Cash, CreditCard, EWallet
    }

    public class PaymentResponse
    {
        public string Id { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty; // Cash, CreditCard, EWallet
        public string Status { get; set; } = string.Empty; // Pending, Completed, Failed, Refunded
        public string? TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
