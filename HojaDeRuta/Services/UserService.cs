using HojaDeRuta.Services.LoginService;

namespace HojaDeRuta.Services
{
    public class UserService
    {
        private readonly ILoginService _loginService;
        private readonly ILogger<UserService> _logger;

        public UserService(ILoginService loginService, ILogger<UserService> logger)
        {
            _loginService = loginService;
            _logger = logger;
        }

        public async Task ValidateUserAsync(string oid)
        {
            await Task.CompletedTask;
            _logger.LogInformation(
                "ValidateUserAsync fue invocado para {Oid}. La sincronizacion efectiva del usuario se realiza al resolver UserContext.",
                oid ?? "(null)");
        }
    }
}
