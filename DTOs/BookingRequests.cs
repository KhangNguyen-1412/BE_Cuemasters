using System;
using System.Collections.Generic;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.DTOs
{
    public class CreateBookingRequest
    {
        // Category-level reservation — no specific TableId from the customer.
        public TableType RequestedTableType { get; set; }
        public DateTime BookingDate { get; set; }
        public string StartTime { get; set; } = string.Empty; // "14:00"
        public string EndTime { get; set; } = string.Empty;   // "16:00"

        public List<FnBOrderRequest> FnBOrders { get; set; } = new();
    }

    public class FnBOrderRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class CompleteSessionRequest
    {
        public string? Notes { get; set; }
    }

    public class CategoryAvailabilitySlot
    {
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public int Available { get; set; }
        public int Capacity { get; set; }
    }

    public class CategoryAvailabilityResponse
    {
        public TableType TableType { get; set; }
        public DateTime Date { get; set; }
        public int TotalTables { get; set; }
        public int BufferSize { get; set; }
        public int OnlineCapacity { get; set; }
        public List<CategoryAvailabilitySlot> Slots { get; set; } = new();
    }
}
