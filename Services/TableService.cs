using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using Microsoft.EntityFrameworkCore;

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
                    Description = t.Type + " Table",
                    PricePerHour = t.HourlyRate,
                    Type = t.Type.ToString(),
                    Status = t.Status == TableManualStatus.Maintenance
                        ? TableRealTimeStatus.Maintenance.ToString()
                        : t.RealTimeStatus.ToString()
                })
                .ToListAsync();
        }

        public async Task<TableAvailabilityResponse?> GetTableAvailabilityAsync(int tableId, DateTime date)
        {
            var table = await _context.Tables.FindAsync(tableId);
            if (table == null || !table.IsActive) return null;

            var response = new TableAvailabilityResponse
            {
                TableId = tableId,
                Date = date.Date
            };

            var activeSessions = await _context.TableSessions
                .Where(s => s.TableId == tableId &&
                            s.Status == TableSessionStatus.Active &&
                            s.StartedAt.Date == date.Date)
                .ToListAsync();

            var startTime = new TimeSpan(8, 0, 0);
            var endTime = new TimeSpan(22, 0, 0);

            for (var time = startTime; time < endTime; time = time.Add(TimeSpan.FromMinutes(30)))
            {
                var slotStart = date.Date.Add(time);
                var slotEnd = slotStart.AddMinutes(30);

                var isBlockedBySession = activeSessions.Any(s =>
                    s.StartedAt < slotEnd &&
                    (s.EndedAt ?? DateTime.MaxValue) > slotStart);

                response.Slots.Add(new TableAvailabilitySlotResponse
                {
                    StartTime = slotStart,
                    EndTime = slotEnd,
                    IsAvailable = table.Status != TableManualStatus.Maintenance && !isBlockedBySession
                });
            }

            return response;
        }
    }
}
