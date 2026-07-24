using HojaDeRuta.Models.Enums;

namespace HojaDeRuta.Models.DTO
{
    public class HojaArchivoDescriptor
    {
        public string? HojaId { get; set; }
        public string? NombreOriginal { get; set; }
        public string? NombreStorage { get; set; }
        public string? Hash { get; set; }
        public string? ContentType { get; set; }
        public HojaArchivoOrigen Origen { get; set; }
        public bool EsPrincipal { get; set; } = true;
        public string? RutaFinal { get; set; }
        public string Estado { get; set; } = "Disponible";
        public string? RutaFuente { get; set; }
    }
}
