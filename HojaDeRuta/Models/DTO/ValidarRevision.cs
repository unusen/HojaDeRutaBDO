using HojaDeRuta.Models.DAO;

namespace HojaDeRuta.Models.DTO
{
    public class ValidarRevision
    {
        public string Error { get; set; }
        public Hoja Hoja { get; set; }
        public HojaEstado Estado { get; set; }
    }
}
