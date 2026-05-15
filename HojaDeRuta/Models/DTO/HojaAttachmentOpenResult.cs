namespace HojaDeRuta.Models.DTO
{
    public class HojaAttachmentOpenResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? PhysicalPath { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }
}
