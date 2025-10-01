using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services.Repository;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

namespace HojaDeRuta.Services
{
    public class RevisorService
    {
        private readonly IGenericRepository<Revisores> revisoresRepository;
        private readonly DBSettings dbSettings;
        public RevisorService(
            IGenericRepository<Revisores> revisoresRepository,
            IOptions<DBSettings> dbSettings
            )
        {
            this.revisoresRepository = revisoresRepository;
            this.dbSettings = dbSettings.Value;
        }

        public async Task<List<Revisores>> GetAllRevisores()
        {
            try
            {
                IEnumerable<Revisores> revisores = await revisoresRepository.GetAllAsync();
                return revisores.OrderBy(r => r.Detalle).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<Revisores> GetRevisorByName(string name)
        {
            Expression<Func<Revisores, bool>> revisor = s => s.Empleado == name;

            var revisores = await revisoresRepository.FindAsync(revisor);

            return revisores.FirstOrDefault();
        }

        public async Task<List<Revisores>> GetRevisoresByNivel(Dictionary<string, int> parameters)
        {
            try
            {
                var spName = dbSettings.Sp["GetRevisoresByNivel"].ToString();

                IEnumerable<Revisores> revisores = await revisoresRepository.ExecuteStoredProcedureAsync(spName, parameters);
                return revisores.OrderBy(r => r.Detalle).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }        
    }
}
