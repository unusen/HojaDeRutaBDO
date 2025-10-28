using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services.Repository;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

namespace HojaDeRuta.Services
{
    public class HojaDeRutaService
    {
        private readonly IGenericRepository<Hoja> _hojaRepository;
        private readonly IGenericRepository<Auditoria> _auditoriaRepository;
        private readonly IGenericRepository<HojaEstado> _hojaEstadoRepository;
        private readonly DBSettings _dbSettings;
        private readonly GroupsSettings _groupsSettings;
        private readonly MonedasSettings _monedasSettings;

        public HojaDeRutaService(
            IGenericRepository<Hoja> hojaRepository,
            IGenericRepository<Auditoria> auditoriaRepository,
            IGenericRepository<HojaEstado> hojaEstadoRepository,
            IOptions<GroupsSettings> groupsSettings,
            IOptions<DBSettings> dbSettings,
            IOptions<MonedasSettings> monedasSettings

            )
        {
            _hojaRepository = hojaRepository;
            _auditoriaRepository = auditoriaRepository;
            _hojaEstadoRepository = hojaEstadoRepository;
            _dbSettings = dbSettings.Value;
            _groupsSettings = groupsSettings.Value;
            _monedasSettings = monedasSettings.Value;
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

                //PARA TEST
                var parameters = new Dictionary<string, string>
                {
                    { "Nivel", "" },
                    { "Sector", "" },
                    { "Usuario ", "" },
                    { "Id", id }
                };


                //var parameters = new Dictionary<string, string>
                //{
                //    { "Id", id }
                //};

                IEnumerable<Hoja> hojas = await _hojaRepository.ExecuteStoredProcedureAsync(spName, parameters);

                return hojas.FirstOrDefault();
                //return await hojaRepository.GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
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
    }
}
