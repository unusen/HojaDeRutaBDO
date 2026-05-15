namespace HojaDeRuta.Models.DTO
{
    public class HojaIndexQuery
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortField { get; set; } = "Numero";
        public string SortDirection { get; set; } = "asc";
        public bool Pendientes { get; set; } = true;
        public string? Numero { get; set; }
        public string? Cliente { get; set; }
        public int? Estado { get; set; }
        public string? Sector { get; set; }
        public string? Socio { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }
}
