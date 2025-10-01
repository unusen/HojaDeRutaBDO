using HojaDeRuta.Models;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.Enums;
using HojaDeRuta.Models.ViewModels;
using HojaDeRuta.Services;
using HojaDeRuta.Services.LoginService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;

namespace HojaDeRuta.Controllers
{
    //TODO: ACTIVAR AUTORIZACION EN CONTROLADOR
    //[Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CreatioService _creatioService;
        private readonly HojaDeRutaService _hojaDeRutaService;
        private readonly ClienteService _clienteService;
        private readonly SharedService _sharedService;
        private readonly ILoginService _loginService;
        private readonly MailService _mailService;
        private readonly RevisorService _revisorService;

        public HomeController(
            ILogger<HomeController> logger,
            CreatioService creatioService,
            HojaDeRutaService hojaDeRutaService,
            ClienteService clienteService,
            SharedService sharedService,
            ILoginService loginService,
            MailService mailService,
            RevisorService revisorService
            )
        {
            _logger = logger;
            _creatioService = creatioService;
            _hojaDeRutaService = hojaDeRutaService;
            _clienteService = clienteService;
            _sharedService = sharedService;
            _loginService = loginService;
            _mailService = mailService;
            _revisorService = revisorService;
        }

        [HttpPost]
        public async Task<IActionResult> SignOut()
        {
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10, string sortOrder = "Numero", string sortDirection = "asc")
        {
            //TODO: ACTIVAR DATOS DE LOGIN
            //try
            //{

            //    var name = await _loginService.GetUserNameAsync();
            //    var email = await _loginService.GetUserEmailAsync();
            //    var roles = await _loginService.GetUserGroupsAsync();

            //    var area = await _loginService.GetUserAreaAsync();
            //    var cargo = await _loginService.GetUserCargoAsync();
            //}
            //catch (Exception)
            //{
            //    return Challenge();
            //}



            //TODO: FILTRAR Y ORDENAR POR FECHA, ORDENAR POR LAS RESTANTES COLUMNAS
            ViewBag.CurrentSection = "Home";

            ViewBag.Estados = Enum.GetValues(typeof(Estado))
                .Cast<Estado>()
                .Select(e => new { Id = (int)e, Desc = e.ToString() })
                .ToList();

            //PARA TEST
            var parameters = new Dictionary<string, string>
            {
                { "Nivel", "11" },
                { "Sector", "AUDI" },
                { "Usuario ", "CSZULZYK" }
            };

            //var parameters = new Dictionary<string, string>
            //{
            //    { "Nivel", roles.FirstOrDefault().Nivel.ToString() },
            //    { "Sector", "AUDI" },
            //    { "Usuario ", name }
            //};

            var allHojas = await _hojaDeRutaService.GetHojas(parameters);

            //Ordenamiento
            allHojas = sortOrder switch
            {
                "Numero" => sortDirection == "asc" ? allHojas.OrderBy(h => h.Numero).ToList() : allHojas.OrderByDescending(h => h.Numero).ToList(),
                "Cliente" => sortDirection == "asc" ? allHojas.OrderBy(h => h.ClienteName).ToList() : allHojas.OrderByDescending(h => h.ClienteName).ToList(),
                "Estado" => sortDirection == "asc" ? allHojas.OrderBy(h => h.Estado).ToList() : allHojas.OrderByDescending(h => h.Estado).ToList(),
                "Fecha" => sortDirection == "asc" ? allHojas.OrderBy(h => h.FechaDocumento).ToList() : allHojas.OrderByDescending(h => h.FechaDocumento).ToList(),
                _ => sortDirection == "asc" ? allHojas.OrderBy(h => h.Numero).ToList() : allHojas.OrderByDescending(h => h.Numero).ToList(),
            };

            var totalItems = allHojas.Count;
            var pagedItems = allHojas.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var pagedList = new PagedList<Hoja>
            {
                Items = pagedItems,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            ViewBag.CurrentSort = sortOrder;
            ViewBag.SortDirection = sortDirection;

            ViewBag.HojasJson = JsonConvert.SerializeObject(allHojas);

            return View(pagedList);
        }

        public async Task<IActionResult> Create()
        {
            return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Create });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Hoja hoja)
        {
            //TODO: GUARDAR LA HOJA CREADA EN LA BASE
            //TODO: VALIDAR EL MODELO CORRECTAMENTE
            if (ModelState.IsValid)
            {
                await _hojaDeRutaService.CreateHoja(hoja);
                return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Update, id = hoja.Id });
            }

            return RedirectToAction(nameof(Upsert), new {
                mode = ViewMode.Create,
                id = "",
                hoja = hoja            
            });

        }

        public async Task<IActionResult> Edit(string id)
        {
            return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Update, id = id.ToString() });
        }

        public async Task<IActionResult> Details(string id)
        {
            return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Visualize, id = id.ToString() });
        }

        public async Task<IActionResult> Upsert(ViewMode mode, string id, Hoja? hoja)
        {
            //TODO: VER COMO SE LLENAN TODOS LOS CAMPOS EN UN CREATE Y EDIT

            //TODO: GENERAR PROCESO BACKGROUND PARA ACTUALIZAR CONTRATOS A LA NOCHE

            //TODO: NUEVA VENTANA AUDITORIA


            ViewBag.CurrentSection = "Upsert";
            ViewBag.Detail = false;

            //Hoja hoja = new Hoja();

            if (mode == ViewMode.Visualize)
            {
                ViewBag.Detail = true;

                ViewData["Title"] = "Visualizar Hoja de Ruta";
                hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);

                return View(hoja);
            }

            List<Clientes> clientes = await _clienteService.GetClientes();
            List<TipoDocumento> tiposDocumento = await _sharedService.GetTipoDocumentos();
            List<Sector> sectores = await _sharedService.GetSectores();
            List<Socios> socios = await _sharedService.GetAllSocios();
            List<SubArea> subAreas = await _sharedService.GetSubAreas();

            ViewBag.Clientes = clientes.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.RazonSocial
            }).ToList();

            ViewBag.ClientesJson = System.Text.Json.JsonSerializer.Serialize(clientes);

            ViewBag.NombreGenerico = tiposDocumento.Select(c => new SelectListItem
            {
                Value = c.NombreGenerico,
                Text = c.NombreGenerico
            }).ToList();

            ViewBag.Sectores = sectores.Select(c => new SelectListItem
            {
                Value = c.Nombre,
                Text = c.Nombre
            }).ToList();

            ViewBag.Subareas = System.Text.Json.JsonSerializer.Serialize(subAreas);

            ViewBag.Sindicos = socios.Select(c => new SelectListItem
            {
                Value = c.Socio,
                Text = c.Detalle
            }).ToList();

            if (mode == ViewMode.Create)
            {
                ViewData["Title"] = "Crear Hoja de Ruta";
                hoja = hoja != null ? hoja : new Hoja();

                //TODO: LLENAR CAMPO PREPARO CON EL USUARIO LOGUEADO, ES MODIFICABLE?
                hoja.Preparo = "CSZULZYK";
                hoja.PreparoFecha = DateTime.Now.ToShortDateString();
                hoja.FechaDocumento = DateTime.Now.ToShortDateString();
            }
            else if (mode == ViewMode.Update)
            {
                ViewData["Title"] = "Editar Hoja de Ruta";
                hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);
                //hoja.Preparo = "DGONZALEZ"; //5
                //hoja.Reviso = "CORTOLANI"; //6
                //hoja.RevisionGerente = "RevisionGerente";
                //hoja.EngagementPartner = "EngagementPartner";
                //hoja.SocioFirmante = null;

                ViewBag.CampoHabilitado = await GetCampoHabilitado(hoja);

                int nivelActual = await GetNivelRevisorActual(hoja);

                var parameters = new Dictionary<string, int>
                {
                    { "NivelActual", nivelActual }
                };

                //TODO: AGREGAR LOGICA DE QUE NO PUEDE REPETIRSE REVISOR
                List<Revisores> revisores = await _revisorService.GetRevisoresByNivel(parameters);

                ViewBag.Revisores = revisores.Select(c => new SelectListItem
                {
                    Value = c.Empleado,
                    Text = c.Detalle
                }).ToList();

                List<Revisores> gestores = await _revisorService.GetAllRevisores();

                ViewBag.Gestores = gestores.Select(c => new SelectListItem
                {
                    Value = c.Empleado,
                    Text = c.Detalle
                }).ToList();                
            }

            return View(hoja);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Hoja hoja, string mode)
        {
            //TODO: IMPACTAR CREATE Y EDIT EN LA BASE

            ViewBag.Detail = false;

            if (mode == "Update")
            {
                ViewData["Title"] = "Editar Hoja de Ruta";

                //TODO: LOGICA DE REVISOR, SI FUE RECIEN AGREGADO SE NOTIFICA
                //await NotificarRevisor(hoja.Id,hoja.re);

                //TODO: LOGICA DE APROBACION DE HOJA
                //TODO: LOGICA DE RECHAZO DE HOJA

                return View(hoja);
            }
            else if (mode == "Create")
            {

            }

            return View(hoja);
        }

        public async Task<IActionResult> Rechazar(Hoja hoja, string rechazador, string motivo)
        {
            //TODO: EL BOTON RECHAZAR SOLO PUEDE VERLO EL REVISOR ACTUAL
            //TODO: CAMBIAR ESTADO DE LA HOJA A RECHAZADO


            //OBTENER TODOS LOS REVISORES DE LA HOJA
            List<string> emails = new List<string>()
            {
                "sebastian.katcheroff@gmail.com"
            };

            string subject = $"La hoja de ruta {hoja.Numero} ha sido rechazada";

            var url = Url.Action(
               nameof(Upsert),
               "Home",
               new { mode = ViewMode.Visualize, id = hoja.Id }, protocol: Request.Scheme);

            string body = await _mailService.GetBodyInformarRechazo(url, hoja.Numero, rechazador, motivo);

            await _mailService.SendMailAsync(
                subject,
                emails,
                body,
                true
                );

            return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Visualize, id = hoja.Id });
        }

        public async Task NotificarRevisor(string hojaId, string revisorId, string numeroHoja)
        {
            //HACER GET DE LOS DATOS DEL REVISOR POR revisorId
            List<string> emails = new List<string>()
            {
                "sebastian.katcheroff@gmail.com"
            };

            string nombre = "Sebastian Katcheroff";

            string subject = $"La hoja de ruta {numeroHoja} requiere su evaluación";

            var url = Url.Action(
                nameof(Upsert),
                "Home",
                new { mode = ViewMode.Update, id = hojaId }, protocol: Request.Scheme);

            string body = await _mailService.GetBodyInformarRevisor(url, numeroHoja, nombre);

            await _mailService.SendMailAsync(
                subject,
                emails,
                body,
                true
                );
        }

        public async Task<string> GetCampoHabilitado(Hoja hoja, bool obtenerAnterior = false)
        {           
            var pasosFlujo = new List<(string Nombre, string Valor)>
            {
                ("Preparo", hoja.Preparo),
                ("Reviso", hoja.Reviso),
                ("RevisionGerente", hoja.RevisionGerente),
                ("EngagementPartner", hoja.EngagementPartner),
                ("SocioFirmante", hoja.SocioFirmante)
            };

            for (int i = 0; i < pasosFlujo.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(pasosFlujo[i].Valor))
                {
                    if (obtenerAnterior && i > 0)
                    {
                        return pasosFlujo[i - 1].Nombre;
                    }

                    return pasosFlujo[i].Nombre;
                }
            }

            return pasosFlujo.Last().Nombre;
        }

        public async Task<int> GetNivelRevisorActual(Hoja hoja)
        {
            string CampoAnterior = await GetCampoHabilitado(hoja, true);

            var revisor = typeof(Hoja)
                    .GetProperty(CampoAnterior)
                    ?.GetValue(hoja, null)?.ToString();

            Revisores revisorActual = await _revisorService.GetRevisorByName(revisor);

            return revisorActual.Cargo.Value;
        }

        public async Task<List<string>> GetContratosByCodigo(string codigoPlataforma)
        {
            List<Contratos> contratos = await _sharedService.GetContratos(codigoPlataforma);
            List<string> contratosName = contratos.Select(c => c.Contrato).ToList();
            return contratosName;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<string> ValidarCreate(Hoja hoja)
        {
            string result = "";

            return result;
        }
    }
}
