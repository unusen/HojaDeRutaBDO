using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services.Repository;
using Microsoft.Extensions.Options;

namespace HojaDeRuta.Services
{
    public class HojaDeRutaService
    {
        private readonly IGenericRepository<Hoja> _hojaRepository;
        private readonly DBSettings _dbSettings;
        private readonly GroupsSettings _groupsSettings;

        public HojaDeRutaService(
            IGenericRepository<Hoja> hojaRepository,
            IOptions<GroupsSettings> groupsSettings,
            IOptions<DBSettings> dbSettings

            )
        {
            _hojaRepository = hojaRepository;
            _dbSettings = dbSettings.Value;
            _groupsSettings = groupsSettings.Value;
        }

        public async Task<List<Hoja>> GetHojas(Dictionary<string, string> parameters)
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
    }
}
