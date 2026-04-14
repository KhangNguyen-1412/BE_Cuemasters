using System.Threading.Tasks;

namespace BilliardsBooking.API.Services
{
    public interface IEmailQueueService
    {
        Task EnqueueEmailAsync(string to, string subject, string body);
    }
}
