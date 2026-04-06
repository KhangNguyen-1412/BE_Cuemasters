using System;
using System.Collections.Generic;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Models
{
    public class MembershipPlan
    {
        public int Id { get; set; }
        public MembershipTier Tier { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyPrice { get; set; }
        public int TableDiscountPercent { get; set; }
        public bool PriorityBooking { get; set; }
        public int FreeCoachingSessionsPerMonth { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UserMembership
    {
        public Guid Id { get; set; }
        
        public Guid UserId { get; set; }
        public User? User { get; set; }

        public int MembershipPlanId { get; set; }
        public MembershipPlan? MembershipPlan { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public bool AutoRenew { get; set; }

        public ICollection<MembershipBenefitUsage> BenefitUsages { get; set; } = new List<MembershipBenefitUsage>();
    }

    public class MembershipBenefitUsage
    {
        public int Id { get; set; }
        
        public Guid UserMembershipId { get; set; }
        public UserMembership? UserMembership { get; set; }

        public BenefitType BenefitType { get; set; }

        public int UsedCount { get; set; }
        public int MaxCount { get; set; }

        // Format: YYYYMM (e.g., 202410)
        public int BillingMonth { get; set; } 
    }
}
