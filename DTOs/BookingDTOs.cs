using System;
using System.Collections.Generic;

namespace BilliardsBooking.API.DTOs
{
    public class BookingResponse
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int TableId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty; // Pending, Confirmed, Cancelled, Completed
        public string? Notes { get; set; }
        public PaymentResponse? Payment { get; set; }
        public CoachResponse? Coach { get; set; }
        public List<FnBOrderResponse> FnBOrders { get; set; } = new();
    }

    public class FnBOrderResponse
    {
        public string Id { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class BookingListResponse
    {
        public List<BookingResponse> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class RescheduleRequest
    {
        public DateTime NewStartTime { get; set; }
        public DateTime NewEndTime { get; set; }
    }
}
