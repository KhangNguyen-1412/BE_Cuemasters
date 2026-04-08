using System;
using System.Collections.Generic;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.DTOs
{
    public class BookingResponse
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        // Null until an admin assigns a physical table at check-in (online reservations).
        public int? TableId { get; set; }
        // Null until assigned — frontend shows "{RequestedTableType} (assigned on arrival)".
        public string? TableName { get; set; }
        // The category the customer reserved.
        public TableType RequestedTableType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string BookingType { get; set; } = "Online";
        public decimal DepositAmount { get; set; }
        public bool DepositForfeited { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckedOutAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public decimal? ActualCost { get; set; }
        public string? GuestName { get; set; }
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

    public class CreateCoachingSessionRequest
    {
        public Guid CoachId { get; set; }
        public DateTime SessionDate { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
    }
}
