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
    }

    public class BookingService : IBookingService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<TableStatusHub> _hubContext;

        public BookingService(AppDbContext context, IHubContext<TableStatusHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<(bool Success, string Message, Guid? BookingId)> CreateBookingAsync(Guid userId, CreateBookingRequest request)
        {
            if (!TimeSpan.TryParse(request.StartTime, out TimeSpan startTime) ||
                !TimeSpan.TryParse(request.EndTime, out TimeSpan endTime))
            {
                return (false, "Invalid time format (Use HH:mm)", null);
            }

            if (startTime >= endTime) return (false, "End time must be after start time", null);

            var slotsNeeded = new List<TimeSpan>();
            var currentTime = startTime;
            while(currentTime < endTime)
            {
                slotsNeeded.Add(currentTime);
                currentTime = currentTime.Add(TimeSpan.FromMinutes(30));
            }

            var existingConflicts = await _context.BookingSlots
                .Where(bs => bs.TableId == request.TableId &&
                             bs.SlotDate == request.BookingDate.Date &&
                             bs.IsActive &&
                             slotsNeeded.Contains(bs.SlotStart))
                .ToListAsync();

            if (existingConflicts.Any())
            {
                return (false, "One or more requested timeslots are already booked.", null);
            }

            var table = await _context.Tables.FindAsync(request.TableId);
            if (table == null || table.Status == TableManualStatus.Maintenance)
            {
                return (false, "Table not available", null);
            }

            decimal tableCost = (decimal)(endTime - startTime).TotalHours * table.HourlyRate;
            decimal fnbCost = 0;
            decimal discount = 0;

            // Membership Discount logic
            var activeMembership = await _context.UserMemberships
                .Include(um => um.MembershipPlan)
                .FirstOrDefaultAsync(um => um.UserId == userId && um.IsActive);
            
            if (activeMembership?.MembershipPlan != null)
            {
                discount = tableCost * (activeMembership.MembershipPlan.TableDiscountPercent / 100m);
                tableCost -= discount;
                
                // Track benefit usage if needed...
            }

            // Coach logic
            decimal coachingCost = 0;
            if (request.CoachId.HasValue)
            {
                var coachObj = await _context.Coaches.FindAsync(request.CoachId.Value);
                if (coachObj == null || !coachObj.IsActive) return (false, "Coach not available", null);
                
                var coachConflicts = await _context.CoachingSessions
                     .Where(cs => cs.CoachId == request.CoachId.Value && cs.SessionDate == request.BookingDate.Date && !cs.IsCompleted)
                     .AnyAsync(cs => (startTime >= cs.StartTime && startTime < cs.EndTime) || (endTime > cs.StartTime && endTime <= cs.EndTime));
                if (coachConflicts) return (false, "Coach is already booked for this time", null);
                
                coachingCost = (decimal)(endTime - startTime).TotalHours * coachObj.HourlyRate;
            }

            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                var booking = new Booking
                {
                    UserId = userId,
                    TableId = request.TableId,
                    BookingDate = request.BookingDate.Date,
                    StartTime = startTime,
                    EndTime = endTime,
                    TotalTableCost = tableCost,
                    DiscountAmount = discount,
                    Status = BookingStatus.Confirmed
                };
                
                await _context.Bookings.AddAsync(booking);

                if (request.CoachId.HasValue)
                {
                    var coachingSession = new CoachingSession
                    {
                        CoachId = request.CoachId.Value,
                        StudentUserId = userId,
                        BookingId = booking.Id,
                        SessionDate = request.BookingDate.Date,
                        StartTime = startTime,
                        EndTime = endTime,
                        Cost = coachingCost
                    };

                    await _context.CoachingSessions.AddAsync(coachingSession);
                }

                foreach (var stOption in slotsNeeded)
                {
                    await _context.BookingSlots.AddAsync(new BookingSlot
                    {
                        BookingId = booking.Id,
                        TableId = request.TableId,
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
                    Amount = tableCost + fnbCost + coachingCost,
                    Method = PaymentMethod.Cash,
                    Status = PaymentStatus.Pending
                });
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _hubContext.Clients.Group($"table-{request.TableId}-{request.BookingDate:yyyy-MM-dd}").SendAsync("SlotBooked", slotsNeeded);
                await _hubContext.Clients.Group($"floorplan-{request.BookingDate:yyyy-MM-dd}").SendAsync("TableStatusChanged", request.TableId, "InUse");

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
                    UserId = b.UserId.ToString(),
                    TableId = b.TableId,
                    TableName = b.Table != null ? b.Table.TableNumber : string.Empty,
                    StartTime = b.BookingDate.Add(b.StartTime),
                    EndTime = b.BookingDate.Add(b.EndTime),
                    TotalPrice = b.TotalTableCost,
                    CreatedAt = b.CreatedAt,
                    Status = b.Status.ToString()
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
                UserId = b.UserId.ToString(),
                TableId = b.TableId,
                TableName = b.Table?.TableNumber ?? "",
                StartTime = b.BookingDate.Add(b.StartTime),
                EndTime = b.BookingDate.Add(b.EndTime),
                TotalPrice = b.TotalTableCost,
                CreatedAt = b.CreatedAt,
                Status = b.Status.ToString()
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
            if ((startDateTime - DateTime.UtcNow).TotalHours < 2)
            {
                return (false, "Cannot cancel within 2 hours of start time");
            }

            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
            
            var slotsToRelease = new List<TimeSpan>();

            foreach(var slot in booking.Slots)
            {
                slot.IsActive = false;
                slotsToRelease.Add(slot.SlotStart);
            }
            
            var associatedSession = await _context.CoachingSessions.FirstOrDefaultAsync(cs => cs.BookingId == bookingId);
            if (associatedSession != null)
            {
                _context.CoachingSessions.Remove(associatedSession);
            }

            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group($"table-{booking.TableId}-{booking.BookingDate:yyyy-MM-dd}")
                .SendAsync("SlotReleased", slotsToRelease);

            return (true, "Booking cancelled successfully");
        }

        public async Task<(bool Success, string Message)> RescheduleBookingAsync(Guid bookingId, Guid userId, RescheduleRequest request)
        {
            var booking = await _context.Bookings
                .Include(b => b.Slots)
                .Include(b => b.Table)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null) return (false, "Booking not found");
            if (booking.Status == BookingStatus.Cancelled) return (false, "Cannot reschedule a cancelled booking");

            var timeUntilBooking = booking.BookingDate.Date.Add(booking.StartTime) - DateTime.UtcNow;
            if (timeUntilBooking.TotalHours < 2)
            {
                return (false, "Bookings can only be rescheduled at least 2 hours in advance");
            }

            var slotsNeeded = new List<TimeSpan>();
            var currentTime = request.NewStartTime.TimeOfDay;
            var endTime = request.NewEndTime.TimeOfDay;
            
            while (currentTime < endTime)
            {
                slotsNeeded.Add(currentTime);
                currentTime = currentTime.Add(TimeSpan.FromMinutes(30));
            }

            var existingConflicts = await _context.BookingSlots
                .Where(bs => bs.TableId == booking.TableId &&
                             bs.SlotDate == request.NewStartTime.Date &&
                             bs.IsActive &&
                             bs.BookingId != bookingId &&
                             slotsNeeded.Contains(bs.SlotStart))
                .ToListAsync();

            if (existingConflicts.Any())
            {
                return (false, "One or more requested timeslots are already booked.");
            }

            // Remove old slots
            _context.BookingSlots.RemoveRange(booking.Slots);

            // Generate new slots
            foreach (var ts in slotsNeeded)
            {
                await _context.BookingSlots.AddAsync(new BookingSlot
                {
                    TableId = booking.TableId,
                    BookingId = booking.Id,
                    SlotDate = request.NewStartTime.Date,
                    SlotStart = ts,
                    IsActive = true,
                });
            }

            booking.BookingDate = request.NewStartTime.Date;
            booking.StartTime = request.NewStartTime.TimeOfDay;
            booking.EndTime = request.NewEndTime.TimeOfDay;

            decimal newTableCost = (decimal)(endTime - request.NewStartTime.TimeOfDay).TotalHours * (booking.Table?.HourlyRate ?? 0m);
            booking.TotalTableCost = newTableCost; // Doesn't account for F&B properly, MVP level

            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("SlotBooked", booking.TableId);

            return (true, "Booking rescheduled successfully");
        }
    }
}
