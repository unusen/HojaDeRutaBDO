using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;

namespace HojaDeRuta.Services
{
    public interface INotificationQueueService
    {
        Task QueueApprovalAsync(EMailBody eMailBody, string urlRedireccion, string? title = null);
        Task QueueRejectionAsync(EMailBody eMailBody, string rechazador, string urlRedireccion);
        Task QueueSignatureAsync(EMailBody eMailBody, string firmante, string urlRedireccion, string? title = null);
        Task QueueCrossAccessAsync(Hoja hoja, string urlRedireccion);
        Task<IReadOnlyCollection<NotificationStatusSnapshot>> GetStatusesAsync(string hojaId);
        Task<NotificationStatusSnapshot> RetryAsync(string hojaId, string jobId);
    }
}
