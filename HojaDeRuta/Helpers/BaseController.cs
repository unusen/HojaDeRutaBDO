using HojaDeRuta.Models.DTO;
using HojaDeRuta.Services.LoginService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

public abstract class BaseController : Controller, IAsyncActionFilter
{
    protected UserContext CurrentUser { get; private set; }
    private readonly ILoginService _loginService;

    public BaseController(ILoginService loginService)
    {
        _loginService = loginService;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
        {
            await next();
            return;
        }

        if (!User.Identity?.IsAuthenticated ?? false)
        {
            context.Result = Challenge(new AuthenticationProperties
            {
                RedirectUri = context.HttpContext.Request.Path
            });
            return;
        }

        var oid = _loginService.GetUserId();
        var userGr = _loginService.GetGraphUserByOid(oid);

        CurrentUser = new UserContext
        {
            UserName = _loginService.GetUserName(),
            Email = _loginService.GetUserEmail(),
            Area = await _loginService.GetUserAreaAsync(),
            Cargo = await _loginService.GetUserCargoAsync(),
            Roles = await _loginService.GetUserGroupsAsync()
        };

        await next();
    }
}
