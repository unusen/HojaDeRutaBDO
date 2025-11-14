using HojaDeRuta.Services.LoginService;

namespace HojaDeRuta.Services
{
    public class UserService
    {
        private readonly ILoginService _loginService;

        public UserService(ILoginService loginService)
        {
            _loginService = loginService;
        }

        public async Task ValidateUserAsync(string oid)
        {
            //guardar oid en una tabla, solo si no existe, junto con la fecha
            //recorrer esa tabla una vez por semana con el sync service
            //analizar que actualizar y que no
            //marcar los registros como procesados
            //eliminar los registros procesados de la tabla oid
        }
    }
}
