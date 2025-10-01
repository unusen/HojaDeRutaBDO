using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    [Table("Clientes_Creatio")]
    public class Clientes
    {
        [Column("ID")]
        public int Id { get; set; }
        public string RazonSocial { get; set; }
        public string CodigoPlataforma { get; set; }
    }
}
