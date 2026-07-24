using HojaDeRuta.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HojaDeRuta.Helpers
{
    public class RequireGroupsFilter : IAsyncAuthorizationFilter
    {
        private readonly IUserContextCacheService _userContextCacheService;
        private readonly ILogger<RequireGroupsFilter> _logger;

        public RequireGroupsFilter(
            IUserContextCacheService userContextCacheService,
            ILogger<RequireGroupsFilter> logger)
        {
            _userContextCacheService = userContextCacheService;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            if (controller == "Error")
            {
                return;
            }

            var userContext = await _userContextCacheService.GetCurrentUserAsync(context.HttpContext.RequestAborted);

            if (userContext.Roles == null || !userContext.Roles.Any())
            {
                _logger.LogWarning(
                    "Acceso denegado para el usuario {User}. No posee grupos configurados en Azure AD.",
                    userContext.UserName);

                context.Result = new RedirectToActionResult(
                    "AccessDenied",
                    "Error",
                    new { message = $"El usuario {userContext.UserName} no tiene permisos para Hoja de Ruta." });
            }
        }
    }
}
