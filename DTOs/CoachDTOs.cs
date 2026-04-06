using System.Collections.Generic;

namespace BilliardsBooking.API.DTOs
{
    public class CoachResponse
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public decimal HourlyRate { get; set; }
        public decimal Rating { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public class CoachAvailabilitySlotResponse
    {
        public System.DateTime StartTime { get; set; }
        public System.DateTime EndTime { get; set; }
        public bool IsAvailable { get; set; }
    }
}
