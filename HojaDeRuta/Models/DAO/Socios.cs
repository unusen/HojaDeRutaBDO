using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class Socios
    {
        [Column("socio")]
        public string Socio { get; set; }

        [Column("detalle")]
        public string Detalle { get; set; }

        [Column("mail")]
        public string Mail { get; set; }

        [Column("sector")]
        public string? Sector { get; set; }

        [Column("liderDeArea")]
        public bool LiderDeArea { get; set; }

        [Column("entra_object_id")]
        public string? EntraObjectId { get; set; }

        public bool Hdr_Activo { get; set; } = true;
    }
}
