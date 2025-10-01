using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class Contratos
    {
        [Column("ID")]
        public int Id { get; set; }
        public string CodigoPlataforma { get; set; }
        public string Contrato { get; set; }
    }
}
