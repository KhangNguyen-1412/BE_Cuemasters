using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilliardsBooking.API.Enums;
using Microsoft.EntityFrameworkCore;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Services
{
    public interface IMembershipService
    {
        Task<List<MembershipPlanResponse>> GetPlansAsync();
        Task<UserMembershipResponse?> GetUserMembershipAsync(Guid userId);
        Task<UserMembershipResponse?> SubscribeAsync(Guid userId, SubscribeRequest request);
        Task<bool> CancelAutoRenewAsync(Guid userId);
    }

    public class MembershipService : IMembershipService
    {
        private readonly AppDbContext _context;

        public MembershipService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<MembershipPlanResponse>> GetPlansAsync()
        {
             return await _context.MembershipPlans
                .Where(m => m.IsActive)
                .Select(m => new MembershipPlanResponse
                {
                    Id = m.Id,
                    Name = m.Name,
                    Description = m.Tier.ToString() + " Membership",
                    PricePerMonth = m.MonthlyPrice,
                    DurationMonths = 1,
                    TableDiscountPercent = m.TableDiscountPercent,
                    FnBDiscountPercent = 0,
                    FreeCoachingHours = m.FreeCoachingSessionsPerMonth,
                    IsActive = m.IsActive
                })
                .ToListAsync();
        }

        public async Task<UserMembershipResponse?> GetUserMembershipAsync(Guid userId)
        {
            var userMem = await _context.UserMemberships
                    .Include(um => um.MembershipPlan)
                    .Where(m => m.UserId == userId && m.IsActive)
                    .FirstOrDefaultAsync();
            if(userMem == null) return null;
            return new UserMembershipResponse
            {
                    Id = userMem.Id.ToString(),
                    UserId = userMem.UserId.ToString(),
                    PlanId = userMem.MembershipPlanId,
                    PlanName = userMem.MembershipPlan!.Name,
                    StartDate = userMem.StartDate,
                    EndDate = userMem.EndDate,
                    AutoRenew = userMem.AutoRenew,
                    Status = userMem.IsActive ? "Active" : "Expired",
                    UsedCoachingHours = 0
            };
        }

        public async Task<UserMembershipResponse?> SubscribeAsync(Guid userId, SubscribeRequest request)
        {
            var plan = await _context.MembershipPlans.FindAsync(request.PlanId);
            if (plan == null || !plan.IsActive) return null;

            var existing = await _context.UserMemberships
                .Where(m => m.UserId == userId && m.IsActive)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                // Can't subscribe if already active for simplicity
                return null;
            }

            var newMembership = new UserMembership
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MembershipPlanId = plan.Id,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                IsActive = true,
                AutoRenew = request.AutoRenew
            };

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = plan.MonthlyPrice,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Completed,
                CreatedAt = DateTime.UtcNow
                // No BookingId, it's a membership payment
            };

            _context.UserMemberships.Add(newMembership);
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return await GetUserMembershipAsync(userId);
        }

        public async Task<bool> CancelAutoRenewAsync(Guid userId)
        {
            var userMem = await _context.UserMemberships
                .Where(m => m.UserId == userId && m.IsActive)
                .FirstOrDefaultAsync();

            if (userMem == null) return false;

            userMem.AutoRenew = false;
            await _context.SaveChangesAsync();

            return true;
        }
    }
}