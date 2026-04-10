using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Models;

namespace BilliardsBooking.API.Services
{
    public interface ICoachService
    {
        Task<List<CoachResponse>> GetAllCoachesAsync();
        Task<List<CoachAvailabilitySlotResponse>> GetCoachAvailabilityAsync(Guid coachId, DateTime date);
        Task<Guid> BookCoachSessionAsync(Guid studentUserId, CreateCoachingSessionRequest request);
    }

    public class CoachService : ICoachService
    {
        private readonly AppDbContext _context;

        public CoachService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<CoachResponse>> GetAllCoachesAsync()
        {
             return await _context.Coaches
                .Include(c => c.User)
                .Where(c => c.IsActive && c.User != null)
                .Select(c => new CoachResponse
                {
                    Id = c.Id.ToString(),
                    UserId = c.UserId.ToString(),
                    FullName = c.User!.FullName,
                    Specialty = c.Specialty.ToString(),
                    Bio = c.Bio,
                    HourlyRate = c.HourlyRate,
                    Rating = c.Rating,
                    AvatarUrl = c.PhotoUrl
                })
                .ToListAsync();
        }

        public async Task<List<CoachAvailabilitySlotResponse>> GetCoachAvailabilityAsync(Guid coachId, DateTime date)
        {
            var coach = await _context.Coaches
                .Include(c => c.Availabilities)
                .Include(c => c.Sessions.Where(s => s.SessionDate == date.Date && !s.IsCompleted))
                .FirstOrDefaultAsync(c => c.Id == coachId);

            if (coach == null) return new List<CoachAvailabilitySlotResponse>();

            var availability = GetApplicableAvailabilities(coach.Availabilities, date);
            var availableWindows = availability
                .Where(a => !a.IsBlocked)
                .OrderBy(a => a.StartTime)
                .ToList();
            var blockedWindows = availability
                .Where(a => a.IsBlocked)
                .ToList();

            var slots = new List<CoachAvailabilitySlotResponse>();

            foreach (var avail in availableWindows)
            {
                for (var time = avail.StartTime; time < avail.EndTime; time = time.Add(TimeSpan.FromMinutes(60)))
                {
                    var slotEnd = time.Add(TimeSpan.FromMinutes(60));
                    if (slotEnd > avail.EndTime)
                    {
                        break;
                    }

                    var overlapsBlocked = blockedWindows.Any(blocked => Overlaps(blocked.StartTime, blocked.EndTime, time, slotEnd));
                    var overlapsSession = coach.Sessions.Any(s => s.SessionDate == date.Date && Overlaps(s.StartTime, s.EndTime, time, slotEnd));
                    var existingSlot = slots.Any(s => s.StartTime == time.ToString(@"hh\:mm") && s.EndTime == slotEnd.ToString(@"hh\:mm"));

                    if (overlapsBlocked)
                    {
                        continue;
                    }

                    if (existingSlot)
                    {
                        continue;
                    }

                    slots.Add(new CoachAvailabilitySlotResponse
                    {
                        StartTime = time.ToString(@"hh\:mm"),
                        EndTime = slotEnd.ToString(@"hh\:mm"),
                        IsAvailable = !overlapsSession
                    });
                }
            }

            return slots
                .OrderBy(s => s.StartTime)
                .ThenBy(s => s.EndTime)
                .ToList();
        }

        public async Task<Guid> BookCoachSessionAsync(Guid studentUserId, CreateCoachingSessionRequest request)
        {
            if (!TimeSpan.TryParse(request.StartTime, out TimeSpan startTime) ||
                !TimeSpan.TryParse(request.EndTime, out TimeSpan endTime))
            {
                throw new Exception("Invalid time format (Use HH:mm)");
            }

            if (startTime >= endTime) throw new Exception("End time must be after start time");

            var coach = await _context.Coaches
                .Include(c => c.Availabilities)
                .FirstOrDefaultAsync(c => c.Id == request.CoachId);

            if (coach == null || !coach.IsActive) 
                throw new Exception("Coach not found or inactive.");

            var availability = GetApplicableAvailabilities(coach.Availabilities, request.SessionDate);
            var isWithinAvailableWindow = availability.Any(a =>
                !a.IsBlocked &&
                a.StartTime <= startTime &&
                a.EndTime >= endTime);
            var isBlocked = availability.Any(a =>
                a.IsBlocked &&
                Overlaps(a.StartTime, a.EndTime, startTime, endTime));

            if (!isWithinAvailableWindow || isBlocked)
            {
                throw new Exception("Coach is not scheduled to work during this time.");
            }

            var hasOverlap = await _context.CoachingSessions.AnyAsync(s => 
                s.CoachId == request.CoachId && 
                s.SessionDate == request.SessionDate.Date && 
                !s.IsCompleted && 
                s.StartTime < endTime && s.EndTime > startTime);

            if (hasOverlap)
               throw new Exception("Coach is not available for the selected time.");

            var durationHours = (decimal)(endTime - startTime).TotalHours;
            var session = new CoachingSession
            {
                CoachId = request.CoachId,
                StudentUserId = studentUserId,
                SessionDate = request.SessionDate.Date,
                StartTime = startTime,
                EndTime = endTime,
                TableSessionId = null,
                Cost = coach.HourlyRate * durationHours,
                IsCompleted = false
            };

            await _context.CoachingSessions.AddAsync(session);
            await _context.SaveChangesAsync();

            return session.Id;
        }

        private static List<CoachAvailability> GetApplicableAvailabilities(IEnumerable<CoachAvailability> availabilities, DateTime date)
        {
            return availabilities
                .Where(a =>
                    (a.SpecificDate.HasValue && a.SpecificDate.Value.Date == date.Date) ||
                    (!a.SpecificDate.HasValue && a.DayOfWeek == date.DayOfWeek))
                .ToList();
        }

        private static bool Overlaps(TimeSpan startA, TimeSpan endA, TimeSpan startB, TimeSpan endB)
        {
            return startA < endB && endA > startB;
        }
    }
}
