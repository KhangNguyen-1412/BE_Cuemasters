using System.Threading.Tasks;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.Models;
using BilliardsBooking.API.Enums;

namespace BilliardsBooking.API.Services
{
    public class EmailQueueService : IEmailQueueService
    {
        private readonly AppDbContext _context;

        public EmailQueueService(AppDbContext context)
        {
            _context = context;
        }

        public async Task EnqueueEmailAsync(string to, string subject, string body)
        {
            var email = new QueuedEmail
            {
                To = to,
                Subject = subject,
                Body = body,
                Status = QueuedEmailStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                NextAttemptAt = DateTime.UtcNow // Try immediately
            };

            _context.QueuedEmails.Add(email);
            await _context.SaveChangesAsync();
        }
    }
}
