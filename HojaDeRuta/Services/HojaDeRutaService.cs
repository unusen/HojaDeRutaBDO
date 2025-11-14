using HojaDeRuta.DBContext;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using HojaDeRuta.Services.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

namespace HojaDeRuta.Services
{
    public class HojaDeRutaService
    {
        private readonly HojasDbContext _context;
        private readonly IGenericRepository<Hoja> _hojaRepository;
        private readonly IGenericRepository<Auditoria> _auditoriaRepository;
        private readonly IGenericRepository<HojaEstado> _hojaEstadoRepository;
        private readonly DBSettings _dbSettings;
        private readonly MonedasSettings _monedasSettings;


        private static readonly string[] EtapasDeRevision = new[]
        {
            nameof(Hoja.Reviso),
            nameof(Hoja.RevisionGerente),
            nameof(Hoja.EngagementPartner),
            nameof(Hoja.SocioFirmante)
        };

        public HojaDeRutaService(
            HojasDbContext context,
            IGenericRepository<Hoja> hojaRepository,
            IGenericRepository<Auditoria> auditoriaRepository,
            IGenericRepository<HojaEstado> hojaEstadoRepository,
            IOptions<DBSettings> dbSettings,
            IOptions<MonedasSettings> monedasSettings
            )
        {
            _context = context;
            _hojaRepository = hojaRepository;
            _auditoriaRepository = auditoriaRepository;
            _hojaEstadoRepository = hojaEstadoRepository;
            _dbSettings = dbSettings.Value;
            _monedasSettings = monedasSettings.Value;
        }

        public async Task<List<Hoja>> GetHojas(Dictionary<string, object> parameters)
        {
            try
            {
                var spName = _dbSettings.Sp["GetHojasByNivel"].ToString();

                IEnumerable<Hoja> hojas = await _hojaRepository.ExecuteStoredProcedureAsync(spName, parameters);
                return hojas.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
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
                throw new Exception($"Error al buscar la hoja {id}. {ex.Message}");
            }
        }

        public async Task<List<dynamic>> GetHojasForReporte(string? columnasSeleccionadas,
            string? socio, string? fechaDesde, string? fechaHasta, int auditoria)
        {
            var spName = _dbSettings.Sp["GetHojasForReporte"].ToString();

            var parameters = new Dictionary<string, object>
                {
                    { "SocioFirmante", socio },
                    { "FechaDesde ", fechaDesde },
                    { "FechaHasta", fechaHasta },
                    { "ColumnasSeleccionadas", columnasSeleccionadas},
                    { "Auditoria", auditoria}
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
                throw new Exception(ex.Message);
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
                throw new Exception(ex.Message);
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
                throw new Exception(ex.Message);
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
                throw new Exception(ex.Message);
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
                throw new Exception(ex.Message);
            }
        }

        public async Task GenerarEstados(Hoja hoja, Estado estado)
        {
            try
            {
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
                throw new Exception(ex.Message);
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
                throw new Exception(ex.Message);
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
                throw new Exception(ex.Message);
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
                throw new Exception(ex.Message);
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
                throw new Exception(ex.Message);
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
                throw new Exception(ex.Message);
            }
        }

        public async Task<bool> HabilitarBotonFlujo(Hoja hoja, string usuarioActual)
        {
            if (usuarioActual != hoja.Manejador)
            {
                return false;
            }

            var estado = hoja.HojaEstados.
                Where(e => e.HojaId == hoja.Id && e.Revisor == usuarioActual).FirstOrDefault();

            if (estado != null)
            {
                return estado.Estado == (int)Estado.Pendiente;
            }

            return false;
        }

        public async Task<List<HojaPendiente>> GetHojasPendientes()
        {
            try
            {
                var hojasPendientes = await _context.Hoja_Estado
                .Where(h => h.Estado == 0 && h.Revisor != null)
                .GroupBy(h => h.Revisor)
                .Select(g => new HojaPendiente
                {
                    Revisor = g.Key,
                    CantidadRegistros = g.Count(),
                    HojasAsociadas = string.Join(" - ", g.Select(h => h.HojaId))
                })
                .OrderBy(r => r.Revisor)
                .ToListAsync();

                return hojasPendientes;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }



        }
    }
}
