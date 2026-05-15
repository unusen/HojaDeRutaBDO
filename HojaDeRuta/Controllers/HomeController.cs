using AutoMapper;
using Microsoft.Extensions.Options;
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
        private readonly INotificationQueueService _notificationQueueService;
        private readonly RevisorService _revisorService;
        private readonly IHojaWorkflowService _hojaWorkflowService;
        private readonly ICatalogCacheService _catalogCacheService;
        private readonly IHojaAttachmentService _hojaAttachmentService;
        private readonly IRutaDocumentoService _rutaDocumentoService;
        private readonly IUserContextCacheService _userContextCacheService;
        private readonly FileService _fileService;
        private readonly IOperationProgressService _operationProgressService;
        private readonly IMapper _mapper;
        private readonly IRazorViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly PathSetings _pathSettings;

        //TODO: PARA TEST LOGIN, ELIMINAR EN PROD
        //private readonly UserContext CurrentUser;

        public HomeController(
            ILogger<HomeController> logger,
            CreatioService creatioService,
            HojaDeRutaService hojaDeRutaService,
            ClienteService clienteService,
            SharedService sharedService,
            IUserContextCacheService userContextCacheService,
            INotificationQueueService notificationQueueService,
            RevisorService revisorService,
            IHojaWorkflowService hojaWorkflowService,
            ICatalogCacheService catalogCacheService,
            IHojaAttachmentService hojaAttachmentService,
            IRutaDocumentoService rutaDocumentoService,
            FileService fileService,
            IOperationProgressService operationProgressService,
            IMapper mapper,
            IRazorViewEngine viewEngine,
            ITempDataProvider tempDataProvider,
            IServiceProvider serviceProvider,
            IOptions<PathSetings> pathSettings
            ) : base(userContextCacheService)
        {
            _logger = logger;
            _creatioService = creatioService;
            _hojaDeRutaService = hojaDeRutaService;
            _clienteService = clienteService;
            _sharedService = sharedService;
            _notificationQueueService = notificationQueueService;
            _revisorService = revisorService;
            _hojaWorkflowService = hojaWorkflowService;
            _catalogCacheService = catalogCacheService;
            _hojaAttachmentService = hojaAttachmentService;
            _rutaDocumentoService = rutaDocumentoService;
            _userContextCacheService = userContextCacheService;
            _fileService = fileService;
            _operationProgressService = operationProgressService;
            _mapper = mapper;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _serviceProvider = serviceProvider;
            _pathSettings = pathSettings.Value;

            //TODO: PARA TEST LOGIN, ELIMINAR EN PROD
            //GroupConfig groupConfig = new GroupConfig
            //{
            //    Name = "HDR_Socio_líder_de_area",
            //    GroupId = "aa52727f-e60f-45bb-b4bf-84a3874c532a",
            //    Nivel = 10
            //};
            //IList<GroupConfig> roles = new List<GroupConfig>
            // {
            //     groupConfig
            // };

            //TODO: PARA TEST LOGIN, ELIMINAR EN PROD
            //CurrentUser = new UserContext
            //{
            //    UserName = "HDR_Testing",
            //    Email = "",
            //    Area = "BANK",
            //    Cargo = "",
            //    Roles = roles
            //};
        }

        private const string ErrorPhasePreflight = "preflight";
        private const string ErrorPhaseExecution = "execution";

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignOut()
        {
            var redirectUrl = Url.Action(nameof(Index), "Home") ?? "/";

            return SignOut(
                new AuthenticationProperties
                {
                    RedirectUri = redirectUrl
                },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10, string sortOrder = "Numero", string sortDirection = "asc", bool pendientes = true)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(UserError))
                {
                    throw new Exception(UserError);
                }

                ConfigurarViewBagIndex(pageSize, pendientes);
                _logger.LogInformation(
                    "Carga de vista Index. User={User} Source={UserContextSource} Pendientes={Pendientes}",
                    CurrentUser.UserName,
                    _userContextCacheService.GetLastSource(),
                    pendientes);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en Index para el usuario {User}", CurrentUser?.UserName ?? "N/A");
                return RedirectToAction("Index", "Error", new { message = "Ocurrió un error inesperado al cargar la lista de hojas de ruta. Por favor, intente nuevamente." });
            }

            //TODO: VER QUE SOLO HAGA LOG CON LOS LOGS ESCRITOS, NO CON TODOS LOS DE SISTEMA
            try
            {
                if (!String.IsNullOrWhiteSpace(UserError))
                {
                    throw new Exception(UserError);
                }

                ConfigurarViewBagIndex(sortOrder, sortDirection, pendientes);

                string nivel = CurrentUser.Roles
                    .OrderByDescending(r => r.Nivel)
                    .FirstOrDefault()
                    ?.Nivel
                    .ToString() ?? "0";

                _logger.LogInformation($"Ingreso al metodo Index {CurrentUser.UserName}");

                _logger.LogInformation($"Datos del usuario:" +
                    $" Nombre: {CurrentUser.UserName}." +
                    $" Empleado: {CurrentUser.Empleado}." +
                    $" Nivel de acceso: {nivel}." +
                    $" Mail: {CurrentUser.Email}." +
                    $" Area: {CurrentUser.Area}." +
                    $" Roles: {CurrentUser.Roles.Count}");

                //Parametros para busqueda de hojas pendientes
                var parameters = new Dictionary<string, object>
                {
                    { "Nivel", nivel },
                    { "Sector", CurrentUser.Area },
                    { "Usuario", CurrentUser.Empleado },
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

                ViewBag.HojasJson = JsonConvert.SerializeObject(allHojas);

                _logger.LogInformation($"Busqueda de estados para llenar el select");

                _logger.LogInformation($"Fin del metodo Index y retorno a la vista");
                return View(pagedList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en el Index para el usuario {User}", CurrentUser?.UserName ?? "N/A");
                return RedirectToAction("Index", "Error", new { message = "Ocurrió un error inesperado al cargar la lista de hojas de ruta. Por favor, intente nuevamente." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetIndexData([FromQuery] HojaIndexQuery query)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                if (!string.IsNullOrWhiteSpace(UserError))
                {
                    throw new Exception(UserError);
                }

                query.PageNumber = Math.Max(1, query.PageNumber);
                query.PageSize = Math.Clamp(query.PageSize, 1, 100);

                var pagedList = await _hojaDeRutaService.GetPagedIndexAsync(query, CurrentUser);
                sw.Stop();

                _logger.LogInformation(
                    "Index data cargado. User={User} DurationMs={DurationMs} TotalItems={TotalItems} PageNumber={PageNumber} PageSize={PageSize} Source={UserContextSource}",
                    CurrentUser.UserName,
                    sw.ElapsedMilliseconds,
                    pagedList.TotalItems,
                    query.PageNumber,
                    query.PageSize,
                    _userContextCacheService.GetLastSource());

                return Json(pagedList);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(
                    ex,
                    "Error al obtener datos del Index. User={User} DurationMs={DurationMs}",
                    CurrentUser?.UserName ?? "N/A",
                    sw.ElapsedMilliseconds);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "No pudimos cargar las hojas de ruta en este momento."
                });
            }
        }

        private void ConfigurarViewBagIndex(int pageSize, bool pendientes)
        {
            ViewBag.CurrentSection = "Home";
            ViewBag.InitialPageSize = pageSize;
            ViewBag.Pendientes = pendientes;
            ViewBag.Estados = Enum.GetValues(typeof(Estado))
                .Cast<Estado>()
                .Select(e => new { Id = (int)e, Desc = e.ToString() })
                .ToList();
        }

        private void ConfigurarViewBagIndex(string sortOrder, string sortDirection, bool pendientes)
        {
            ViewBag.CurrentSection = "Home";
            ViewBag.CurrentSort = sortOrder;
            ViewBag.SortDirection = sortDirection;
            ViewBag.Pendientes = pendientes;
            ViewBag.Estados = Enum.GetValues(typeof(Estado))
                .Cast<Estado>()
                .Select(e => new { Id = (int)e, Desc = e.ToString() })
                .ToList();
        }

        public async Task<IActionResult> Upsert(ViewMode mode, string id, HojaViewModel? hojaViewModel = null)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(UserError))
                {
                    throw new Exception(UserError);
                }

                _logger.LogInformation($"Ingreso al metodo Upsert. Modo: {mode.ToString()}." +
                    $" IdHoja: {id}");

                Hoja hoja = _mapper.Map<Hoja>(hojaViewModel);

                _logger.LogInformation($"Mapeo de la hoja view model a la entidad hoja");

                //string usuarioConectado = CurrentUser.Email.Split('@')[0];

                _logger.LogInformation($"El nombre de usuario conectado es {CurrentUser.Empleado}");

                if (CurrentUser == null)
                {
                    _logger.LogError("Firma - No se pudo obtener el contexto del usuario actual.");
                    return RedirectToAction("Index", "Error", new { message = "No se pudo identificar su sesión de usuario. Por favor, reingrese al sistema." });
                }

                if (mode != ViewMode.Create)
                {
                    hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);

                    if (hoja == null)
                    {
                        _logger.LogWarning($"Firma - Hoja {id} no encontrada.");
                        return RedirectToAction("Index", "Error", new { message = $"No se pudo encontrar la Hoja de Ruta Nº {id}. Es posible que haya sido eliminada o que la URL sea incorrecta." });
                    }

                    _logger.LogInformation($"Validar si el revisor {CurrentUser.UserName} esta autorizado a ver la hoja {id}");

                    bool auth = await _revisorService.IsRevisorAuthorized(hoja, CurrentUser.Empleado);

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

                if (mode != ViewMode.Create &&
                    hoja != null &&
                    hoja.Estado != (int)Estado.Aprobada &&
                    hoja.Estado != (int)Estado.Rechazada)
                {
                    hoja.Manejador = await _hojaWorkflowService.ResolveCurrentHandlerAsync(hoja) ?? hoja.Manejador;
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

                    hoja.Preparo = CurrentUser.Empleado;
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

                    ViewBag.HabilitarBotones = await _hojaWorkflowService.CanActOnCurrentStageAsync(hoja, CurrentUser, null);
                }

                await CargarViewBags(hoja, mode);
                ViewBag.ErrorArchivo = TempData["ErrorArchivo"]?.ToString();
                ConfigurarViewBagAdjunto(hoja);
                await ConfigurarViewBagEtapaActualAsync(hoja);

                // Verificación de adjuntos (Solo si no está aprobada)
                if (false && hoja.Estado != (int)Estado.Aprobada && !string.IsNullOrEmpty(hoja.Adjuntos) && !string.IsNullOrEmpty(hoja.RutaDoc))
                {
                    try
                    {
                        // Si viene un error de la firma (vía TempData), lo priorizamos
                        if (TempData["ErrorArchivo"] != null)
                        {
                            ViewBag.ErrorArchivo = TempData["ErrorArchivo"].ToString();
                        }
                        else
                        {
                            string fullRutaDocNetwork = hoja.RutaDoc;
                            
                            if (hoja.RutaDoc.Length >= 2 && hoja.RutaDoc[1] == ':')
                            {
                                string letraRutaDoc = hoja.RutaDoc.Substring(0, 1);
                                var urlBaseRutaDoc = await _sharedService.GetRutaByLetra(letraRutaDoc);
                                if (urlBaseRutaDoc != null)
                                {
                                    fullRutaDocNetwork = urlBaseRutaDoc.Ruta + hoja.RutaDoc.Substring(2);
                                }
                            }

                            if (!string.IsNullOrEmpty(_pathSettings.LocalOverridePath))
                            {
                                if (hoja.RutaDoc.Length >= 2 && hoja.RutaDoc[1] == ':')
                                {
                                    fullRutaDocNetwork = _pathSettings.LocalOverridePath + hoja.RutaDoc.Substring(2);
                                }
                            }

                            if (Directory.Exists(fullRutaDocNetwork))
                            {
                                var files = Directory.GetFiles(fullRutaDocNetwork).Select(Path.GetFileName).ToList();
                                bool existe = files.Any(f => f.Equals(hoja.Adjuntos, StringComparison.OrdinalIgnoreCase));

                                if (!existe)
                                {
                                    ViewBag.ErrorArchivo = "No encontramos el archivo adjunto en la carpeta indicada. Verificalo antes de continuar con la firma.";
                                }
                                else if (files.Count > 1)
                                {
                                    ViewBag.AdvertenciaArchivo = "Atención: Se detectaron múltiples archivos en la carpeta. Verifique que el documento sea el correcto.";
                                }
                            }
                            else
                            {
                                ViewBag.ErrorArchivo = "No pudimos acceder a la carpeta donde deberia estar el documento adjunto. Revisa la ruta configurada e intenta nuevamente.";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Firma - Error en verificación para la hoja {HojaId}", id ?? "N/A");
                    }
                }

                _logger.LogInformation($"Se retorna la hoja {id} en modo Update");
                return View(hoja);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar Upsert (GET) para la hoja {HojaId} en modo {Mode}", id ?? "N/A", mode);
                return RedirectToAction("Index", "Error", new { message = "No se pudo cargar la información de la hoja de ruta solicitada." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Hoja hoja, ViewMode mode, IFormFile? archivoDoc, string? operationId)
        {
            var executionStarted = false;
            var currentStepKey = string.Empty;
            Hoja? existingHoja = null;

            try
            {
                _logger.LogInformation("Ingreso al metodo Upsert. Modo: {Mode}. IdHoja: {HojaId}", 
                    mode.ToString(), hoja?.Id ?? "N/A");

                ViewBag.Detail = false;

                if (mode == ViewMode.Update && !string.IsNullOrWhiteSpace(hoja?.Id))
                {
                    existingHoja = await _hojaDeRutaService.GetHojaByIdAsync(hoja.Id);
                }

                // Resolución de ruta de red (mapeo de letras)
                hoja.RutaDoc = await _rutaDocumentoService.ResolveNetworkPathAsync(hoja.RutaDoc);

                // Validación de existencia del adjunto en la red
                if (false && !string.IsNullOrEmpty(hoja.Adjuntos))
                {
                    try
                    {
                        if (Directory.Exists(hoja.RutaDoc))
                        {
                            var files = Directory.GetFiles(hoja.RutaDoc).Select(Path.GetFileName).ToList();
                            if (!files.Any(f => f.Equals(hoja.Adjuntos, StringComparison.OrdinalIgnoreCase)))
                            {
                                _logger.LogWarning($"Firma - El archivo vinculado {hoja.Adjuntos} no existe físicamente en {hoja.RutaDoc}");
                                // Aquí podrías optar por mostrar una alerta o permitir guardar igual
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Firma - Error al validar existencia en red: {ex.Message}");
                    }
                }

                // Limpiamos campos temporales
                hoja.ArchivoTemp = existingHoja?.ArchivoTemp ?? hoja.ArchivoTemp;
                hoja.ArchivoHash = existingHoja?.ArchivoHash ?? hoja.ArchivoHash;

                // Blindaje de RutaPapeles
                hoja.RutaPapeles = await _rutaDocumentoService.ResolveNetworkPathAsync(hoja.RutaPapeles);
                if (!string.IsNullOrEmpty(hoja.RutaPapeles) && hoja.RutaPapeles.Length >= 2 && hoja.RutaPapeles[1] == ':')
                {
                    string letraRutaPapeles = hoja.RutaPapeles.Substring(0, 1);
                    var urlBaseRutaPapeles = await _sharedService.GetRutaByLetra(letraRutaPapeles);
                    
                    if (urlBaseRutaPapeles != null)
                    {
                        hoja.RutaPapeles = urlBaseRutaPapeles.Ruta + hoja.RutaPapeles.Substring(2);
                    }
                    else if (!string.IsNullOrEmpty(_pathSettings.LocalOverridePath))
                    {
                        // Soporte para override local en RutaPapeles también
                        hoja.RutaPapeles = _pathSettings.LocalOverridePath + hoja.RutaPapeles.Substring(2);
                    }
                }

                if (mode == ViewMode.Update)
                {
                    if (!ModelState.IsValid)
                    {
                        var erroresModelo = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                        return CrearRespuestaErrorFlujo("Revisa los campos obligatorios antes de continuar.", erroresModelo, ErrorPhasePreflight, operationId);
                    }

                    await _hojaAttachmentService.PreparePrimaryAttachmentAsync(hoja, existingHoja, archivoDoc);

                    var workflowValidation = await _hojaWorkflowService.ValidateWorkflowConfigurationAsync(hoja, hoja.Preparo ?? string.Empty, false);
                    if (!workflowValidation.IsValid)
                    {
                        return CrearRespuestaErrorFlujo("Revisá la configuración de revisores antes de continuar.", workflowValidation.Errors, ErrorPhasePreflight, operationId);
                    }

                    var redirectUrl = Url.Action(nameof(Upsert), "Home", new { mode = ViewMode.Visualize, id = hoja.Id }, protocol: Request.Scheme);
                    executionStarted = !string.IsNullOrWhiteSpace(operationId);

                    if (executionStarted)
                    {
                        await _operationProgressService.StartAsync(operationId!, ObtenerTituloOperacionUpsert(mode), ObtenerPasosOperacionUpsert(mode));
                        currentStepKey = "validando-datos";
                        await _operationProgressService.SetStepRunningAsync(operationId!, currentStepKey, "Validando la información antes de guardar...");
                        await _operationProgressService.SetStepCompletedAsync(operationId!, currentStepKey);
                    }

                    _logger.LogInformation("El usuario {User} está guardando cambios en modo Update para la hoja {HojaId}", CurrentUser.UserName, hoja?.Id ?? "N/A");

                    currentStepKey = "actualizando-hoja";
                    await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Actualizando la hoja...");
                    bool isUpdate = await _hojaDeRutaService.UpdateHoja(hoja);
                    if (!isUpdate)
                    {
                        _logger.LogError("Firma - Error al actualizar la hoja {HojaId} en la base de datos.", hoja?.Id ?? "N/A");
                        await RegistrarFallaOperacionAsync(operationId, currentStepKey, "No pudimos guardar los cambios de la hoja. Intenta nuevamente en unos instantes.");
                        return CrearRespuestaErrorFlujo("No pudimos guardar los cambios de la hoja. Intenta nuevamente en unos instantes.", errorPhase: ErrorPhaseExecution, operationId: operationId);
                    }

                    await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                    currentStepKey = "regenerando-estados";
                    await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Sincronizando los estados del flujo...");
                    await _hojaWorkflowService.SyncWorkflowStatesAsync(hoja);
                    hoja.Manejador = await _hojaWorkflowService.ResolveCurrentHandlerAsync(hoja) ?? string.Empty;
                    await _hojaDeRutaService.UpdateHoja(hoja);
                    await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                    currentStepKey = "finalizando";
                    await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Finalizando la actualización...");
                    await FinalizarOperacionAsync(operationId, "Cambios guardados con éxito.", redirectUrl);
                    return CrearRespuestaExitoFlujo("Cambios guardados con éxito.", hoja?.Id, redirectUrl, operationId);
                }
                else if (mode == ViewMode.Create)
                {
                    if (!ModelState.IsValid)
                    {
                        var erroresModelo = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                        return CrearRespuestaErrorFlujo("Revisa los campos obligatorios antes de continuar.", erroresModelo, ErrorPhasePreflight, operationId);
                    }

                    hoja.Preparo = CurrentUser.Empleado;
                    executionStarted = !string.IsNullOrWhiteSpace(operationId);
                    if (executionStarted)
                    {
                        await _operationProgressService.StartAsync(operationId!, ObtenerTituloOperacionUpsert(mode), ObtenerPasosOperacionUpsert(mode));
                        currentStepKey = "validando-datos";
                        await _operationProgressService.SetStepRunningAsync(operationId!, currentStepKey, "Validando la información ingresada...");
                        await _operationProgressService.SetStepCompletedAsync(operationId!, currentStepKey);
                    }

                    hoja.Id = $"{hoja.Sector}{hoja.Numero}";
                    hoja.Estado = (int)Estado.Pendiente;
                    hoja.Manejador = string.Empty;
                    hoja.ArchivoTemp = null;
                    hoja.ArchivoHash = null;
                    await _hojaAttachmentService.PreparePrimaryAttachmentAsync(hoja, null, archivoDoc);
                    var redirectUrl = Url.Action(nameof(Upsert), "Home", new { mode = ViewMode.Visualize, id = hoja.Id }, protocol: Request.Scheme);

                    currentStepKey = "preparando-responsables";
                    await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Preparando responsables y revisores...");
                    var workflowValidation = await _hojaWorkflowService.ValidateWorkflowConfigurationAsync(hoja, CurrentUser.Empleado, true);
                    if (!workflowValidation.IsValid)
                    {
                        return CrearRespuestaErrorFlujo("Revisá la configuración de revisores antes de continuar.", workflowValidation.Errors, ErrorPhasePreflight, operationId);
                    }

                    await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                    currentStepKey = "creando-hoja";
                    await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Creando la hoja de ruta...");
                    await _hojaDeRutaService.CreateHoja(hoja);
                    await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                    currentStepKey = "generando-estados";
                    await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Generando los estados iniciales...");
                    await _hojaWorkflowService.SyncWorkflowStatesAsync(hoja);
                    hoja.Manejador = await _hojaWorkflowService.ResolveCurrentHandlerAsync(hoja) ?? string.Empty;
                    await _hojaDeRutaService.UpdateHoja(hoja);
                    await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                    currentStepKey = "enviando-notificaciones";
                    await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Programando las notificaciones en segundo plano...");

                    var currentStage = await _hojaWorkflowService.ResolveCurrentStageAsync(hoja);
                    Revisores? revisorActual = null;
                    if (currentStage != null)
                    {
                        revisorActual = await _revisorService.GetRevisorByName(currentStage.ReviewerEmployee);
                    }

                    Clientes cliente = await _clienteService.GetClienteById(hoja.Cliente);

                    EMailBody eMailBody = new EMailBody()
                    {
                        HojaId = hoja.Id,
                        NumeroHoja = hoja.Numero,
                        Sector = hoja.Sector,
                        RutaDoc = hoja.RutaDoc,
                        RutaPapeles = hoja.RutaPapeles,
                        Cliente = cliente?.RazonSocial ?? "Cliente Desconocido",
                        Revisor = revisorActual
                    };

                    var url = $"{Url.Action(nameof(Upsert), "Home", new { mode = ViewMode.Update, id = eMailBody.HojaId }, protocol: Request.Scheme)}";
                    if (revisorActual != null)
                    {
                        await _notificationQueueService.QueueApprovalAsync(
                            eMailBody,
                            url,
                            ObtenerTituloNotificacionEtapa(hoja, revisorActual?.Empleado));

                        if (revisorActual.Area != hoja.Sector)
                        {
                            await _notificationQueueService.QueueCrossAccessAsync(hoja, url);
                        }
                    }

                    await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                    currentStepKey = "finalizando";
                    await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Finalizando la creación...");
                    await FinalizarOperacionAsync(operationId, "Hoja de Ruta creada con éxito.", redirectUrl);
                    return CrearRespuestaExitoFlujo("Hoja de Ruta creada con éxito.", hoja.Id, redirectUrl, operationId);
                }

                return CrearRespuestaErrorFlujo("No pudimos identificar la operacion solicitada. Recarga la pantalla e intenta nuevamente.", errorPhase: ErrorPhasePreflight, operationId: operationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico al intentar guardar/actualizar la hoja {HojaId}", hoja?.Id ?? "N/A");

                const string message = "No pudimos procesar la solicitud. Intenta nuevamente en unos instantes.";
                if (executionStarted)
                {
                    await RegistrarFallaOperacionAsync(operationId, currentStepKey, message, ex.Message);
                    return CrearRespuestaErrorFlujo(message, errorPhase: ErrorPhaseExecution, operationId: operationId);
                }

                return CrearRespuestaErrorFlujo(message, errorPhase: ErrorPhasePreflight, operationId: operationId);
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

                _logger.LogInformation("Se encontraron {Count} socios para filtrar.", socios?.Count ?? 0);

                ViewBag.Socios = (socios ?? new List<Socios>()).Select(c => new SelectListItem
                {
                    Value = c.Mail,
                    Text = c.Detalle
                }).ToList();

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al acceder a la sección de reportes");
                return RedirectToAction("Index", "Error", new { message = "No se pudo cargar la sección de reportes." });
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

                int nivel = CurrentUser.Roles.FirstOrDefault().Nivel;
                string area = CurrentUser.Area;
                string user = CurrentUser.Empleado;

                int auditoria = checkAuditoria ? 1 : 0;

                var hojas = await _hojaDeRutaService.GetHojasForReporte(
                   columnasSeleccionadas, socio,
                   fechaDesde?.ToString("yyyy-MM-dd"),
                   fechaHasta?.ToString("yyyy-MM-dd"),
                   auditoria, nivel, area, user);

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
                _logger.LogError(ex, "Error al procesar/generar reporte para el socio {Socio}", socio);
                return RedirectToAction("Index", "Error", new { message = "Ocurrió un error inesperado al generar el reporte solicitado." });
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

        [HttpPost]
        public async Task<IActionResult> FirmarDoc(string Id, IFormFile? archivoDoc, string? operationId)
        {
            var executionStarted = false;
            var currentStepKey = string.Empty;

            try
            {
                _logger.LogInformation($"Firma - Inicio de proceso en FirmarDoc para la hoja {Id}."
                    + $" Revisor: {CurrentUser.UserName}.");

                if (archivoDoc == null)
                {
                    _logger.LogWarning($"Firma - El parámetro archivoDoc es NULO en FirmarDoc para la hoja {Id}.");
                }
                else
                {
                    _logger.LogInformation($"Firma - Parámetro archivoDoc recibido: {archivoDoc.FileName}, Tamaño: {archivoDoc.Length} bytes.");
                }

                Hoja hoja = await _hojaDeRutaService.GetHojaByIdAsync(Id);
                string error = string.Empty;

                if (hoja == null)
                {
                    error = "No pudimos encontrar la hoja que querés firmar. Recargá la pantalla e intentá nuevamente.";
                }
                else if (hoja.Estado == (int)Estado.Rechazada)
                {
                    error = "La hoja fue rechazada y ya no puede firmarse.";
                }
                else if (hoja.SocioFirmante.ToUpper() != CurrentUser.Empleado.ToUpper())
                {
                    _logger.LogError($"Solo puede firmar la hoja el Socio Firmante." +
                        $"Hoja: {Id}. Socio Firmante: {hoja.SocioFirmante}" +
                        $" Revisor: {CurrentUser.UserName}.");

                    error = "Solo el socio firmante asignado puede completar esta firma.";
                }
                else if (!await _hojaWorkflowService.CanActOnCurrentStageAsync(hoja, CurrentUser, "FIRMAR"))
                {
                    error = "Tu usuario no puede completar la firma en la etapa actual de esta hoja.";
                }

                if (string.IsNullOrWhiteSpace(error))
                {
                    if (archivoDoc != null && archivoDoc.Length > 0)
                    {
                        await _hojaAttachmentService.PreparePrimaryAttachmentAsync(hoja, hoja, archivoDoc);
                    }

                    var cargaAuditorias = await _hojaDeRutaService.GetAuditoriaById(hoja.Id);

                    if (RequiereAuditoria(hoja) && !AuditoriaEstaCompleta(cargaAuditorias))
                    {
                        error = "Antes de firmar, completá toda la información de auditoría requerida.";
                        _logger.LogError(error);
                    }
                }

                if (!String.IsNullOrWhiteSpace(error))
                {
                    _logger.LogError("Firma - Error de validación en FirmarDoc para hoja {HojaId}: {Error}", Id, error);
                    return CrearRespuestaErrorFlujo(error, errorPhase: ErrorPhasePreflight, operationId: operationId);
                }

                executionStarted = !string.IsNullOrWhiteSpace(operationId);
                var redirectUrl = Url.Action(nameof(Upsert), "Home",
                    new { mode = ViewMode.Visualize, id = hoja.Id },
                    protocol: Request.Scheme);

                if (executionStarted)
                {
                    await _operationProgressService.StartAsync(operationId!, ObtenerTituloOperacionRevision("FIRMAR"), ObtenerPasosOperacionRevision("FIRMAR"));

                    currentStepKey = "validando-firma";
                    await _operationProgressService.SetStepRunningAsync(operationId!, currentStepKey, "Validando permisos de firma...");
                    await _operationProgressService.SetStepCompletedAsync(operationId!, currentStepKey);

                    currentStepKey = "verificando-auditoria";
                    await _operationProgressService.SetStepRunningAsync(operationId!, currentStepKey, "Verificando la auditoría asociada...");
                    await _operationProgressService.SetStepCompletedAsync(operationId!, currentStepKey);
                }

                var clientesTask = _catalogCacheService.GetClientesAsync();
                var sociosTask = _catalogCacheService.GetSociosAsync();
                var revisoresTask = _catalogCacheService.GetRevisoresAsync();
                await Task.WhenAll(clientesTask, sociosTask, revisoresTask);

                List<Clientes> clientes = clientesTask.Result;
                List<Socios> socios = sociosTask.Result;
                List<Revisores> revisores = revisoresTask.Result;

                HojaFile hojaFile = _mapper.Map<HojaFile>(hoja, opt =>
                {
                    opt.Items["Clientes"] = clientes;
                    opt.Items["Socios"] = socios;
                    opt.Items["Revisores"] = revisores;
                });

                currentStepKey = "copiando-documento";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Copiando el documento firmado...");

                var finalizeResult = await _hojaAttachmentService.FinalizeSignatureAsync(hoja, hojaFile.Sector);
                if (!finalizeResult.Success)
                {
                    _logger.LogError("Firma - Error en la gestiÃ³n de archivos para la hoja {HojaId}: {Message}", hoja.Id, finalizeResult.Message);
                    await RegistrarFallaOperacionAsync(operationId, currentStepKey, finalizeResult.Message, finalizeResult.Message);
                    return CrearRespuestaErrorFlujo(finalizeResult.Message, errorPhase: ErrorPhaseExecution, operationId: operationId);
                }

                _logger.LogInformation(
                    "Firma - Documento finalizado. Hoja={HojaId} Origen={Origen} Destino={Destino}",
                    hoja.Id,
                    finalizeResult.Origen,
                    finalizeResult.FinalPath);

                if (false)
                {
                    try
                    {
                    string fullRutaDocNetwork = await _rutaDocumentoService.ResolveNetworkPathAsync(hoja.RutaDoc);

                    _logger.LogInformation($"Firma - Proceso de COPIA de archivo iniciado. Hoja: {hoja.Id}");
                    
                    if (string.IsNullOrEmpty(hoja.Adjuntos))
                    {
                        throw new Exception("No encontramos un documento adjunto para firmar. Verificá el archivo cargado antes de continuar.");
                    }

                    string origenFullPath = Path.Combine(fullRutaDocNetwork, hoja.Adjuntos);
                    string pathBase = _pathSettings.PathBase;
                    string destinoFolder = Path.Combine(pathBase, hojaFile.Sector);
                    
                    if (!Directory.Exists(destinoFolder)) Directory.CreateDirectory(destinoFolder);
                    string destinoFullPath = Path.Combine(destinoFolder, hoja.Adjuntos);

                    if (System.IO.File.Exists(origenFullPath))
                    {
                        _logger.LogInformation($"Firma - Copiando archivo desde {origenFullPath} hacia {destinoFullPath}");
                        System.IO.File.Copy(origenFullPath, destinoFullPath, true);
                        _logger.LogInformation("Firma - Copia completada exitosamente.");
                    }
                    else
                    {
                        _logger.LogError($"Firma - El archivo de origen no existe: {origenFullPath}");
                        throw new Exception("No encontramos el archivo adjunto en la carpeta indicada. Verificalo y volvé a intentarlo.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Firma - Error en la gestión de archivos: {Message}", ex.Message);
                    await RegistrarFallaOperacionAsync(operationId, currentStepKey, ex.Message, ex.Message);
                    return CrearRespuestaErrorFlujo(ex.Message, errorPhase: ErrorPhaseExecution, operationId: operationId);
                }
                }

                await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                var stagesToClose = await _hojaWorkflowService.GetStagesToCloseOnSignatureAsync(hoja);
                
                if (!stagesToClose.Any())
                {
                    _logger.LogError($"Firma - No se encontró la etapa SocioFirmante para la hoja {Id}");
                    await RegistrarFallaOperacionAsync(operationId, currentStepKey, "No pudimos completar la firma porque no encontramos la etapa final de aprobación. Recargá la hoja e intentá nuevamente.");
                    return CrearRespuestaErrorFlujo("No pudimos completar la firma porque no encontramos la etapa final de aprobación. Recargá la hoja e intentá nuevamente.", errorPhase: executionStarted ? ErrorPhaseExecution : ErrorPhasePreflight, operationId: operationId);
                }

                Revisores gestorFinal = await _revisorService.GetRevisorByName(hoja.GestorFinal);
                
                if (gestorFinal == null)
                {
                    _logger.LogWarning($"Firma - No se encontró la información del Gestor Final: {hoja.GestorFinal}");
                    await RegistrarFallaOperacionAsync(operationId, currentStepKey, "No pudimos completar la firma porque falta información del gestor final. Revisá la hoja antes de volver a intentarlo.");
                    return CrearRespuestaErrorFlujo("No pudimos completar la firma porque falta información del gestor final. Revisá la hoja antes de volver a intentarlo.", errorPhase: executionStarted ? ErrorPhaseExecution : ErrorPhasePreflight, operationId: operationId);
                }

                currentStepKey = "actualizando-estado-final";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Actualizando el estado final de la hoja...");
                var estadosPorEtapa = (hoja.HojaEstados ?? Enumerable.Empty<HojaEstado>())
                    .Where(e => !string.IsNullOrWhiteSpace(e.Etapa))
                    .ToDictionary(e => e.Etapa!, StringComparer.OrdinalIgnoreCase);

                foreach (var stage in stagesToClose)
                {
                    if (!estadosPorEtapa.TryGetValue(stage.StageKey, out var estadoEtapa))
                    {
                        continue;
                    }

                    estadoEtapa.Estado = (int)Estado.Aprobada;
                    estadoEtapa.MotivoDeRechazo = null;
                    await _hojaDeRutaService.UpdateEstado(estadoEtapa);
                }

                _logger.LogInformation(
                    "Firma - Se cerraron {StageCount} etapas pendientes en la hoja {HojaId}: {Stages}",
                    stagesToClose.Count,
                    hoja.Id,
                    string.Join(", ", stagesToClose.Select(stage => stage.StageKey)));
                await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                currentStepKey = "actualizando-hoja";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Guardando la hoja aprobada...");
                hoja.Estado = (int)Estado.Aprobada;
                await _hojaDeRutaService.UpdateHoja(hoja);
                await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                EMailBody eMailBody = new EMailBody()
                {
                    HojaId = hoja.Id,
                    NumeroHoja = hoja.Numero,
                    Sector = hoja.Sector,
                    RutaDoc = hoja.RutaDoc,
                    RutaPapeles = hoja.RutaPapeles,
                    Revisor = gestorFinal
                };

                currentStepKey = "enviando-notificacion";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Enviando la notificación final...");
                var url = Url.Action(nameof(Upsert), "Home",
                    new { mode = ViewMode.Update, id = eMailBody.HojaId },
                    protocol: Request.Scheme);
                await _notificationQueueService.QueueSignatureAsync(
                    eMailBody,
                    hoja.SocioFirmante,
                    url,
                    "Notificacion de firma al gestor final");
                await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                currentStepKey = "finalizando";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Finalizando la firma...");
                await FinalizarOperacionAsync(operationId, "La hoja fue firmada correctamente.", redirectUrl);
                return CrearRespuestaExitoFlujo("La hoja fue firmada correctamente.", hoja.Id, redirectUrl, operationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la firma final para la hoja {HojaId}. Revisor: {User}", Id, CurrentUser?.UserName);
                const string message = "No pudimos completar la firma en este momento. Intentá nuevamente en unos instantes.";
                if (executionStarted)
                {
                    await RegistrarFallaOperacionAsync(operationId, currentStepKey, message, ex.Message);
                    return CrearRespuestaErrorFlujo(message, errorPhase: ErrorPhaseExecution, operationId: operationId);
                }

                return CrearRespuestaErrorFlujo(message, errorPhase: ErrorPhasePreflight, operationId: operationId);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevisarEtapa(string Id, string accion, string? motivoRechazo, IFormFile archivoDoc, string? operationId)
        {
            _logger.LogInformation($"Revisión de etapa para la hoja {Id}");
            var executionStarted = false;
            var currentStepKey = string.Empty;

            try
            {
                _logger.LogInformation($"Firma - RevisarEtapa llamada para la hoja {Id} con acción {accion}.");

                if (accion == "FIRMAR")
                {
                    if (archivoDoc == null)
                    {
                        _logger.LogWarning($"Firma - archivoDoc es NULO en RevisarEtapa para la hoja {Id} (acción FIRMAR).");
                    }
                    else
                    {
                        _logger.LogInformation($"Firma - archivoDoc recibido en RevisarEtapa: {archivoDoc.FileName}, Tamaño: {archivoDoc.Length}.");
                    }
                    
                    _logger.LogInformation($"Firma - Redirigiendo internamente a FirmarDoc desde RevisarEtapa para la hoja {Id}.");
                    return await FirmarDoc(Id, archivoDoc, operationId);
                }

                _logger.LogInformation($"Validación de revisión de etapa para la hoja {Id}");
                ValidarRevision validarRevision = await ValidarRevisionDeEtapa(Id, accion);
                string error = validarRevision.Error;

                if (!String.IsNullOrWhiteSpace(error))
                {
                    _logger.LogError("Firma - Error en RevisarEtapa para hoja {HojaId}: {Error}", Id, error);
                    return CrearRespuestaErrorFlujo(error, errorPhase: ErrorPhasePreflight, operationId: operationId);
                }

                Hoja hoja = validarRevision.Hoja;
                HojaEstado estado = validarRevision.Estado;

                if (hoja == null || estado == null)
                {
                    _logger.LogError("Firma - Error de integridad: Hoja o Estado nulo en RevisarEtapa para la hoja {HojaId}", Id);
                    return CrearRespuestaErrorFlujo("No pudimos obtener la información necesaria para procesar la hoja. Recargá la pantalla e intentá nuevamente.", errorPhase: ErrorPhasePreflight, operationId: operationId);
                }

                executionStarted = !string.IsNullOrWhiteSpace(operationId);
                var redirectUrl = Url.Action(nameof(Upsert), "Home",
                    new { mode = ViewMode.Visualize, id = Id },
                    protocol: Request.Scheme);

                if (executionStarted)
                {
                    await _operationProgressService.StartAsync(operationId!, ObtenerTituloOperacionRevision(accion), ObtenerPasosOperacionRevision(accion));
                    currentStepKey = "validando-etapa";
                    await _operationProgressService.SetStepRunningAsync(operationId!, currentStepKey, "Validando la etapa actual...");
                    await _operationProgressService.SetStepCompletedAsync(operationId!, currentStepKey);
                }

                Clientes cliente = await _clienteService.GetClienteById(hoja.Cliente);
                string razonSocialCliente = cliente?.RazonSocial ?? "Cliente no encontrado";

                List<Revisores> revisores = new List<Revisores>();

                EMailBody eMailBody = new EMailBody()
                {
                    HojaId = hoja.Id,
                    NumeroHoja = hoja.Numero,
                    Sector = hoja.Sector,
                    RutaDoc = hoja.RutaDoc,
                    RutaPapeles = hoja.RutaPapeles,
                    Cliente = razonSocialCliente
                };

                switch (accion)
                {
                    case "APROBAR":
                        currentStepKey = "actualizando-estado";
                        await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Actualizando el estado de la etapa...");
                        estado.Estado = (int)Estado.Aprobada;
                        hoja.Manejador = hoja.Manejador ?? string.Empty;
                        
                        if (revisores != null && revisores.Any())
                        {
                            hoja.Manejador = revisores.FirstOrDefault()?.Empleado ?? "";
                        }
                        else
                        {
                            _logger.LogWarning($"Firma - No se encontraron revisores para notificar aprobación en la hoja {Id}");
                        }

                        break;
                    case "RECHAZAR":
                        currentStepKey = "registrando-rechazo";
                        await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Registrando el rechazo de la etapa...");
                        estado.Estado = (int)Estado.Rechazada;
                        estado.MotivoDeRechazo = !String.IsNullOrWhiteSpace(motivoRechazo)
                                                ? motivoRechazo : "Motivo no especificado";
                        revisores = await _revisorService.GetRevisoresParaNotificar(hoja, hoja.Manejador, true);
                        hoja.Estado = (int)Estado.Rechazada;
                        break;
                    default:
                        break;
                }

                await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                currentStepKey = "actualizando-hoja";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Guardando los cambios de la hoja...");
                await _hojaDeRutaService.UpdateEstado(estado);

                if (accion == "APROBAR")
                {
                    hoja.Manejador = await _hojaWorkflowService.ResolveCurrentHandlerAsync(hoja) ?? string.Empty;
                }
                else if (accion == "RECHAZAR")
                {
                    hoja.Manejador = string.Empty;
                }

                await _hojaDeRutaService.UpdateHoja(hoja);
                await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                currentStepKey = "enviando-notificaciones";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Programando las notificaciones en segundo plano...");
                var url = Url.Action(nameof(Upsert), "Home",
                    new { mode = ViewMode.Update, id = eMailBody.HojaId },
                        protocol: Request.Scheme);

                revisores = new List<Revisores>();
                if (accion == "APROBAR")
                {
                    var currentStage = await _hojaWorkflowService.ResolveCurrentStageAsync(hoja);
                    if (currentStage != null)
                    {
                        var nextReviewer = await _revisorService.GetRevisorByName(currentStage.ReviewerEmployee);
                        if (nextReviewer != null)
                        {
                            revisores.Add(nextReviewer);
                        }
                    }
                }
                else if (accion == "RECHAZAR")
                {
                    var preparador = await _revisorService.GetRevisorByName(hoja.Preparo);
                    if (preparador != null)
                    {
                        revisores.Add(preparador);
                    }

                    eMailBody.MotivoDeRechazo = motivoRechazo;
                }

                foreach (var revisor in revisores)
                {
                    eMailBody.Revisor = revisor;

                    switch (accion)
                    {
                        case "APROBAR":
                            await _notificationQueueService.QueueApprovalAsync(
                                eMailBody,
                                url,
                                ObtenerTituloNotificacionEtapa(hoja, revisor?.Empleado));
                            if (revisor.Area != hoja.Sector)
                            {
                                await _notificationQueueService.QueueCrossAccessAsync(hoja, url);
                            }

                            break;
                        case "RECHAZAR":
                            eMailBody.MotivoDeRechazo = motivoRechazo;
                            await _notificationQueueService.QueueRejectionAsync(eMailBody, CurrentUser.Empleado, url);
                            break;
                        default:
                            break;
                    }
                }

                await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                currentStepKey = "finalizando";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Finalizando la operación...");
                await FinalizarOperacionAsync(operationId, $"Acción {accion} procesada correctamente.", redirectUrl);
                return CrearRespuestaExitoFlujo($"Acción {accion} procesada correctamente.", Id, redirectUrl, operationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al revisar etapa para la hoja {HojaId}. Revisor: {User}, Acción: {Accion}", Id, CurrentUser?.UserName, accion);
                const string message = "No pudimos procesar la revisión de esta etapa. Intentá nuevamente en unos instantes.";
                if (executionStarted)
                {
                    await RegistrarFallaOperacionAsync(operationId, currentStepKey, message, ex.Message);
                    return CrearRespuestaErrorFlujo(message, errorPhase: ErrorPhaseExecution, operationId: operationId);
                }

                return CrearRespuestaErrorFlujo(message, errorPhase: ErrorPhasePreflight, operationId: operationId);
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
                    error = "No pudimos identificar la acción solicitada. Recargá la pantalla e intentá nuevamente.";
                }

                Hoja hoja = await _hojaDeRutaService.GetHojaByIdAsync(Id);
                HojaEstado estado = new HojaEstado();

                if (hoja == null)
                {
                    error = "La hoja que intentás revisar ya no está disponible o no pudo cargarse.";
                }
                else
                {
                    var currentStage = await _hojaWorkflowService.ResolveCurrentStageAsync(hoja);
                    var identificadoresRevisor = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (currentStage == null)
                    {
                        validarRevision.Error = "La hoja no tiene una etapa activa para procesar en este momento.";
                        validarRevision.Hoja = hoja;
                        return validarRevision;
                    }

                    if (!await _hojaWorkflowService.CanActOnCurrentStageAsync(hoja, CurrentUser, accion))
                    {
                        validarRevision.Error = "Tu usuario no puede actuar sobre la etapa actual de esta hoja.";
                        validarRevision.Hoja = hoja;
                        return validarRevision;
                    }

                    estado = hoja.HojaEstados.FirstOrDefault(h =>
                        string.Equals(h.Etapa, currentStage.StageKey, StringComparison.OrdinalIgnoreCase));

                    if (estado == null)
                    {
                        _logger.LogWarning(
                            "No se encontrÃ³ estado para la etapa activa {StageKey} de la hoja {HojaId}.",
                            currentStage.StageKey,
                            Id);
                        validarRevision.Error = "No pudimos validar el estado actual de la hoja. RecargÃ¡ la pantalla e intentÃ¡ nuevamente.";
                        validarRevision.Hoja = hoja;
                        return validarRevision;
                    }

                    validarRevision.Error = error;
                    validarRevision.Estado = estado;
                    validarRevision.Hoja = hoja;
                    return validarRevision;

                    void AgregarIdentificador(string? valor)
                    {
                        if (!string.IsNullOrWhiteSpace(valor))
                        {
                            identificadoresRevisor.Add(valor.Trim());
                        }
                    }

                    AgregarIdentificador(CurrentUser.Empleado);
                    AgregarIdentificador(CurrentUser.Email);
                    AgregarIdentificador(hoja.Manejador);

                    if (!identificadoresRevisor.Contains((hoja.Manejador ?? string.Empty).Trim()))
                    {
                        error = "La hoja cambió de responsable y ya no puede procesarse desde esta pantalla. Recargala para continuar.";
                    }

                    estado = hoja.HojaEstados.FirstOrDefault(h =>
                        !string.IsNullOrWhiteSpace(h.Revisor) &&
                        identificadoresRevisor.Contains(h.Revisor.Trim()));

                    if (estado == null)
                    {
                        _logger.LogWarning(
                            "No se encontró estado de revisión para la hoja {HojaId}. Usuario actual: {Empleado}. Email: {Email}. Manejador hoja: {Manejador}. Revisores del flujo: {Revisores}",
                            Id,
                            CurrentUser.Empleado,
                            CurrentUser.Email,
                            hoja.Manejador,
                            string.Join(", ", hoja.HojaEstados
                                .Where(h => !string.IsNullOrWhiteSpace(h.Revisor))
                                .Select(h => h.Revisor)));
                        error = "No pudimos validar el estado actual de la hoja. Recargá la pantalla e intentá nuevamente.";
                    }
                }

                validarRevision.Error = error;
                validarRevision.Estado = estado;
                validarRevision.Hoja = hoja;

                return validarRevision;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en ValidarRevisionDeEtapa para la hoja {HojaId}", Id);
                validarRevision.Error = "No pudimos validar el estado actual de la hoja. Intentá nuevamente en unos instantes.";
                return validarRevision;
            }
        }

        [HttpGet]
        public async Task<IActionResult> AbrirAdjuntoPrincipal(string id, bool download = false)
        {
            try
            {
                var hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);
                if (hoja == null)
                {
                    return RedirectToAction("Index", "Error", new { message = "No encontramos la hoja asociada al archivo solicitado." });
                }

                var autorizado = await _revisorService.IsRevisorAuthorized(hoja, CurrentUser.Empleado);
                if (!autorizado)
                {
                    return RedirectToAction("AccessDenied", "Error", new
                    {
                        message = $"Tu usuario no tiene permiso para abrir el adjunto de la HDR NÂº {hoja.Numero}."
                    });
                }

                var openResult = await _hojaAttachmentService.GetOpenResultAsync(hoja);
                if (!openResult.Success || string.IsNullOrWhiteSpace(openResult.PhysicalPath))
                {
                    return RedirectToAction("Index", "Error", new { message = openResult.Message });
                }

                if (download)
                {
                    return PhysicalFile(openResult.PhysicalPath, openResult.ContentType, openResult.FileName, enableRangeProcessing: true);
                }

                Response.Headers["Content-Disposition"] = $"inline; filename=\"{openResult.FileName}\"";
                return PhysicalFile(openResult.PhysicalPath, openResult.ContentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al abrir el adjunto principal de la hoja {HojaId}", id);
                return RedirectToAction("Index", "Error", new { message = "No pudimos abrir el archivo adjunto en este momento." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerarArchivoHoja(string hojaId, string formato)
        {
            Hoja hoja = await _hojaDeRutaService.GetHojaByIdAsync(hojaId);

            var clientesTask = _catalogCacheService.GetClientesAsync();
            var sociosTask = _catalogCacheService.GetSociosAsync();
            var revisoresTask = _catalogCacheService.GetRevisoresAsync();
            await Task.WhenAll(clientesTask, sociosTask, revisoresTask);

            List<Clientes> clientes = clientesTask.Result;
            List<Socios> socios = sociosTask.Result;
            List<Revisores> revisores = revisoresTask.Result;

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
            List<Contratos> contratos = await _catalogCacheService.GetContratosByCodigoPlataformaAsync(codigoPlataforma);
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

            List<Clientes> clientes = await _catalogCacheService.GetClientesAsync();
            List<TipoDocumento> tiposDocumento = await _catalogCacheService.GetTipoDocumentosAsync();
            List<Sector> sectores = await _catalogCacheService.GetSectoresAsync();
            List<Socios> socios = await _catalogCacheService.GetSociosAsync();
            List<SubArea> subAreas = await _catalogCacheService.GetSubAreasAsync();
            List<Revisores> gestores = await _catalogCacheService.GetRevisoresAsync();
            List<string> monedas = await _hojaDeRutaService.GetMonedas();
            List<Jurisdiccion> jurisdicciones = await _catalogCacheService.GetJurisdiccionesAsync();
            List<Contratos> contratosPlataforma = new List<Contratos>();

            var clienteActual = clientes.FirstOrDefault(c => c.Id == hoja.Cliente);
            if (clienteActual != null)
            {
                hoja.CodCliente = clienteActual.CodigoPlataforma;
                contratosPlataforma = await _catalogCacheService.GetContratosByCodigoPlataformaAsync(clienteActual.CodigoPlataforma);
            }
            else if (!string.IsNullOrWhiteSpace(hoja.ContratoPlataforma))
            {
                contratosPlataforma.Add(new Contratos
                {
                    Contrato = hoja.ContratoPlataforma,
                    CodigoPlataforma = string.Empty
                });
            }

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

            ViewBag.ContratosPlataforma = contratosPlataforma
                .Select(c => c.Contrato)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .Select(c => new SelectListItem
                {
                    Value = c,
                    Text = c
                })
                .ToList();

            ViewBag.Jurisdicciones = jurisdicciones.Select(c => new SelectListItem
            {
                Value = c.Name,
                Text = c.Name
            }).ToList();

            ViewBag.CampoHabilitado = await _revisorService.GetCampoHabilitado(hoja);

            List<Revisores> revisoresPermitidos = await _hojaWorkflowService.GetAllowedReviewersForStageAsync(
                hoja,
                nameof(Hoja.Reviso),
                hoja.Reviso);

            ViewBag.Revisores = revisoresPermitidos.Any()
                ? revisoresPermitidos.Select(c => new SelectListItem
                {
                    Value = c.Empleado,
                    Text = c.Detalle
                }).ToList()
                : new List<SelectListItem>
                {
                    new SelectListItem
                    {
                        Value = "",
                        Text = "No existen revisores para el paso actual"
                    }
                };

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

            var auditoria = !string.IsNullOrWhiteSpace(hoja.Id)
                ? await _hojaDeRutaService.GetAuditoriaById(hoja.Id)
                : null;
            var requiereAuditoria = RequiereAuditoria(hoja);
            var auditoriaIncompleta = requiereAuditoria && !AuditoriaEstaCompleta(auditoria);
            var habilitarBotones = ViewBag.HabilitarBotones is bool canAct && canAct;
            var esResponsableActual = string.Equals(hoja.Manejador, CurrentUser?.Empleado, StringComparison.OrdinalIgnoreCase)
                || habilitarBotones;

            ViewBag.AuditoriaRequerida = requiereAuditoria;
            ViewBag.AuditoriaCompleta = requiereAuditoria && !auditoriaIncompleta;
            ViewBag.AuditoriaReadOnly = AuditoriaEsSoloLectura(hoja);
            ViewBag.AdvertenciaAuditoria = requiereAuditoria
                && auditoriaIncompleta
                && !string.IsNullOrWhiteSpace(hoja.Id)
                && esResponsableActual
                ? "Esta hoja requiere completar datos de auditoría antes de la firma final. El avance de etapa puede continuar, pero el socio firmante no podrá firmar hasta que la carga esté completa."
                : null;
        }

        [HttpGet]
        public async Task<IActionResult> GetOperationProgress(string operationId)
        {
            var snapshot = await _operationProgressService.GetAsync(operationId);
            if (snapshot == null)
            {
                return NotFound(new
                {
                    message = "No encontramos información de progreso para la operación solicitada."
                });
            }

            return Json(snapshot);
        }

        [HttpGet]
        public async Task<IActionResult> GetNotificationStatuses(string hojaId)
        {
            var statuses = await _notificationQueueService.GetStatusesAsync(hojaId);
            return Json(statuses);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryNotification(string hojaId, string jobId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hojaId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No se indico la hoja asociada a la notificacion."
                    });
                }

                var hoja = await _hojaDeRutaService.GetHojaByIdAsync(hojaId);
                if (hoja == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "No encontramos la hoja asociada a la notificacion."
                    });
                }

                var autorizado = await _revisorService.IsRevisorAuthorized(hoja, CurrentUser.Empleado);
                if (!autorizado)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        message = "Tu usuario no tiene permisos para reintentar esta notificacion."
                    });
                }

                var snapshot = await _notificationQueueService.RetryAsync(hojaId, jobId);
                return Json(new
                {
                    success = true,
                    message = "Reintentando el envio del email.",
                    status = snapshot
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo reintentar la notificacion {JobId} para la hoja {HojaId}", jobId, hojaId);
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAuditoriaById(string IdHoja)
        {
            try
            {
                Hoja hoja = await ValidarAccesoAuditoriaAsync(IdHoja);
                if (!RequiereAuditoria(hoja))
                {
                    return Json(new { exists = false });
                }

                Auditoria auditoria = await _hojaDeRutaService.GetAuditoriaById(IdHoja);

                if (auditoria == null)
                {
                    return Json(new { exists = false });
                }

                return Json(new { exists = true, data = auditoria });

            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Acceso denegado al obtener auditoría para la hoja {HojaId}", IdHoja);
                return Json(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validación fallida al obtener auditoría para la hoja {HojaId}", IdHoja);
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener auditoría para la hoja {HojaId}", IdHoja);
                return Json(new { success = false, message = "No pudimos recuperar la información de auditoría en este momento." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAuditoria(Auditoria auditoria, string? operationId)
        {
            var executionStarted = false;
            var currentStepKey = string.Empty;

            try
            {
                Hoja hoja = await ValidarAccesoAuditoriaAsync(auditoria?.HojaId);

                if (!RequiereAuditoria(hoja))
                {
                    return CrearRespuestaErrorFlujo(
                        "La hoja indicada no requiere carga de auditoría.",
                        errorPhase: ErrorPhasePreflight,
                        operationId: operationId);
                }

                if (AuditoriaEsSoloLectura(hoja))
                {
                    return CrearRespuestaErrorFlujo(
                        "La auditoría ya no puede modificarse porque la hoja se encuentra firmada.",
                        errorPhase: ErrorPhasePreflight,
                        operationId: operationId);
                }

                if (!ModelState.IsValid)
                {
                    var errores = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new
                        {
                            Campo = x.Key,
                            Errores = x.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                        });

                    return Json(new { success = false, validationErrors = errores, errorPhase = ErrorPhasePreflight, operationId });
                }

                executionStarted = !string.IsNullOrWhiteSpace(operationId);
                var redirectUrl = Url.Action(nameof(Upsert), new { mode = ViewMode.Update, id = auditoria.HojaId });

                if (executionStarted)
                {
                    await _operationProgressService.StartAsync(operationId!, ObtenerTituloOperacionAuditoria(), ObtenerPasosOperacionAuditoria());
                    currentStepKey = "validando-auditoria";
                    await _operationProgressService.SetStepRunningAsync(operationId!, currentStepKey, "Validando la información de auditoría...");
                    await _operationProgressService.SetStepCompletedAsync(operationId!, currentStepKey);
                }

                Auditoria oldAuditoria = await _hojaDeRutaService.GetAuditoriaById(auditoria.HojaId);

                currentStepKey = "guardando-datos";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Guardando los datos de auditoría...");

                if (oldAuditoria != null)
                {
                    await _hojaDeRutaService.UpdateAuditoria(auditoria);
                }
                else
                {
                    await _hojaDeRutaService.CreateAuditoria(auditoria);
                }

                await MarcarPasoCompletadoAsync(operationId, currentStepKey);

                currentStepKey = "finalizando";
                await MarcarPasoEnCursoAsync(operationId, currentStepKey, "Finalizando el guardado de auditoría...");
                await FinalizarOperacionAsync(operationId, "Auditoría guardada correctamente.", redirectUrl);

                return CrearRespuestaExitoFlujo("Auditoría guardada correctamente.", auditoria.HojaId, redirectUrl, operationId);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Acceso denegado al guardar auditoría para la hoja {HojaId}", auditoria?.HojaId);
                return CrearRespuestaErrorFlujo(ex.Message, errorPhase: ErrorPhasePreflight, operationId: operationId);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Validación fallida al guardar auditoría para la hoja {HojaId}", auditoria?.HojaId);
                return CrearRespuestaErrorFlujo(ex.Message, errorPhase: ErrorPhasePreflight, operationId: operationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar la auditoría para la hoja {HojaId}", auditoria?.HojaId);
                const string message = "No pudimos guardar la información de auditoría. Intentá nuevamente en unos instantes.";

                if (executionStarted)
                {
                    await RegistrarFallaOperacionAsync(operationId, currentStepKey, message, ex.Message);
                    return CrearRespuestaErrorFlujo(message, errorPhase: ErrorPhaseExecution, operationId: operationId);
                }

                return CrearRespuestaErrorFlujo(message, errorPhase: ErrorPhasePreflight, operationId: operationId);
            }
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
        [HttpGet]
        public async Task<IActionResult> VerificarArchivoPrincipal(string id)
        {
            try
            {
                var hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);
                if (hoja == null)
                {
                    return Json(new { success = false, message = "TodavÃ­a faltan datos del documento adjunto para poder validarlo.", severity = "error" });
                }

                var validationResult = await _hojaAttachmentService.ValidatePrimaryAttachmentAsync(hoja);
                return Json(new
                {
                    success = validationResult.Success,
                    message = validationResult.Message,
                    severity = validationResult.Severity
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en la verificaciÃ³n del archivo principal para la hoja {HojaId}", id);
                return Json(new { success = false, message = "No pudimos verificar el archivo en este momento. ReintentÃ¡ en unos instantes.", severity = "error" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> VerificarExistenciaArchivo(string id)
        {
            try
            {
                var hoja = await _hojaDeRutaService.GetHojaByIdAsync(id);
                if (hoja == null || string.IsNullOrEmpty(hoja.Adjuntos) || string.IsNullOrEmpty(hoja.RutaDoc))
                {
                    return Json(new { success = false, message = "Todavía faltan datos del documento adjunto para poder validarlo.", severity = "error" });
                }

                var validationResult = await _rutaDocumentoService.ValidateAttachmentAsync(hoja.Id, hoja.RutaDoc, hoja.Adjuntos);
                return Json(new
                {
                    success = validationResult.Success,
                    message = validationResult.Message,
                    severity = validationResult.Severity
                });

                string fullRutaDocNetwork = hoja.RutaDoc;
                
                // Resolver si es unidad mapeada
                if (hoja.RutaDoc.Length >= 2 && hoja.RutaDoc[1] == ':')
                {
                    string letraRutaDoc = hoja.RutaDoc.Substring(0, 1);
                    var urlBaseRutaDoc = await _sharedService.GetRutaByLetra(letraRutaDoc);
                    if (urlBaseRutaDoc != null)
                    {
                        fullRutaDocNetwork = urlBaseRutaDoc.Ruta + hoja.RutaDoc.Substring(2);
                    }
                }

                if (!string.IsNullOrEmpty(_pathSettings.LocalOverridePath))
                {
                    if (hoja.RutaDoc.Length >= 2 && hoja.RutaDoc[1] == ':')
                    {
                        fullRutaDocNetwork = _pathSettings.LocalOverridePath + hoja.RutaDoc.Substring(2);
                    }
                }

                if (!Directory.Exists(fullRutaDocNetwork))
                {
                    return Json(new { success = false, message = "No pudimos acceder a la carpeta del documento adjunto. Revisá la ruta configurada e intentá nuevamente.", severity = "error" });
                }

                var files = Directory.GetFiles(fullRutaDocNetwork).Select(Path.GetFileName).ToList();
                bool existe = files.Any(f => f.Equals(hoja.Adjuntos, StringComparison.OrdinalIgnoreCase));
                    
                if (!existe)
                {
                    // No existe el archivo específico -> Error crítico
                    return Json(new { success = false, message = "No encontramos el archivo adjunto en la carpeta indicada. Verificá el nombre y volvé a intentarlo.", severity = "error" });
                }

                // Si existe, verificamos si hay otros archivos (Warning no crítico)
                if (files.Count > 1)
                {
                    return Json(new { success = true, message = "Archivo encontrado, pero se detectaron múltiples archivos adicionales en la carpeta. Verifique que sea el correcto.", severity = "warning" });
                }

                // Existe y es el único -> Éxito total
                return Json(new { success = true, message = "Archivo encontrado correctamente.", severity = "success" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado en la verificación de archivo para la hoja {HojaId}", id);
                return Json(new { success = false, message = "No pudimos verificar el archivo en este momento. Reintentá en unos instantes.", severity = "error" });
            }
        }

        private static List<OperationProgressStepSnapshot> ObtenerPasosOperacionUpsert(ViewMode mode)
        {
            return mode switch
            {
                ViewMode.Create => CrearPasos(
                    ("validando-datos", "Validando datos"),
                    ("preparando-responsables", "Preparando responsables"),
                    ("creando-hoja", "Creando hoja"),
                    ("generando-estados", "Generando estados"),
                    ("enviando-notificaciones", "Programando notificaciones"),
                    ("finalizando", "Finalizando")),
                ViewMode.Update => CrearPasos(
                    ("validando-datos", "Validando datos"),
                    ("actualizando-hoja", "Actualizando hoja"),
                    ("regenerando-estados", "Regenerando estados"),
                    ("finalizando", "Finalizando")),
                _ => new List<OperationProgressStepSnapshot>()
            };
        }

        private static string ObtenerTituloOperacionUpsert(ViewMode mode)
        {
            return mode == ViewMode.Create ? "Creando hoja" : "Guardando cambios";
        }

        private static List<OperationProgressStepSnapshot> ObtenerPasosOperacionRevision(string accion)
        {
            return accion switch
            {
                "APROBAR" => CrearPasos(
                    ("validando-etapa", "Validando etapa"),
                    ("actualizando-estado", "Actualizando estado"),
                    ("actualizando-hoja", "Actualizando hoja"),
                    ("enviando-notificaciones", "Programando notificaciones"),
                    ("finalizando", "Finalizando")),
                "RECHAZAR" => CrearPasos(
                    ("validando-etapa", "Validando etapa"),
                    ("registrando-rechazo", "Registrando rechazo"),
                    ("actualizando-hoja", "Actualizando hoja"),
                    ("enviando-notificaciones", "Programando notificaciones"),
                    ("finalizando", "Finalizando")),
                "FIRMAR" => CrearPasos(
                    ("validando-firma", "Validando firma"),
                    ("verificando-auditoria", "Verificando auditoría"),
                    ("copiando-documento", "Copiando documento"),
                    ("actualizando-estado-final", "Actualizando estado final"),
                    ("actualizando-hoja", "Actualizando hoja"),
                    ("enviando-notificacion", "Enviando notificación"),
                    ("finalizando", "Finalizando")),
                _ => new List<OperationProgressStepSnapshot>()
            };
        }

        private static string ObtenerTituloOperacionRevision(string accion)
        {
            return accion switch
            {
                "APROBAR" => "Aprobando hoja",
                "RECHAZAR" => "Rechazando hoja",
                "FIRMAR" => "Firmando documento",
                _ => "Procesando hoja"
            };
        }

        private static string ObtenerTituloNotificacionEtapa(Hoja hoja, string? revisor)
        {
            if (hoja == null || string.IsNullOrWhiteSpace(revisor))
            {
                return "Notificacion al Revisor";
            }

            var etapa = new (string? Revisor, string Label)[]
            {
                (hoja.Reviso, "Revisor"),
                (hoja.RevisionGerente, "Gerente/Dir."),
                (hoja.EngagementPartner, "Eng. Partner"),
                (hoja.SocioFirmante, "Socio firmante")
            }
            .FirstOrDefault(item => string.Equals(item.Revisor, revisor, StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(etapa.Label)
                ? "Notificacion al Revisor"
                : $"Notificacion al {etapa.Label}";
        }

        private static List<OperationProgressStepSnapshot> ObtenerPasosOperacionAuditoria()
        {
            return CrearPasos(
                ("validando-auditoria", "Validando auditoría"),
                ("guardando-datos", "Guardando datos"),
                ("finalizando", "Finalizando"));
        }

        private static string ObtenerTituloOperacionAuditoria()
        {
            return "Guardando auditoría";
        }

        private static bool RequiereAuditoria(Hoja? hoja)
        {
            return string.Equals(
                hoja?.NombreGenerico?.Trim(),
                "Informe del auditor",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool AuditoriaEstaCompleta(Auditoria? auditoria)
        {
            if (auditoria == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(auditoria.HojaId)
                || !auditoria.Activo.HasValue
                || !auditoria.Pasivo.HasValue
                || !auditoria.PatrimonioNeto.HasValue
                || !auditoria.Resultado.HasValue
                || !auditoria.TotalIngresos.HasValue
                || !auditoria.TotalOtrosIngresos.HasValue
                || string.IsNullOrWhiteSpace(auditoria.Moneda)
                || string.IsNullOrWhiteSpace(auditoria.TipoNumeracion))
            {
                return false;
            }

            return auditoria.Activo.Value == auditoria.Pasivo.Value + auditoria.PatrimonioNeto.Value;
        }

        private static bool AuditoriaEsSoloLectura(Hoja? hoja)
        {
            return hoja?.Estado == (int)Estado.Aprobada;
        }

        private void ConfigurarViewBagAdjunto(Hoja hoja)
        {
            var primaryAttachment = _hojaAttachmentService.GetPrimaryAttachment(hoja);
            var storageMode = _hojaAttachmentService.ResolveMode(hoja);
            hoja.ArchivosAdjuntos = _hojaAttachmentService.GetAttachments(hoja).ToList();

            ViewBag.PrimaryAttachment = primaryAttachment;
            ViewBag.AttachmentMode = storageMode.ToString();
            ViewBag.AttachmentModeLabel = storageMode == FileStorageMode.SharedFolder
                ? "Carpeta compartida"
                : "Archivo en la app";
            ViewBag.AttachmentCanOpen = primaryAttachment != null && !string.IsNullOrWhiteSpace(hoja.Id);
        }

        private async Task ConfigurarViewBagEtapaActualAsync(Hoja hoja)
        {
            if (hoja == null || hoja.Estado != (int)Estado.Pendiente || CurrentUser == null)
            {
                ViewBag.EtapaActual = null;
                return;
            }

            var currentStage = await _hojaWorkflowService.ResolveCurrentStageAsync(hoja);
            var etapaActual = currentStage?.StageKey;

            if (string.Equals(etapaActual, nameof(Hoja.EngagementPartner), StringComparison.OrdinalIgnoreCase)
                && await _hojaWorkflowService.CanActOnCurrentStageAsync(hoja, CurrentUser, "FIRMAR"))
            {
                etapaActual = nameof(Hoja.SocioFirmante);
            }

            ViewBag.EtapaActual = etapaActual;
        }

        private async Task<Hoja> ValidarAccesoAuditoriaAsync(string? hojaId)
        {
            if (string.IsNullOrWhiteSpace(hojaId))
            {
                throw new InvalidOperationException("No se indicó la hoja asociada a la auditoría.");
            }

            var hoja = await _hojaDeRutaService.GetHojaByIdAsync(hojaId);
            if (hoja == null)
            {
                throw new InvalidOperationException("No encontramos la hoja asociada a la auditoría.");
            }

            var autorizado = await _revisorService.IsRevisorAuthorized(hoja, CurrentUser.Empleado);
            if (!autorizado)
            {
                throw new UnauthorizedAccessException("Tu usuario no tiene permisos para acceder a la auditoría de esta hoja.");
            }

            return hoja;
        }

        private static List<OperationProgressStepSnapshot> CrearPasos(params (string Key, string Label)[] steps)
        {
            return steps.Select(step => new OperationProgressStepSnapshot
            {
                Key = step.Key,
                Label = step.Label
            }).ToList();
        }

        private async Task MarcarPasoEnCursoAsync(string? operationId, string stepKey, string message)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                return;
            }

            await _operationProgressService.SetStepRunningAsync(operationId, stepKey, message);
        }

        private async Task MarcarPasoCompletadoAsync(string? operationId, string stepKey, string? message = null)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                return;
            }

            await _operationProgressService.SetStepCompletedAsync(operationId, stepKey, message: message);
        }

        private async Task FinalizarOperacionAsync(string? operationId, string message, string? redirectUrl)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                return;
            }

            await _operationProgressService.CompleteAsync(operationId, message, redirectUrl);
        }

        private async Task RegistrarFallaOperacionAsync(string? operationId, string? stepKey, string message, string? detail = null)
        {
            if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(stepKey))
            {
                return;
            }

            try
            {
                await _operationProgressService.FailAsync(operationId, stepKey, message, detail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar la falla de la operación {OperationId}", operationId);
            }
        }

        private JsonResult CrearRespuestaExitoFlujo(string message, string? id, string? redirectUrl, string? operationId)
        {
            return Json(new
            {
                success = true,
                message,
                id,
                redirectUrl,
                operationId
            });
        }

        private JsonResult CrearRespuestaErrorFlujo(string message, IEnumerable<string>? errors = null, string errorPhase = ErrorPhasePreflight, string? operationId = null)
        {
            var errorList = errors?
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToArray();

            return Json(new
            {
                success = false,
                message,
                errors = errorList,
                errorPhase,
                operationId
            });
        }
    }
}

