using System;

namespace BilliardsBooking.API.DTOs
{
    public class MembershipPlanResponse
    {
        public int Id { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public int TableDiscountPercent { get; set; }
        public bool PriorityBooking { get; set; }
        public int FreeCoachingSessionsPerMonth { get; set; }
        public int MaxAdvanceBookingDays { get; set; }
        public bool IsActive { get; set; }
    }

    public class UserMembershipResponse
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int PlanId { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public int TableDiscountPercent { get; set; }
        public bool PriorityBooking { get; set; }
        public int FreeCoachingSessionsPerMonth { get; set; }
        public int MaxAdvanceBookingDays { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AutoRenew { get; set; }
        public string Status { get; set; } = string.Empty; // Active, Expired, Cancelled
        public int UsedFreeCoachingSessions { get; set; }
    }

    public class SubscribeRequest
    {
        public int PlanId { get; set; }
        public bool AutoRenew { get; set; }
    }
}
