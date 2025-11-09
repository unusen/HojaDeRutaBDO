using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using HojaDeRuta.Services.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using System.Linq.Expressions;

namespace HojaDeRuta.Services
{
    public class HojaDeRutaService
    {
        private readonly IGenericRepository<Hoja> _hojaRepository;
        private readonly IGenericRepository<Auditoria> _auditoriaRepository;
        private readonly IGenericRepository<HojaEstado> _hojaEstadoRepository;
        private readonly DBSettings _dbSettings;
        private readonly RevisorService _revisorService;
        private readonly GroupsSettings _groupsSettings;
        private readonly MonedasSettings _monedasSettings;
        private readonly string _userName;
        private readonly string _userEmail;
        private readonly string _userArea;
        private readonly string _userCargo;
        private readonly IList<GroupConfig> _userRoles;

        private static readonly string[] EtapasDeRevision = new[]
        {
            nameof(Hoja.Reviso),
            nameof(Hoja.RevisionGerente),
            nameof(Hoja.EngagementPartner),
            nameof(Hoja.SocioFirmante)
        };

        public HojaDeRutaService(
            IGenericRepository<Hoja> hojaRepository,
            IGenericRepository<Auditoria> auditoriaRepository,
            IGenericRepository<HojaEstado> hojaEstadoRepository,
            RevisorService revisorService,
            IOptions<GroupsSettings> groupsSettings,
            IOptions<DBSettings> dbSettings,
            IOptions<MonedasSettings> monedasSettings
            )
        {
            _hojaRepository = hojaRepository;
            _auditoriaRepository = auditoriaRepository;
            _hojaEstadoRepository = hojaEstadoRepository;
            _revisorService = revisorService;
            _dbSettings = dbSettings.Value;
            _groupsSettings = groupsSettings.Value;
            _monedasSettings = monedasSettings.Value;

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

        public async Task<List<Hoja>> GetHojas(Dictionary<string, object> parameters)
        {
            try
            {
                //IEnumerable<Hoja> hojas =  await hojaRepository.GetAllAsync();
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
                Expression<Func<Auditoria, Object>> order = a => a.HojaId;

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

        public async Task<List<String>> GetMonedas()
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
  

        public async Task<bool> HabilitarBotonFlujo(Hoja hoja)
        {
            if (_userName != hoja.Manejador)
            {
                return false;
            }

            var estado = hoja.HojaEstados.
                Where(e => e.HojaId == hoja.Id && e.Revisor == _userName).FirstOrDefault();

            if (estado != null)
            {
                return estado.Estado == (int)Estado.Pendiente;
            }

            return false;
        }     
    }
}
