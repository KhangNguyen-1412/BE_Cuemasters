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
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    public class CoachAvailabilityResponse
    {
        public int Id { get; set; }
        public int DayOfWeek { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsBlocked { get; set; }
        public string? SpecificDate { get; set; }
    }

    public class UpsertCoachAvailabilityRequest
    {
        public int DayOfWeek { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public bool IsBlocked { get; set; }
        public DateTime? SpecificDate { get; set; }
    }
}
