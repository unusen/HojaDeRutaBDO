using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class HojaEstado
    {
        [Column("ID")]
        public int Id { get; set; }
        public string HojaId { get; set; }
        public string Etapa { get; set; }
        public string Estado { get; set; }
    }
}
