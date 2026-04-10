using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
using Microsoft.EntityFrameworkCore;

namespace BilliardsBooking.API.Services
{
    public interface IInvoiceService
    {
        Task<Invoice?> BuildInvoiceAsync(Guid sessionId, PaymentMethod paymentMethod, string? actorUserId = null);
        Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId);
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;

        public InvoiceService(AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<Invoice?> BuildInvoiceAsync(Guid sessionId, PaymentMethod paymentMethod, string? actorUserId = null)
        {
            var session = await _context.TableSessions
                .Include(s => s.Table)
                .Include(s => s.User)
                .Include(s => s.Reservation)
                    .ThenInclude(r => r!.Payments)
                .Include(s => s.FnBOrders)
                    .ThenInclude(o => o.Items)
                        .ThenInclude(i => i.MenuItem)
                .Include(s => s.CoachingSessions)
                    .ThenInclude(cs => cs.Coach)
                        .ThenInclude(c => c!.User)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null || session.Table == null)
            {
                return null;
            }

            var existingInvoice = await _context.Invoices
                .Include(i => i.Lines)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.TableSessionId == sessionId);
            if (existingInvoice != null)
            {
                return existingInvoice;
            }

            var serviceEndedAt = session.EndedAt ?? DateTime.UtcNow;
            var durationHours = Math.Max(0m, (decimal)(serviceEndedAt - session.StartedAt).TotalHours);
            var tableRate = session.Table.HourlyRate;
            var tableCost = Math.Round(durationHours * tableRate, 2);

            var activeMembership = session.UserId == null
                ? null
                : await _context.UserMemberships
                    .Include(um => um.MembershipPlan)
                    .FirstOrDefaultAsync(um => um.UserId == session.UserId && um.IsActive);

            var discountPercent = activeMembership?.MembershipPlan?.TableDiscountPercent ?? 0;
            var discountAmount = Math.Round(tableCost * (discountPercent / 100m), 2);
            var membershipTier = activeMembership?.MembershipPlan?.Tier.ToString();

