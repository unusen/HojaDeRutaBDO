namespace HojaDeRuta.Models.DTO
{
    public class AuditoriaInputModel
    {
        public string? HojaId { get; set; }
        public string? Activo { get; set; }
        public string? Pasivo { get; set; }
        public string? PatrimonioNeto { get; set; }
        public string? Moneda { get; set; }
        public string? TipoNumeracion { get; set; }
        public string? Resultado { get; set; }
        public string? TotalIngresos { get; set; }
        public string? TotalOtrosIngresos { get; set; }

        public bool TieneDatos()
        {
            return new[]
            {
                Activo, Pasivo, PatrimonioNeto, Moneda, TipoNumeracion,
                Resultado, TotalIngresos, TotalOtrosIngresos
            }.Any(value => !string.IsNullOrWhiteSpace(value));
        }
    }
}
