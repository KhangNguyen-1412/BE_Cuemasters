using System.Security.Claims;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Staff")]
    public class StaffController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StaffController(AppDbContext context)
        {
            _context = context;
        }

        private async Task<Guid?> GetCoachIdAsync()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return null;

            var coach = await _context.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
            return coach?.Id;
        }

        [HttpGet("schedule")]
        public async Task<IActionResult> GetSchedule([FromQuery] string? dateStr)
        {
            var coachId = await GetCoachIdAsync();
            if (coachId == null) return Unauthorized();

            var targetDate = string.IsNullOrEmpty(dateStr) ? DateTime.Today : DateTime.Parse(dateStr).Date;

            // Get availabilities applicable to the target date
            var dayOfWeek = targetDate.DayOfWeek;
            
            var schedule = await _context.CoachAvailabilities
                .Where(ca => ca.CoachId == coachId && 
                            (ca.SpecificDate == null || ca.SpecificDate.Value.Date == targetDate) &&
                            ca.DayOfWeek == dayOfWeek)
                .Select(ca => new 
                { 
                    ca.Id,
                    ca.DayOfWeek,
                    StartTime = ca.StartTime.ToString(@"hh\:mm"),
                    EndTime = ca.EndTime.ToString(@"hh\:mm"),
                    ca.IsBlocked
                })
                .ToListAsync();

            return Ok(schedule);
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var coachId = await GetCoachIdAsync();
            if (coachId == null) return Unauthorized();

            var sessions = await _context.CoachingSessions
                .Include(s => s.StudentUser)
                .Where(s => s.CoachId == coachId && s.SessionDate >= DateTime.Today)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .Select(s => new 
                {
                    s.Id,
                    SessionDate = s.SessionDate.ToString("yyyy-MM-dd"),
                    StartTime = s.StartTime.ToString(@"hh\:mm"),
                    EndTime = s.EndTime.ToString(@"hh\:mm"),
                    StudentName = s.StudentUser != null ? s.StudentUser.FullName : "Unknown",
                    s.IsGroupSession,
                    s.MaxParticipants,
                    s.IsCompleted
                })
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability() 
        {
            var coachId = await GetCoachIdAsync();
            if (coachId == null) return Unauthorized();

            var availabilities = await _context.CoachAvailabilities
                .Where(ca => ca.CoachId == coachId)
                .OrderBy(ca => ca.DayOfWeek)
                .ThenBy(ca => ca.StartTime)
                .Select(ca => new 
                {
                    ca.Id,
                    DayOfWeek = (int)ca.DayOfWeek,
                    StartTime = ca.StartTime.ToString(@"hh\:mm"),
                    EndTime = ca.EndTime.ToString(@"hh\:mm"),
                    ca.IsBlocked,
                    SpecificDate = ca.SpecificDate.HasValue ? ca.SpecificDate.Value.ToString("yyyy-MM-dd") : null
                })
                .ToListAsync();

            return Ok(availabilities);
        }

        [HttpPost("availability")]
        public async Task<IActionResult> CreateAvailability([FromBody] UpsertCoachAvailabilityRequest request)
        {
            var coachId = await GetCoachIdAsync();
            if (coachId == null) return Unauthorized();

            var validationError = await ValidateAvailabilityRequestAsync(coachId.Value, request);
            if (validationError != null)
            {
                return BadRequest(new { message = validationError });
            }

            var availability = new CoachAvailability
            {
                CoachId = coachId.Value,
                DayOfWeek = (DayOfWeek)request.DayOfWeek,
                StartTime = TimeSpan.Parse(request.StartTime),
                EndTime = TimeSpan.Parse(request.EndTime),
                IsBlocked = request.IsBlocked,
                SpecificDate = request.SpecificDate?.Date
            };

            _context.CoachAvailabilities.Add(availability);
            await _context.SaveChangesAsync();

            return Ok(new CoachAvailabilityResponse
            {
                Id = availability.Id,
                DayOfWeek = (int)availability.DayOfWeek,
                StartTime = availability.StartTime.ToString(@"hh\:mm"),
                EndTime = availability.EndTime.ToString(@"hh\:mm"),
                IsBlocked = availability.IsBlocked,
                SpecificDate = availability.SpecificDate?.ToString("yyyy-MM-dd")
            });
        }

        [HttpPut("availability/{id:int}")]
        public async Task<IActionResult> UpdateAvailability(int id, [FromBody] UpsertCoachAvailabilityRequest request)
        {
            var coachId = await GetCoachIdAsync();
            if (coachId == null) return Unauthorized();

            var availability = await _context.CoachAvailabilities
                .FirstOrDefaultAsync(ca => ca.Id == id && ca.CoachId == coachId.Value);

            if (availability == null)
            {
                return NotFound(new { message = "Không tìm thấy khung giờ để cập nhật." });
            }

            var validationError = await ValidateAvailabilityRequestAsync(coachId.Value, request, id);
            if (validationError != null)
            {
                return BadRequest(new { message = validationError });
            }

            availability.DayOfWeek = (DayOfWeek)request.DayOfWeek;
            availability.StartTime = TimeSpan.Parse(request.StartTime);
            availability.EndTime = TimeSpan.Parse(request.EndTime);
            availability.IsBlocked = request.IsBlocked;
            availability.SpecificDate = request.SpecificDate?.Date;

            await _context.SaveChangesAsync();

            return Ok(new CoachAvailabilityResponse
            {
                Id = availability.Id,
                DayOfWeek = (int)availability.DayOfWeek,
                StartTime = availability.StartTime.ToString(@"hh\:mm"),
                EndTime = availability.EndTime.ToString(@"hh\:mm"),
                IsBlocked = availability.IsBlocked,
                SpecificDate = availability.SpecificDate?.ToString("yyyy-MM-dd")
            });
        }

        [HttpDelete("availability/{id:int}")]
        public async Task<IActionResult> DeleteAvailability(int id)
        {
            var coachId = await GetCoachIdAsync();
            if (coachId == null) return Unauthorized();

            var availability = await _context.CoachAvailabilities
                .FirstOrDefaultAsync(ca => ca.Id == id && ca.CoachId == coachId.Value);

            if (availability == null)
            {
                return NotFound(new { message = "Không tìm thấy khung giờ để xóa." });
            }

            _context.CoachAvailabilities.Remove(availability);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa khung giờ." });
        }

        [HttpPut("sessions/{id}/complete")]
        public async Task<IActionResult> CompleteSession(Guid id, [FromBody] BilliardsBooking.API.DTOs.CompleteSessionRequest request)
        {
            var coachId = await GetCoachIdAsync();
            if (coachId == null) return Unauthorized();

            var session = await _context.CoachingSessions.FindAsync(id);
            if (session == null) return NotFound();

            if (session.CoachId != coachId) return Forbid();

            session.IsCompleted = true;
            session.CoachNotes = request.Notes;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Session marked as complete." });
        }

        private async Task<string?> ValidateAvailabilityRequestAsync(Guid coachId, UpsertCoachAvailabilityRequest request, int? existingId = null)
        {
            if (request.DayOfWeek < 0 || request.DayOfWeek > 6)
            {
                return "Ngày trong tuần không hợp lệ.";
            }

            if (!TimeSpan.TryParse(request.StartTime, out var startTime) || !TimeSpan.TryParse(request.EndTime, out var endTime))
            {
                return "Khung giờ không hợp lệ. Hãy dùng định dạng HH:mm.";
            }

            if (startTime >= endTime)
            {
                return "Giờ kết thúc phải sau giờ bắt đầu.";
            }

            var sameScopeEntries = await _context.CoachAvailabilities
                .Where(ca => ca.CoachId == coachId && ca.Id != existingId)
                .Where(ca =>
                    (request.SpecificDate.HasValue && ca.SpecificDate.HasValue && ca.SpecificDate.Value.Date == request.SpecificDate.Value.Date) ||
                    (!request.SpecificDate.HasValue && !ca.SpecificDate.HasValue && ca.DayOfWeek == (DayOfWeek)request.DayOfWeek))
                .ToListAsync();

            var overlapsSameType = sameScopeEntries.Any(ca =>
                ca.IsBlocked == request.IsBlocked &&
                startTime < ca.EndTime &&
                endTime > ca.StartTime);

            if (overlapsSameType)
            {
                return request.IsBlocked
                    ? "Khung giờ bận bị trùng với một khoảng bận khác."
                    : "Khung giờ rảnh bị trùng với một khoảng rảnh khác.";
            }

            return null;
        }
    }
}
