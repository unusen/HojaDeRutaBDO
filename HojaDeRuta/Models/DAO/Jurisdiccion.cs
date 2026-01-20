using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO
{
    public class Jurisdiccion
    {
        [Column("name")]
        public string Name { get; set; }
    }
}
