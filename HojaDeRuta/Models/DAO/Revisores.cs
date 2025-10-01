using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class Revisores
    {
        [Column("empleado")]
        public string? Empleado { get; set; }

        [Column("detalle")]
        public string? Detalle { get; set; }

        [Column("mail")]
        public string? Mail { get; set; }

        [Column("cargo")]
        public int? Cargo { get; set; }

        [Column("area")]
        public string? Area { get; set; }

        [Column("subarea")]
        public string? Subarea { get; set; }
    }
}