            var fnbTotal = session.FnBOrders.Sum(order => order.TotalAmount);
            var coachingTotal = 0m;
            var lines = new List<InvoiceLine>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Type = InvoiceLineType.TableTime,
                    Description = $"{session.Table.TableNumber} - {durationHours:F2}h",
                    UnitPrice = tableRate,
                    Quantity = durationHours,
                    Total = tableCost
                }
            };

            foreach (var order in session.FnBOrders)
            {
                foreach (var item in order.Items)
                {
                    lines.Add(new InvoiceLine
                    {
                        Id = Guid.NewGuid(),
                        Type = InvoiceLineType.FnB,
                        Description = $"{item.MenuItem?.Name ?? "F&B"} x{item.Quantity}",
                        UnitPrice = item.UnitPrice,
                        Quantity = item.Quantity,
                        Total = Math.Round(item.UnitPrice * item.Quantity, 2)
                    });
                }
            }

            foreach (var coachingSession in session.CoachingSessions)
            {
                var coachDurationHours = Math.Max(0m, (decimal)(coachingSession.EndTime - coachingSession.StartTime).TotalHours);
                var coachRate = coachingSession.Coach?.HourlyRate ?? 0m;
                var coachTotal = Math.Round(coachRate * coachDurationHours, 2);
                coachingTotal += coachTotal;

                lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    Type = InvoiceLineType.Coaching,
                    Description = $"Coach {coachingSession.Coach?.User?.FullName ?? coachingSession.CoachId.ToString()} - {coachDurationHours:F2}h",
                    UnitPrice = coachRate,
                    Quantity = coachDurationHours,
                    Total = coachTotal
                });
            }

            var depositApplied = 0m;
            if (session.Reservation != null)
            {
                depositApplied = session.Reservation.Payments
                    .Where(p => p.Status == PaymentStatus.Completed &&
                                (p.Type == PaymentType.Deposit || p.Type == PaymentType.Refund))
                    .Sum(p => p.Amount);
                if (depositApplied < 0)
                {
                    depositApplied = 0;
                }
            }

            if (discountAmount > 0)
            {
                lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    Type = InvoiceLineType.Discount,
                    Description = $"Membership discount {discountPercent:F0}%",
                    UnitPrice = -discountAmount,
                    Quantity = 1,
                    Total = -discountAmount
                });
            }

            if (depositApplied > 0)
            {
                lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    Type = InvoiceLineType.DepositDeduction,
                    Description = "Deposit applied",
                    UnitPrice = -depositApplied,
                    Quantity = 1,
                    Total = -depositApplied
                });
            }

            var grandTotal = Math.Max(0, Math.Round(tableCost + fnbTotal + coachingTotal - discountAmount, 2));
            var balanceDue = Math.Max(0, Math.Round(grandTotal - depositApplied, 2));
            var paymentCompletedAt = DateTime.UtcNow;

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TableSessionId = session.Id,
                UserId = session.UserId,
                GuestName = session.GuestName,
                TableId = session.TableId,
                TableNumber = session.Table.TableNumber,
                TableType = session.Table.Type,
                ServiceDate = session.StartedAt.Date,
                ServiceStartedAt = session.StartedAt,
                ServiceEndedAt = serviceEndedAt,
                PaymentCompletedAt = paymentCompletedAt,
                SessionDurationHours = Math.Round(durationHours, 2),
                TableHourlyRateSnapshot = tableRate,
                TableTimeCost = tableCost,
                FnBTotal = fnbTotal,
                CoachingTotal = coachingTotal,
                DiscountAmount = discountAmount,
                MembershipDiscountPercent = discountPercent,
                MembershipTierSnapshot = membershipTier,
                DepositApplied = depositApplied,
                GrandTotal = grandTotal,
                BalanceDue = balanceDue,
                Lines = lines
            };

            var paymentAmount = session.Type == SessionType.WalkIn ? grandTotal : balanceDue;
            var paymentType = session.Type == SessionType.WalkIn ? PaymentType.FullPayment : PaymentType.FinalSettlement;

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = session.UserId,
                InvoiceId = invoice.Id,
                Type = paymentType,
                Method = paymentMethod,
                Status = PaymentStatus.Completed,
                Amount = paymentAmount,
                Notes = session.Type == SessionType.WalkIn ? "Walk-in full settlement" : "Final settlement at checkout",
                CreatedAt = paymentCompletedAt,
                CompletedAt = paymentCompletedAt
            };

            invoice.Payments.Add(payment);
            _context.Invoices.Add(invoice);

            await _auditService.LogAsync(
                AuditAction.PaymentRecorded,
                nameof(Invoice),
                invoice.Id.ToString(),
                actorUserId,
                newValue: new { payment.Type, payment.Amount, payment.Method, payment.Status });

            await _context.SaveChangesAsync();

            session.InvoiceId = invoice.Id;
            return invoice;
        }

        public async Task<InvoiceResponse?> GetInvoiceAsync(Guid invoiceId)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Lines)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            return invoice == null ? null : MapInvoice(invoice);
        }

        public static InvoiceResponse MapInvoice(Invoice invoice)
        {
            return new InvoiceResponse
            {
                Id = invoice.Id,
                TableSessionId = invoice.TableSessionId,
                UserId = invoice.UserId,
                GuestName = invoice.GuestName,
                TableId = invoice.TableId,
                TableNumber = invoice.TableNumber,
                TableType = invoice.TableType.ToString(),
                ServiceDate = invoice.ServiceDate,
                ServiceStartedAt = invoice.ServiceStartedAt,
                ServiceEndedAt = invoice.ServiceEndedAt,
                PaymentCompletedAt = invoice.PaymentCompletedAt,
                SessionDurationHours = invoice.SessionDurationHours,
                TableHourlyRateSnapshot = invoice.TableHourlyRateSnapshot,
                TableTimeCost = invoice.TableTimeCost,
                FnBTotal = invoice.FnBTotal,
                CoachingTotal = invoice.CoachingTotal,
                DiscountAmount = invoice.DiscountAmount,
                MembershipDiscountPercent = invoice.MembershipDiscountPercent,
                MembershipTierSnapshot = invoice.MembershipTierSnapshot,
                DepositApplied = invoice.DepositApplied,
                GrandTotal = invoice.GrandTotal,
                BalanceDue = invoice.BalanceDue,
                CreatedAt = invoice.CreatedAt,
                Lines = invoice.Lines.Select(line => new InvoiceLineResponse
                {
                    Id = line.Id,
                    Type = line.Type.ToString(),
                    Description = line.Description,
                    UnitPrice = line.UnitPrice,
                    Quantity = line.Quantity,
                    Total = line.Total
                }).ToList(),
                Payments = invoice.Payments.Select(MapPayment).ToList()
            };
        }

        public static PaymentResponse MapPayment(Payment payment)
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
