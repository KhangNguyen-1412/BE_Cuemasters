using System.Security.Claims;
using BilliardsBooking.API.Data;
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
                .Select(ca => new 
                {
                    ca.Id,
                    ca.DayOfWeek,
                    StartTime = ca.StartTime.ToString(@"hh\:mm"),
                    EndTime = ca.EndTime.ToString(@"hh\:mm"),
                    ca.IsBlocked
                })
                .ToListAsync();

            return Ok(availabilities);
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
    }
}