using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Models;
using BilliardsBooking.API.Enums;
using System;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class MembershipsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MembershipsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllMemberships()
        {
            var plans = await _context.MembershipPlans
                .Select(p => new AdminMembershipPlanResponse
                {
                    Id = p.Id,
                    Tier = p.Tier.ToString(),
                    Name = p.Name,
                    MonthlyPrice = p.MonthlyPrice,
                    TableDiscountPercent = p.TableDiscountPercent,
                    PriorityBooking = p.PriorityBooking,
                    FreeCoachingSessionsPerMonth = p.FreeCoachingSessionsPerMonth,
                    MaxAdvanceBookingDays = p.MaxAdvanceBookingDays,
                    IsActive = p.IsActive,
                    ActiveSubscribers = _context.UserMemberships.Count(um => um.MembershipPlanId == p.Id && um.IsActive)
                })
                .ToListAsync();

            return Ok(plans);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMembershipPlan(int id)
        {
            var plan = await _context.MembershipPlans
                .Where(p => p.Id == id)
                .Select(p => new AdminMembershipPlanResponse
                {
                    Id = p.Id,
                    Tier = p.Tier.ToString(),
                    Name = p.Name,
                    MonthlyPrice = p.MonthlyPrice,
                    TableDiscountPercent = p.TableDiscountPercent,
                    PriorityBooking = p.PriorityBooking,
                    FreeCoachingSessionsPerMonth = p.FreeCoachingSessionsPerMonth,
                    MaxAdvanceBookingDays = p.MaxAdvanceBookingDays,
                    IsActive = p.IsActive,
                    ActiveSubscribers = _context.UserMemberships.Count(um => um.MembershipPlanId == p.Id && um.IsActive)
                })
                .FirstOrDefaultAsync();

            if (plan == null) return NotFound();

            return Ok(plan);
        }

        [HttpPost]
        public async Task<IActionResult> CreateMembershipPlan(AdminUpsertMembershipPlanRequest request)
        {
            if (!Enum.TryParse<MembershipTier>(request.Tier, out var tier))
            {
                return BadRequest("Invalid membership tier.");
            }

            var plan = new MembershipPlan
            {
                Tier = tier,
                Name = request.Name,
                MonthlyPrice = request.MonthlyPrice,
                TableDiscountPercent = request.TableDiscountPercent,
                PriorityBooking = request.PriorityBooking,
                FreeCoachingSessionsPerMonth = request.FreeCoachingSessionsPerMonth,
                MaxAdvanceBookingDays = request.MaxAdvanceBookingDays,
                IsActive = request.IsActive
            };

            _context.MembershipPlans.Add(plan);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMembershipPlan), new { id = plan.Id }, null);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMembershipPlan(int id, AdminUpsertMembershipPlanRequest request)
        {
            var plan = await _context.MembershipPlans.FindAsync(id);
            if (plan == null) return NotFound();

            if (!Enum.TryParse<MembershipTier>(request.Tier, out var tier))
            {
                return BadRequest("Invalid membership tier.");
            }

            plan.Tier = tier;
            plan.Name = request.Name;
            plan.MonthlyPrice = request.MonthlyPrice;
            plan.TableDiscountPercent = request.TableDiscountPercent;
            plan.PriorityBooking = request.PriorityBooking;
            plan.FreeCoachingSessionsPerMonth = request.FreeCoachingSessionsPerMonth;
            plan.MaxAdvanceBookingDays = request.MaxAdvanceBookingDays;
            plan.IsActive = request.IsActive;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMembershipPlan(int id)
        {
            var plan = await _context.MembershipPlans.FindAsync(id);
            if (plan == null) return NotFound();

            var hasActiveSubscribers = await _context.UserMemberships
                .AnyAsync(um => um.MembershipPlanId == id && um.IsActive);

            if (hasActiveSubscribers)
            {
                plan.IsActive = false;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Plan soft deleted (deactivated) because it has active subscribers." });
            }

            _context.MembershipPlans.Remove(plan);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Plan hard deleted successfully." });
        }
    }
}
