using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Roles = "Admin")]
    public class CoachesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CoachesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AdminCoachResponse>>> GetAllCoaches()
        {
            var coaches = await _context.Coaches
                .Include(coach => coach.User)
                .OrderByDescending(coach => coach.IsActive)
                .ThenBy(coach => coach.User != null ? coach.User.FullName : string.Empty)
                .ToListAsync();

            var busyCoachIds = await GetBusyCoachIdsAsync();
            return Ok(coaches.Select(coach => MapCoach(coach, busyCoachIds.Contains(coach.Id))).ToList());
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AdminCoachResponse>> GetCoachById(Guid id)
        {
            var coach = await _context.Coaches
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (coach == null)
            {
                return NotFound(new { Message = "Coach not found." });
            }

            var busyCoachIds = await GetBusyCoachIdsAsync();
            return Ok(MapCoach(coach, busyCoachIds.Contains(coach.Id)));
        }

        [HttpPost]
        public async Task<ActionResult<AdminCoachResponse>> CreateCoach([FromBody] AdminUpsertCoachRequest request)
        {
            if (!TryParseCoachRequest(request, out var specialty, out var errorResult))
            {
                return errorResult!;
            }

            User user;

            if (!string.IsNullOrWhiteSpace(request.UserId))
            {
                if (!Guid.TryParse(request.UserId, out var userId))
                {
                    return BadRequest(new { Message = "Invalid user id." });
                }

                user = await _context.Users.FirstOrDefaultAsync(item => item.Id == userId) ?? null!;
                if (user == null)
                {
                    return NotFound(new { Message = "User not found." });
                }

                var existingCoach = await _context.Coaches.AnyAsync(coach => coach.UserId == userId);
                if (existingCoach)
                {
                    return Conflict(new { Message = "This user is already assigned to a coach profile." });
                }
            }
            else
            {
                var normalizedEmail = request.Email.Trim().ToLowerInvariant();
                var emailExists = await _context.Users.AnyAsync(item => item.Email.ToLower() == normalizedEmail);

                if (emailExists)
                {
                    return Conflict(new { Message = "A user with this email already exists." });
                }

                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = normalizedEmail,
                    FullName = request.FullName.Trim(),
                    PhoneNumber = request.PhoneNumber,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Staff@123"),
                    Role = Role.Staff,
                    AvatarUrl = request.PhotoUrl,
                    IsActive = request.IsActive
                };

                _context.Users.Add(user);
            }

            user.FullName = request.FullName.Trim();
            user.Email = request.Email.Trim();
            user.PhoneNumber = request.PhoneNumber;
            user.Role = Role.Staff;
            user.AvatarUrl = request.PhotoUrl;
            user.IsActive = request.IsActive;

            var coach = new Coach
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Bio = request.Bio.Trim(),
                Specialty = specialty,
                HourlyRate = request.HourlyRate,
                PhotoUrl = request.PhotoUrl,
                IsActive = request.IsActive
            };

            _context.Coaches.Add(coach);
            await _context.SaveChangesAsync();

            coach.User = user;
            return CreatedAtAction(nameof(GetCoachById), new { id = coach.Id }, MapCoach(coach, false));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AdminCoachResponse>> UpdateCoach(Guid id, [FromBody] AdminUpsertCoachRequest request)
        {
            if (!TryParseCoachRequest(request, out var specialty, out var errorResult))
            {
                return errorResult!;
            }

            var coach = await _context.Coaches
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (coach == null)
            {
                return NotFound(new { Message = "Coach not found." });
            }

            if (coach.User == null)
            {
                return Conflict(new { Message = "Coach user profile is missing." });
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var emailTaken = await _context.Users.AnyAsync(user =>
                user.Id != coach.UserId &&
                user.Email.ToLower() == normalizedEmail);

            if (emailTaken)
            {
                return Conflict(new { Message = "A user with this email already exists." });
            }

            coach.User.FullName = request.FullName.Trim();
            coach.User.Email = normalizedEmail;
            coach.User.PhoneNumber = request.PhoneNumber;
            coach.User.AvatarUrl = request.PhotoUrl;
            coach.User.Role = Role.Staff;
            coach.User.IsActive = request.IsActive;

            coach.Bio = request.Bio.Trim();
            coach.Specialty = specialty;
            coach.HourlyRate = request.HourlyRate;
            coach.PhotoUrl = request.PhotoUrl;
            coach.IsActive = request.IsActive;

            await _context.SaveChangesAsync();

            var busyCoachIds = await GetBusyCoachIdsAsync();
            return Ok(MapCoach(coach, busyCoachIds.Contains(coach.Id)));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteCoach(Guid id)
        {
            var coach = await _context.Coaches
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (coach == null)
            {
                return NotFound(new { Message = "Coach not found." });
            }

            coach.IsActive = false;
            if (coach.User != null)
            {
                coach.User.IsActive = false;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Coach archived successfully." });
        }

        private async Task<HashSet<Guid>> GetBusyCoachIdsAsync()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var nowTime = now.TimeOfDay;
            return await _context.CoachingSessions
                .Where(session =>
                    !session.IsCompleted &&
                    session.SessionDate == today &&
                    session.StartTime <= nowTime &&
                    session.EndTime > nowTime)
                .Select(session => session.CoachId)
                .ToHashSetAsync();
        }

        private static AdminCoachResponse MapCoach(Coach coach, bool isBusy)
        {
            return new AdminCoachResponse
            {
                Id = coach.Id.ToString(),
                UserId = coach.UserId.ToString(),
                FullName = coach.User?.FullName ?? string.Empty,
                Email = coach.User?.Email ?? string.Empty,
                PhoneNumber = coach.User?.PhoneNumber,
                Specialty = coach.Specialty.ToString(),
                Bio = coach.Bio,
                HourlyRate = coach.HourlyRate,
                Rating = coach.Rating,
                TotalSessions = coach.TotalSessions,
                PhotoUrl = coach.PhotoUrl ?? coach.User?.AvatarUrl,
                IsActive = coach.IsActive,
                CurrentStatus = !coach.IsActive ? "Inactive" : (isBusy ? "Busy" : "Available")
            };
        }

        private static bool TryParseCoachRequest(
            AdminUpsertCoachRequest request,
            out Specialty specialty,
            out ActionResult? errorResult)
        {
            errorResult = null;
            specialty = default;

            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                errorResult = new BadRequestObjectResult(new { Message = "Coach full name is required." });
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                errorResult = new BadRequestObjectResult(new { Message = "Coach email is required." });
                return false;
            }

            if (request.HourlyRate < 0)
            {
                errorResult = new BadRequestObjectResult(new { Message = "Hourly rate must be zero or greater." });
                return false;
            }

            if (!Enum.TryParse(request.Specialty, true, out specialty))
            {
                errorResult = new BadRequestObjectResult(new { Message = "Invalid coach specialty." });
                return false;
            }

            return true;
        }
    }
}
