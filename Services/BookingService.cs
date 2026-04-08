using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Hubs;
using BilliardsBooking.API.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Services
{
    public interface IBookingService
    {
        Task<(bool Success, string Message, Guid? BookingId)> CreateBookingAsync(Guid userId, CreateBookingRequest request);
        Task<BookingListResponse> GetBookingsForUserAsync(Guid userId, int page, int pageSize, string? status = null);
        Task<BookingResponse?> GetBookingByIdAsync(Guid bookingId);
        Task<(bool Success, string Message)> CancelBookingAsync(Guid bookingId, Guid userId);
        Task<(bool Success, string Message)> RescheduleBookingAsync(Guid bookingId, Guid userId, RescheduleRequest request);
        Task<(bool Success, string Message)> CheckInAsync(Guid bookingId, int tableId);
        Task<(bool Success, string Message, CheckoutSummary? Summary)> CheckOutAsync(Guid bookingId, string paymentMethod);
        Task<(bool Success, string Message, Guid? BookingId)> StartWalkInAsync(int tableId, string guestName);
        Task<CategoryAvailabilityResponse> GetCategoryAvailabilityAsync(TableType tableType, DateTime date);
        Task<List<PendingCheckinResponse>> GetPendingCheckinsAsync(DateTime date);
        Task<List<UpcomingWarningResponse>> GetUpcomingWarningsAsync();
        Task<(bool Success, string Message)> LinkCoachSessionAsync(Guid bookingId, Guid coachingSessionId);
        Task<List<LinkableCoachSessionResponse>> GetLinkableCoachSessionsAsync(Guid bookingId);
    }

    public class BookingService : IBookingService
    {
        // 2-table walk-in buffer per category. Online reservations can claim at most
        // (totalActiveTablesInCategory - WalkInBufferPerCategory) concurrent slots.
        public const int WalkInBufferPerCategory = 2;

        private readonly AppDbContext _context;
        private readonly IHubContext<TableStatusHub> _hubContext;

        public BookingService(AppDbContext context, IHubContext<TableStatusHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        private static List<TimeSpan> BuildHalfHourSlots(TimeSpan startTime, TimeSpan endTime)
        {
            var slots = new List<TimeSpan>();
            var current = startTime;
            while (current < endTime)
            {
                slots.Add(current);
                current = current.Add(TimeSpan.FromMinutes(30));
            }
            return slots;
        }

        public async Task<(bool Success, string Message, Guid? BookingId)> CreateBookingAsync(Guid userId, CreateBookingRequest request)
        {
            if (!TimeSpan.TryParse(request.StartTime, out TimeSpan startTime) ||
                !TimeSpan.TryParse(request.EndTime, out TimeSpan endTime))
            {
                return (false, "Invalid time format (Use HH:mm)", null);
            }

            if (startTime >= endTime) return (false, "End time must be after start time", null);
            if (startTime.Ticks % TimeSpan.FromMinutes(30).Ticks != 0 ||
                endTime.Ticks % TimeSpan.FromMinutes(30).Ticks != 0)
            {
                return (false, "Times must align to 30-minute boundaries", null);
            }

            // 1. Enforce advance booking window
            var daysInAdvance = (request.BookingDate.Date - DateTime.UtcNow.Date).Days;

            var activeMembership = await _context.UserMemberships
                .Include(um => um.MembershipPlan)
                .FirstOrDefaultAsync(um => um.UserId == userId && um.IsActive);

            int maxAdvanceDays = activeMembership?.MembershipPlan?.MaxAdvanceBookingDays ?? 0;

            if (daysInAdvance > maxAdvanceDays)
            {
                var tierName = activeMembership?.MembershipPlan?.Name ?? "Khách";
                return (false, $"Hạng {tierName} chỉ được đặt trước tối đa {maxAdvanceDays} ngày.", null);
            }

            var hasActiveBooking = await _context.Bookings.AnyAsync(booking =>
                booking.UserId == userId &&
                (booking.Status == BookingStatus.Pending ||
                 booking.Status == BookingStatus.Confirmed ||
                 booking.Status == BookingStatus.InProgress));

            if (hasActiveBooking)
            {
                return (false, "Bạn đã có lịch đặt bàn hiện tại. Vui lòng hủy lịch hiện tại trước khi đặt lịch mới.", null);
            }

            // 2. Category capacity check (replaces the per-table conflict logic).
            //    Customers book by category (RequestedTableType) — a specific table is
            //    only assigned at check-in by the admin. The walk-in buffer reserves
            //    WalkInBufferPerCategory tables per category for the front desk.
            var totalTablesInCategory = await _context.Tables
                .CountAsync(t => t.Type == request.RequestedTableType
                                 && t.IsActive
                                 && t.Status != TableManualStatus.Maintenance);

            if (totalTablesInCategory == 0)
            {
                return (false, "Loại bàn này hiện không có sẵn.", null);
            }

            var effectiveCapacity = Math.Max(0, totalTablesInCategory - WalkInBufferPerCategory);
            if (effectiveCapacity == 0)
            {
                return (false, "Loại bàn này đã hết chỗ cho đặt trước (toàn bộ dành cho khách vãng lai).", null);
            }

            var slotsNeeded = BuildHalfHourSlots(startTime, endTime);

            var slotOccupancy = await _context.BookingSlots
                .Where(bs => bs.RequestedTableType == request.RequestedTableType
                             && bs.SlotDate == request.BookingDate.Date
                             && bs.IsActive
                             && slotsNeeded.Contains(bs.SlotStart))
                .GroupBy(bs => bs.SlotStart)
                .Select(g => new { SlotStart = g.Key, Count = g.Count() })
                .ToListAsync();

            if (slotOccupancy.Any(o => o.Count >= effectiveCapacity))
            {
                return (false, "Khung giờ đã hết chỗ cho loại bàn này. Vui lòng chọn khung khác.", null);
            }

            // 3. Representative pricing — uses the category's highest hourly rate so the
            //    deposit + pre-checkout estimate reflects the most expensive possible assignment.
            //    Actual cost is recalculated at checkout from the assigned table's rate.
            var representativeRate = await _context.Tables
                .Where(t => t.Type == request.RequestedTableType && t.IsActive && t.Status != TableManualStatus.Maintenance)
                .MaxAsync(t => t.HourlyRate);

            const decimal DEPOSIT_AMOUNT = 50_000m; // 50,000₫

            decimal tableCost = (decimal)(endTime - startTime).TotalHours * representativeRate;
            decimal fnbCost = 0;
            decimal discount = 0;

            if (activeMembership?.MembershipPlan != null)
            {
                discount = tableCost * (activeMembership.MembershipPlan.TableDiscountPercent / 100m);
                tableCost -= discount;
            }

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var booking = new Booking
                {
                    UserId = userId,
                    TableId = null,
                    RequestedTableType = request.RequestedTableType,
                    BookingDate = request.BookingDate.Date,
                    StartTime = startTime,
                    EndTime = endTime,
                    TotalTableCost = tableCost,
                    DiscountAmount = discount,
                    Status = BookingStatus.Confirmed,
                    BookingType = BookingType.Online,
                    DepositAmount = DEPOSIT_AMOUNT
                };

                await _context.Bookings.AddAsync(booking);

                foreach (var stOption in slotsNeeded)
                {
                    await _context.BookingSlots.AddAsync(new BookingSlot
                    {
                        BookingId = booking.Id,
                        TableId = null,
                        RequestedTableType = request.RequestedTableType,
                        SlotDate = booking.BookingDate,
                        SlotStart = stOption,
                        IsActive = true
                    });
                }

                // Handle FnB
                if (request.FnBOrders != null && request.FnBOrders.Any())
                {
                    var fnbOrder = new FnBOrder
                    {
                        BookingId = booking.Id,
                        TotalAmount = 0
                    };
                    await _context.FnBOrders.AddAsync(fnbOrder);

                    foreach (var reqItem in request.FnBOrders)
                    {
                        var menuItem = await _context.FnBMenuItems.FindAsync(reqItem.MenuItemId);
                        if (menuItem != null && menuItem.IsAvailable)
                        {
                            var cost = reqItem.Quantity * menuItem.Price;
                            fnbCost += cost;

                            await _context.FnBOrderItems.AddAsync(new FnBOrderItem
                            {
                                FnBOrderId = fnbOrder.Id,
                                MenuItemId = menuItem.Id,
                                Quantity = reqItem.Quantity,
                                UnitPrice = menuItem.Price
                            });
                        }
                    }
                    fnbOrder.TotalAmount = fnbCost;
                }

                await _context.SaveChangesAsync();

                await _context.Payments.AddAsync(new Payment
                {
                    UserId = userId,
                    BookingId = booking.Id,
                    Amount = DEPOSIT_AMOUNT,
                    Method = PaymentMethod.Cash,
                    Status = PaymentStatus.Pending
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _hubContext.Clients
                    .Group($"floorplan-{request.BookingDate:yyyy-MM-dd}")
                    .SendAsync("CategoryCapacityChanged", request.RequestedTableType.ToString(), request.BookingDate.Date);

                return (true, "Booking successful", booking.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"An error occurred. {ex.Message}", null);
            }
        }

        public async Task<BookingListResponse> GetBookingsForUserAsync(Guid userId, int page, int pageSize, string? status = null)
        {
            var query = _context.Bookings
                .Include(b => b.Table)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.BookingDate)
                .ThenByDescending(b => b.StartTime);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(b => b.Status == parsedStatus)
                    .OrderByDescending(b => b.BookingDate)
                    .ThenByDescending(b => b.StartTime);
            }

            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(b => new BookingResponse
                {
                    Id = b.Id.ToString(),
                    UserId = b.UserId.HasValue ? b.UserId.Value.ToString() : string.Empty,
                    TableId = b.TableId,
                    TableName = b.Table != null ? b.Table.TableNumber : null,
                    RequestedTableType = b.RequestedTableType,
                    StartTime = b.BookingDate.Add(b.StartTime),
                    EndTime = b.BookingDate.Add(b.EndTime),
                    TotalPrice = b.TotalTableCost,
                    CreatedAt = b.CreatedAt,
                    Status = b.Status.ToString(),
                    BookingType = b.BookingType.ToString(),
                    DepositAmount = b.DepositAmount,
                    DepositForfeited = b.DepositForfeited,
                    CheckedInAt = b.CheckedInAt,
                    CheckedOutAt = b.CheckedOutAt,
                    AssignedAt = b.AssignedAt,
                    ActualCost = b.ActualCost,
                    GuestName = b.GuestName
                }).ToListAsync();

            return new BookingListResponse
            {
                Items = items,
                TotalItems = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        public async Task<BookingResponse?> GetBookingByIdAsync(Guid bookingId)
        {
            var b = await _context.Bookings
                .Include(b => b.Table)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
            
            if (b == null) return null;
            
            return new BookingResponse
            {
                Id = b.Id.ToString(),
                UserId = b.UserId.HasValue ? b.UserId.Value.ToString() : string.Empty,
                TableId = b.TableId,
                TableName = b.Table?.TableNumber,
                RequestedTableType = b.RequestedTableType,
                StartTime = b.BookingDate.Add(b.StartTime),
                EndTime = b.BookingDate.Add(b.EndTime),
                TotalPrice = b.TotalTableCost,
                CreatedAt = b.CreatedAt,
                Status = b.Status.ToString(),
                BookingType = b.BookingType.ToString(),
                DepositAmount = b.DepositAmount,
                DepositForfeited = b.DepositForfeited,
                CheckedInAt = b.CheckedInAt,
                CheckedOutAt = b.CheckedOutAt,
                AssignedAt = b.AssignedAt,
                ActualCost = b.ActualCost,
                GuestName = b.GuestName
            };
        }

        public async Task<(bool Success, string Message)> CancelBookingAsync(Guid bookingId, Guid userId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Slots)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null) return (false, "Booking not found");
            
            if (booking.Status == BookingStatus.Cancelled) return (true, "Already cancelled");

            var startDateTime = booking.BookingDate.Add(booking.StartTime);
            var hoursUntilStart = (startDateTime - DateTime.UtcNow).TotalHours;

            // Tiered cancellation refund policy
            decimal refundPercent;
            string refundMessage;
            if (hoursUntilStart >= 4)
            {
                refundPercent = 1.0m;       // 100% refund
                refundMessage = "Hoàn 100% tiền cọc.";
            }
            else if (hoursUntilStart >= 2)
            {
                refundPercent = 0.5m;       // 50% refund
                refundMessage = "Hoàn 50% tiền cọc.";
            }
            else
            {
                return (false, "Không thể hủy trong vòng 2 giờ trước giờ bắt đầu.");
            }

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            booking.DepositForfeited = refundPercent < 1.0m;

            foreach(var slot in booking.Slots)
            {
                slot.IsActive = false;
            }

            // NOTE: CoachingSessions are independent entities and are NOT cascaded on
            // table booking cancellation. If a student pre-booked both a table and a
            // coach, they must cancel the coach session separately.

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"floorplan-{booking.BookingDate:yyyy-MM-dd}")
                .SendAsync("CategoryCapacityChanged", booking.RequestedTableType.ToString(), booking.BookingDate.Date);

            return (true, $"Đã hủy đặt bàn thành công. {refundMessage}");
        }

        public async Task<(bool Success, string Message)> RescheduleBookingAsync(Guid bookingId, Guid userId, RescheduleRequest request)
        {
            var booking = await _context.Bookings
                .Include(b => b.Slots)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null) return (false, "Booking not found");
            if (booking.Status == BookingStatus.Cancelled) return (false, "Cannot reschedule a cancelled booking");
            if (booking.Status != BookingStatus.Confirmed)
            {
                return (false, "Only Confirmed (not yet checked-in) bookings can be rescheduled.");
            }

            var timeUntilBooking = booking.BookingDate.Date.Add(booking.StartTime) - DateTime.UtcNow;
            if (timeUntilBooking.TotalHours < 2)
            {
                return (false, "Bookings can only be rescheduled at least 2 hours in advance");
            }

            var newStartTime = request.NewStartTime.TimeOfDay;
            var newEndTime = request.NewEndTime.TimeOfDay;
            var newSessionDate = request.NewStartTime.Date;

            if (newStartTime >= newEndTime) return (false, "End time must be after start time");

            var slotsNeeded = BuildHalfHourSlots(newStartTime, newEndTime);

            // Category capacity check for the new slot — exclude THIS booking's own slots.
            var totalTablesInCategory = await _context.Tables
                .CountAsync(t => t.Type == booking.RequestedTableType
                                 && t.IsActive
                                 && t.Status != TableManualStatus.Maintenance);

            var effectiveCapacity = Math.Max(0, totalTablesInCategory - WalkInBufferPerCategory);
            if (effectiveCapacity == 0)
            {
                return (false, "Loại bàn này hiện không có chỗ cho đặt trước.");
            }

            var slotOccupancy = await _context.BookingSlots
                .Where(bs => bs.RequestedTableType == booking.RequestedTableType
                             && bs.SlotDate == newSessionDate
                             && bs.IsActive
                             && bs.BookingId != bookingId
                             && slotsNeeded.Contains(bs.SlotStart))
                .GroupBy(bs => bs.SlotStart)
                .Select(g => new { SlotStart = g.Key, Count = g.Count() })
                .ToListAsync();

            if (slotOccupancy.Any(o => o.Count >= effectiveCapacity))
            {
                return (false, "Khung giờ mới đã hết chỗ cho loại bàn này.");
            }

            // Remove old slots
            _context.BookingSlots.RemoveRange(booking.Slots);

            // Generate new slots
            foreach (var ts in slotsNeeded)
            {
                await _context.BookingSlots.AddAsync(new BookingSlot
                {
                    TableId = null,
                    RequestedTableType = booking.RequestedTableType,
                    BookingId = booking.Id,
                    SlotDate = newSessionDate,
                    SlotStart = ts,
                    IsActive = true,
                });
            }

            booking.BookingDate = newSessionDate;
            booking.StartTime = newStartTime;
            booking.EndTime = newEndTime;

            // Recalculate representative cost (actual cost is recomputed at checkout from assigned table).
            var representativeRate = await _context.Tables
                .Where(t => t.Type == booking.RequestedTableType && t.IsActive && t.Status != TableManualStatus.Maintenance)
                .MaxAsync(t => (decimal?)t.HourlyRate) ?? 0m;

            decimal newTableCost = (decimal)(newEndTime - newStartTime).TotalHours * representativeRate;
            newTableCost -= booking.DiscountAmount;
            if (newTableCost < 0) newTableCost = 0;
            booking.TotalTableCost = newTableCost;

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"floorplan-{newSessionDate:yyyy-MM-dd}")
                .SendAsync("CategoryCapacityChanged", booking.RequestedTableType.ToString(), newSessionDate);

            return (true, "Booking rescheduled successfully");
        }

        public async Task<(bool Success, string Message)> CheckInAsync(Guid bookingId, int tableId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Slots)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return (false, "Booking not found");
            if (booking.Status != BookingStatus.Confirmed) return (false, "Booking must be Confirmed to check in");
            if (booking.TableId.HasValue) return (false, "Booking already assigned to a table");

            var table = await _context.Tables.FindAsync(tableId);
            if (table == null || !table.IsActive) return (false, "Chosen table not found");
            if (table.Status == TableManualStatus.Maintenance) return (false, "Chosen table is under maintenance");
            if (table.Type != booking.RequestedTableType)
            {
                return (false, $"Bàn {table.TableNumber} không thuộc loại {booking.RequestedTableType} mà khách đã đặt.");
            }

            // Ensure the chosen physical table isn't currently occupied by another in-progress booking.
            var now = DateTime.UtcNow;
            var tableOccupied = await _context.Bookings.AnyAsync(b =>
                b.Id != bookingId &&
                b.TableId == tableId &&
                b.Status == BookingStatus.InProgress &&
                b.CheckedOutAt == null);

            if (tableOccupied) return (false, $"Bàn {table.TableNumber} đang có khách sử dụng.");

            booking.TableId = tableId;
            booking.Status = BookingStatus.InProgress;
            booking.CheckedInAt = now;
            booking.AssignedAt = now;
            // Recompute cost from the actually assigned table's hourly rate, preserving discount.
            var bookedHours = (decimal)(booking.EndTime - booking.StartTime).TotalHours;
            booking.TotalTableCost = Math.Max(0, bookedHours * table.HourlyRate - booking.DiscountAmount);

            // Backfill TableId on this booking's slots so the per-table views render consistently.
            foreach (var slot in booking.Slots)
            {
                slot.TableId = tableId;
            }

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"floorplan-{booking.BookingDate:yyyy-MM-dd}")
                .SendAsync("TableStatusChanged", tableId, "InUse");
            await _hubContext.Clients
                .Group($"floorplan-{booking.BookingDate:yyyy-MM-dd}")
                .SendAsync("BookingAssigned", booking.Id, tableId);

            return (true, "Check-in successful");
        }

        public async Task<(bool Success, string Message, CheckoutSummary? Summary)> CheckOutAsync(Guid bookingId, string paymentMethod)
        {
            var booking = await _context.Bookings
                .Include(b => b.Table)
                .Include(b => b.User)
                .Include(b => b.Slots)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null) return (false, "Booking not found", null);
            if (booking.Status != BookingStatus.InProgress) return (false, "Booking is not in progress", null);

            booking.CheckedOutAt = DateTime.UtcNow;

            // Calculate ACTUAL duration from CheckedInAt → now
            var actualStart = booking.CheckedInAt ?? booking.BookingDate.Add(booking.StartTime);
            var actualDuration = (decimal)(booking.CheckedOutAt.Value - actualStart).TotalHours;
            if (actualDuration < 0) actualDuration = 0;

            var tableCost = actualDuration * (booking.Table?.HourlyRate ?? 0);

            // F&B + coaching costs
            var fnbCost = await _context.FnBOrders
                .Where(fo => fo.BookingId == bookingId)
                .SumAsync(fo => fo.TotalAmount);

            var linkedCoachSessions = await _context.CoachingSessions
                .Where(cs => cs.BookingId == bookingId)
                .ToListAsync();
            var coachCost = linkedCoachSessions.Sum(cs => cs.Cost);

            var totalCost = tableCost + fnbCost + coachCost - booking.DiscountAmount;
            if (totalCost < 0) totalCost = 0;

            var amountDue = totalCost - booking.DepositAmount;
            if (amountDue < 0) amountDue = 0;

            booking.ActualCost = totalCost;
            booking.Status = BookingStatus.Completed;

            // Close out any linked coaching sessions — they are now paid for via this bill.
            foreach (var session in linkedCoachSessions)
            {
                session.IsCompleted = true;
            }

            // Release booking slots
            foreach (var slot in booking.Slots)
            {
                slot.IsActive = false;
            }

            // Update payment record
            if (!Enum.TryParse<PaymentMethod>(paymentMethod, true, out var parsedMethod))
            {
                parsedMethod = Enums.PaymentMethod.Cash;
            }

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
            if (payment != null)
            {
                payment.Amount = amountDue;
                payment.Method = parsedMethod;
                payment.Status = PaymentStatus.Completed;
            }
            else
            {
                await _context.Payments.AddAsync(new Payment
                {
                    UserId = booking.UserId ?? Guid.Empty,
                    BookingId = booking.Id,
                    Amount = amountDue,
                    Method = parsedMethod,
                    Status = PaymentStatus.Completed
                });
            }

            await _context.SaveChangesAsync();

            var customerName = booking.GuestName ?? booking.User?.FullName ?? "Khách";

            var summary = new CheckoutSummary
            {
                BookingId = booking.Id,
                TableNumber = booking.Table?.TableNumber ?? "",
                CustomerName = customerName,
                BookingType = booking.BookingType.ToString(),
                CheckedInAt = booking.CheckedInAt,
                CheckedOutAt = booking.CheckedOutAt.Value,
                ActualDurationHours = Math.Round(actualDuration, 2),
                TableCost = Math.Round(tableCost, 0),
                FnBCost = fnbCost,
                CoachingCost = coachCost,
                DiscountAmount = booking.DiscountAmount,
                DepositAmount = booking.DepositAmount,
                TotalCost = Math.Round(totalCost, 0),
                AmountDue = Math.Round(amountDue, 0)
            };

            // SignalR: notify table freed
            await _hubContext.Clients
                .Group($"floorplan-{booking.BookingDate:yyyy-MM-dd}")
                .SendAsync("TableStatusChanged", booking.TableId, "Available");

            return (true, "Checkout complete", summary);
        }

        public async Task<(bool Success, string Message, Guid? BookingId)> StartWalkInAsync(int tableId, string guestName)
        {
            var table = await _context.Tables.FindAsync(tableId);
            if (table == null) return (false, "Table not found", null);
            if (table.Status == TableManualStatus.Maintenance) return (false, "Table is under maintenance", null);

            // Check table is actually available right now (no active booking/slots)
            var now = DateTime.UtcNow;
            var currentTime = now.TimeOfDay;
            var today = now.Date;
            var hasActiveBooking = await _context.Bookings.AnyAsync(b =>
                b.TableId == tableId &&
                b.BookingDate == today &&
                b.StartTime <= currentTime &&
                b.EndTime > currentTime &&
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress));

            if (hasActiveBooking) return (false, "Table is currently occupied", null);

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var booking = new Booking
                {
                    BookingType = BookingType.WalkIn,
                    TableId = tableId,
                    RequestedTableType = table.Type,
                    GuestName = guestName,
                    UserId = null,
                    BookingDate = now.Date,
                    StartTime = now.TimeOfDay,
                    EndTime = TimeSpan.FromHours(Math.Min(now.TimeOfDay.TotalHours + 8, 23) + (59.0 / 60)), // cap at 23:59 to avoid SQL Time overflow
                    Status = BookingStatus.InProgress,
                    CheckedInAt = now,
                    AssignedAt = now,
                    DepositAmount = 0, // no deposit for walk-ins
                    TotalTableCost = 0  // will be calculated at checkout
                };

                await _context.Bookings.AddAsync(booking);

                // Create booking slots for current time onward to block the table (next 8 hours in 30-min slots)
                var slotTime = now.TimeOfDay;
                var maxSlotTime = slotTime.Add(TimeSpan.FromHours(8));
                while (slotTime < maxSlotTime && slotTime < TimeSpan.FromHours(24))
                {
                    // Round down to nearest 30-minute boundary
                    var roundedSlot = TimeSpan.FromMinutes(Math.Floor(slotTime.TotalMinutes / 30) * 30);

                    await _context.BookingSlots.AddAsync(new BookingSlot
                    {
                        BookingId = booking.Id,
                        TableId = tableId,
                        RequestedTableType = table.Type,
                        SlotDate = now.Date,
                        SlotStart = roundedSlot,
                        IsActive = true
                    });

                    slotTime = slotTime.Add(TimeSpan.FromMinutes(30));
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // SignalR: notify table is now InUse
                await _hubContext.Clients
                    .Group($"floorplan-{now:yyyy-MM-dd}")
                    .SendAsync("TableStatusChanged", tableId, "InUse");

                return (true, "Walk-in session started", booking.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                return (false, $"An error occurred: {innerMsg}", null);
            }
        }

        /// <summary>
        /// Returns per-30-min-slot availability for a table category on a date. Powers the
        /// category selector on the customer-facing FloorPlan page.
        /// </summary>
        public async Task<CategoryAvailabilityResponse> GetCategoryAvailabilityAsync(TableType tableType, DateTime date)
        {
            var totalTables = await _context.Tables
                .CountAsync(t => t.Type == tableType && t.IsActive && t.Status != TableManualStatus.Maintenance);

            var effectiveCapacity = Math.Max(0, totalTables - WalkInBufferPerCategory);

            // Business hours: 08:00 → 24:00 in 30-min slots (48 rows / day).
            var dayStart = TimeSpan.FromHours(8);
            var dayEnd = TimeSpan.FromHours(24);

            // Pull all active slots for the day+category in one round trip, then group in memory.
            var occupancy = await _context.BookingSlots
                .Where(bs => bs.RequestedTableType == tableType
                             && bs.SlotDate == date.Date
                             && bs.IsActive)
                .GroupBy(bs => bs.SlotStart)
                .Select(g => new { SlotStart = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SlotStart, x => x.Count);

            var slots = new List<CategoryAvailabilitySlot>();
            for (var t = dayStart; t < dayEnd; t = t.Add(TimeSpan.FromMinutes(30)))
            {
                var used = occupancy.TryGetValue(t, out var c) ? c : 0;
                var available = Math.Max(0, effectiveCapacity - used);
                slots.Add(new CategoryAvailabilitySlot
                {
                    StartTime = t.ToString(@"hh\:mm"),
                    EndTime = t.Add(TimeSpan.FromMinutes(30)).ToString(@"hh\:mm"),
                    Available = available,
                    Capacity = effectiveCapacity
                });
            }

            return new CategoryAvailabilityResponse
            {
                TableType = tableType,
                Date = date.Date,
                TotalTables = totalTables,
                BufferSize = WalkInBufferPerCategory,
                OnlineCapacity = effectiveCapacity,
                Slots = slots
            };
        }

        /// <summary>
        /// Returns Confirmed bookings for a date that have NOT yet been assigned a physical
        /// table. The admin uses this to drive the "Pending Check-ins" panel.
        /// </summary>
        public async Task<List<PendingCheckinResponse>> GetPendingCheckinsAsync(DateTime date)
        {
            var pending = await _context.Bookings
                .Include(b => b.User)
                .Where(b => b.BookingDate == date.Date
                            && b.Status == BookingStatus.Confirmed
                            && b.TableId == null)
                .OrderBy(b => b.StartTime)
                .ToListAsync();

            // Cache available table counts per category to avoid N queries.
            var availableByCategory = new Dictionary<TableType, int>();
            foreach (var category in pending.Select(b => b.RequestedTableType).Distinct())
            {
                availableByCategory[category] = await _context.Tables
                    .CountAsync(t => t.Type == category && t.IsActive && t.Status != TableManualStatus.Maintenance);
            }

            return pending.Select(b => new PendingCheckinResponse
            {
                BookingId = b.Id,
                UserFullName = b.User?.FullName,
                UserEmail = b.User?.Email,
                GuestName = b.GuestName,
                RequestedTableType = b.RequestedTableType,
                StartTime = b.BookingDate.Add(b.StartTime),
                EndTime = b.BookingDate.Add(b.EndTime),
                DepositAmount = b.DepositAmount,
                AvailableTableCount = availableByCategory.TryGetValue(b.RequestedTableType, out var ct) ? ct : 0
            }).ToList();
        }

        /// <summary>
        /// Overstay warnings: currently-running bookings whose EndTime is within 15 minutes
        /// AND whose category is fully committed (no spare buffer) at that ending slot — so
        /// there's no operational slack to absorb an overstay.
        /// </summary>
        public async Task<List<UpcomingWarningResponse>> GetUpcomingWarningsAsync()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var cutoff = now.AddMinutes(15);

            var runningBookings = await _context.Bookings
                .Include(b => b.Table)
                .Include(b => b.User)
                .Where(b => b.Status == BookingStatus.InProgress
                            && b.BookingDate == today
                            && b.TableId != null
                            && b.CheckedOutAt == null)
                .ToListAsync();

            var warnings = new List<UpcomingWarningResponse>();
            foreach (var b in runningBookings)
            {
                var endsAt = b.BookingDate.Add(b.EndTime);
                if (endsAt > cutoff || endsAt < now) continue; // only window: [now, now+15min]

                // Is there a Confirmed booking starting at this booking's end time in the same category?
                var hasIncoming = await _context.Bookings.AnyAsync(nb =>
                    nb.Status == BookingStatus.Confirmed
                    && nb.BookingDate == today
                    && nb.RequestedTableType == b.RequestedTableType
                    && nb.StartTime == b.EndTime);

                if (!hasIncoming) continue;

                // Is the category at full capacity at the incoming slot? (No buffer left.)
                var occupied = await _context.BookingSlots.CountAsync(bs =>
                    bs.SlotDate == today
                    && bs.SlotStart == b.EndTime
                    && bs.RequestedTableType == b.RequestedTableType
                    && bs.IsActive);

                var totalTables = await _context.Tables.CountAsync(t =>
                    t.Type == b.RequestedTableType && t.IsActive && t.Status != TableManualStatus.Maintenance);

                if (occupied < totalTables) continue; // slack exists → no warning

                warnings.Add(new UpcomingWarningResponse
                {
                    BookingId = b.Id,
                    TableId = b.TableId,
                    TableNumber = b.Table?.TableNumber,
                    CurrentCustomerName = b.GuestName ?? b.User?.FullName,
                    EndsAt = endsAt,
                    Category = b.RequestedTableType,
                    MinutesRemaining = (int)Math.Max(0, (endsAt - now).TotalMinutes)
                });
            }

            return warnings;
        }

        /// <summary>
        /// Links an independent, unlinked CoachingSession into a running table booking's
        /// bill. Triggered by the "Add Coach Fee" button in the admin CheckoutPanel.
        /// </summary>
        public async Task<(bool Success, string Message)> LinkCoachSessionAsync(Guid bookingId, Guid coachingSessionId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return (false, "Booking not found");
            if (booking.Status != BookingStatus.InProgress)
            {
                return (false, "Chỉ có thể thêm phí HLV cho phiên đang sử dụng.");
            }

            var session = await _context.CoachingSessions.FindAsync(coachingSessionId);
            if (session == null) return (false, "Coaching session not found");
            if (session.BookingId != null)
            {
                return (false, "Phiên HLV này đã được liên kết với hóa đơn khác.");
            }
            if (session.IsCompleted)
            {
                return (false, "Phiên HLV đã hoàn thành.");
            }

            // Soft ownership check: must match the booking's user, unless it's a walk-in
            // (where the admin is manually attaching a lesson for an anonymous guest).
            if (booking.UserId.HasValue && session.StudentUserId != booking.UserId.Value)
            {
                return (false, "Phiên HLV này thuộc về khách khác.");
            }

            session.BookingId = bookingId;
            session.LinkedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (true, "Đã thêm phí huấn luyện viên vào hóa đơn.");
        }

        /// <summary>
        /// Returns unlinked CoachingSessions the admin can attach to a given booking via
        /// the "Add Coach Fee" picker. Scope: same user (if any) + same date.
        /// </summary>
        public async Task<List<LinkableCoachSessionResponse>> GetLinkableCoachSessionsAsync(Guid bookingId)
        {
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return new List<LinkableCoachSessionResponse>();

            var query = _context.CoachingSessions
                .Include(cs => cs.Coach)
                    .ThenInclude(c => c!.User)
                .Where(cs => cs.BookingId == null
                             && !cs.IsCompleted
                             && cs.CancelledAt == null
                             && cs.SessionDate == booking.BookingDate);

            if (booking.UserId.HasValue)
            {
                var uid = booking.UserId.Value;
                query = query.Where(cs => cs.StudentUserId == uid);
            }

            var sessions = await query.OrderBy(cs => cs.StartTime).ToListAsync();

            return sessions.Select(cs => new LinkableCoachSessionResponse
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
    }
}
