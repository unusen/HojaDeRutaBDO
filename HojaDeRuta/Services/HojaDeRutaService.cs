using HojaDeRuta.DBContext;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using HojaDeRuta.Models.ViewModels;
using HojaDeRuta.Services.Repository;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Linq.Expressions;

namespace HojaDeRuta.Services
{
    public class HojaDeRutaService
    {
        private static bool _pagedIndexSpEnabled = true;
        private readonly ILogger<HojaDeRutaService> _logger;
        private readonly HojasDbContext _context;
        private readonly IGenericRepository<Hoja> _hojaRepository;
        private readonly IGenericRepository<Auditoria> _auditoriaRepository;
        private readonly IGenericRepository<HojaEstado> _hojaEstadoRepository;
        private readonly DBSettings _dbSettings;
        private readonly MailSettings _mailSettings;
        private readonly MonedasSettings _monedasSettings;
        private readonly IMapper _mapper;
        private readonly ICatalogCacheService _catalogCacheService;

        public HojaDeRutaService(
            ILogger<HojaDeRutaService> logger,
            HojasDbContext context,
            IGenericRepository<Hoja> hojaRepository,
            IGenericRepository<Auditoria> auditoriaRepository,
            IGenericRepository<HojaEstado> hojaEstadoRepository,
            IOptions<DBSettings> dbSettings,
            IOptions<MailSettings> mailSettings,
            IOptions<MonedasSettings> monedasSettings,
            IMapper mapper,
            ICatalogCacheService catalogCacheService
            )
        {
            _logger = logger;
            _context = context;
            _hojaRepository = hojaRepository;
            _auditoriaRepository = auditoriaRepository;
            _hojaEstadoRepository = hojaEstadoRepository;
            _dbSettings = dbSettings.Value;
            _mailSettings = mailSettings.Value;
            _monedasSettings = monedasSettings.Value;
            _mapper = mapper;
            _catalogCacheService = catalogCacheService;
        }

        public async Task<List<Hoja>> GetHojas(Dictionary<string, object> parameters)
        {
            try
            {
                var spName = _dbSettings.Sp["GetHojasByNivel"].ToString();

                IEnumerable<Hoja> hojas = await _hojaRepository.ExecuteStoredProcedureAsync(spName, parameters);
                return hojas
                    .GroupBy(h => h.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recuperar la lista de hojas con los parámetros proporcionados.");
                throw new Exception("No se pudo obtener el listado de hojas de ruta desde la base de datos.", ex);
            }
        }

        public async Task<Hoja> GetHojaByIdAsync(string id)
        {
            try
            {
                var spName = _dbSettings.Sp["GetHojasByNivel"].ToString();

                var parameters = new Dictionary<string, object>
                {
                    { "Nivel", "" },
                    { "Sector", "" },
                    { "Usuario", "" },
                    { "Id", id },
                    { "Pendientes", 0 }
                };

                IEnumerable<Hoja> hojas = await _hojaRepository.ExecuteStoredProcedureAsync(spName, parameters);

                Hoja hoja = hojas.FirstOrDefault();

                IEnumerable<HojaEstado> estados = await GetEstadosByHojaId(hoja.Id);

                if (estados.Count() > 0)
                {
                    hoja.HojaEstados = estados;
                }

                return hoja;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar la hoja con ID {HojaId}", id);
                throw new Exception($"Ocurrió un error al intentar recuperar la información de la Hoja de Ruta Nº {id}.", ex);
            }
        }

        public async Task<PagedList<HojaViewModel>> GetPagedIndexAsync(HojaIndexQuery query, UserContext currentUser)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (_pagedIndexSpEnabled &&
                    _dbSettings.Sp.TryGetValue("GetHojasIndexPaged", out var spName) &&
                    !string.IsNullOrWhiteSpace(spName))
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { "Nivel", currentUser.HighestRole },
                        { "Sector", currentUser.Area },
                        { "Usuario", currentUser.Empleado },
                        { "Pendientes", query.Pendientes ? 1 : 0 },
                        { "Numero", query.Numero },
                        { "Cliente", query.Cliente },
                        { "Estado", query.Estado },
                        { "SectorFiltro", query.Sector },
                        { "Socio", query.Socio },
                        { "FechaDesde", query.FechaDesde?.Date },
                        { "FechaHasta", query.FechaHasta?.Date },
                        { "SortField", NormalizeSortField(query.SortField) },
                        { "SortDirection", NormalizeSortDirection(query.SortDirection) },
                        { "PageNumber", Math.Max(1, query.PageNumber) },
                        { "PageSize", Math.Max(1, query.PageSize) }
                    };

