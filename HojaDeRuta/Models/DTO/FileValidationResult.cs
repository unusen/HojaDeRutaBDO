namespace HojaDeRuta.Models.DTO
{
    public class FileValidationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "success";
    }
}
