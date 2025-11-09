using HojaDeRuta.Models.DAO;

namespace HojaDeRuta.Models.DTO
{
    public class EMailBody
    {
        public string HojaId { get; set; }
        public string NumeroHoja { get; set; }
        public string Sector { get; set; }
        public string RutaDoc { get; set; }
        public string RutaPapeles { get; set; }
        public string Cliente { get; set; }
        public string? MotivoDeRechazo { get; set; }
        public Revisores Revisor { get; set; }

    }
}
