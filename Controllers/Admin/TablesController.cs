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
    public class TablesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TablesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AdminTableResponse>>> GetAllTables()
        {
            var tables = await _context.Tables
                .OrderByDescending(table => table.IsActive)
                .ThenBy(table => table.TableNumber)
                .ToListAsync();

            var bookings = await LoadRelevantBookingsAsync(tables.Select(table => table.Id).ToList());
            var now = DateTime.UtcNow;

            return Ok(tables.Select(table => MapTable(table, bookings, now)).ToList());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AdminTableResponse>> GetTableById(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null)
            {
                return NotFound(new { Message = "Table not found." });
            }

            var bookings = await LoadRelevantBookingsAsync(new List<int> { id });
            return Ok(MapTable(table, bookings, DateTime.UtcNow));
        }

        [HttpPost]
        public async Task<ActionResult<AdminTableResponse>> CreateTable([FromBody] AdminUpsertTableRequest request)
        {
            if (!TryParseTableRequest(request, out var tableType, out var tableStatus, out var errorResult))
            {
                return errorResult!;
            }

            var normalizedTableNumber = request.TableNumber.Trim();

            var tableNumberExists = await _context.Tables
                .AnyAsync(table => table.TableNumber.ToLower() == normalizedTableNumber.ToLower());

            if (tableNumberExists)
            {
                return Conflict(new { Message = "A table with this number already exists." });
            }

            var table = new BilliardTable
            {
                TableNumber = normalizedTableNumber,
                Type = tableType,
                HourlyRate = request.HourlyRate,
                Status = tableStatus,
                IsActive = request.IsActive,
                PositionX = request.PositionX,
                PositionY = request.PositionY
            };

            _context.Tables.Add(table);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTableById), new { id = table.Id }, MapTable(table, new List<Booking>(), DateTime.UtcNow));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<AdminTableResponse>> UpdateTable(int id, [FromBody] AdminUpsertTableRequest request)
        {
            if (!TryParseTableRequest(request, out var tableType, out var tableStatus, out var errorResult))
            {
                return errorResult!;
            }

            var table = await _context.Tables.FindAsync(id);
            if (table == null)
            {
                return NotFound(new { Message = "Table not found." });
            }

            var normalizedTableNumber = request.TableNumber.Trim();

            var tableNumberExists = await _context.Tables
                .AnyAsync(existingTable =>
                    existingTable.Id != id &&
                    existingTable.TableNumber.ToLower() == normalizedTableNumber.ToLower());

            if (tableNumberExists)
            {
                return Conflict(new { Message = "A table with this number already exists." });
            }

            table.TableNumber = normalizedTableNumber;
            table.Type = tableType;
            table.HourlyRate = request.HourlyRate;
            table.Status = tableStatus;
            table.IsActive = request.IsActive;
            table.PositionX = request.PositionX;
            table.PositionY = request.PositionY;

            await _context.SaveChangesAsync();

            var bookings = await LoadRelevantBookingsAsync(new List<int> { table.Id });
            return Ok(MapTable(table, bookings, DateTime.UtcNow));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null)
            {
                return NotFound(new { Message = "Table not found." });
            }

            var hasBookingHistory = await _context.Bookings.AnyAsync(booking => booking.TableId == id);

            if (hasBookingHistory)
            {
                table.IsActive = false;
                table.Status = TableManualStatus.Maintenance;
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Table archived because it already has booking history." });
            }

            _context.Tables.Remove(table);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<List<Booking>> LoadRelevantBookingsAsync(List<int> tableIds)
        {
            if (tableIds.Count == 0)
            {
                return new List<Booking>();
            }

            var today = DateTime.UtcNow.Date;
            var futureWindow = today.AddDays(7);

            return await _context.Bookings
                .Include(booking => booking.User)
                .Where(booking =>
                    tableIds.Contains(booking.TableId) &&
                    booking.Status != BookingStatus.Cancelled &&
                    booking.BookingDate >= today &&
                    booking.BookingDate <= futureWindow)
                .ToListAsync();
        }

        private static AdminTableResponse MapTable(BilliardTable table, IEnumerable<Booking> bookings, DateTime now)
        {
            var currentBooking = bookings
                .Where(booking =>
                    booking.TableId == table.Id &&
                    booking.BookingDate.Date == now.Date &&
                    booking.BookingDate.Add(booking.StartTime) <= now &&
                    booking.BookingDate.Add(booking.EndTime) > now &&
                    (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.InProgress))
                .OrderByDescending(booking => booking.StartTime)
                .FirstOrDefault();

            var nextBooking = bookings
                .Where(booking =>
                    booking.TableId == table.Id &&
                    booking.BookingDate.Add(booking.StartTime) > now &&
                    (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.InProgress))
                .OrderBy(booking => booking.BookingDate)
                .ThenBy(booking => booking.StartTime)
                .FirstOrDefault();

            return new AdminTableResponse
            {
                Id = table.Id,
                TableNumber = table.TableNumber,
                Type = table.Type.ToString(),
                HourlyRate = table.HourlyRate,
                ManualStatus = table.Status.ToString(),
                DisplayStatus = GetDisplayStatus(table, currentBooking, nextBooking),
                IsActive = table.IsActive,
                PositionX = table.PositionX,
                PositionY = table.PositionY,
                CurrentCustomerName = currentBooking?.User?.FullName,
                CurrentSessionStartedAt = currentBooking == null ? null : currentBooking.BookingDate.Add(currentBooking.StartTime),
                NextBookingStartTime = nextBooking == null ? null : nextBooking.BookingDate.Add(nextBooking.StartTime),
                CurrentSessionAmount = currentBooking?.TotalTableCost ?? 0
            };
        }

        private static string GetDisplayStatus(BilliardTable table, Booking? currentBooking, Booking? nextBooking)
        {
            if (!table.IsActive)
            {
                return "Inactive";
            }

            if (table.Status == TableManualStatus.Maintenance)
            {
                return "Maintenance";
            }

            if (currentBooking != null)
            {
                return "InUse";
            }

            if (nextBooking != null)
            {
                return "Reserved";
            }

            return "Available";
        }

        private static bool TryParseTableRequest(
            AdminUpsertTableRequest request,
            out TableType tableType,
            out TableManualStatus tableStatus,
            out ActionResult? errorResult)
        {
            errorResult = null;
            tableType = default;
            tableStatus = default;

            if (string.IsNullOrWhiteSpace(request.TableNumber))
            {
                errorResult = new BadRequestObjectResult(new { Message = "Table number is required." });
                return false;
            }

            if (request.HourlyRate < 0)
            {
                errorResult = new BadRequestObjectResult(new { Message = "Hourly rate must be zero or greater." });
                return false;
            }

            if (!Enum.TryParse(request.Type, true, out tableType))
            {
                errorResult = new BadRequestObjectResult(new { Message = "Invalid table type." });
                return false;
            }

            if (!Enum.TryParse(request.Status, true, out tableStatus))
            {
                errorResult = new BadRequestObjectResult(new { Message = "Invalid table status." });
                return false;
            }

            return true;
        }
    }
}
