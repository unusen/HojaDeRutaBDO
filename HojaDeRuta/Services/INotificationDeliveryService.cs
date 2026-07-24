using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;

namespace HojaDeRuta.Services
{
    public interface INotificationDeliveryService
    {
        Task SendApprovalAsync(EMailBody eMailBody, string urlRedireccion);
        Task SendRejectionAsync(EMailBody eMailBody, string rechazador, string urlRedireccion);
        Task SendSignatureAsync(EMailBody eMailBody, string firmante, string urlRedireccion);
        Task SendCrossAccessAsync(Hoja hoja, string urlRedireccion);
        Task SendWeeklyPendingAsync(HojaPendiente pendiente);
    }
}