                    var rows = await _hojaRepository.ExecuteStoredProcedureDynamicAsync(spName, parameters);
                    var pagedResult = MapPagedIndexRows(rows);

                    sw.Stop();
                    LogIndexDuration(sw.ElapsedMilliseconds, "sp", pagedResult.TotalItems);
                    return pagedResult;
                }
            }
            catch (Exception ex)
            {
                _pagedIndexSpEnabled = false;
                _logger.LogWarning(ex, "No se pudo usar el SP paginado de Index. Se aplicará fallback en memoria.");
            }

            var fallback = await BuildIndexFallbackAsync(query, currentUser);
            sw.Stop();
            LogIndexDuration(sw.ElapsedMilliseconds, "fallback", fallback.TotalItems);
            return fallback;
        }

        public async Task<List<dynamic>> GetHojasForReporte(string? columnasSeleccionadas,
            string? socio, string? fechaDesde, string? fechaHasta, int auditoria,
            int nivel, string area, string user)
        {
            var spName = _dbSettings.Sp["GetHojasForReporte"].ToString();

            var parameters = new Dictionary<string, object>
                {
                    { "SocioFirmante", socio },
                    { "FechaDesde ", fechaDesde },
                    { "FechaHasta", fechaHasta },
                    { "ColumnasSeleccionadas", columnasSeleccionadas},
                    { "Auditoria", auditoria},
                    { "Nivel", nivel},
                    { "Sector", area},
                    { "Usuario", user}
                };

            var hojas = await _hojaRepository.ExecuteStoredProcedureDynamicAsync(spName, parameters);

            return hojas.ToList();
        }

        public async Task CreateHoja(Hoja hoja)
        {
            try
            {
                await _hojaRepository.AddAsync(hoja);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear la hoja {HojaId}", hoja.Id);
                throw new Exception("No se pudo persistir la nueva Hoja de Ruta en la base de datos.", ex);
            }
        }

        public async Task<bool> UpdateHoja(Hoja hoja)
        {
            try
            {
                return await _hojaRepository.UpdateAsync(hoja);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar la hoja {HojaId}", hoja.Id);
                throw new Exception("No se pudieron guardar los cambios de la Hoja de Ruta.", ex);
            }
        }

        public async Task<IEnumerable<HojaEstado>> GetEstadosByHojaId(string hojaId)
        {
            try
            {
                Expression<Func<HojaEstado, bool>> expression = a => a.HojaId == hojaId;

                return await _hojaEstadoRepository.FindAsync(expression);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estados para la hoja {HojaId}", hojaId);
                throw new Exception("Error al recuperar el historial de estados de la hoja.", ex);
            }
        }

        public async Task CreateEstado(HojaEstado estado)
        {
            try
            {
                await _hojaEstadoRepository.AddAsync(estado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear estado para la hoja {HojaId}", estado.HojaId);
                throw new Exception("No se pudo registrar el nuevo estado en el flujo.", ex);
            }
        }

        public async Task<bool> UpdateEstado(HojaEstado hojaEstado)
        {
            try
            {
                return await _hojaEstadoRepository.UpdateAsync(hojaEstado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar estado {EstadoId} para la hoja {HojaId}", hojaEstado.HojaEstadoId, hojaEstado.HojaId);
                throw new Exception("Error al intentar actualizar el estado del revisor.", ex);
            }
        }

        public async Task GenerarEstados(Hoja hoja, Estado estado)
        {
            try
            {
                var EtapasDeRevision = new[]
                {
                    nameof(Hoja.Reviso),
                    nameof(Hoja.RevisionGerente),
                    nameof(Hoja.EngagementPartner),
                    nameof(Hoja.SocioFirmante)
                };

                var hojaType = hoja.GetType();

                foreach (var nombreCampo in EtapasDeRevision)
                {
                    var propInfo = hojaType.GetProperty(nombreCampo);
                    var valorRevisor = propInfo?.GetValue(hoja) as string;

                    if (!string.IsNullOrEmpty(valorRevisor))
                    {
                        var estadoExistente = hoja.HojaEstados.FirstOrDefault
                            (he => he.Etapa.Equals(nombreCampo, StringComparison.OrdinalIgnoreCase));

                        if (estadoExistente == null)
                        {
                            HojaEstado hojaEstado = new HojaEstado
                            {
                                HojaId = hoja.Id,
                                Estado = (int)estado,
                                Etapa = nombreCampo,
                                Revisor = valorRevisor
                            };

                            await CreateEstado(hojaEstado);
                        }
                        else
                        {
                            var estadoFirmante = hoja.HojaEstados.Where(h => h.Etapa == "SocioFirmante").FirstOrDefault();

                            if (estadoFirmante.Revisor != hoja.SocioFirmante)
                            {
                                estadoFirmante.Revisor = hoja.SocioFirmante;
                                await UpdateEstado(estadoFirmante);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar estados automáticos para la hoja {HojaId}", hoja.Id);
                throw new Exception("Error interno en la generación automática del flujo de revisión.", ex);
            }
        }


        public async Task<int> GetProximoNumero()
        {
            try
            {
                var maxValue = await _hojaRepository.GetMaxValueAsync(h => h.Numero);

                if (string.IsNullOrWhiteSpace(maxValue) || !int.TryParse(maxValue, out int tempValue))
                {
                    throw new Exception("No se pudo encontrar el último número de Hoja de Ruta");
                }

                return Convert.ToInt32(maxValue) + 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular el próximo número de Hoja de Ruta");
                throw new Exception("No se pudo determinar el siguiente número correlativo para la nueva hoja.", ex);
            }
        }

        public async Task<int> ReservarProximoNumeroAsync()
        {
            try
            {
                if (!_dbSettings.Sp.TryGetValue("GetNextHojaNumero", out var spName) || string.IsNullOrWhiteSpace(spName))
                {
                    throw new InvalidOperationException("No se encontro configurado DBSettings:Sp:GetNextHojaNumero.");
                }

                var nextNumber = await _hojaRepository.ExecuteStoredProcedureWithReturnValueAsync(spName, new Dictionary<string, object>());
                if (nextNumber <= 0)
                {
                    throw new InvalidOperationException("El SP de numeracion atomica devolvio un valor invalido.");
                }

                return nextNumber;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reservar el próximo número de Hoja de Ruta");
                throw new Exception("No se pudo reservar un número correlativo para la nueva hoja.", ex);
            }
        }

        public async Task<Auditoria> GetAuditoriaById(string IdHoja)
        {
            try
            {
                Expression<Func<Auditoria, bool>> entityName = a => a.HojaId == IdHoja;
                Expression<Func<Auditoria, object>> order = a => a.HojaId;

                return await _auditoriaRepository.GetFirstOrLastAsync(entityName, order, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener auditoría para la hoja {HojaId}", IdHoja);
                throw new Exception("No se pudo recuperar la información de auditoría asociada.", ex);
            }
        }

        public async Task CreateAuditoria(Auditoria auditoria)
        {
            try
            {
                await _auditoriaRepository.AddAsync(auditoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear registro de auditoría para la hoja {HojaId}", auditoria.HojaId);
                throw new Exception("Error al guardar los datos de auditoría.", ex);
            }
        }

        public async Task<bool> UpdateAuditoria(Auditoria auditoria)
        {
            try
            {
                return await _auditoriaRepository.UpdateAsync(auditoria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar auditoría para la hoja {HojaId}", auditoria.HojaId);
                throw new Exception("Error al actualizar los datos de auditoría.", ex);
            }
        }

        public async Task<List<string>> GetMonedas()
        {
            try
            {
                return _monedasSettings;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recuperar la lista de monedas desde la configuración.");
                throw new Exception("Error al cargar las monedas disponibles.", ex);
            }
        }

        public async Task<bool> HabilitarBotonFlujo(Hoja hoja, string usuarioActual)
        {
            _logger.LogInformation($"Validar la habilitación de botones de la hoja {hoja.Id} para el usuario {usuarioActual} ");

            if (usuarioActual.ToUpper() != hoja.Manejador.ToUpper())
            {
                _logger.LogError($"El usuario {usuarioActual} no puede ser diferente" +
                    $" al manejador {hoja.Manejador} de la hoja {hoja.Id}");

                return false;
            }

            _logger.LogInformation($"Validar los estados de la hoja {hoja.Id}");

            var estado = hoja.HojaEstados.
                Where(e => e.HojaId == hoja.Id && 
                e.Revisor.ToUpper() == usuarioActual.ToUpper()).FirstOrDefault();

            if (estado != null)
            {
                _logger.LogInformation($"El usuario {usuarioActual} es revisor válido" +
                    $" de la hoja {hoja.Id}");

                return estado.Estado == (int)Estado.Pendiente;
            }

            _logger.LogError($"El usuario {usuarioActual} no posee estados vinculados" +
                $" a la hoja {hoja.Id}");

            return false;
        }

        private async Task<PagedList<HojaViewModel>> BuildIndexFallbackAsync(HojaIndexQuery query, UserContext currentUser)
        {
            var parameters = new Dictionary<string, object>
            {
                { "Nivel", currentUser.HighestRole.ToString() },
                { "Sector", currentUser.Area },
                { "Usuario", currentUser.Empleado },
                { "Id", null! },
                { "Pendientes", query.Pendientes ? 1 : 0 }
            };

            var hojas = await GetHojas(parameters);
            var clientes = await _catalogCacheService.GetClientesAsync();
            var socios = await _catalogCacheService.GetSociosAsync();

            var mapped = _mapper.Map<List<HojaViewModel>>(hojas, opt =>
            {
                opt.Items["Clientes"] = clientes;
                opt.Items["Socios"] = socios;
            });

            mapped = mapped
                .GroupBy(h => h.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var filtered = ApplyIndexFilters(mapped, query);
            var sorted = ApplyIndexSort(filtered, query);
            var safePageNumber = Math.Max(1, query.PageNumber);
            var safePageSize = Math.Max(1, query.PageSize);

            return new PagedList<HojaViewModel>
            {
                Items = sorted.Skip((safePageNumber - 1) * safePageSize).Take(safePageSize).ToList(),
                PageNumber = safePageNumber,
                PageSize = safePageSize,
                TotalItems = sorted.Count
            };
        }

        private static List<HojaViewModel> ApplyIndexFilters(IEnumerable<HojaViewModel> hojas, HojaIndexQuery query)
        {
            return hojas.Where(h =>
                Matches(h.Numero, query.Numero) &&
                Matches(h.ClienteName, query.Cliente) &&
                (!query.Estado.HasValue || (int?)h.Estado == query.Estado) &&
                Matches(h.Sector, query.Sector) &&
                Matches(h.SocioFirmanteDetalle, query.Socio) &&
                (!query.FechaDesde.HasValue || (h.FechaDocumento?.Date ?? DateTime.MinValue) >= query.FechaDesde.Value.Date) &&
                (!query.FechaHasta.HasValue || (h.FechaDocumento?.Date ?? DateTime.MinValue) <= query.FechaHasta.Value.Date))
                .ToList();
        }

        private static List<HojaViewModel> ApplyIndexSort(IEnumerable<HojaViewModel> hojas, HojaIndexQuery query)
        {
            var sortField = NormalizeSortField(query.SortField);
            var descending = NormalizeSortDirection(query.SortDirection) == "desc";

            Func<HojaViewModel, object?> selector = sortField switch
            {
                "ClienteName" => hoja => hoja.ClienteName,
                "NombreGenerico" => hoja => hoja.NombreGenerico,
                "Sector" => hoja => hoja.Sector,
                "SocioFirmanteDetalle" => hoja => hoja.SocioFirmanteDetalle,
                "FechaDocumento" => hoja => hoja.FechaDocumento,
                "Estado" => hoja => hoja.Estado,
                _ => hoja => hoja.Numero
            };

            return descending
                ? hojas.OrderByDescending(selector).ToList()
                : hojas.OrderBy(selector).ToList();
        }

        private static bool Matches(string? source, string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return (source ?? string.Empty).Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSortField(string? sortField)
        {
            return sortField switch
            {
                "Cliente" => "ClienteName",
                "ClienteName" => "ClienteName",
                "NombreGenerico" => "NombreGenerico",
                "Sector" => "Sector",
                "SocioFirmante" => "SocioFirmanteDetalle",
                "SocioFirmanteDetalle" => "SocioFirmanteDetalle",
                "Fecha" => "FechaDocumento",
                "FechaDocumento" => "FechaDocumento",
                "Estado" => "Estado",
                _ => "Numero"
            };
        }

        private static string NormalizeSortDirection(string? sortDirection)
        {
            return string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";
        }

        private static PagedList<HojaViewModel> MapPagedIndexRows(IEnumerable<dynamic> rows)
        {
            var items = new List<HojaViewModel>();
            var totalItems = 0;
            var pageNumber = 1;
            var pageSize = 0;

            foreach (var row in rows)
            {
                if (row is not IDictionary<string, object> values)
                {
                    continue;
                }

                var map = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
                totalItems = totalItems == 0 ? GetValue<int>(map, "TotalItems") : totalItems;
                pageNumber = GetValue<int?>(map, "PageNumber") ?? pageNumber;
                pageSize = GetValue<int?>(map, "PageSize") ?? pageSize;

                items.Add(new HojaViewModel
                {
                    Id = GetValue<string>(map, "Id"),
                    Cliente = GetValue<int>(map, "Cliente"),
                    ClienteName = GetValue<string>(map, "ClienteName"),
                    Sector = GetValue<string>(map, "Sector") ?? string.Empty,
                    Subarea = GetValue<string>(map, "Subarea") ?? string.Empty,
                    Numero = GetValue<string>(map, "Numero") ?? string.Empty,
                    NombreGenerico = GetValue<string>(map, "NombreGenerico") ?? string.Empty,
                    Descripcion = GetValue<string>(map, "Descripcion") ?? string.Empty,
                    FechaDocumento = GetValue<DateTime?>(map, "FechaDocumento"),
                    SocioFirmante = GetValue<string>(map, "SocioFirmante") ?? string.Empty,
                    SocioFirmanteDetalle = GetValue<string>(map, "SocioFirmanteDetalle") ?? string.Empty,
                    Sindico = GetValue<string>(map, "Sindico"),
                    ContratoPlataforma = GetValue<string>(map, "ContratoPlataforma") ?? string.Empty,
                    Preparo = GetValue<string>(map, "Preparo"),
                    Reviso = GetValue<string>(map, "Reviso") ?? string.Empty,
                    RevisionGerente = GetValue<string>(map, "RevisionGerente"),
                    EngagementPartner = GetValue<string>(map, "EngagementPartner"),
                    GestorFinal = GetValue<string>(map, "GestorFinal"),
                    Manejador = GetValue<string>(map, "Manejador"),
                    LugarFirma = GetValue<string>(map, "LugarFirma") ?? string.Empty,
                    RutaDoc = GetValue<string>(map, "RutaDoc") ?? string.Empty,
                    RutaPapeles = GetValue<string>(map, "RutaPapeles") ?? string.Empty,
                    Adjuntos = GetValue<string>(map, "Adjuntos") ?? string.Empty,
                    Observaciones = GetValue<string>(map, "Observaciones"),
                    Estado = ParseEstado(map)
                });
            }

            var distinctItems = items
                .GroupBy(item => item.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            var duplicateCount = items.Count - distinctItems.Count;

            return new PagedList<HojaViewModel>
            {
                Items = distinctItems,
                PageNumber = pageNumber,
                PageSize = pageSize == 0 ? distinctItems.Count : pageSize,
                TotalItems = duplicateCount > 0 && totalItems >= duplicateCount
                    ? totalItems - duplicateCount
                    : (totalItems == 0 ? distinctItems.Count : totalItems)
            };
        }

        private static T? GetValue<T>(IDictionary<string, object?> values, string key)
        {
            if (!values.TryGetValue(key, out var value) || value == null || value == DBNull.Value)
            {
                return default;
            }

            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return (T?)Convert.ChangeType(value, targetType);
        }

        private static Estado? ParseEstado(IDictionary<string, object?> values)
        {
            var estado = GetValue<int?>(values, "Estado");
            return estado.HasValue ? (Estado)estado.Value : null;
        }

        private void LogIndexDuration(long durationMs, string source, int totalItems)
        {
            if (durationMs > 500)
            {
                _logger.LogWarning(
                    "Carga de index lenta. Source={Source} DurationMs={DurationMs} TotalItems={TotalItems}",
                    source,
                    durationMs,
                    totalItems);
                return;
            }

            _logger.LogInformation(
                "Carga de index completada. Source={Source} DurationMs={DurationMs} TotalItems={TotalItems}",
                source,
                durationMs,
                totalItems);
        }

        public async Task<List<HojaPendiente>> GetHojasPendientes()
        {
            try
            {
                var fechaMinimaConfigurada = DateTime.TryParseExact(
                    _mailSettings.EnviarPendientesDesde,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var fechaMinima)
                    ? fechaMinima.Date
                    : (DateTime?)null;

                if (!string.IsNullOrWhiteSpace(_mailSettings.EnviarPendientesDesde) && !fechaMinimaConfigurada.HasValue)
                {
                    _logger.LogWarning(
                        "MailSettings:EnviarPendientesDesde tiene un formato invalido: {ConfigValue}. Se omitira el filtro por fecha.",
                        _mailSettings.EnviarPendientesDesde);
                }

                var query = _context.Hoja_Estado
                    .Join(
                        _context.Hojas,
                        estado => estado.HojaId,
                        hoja => hoja.Id,
                        (estado, hoja) => new { EstadoEtapa = estado, Hoja = hoja })
                    .Where(x =>
                        x.EstadoEtapa.Estado == (int)Estado.Pendiente &&
                        x.EstadoEtapa.Revisor != null &&
                        x.Hoja.Estado != (int)Estado.Rechazada);

                if (fechaMinimaConfigurada.HasValue)
                {
                    var fechaCorte = fechaMinimaConfigurada.Value;
                    query = query.Where(x =>
                        x.Hoja.FechaDocumento.HasValue &&
                        x.Hoja.FechaDocumento.Value >= fechaCorte);
                }

                var hojasPendientes = await query
                    .GroupBy(x => x.EstadoEtapa.Revisor)
                    .Select(g => new HojaPendiente
                    {
                        Revisor = g.Key,
                        CantidadRegistros = g.Count(),
                        HojasAsociadas = string.Join(" - ", g.Select(x => x.EstadoEtapa.HojaId))
                    })
                    .OrderBy(r => r.Revisor)
                    .ToListAsync();

                return hojasPendientes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el listado consolidado de hojas pendientes.");
                throw new Exception("Error al recuperar las notificaciones pendientes.", ex);
            }



        }
    }
}
