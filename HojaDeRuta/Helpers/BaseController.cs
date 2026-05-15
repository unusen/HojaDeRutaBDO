using HojaDeRuta.Models.DTO;
using HojaDeRuta.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

public abstract class BaseController : Controller, IAsyncActionFilter
{
    protected UserContext CurrentUser { get; private set; } = new();
    protected string? UserError { get; private set; }

    private readonly IUserContextCacheService _userContextCacheService;

    protected BaseController(IUserContextCacheService userContextCacheService)
    {
        _userContextCacheService = userContextCacheService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
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

        try
        {
            CurrentUser = await _userContextCacheService.GetCurrentUserAsync(context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            UserError = ex.Message;
            throw;
        }

        await next();
    }
}
