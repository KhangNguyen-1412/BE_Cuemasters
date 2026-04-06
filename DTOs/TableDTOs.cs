using System.Collections.Generic;

namespace BilliardsBooking.API.DTOs
{
    public class TableResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PricePerHour { get; set; }
        public string Type { get; set; } = string.Empty; // Pool, Carom, Snooker
        public string Status { get; set; } = string.Empty; // Available, Occupied, Maintenance
        public string? ImageUrl { get; set; }
    }

    public class TableAvailabilitySlotResponse
    {
        public System.DateTime StartTime { get; set; }
        public System.DateTime EndTime { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class TableAvailabilityResponse
    {
        public int TableId { get; set; }
        public System.DateTime Date { get; set; }
        public List<TableAvailabilitySlotResponse> Slots { get; set; } = new();
    }
}
