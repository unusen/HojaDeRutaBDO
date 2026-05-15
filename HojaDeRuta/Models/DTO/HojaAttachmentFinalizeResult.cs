using HojaDeRuta.Models.Enums;

namespace HojaDeRuta.Models.DTO
{
    public class HojaAttachmentFinalizeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? FinalPath { get; set; }
        public string? FileName { get; set; }
        public HojaArchivoOrigen Origen { get; set; }
    }
}
