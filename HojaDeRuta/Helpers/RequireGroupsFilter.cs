namespace HojaDeRuta.Helpers
{
    using HojaDeRuta.Services.LoginService;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    public class RequireGroupsFilter : IAsyncAuthorizationFilter
    {
        private readonly ILoginService _loginService;

        public RequireGroupsFilter(ILoginService loginService)
        {
            _loginService = loginService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // No aplicar la validación en ErrorController
            var controller = context.RouteData.Values["controller"]?.ToString();
            if (controller == "Error")
            {
                return; 
            }

            var name =  _loginService.GetUserName();
            var groups = await _loginService.GetUserGroupsAsync();

            if (!groups.Any())
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Error",
                    new { message = $"El usuario {name} no tiene permisos para Hoja de Ruta." }
                );
            }

            //TODO: QUITAR VALIDACION DE MAS DE UN PERMISO POR USUARIO
            //if (groups.Count > 1)
            //{
            //    context.Result = new RedirectToActionResult("AccessDenied", "Error",
            //        new { message = $"Solo es posible un permiso por usuario." +
            //        $"El usuario {name} tiene {groups.Count} permisos para Hoja de Ruta." }
            //    );
            //}
        }
    }

}
