using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Services.StateMachines;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Services
{
    public interface IPaymentService
    {
        Task<PaymentResponse?> CreatePaymentAsync(CreatePaymentRequest request);
        Task<List<PaymentResponse>> GetPaymentsByBookingAsync(Guid bookingId);
    }

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;

        public PaymentService(AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<PaymentResponse?> CreatePaymentAsync(CreatePaymentRequest request)
        {
            if (!Guid.TryParse(request.BookingId, out var legacyId))
            {
                return null;
            }

            if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, true, out var method))
            {
                method = PaymentMethod.Cash;
            }

            var reservation = await _context.Reservations.FirstOrDefaultAsync(r => r.Id == legacyId);
            if (reservation != null)
            {
                var existingDeposit = await _context.Payments
                    .Where(p => p.ReservationId == legacyId && p.Type == PaymentType.Deposit)
                    .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (existingDeposit != null)
                {
                    return Map(existingDeposit);
                }

                var payment = new Models.Payment
                {
                    Id = Guid.NewGuid(),
                    UserId = reservation.UserId,
                    ReservationId = reservation.Id,
                    Type = PaymentType.Deposit,
                    Method = method,
                    Status = PaymentStatus.Completed,
                    Amount = reservation.DepositAmount,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    Notes = "Reservation deposit"
                };

                _context.Payments.Add(payment);

                await _auditService.LogAsync(
                    AuditAction.PaymentRecorded,
                    nameof(Models.Payment),
                    payment.Id.ToString(),
                    payment.UserId.ToString(),
                    newValue: new { payment.Type, payment.Amount, payment.Method, payment.Status });

                if (reservation.Status == ReservationStatus.Pending)
                {
                    ReservationStateMachine.Transition(reservation, ReservationStatus.Confirmed);
                }

                await _context.SaveChangesAsync();
                return Map(payment);
            }

            var session = await _context.TableSessions
                .Include(s => s.Invoice)
                    .ThenInclude(i => i!.Payments)
                .FirstOrDefaultAsync(s => s.Id == legacyId || s.ReservationId == legacyId);

            if (session?.Invoice == null)
            {
                return null;
            }

            var latestPayment = session.Invoice.Payments
                .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
                .FirstOrDefault();
            if (latestPayment != null)
            {
                return Map(latestPayment);
            }

            var amount = session.Type == SessionType.WalkIn ? session.Invoice.GrandTotal : session.Invoice.BalanceDue;
            var paymentType = session.Type == SessionType.WalkIn ? PaymentType.FullPayment : PaymentType.FinalSettlement;

            var settlement = new Models.Payment
            {
                Id = Guid.NewGuid(),
                UserId = session.UserId,
                InvoiceId = session.Invoice.Id,
                Type = paymentType,
                Method = method,
                Status = PaymentStatus.Completed,
                Amount = amount,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Notes = "Settlement payment"
            };

            _context.Payments.Add(settlement);

            await _auditService.LogAsync(
                AuditAction.PaymentRecorded,
                nameof(Models.Payment),
                settlement.Id.ToString(),
                settlement.UserId.ToString(),
                newValue: new { settlement.Type, settlement.Amount, settlement.Method, settlement.Status });

            session.Invoice.PaymentCompletedAt = settlement.CompletedAt;
            await _context.SaveChangesAsync();
            return Map(settlement);
        }

        public async Task<List<PaymentResponse>> GetPaymentsByBookingAsync(Guid bookingId)
        {
            var payments = await _context.Payments
                .Where(p => p.ReservationId == bookingId || p.BookingId == bookingId)
                .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
                .ToListAsync();

            var session = await _context.TableSessions
                .Include(s => s.Invoice)
                    .ThenInclude(i => i!.Payments)
                .FirstOrDefaultAsync(s => s.Id == bookingId || s.ReservationId == bookingId);

            if (session?.Invoice != null)
            {
                payments.AddRange(session.Invoice.Payments.Where(p => payments.All(existing => existing.Id != p.Id)));
            }

            return payments
                .OrderByDescending(p => p.CompletedAt ?? p.CreatedAt)
                .Select(Map)
                .ToList();
        }

        private static PaymentResponse Map(Models.Payment payment)
        {
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
                CompletedAt = payment.CompletedAt
            };
        }
    }
}
