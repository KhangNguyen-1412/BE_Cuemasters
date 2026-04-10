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
    public interface IReservationService
    {
        Task<(bool Success, string Message, Guid? ReservationId)> CreateReservationAsync(Guid userId, CreateBookingRequest request, string? actorUserId = null);
        Task<ReservationListResponse> GetReservationsForUserAsync(Guid userId, int page, int pageSize, string? status = null);
        Task<ReservationResponse?> GetReservationResponseAsync(Guid reservationId, Guid? userId = null);
        Task<Reservation?> GetReservationAsync(Guid reservationId);
        Task<(bool Success, string Message)> CancelReservationAsync(Guid reservationId, Guid userId, string? actorUserId = null);
        Task<(bool Success, string Message)> RescheduleReservationAsync(Guid reservationId, Guid userId, RescheduleRequest request, string? actorUserId = null);
        Task<CategoryAvailabilityResponse> GetCategoryAvailabilityAsync(TableType tableType, DateTime date);
        Task<List<PendingCheckinResponse>> GetPendingCheckinsAsync(DateTime date);
        Task<int> GetWalkInBufferAsync(TableType tableType, DateTime dateTime);
        Task<(bool Success, string Message)> MarkNoShowAsync(Guid reservationId, string? actorUserId = null);
        Task<(bool Success, string Message)> CreateRefundAsync(Guid reservationId, decimal amount, string reason, string? actorUserId = null);
    }

    public class ReservationService : IReservationService
    {
        private const decimal DepositAmount = 50_000m;
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IHubContext<TableStatusHub> _hubContext;

        public ReservationService(
            AppDbContext context,
            IAuditService auditService,
            IHubContext<TableStatusHub> hubContext)
        {
            _context = context;
            _auditService = auditService;
            _hubContext = hubContext;
        }

        public async Task<(bool Success, string Message, Guid? ReservationId)> CreateReservationAsync(
            Guid userId,
            CreateBookingRequest request,
            string? actorUserId = null)
        {
            if (!TimeSpan.TryParse(request.StartTime, out var startTime) ||
                !TimeSpan.TryParse(request.EndTime, out var endTime))
            {
                return (false, "Invalid time format (Use HH:mm).", null);
            }

            if (startTime >= endTime)
            {
                return (false, "End time must be after start time.", null);
            }

            if (startTime.Ticks % TimeSpan.FromMinutes(30).Ticks != 0 ||
                endTime.Ticks % TimeSpan.FromMinutes(30).Ticks != 0)
            {
                return (false, "Times must align to 30-minute boundaries.", null);
            }

            var activeMembership = await _context.UserMemberships
                .Include(um => um.MembershipPlan)
                .FirstOrDefaultAsync(um => um.UserId == userId && um.IsActive);

            var daysInAdvance = (request.BookingDate.Date - DateTime.UtcNow.Date).Days;
            var maxAdvanceDays = activeMembership?.MembershipPlan?.MaxAdvanceBookingDays ?? 0;
            if (daysInAdvance > maxAdvanceDays)
            {
                var tierName = activeMembership?.MembershipPlan?.Name ?? "Guest";
                return (false, $"{tierName} tier can only book up to {maxAdvanceDays} day(s) in advance.", null);
            }

            var hasActiveReservation = await _context.Reservations.AnyAsync(r =>
                r.UserId == userId &&
                (r.Status == ReservationStatus.Pending ||
                 r.Status == ReservationStatus.Confirmed ||
                 r.Status == ReservationStatus.CheckedIn));
            if (hasActiveReservation)
            {
                return (false, "You already have an active reservation.", null);
            }

            var hasActiveSession = await _context.TableSessions.AnyAsync(s =>
                s.UserId == userId &&
                s.Status == TableSessionStatus.Active);
            if (hasActiveSession)
            {
                return (false, "You already have an active table session.", null);
            }

            var totalTablesInCategory = await _context.Tables.CountAsync(t =>
                t.Type == request.RequestedTableType &&
                t.IsActive &&
                t.Status != TableManualStatus.Maintenance);
            if (totalTablesInCategory == 0)
            {
                return (false, "No tables are available for this category.", null);
            }

            var effectiveCapacity = Math.Max(
                0,
                totalTablesInCategory - await GetWalkInBufferAsync(request.RequestedTableType, request.BookingDate.Date.Add(startTime)));
            if (effectiveCapacity == 0)
            {
                return (false, "This category is reserved for walk-ins at the selected time.", null);
            }

            var slotsNeeded = BuildHalfHourSlots(startTime, endTime);
            var slotOccupancy = await _context.ReservationSlots
                .Where(slot => slot.RequestedTableType == request.RequestedTableType &&
                               slot.SlotDate == request.BookingDate.Date &&
                               slot.IsActive &&
                               slotsNeeded.Contains(slot.SlotStart))
                .GroupBy(slot => slot.SlotStart)
                .Select(group => new { SlotStart = group.Key, Count = group.Count() })
                .ToListAsync();

            if (slotOccupancy.Any(item => item.Count >= effectiveCapacity))
            {
                return (false, "The selected time is fully booked for this table category.", null);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = new Reservation
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    RequestedTableType = request.RequestedTableType,
                    BookingDate = request.BookingDate.Date,
                    StartTime = startTime,
                    EndTime = endTime,
                    DepositAmount = DepositAmount
                };

                ReservationStateMachine.Transition(reservation, ReservationStatus.Confirmed);
                _context.Reservations.Add(reservation);

                foreach (var slotStart in slotsNeeded)
                {
                    _context.ReservationSlots.Add(new ReservationSlot
                    {
                        ReservationId = reservation.Id,
                        RequestedTableType = request.RequestedTableType,
                        SlotDate = reservation.BookingDate,
                        SlotStart = slotStart,
                        IsActive = true
                    });
                }

                var depositPayment = new Payment
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ReservationId = reservation.Id,
                    Type = PaymentType.Deposit,
                    Method = PaymentMethod.Cash,
                    Status = PaymentStatus.Completed,
                    Amount = DepositAmount,
                    Notes = "Deposit recorded at reservation confirmation.",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };

                _context.Payments.Add(depositPayment);

                await _auditService.LogAsync(
                    AuditAction.ReservationCreated,
                    nameof(Reservation),
                    reservation.Id.ToString(),
                    actorUserId ?? userId.ToString(),
                    newValue: new
                    {
                        reservation.RequestedTableType,
                        reservation.BookingDate,
                        reservation.StartTime,
                        reservation.EndTime,
                        reservation.DepositAmount
                    });

                await _auditService.LogAsync(
                    AuditAction.ReservationConfirmed,
                    nameof(Reservation),
                    reservation.Id.ToString(),
                    actorUserId ?? userId.ToString(),
                    previousValue: new { Status = ReservationStatus.Pending },
                    newValue: new { reservation.Status, reservation.ConfirmedAt });

                await _auditService.LogAsync(
                    AuditAction.PaymentRecorded,
                    nameof(Reservation),
                    reservation.Id.ToString(),
                    actorUserId ?? userId.ToString(),
                    newValue: new { depositPayment.Type, depositPayment.Amount, depositPayment.Status });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _hubContext.Clients
                    .Group($"floorplan-{reservation.BookingDate:yyyy-MM-dd}")
                    .SendAsync("CategoryCapacityChanged", reservation.RequestedTableType.ToString(), reservation.BookingDate);

                return (true, "Reservation created successfully.", reservation.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"An error occurred: {ex.Message}", null);
            }
        }

        public async Task<ReservationListResponse> GetReservationsForUserAsync(Guid userId, int page, int pageSize, string? status = null)
        {
            var query = _context.Reservations
                .Include(r => r.TableSession)
                .Where(r => r.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<ReservationStatus>(status, true, out var reservationStatus))
                {
                    query = query.Where(r => r.Status == reservationStatus);
                }
                else if (Enum.TryParse<BookingStatus>(status, true, out var bookingStatus))
                {
                    query = bookingStatus switch
                    {
                        BookingStatus.InProgress => query.Where(r => r.Status == ReservationStatus.CheckedIn),
                        BookingStatus.Completed => query.Where(r => r.Status == ReservationStatus.Completed),
                        BookingStatus.Cancelled => query.Where(r => r.Status == ReservationStatus.Cancelled),
                        BookingStatus.NoShow => query.Where(r => r.Status == ReservationStatus.NoShow),
                        BookingStatus.Confirmed => query.Where(r => r.Status == ReservationStatus.Confirmed),
                        _ => query
                    };
                }
            }

            query = query.OrderByDescending(r => r.BookingDate).ThenByDescending(r => r.StartTime);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReservationResponse
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    RequestedTableType = r.RequestedTableType,
                    BookingDate = r.BookingDate,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    Status = r.Status.ToString(),
                    DepositAmount = r.DepositAmount,
                    DepositForfeited = r.DepositForfeited,
                    CreatedAt = r.CreatedAt,
                    CancelledAt = r.CancelledAt,
                    ConfirmedAt = r.ConfirmedAt,
                    TableSessionId = r.TableSession != null ? r.TableSession.Id : null
                })
                .ToListAsync();

            return new ReservationListResponse
            {
                Items = items,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
            };
        }

        public async Task<ReservationResponse?> GetReservationResponseAsync(Guid reservationId, Guid? userId = null)
        {
            var query = _context.Reservations.Include(r => r.TableSession).AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(r => r.UserId == userId.Value);
            }

            var reservation = await query.FirstOrDefaultAsync(r => r.Id == reservationId);
            if (reservation == null)
            {
                return null;
            }

            return new ReservationResponse
            {
                Id = reservation.Id,
                UserId = reservation.UserId,
                RequestedTableType = reservation.RequestedTableType,
                BookingDate = reservation.BookingDate,
                StartTime = reservation.StartTime,
                EndTime = reservation.EndTime,
                Status = reservation.Status.ToString(),
                DepositAmount = reservation.DepositAmount,
                DepositForfeited = reservation.DepositForfeited,
                CreatedAt = reservation.CreatedAt,
                CancelledAt = reservation.CancelledAt,
                ConfirmedAt = reservation.ConfirmedAt,
                TableSessionId = reservation.TableSession?.Id
            };
        }

        public Task<Reservation?> GetReservationAsync(Guid reservationId)
        {
            return _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Slots)
                .Include(r => r.Payments)
                .Include(r => r.TableSession)
                .FirstOrDefaultAsync(r => r.Id == reservationId);
        }

        public async Task<(bool Success, string Message)> CancelReservationAsync(Guid reservationId, Guid userId, string? actorUserId = null)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Slots)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId);

            if (reservation == null)
            {
                return (false, "Reservation not found.");
            }

            if (reservation.Status == ReservationStatus.Cancelled)
            {
                return (true, "Reservation already cancelled.");
            }

            if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
            {
                return (false, "Only pending or confirmed reservations can be cancelled.");
            }

            var startDateTime = reservation.BookingDate.Add(reservation.StartTime);
            var hoursUntilStart = (startDateTime - DateTime.UtcNow).TotalHours;

            decimal refundPercent;
            if (hoursUntilStart >= 4)
            {
                refundPercent = 1m;
            }
            else if (hoursUntilStart >= 2)
            {
                refundPercent = 0.5m;
            }
            else
            {
                return (false, "Reservations cannot be cancelled within 2 hours of start time.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var previousState = new { reservation.Status, reservation.CancelledAt, reservation.DepositForfeited };
                ReservationStateMachine.Transition(reservation, ReservationStatus.Cancelled);

                foreach (var slot in reservation.Slots)
                {
                    slot.IsActive = false;
                }

                reservation.DepositForfeited = refundPercent < 1m;

                var refundAmount = Math.Round(reservation.DepositAmount * refundPercent, 2);
                if (refundAmount > 0)
                {
                    _context.Payments.Add(new Payment
                    {
                        Id = Guid.NewGuid(),
                        UserId = reservation.UserId,
                        ReservationId = reservation.Id,
                        Type = PaymentType.Refund,
                        Method = PaymentMethod.Cash,
                        Status = PaymentStatus.Completed,
                        Amount = -refundAmount,
                        Notes = $"Cancellation refund ({refundPercent:P0}).",
                        CreatedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow
                    });
                }

                await _auditService.LogAsync(
                    AuditAction.ReservationCancelled,
                    nameof(Reservation),
                    reservation.Id.ToString(),
                    actorUserId ?? userId.ToString(),
                    previousState,
                    new
                    {
                        reservation.Status,
                        reservation.CancelledAt,
                        reservation.DepositForfeited,
                        RefundAmount = refundAmount
                    });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _hubContext.Clients
                    .Group($"floorplan-{reservation.BookingDate:yyyy-MM-dd}")
                    .SendAsync("CategoryCapacityChanged", reservation.RequestedTableType.ToString(), reservation.BookingDate);

                return (true, refundPercent switch
                {
                    1m => "Reservation cancelled. Full deposit refunded.",
                    0.5m => "Reservation cancelled. 50% of the deposit refunded.",
                    _ => "Reservation cancelled."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"An error occurred: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> RescheduleReservationAsync(
            Guid reservationId,
            Guid userId,
            RescheduleRequest request,
            string? actorUserId = null)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Slots)
                .FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId);

            if (reservation == null)
            {
                return (false, "Reservation not found.");
            }

            if (reservation.Status != ReservationStatus.Confirmed)
            {
                return (false, "Only confirmed reservations can be rescheduled.");
            }

            if (reservation.BookingDate.Add(reservation.StartTime) - DateTime.UtcNow < TimeSpan.FromHours(2))
            {
                return (false, "Reservations can only be rescheduled at least 2 hours in advance.");
            }

            var newStartTime = request.NewStartTime.TimeOfDay;
            var newEndTime = request.NewEndTime.TimeOfDay;
            var newDate = request.NewStartTime.Date;

            if (newStartTime >= newEndTime)
            {
                return (false, "End time must be after start time.");
            }

            var slotsNeeded = BuildHalfHourSlots(newStartTime, newEndTime);
            var totalTables = await _context.Tables.CountAsync(t =>
                t.Type == reservation.RequestedTableType &&
                t.IsActive &&
                t.Status != TableManualStatus.Maintenance);
            var effectiveCapacity = Math.Max(
                0,
                totalTables - await GetWalkInBufferAsync(reservation.RequestedTableType, newDate.Add(newStartTime)));

            var slotOccupancy = await _context.ReservationSlots
                .Where(slot => slot.RequestedTableType == reservation.RequestedTableType &&
                               slot.SlotDate == newDate &&
                               slot.IsActive &&
                               slot.ReservationId != reservationId &&
                               slotsNeeded.Contains(slot.SlotStart))
                .GroupBy(slot => slot.SlotStart)
                .Select(group => new { SlotStart = group.Key, Count = group.Count() })
                .ToListAsync();

            if (slotOccupancy.Any(item => item.Count >= effectiveCapacity))
            {
                return (false, "The selected replacement time is not available.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var previousState = new
                {
                    reservation.BookingDate,
                    reservation.StartTime,
                    reservation.EndTime
                };

                foreach (var slot in reservation.Slots)
                {
                    slot.IsActive = false;
                }

                reservation.BookingDate = newDate;
                reservation.StartTime = newStartTime;
                reservation.EndTime = newEndTime;

                foreach (var slotStart in slotsNeeded)
                {
                    _context.ReservationSlots.Add(new ReservationSlot
                    {
                        ReservationId = reservation.Id,
                        RequestedTableType = reservation.RequestedTableType,
                        SlotDate = newDate,
                        SlotStart = slotStart,
                        IsActive = true
                    });
                }

                await _auditService.LogAsync(
                    AuditAction.ReservationConfirmed,
                    nameof(Reservation),
                    reservation.Id.ToString(),
                    actorUserId ?? userId.ToString(),
                    previousState,
                    new { reservation.BookingDate, reservation.StartTime, reservation.EndTime },
                    notes: "Reservation rescheduled.");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Reservation rescheduled successfully.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"An error occurred: {ex.Message}");
            }
        }

        public async Task<CategoryAvailabilityResponse> GetCategoryAvailabilityAsync(TableType tableType, DateTime date)
        {
            var totalTables = await _context.Tables.CountAsync(t =>
                t.Type == tableType &&
                t.IsActive &&
                t.Status != TableManualStatus.Maintenance);

            var dayStart = TimeSpan.FromHours(8);
            var dayEnd = TimeSpan.FromHours(24);
            var slots = new List<CategoryAvailabilitySlot>();

            var reservationSlots = await _context.ReservationSlots
                .Where(slot => slot.RequestedTableType == tableType &&
                               slot.SlotDate == date.Date &&
                               slot.IsActive)
                .GroupBy(slot => slot.SlotStart)
                .Select(group => new { SlotStart = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.SlotStart, item => item.Count);

            var buffer = await GetWalkInBufferAsync(tableType, date.Date.Add(TimeSpan.FromHours(12)));
            var onlineCapacity = Math.Max(0, totalTables - buffer);

            for (var time = dayStart; time < dayEnd; time = time.Add(TimeSpan.FromMinutes(30)))
            {
                var used = reservationSlots.TryGetValue(time, out var count) ? count : 0;
                slots.Add(new CategoryAvailabilitySlot
                {
                    StartTime = time.ToString(@"hh\:mm"),
                    EndTime = time.Add(TimeSpan.FromMinutes(30)).ToString(@"hh\:mm"),
                    Available = Math.Max(0, onlineCapacity - used),
                    Capacity = onlineCapacity
                });
            }

            return new CategoryAvailabilityResponse
            {
                TableType = tableType,
                Date = date.Date,
                TotalTables = totalTables,
                BufferSize = buffer,
                OnlineCapacity = onlineCapacity,
                Slots = slots
            };
        }

        public async Task<List<PendingCheckinResponse>> GetPendingCheckinsAsync(DateTime date)
        {
            var reservations = await _context.Reservations
                .Include(r => r.User)
                .Where(r => r.BookingDate == date.Date &&
                            r.Status == ReservationStatus.Confirmed &&
                            r.TableSession == null)
                .OrderBy(r => r.StartTime)
                .ToListAsync();

            var availableByCategory = new Dictionary<TableType, int>();
            foreach (var category in reservations.Select(r => r.RequestedTableType).Distinct())
            {
                availableByCategory[category] = await _context.Tables.CountAsync(t =>
                    t.Type == category &&
                    t.IsActive &&
                    t.Status != TableManualStatus.Maintenance &&
                    t.RealTimeStatus != TableRealTimeStatus.Occupied);
            }

            return reservations.Select(r => new PendingCheckinResponse
            {
                BookingId = r.Id,
                UserFullName = r.User?.FullName,
                UserEmail = r.User?.Email,
                RequestedTableType = r.RequestedTableType,
                StartTime = r.BookingDate.Add(r.StartTime),
                EndTime = r.BookingDate.Add(r.EndTime),
                DepositAmount = r.DepositAmount,
                AvailableTableCount = availableByCategory.TryGetValue(r.RequestedTableType, out var available) ? available : 0
            }).ToList();
        }

        public async Task<int> GetWalkInBufferAsync(TableType tableType, DateTime dateTime)
        {
            var configs = await _context.BufferConfigs
                .Where(c => c.TableType == tableType && c.IsActive)
                .ToListAsync();

            var match = configs
                .Where(c => c.DayOfWeek == null || c.DayOfWeek == dateTime.DayOfWeek)
                .Where(c => c.TimeFrom == null || dateTime.TimeOfDay >= c.TimeFrom.Value)
                .Where(c => c.TimeTo == null || dateTime.TimeOfDay <= c.TimeTo.Value)
                .OrderByDescending(c => (c.DayOfWeek.HasValue ? 2 : 0) + ((c.TimeFrom.HasValue || c.TimeTo.HasValue) ? 1 : 0))
                .FirstOrDefault();

            return match?.BufferCount ?? 2;
        }

        public async Task<(bool Success, string Message)> MarkNoShowAsync(Guid reservationId, string? actorUserId = null)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Slots)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
            {
                return (false, "Reservation not found.");
            }

            if (reservation.Status != ReservationStatus.Confirmed)
            {
                return (false, "Only confirmed reservations can be marked as no-show.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var previousState = new { reservation.Status, reservation.DepositForfeited };
                ReservationStateMachine.Transition(reservation, ReservationStatus.NoShow);
                reservation.DepositForfeited = true;

                foreach (var slot in reservation.Slots)
                {
                    slot.IsActive = false;
                }

                _context.Payments.Add(new Payment
                {
                    Id = Guid.NewGuid(),
                    UserId = reservation.UserId,
                    ReservationId = reservation.Id,
                    Type = PaymentType.ForfeitAdjustment,
                    Method = PaymentMethod.Cash,
                    Status = PaymentStatus.Completed,
                    Amount = reservation.DepositAmount,
                    Notes = "Deposit forfeited - no-show.",
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });

                await _auditService.LogAsync(
                    AuditAction.ReservationNoShow,
                    nameof(Reservation),
                    reservation.Id.ToString(),
                    actorUserId,
                    previousState,
                    new { reservation.Status, reservation.DepositForfeited });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _hubContext.Clients
                    .Group($"floorplan-{reservation.BookingDate:yyyy-MM-dd}")
                    .SendAsync("CategoryCapacityChanged", reservation.RequestedTableType.ToString(), reservation.BookingDate);

                return (true, "Reservation marked as no-show.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"An error occurred: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> CreateRefundAsync(Guid reservationId, decimal amount, string reason, string? actorUserId = null)
        {
            if (amount <= 0)
            {
                return (false, "Refund amount must be greater than zero.");
            }

            var reservation = await _context.Reservations
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == reservationId);
            if (reservation == null)
            {
                return (false, "Reservation not found.");
            }

            var refundableBalance = reservation.Payments
                .Where(p => p.Status == PaymentStatus.Completed &&
                            (p.Type == PaymentType.Deposit || p.Type == PaymentType.Refund))
                .Sum(p => p.Amount);

            if (amount > refundableBalance)
            {
                return (false, "Refund amount exceeds the remaining refundable deposit.");
            }

            _context.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                UserId = reservation.UserId,
                ReservationId = reservation.Id,
                Type = PaymentType.Refund,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Completed,
                Amount = -amount,
                Notes = reason,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });

            await _auditService.LogAsync(
                AuditAction.PaymentRecorded,
                nameof(Reservation),
                reservation.Id.ToString(),
                actorUserId,
                newValue: new { Type = PaymentType.Refund, Amount = -amount, Reason = reason });

            await _context.SaveChangesAsync();
            return (true, "Refund recorded successfully.");
        }

        private static List<TimeSpan> BuildHalfHourSlots(TimeSpan startTime, TimeSpan endTime)
        {
            var slots = new List<TimeSpan>();
            for (var current = startTime; current < endTime; current = current.Add(TimeSpan.FromMinutes(30)))
            {
                slots.Add(current);
            }

            return slots;
        }
    }
}
