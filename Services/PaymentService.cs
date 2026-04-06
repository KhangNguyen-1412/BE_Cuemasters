using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.DTOs;
using BilliardsBooking.API.Models;
using BilliardsBooking.API.Enums;

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

        public PaymentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentResponse?> CreatePaymentAsync(CreatePaymentRequest request)
        {
            var bookingId = Guid.Parse(request.BookingId);
            var booking = await _context.Bookings.FindAsync(bookingId);
            if (booking == null) return null;

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                UserId = booking.UserId,
                Amount = booking.TotalTableCost, /* Booking doesn't have TotalPrice, wait */
                Method = request.PaymentMethod == "Cash" ? PaymentMethod.Cash : PaymentMethod.VnPay,
                Status = PaymentStatus.Completed, // Assume immediate success for MVP
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            booking.Status = BookingStatus.Confirmed; // Update booking status when paid
            await _context.SaveChangesAsync();

            return new PaymentResponse
            {
                Id = payment.Id.ToString(),
                BookingId = payment.BookingId?.ToString() ?? "",
                Amount = payment.Amount,
                Method = payment.Method.ToString(),
                Status = payment.Status.ToString(),
                CreatedAt = payment.CreatedAt
            };
        }

        public async Task<List<PaymentResponse>> GetPaymentsByBookingAsync(Guid bookingId)
        {
            return await _context.Payments
                .Where(p => p.BookingId == bookingId)
                .Select(p => new PaymentResponse
                {
                    Id = p.Id.ToString(),
                    BookingId = p.BookingId.ToString() ?? "",
                    Amount = p.Amount,
                    Method = p.Method.ToString(),
                    Status = p.Status.ToString(),
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();
        }
    }
}