using Microsoft.AspNetCore.Mvc;

namespace HojaDeRuta.Controllers
{
    public class ErrorController : Controller
    {
        public IActionResult Index(string message)
        {
            ViewBag.Message = string.IsNullOrEmpty(message)
                ? "Ocurrió un error inesperado. Consulte a su administrador para mas detalles."
                : message;

            return View();
        }

        public IActionResult AccessDenied(string message)
        {
            ViewBag.Message = string.IsNullOrEmpty(message)
                ? "Acceso denegado a la aplicación. Consulte a su administrador"
                : message;

            return View();
        }
    }
}
