using HojaDeRuta.Models.Config;

namespace HojaDeRuta.Models.DTO
{
    public class UserContext
    {
        public string UserName { get; set; }
        public string Empleado { get; set; }
        public string Email { get; set; }
        public string Area { get; set; }
        public string Cargo { get; set; }
        public IList<GroupConfig> Roles { get; set; }
    }
}
