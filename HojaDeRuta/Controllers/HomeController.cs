using AutoMapper;
using HojaDeRuta.Models;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using HojaDeRuta.Models.ViewModels;
using HojaDeRuta.Services;
using HojaDeRuta.Services.LoginService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Diagnostics;
using System.Globalization;

namespace HojaDeRuta.Controllers
{
    //TODO: ACTIVAR AUTORIZACION EN CONTROLADOR
    [Authorize]
    //[AllowAnonymous]
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;
        private readonly CreatioService _creatioService;
        private readonly HojaDeRutaService _hojaDeRutaService;
        private readonly ClienteService _clienteService;
        private readonly SharedService _sharedService;
        private readonly MailService _mailService;
        private readonly RevisorService _revisorService;
        private readonly FileService _fileService;
        private readonly IMapper _mapper;
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;

        //TODO: PARA TEST LOGIN, ELIMINAR EN PROD
        //private readonly UserContext CurrentUser;

        public HomeController(
            ILogger<HomeController> logger,
            CreatioService creatioService,
            HojaDeRutaService hojaDeRutaService,
            ClienteService clienteService,
            SharedService sharedService,
            ILoginService loginService,
            MailService mailService,
            RevisorService revisorService,
            FileService fileService,
            IMapper mapper,
            IRazorViewEngine viewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider
            ) : base(loginService)
        {
            _logger = logger;
            _creatioService = creatioService;
            _hojaDeRutaService = hojaDeRutaService;
            _clienteService = clienteService;
            _sharedService = sharedService;
            _mailService = mailService;
            _revisorService = revisorService;
            _fileService = fileService;
            _mapper = mapper;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;

            //TODO: PARA TEST LOGIN, ELIMINAR EN PROD
            //GroupConfig groupConfig = new GroupConfig
            //{
            //    Name = "HDR_Socio_líder_de_area",
            //    GroupId = "aa52727f-e60f-45bb-b4bf-84a3874c532a",
            //    Nivel = 9
            //};
            //IList<GroupConfig> roles = new List<GroupConfig>
            // {
            //     groupConfig
            // };

            //TODO: PARA TEST LOGIN, ELIMINAR EN PROD
            //CurrentUser = new UserContext
            //{
            //    UserName = "LUISROMERO",
            //    Email = "",
            //    Area = "AUDI",
            //    Cargo = "",
            //    Roles = roles
            //};
        }

        [HttpPost]
        public async Task<IActionResult> SignOut()
        {
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10, string sortOrder = "Numero", string sortDirection = "asc", bool pendientes = true)
        {
            try
            {
                ViewBag.CurrentSection = "Home";

                string nivel = CurrentUser.Roles.FirstOrDefault().Nivel.ToString();

                _logger.LogInformation($"Ingreso al metodo Index {CurrentUser.UserName}." +
                    $" Nivel de acceso {nivel}");

                //Parametros para busqueda de hojas pendientes
                var parameters = new Dictionary<string, object>
                {
                    { "Nivel", nivel },
                    { "Sector", CurrentUser.Area },
                    { "Usuario", CurrentUser.UserName },
                    { "Id", null },
                    { "Pendientes", 1 }
                };

                if (!pendientes)
                {
                    //Parametros para busqueda de todas las hojas
                    parameters["Pendientes"] = 0;
                }

                var hojas = await _hojaDeRutaService.GetHojas(parameters);
                _logger.LogInformation($"Se obtuvieron {hojas.Count}");

                List<Clientes> clientes = await _clienteService.GetClientes();
                _logger.LogInformation($"Se encontraron {clientes.Count} clientes");

                List<Socios> socios = await _sharedService.GetAllSocios();
                _logger.LogInformation($"Se encontraron {socios.Count} socios");

                _logger.LogInformation($"Mapeo de hojas para mostrar en el Index");
                var allHojas = _mapper.Map<List<HojaViewModel>>(hojas, opt =>
                {
                    opt.Items["Clientes"] = clientes;
                    opt.Items["Socios"] = socios;
                });

                _logger.LogInformation($"Se mapearon {allHojas.Count} hojas");

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

                var pagedList = new PagedList<HojaViewModel>
                {
                    Items = pagedItems,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalItems = totalItems
                };

                _logger.LogInformation($"Se envio al Index el objeto: {System.Text.Json.JsonSerializer.Serialize(pagedList)}");

                ViewBag.CurrentSort = sortOrder;
                ViewBag.SortDirection = sortDirection;

                ViewBag.HojasJson = JsonConvert.SerializeObject(allHojas);

                _logger.LogInformation($"Busqueda de estados para llenar el select");

                ViewBag.Estados = Enum.GetValues(typeof(Estado))
                    .Cast<Estado>()
                    .Select(e => new { Id = (int)e, Desc = e.ToString() })
                    .ToList();

                ViewBag.Pendientes = pendientes;

                _logger.LogInformation($"Fin del metodo Index y retorno a la vista");
                return View(pagedList);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error en Index. Detalle: {ex.Message}");
                return RedirectToAction("Index", "Error", new { message = ex.Message });
            }
        }

