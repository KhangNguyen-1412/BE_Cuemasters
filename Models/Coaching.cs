using System;
using System.Collections.Generic;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class Coach
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User? User { get; set; }

        public string Bio { get; set; } = string.Empty;
        public Specialty Specialty { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal Rating { get; set; } = 0;
        public int TotalSessions { get; set; } = 0;
        public string? PhotoUrl { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<CoachAvailability> Availabilities { get; set; } = new List<CoachAvailability>();
        public ICollection<CoachingSession> Sessions { get; set; } = new List<CoachingSession>();
    }

    public class CoachAvailability
    {
        public int Id { get; set; }
        
        public Guid CoachId { get; set; }
        public Coach? Coach { get; set; }

        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime? SpecificDate { get; set; }
    }

    public class CoachingSession
    {
        public Guid Id { get; set; }

        public Guid CoachId { get; set; }
        public Coach? Coach { get; set; }

        // Null at creation time — the session is independent of any table reservation.
        // Populated when an admin links this session to a table booking at checkout.
        public Guid? TableSessionId { get; set; }
        public TableSession? TableSession { get; set; }

        public Guid StudentUserId { get; set; }
        public User? StudentUser { get; set; }

        public DateTime SessionDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public bool IsGroupSession { get; set; }
        public int MaxParticipants { get; set; } = 1;
        public decimal Cost { get; set; }

        public bool IsCompleted { get; set; }
        public string? CoachNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LinkedAt { get; set; }  // When an admin merged this into a table booking
        public DateTime? CancelledAt { get; set; }
    }
}
