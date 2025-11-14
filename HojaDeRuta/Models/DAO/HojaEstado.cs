using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class HojaEstado
    {
        [Column("ID")]
        public int HojaEstadoId { get; set; }
        public string? HojaId { get; set; }
        public string? Etapa { get; set; }
        public int? Estado { get; set; }
        public string? Revisor { get; set; }
        public string? MotivoDeRechazo { get; set; }
    }
}