        public async Task<IActionResult> Upsert(ViewMode mode, string id, HojaViewModel? hojaViewModel = null)
        {
            try
            {
                _logger.LogInformation($"Ingreso al metodo Upsert. Modo: {mode.ToString()}." +
                    $" IdHoja: {id}");

                Hoja hoja = _mapper.Map<Hoja>(hojaViewModel);

                _logger.LogInformation($"Mapeo de la hoja view model a la entidad hoja");

                if (!String.IsNullOrWhiteSpace(id))
                {
                    hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);

                    if (hoja == null)
                    {
                        throw new Exception($"No se pudo encontrar la hoja {id}");
                    }

                    _logger.LogInformation($"Validar si el revisor {CurrentUser.UserName} esta autorizado a ver la hoja {id}");
                    bool auth = await _revisorService.IsRevisorAuthorized(hoja, CurrentUser.UserName);

                    if (!auth)
                    {
                        _logger.LogError($"El revisor {CurrentUser.UserName} NO esta autorizado a ver la hoja {id}");

                        return RedirectToAction("AccessDenied", "Error", new
                        {
                            message = $"Tu usuario no tiene permiso para visualizar la HDR Nº {hoja.Numero}."
                        });
                    }

                    _logger.LogInformation($"El revisor {CurrentUser.UserName} tiene autorización para ver la hoja {id}");
                }

                ViewBag.CurrentSection = "Upsert";
                ViewBag.Detail = false;

                if (mode == ViewMode.Visualize)
                {
                    _logger.LogInformation($"El usuario {CurrentUser.UserName} ingresó al modo {ViewMode.Visualize.ToString()}");

                    ViewBag.Detail = true;
                    ViewData["Title"] = "Visualizar Hoja de Ruta";

                    hoja.IsSindico = String.IsNullOrWhiteSpace(hoja.Sindico);

                    ModelState.Clear();
                }

                if (mode == ViewMode.Create)
                {
                    _logger.LogInformation($"El usuario {CurrentUser.UserName} ingresó al modo Create");

                    ViewData["Title"] = "Crear Hoja de Ruta";
                    hoja = new Hoja();

                    ModelState.Clear();

                    hoja.Preparo = CurrentUser.UserName;
                    hoja.PreparoFecha = DateTime.Now.ToShortDateString();
                    hoja.FechaDocumento = DateTime.Now;

                    int proximoNumero = await _hojaDeRutaService.GetProximoNumero();
                    hoja.Numero = proximoNumero.ToString();

                    _logger.LogInformation($"El próximo número de hoja de ruta es {proximoNumero}");

                }
                else if (mode == ViewMode.Update)
                {
                    _logger.LogInformation($"El usuario {CurrentUser.UserName} ingresó al modo Update");

                    ViewData["Title"] = "Editar Hoja de Ruta";
                    hoja.IsSindico = !String.IsNullOrWhiteSpace(hoja.Sindico);
                    ModelState.Clear();

                    var isRechazo = hoja.HojaEstados?.Any
                        (e => e.Estado == (int)Estado.Rechazada) ?? false;

                    if (isRechazo)
                    {
                        _logger.LogInformation($"La hoja {id} se encuentra rechazada y se retorna en modo visualización");
                        return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Visualize, id = hoja.Id });
                    }

