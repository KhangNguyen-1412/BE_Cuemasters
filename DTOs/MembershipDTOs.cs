using System;

namespace BilliardsBooking.API.DTOs
{
    public class MembershipPlanResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PricePerMonth { get; set; }
        public int DurationMonths { get; set; }
        public decimal TableDiscountPercent { get; set; }
        public decimal FnBDiscountPercent { get; set; }
        public int FreeCoachingHours { get; set; }
        public bool IsActive { get; set; }
    }

    public class UserMembershipResponse
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AutoRenew { get; set; }
        public string Status { get; set; } = string.Empty; // Active, Expired, Cancelled
        public int UsedCoachingHours { get; set; }
    }

    public class SubscribeRequest
    {
        public int PlanId { get; set; }
        public bool AutoRenew { get; set; }
    }
}
