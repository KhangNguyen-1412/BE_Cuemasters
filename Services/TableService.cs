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
    public interface ITableService
    {
        Task<List<TableResponse>> GetTablesWithStatusAsync();
        Task<TableAvailabilityResponse?> GetTableAvailabilityAsync(int tableId, DateTime date);
    }

    public class TableService : ITableService
    {
        private readonly AppDbContext _context;

        public TableService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<TableResponse>> GetTablesWithStatusAsync()
        {
            return await _context.Tables
                .Where(t => t.IsActive)
                .Select(t => new TableResponse
                {
                    Id = t.Id,
                    Name = t.TableNumber,
                    Description = t.Type.ToString() + " Table",
                    PricePerHour = t.HourlyRate,
                    Type = t.Type.ToString(),
                    Status = t.Status.ToString(),
                })
                .ToListAsync();
        }

        public async Task<TableAvailabilityResponse?> GetTableAvailabilityAsync(int tableId, DateTime date)
        {
            var table = await _context.Tables.FindAsync(tableId);
            if (table == null) return null;

            var response = new TableAvailabilityResponse
            {
                TableId = tableId,
                Date = date.Date,
                Slots = new List<TableAvailabilitySlotResponse>()
            };

            // Generate 30 min slots from 8 AM to 10 PM
            var startTime = new TimeSpan(8, 0, 0);
            var endTime = new TimeSpan(22, 0, 0);

            var bookSlots = await _context.BookingSlots
                .Where(bs => bs.TableId == tableId && bs.SlotDate == date.Date && bs.IsActive)
                .ToListAsync();

            for (var time = startTime; time < endTime; time = time.Add(TimeSpan.FromMinutes(30)))
            {
                var slotEnd = time.Add(TimeSpan.FromMinutes(30));
                
                // If any existing booking slot overlaps
                var isAvailable = !bookSlots.Any(bs => bs.SlotStart == time);

                response.Slots.Add(new TableAvailabilitySlotResponse
                {
                    StartTime = date.Date.Add(time),
                    EndTime = date.Date.Add(slotEnd),
                    IsAvailable = isAvailable
                });
            }

            return response;
        }
    }
}