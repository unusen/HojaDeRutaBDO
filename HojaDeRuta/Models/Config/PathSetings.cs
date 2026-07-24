namespace HojaDeRuta.Models.Config
{
    public class PathSetings
    {
        public string PathBase { get; set; } = string.Empty;
        public string? TempRoot { get; set; }
        public string? FinalRoot { get; set; }
        public string FileStorageMode { get; set; } = "Hybrid";
        public string? LocalOverridePath { get; set; }
    }
}
