using System;
using System.Collections.Generic;

namespace BilliardsBooking.API.DTOs
{
    public class CreateBookingRequest
    {
        public int TableId { get; set; }
        public DateTime BookingDate { get; set; }
        public string StartTime { get; set; } = string.Empty; // "14:00"
        public string EndTime { get; set; } = string.Empty;   // "16:00"

        public Guid? CoachId { get; set; }
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
}
