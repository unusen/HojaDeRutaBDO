using HojaDeRuta.Models.DAO;

namespace HojaDeRuta.Models.DTO
{
    public class NotificationDispatchRequest
    {
        public string JobId { get; set; } = string.Empty;
        public string HojaId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string UrlRedireccion { get; set; } = string.Empty;
        public EMailBody? EMailBody { get; set; }
        public Hoja? Hoja { get; set; }
        public string? Firmante { get; set; }
        public string? Rechazador { get; set; }
        public List<string> Recipients { get; set; } = new();
    }
}
