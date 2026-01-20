using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using HojaDeRuta.Controllers;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Services.LoginService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;

public abstract class BaseController : Controller, IAsyncActionFilter
{
    protected UserContext CurrentUser { get; private set; }
    protected string? UserError { get; private set; }

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

        try
        {
            CurrentUser = new UserContext
            {
                UserName = _loginService.GetUserName(),
                Empleado = _loginService.GetUserName().Split('@')[0],
                Email = _loginService.GetUserEmail(),
                Area = await _loginService.GetUserAreaAsync(),
                //Cargo = await _loginService.GetUserCargoAsync(),
                Roles = await _loginService.GetUserGroupsAsync()
            };

            //TODO TEST:
            //if (CurrentUser.UserName == "HDR_Testing_1")
            //{
            //    CurrentUser.Area = "AUDI";
            //    GroupConfig groupConfig = new GroupConfig
            //    {
            //        Name = "HDR_Socios_General",
            //        GroupId = "827621be-0983-4006-8754-e1528da50706",
            //        Nivel = 9
            //    };
            //    IList<GroupConfig> roles = new List<GroupConfig>
            //    {
            //         groupConfig
            //    };
            //    CurrentUser.Roles = roles;
            //}
            //else if (CurrentUser.UserName == "HDR_Testing_2")
            //{
            //    CurrentUser.Area = "BANK";
            //    GroupConfig groupConfig = new GroupConfig
            //    {
            //        Name = "HDR_Directores",
            //        GroupId = "",
            //        Nivel = 8
            //    };
            //    IList<GroupConfig> roles = new List<GroupConfig>
            //    {
            //         groupConfig
            //    };
            //    CurrentUser.Roles = roles;
            //}
            //else if (CurrentUser.UserName == "HDR_Testing_3")
            //{
            //    CurrentUser.Area = "BANK";
            //    GroupConfig groupConfig = new GroupConfig
            //    {
            //        Name = "HDR_Supervisores",
            //        GroupId = "",
            //        Nivel = 6
            //    };
            //    IList<GroupConfig> roles = new List<GroupConfig>
            //    {
            //         groupConfig
            //    };
            //    CurrentUser.Roles = roles;
            //}
            //else if (CurrentUser.UserName == "HDR_Testing_4")
            //{
            //    CurrentUser.Area = "BANK";
            //    GroupConfig groupConfig = new GroupConfig
            //    {
            //        Name = "HDR_Gestores",
            //        GroupId = "bb28f657-54dd-426f-b75a-2ddb0bde6ca4",
            //        Nivel = 4
            //    };
            //    IList<GroupConfig> roles = new List<GroupConfig>
            //    {
            //         groupConfig
            //    };
            //    CurrentUser.Roles = roles;
            //}
            //else if (CurrentUser.UserName == "HDR_Testing")
            //{
            //    CurrentUser.Area = "BANK";  
            //}

            await _loginService.SyncUsuariosLogueados(
                CurrentUser.UserName,
                CurrentUser.Email,
                CurrentUser.Area,
                CurrentUser.Cargo,
                CurrentUser.Roles);
        }
        catch (Exception ex)
        {
            UserError = ex.Message;
            throw;
        }

        await next();
    }
}
