using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Hubs;
using BilliardsBooking.API.Models;
using BilliardsBooking.API.Services.StateMachines;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Services
{
    public interface ITableSessionService
    {
        Task<TableSession?> GetSessionAsync(Guid sessionId);
        Task<TableSession?> ResolveLegacySessionAsync(Guid legacyId);
        Task<(bool Success, string Message)> CheckInAsync(Guid reservationId, int tableId, string? actorUserId = null);
        Task<(bool Success, string Message, Guid? SessionId)> StartWalkInAsync(int tableId, string guestName, string? actorUserId = null);
        Task<(bool Success, string Message, CheckoutSummary? Summary)> CheckoutAsync(Guid sessionId, string paymentMethod, string? actorUserId = null);
        Task<(bool Success, string Message)> LinkCoachEarlyAsync(Guid sessionId, Guid coachingSessionId, string? actorUserId = null);
        Task<RunningTotalResponse?> GetRunningTotalAsync(Guid sessionId);
        Task<(bool Success, string Message)> AddFnBOrderAsync(Guid sessionId, List<FnBOrderRequest> items, string? actorUserId = null);
        Task<List<LinkableCoachSessionResponse>> GetLinkableCoachSessionsAsync(Guid sessionId);
        Task<List<UpcomingWarningResponse>> GetUpcomingWarningsAsync();
        Task<(bool Success, string Message)> TransferTableAsync(Guid sessionId, int newTableId, string reason, string? actorUserId = null);
        Task<(bool Success, string Message)> ExtendSessionAsync(Guid sessionId, int additionalMinutes, string? actorUserId = null);
        Task<CheckoutSummary?> GetInterimBillAsync(Guid sessionId);
        Task<(bool Success, string Message)> SetTableMaintenanceAsync(int tableId, string? actorUserId = null);
    }

    public partial class TableSessionService : ITableSessionService
    {
        private readonly AppDbContext _context;
        private readonly IReservationService _reservationService;
        private readonly IInvoiceService _invoiceService;
        private readonly IAuditService _auditService;
        private readonly IHubContext<TableStatusHub> _hubContext;

        public TableSessionService(
            AppDbContext context,
            IReservationService reservationService,
            IInvoiceService invoiceService,
            IAuditService auditService,
            IHubContext<TableStatusHub> hubContext)
        {
            _context = context;
            _reservationService = reservationService;
            _invoiceService = invoiceService;
            _auditService = auditService;
            _hubContext = hubContext;
        }

        public Task<TableSession?> GetSessionAsync(Guid sessionId) =>
            SessionQuery().FirstOrDefaultAsync(s => s.Id == sessionId);

        public async Task<TableSession?> ResolveLegacySessionAsync(Guid legacyId)
        {
            var session = await GetSessionAsync(legacyId);
            if (session != null) return session;

            var reservation = await _context.Reservations
                .Include(r => r.TableSession)
                .FirstOrDefaultAsync(r => r.Id == legacyId);

            return reservation?.TableSession == null
                ? null
                : await GetSessionAsync(reservation.TableSession.Id);
        }

        public async Task<(bool Success, string Message)> CheckInAsync(Guid reservationId, int tableId, string? actorUserId = null)
        {
            var reservation = await _context.Reservations
                .Include(r => r.TableSession)
                .FirstOrDefaultAsync(r => r.Id == reservationId);
            if (reservation == null) return (false, "Reservation not found.");
            if (reservation.Status != ReservationStatus.Confirmed) return (false, "Reservation must be confirmed to check in.");
            if (reservation.TableSession != null) return (false, "Reservation is already checked in.");

            var table = await _context.Tables.FindAsync(tableId);
            if (table == null || !table.IsActive) return (false, "Table not found.");
            if (table.Status == TableManualStatus.Maintenance || table.RealTimeStatus == TableRealTimeStatus.Maintenance)
                return (false, $"Table {table.TableNumber} is under maintenance.");

            var hasActiveSession = await _context.TableSessions.AnyAsync(s => s.TableId == tableId && s.Status == TableSessionStatus.Active);
            if (hasActiveSession || table.RealTimeStatus == TableRealTimeStatus.Occupied)
                return (false, $"Table {table.TableNumber} is currently occupied.");

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var session = new TableSession
                {
                    Id = Guid.NewGuid(),
                    TableId = tableId,
                    ReservationId = reservation.Id,
                    UserId = reservation.UserId,
                    Type = SessionType.Online,
                    StartedAt = DateTime.UtcNow,
                    Status = TableSessionStatus.Active
                };

                _context.TableSessions.Add(session);
                ReservationStateMachine.Transition(reservation, ReservationStatus.CheckedIn);
                table.RealTimeStatus = TableRealTimeStatus.Occupied;

                await _auditService.LogAsync(
                    AuditAction.SessionStarted,
                    nameof(TableSession),
                    session.Id.ToString(),
                    actorUserId,
                    newValue: new { session.TableId, session.ReservationId, session.UserId, session.StartedAt, session.Type });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                reservation.TableSessionId = session.Id;
                await _hubContext.Clients.Group($"floorplan-{reservation.BookingDate:yyyy-MM-dd}").SendAsync("TableStatusChanged", tableId, "InUse");
                await _hubContext.Clients.Group($"floorplan-{reservation.BookingDate:yyyy-MM-dd}").SendAsync("BookingAssigned", reservation.Id, tableId);
                return (true, "Check-in successful.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"An error occurred: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, Guid? SessionId)> StartWalkInAsync(int tableId, string guestName, string? actorUserId = null)
        {
            var table = await _context.Tables.FindAsync(tableId);
            if (table == null || !table.IsActive) return (false, "Table not found.", null);
            if (table.Status == TableManualStatus.Maintenance || table.RealTimeStatus == TableRealTimeStatus.Maintenance)
                return (false, "Table is under maintenance.", null);

            var hasActiveSession = await _context.TableSessions.AnyAsync(s => s.TableId == tableId && s.Status == TableSessionStatus.Active);
            if (hasActiveSession || table.RealTimeStatus == TableRealTimeStatus.Occupied) return (false, "Table is currently occupied.", null);

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var session = new TableSession
                {
                    Id = Guid.NewGuid(),
                    TableId = tableId,
                    GuestName = guestName,
                    Type = SessionType.WalkIn,
                    StartedAt = DateTime.UtcNow,
                    Status = TableSessionStatus.Active
                };

                table.RealTimeStatus = TableRealTimeStatus.Occupied;
                _context.TableSessions.Add(session);

                await _auditService.LogAsync(
                    AuditAction.SessionStarted,
                    nameof(TableSession),
                    session.Id.ToString(),
                    actorUserId,
                    newValue: new { session.TableId, session.GuestName, session.StartedAt, session.Type });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                await _hubContext.Clients.Group($"floorplan-{DateTime.UtcNow:yyyy-MM-dd}").SendAsync("TableStatusChanged", tableId, "InUse");
                return (true, "Walk-in session started.", session.Id);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"An error occurred: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, CheckoutSummary? Summary)> CheckoutAsync(Guid sessionId, string paymentMethod, string? actorUserId = null)
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null) return (false, "Session not found.", null);
            if (session.Status != TableSessionStatus.Active) return (false, "Session is not active.", null);

            if (!Enum.TryParse<PaymentMethod>(paymentMethod, true, out var parsedMethod))
                parsedMethod = PaymentMethod.Cash;

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var previousState = new { session.Status, session.EndedAt };
                session.EndedAt = DateTime.UtcNow;
                TableSessionStateMachine.Transition(session, TableSessionStatus.Completed);

                if (session.Table != null)
                {
                    session.Table.RealTimeStatus = session.Table.Status == TableManualStatus.Maintenance
                        ? TableRealTimeStatus.Maintenance
                        : TableRealTimeStatus.Available;
                }

                if (session.Reservation != null)
                {
                    ReservationStateMachine.Transition(session.Reservation, ReservationStatus.Completed);
                    foreach (var slot in session.Reservation.Slots)
                        slot.IsActive = false;
                }

                foreach (var coachSession in session.CoachingSessions)
                    coachSession.IsCompleted = true;

                await _auditService.LogAsync(
                    AuditAction.SessionCheckedOut,
                    nameof(TableSession),
                    session.Id.ToString(),
                    actorUserId,
                    previousState,
                    new { session.Status, session.EndedAt });

                await _context.SaveChangesAsync();

                var invoice = await _invoiceService.BuildInvoiceAsync(session.Id, parsedMethod, actorUserId);
                if (invoice == null)
                {
                    await tx.RollbackAsync();
                    return (false, "Could not create invoice.", null);
                }

                await tx.CommitAsync();
                session.InvoiceId = invoice.Id;

                await _hubContext.Clients.Group($"floorplan-{session.StartedAt:yyyy-MM-dd}").SendAsync("TableStatusChanged", session.TableId, "Available");
                return (true, "Checkout complete.", await BuildCheckoutSummaryAsync(session.Id));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return (false, $"An error occurred: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message)> LinkCoachEarlyAsync(Guid sessionId, Guid coachingSessionId, string? actorUserId = null)
        {
            var session = await _context.TableSessions.FindAsync(sessionId);
            if (session == null || session.Status != TableSessionStatus.Active)
                return (false, "Session does not exist or is not active.");

            var coachSession = await _context.CoachingSessions.FindAsync(coachingSessionId);
            if (coachSession == null) return (false, "Coaching session does not exist.");
            if (coachSession.TableSessionId != null) return (false, "Coaching session is already linked to another table session.");
            if (session.UserId.HasValue && coachSession.StudentUserId != session.UserId.Value) return (false, "Coaching session belongs to a different customer.");

            coachSession.TableSessionId = sessionId;
            coachSession.LinkedAt = DateTime.UtcNow;

            await _auditService.LogAsync(
                AuditAction.SessionCoachLinked,
                nameof(TableSession),
                sessionId.ToString(),
                actorUserId,
                newValue: new { coachingSessionId, coachSession.LinkedAt });

            await _context.SaveChangesAsync();

            var runningTotal = await GetRunningTotalAsync(sessionId);
            await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("RunningTotalUpdated", runningTotal);
            return (true, "Coach linked to the running table session.");
        }
    }
}

namespace BilliardsBooking.API.Services
{
    public partial class TableSessionService
    {
        public async Task<RunningTotalResponse?> GetRunningTotalAsync(Guid sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null || session.Table == null) return null;

            var effectiveEnd = session.EndedAt ?? DateTime.UtcNow;
            var durationHours = Math.Max(0m, (decimal)(effectiveEnd - session.StartedAt).TotalHours);
            var tableTimeCost = Math.Round(durationHours * session.Table.HourlyRate, 2);
            var fnbTotal = session.FnBOrders.Sum(o => o.TotalAmount);
            var coachingTotal = session.CoachingSessions.Sum(cs => cs.Cost > 0
                ? cs.Cost
                : Math.Round((decimal)(cs.EndTime - cs.StartTime).TotalHours * (cs.Coach?.HourlyRate ?? 0m), 2));

            var activeMembership = session.UserId == null
                ? null
                : await _context.UserMemberships.Include(um => um.MembershipPlan).FirstOrDefaultAsync(um => um.UserId == session.UserId && um.IsActive);
            var discountAmount = Math.Round(tableTimeCost * ((activeMembership?.MembershipPlan?.TableDiscountPercent ?? 0) / 100m), 2);

            var depositApplied = 0m;
            if (session.Reservation != null)
            {
                depositApplied = session.Reservation.Payments
                    .Where(p => p.Status == PaymentStatus.Completed && (p.Type == PaymentType.Deposit || p.Type == PaymentType.Refund))
                    .Sum(p => p.Amount);
                if (depositApplied < 0) depositApplied = 0;
            }

            var subtotal = Math.Max(0, Math.Round(tableTimeCost + fnbTotal + coachingTotal - discountAmount, 2));
            return new RunningTotalResponse
            {
                TableTimeCost = tableTimeCost,
                FnBTotal = fnbTotal,
                CoachingTotal = coachingTotal,
                Subtotal = subtotal,
                DepositApplied = depositApplied,
                EstimatedBalanceDue = Math.Max(0, subtotal - depositApplied),
                Note = "Time is estimated from check-in and may differ slightly at checkout."
            };
        }

        public async Task<(bool Success, string Message)> AddFnBOrderAsync(Guid sessionId, List<FnBOrderRequest> items, string? actorUserId = null)
        {
            var session = await _context.TableSessions.FindAsync(sessionId);
            if (session == null || session.Status != TableSessionStatus.Active)
                return (false, "Session does not exist or is not active.");
            if (items.Count == 0) return (false, "No F&B items were provided.");

            var order = new FnBOrder { Id = Guid.NewGuid(), TableSessionId = sessionId };
            decimal totalAmount = 0;

            foreach (var requestItem in items)
            {
                var menuItem = await _context.FnBMenuItems.FindAsync(requestItem.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable) continue;

                totalAmount += menuItem.Price * requestItem.Quantity;
                order.Items.Add(new FnBOrderItem
                {
                    MenuItemId = menuItem.Id,
                    Quantity = requestItem.Quantity,
                    UnitPrice = menuItem.Price
                });
            }

            if (order.Items.Count == 0) return (false, "No valid F&B items were provided.");

            order.TotalAmount = totalAmount;
            _context.FnBOrders.Add(order);

            await _auditService.LogAsync(
                AuditAction.SessionFnBAdded,
                nameof(TableSession),
                sessionId.ToString(),
                actorUserId,
                newValue: new { order.TotalAmount, ItemCount = order.Items.Count });

            await _context.SaveChangesAsync();

            var runningTotal = await GetRunningTotalAsync(sessionId);
            await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("RunningTotalUpdated", runningTotal);
            return (true, "F&B order added to the session.");
        }

        public async Task<List<LinkableCoachSessionResponse>> GetLinkableCoachSessionsAsync(Guid sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null) return new List<LinkableCoachSessionResponse>();

            var query = _context.CoachingSessions
                .Include(cs => cs.Coach).ThenInclude(c => c!.User)
                .Where(cs => cs.TableSessionId == null && !cs.IsCompleted && cs.CancelledAt == null && cs.SessionDate == session.StartedAt.Date);

            if (session.UserId.HasValue)
            {
                var userId = session.UserId.Value;
                query = query.Where(cs => cs.StudentUserId == userId);
            }

            var coachSessions = await query.OrderBy(cs => cs.StartTime).ToListAsync();
            return coachSessions.Select(cs => new LinkableCoachSessionResponse
            {
                Id = cs.Id,
                CoachId = cs.CoachId,
                CoachName = cs.Coach?.User?.FullName ?? "Coach",
                SessionDate = cs.SessionDate,
                StartTime = cs.StartTime.ToString(@"hh\:mm"),
                EndTime = cs.EndTime.ToString(@"hh\:mm"),
                Cost = cs.Cost
            }).ToList();
        }

        public async Task<List<UpcomingWarningResponse>> GetUpcomingWarningsAsync()
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddMinutes(15);
            var sessions = await SessionQuery()
                .Where(s => s.Status == TableSessionStatus.Active && s.ReservationId != null && s.Reservation != null)
                .ToListAsync();

            var warnings = new List<UpcomingWarningResponse>();
            foreach (var session in sessions)
            {
                if (session.Reservation == null) continue;

                var scheduledEnd = session.Reservation.BookingDate.Add(session.Reservation.EndTime);
                if (scheduledEnd < now || scheduledEnd > cutoff) continue;

                var hasIncoming = await _context.Reservations.AnyAsync(r =>
                    r.Status == ReservationStatus.Confirmed &&
                    r.BookingDate == session.Reservation.BookingDate &&
                    r.RequestedTableType == session.Reservation.RequestedTableType &&
                    r.StartTime == session.Reservation.EndTime);
                if (!hasIncoming) continue;

                var occupancy = await _context.ReservationSlots.CountAsync(slot =>
                    slot.SlotDate == session.Reservation.BookingDate &&
                    slot.SlotStart == session.Reservation.EndTime &&
                    slot.RequestedTableType == session.Reservation.RequestedTableType &&
                    slot.IsActive);

                var totalTables = await _context.Tables.CountAsync(t =>
                    t.Type == session.Reservation.RequestedTableType &&
                    t.IsActive &&
                    t.Status != TableManualStatus.Maintenance);
                var capacity = Math.Max(0, totalTables - await _reservationService.GetWalkInBufferAsync(session.Reservation.RequestedTableType, scheduledEnd));

                if (occupancy < capacity) continue;

                warnings.Add(new UpcomingWarningResponse
                {
                    BookingId = session.ReservationId ?? session.Id,
                    TableId = session.TableId,
                    TableNumber = session.Table?.TableNumber,
                    CurrentCustomerName = session.GuestName ?? session.User?.FullName,
                    EndsAt = scheduledEnd,
                    Category = session.Reservation.RequestedTableType,
                    MinutesRemaining = (int)Math.Max(0, (scheduledEnd - now).TotalMinutes)
                });
            }

            return warnings.OrderBy(w => w.EndsAt).ToList();
        }

        public async Task<(bool Success, string Message)> TransferTableAsync(Guid sessionId, int newTableId, string reason, string? actorUserId = null)
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null || session.Status != TableSessionStatus.Active || session.Table == null)
                return (false, "Active session not found.");

            var newTable = await _context.Tables.FindAsync(newTableId);
            if (newTable == null || !newTable.IsActive) return (false, "Target table not found.");
            if (newTable.Status == TableManualStatus.Maintenance || newTable.RealTimeStatus == TableRealTimeStatus.Maintenance)
                return (false, "Target table is under maintenance.");

            var newTableInUse = await _context.TableSessions.AnyAsync(s => s.TableId == newTableId && s.Status == TableSessionStatus.Active);
            if (newTableInUse || newTable.RealTimeStatus == TableRealTimeStatus.Occupied)
                return (false, "Target table is not available.");

            var oldTableId = session.TableId;
            session.Table.RealTimeStatus = TableRealTimeStatus.Available;
            newTable.RealTimeStatus = TableRealTimeStatus.Occupied;
            session.TableId = newTableId;

            await _auditService.LogAsync(
                AuditAction.SessionTableChanged,
                nameof(TableSession),
                sessionId.ToString(),
                actorUserId,
                new { TableId = oldTableId },
                new { TableId = newTableId },
                reason);

            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group($"floorplan-{session.StartedAt:yyyy-MM-dd}").SendAsync("TableStatusChanged", oldTableId, "Available");
            await _hubContext.Clients.Group($"floorplan-{session.StartedAt:yyyy-MM-dd}").SendAsync("TableStatusChanged", newTableId, "InUse");
            return (true, "Table transferred successfully.");
        }

        public async Task<(bool Success, string Message)> ExtendSessionAsync(Guid sessionId, int additionalMinutes, string? actorUserId = null)
        {
            if (additionalMinutes <= 0) return (false, "Additional minutes must be greater than zero.");

            var session = await GetSessionAsync(sessionId);
            if (session == null || session.Status != TableSessionStatus.Active) return (false, "Active session not found.");

            if (session.Reservation != null)
            {
                var extensionStart = DateTime.UtcNow;
                var extensionEnd = extensionStart.AddMinutes(additionalMinutes);
                var slotsNeeded = BuildHalfHourSlots(extensionStart.TimeOfDay, extensionEnd.TimeOfDay);
                var occupancy = await _context.ReservationSlots
                    .Where(slot => slot.RequestedTableType == session.Reservation.RequestedTableType &&
                                   slot.SlotDate == session.Reservation.BookingDate &&
                                   slot.IsActive &&
                                   slotsNeeded.Contains(slot.SlotStart))
                    .GroupBy(slot => slot.SlotStart)
                    .Select(group => new { group.Key, Count = group.Count() })
                    .ToListAsync();

                var totalTables = await _context.Tables.CountAsync(t =>
                    t.Type == session.Reservation.RequestedTableType &&
                    t.IsActive &&
                    t.Status != TableManualStatus.Maintenance);
                var capacity = Math.Max(0, totalTables - await _reservationService.GetWalkInBufferAsync(session.Reservation.RequestedTableType, extensionStart));

                if (occupancy.Any(item => item.Count >= capacity))
                    return (false, "Session cannot be extended because the next slot is fully reserved.");
            }

            await _auditService.LogAsync(
                AuditAction.SessionTableChanged,
                nameof(TableSession),
                sessionId.ToString(),
                actorUserId,
                newValue: new { AdditionalMinutes = additionalMinutes },
                notes: "Session extension approved.");

            await _context.SaveChangesAsync();
            return (true, "Session extension recorded.");
        }

        public Task<CheckoutSummary?> GetInterimBillAsync(Guid sessionId) => BuildCheckoutSummaryAsync(sessionId);

        public async Task<(bool Success, string Message)> SetTableMaintenanceAsync(int tableId, string? actorUserId = null)
        {
            var table = await _context.Tables.FindAsync(tableId);
            if (table == null) return (false, "Table not found.");

            var activeSession = await _context.TableSessions.AnyAsync(s => s.TableId == tableId && s.Status == TableSessionStatus.Active);
            if (activeSession) return (false, "Table currently has an active session and cannot be moved to maintenance.");

            var now = DateTime.UtcNow;
            var upcomingReservations = await _context.Reservations.CountAsync(r =>
                r.Status == ReservationStatus.Confirmed &&
                r.RequestedTableType == table.Type &&
                r.BookingDate == now.Date &&
                r.StartTime >= now.TimeOfDay &&
                r.StartTime <= now.AddHours(4).TimeOfDay);

            table.RealTimeStatus = TableRealTimeStatus.Maintenance;
            table.Status = TableManualStatus.Maintenance;

            await _auditService.LogAsync(
                AuditAction.TableStatusOverride,
                nameof(BilliardTable),
                tableId.ToString(),
                actorUserId,
                newValue: new { table.RealTimeStatus, table.Status });

            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group($"floorplan-{now:yyyy-MM-dd}").SendAsync("TableStatusChanged", tableId, "Maintenance");

            var message = upcomingReservations > 0
                ? $"Table moved to maintenance. Warning: {upcomingReservations} confirmed reservation(s) exist in this category within the next 4 hours."
                : "Table moved to maintenance.";

            return (true, message);
        }
    }
}

