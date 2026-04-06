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
                    Id = c.Id.ToString(), // Hack assuming we're keeping Id as Int in DTO but Guid in DB? Actually let's assume DTO coach id is int. Oh wait, DTO CoachResponse has int Id, DB Coach has Guid Id.
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

            var dayOfWeek = date.DayOfWeek;
            
            // Check recurring availability
            var availability = coach.Availabilities
                .Where(a => a.DayOfWeek == dayOfWeek || (a.SpecificDate.HasValue && a.SpecificDate.Value.Date == date.Date))
                .ToList();

            var slots = new List<CoachAvailabilitySlotResponse>();

            foreach(var avail in availability)
            {
               if(avail.IsBlocked) continue;
               
               for (var time = avail.StartTime; time < avail.EndTime; time = time.Add(TimeSpan.FromMinutes(60)))
               {
                   var slotEnd = time.Add(TimeSpan.FromMinutes(60));
                   var isAvailable = !coach.Sessions.Any(s => s.SessionDate == date.Date && s.StartTime < slotEnd && s.EndTime > time);
                   
                   slots.Add(new CoachAvailabilitySlotResponse
                   {
                        StartTime = date.Date.Add(time),
                        EndTime = date.Date.Add(slotEnd),
                        IsAvailable = isAvailable
                   });
               }
            }

            return slots;
        }
    }
}
