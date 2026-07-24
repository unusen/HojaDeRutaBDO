using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class Contratos
    {
        [Column("ID")]
        public int Id { get; set; }
        public string CodigoPlataforma { get; set; }
        public string? RazonSocial { get; set; }
        public string Contrato { get; set; }
        public bool EsManual { get; set; }
        [Column(TypeName = "date")]
        public DateTime? FechaAlta { get; set; }
    }
}