                    ViewBag.HabilitarBotones = await _hojaDeRutaService.HabilitarBotonFlujo(hoja, CurrentUser.UserName);
                }

                await CargarViewBags(hoja, mode);

                _logger.LogInformation($"Se retorna la hoja {id} en modo Update");
                return View(hoja);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error en Upsert HttpGet. Detalle: {ex.Message}");
                return RedirectToAction("Index", "Error", new { message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Hoja hoja, ViewMode mode)
        {
            try
            {
                _logger.LogInformation($"Ingreso al metodo Upsert. Modo: {mode.ToString()}." +
                   $" IdHoja: {hoja.Id}");

                ViewBag.Detail = false;

                await CargarViewBags(hoja, mode);

                if (mode == ViewMode.Update)
                {
                    _logger.LogInformation($"El usuario {CurrentUser.UserName} ingresó al modo Update");

                    ViewData["Title"] = "Editar Hoja de Ruta";

                    bool isUpdate = await _hojaDeRutaService.UpdateHoja(hoja);
                    string updateLogText = isUpdate ? "Se actualizó correctamente" : "Error al actualizar";
                    _logger.LogInformation($"{updateLogText} la hoja {hoja.Id}");

                    await _hojaDeRutaService.GenerarEstados(hoja, Estado.Pendiente);
                    _logger.LogInformation($"Se generaron correctamente los estados pendientes para la hoja {hoja.Id}");

                    _logger.LogInformation($"Se retorna la hoja {hoja.Id} en modo Update");

                    return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Update, id = hoja.Id });
                }
                else if (mode == ViewMode.Create)
                {
                    _logger.LogInformation($"Ingreso al metodo Upsert. Modo: {mode.ToString()}." +
                  $" IdHoja: {hoja.Id}");

                    if (ModelState.IsValid)
                    {
                        _logger.LogInformation($"Modelo de datos válido");

                        hoja.Id = $"{hoja.Sector}{hoja.Numero}";
                        hoja.Estado = (int)Estado.Pendiente;

                        Revisores revisorActual = await _revisorService.GetRevisorByName(hoja.Manejador);
                        hoja.Manejador = hoja.Preparo;

                        _logger.LogInformation($"Busqueda de revisores para notificar para la hoja {hoja.Id}");
                        var revisoresNotificar = await _revisorService.GetRevisoresParaNotificar(hoja, hoja.Preparo, false);

                        if (revisoresNotificar.Any())
                        {
                            revisorActual = revisoresNotificar.FirstOrDefault();
                            _logger.LogInformation($"{revisorActual.Detalle} es el revisor actual para la hoja {hoja.Id}");

                            hoja.Manejador = revisorActual.Empleado;
                        }

                        _logger.LogInformation($"El manejador actual para la hoja {hoja.Id}" +
                            $" es {hoja.Manejador}");

                        _logger.LogInformation($"Creación de hoja {hoja.Id} por parte del usuario {CurrentUser.UserName}");
                        await _hojaDeRutaService.CreateHoja(hoja);

                        await _hojaDeRutaService.GenerarEstados(hoja, Estado.Pendiente);

                        Clientes cliente = await _clienteService.GetClienteById(hoja.Cliente);

                        EMailBody eMailBody = new EMailBody()
                        {
                            HojaId = hoja.Id,
                            NumeroHoja = hoja.Numero,
                            Sector = hoja.Sector,
                            RutaDoc = hoja.RutaDoc,
                            RutaPapeles = hoja.RutaPapeles,
                            Cliente = cliente.RazonSocial,
                            Revisor = revisorActual
                        };

                        var url = Url.Action(nameof(Upsert), "Home",
                                new { mode = ViewMode.Update, id = eMailBody.HojaId },
                                    protocol: Request.Scheme);

                        await _mailService.NotificarAprobacion(eMailBody, url);

                        if (revisorActual.Area != hoja.Sector)
                        {
                            await _mailService.NotificarAccesoCruzado(hoja, url);
                        }

                        return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Visualize, id = hoja.Id });
                    }
                    else
                    {
                        var errores = ModelState.Where(x => x.Value.Errors.Count > 0)
                                       .SelectMany(x => x.Value.Errors).ToList();

                        _logger.LogError($"Error en el modelo de datos para la creación" +
                            $" de la hoja {hoja.Id}. Detalle: {String.Join('-', errores)}");
                    }

                    return View(hoja);
                }

                return View(hoja);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index", "Error", new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Reportes()
        {
            try
            {
                ViewData["Title"] = "Generar reportes por firmante";

                _logger.LogInformation("Ingreso a la sección de reportes");

                List<Socios> socios = await _sharedService.GetAllSocios();

                _logger.LogInformation($"Se encontraron {socios.Count} para filtrar.");

                ViewBag.Socios = socios.Select(c => new SelectListItem
                {
                    Value = c.Mail,
                    Text = c.Detalle
                }).ToList();

                return View();
            }
            catch (Exception ex)
            {
                string mensaje = $"Error al ingresar a la sección de reportes. {ex.Message}";
                _logger.LogError(mensaje);
                return RedirectToAction("Index", "Error", new { message = mensaje });
            }
            
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reportes(
            [FromForm] string columnasSeleccionadas, [FromForm] string socio,
            [FromForm] DateTime? fechaDesde, [FromForm] DateTime? fechaHasta, bool checkAuditoria)
        {
            try
            {
                _logger.LogInformation("Ingreso al proceso de reportes");

                if (String.IsNullOrWhiteSpace(socio))
                {
                    throw new Exception("El campo socio no puede estar vacio para generar el reporte");
                }

                int auditoria = checkAuditoria ? 1 : 0;

                var hojas = await _hojaDeRutaService.GetHojasForReporte(
                   columnasSeleccionadas, socio,
                   fechaDesde?.ToString("yyyy-MM-dd"),
                   fechaHasta?.ToString("yyyy-MM-dd"),
                   auditoria);

                string titulo = $"Reportes de hojas para el socio {socio}";
                titulo += fechaDesde.HasValue ? $" desde {fechaDesde?.ToString("dd-MM-yyyy")}" : "";
                titulo += fechaHasta.HasValue ? $" hasta {fechaHasta?.ToString("dd-MM-yyyy")}" : $" hasta {DateTime.Now.ToString("dd-MM-yyyy")}";

                var excelBytes = _fileService.GetExcelFromDynamic(hojas, titulo);
                var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                var fileName = $"ReporteHDR_{socio}_{DateTime.Now.ToString("dd-MM-yyyy")}.xlsx";

                return File(excelBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                string mensaje = $"Error al procesar reportes. {ex.Message}";
                _logger.LogError(mensaje);
                return RedirectToAction("Index", "Error", new { message = mensaje });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerColumnasReporte()
        {
            var columnas = typeof(HojaFile).GetProperties()
                .Select(p => new
                {
                    Column = p.GetCustomAttributes(typeof(ColumnAttribute), false)
                                  .Cast<ColumnAttribute>()
                                  .FirstOrDefault()?.Name ?? p.Name,

                    Propiedad = p.Name,

                    Nombre = p.GetCustomAttributes(typeof(DisplayAttribute), false)
                                 .Cast<DisplayAttribute>()
                                 .FirstOrDefault()?.Name ?? p.Name
                }).ToList();

            return Json(columnas);
        }

        public async Task<IActionResult> FirmarDoc(string Id)
        {
            try
            {
                _logger.LogInformation($"Inicio de proceso de firma del doc. para la hoja {Id}."
                    + $" Revisor: {CurrentUser.UserName}.");

                string error = "";

                Hoja hoja = await _hojaDeRutaService.GetHojaByIdAsync(Id);

                if (hoja == null)
                {
                    error = $"No se pudo encontrar la hoja {Id}";
                }

                if (hoja.SocioFirmante != CurrentUser.UserName)
                {
                    _logger.LogError($"Solo puede firmar la hoja el Socio Firmante." +
                        $"Hoja: {Id}. Socio Firmante: {hoja.SocioFirmante}" +
                        $" Revisor: {CurrentUser.UserName}.");

                    error = "Solo puede firmar la hoja el Socio Firmante";
                }

                bool requiereAuditoria = await _sharedService.RequiereAuditoria(hoja.NombreGenerico);
                var cargaAuditorias = await _hojaDeRutaService.GetAuditoriaById(hoja.Id);

                if (requiereAuditoria && cargaAuditorias == null)
                {
                    error = "No se puede firmar la hoja hasta no completarse la pantalla de auditoria";
                    _logger.LogError(error);
                }

                if (!String.IsNullOrWhiteSpace(error))
                {
                    TempData["MensajeModal"] = error;
                    TempData["TipoModal"] = "error";

                    _logger.LogError(error);

                    return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Update, id = Id });
                }

                List<Clientes> clientes = await _clienteService.GetClientes();
                List<Socios> socios = await _sharedService.GetAllSocios();
                List<Revisores> revisores = await _revisorService.GetAllRevisores();

                HojaFile hojaFile = _mapper.Map<HojaFile>(hoja, opt =>
                {
                    opt.Items["Clientes"] = clientes;
                    opt.Items["Socios"] = socios;
                    opt.Items["Revisores"] = revisores;
                });
                var html = _fileService.GetHtmlFromHoja(hojaFile);

                try
                {
                    string fileNamePdf = $"{hojaFile.NombreGenerico}_{hojaFile.Subarea}_{hojaFile.Numero}";
                    await _fileService.SavePDF(html, hojaFile.Sector, fileNamePdf);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar copia en PDF de la hoja {HojaId}", hoja.Id);
                }

                IEnumerable<HojaEstado> estados = await _hojaDeRutaService.GetEstadosByHojaId(hoja.Id);
                HojaEstado estadoFinal = estados.Where(e => e.Etapa == "SocioFirmante").FirstOrDefault();
                estadoFinal.Estado = (int)Estado.Aprobada;
                await _hojaDeRutaService.UpdateEstado(estadoFinal);

                hoja.Estado = (int)Estado.Aprobada;
                await _hojaDeRutaService.UpdateHoja(hoja);

                Revisores gestorFinal = await _revisorService.GetRevisorByName(hoja.GestorFinal);

                EMailBody eMailBody = new EMailBody()
                {
                    HojaId = hoja.Id,
                    NumeroHoja = hoja.Numero,
                    Sector = hoja.Sector,
                    RutaDoc = hoja.RutaDoc,
                    RutaPapeles = hoja.RutaPapeles,
                    Revisor = gestorFinal
                };

                var url = Url.Action(nameof(Upsert), "Home",
                                new { mode = ViewMode.Update, id = eMailBody.HojaId },
                                    protocol: Request.Scheme);

                await _mailService.NotificarFirma(eMailBody, hoja.SocioFirmante, url);

                TempData["MensajeModal"] = "La hoja fue firmada correctamente.";
                TempData["TipoModal"] = "success";

                return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Visualize, id = Id });
            }
            catch (Exception ex)
            {
                string mensaje = $"Error en la firma final para la hoja {Id}." +
                 $" Revisor: {CurrentUser.UserName}. {ex.Message}";

                _logger.LogError(mensaje);

                return RedirectToAction("Index", "Error", new { message = mensaje });
            }
        }

        public async Task<IActionResult> RevisarEtapa(string Id, string accion, string? motivoRechazo)
        {
            _logger.LogInformation($"Revisión de etapa para la hoja {Id}");

            try
            {
                if (accion == "FIRMAR")
                {
                    return RedirectToAction("FirmarDoc", "Home", new { Id = Id });
                }

                _logger.LogInformation($"Validación de revisión de etapa para la hoja {Id}");
                ValidarRevision validarRevision = await ValidarRevisionDeEtapa(Id, accion);
                string error = validarRevision.Error;

                if (!String.IsNullOrWhiteSpace(error))
                {
                    TempData["MensajeModal"] = $"Error al aprobar la hoja: {error}";
                    TempData["TipoModal"] = "error";

                    _logger.LogError(error);

                    return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Update, id = Id });
                }

                Hoja hoja = validarRevision.Hoja;
                HojaEstado estado = validarRevision.Estado;

                Clientes cliente = await _clienteService.GetClienteById(hoja.Cliente);

                List<Revisores> revisores = new List<Revisores>();

                EMailBody eMailBody = new EMailBody()
                {
                    HojaId = hoja.Id,
                    NumeroHoja = hoja.Numero,
                    Sector = hoja.Sector,
                    RutaDoc = hoja.RutaDoc,
                    RutaPapeles = hoja.RutaPapeles,
                    Cliente = cliente.RazonSocial
                };

                switch (accion)
                {
                    case "APROBAR":
                        estado.Estado = (int)Estado.Aprobada;
                        revisores = await _revisorService.GetRevisoresParaNotificar(hoja, hoja.Manejador, false);
                        hoja.Manejador = revisores.FirstOrDefault().Mail;
                        TempData["MensajeModal"] = "La hoja fue aprobada correctamente.";
                        TempData["TipoModal"] = "success";
                        break;
                    case "RECHAZAR":
                        estado.Estado = (int)Estado.Rechazada;
                        estado.MotivoDeRechazo = !String.IsNullOrWhiteSpace(motivoRechazo)
                                                ? motivoRechazo : "Motivo no especificado";
                        revisores = await _revisorService.GetRevisoresParaNotificar(hoja, hoja.Manejador, true);
                        hoja.Estado = (int)Estado.Rechazada;
                        TempData["MensajeModal"] = "La hoja fue rechazada.";
                        TempData["TipoModal"] = "error";
                        break;
                    default:
                        break;
                }

                await _hojaDeRutaService.UpdateEstado(estado);
                await _hojaDeRutaService.UpdateHoja(hoja);

                var url = Url.Action(nameof(Upsert), "Home",
                    new { mode = ViewMode.Update, id = eMailBody.HojaId },
                        protocol: Request.Scheme);

                foreach (var revisor in revisores)
                {
                    eMailBody.Revisor = revisor;

                    switch (accion)
                    {
                        case "APROBAR":
                            await _mailService.NotificarAprobacion(eMailBody, url);
                            if (revisor.Area != hoja.Sector)
                            {
                                await _mailService.NotificarAccesoCruzado(hoja, url);
                            }

                            break;
                        case "RECHAZAR":
                            eMailBody.MotivoDeRechazo = motivoRechazo;
                            await _mailService.NotificarRechazo(eMailBody, CurrentUser.UserName, url);
                            break;
                        default:
                            break;
                    }
                }

                return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Update, id = Id });
            }
            catch (Exception ex)
            {
                string mensaje= $"Error al revisar etapa para la hoja {Id}." +
                    $" Revisor: {CurrentUser.UserName}, Acción: {accion}. {ex.Message}";

                _logger.LogError(mensaje);

                return RedirectToAction("Index", "Error", new { message = mensaje });
            }
        }

        public async Task<ValidarRevision> ValidarRevisionDeEtapa(string Id, string accion)
        {
            ValidarRevision validarRevision = new ValidarRevision();

            try
            {
                string error = "";

                _logger.LogInformation($"Inicio de proceso de revisión de etapa para la hoja {Id}."
                    + $" Revisor: {CurrentUser.UserName}. Acción: {accion}");

                if (String.IsNullOrWhiteSpace(accion))
                {
                    error = $"Error al encontrar la accion de revisión para la hoja {Id}."
                    + $" Revisor: {CurrentUser.UserName}. Acción {accion}";
                }

                Hoja hoja = await _hojaDeRutaService.GetHojaByIdAsync(Id);
                HojaEstado estado = new HojaEstado();

                if (hoja == null)
                {
                    error = $"No se encontró la hoja {Id} para su revisión. Revisor: {CurrentUser.UserName}";
                }
                else
                {
                    var aprobador = CurrentUser.UserName;

                    if (aprobador != hoja.Manejador)
                    {
                        error = $"El aprobador tiene que ser el manejador actual." +
                            $" Aprobador: {aprobador} -  Manejador actual: {hoja.Manejador}";
                    }

                    estado = hoja.HojaEstados.Where(h => h.Revisor == aprobador).FirstOrDefault();

                    if (estado == null)
                    {
                        error = $"No se pudo encontrar el estado actual del revisor" +
                            $"{aprobador} para la hoja {hoja.Id}";
                    }
                }

                validarRevision.Error = error;
                validarRevision.Estado = estado;
                validarRevision.Hoja = hoja;

                return validarRevision;
            }
            catch (Exception ex)
            {
                validarRevision.Error = ex.Message;
                return validarRevision;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerarArchivoHoja(string hojaId, string formato)
        {
            Hoja hoja = await _hojaDeRutaService.GetHojaByIdAsync(hojaId);

            List<Clientes> clientes = await _clienteService.GetClientes();
            List<Socios> socios = await _sharedService.GetAllSocios();
            List<Revisores> revisores = await _revisorService.GetAllRevisores();

            HojaFile hojaFile = _mapper.Map<HojaFile>(hoja, opt =>
            {
                opt.Items["Clientes"] = clientes;
                opt.Items["Socios"] = socios;
                opt.Items["Revisores"] = revisores;
            });

            byte[] bytes;
            string contentType;
            string fileName = $"{hoja.Id}_{hoja.Sector}";

            var html = _fileService.GetHtmlFromHoja(hojaFile);

            switch (formato.ToLower())
            {
                case "pdf":
                    bytes = await _fileService.GetPdfFromHtml(html);
                    contentType = "application/pdf";
                    fileName = $"{fileName}.pdf";
                    break;

                case "word":
                    bytes = _fileService.GetWordFromHoja(hojaFile);
                    contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    fileName = $"{fileName}.docx";
                    break;

                //case "ppt":
                //    bytes = _fileService.GetPptxFromData(hoja);
                //    contentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                //    fileName = $"{fileName}.pptx";
                //    break;

                default:
                    return BadRequest("Formato no soportado");
            }

            Response.Cookies.Append("archivoDescargado", "1", new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddMinutes(1),
                Path = "/"
            });

            return File(bytes, contentType, fileName);
        }   

        public async Task<List<string>> GetContratosByCodigo(string codigoPlataforma)
        {
            List<Contratos> contratos = await _sharedService.GetContratosByCodigoPlataforma(codigoPlataforma);
            List<string> contratosName = contratos.Select(c => c.Contrato).ToList();
            return contratosName;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private async Task CargarViewBags(Hoja hoja, ViewMode viewMode)
        {
            _logger.LogInformation($"Carga de viewbags para la hoja {hoja.Id}");

            List<Clientes> clientes = await _clienteService.GetClientes();
            List<TipoDocumento> tiposDocumento = await _sharedService.GetTipoDocumentos();
            List<Sector> sectores = await _sharedService.GetSectores();
            List<Socios> socios = await _sharedService.GetAllSocios();
            List<SubArea> subAreas = await _sharedService.GetSubAreas();
            List<Revisores> gestores = await _revisorService.GetAllRevisores();
            List<string> monedas = await _hojaDeRutaService.GetMonedas();

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
                //Selected = (viewMode != ViewMode.Create && c.NombreGenerico == hoja.NombreGenerico)
            }).ToList();

            ViewBag.NombreGenericoFull = tiposDocumento;

            ViewBag.Sectores = sectores.Select(c => new SelectListItem
            {
                Value = c.Nombre,
                Text = c.Nombre
            }).ToList();

            ViewBag.Subareas = System.Text.Json.JsonSerializer.Serialize(subAreas);

            ViewBag.Sindicos = socios.Select(c => new SelectListItem
            {
                Value = c.Mail,
                Text = c.Detalle
            }).ToList();

            ViewBag.CampoHabilitado = await _revisorService.GetCampoHabilitado(hoja);

            int nivelActual = await _revisorService.GetNivelRevisorActual(hoja);

            //Si tiene nivel mayor a 0 significa que existe el revisor
            if (nivelActual > 0)
            {
                var parameters = new Dictionary<string, int>
                {
                    { "NivelActual", nivelActual }
                };

                List<Revisores> revisores = await _revisorService.GetRevisoresByNivel(parameters);

                ViewBag.Revisores = revisores.Select(c => new SelectListItem
                {
                    Value = c.Empleado,
                    Text = c.Detalle
                }).ToList();

                string revisoActual = hoja.Reviso;

                SelectListItem reviso = await _revisorService.GetRevisorFromList(hoja.Reviso, revisores);

                if (reviso != null)
                {
                    ViewBag.Revisores.Add(reviso);
                }
            }
            else
            {
                ViewBag.Revisores = new List<SelectListItem>
                {
                    new SelectListItem
                    {
                        Value = "",
                        Text = "No existen revisores para el paso actual"
                    }
                };
            }

            var textInfo = new CultureInfo("es-ES").TextInfo;

            ViewBag.Socios = (from s in socios
                              join r in gestores on s.Mail equals r.Mail into sr
                              from r in sr.DefaultIfEmpty()
                              select new SelectListItem
                              {
                                  Value = s.Mail,
                                  Text = textInfo.ToTitleCase(
                                      $"{s.Detalle.ToLower()} ({r?.Area.ToUpperInvariant() ?? ""})"
                                  )
                              }).ToList();

            ViewBag.Gestores = gestores.Select(c => new SelectListItem
            {
                Value = c.Empleado,
                Text = c.Detalle
            }).ToList();

            ViewBag.RevisoresFull = gestores;

            ViewBag.ViewMode = viewMode;

            ViewBag.Monedas = monedas.Select(m => new SelectListItem
            {
                Value = m,
                Text = m
            }).ToList();

            ViewBag.Estados = Enum.GetValues(typeof(Estado))
                .Cast<Estado>()
                .Select(e => new { Id = (int)e, Desc = e.ToString() })
                .ToList();


            ViewBag.Rechazo = hoja.HojaEstados.Where(e => e.Estado == (int)Estado.Rechazada)
                            .FirstOrDefault()?.MotivoDeRechazo ?? string.Empty;
        }

        public async Task<IActionResult> GetAuditoriaById(string IdHoja)
        {
            try
            {
                Auditoria auditoria = await _hojaDeRutaService.GetAuditoriaById(IdHoja);

                if (auditoria == null)
                {
                    return Json(new { exists = false });
                }

                return Json(new { exists = true, data = auditoria });

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<IActionResult> SaveAuditoria(Auditoria auditoria)
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new
                    {
                        Campo = x.Key,
                        Errores = x.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    });

                return Json(new { success = false, validationErrors = errores });
            }

            Auditoria oldAuditoria = await _hojaDeRutaService.GetAuditoriaById(auditoria.HojaId);

            if (oldAuditoria != null)
            {
                await _hojaDeRutaService.UpdateAuditoria(auditoria);
            }
            else
            {
                await _hojaDeRutaService.CreateAuditoria(auditoria);
            }

            var redirectUrl = Url.Action(nameof(Upsert), new { mode = ViewMode.Update, id = auditoria.HojaId });
            return Json(new { success = true, redirectUrl });
        }

        //public async Task<string> RenderViewToStringAsync(string viewName, object model)
        //{
        //    var actionContext = new ActionContext(HttpContext, RouteData, ControllerContext.ActionDescriptor);

        //    using var sw = new StringWriter();
        //    var viewResult = _viewEngine.FindView(actionContext, viewName, false);
        //    if (viewResult.View == null)
        //        throw new ArgumentNullException($"{viewName} no fue encontrado.");

        //    var viewDictionary = new ViewDataDictionary(
        //        new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
        //        new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
        //    {
        //        Model = model
        //    };

        //    var tempData = new TempDataDictionary(HttpContext, _tempDataProvider);

        //    var viewContext = new ViewContext(
        //        actionContext,
        //        viewResult.View,
        //        viewDictionary,
        //        tempData,
        //        sw,
        //        new HtmlHelperOptions()
        //    );

        //    await viewResult.View.RenderAsync(viewContext);
        //    return sw.ToString();
        //}
    }
}
