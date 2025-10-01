using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class TipoDocumento
    {
        [Column("nombre_generico")]
        public string NombreGenerico { get; set; }
    }
}
