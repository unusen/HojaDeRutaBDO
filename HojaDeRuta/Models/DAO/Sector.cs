using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class Sector
    {
        [Column("sector")]
        public string Nombre { get; set; }

        [Column("detalle")]
        public string Detalle { get; set; }

        [Column("Hdr_Activo")]
        public bool Hdr_Activo { get; set; } = true;
    }
}