namespace BilliardsBooking.API.Services
{
    public partial class TableSessionService
    {
        private IQueryable<TableSession> SessionQuery()
        {
            return _context.TableSessions
                .Include(s => s.Table)
                .Include(s => s.User)
                .Include(s => s.Reservation)
                    .ThenInclude(r => r!.Slots)
                .Include(s => s.Reservation)
                    .ThenInclude(r => r!.Payments)
                .Include(s => s.FnBOrders)
                    .ThenInclude(o => o.Items)
                        .ThenInclude(i => i.MenuItem)
                .Include(s => s.CoachingSessions)
                    .ThenInclude(cs => cs.Coach)
                        .ThenInclude(c => c!.User)
                .Include(s => s.Invoice)
                    .ThenInclude(i => i!.Payments);
        }

        private async Task<CheckoutSummary?> BuildCheckoutSummaryAsync(Guid sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null || session.Table == null) return null;

            var runningTotal = await GetRunningTotalAsync(sessionId);
            if (runningTotal == null) return null;

            var effectiveEnd = session.EndedAt ?? DateTime.UtcNow;
            var actualDurationHours = Math.Max(0m, (decimal)(effectiveEnd - session.StartedAt).TotalHours);

            return new CheckoutSummary
            {
                BookingId = session.ReservationId ?? session.Id,
                TableNumber = session.Table.TableNumber,
                CustomerName = session.GuestName ?? session.User?.FullName ?? "Guest",
                BookingType = session.Type.ToString(),
                CheckedInAt = session.StartedAt,
                CheckedOutAt = effectiveEnd,
                ActualDurationHours = Math.Round(actualDurationHours, 2),
                TableCost = runningTotal.TableTimeCost,
                FnBCost = runningTotal.FnBTotal,
                CoachingCost = runningTotal.CoachingTotal,
                DiscountAmount = Math.Max(0, runningTotal.TableTimeCost + runningTotal.FnBTotal + runningTotal.CoachingTotal - runningTotal.Subtotal),
                DepositAmount = runningTotal.DepositApplied,
                TotalCost = runningTotal.Subtotal,
                AmountDue = runningTotal.EstimatedBalanceDue
            };
        }

        private static List<TimeSpan> BuildHalfHourSlots(TimeSpan start, TimeSpan end)
        {
            var slots = new List<TimeSpan>();
            var current = TimeSpan.FromMinutes(Math.Floor(start.TotalMinutes / 30) * 30);
            while (current < end && current < TimeSpan.FromHours(24))
            {
                slots.Add(current);
                current = current.Add(TimeSpan.FromMinutes(30));
            }

            return slots;
        }
    }
}
