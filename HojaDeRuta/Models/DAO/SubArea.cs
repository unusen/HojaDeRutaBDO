using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class SubArea
    {
        [Column("subarea")]
        public string Nombre { get; set; }

        [Column("DETALLE")]
        public string Detalle { get; set; }

        [Column("SECTOR")]
        public string Sector { get; set; }
    }
}
