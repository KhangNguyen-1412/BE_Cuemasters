using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using BilliardsBooking.API.Data;
using BilliardsBooking.API.Enums;
using BilliardsBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BilliardsBooking.API.Services
{
    public class EmailBackgroundWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailBackgroundWorker> _logger;
        private readonly IConfiguration _config;

        public EmailBackgroundWorker(
            IServiceProvider serviceProvider,
            ILogger<EmailBackgroundWorker> logger,
            IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Background Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessEmailsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing emails.");
                }

                // Poll every 30 seconds
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }

            _logger.LogInformation("Email Background Worker is stopping.");
        }

        private async Task ProcessEmailsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pendingEmails = await context.QueuedEmails
                .Where(e => e.Status == QueuedEmailStatus.Pending && 
                            (e.NextAttemptAt == null || e.NextAttemptAt <= DateTime.UtcNow))
                .OrderBy(e => e.CreatedAt)
                .Take(10) // Process in batches
                .ToListAsync(stoppingToken);

            if (!pendingEmails.Any()) return;

            var smtpSettings = _config.GetSection("SmtpSettings");
            var host = smtpSettings["Host"];
            var port = int.Parse(smtpSettings["Port"] ?? "587");
            var username = smtpSettings["Username"];
            var password = smtpSettings["Password"];
            var fromEmail = smtpSettings["FromEmail"] ?? "noreply@cuemasters.com";
            var enableSsl = bool.Parse(smtpSettings["EnableSsl"] ?? "true");

            if (string.IsNullOrEmpty(host))
            {
                _logger.LogWarning("SMTP Host is not configured. Logging emails to console for development.");
                foreach (var email in pendingEmails)
                {
                    _logger.LogInformation("DEV EMAIL LOG: To: {To}, Subject: {Subject}, Body: {Body}", 
                        email.To, email.Subject, email.Body);
                    
                    email.Status = QueuedEmailStatus.Sent;
                    email.LastAttemptAt = DateTime.UtcNow;
                }
                await context.SaveChangesAsync(stoppingToken);
                return;
            }

            using var smtpClient = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl
            };

            foreach (var email in pendingEmails)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(fromEmail, "CueMasters Billiards"),
                        Subject = email.Subject,
                        Body = email.Body,
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(email.To);

                    await smtpClient.SendMailAsync(mailMessage);

                    email.Status = QueuedEmailStatus.Sent;
                    email.LastAttemptAt = DateTime.UtcNow;
                    _logger.LogInformation("Successfully sent email to {To}", email.To);
                }
                catch (Exception ex)
                {
                    email.RetryCount++;
                    email.LastAttemptAt = DateTime.UtcNow;
                    email.ErrorMessage = ex.Message;

                    if (email.RetryCount >= email.MaxRetries)
                    {
                        email.Status = QueuedEmailStatus.Failed;
                        _logger.LogError(ex, "Failed to send email to {To} after {MaxRetries} retries.", email.To, email.MaxRetries);
                    }
                    else
                    {
                        // Exponential backoff: 2min, 4min, 8min...
                        var backoffMinutes = Math.Pow(2, email.RetryCount);
                        email.NextAttemptAt = DateTime.UtcNow.AddMinutes(backoffMinutes);
                        _logger.LogWarning(ex, "Error sending email to {To}. Retrying in {Minutes} minutes.", email.To, backoffMinutes);
                    }
                }
            }

            await context.SaveChangesAsync(stoppingToken);
        }
    }
}
