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
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;

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
        private readonly FileService _fileService;
        private readonly IMapper _mapper;
        private readonly string _userName;
        private readonly string _userEmail;
        private readonly string _userArea;
        private readonly string _userCargo;
        private readonly IList<GroupConfig> _userRoles;
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;

        private static readonly string[] EtapasDeRevision = new[]
        {
            nameof(Hoja.Reviso),
            nameof(Hoja.RevisionGerente),
            nameof(Hoja.EngagementPartner),
            nameof(Hoja.SocioFirmante)
        };

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
            _fileService = fileService;
            _mapper = mapper;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;

            _userName = "GACEVEDO"; //await _loginService.GetUserNameAsync();
            _userEmail = "sebastian.katcheroff@gmail.com"; //await _loginService.GetUserEmailAsync();
            _userArea = "ILEG"; //await _loginService.GetUserAreaAsync();
            _userCargo = ""; // await _loginService.GetUserCargoAsync();

            GroupConfig groupConfig = new GroupConfig
            {
                Name = "",
                GroupId = "",
                Nivel = 11
            };
            _userRoles = new List<GroupConfig>();
            _userRoles.Add(groupConfig);
            //_userRoles = await _loginService.GetUserGroupsAsync();
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
            ViewBag.CurrentSection = "Home";
            //var name = "";
            //var email = "";
            //IList<GroupConfig> roles = new List<GroupConfig>();
            //var area = "";
            //var cargo = "";

            //TODO: ACTIVAR DATOS DE LOGIN
            //try
            //{
            //    //name = "SGALAZ"; //await _loginService.GetUserNameAsync();
            //    //email = "sebastian.katcheroff@gmail.com"; //await _loginService.GetUserEmailAsync();

            //    //GroupConfig groupConfig = new GroupConfig
            //    //{
            //    //    Name = "",
            //    //    GroupId = "",
            //    //    Nivel = 10
            //    //};
            //    //roles.Add(groupConfig);
            //    ////roles = await _loginService.GetUserGroupsAsync();

            //    //area = "AUDI"; //await _loginService.GetUserAreaAsync();
            //    //cargo = ""; // await _loginService.GetUserCargoAsync();
            //}
            //catch (Exception)
            //{
            //    return Challenge();
            //}

            string nivel = _userRoles.FirstOrDefault().Nivel.ToString();

            //Parametros para busqueda de hojas pendientes
            var parameters = new Dictionary<string, object>
            {
                { "Nivel", nivel },
                { "Sector", _userArea },
                { "Usuario", _userName },
                { "Id", null },
                { "Pendientes", 1 }
            };

            if (!pendientes)
            {
                //Parametros para busqueda de todas las hojas
                parameters["Pendientes"] = 0;
            }

            var hojas = await _hojaDeRutaService.GetHojas(parameters);

            List<Clientes> clientes = await _clienteService.GetClientes();
            List<Socios> socios = await _sharedService.GetAllSocios();

            var allHojas = _mapper.Map<List<HojaViewModel>>(hojas, opt =>
            {
                opt.Items["Clientes"] = clientes;
                opt.Items["Socios"] = socios;
            });

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

            ViewBag.CurrentSort = sortOrder;
            ViewBag.SortDirection = sortDirection;

            ViewBag.HojasJson = JsonConvert.SerializeObject(allHojas);

            ViewBag.Estados = Enum.GetValues(typeof(Estado))
                .Cast<Estado>()
                .Select(e => new { Id = (int)e, Desc = e.ToString() })
                .ToList();

            ViewBag.Pendientes = pendientes;

            return View(pagedList);
        }

        public async Task<IActionResult> Upsert(ViewMode mode, string id, HojaViewModel? hojaViewModel = null)
        {
            try
            {
                //TODO: GENERAR PROCESO BACKGROUND PARA ACTUALIZAR CONTRATOS A LA NOCHE                

                Hoja hoja = _mapper.Map<Hoja>(hojaViewModel);

                if (!String.IsNullOrWhiteSpace(id))
                {
                    hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);

                    bool auth = await _revisorService.IsRevisorAuthorized(hoja, _userName);

                    if (!auth)
                    {
                        return RedirectToAction("AccessDenied", "Error", new
                        {
                            message = $"Tu usuario no tiene permiso para visualizar la HDR Nº {hoja.Numero}."
                        });
                    }
                }


                ViewBag.CurrentSection = "Upsert";
                ViewBag.Detail = false;

                if (mode == ViewMode.Visualize)
                {
                    ViewBag.Detail = true;

                    ViewData["Title"] = "Visualizar Hoja de Ruta";
                    //hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);

                    hoja.IsSindico = String.IsNullOrWhiteSpace(hoja.Sindico);

                    ModelState.Clear();
                }

                if (mode == ViewMode.Create)
                {
                    ViewData["Title"] = "Crear Hoja de Ruta";
                    hoja = new Hoja();

                    ModelState.Clear();

                    hoja.Preparo = _userName;
                    hoja.PreparoFecha = DateTime.Now.ToShortDateString();
                    hoja.FechaDocumento = DateTime.Now;

                    int proximoNumero = await _hojaDeRutaService.GetProximoNumero();
                    hoja.Numero = proximoNumero.ToString();
                }
                else if (mode == ViewMode.Update)
                {
                    ViewData["Title"] = "Editar Hoja de Ruta";
                    //hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);
                    hoja.IsSindico = !String.IsNullOrWhiteSpace(hoja.Sindico);
                    ModelState.Clear();

                    var isRechazo = hoja.HojaEstados?.Any
                        (e => e.Estado == (int)Estado.Rechazada) ?? false;

                    if (isRechazo)
                    {
                        return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Visualize, id = hoja.Id });
                    }

                    ViewBag.HabilitarBotones = await _hojaDeRutaService.HabilitarBotonFlujo(hoja);
                }

                //var estados = await _hojaDeRutaService.GetEstadosByHojaId(hoja.Id);

                //if (estados.Count() > 0)
                //{
                //    hoja.HojaEstados = estados;
                //}

                await CargarViewBags(hoja, mode);

                return View(hoja);
            }
            catch (Exception ex)
            {
                return RedirectToAction("Index", "Error", new { message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Hoja hoja, ViewMode mode)
        {
            try
            {
                ViewBag.Detail = false;

                await CargarViewBags(hoja, mode);

                if (mode == ViewMode.Update)
                {
                    ViewData["Title"] = "Editar Hoja de Ruta";

                    //Revisores revisorActual = await _revisorService.GetRevisorActual(hoja);

                    //if (revisorActual.Empleado != hoja.Manejador)
                    //{
                    //    hoja.Manejador = revisorActual.Empleado;
                    //}                    

                    bool isUpate = await _hojaDeRutaService.UpdateHoja(hoja);

                    await _hojaDeRutaService.GenerarEstados(hoja, Estado.Pendiente);

                    //await NotificarRevisor(hoja.Id, revisorActual, hoja.Numero);

                    return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Update, id = hoja.Id });
                }
                else if (mode == ViewMode.Create)
                {
                    if (ModelState.IsValid)
                    {
                        hoja.Id = $"{hoja.Sector}{hoja.Numero}";
                        hoja.Estado = (int)Estado.Pendiente;

                        //Revisores revisorActual = await _revisorService.GetRevisorActual(hoja);
                        var rev = await _revisorService.GetRevisoresParaNotificar(hoja, hoja.Preparo, false);
                        Revisores revisorActual = rev.FirstOrDefault();

                        hoja.Manejador = revisorActual.Empleado;

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

                        await NotificarAprobacion(eMailBody);

                        return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Visualize, id = hoja.Id });
                    }
                    else
                    {
                        var errores = ModelState.Where(x => x.Value.Errors.Count > 0)
                                       .SelectMany(x => x.Value.Errors).ToList();
                        //TODO: LOGUEAR ERROR
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

        public async Task<IActionResult> FirmarDoc(string Id)
        {
            try
            {
                _logger.LogInformation($"Inicio de proceso de firma del doc. para la hoja {Id}."
                    + $" Revisor: {_userName}.");

                string error = "";

                Hoja hoja = await _hojaDeRutaService.GetHojaByIdAsync(Id);

                if (hoja == null)
                {
                    _logger.LogError($"No se pudo encontrar la hoja {Id}");
                    throw new Exception($"No se pudo encontrar la hoja {Id}");
                }

                if (hoja.SocioFirmante != _userName)
                {
                    _logger.LogError($"Solo puede firmar la hoja el Socio Firmante." +
                        $"Hoja: {Id}. Socio Firmante: {hoja.SocioFirmante}" +
                        $" Revisor: {_userName}.");

                    error = "Solo puede firmar la hoja el Socio Firmante";
                }


                //GENERAR DOCUMENTOS, RUTAS, ETC REVISAR TODO
                //MODIFICAR ESTADOS DE REVISOR Y DE HOJA

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
                    string fileNamePdf = "";
                    await _fileService.SavePDF(html, fileNamePdf);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar copia en PDF de la hoja {HojaId}", hoja.Id);
                }

                TempData["MensajeModal"] = "La hoja fue firmada correctamente.";
                TempData["TipoModal"] = "success";

                return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Visualize, id = Id });

            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }

        }

        public async Task<IActionResult> RevisarEtapa(string Id, string accion, string? motivoRechazo)
        {
            try
            {
                if (accion == "FIRMAR")
                {
                    return RedirectToAction("FirmarDoc", "Home", new { Id = Id });
                }

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

                foreach (var revisor in revisores)
                {
                    eMailBody.Revisor = revisor;

                    switch (accion)
                    {
                        case "APROBAR":
                            await NotificarAprobacion(eMailBody);
                            break;
                        case "RECHAZAR":
                            eMailBody.MotivoDeRechazo = motivoRechazo;
                            await NotificarRechazo(eMailBody, _userName);
                            break;
                        default:
                            break;

                    }
                }

                return RedirectToAction(nameof(Upsert), new { mode = ViewMode.Update, id = Id });
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al revisar etapa para la hoja {Id}." +
                    $" Revisor: {_userName}, Acción: {accion}. {ex.Message}");
            }
        }

        public async Task<ValidarRevision> ValidarRevisionDeEtapa(string Id, string accion)
        {
            ValidarRevision validarRevision = new ValidarRevision();

            try
            {
                string error = "";

                _logger.LogInformation($"Inicio de proceso de revisión de etapa para la hoja {Id}."
                    + $" Revisor: {_userName}. Acción: {accion}");

                if (String.IsNullOrWhiteSpace(accion))
                {
                    error = $"Error al encontrar la accion de revisión para la hoja {Id}."
                    + $" Revisor: {_userName}. Acción {accion}";
                }

                Hoja hoja = await _hojaDeRutaService.GetHojaByIdAsync(Id);
                HojaEstado estado = new HojaEstado();

                if (hoja == null)
                {
                    error = $"No se encontró la hoja {Id} para su revisión. Revisor: {_userName}";
                }
                else
                {
                    var aprobador = _userName;

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

        public async Task NotificarAprobacion(EMailBody eMailBody)
        {
            try
            {
                eMailBody.Revisor.Mail = "sebastian.katcheroff@gmail.com";

                string subject = $"La hoja de ruta {eMailBody.NumeroHoja}" +
                    $" para el cliente {eMailBody.Cliente} requiere su evaluación";

                var url = Url.Action(nameof(Upsert), "Home",
                        new { mode = ViewMode.Update, id = eMailBody.HojaId },
                            protocol: Request.Scheme);

                string body = await _mailService.GetBodyInformarRevisor(url, eMailBody);

                await _mailService.SendMailAsync(subject, eMailBody.Revisor.Mail, body, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al notificar al revisor" +
                    $" {eMailBody.Revisor.Empleado}. {ex.Message}");
            }
        }

        public async Task NotificarRechazo(EMailBody eMailBody, string rechazador)
        {
            try
            {
                eMailBody.Revisor.Mail = "sebastian.katcheroff@gmail.com";

                string subject = $"La hoja de ruta {eMailBody.NumeroHoja}" +
                    $" para el cliente {eMailBody.Cliente} fue rechazada";

                var url = Url.Action(nameof(Upsert), "Home",
                        new { mode = ViewMode.Update, id = eMailBody.HojaId },
                            protocol: Request.Scheme);

                string body = await _mailService.GetBodyInformarRechazo(url, eMailBody, rechazador);

                await _mailService.SendMailAsync(subject, eMailBody.Revisor.Mail, body, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al notificar al revisor" +
                    $" {eMailBody.Revisor.Empleado}. {ex.Message}");
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

        //public async Task<IActionResult> GetRevisoresByNivel(string revisor)
        //{
        //    List<Revisores> revisores = new List<Revisores>();
        //    Revisores revisorActual = await _revisorService.GetRevisorByName(revisor);

        //    int nivel = revisorActual?.Cargo ?? 0;

        //    if (nivel > 0)
        //    {
        //        var parameters = new Dictionary<string, int>
        //        {
        //            { "NivelActual", nivel }
        //        };

        //        revisores = await _revisorService.GetRevisoresByNivel(parameters);
        //    }

        //    var result = revisores.Select(x => new
        //    {
        //        value = x.Empleado,
        //        text = x.Detalle
        //    });

        //    return Json(result);
        //}     

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

        private async Task CargarViewBags(Hoja hoja, ViewMode viewMode)
        {
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

            ViewBag.Socios = socios.Select(c => new SelectListItem
            {
                Value = c.Mail,
                Text = c.Detalle
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

        public async Task<string> RenderViewToStringAsync(string viewName, object model)
        {
            var actionContext = new ActionContext(HttpContext, RouteData, ControllerContext.ActionDescriptor);

            using var sw = new StringWriter();
            var viewResult = _viewEngine.FindView(actionContext, viewName, false);
            if (viewResult.View == null)
                throw new ArgumentNullException($"{viewName} no fue encontrado.");

            var viewDictionary = new ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
            {
                Model = model
            };

            var tempData = new TempDataDictionary(HttpContext, _tempDataProvider);

            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewDictionary,
                tempData,
                sw,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return sw.ToString();
        }


    }
}
