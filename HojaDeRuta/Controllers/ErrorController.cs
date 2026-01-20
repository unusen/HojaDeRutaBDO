using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HojaDeRuta.Controllers
{
    [AllowAnonymous]
    public class ErrorController : Controller
    {
        [Route("Error")]
        public IActionResult Index(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                ViewBag.Message = message;
                return View();
            }

            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();

            if (exceptionFeature != null)
            {
                var exception = exceptionFeature.Error;
                ViewBag.Message = exception.Message;
            }
            else
            {
                ViewBag.Message = "Ocurrio un error inesperado. Consulte a su administrador.";
            }

            return View();
        }

        [Route("AccessDenied")]
        public IActionResult AccessDenied(string message)
        {
            ViewBag.Message = string.IsNullOrEmpty(message)
                ? "Acceso denegado a la aplicación. Consulte a su administrador"
                : message;

            return View();
        }
    }
}
