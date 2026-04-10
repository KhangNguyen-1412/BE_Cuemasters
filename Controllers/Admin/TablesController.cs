using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
using BilliardsBooking.API.Services;
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
        private readonly ITableSessionService _tableSessionService;

        public TablesController(AppDbContext context, ITableSessionService tableSessionService)
        {
            _context = context;
            _tableSessionService = tableSessionService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AdminTableResponse>>> GetAllTables()
        {
            var tables = await _context.Tables
                .OrderByDescending(table => table.IsActive)
                .ThenBy(table => table.TableNumber)
                .ToListAsync();

            var activeSessions = await _context.TableSessions
                .Include(session => session.User)
                .Include(session => session.Table)
                .Include(session => session.FnBOrders)
                .Include(session => session.CoachingSessions)
                .Where(session => session.Status == TableSessionStatus.Active)
                .ToListAsync();

            var upcomingReservations = await _context.Reservations
                .Include(reservation => reservation.User)
                .Where(reservation => reservation.Status == ReservationStatus.Confirmed)
                .OrderBy(reservation => reservation.BookingDate)
                .ThenBy(reservation => reservation.StartTime)
                .ToListAsync();

            var now = DateTime.UtcNow;
            return Ok(tables.Select(table => MapTable(table, activeSessions, upcomingReservations, now)).ToList());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AdminTableResponse>> GetTableById(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null)
            {
                return NotFound(new { Message = "Table not found." });
            }

            var activeSessions = await _context.TableSessions
                .Include(session => session.User)
                .Include(session => session.Table)
                .Include(session => session.FnBOrders)
                .Include(session => session.CoachingSessions)
                .Where(session => session.Status == TableSessionStatus.Active && session.TableId == id)
                .ToListAsync();

            var upcomingReservations = await _context.Reservations
                .Include(reservation => reservation.User)
                .Where(reservation => reservation.Status == ReservationStatus.Confirmed &&
                                      reservation.RequestedTableType == table.Type)
                .OrderBy(reservation => reservation.BookingDate)
                .ThenBy(reservation => reservation.StartTime)
                .ToListAsync();

            return Ok(MapTable(table, activeSessions, upcomingReservations, DateTime.UtcNow));
        }

        [HttpPost]
        public async Task<ActionResult<AdminTableResponse>> CreateTable([FromBody] AdminUpsertTableRequest request)
        {
            if (!TryParseTableRequest(request, out var tableType, out var tableStatus, out var errorResult))
            {
                return errorResult!;
            }

            var normalizedTableNumber = request.TableNumber.Trim();
            var tableNumberExists = await _context.Tables.AnyAsync(table => table.TableNumber.ToLower() == normalizedTableNumber.ToLower());
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
                RealTimeStatus = tableStatus == TableManualStatus.Maintenance ? TableRealTimeStatus.Maintenance : TableRealTimeStatus.Available,
                IsActive = request.IsActive,
                PositionX = request.PositionX,
                PositionY = request.PositionY
            };

            _context.Tables.Add(table);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTableById), new { id = table.Id }, MapTable(table, new List<TableSession>(), new List<Reservation>(), DateTime.UtcNow));
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
            var tableNumberExists = await _context.Tables.AnyAsync(existing => existing.Id != id && existing.TableNumber.ToLower() == normalizedTableNumber.ToLower());
            if (tableNumberExists)
            {
                return Conflict(new { Message = "A table with this number already exists." });
            }

            table.TableNumber = normalizedTableNumber;
            table.Type = tableType;
            table.HourlyRate = request.HourlyRate;
            table.Status = tableStatus;
            table.RealTimeStatus = tableStatus == TableManualStatus.Maintenance ? TableRealTimeStatus.Maintenance : table.RealTimeStatus;
            table.IsActive = request.IsActive;
            table.PositionX = request.PositionX;
            table.PositionY = request.PositionY;

            await _context.SaveChangesAsync();
            return await GetTableById(id);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null)
            {
                return NotFound(new { Message = "Table not found." });
            }

            var hasSessionHistory = await _context.TableSessions.AnyAsync(session => session.TableId == id);
            if (hasSessionHistory)
            {
                table.IsActive = false;
                table.Status = TableManualStatus.Maintenance;
                table.RealTimeStatus = TableRealTimeStatus.Maintenance;
                await _context.SaveChangesAsync();
                return Ok(new { Message = "Table archived because it already has session history." });
            }

            _context.Tables.Remove(table);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id:int}/walkin")]
        public async Task<IActionResult> StartWalkIn(int id, [FromBody] WalkInRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.GuestName))
            {
                return BadRequest(new { Message = "Guest name is required." });
            }

            var (success, message, sessionId) = await _tableSessionService.StartWalkInAsync(id, request.GuestName.Trim());
            return success
                ? Ok(new { Id = sessionId, Message = message })
                : BadRequest(new { Message = message });
        }

        [HttpPost("{id:int}/maintenance")]
        public async Task<IActionResult> SetMaintenance(int id)
        {
            var (success, message) = await _tableSessionService.SetTableMaintenanceAsync(id);
            return success ? Ok(new { Message = message }) : BadRequest(new { Message = message });
        }

        private static AdminTableResponse MapTable(
            BilliardTable table,
            IEnumerable<TableSession> activeSessions,
            IEnumerable<Reservation> upcomingReservations,
            DateTime now)
        {
            var currentSession = activeSessions
                .Where(session => session.TableId == table.Id)
                .OrderByDescending(session => session.StartedAt)
                .FirstOrDefault();

            var nextReservation = upcomingReservations
                .Where(reservation => reservation.RequestedTableType == table.Type &&
                                      reservation.BookingDate.Add(reservation.StartTime) > now)
                .OrderBy(reservation => reservation.BookingDate)
                .ThenBy(reservation => reservation.StartTime)
                .FirstOrDefault();

            var currentDurationHours = currentSession == null
                ? 0
                : Math.Max(0m, (decimal)((DateTime.UtcNow - currentSession.StartedAt).TotalHours));

            return new AdminTableResponse
            {
                Id = table.Id,
                TableNumber = table.TableNumber,
                Type = table.Type.ToString(),
                HourlyRate = table.HourlyRate,
                ManualStatus = table.Status.ToString(),
                DisplayStatus = GetDisplayStatus(table, currentSession, nextReservation, now),
                IsActive = table.IsActive,
                PositionX = table.PositionX,
                PositionY = table.PositionY,
                CurrentCustomerName = currentSession?.GuestName ?? currentSession?.User?.FullName,
                CurrentSessionStartedAt = currentSession?.StartedAt,
                NextBookingStartTime = nextReservation == null ? null : nextReservation.BookingDate.Add(nextReservation.StartTime),
                NextBookingId = nextReservation?.Id.ToString(),
                NextCustomerName = nextReservation?.User?.FullName,
                CurrentSessionAmount = currentSession == null
                    ? 0
                    : Math.Round(currentDurationHours * table.HourlyRate, 2) +
                      currentSession.FnBOrders.Sum(order => order.TotalAmount) +
                      currentSession.CoachingSessions.Sum(session => session.Cost)
            };
        }

        private static string GetDisplayStatus(BilliardTable table, TableSession? currentSession, Reservation? nextReservation, DateTime now)
        {
            if (!table.IsActive) return "Inactive";
            if (table.Status == TableManualStatus.Maintenance || table.RealTimeStatus == TableRealTimeStatus.Maintenance) return "Maintenance";
            if (currentSession != null || table.RealTimeStatus == TableRealTimeStatus.Occupied) return "InUse";
            if (nextReservation != null && nextReservation.BookingDate.Add(nextReservation.StartTime) <= now.AddMinutes(30)) return "Reserved";
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
