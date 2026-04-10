using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
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
        Task<AdminBookingListResponse> GetAdminBookingsAsync(int page, int pageSize, string? status = null, int? tableId = null, string? userId = null, DateTime? dateFrom = null, DateTime? dateTo = null);
        Task<AdminBookingResponse?> GetAdminBookingByIdAsync(Guid bookingId);
    }

    public class BookingService : IBookingService
    {
        private readonly AppDbContext _context;
        private readonly IReservationService _reservationService;
        private readonly ITableSessionService _tableSessionService;

        public BookingService(
            AppDbContext context,
            IReservationService reservationService,
            ITableSessionService tableSessionService)
        {
            _context = context;
            _reservationService = reservationService;
            _tableSessionService = tableSessionService;
        }

        public Task<(bool Success, string Message, Guid? BookingId)> CreateBookingAsync(Guid userId, CreateBookingRequest request) =>
            _reservationService.CreateReservationAsync(userId, request, userId.ToString());

        public async Task<BookingListResponse> GetBookingsForUserAsync(Guid userId, int page, int pageSize, string? status = null)
        {
            var query = _context.Reservations
                .Include(r => r.Payments)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.Table)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.Invoice)
                        .ThenInclude(i => i!.Payments)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.CoachingSessions)
                        .ThenInclude(cs => cs.Coach)
                            .ThenInclude(c => c!.User)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.FnBOrders)
                .Where(r => r.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<BookingStatus>(status, true, out var bookingStatus))
                {
                    query = bookingStatus switch
                    {
                        BookingStatus.InProgress => query.Where(r => r.Status == ReservationStatus.CheckedIn),
                        BookingStatus.Completed => query.Where(r => r.Status == ReservationStatus.Completed),
                        BookingStatus.Cancelled => query.Where(r => r.Status == ReservationStatus.Cancelled),
                        BookingStatus.NoShow => query.Where(r => r.Status == ReservationStatus.NoShow),
                        BookingStatus.Confirmed => query.Where(r => r.Status == ReservationStatus.Confirmed),
                        BookingStatus.Pending => query.Where(r => r.Status == ReservationStatus.Pending),
                        _ => query
                    };
                }
            }

            query = query.OrderByDescending(r => r.BookingDate).ThenByDescending(r => r.StartTime);
            var total = await query.CountAsync();
            var reservations = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return new BookingListResponse
            {
                Items = reservations.Select(MapBookingResponse).ToList(),
                TotalItems = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };
        }

        public async Task<BookingResponse?> GetBookingByIdAsync(Guid bookingId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Payments)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.Table)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.Invoice)
                        .ThenInclude(i => i!.Payments)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.CoachingSessions)
                        .ThenInclude(cs => cs.Coach)
                            .ThenInclude(c => c!.User)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.FnBOrders)
                .FirstOrDefaultAsync(r => r.Id == bookingId);

            if (reservation != null)
            {
                return MapBookingResponse(reservation);
            }

            var session = await _tableSessionService.ResolveLegacySessionAsync(bookingId);
            return session == null ? null : MapWalkInBookingResponse(session);
        }

        public Task<(bool Success, string Message)> CancelBookingAsync(Guid bookingId, Guid userId) =>
            _reservationService.CancelReservationAsync(bookingId, userId, userId.ToString());

        public Task<(bool Success, string Message)> RescheduleBookingAsync(Guid bookingId, Guid userId, RescheduleRequest request) =>
            _reservationService.RescheduleReservationAsync(bookingId, userId, request, userId.ToString());

        public Task<(bool Success, string Message)> CheckInAsync(Guid bookingId, int tableId) =>
            _tableSessionService.CheckInAsync(bookingId, tableId);

        public async Task<(bool Success, string Message, CheckoutSummary? Summary)> CheckOutAsync(Guid bookingId, string paymentMethod)
        {
            var session = await _tableSessionService.ResolveLegacySessionAsync(bookingId);
            if (session == null)
            {
                return (false, "Active session not found.", null);
            }

            return await _tableSessionService.CheckoutAsync(session.Id, paymentMethod);
        }

        public async Task<(bool Success, string Message, Guid? BookingId)> StartWalkInAsync(int tableId, string guestName)
        {
            var result = await _tableSessionService.StartWalkInAsync(tableId, guestName);
            return (result.Success, result.Message, result.SessionId);
        }

        public Task<CategoryAvailabilityResponse> GetCategoryAvailabilityAsync(TableType tableType, DateTime date) =>
            _reservationService.GetCategoryAvailabilityAsync(tableType, date);

        public Task<List<PendingCheckinResponse>> GetPendingCheckinsAsync(DateTime date) =>
            _reservationService.GetPendingCheckinsAsync(date);

        public Task<List<UpcomingWarningResponse>> GetUpcomingWarningsAsync() =>
            _tableSessionService.GetUpcomingWarningsAsync();

        public async Task<(bool Success, string Message)> LinkCoachSessionAsync(Guid bookingId, Guid coachingSessionId)
        {
            var session = await _tableSessionService.ResolveLegacySessionAsync(bookingId);
            return session == null
                ? (false, "Active session not found.")
                : await _tableSessionService.LinkCoachEarlyAsync(session.Id, coachingSessionId);
        }

        public async Task<List<LinkableCoachSessionResponse>> GetLinkableCoachSessionsAsync(Guid bookingId)
        {
            var session = await _tableSessionService.ResolveLegacySessionAsync(bookingId);
            return session == null
                ? new List<LinkableCoachSessionResponse>()
                : await _tableSessionService.GetLinkableCoachSessionsAsync(session.Id);
        }

        public async Task<AdminBookingListResponse> GetAdminBookingsAsync(
            int page,
            int pageSize,
            string? status = null,
            int? tableId = null,
            string? userId = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var items = await BuildAdminBookingsAsync();

            if (!string.IsNullOrWhiteSpace(status))
            {
                items = items.Where(i => string.Equals(i.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (tableId.HasValue)
            {
                items = items.Where(i => i.TableId == tableId.Value).ToList();
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                items = items.Where(i => string.Equals(i.UserId, userId, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (dateFrom.HasValue)
            {
                items = items.Where(i => i.BookingDate.Date >= dateFrom.Value.Date).ToList();
            }

            if (dateTo.HasValue)
            {
                items = items.Where(i => i.BookingDate.Date <= dateTo.Value.Date).ToList();
            }

            items = items
                .OrderByDescending(i => i.BookingDate)
                .ThenByDescending(i => i.StartTime)
                .ToList();

            var totalItems = items.Count;
            var paged = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new AdminBookingListResponse
            {
                Items = paged,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
            };
        }

        public async Task<AdminBookingResponse?> GetAdminBookingByIdAsync(Guid bookingId)
        {
            var items = await BuildAdminBookingsAsync();
            return items.FirstOrDefault(i => Guid.TryParse(i.Id, out var id) && id == bookingId);
        }

        private async Task<List<AdminBookingResponse>> BuildAdminBookingsAsync()
        {
            var reservations = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Payments)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.Table)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.Invoice)
                        .ThenInclude(i => i!.Payments)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.CoachingSessions)
                        .ThenInclude(cs => cs.Coach)
                            .ThenInclude(c => c!.User)
                .Include(r => r.TableSession)
                    .ThenInclude(s => s!.FnBOrders)
                .ToListAsync();

            var walkIns = await _context.TableSessions
                .Include(s => s.Table)
                .Include(s => s.User)
                .Include(s => s.Invoice)
                    .ThenInclude(i => i!.Payments)
                .Include(s => s.CoachingSessions)
                    .ThenInclude(cs => cs.Coach)
                        .ThenInclude(c => c!.User)
                .Include(s => s.FnBOrders)
                .Where(s => s.Type == SessionType.WalkIn)
                .ToListAsync();

            var items = reservations.Select(MapAdminReservation).ToList();
            items.AddRange(walkIns.Select(MapAdminWalkIn));
            return items;
        }

        private static BookingResponse MapBookingResponse(Reservation reservation)
        {
            var session = reservation.TableSession;
            var invoice = session?.Invoice;
            var latestPayment = (invoice?.Payments ?? reservation.Payments)
                .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
                .FirstOrDefault();
            var coachSession = session?.CoachingSessions
                .OrderBy(cs => cs.SessionDate)
                .ThenBy(cs => cs.StartTime)
                .FirstOrDefault();

            return new BookingResponse
            {
                Id = reservation.Id.ToString(),
                UserId = reservation.UserId?.ToString() ?? string.Empty,
                TableId = session?.TableId,
                TableName = session?.Table?.TableNumber,
                RequestedTableType = reservation.RequestedTableType,
                StartTime = reservation.BookingDate.Add(reservation.StartTime),
                EndTime = reservation.BookingDate.Add(reservation.EndTime),
                TotalPrice = invoice?.GrandTotal ?? 0,
                CreatedAt = reservation.CreatedAt,
                Status = MapLegacyStatus(reservation.Status),
                BookingType = BookingType.Online.ToString(),
                DepositAmount = reservation.DepositAmount,
                DepositForfeited = reservation.DepositForfeited,
                CheckedInAt = session?.StartedAt,
                CheckedOutAt = session?.EndedAt,
                AssignedAt = session?.StartedAt,
                ActualCost = invoice?.GrandTotal,
                GuestName = null,
                Payment = MapPaymentResponse(latestPayment),
                Coach = MapCoachResponse(coachSession),
                FnBOrders = MapFnBOrderResponses(session?.FnBOrders)
            };
        }

        private static BookingResponse MapWalkInBookingResponse(TableSession session)
        {
            var latestPayment = session.Invoice?.Payments
                .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
                .FirstOrDefault();
            var coachSession = session.CoachingSessions
                .OrderBy(cs => cs.SessionDate)
                .ThenBy(cs => cs.StartTime)
                .FirstOrDefault();

            return new BookingResponse
            {
                Id = session.Id.ToString(),
                UserId = session.UserId?.ToString() ?? string.Empty,
                TableId = session.TableId,
                TableName = session.Table?.TableNumber,
                RequestedTableType = session.Table?.Type ?? TableType.Pool,
                StartTime = session.StartedAt,
                EndTime = session.EndedAt ?? session.StartedAt,
                TotalPrice = session.Invoice?.GrandTotal ?? 0,
                CreatedAt = session.CreatedAt,
                Status = session.Status == TableSessionStatus.Active ? BookingStatus.InProgress.ToString() : BookingStatus.Completed.ToString(),
                BookingType = BookingType.WalkIn.ToString(),
                DepositAmount = 0,
                DepositForfeited = false,
                CheckedInAt = session.StartedAt,
                CheckedOutAt = session.EndedAt,
                AssignedAt = session.StartedAt,
                ActualCost = session.Invoice?.GrandTotal,
                GuestName = session.GuestName,
                Payment = MapPaymentResponse(latestPayment),
                Coach = MapCoachResponse(coachSession),
                FnBOrders = MapFnBOrderResponses(session.FnBOrders)
            };
        }

        private static PaymentResponse? MapPaymentResponse(Payment? payment)
        {
            if (payment == null)
            {
                return null;
            }

            return new PaymentResponse
            {
                Id = payment.Id.ToString(),
                BookingId = payment.BookingId?.ToString() ?? string.Empty,
                ReservationId = payment.ReservationId?.ToString() ?? string.Empty,
                InvoiceId = payment.InvoiceId?.ToString() ?? string.Empty,
                Type = payment.Type.ToString(),
                Amount = payment.Amount,
                Method = payment.Method.ToString(),
                Status = payment.Status.ToString(),
                TransactionId = payment.TransactionId,
                Notes = payment.Notes,
                CreatedAt = payment.CreatedAt,
                CompletedAt = payment.CompletedAt,
            };
        }

        private static CoachResponse? MapCoachResponse(CoachingSession? coachingSession)
        {
            var coach = coachingSession?.Coach;
            if (coach == null)
            {
                return null;
            }

            return new CoachResponse
            {
                Id = coach.Id.ToString(),
                UserId = coach.UserId.ToString(),
                FullName = coach.User?.FullName ?? string.Empty,
                Specialty = coach.Specialty.ToString(),
                Bio = coach.Bio,
                HourlyRate = coach.HourlyRate,
                Rating = coach.Rating,
                AvatarUrl = coach.PhotoUrl,
            };
        }

        private static List<FnBOrderResponse> MapFnBOrderResponses(IEnumerable<FnBOrder>? orders)
        {
            if (orders == null)
            {
                return new List<FnBOrderResponse>();
            }

            return orders
                .Select(order => new FnBOrderResponse
                {
                    Id = order.Id.ToString(),
                    TotalPrice = order.TotalAmount,
                    Status = "Pending",
                })
                .ToList();
        }

        private static AdminBookingResponse MapAdminReservation(Reservation reservation)
        {
            var session = reservation.TableSession;
            var invoice = session?.Invoice;
            var latestPayment = (invoice?.Payments ?? reservation.Payments)
                .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
                .FirstOrDefault();

            return new AdminBookingResponse
            {
                Id = reservation.Id.ToString(),
                UserId = reservation.UserId?.ToString() ?? string.Empty,
                UserFullName = reservation.User?.FullName ?? string.Empty,
                UserEmail = reservation.User?.Email ?? string.Empty,
                TableId = session?.TableId,
                TableNumber = session?.Table?.TableNumber,
                RequestedTableType = reservation.RequestedTableType,
                BookingDate = reservation.BookingDate,
                StartTime = reservation.BookingDate.Add(reservation.StartTime),
                EndTime = reservation.BookingDate.Add(reservation.EndTime),
                TotalPrice = invoice?.GrandTotal ?? 0,
                DiscountAmount = invoice?.DiscountAmount ?? 0,
                FnBTotal = invoice?.FnBTotal ?? session?.FnBOrders.Sum(o => o.TotalAmount) ?? 0,
                CoachingTotal = invoice?.CoachingTotal ?? session?.CoachingSessions.Sum(cs => cs.Cost) ?? 0,
                PaymentAmount = latestPayment?.Amount ?? 0,
                PaymentStatus = latestPayment?.Status.ToString(),
                Status = MapLegacyStatus(reservation.Status),
                BookingType = BookingType.Online.ToString(),
                DepositAmount = reservation.DepositAmount,
                DepositForfeited = reservation.DepositForfeited,
                CheckedInAt = session?.StartedAt,
                CheckedOutAt = session?.EndedAt,
                AssignedAt = session?.StartedAt,
                ActualCost = invoice?.GrandTotal,
                GuestName = null,
                CreatedAt = reservation.CreatedAt,
                CancelledAt = reservation.CancelledAt,
                CoachName = session == null
                    ? null
                    : string.Join(", ", session.CoachingSessions.Select(cs => cs.Coach?.User?.FullName ?? cs.CoachId.ToString()).Distinct())
            };
        }

        private static AdminBookingResponse MapAdminWalkIn(TableSession session)
        {
            var invoice = session.Invoice;
            var latestPayment = invoice?.Payments.OrderByDescending(p => p.CompletedAt ?? p.CreatedAt).FirstOrDefault();

            return new AdminBookingResponse
            {
                Id = session.Id.ToString(),
                UserId = session.UserId?.ToString() ?? string.Empty,
                UserFullName = session.GuestName ?? session.User?.FullName ?? string.Empty,
                UserEmail = session.User?.Email ?? string.Empty,
                TableId = session.TableId,
                TableNumber = session.Table?.TableNumber,
                RequestedTableType = session.Table?.Type ?? TableType.Pool,
                BookingDate = session.StartedAt.Date,
                StartTime = session.StartedAt,
                EndTime = session.EndedAt ?? session.StartedAt,
                TotalPrice = invoice?.GrandTotal ?? 0,
                DiscountAmount = invoice?.DiscountAmount ?? 0,
                FnBTotal = invoice?.FnBTotal ?? session.FnBOrders.Sum(o => o.TotalAmount),
                CoachingTotal = invoice?.CoachingTotal ?? session.CoachingSessions.Sum(cs => cs.Cost),
                PaymentAmount = latestPayment?.Amount ?? 0,
                PaymentStatus = latestPayment?.Status.ToString(),
                Status = session.Status == TableSessionStatus.Active ? BookingStatus.InProgress.ToString() : BookingStatus.Completed.ToString(),
                BookingType = BookingType.WalkIn.ToString(),
                DepositAmount = 0,
                DepositForfeited = false,
                CheckedInAt = session.StartedAt,
                CheckedOutAt = session.EndedAt,
                AssignedAt = session.StartedAt,
                ActualCost = invoice?.GrandTotal,
                GuestName = session.GuestName,
                CreatedAt = session.CreatedAt,
                CancelledAt = null,
                CoachName = string.Join(", ", session.CoachingSessions.Select(cs => cs.Coach?.User?.FullName ?? cs.CoachId.ToString()).Distinct())
            };
        }

        private static string MapLegacyStatus(ReservationStatus status) => status switch
        {
            ReservationStatus.CheckedIn => BookingStatus.InProgress.ToString(),
            _ => status.ToString()
        };
    }
}
