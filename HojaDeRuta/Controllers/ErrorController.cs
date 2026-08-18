using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HojaDeRuta.Controllers
{
    [AllowAnonymous]
    public class ErrorController : Controller
    {
        [Route("Error")]
        public IActionResult Index(string? message, string? incidentId)
        {
            if (!string.IsNullOrEmpty(message))
            {
                ViewBag.Message = message;
            }
            else if (!string.IsNullOrWhiteSpace(incidentId))
            {
                ViewBag.Message = "Ocurrió un error inesperado al procesar la solicitud. Intentá nuevamente en unos instantes.";
            }
            else
            {
                ViewBag.Message = "Ocurrio un error inesperado. Consulte a su administrador.";
            }

            ViewBag.IncidentId = incidentId;

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
