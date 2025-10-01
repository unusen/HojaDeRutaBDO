using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services.Repository;
using Microsoft.Extensions.Options;
using NuGet.Common;
using System.Linq.Expressions;

namespace HojaDeRuta.Services
{
    public class SharedService
    {
        private readonly IGenericRepository<TipoDocumento> tipoDocRepository;
        private readonly IGenericRepository<Sector> sectorRepository;
        private readonly IGenericRepository<SubArea> subAreaRepository;
        private readonly IGenericRepository<Socios> socioslRepository;
        private readonly IGenericRepository<Contratos> contratosRepository;
        private readonly IGenericRepository<SyncControl> syncControlRepository;

        public SharedService(
            IGenericRepository<TipoDocumento> tipoDocRepository,
            IGenericRepository<Sector> sectorRepository,
            IGenericRepository<SubArea> subAreaRepository,
            IGenericRepository<Socios> socioslRepository,
            IGenericRepository<Contratos> contratosRepository,
            IGenericRepository<SyncControl> syncControlRepository
            )
        {
            this.tipoDocRepository = tipoDocRepository;
            this.sectorRepository = sectorRepository;
            this.subAreaRepository = subAreaRepository;
            this.socioslRepository = socioslRepository;
            this.contratosRepository = contratosRepository;
            this.syncControlRepository = syncControlRepository;
        }

        public async Task<List<TipoDocumento>> GetTipoDocumentos()
        {
            try
            {
                IEnumerable<TipoDocumento> tipoDoc = await tipoDocRepository.GetAllAsync();
                return tipoDoc.OrderBy(t => t.NombreGenerico).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<List<Sector>> GetSectores()
        {
            try
            {
                IEnumerable<Sector> sectores = await sectorRepository.GetAllAsync();
                return sectores.OrderBy(s => s.Nombre).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<List<SubArea>> GetSubAreas()
        {
            try
            {
                IEnumerable<SubArea> subAreas = await subAreaRepository.GetAllAsync();
                return subAreas.OrderBy(s => s.Nombre).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<List<Socios>> GetAllSocios()
        {
            //TODO: LOGICA DE ACTUALIZACION DE SOCIOS A PARTIR DEL LOGIN
            //TODO: LOGICA DE ACTUALIZACION DE USUARIOS EN GENERAL CON CADA LOGIN
            try
            {
                IEnumerable<Socios> socios = await socioslRepository.GetAllAsync();
                return socios.OrderBy(r => r.Detalle).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<List<Contratos>> GetContratos(string? CodigoPlataforma)
        {
            try
            {
                IEnumerable<Contratos> contratos = new List<Contratos>();
                if (!String.IsNullOrWhiteSpace(CodigoPlataforma))
                {
                    Expression<Func<Contratos, bool>> cod = c => c.CodigoPlataforma == CodigoPlataforma;
                    contratos = await contratosRepository.FindAsync(cod);

                }
                else
                {
                    contratos = await contratosRepository.GetAllAsync();
                }
                    
                return contratos.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }


        public async Task<DateTime?> GetLastSync(string EntityName)
        {
            try
            {
                Expression<Func<SyncControl, bool>> entityName = s => s.EntityName == EntityName;
                Expression<Func<SyncControl, Object>> lastSync = s => s.LastSyncDate;

                var sync = await syncControlRepository.GetFirstOrLastAsync(entityName, lastSync, false);

                return sync?.LastSyncDate ?? DateTime.MinValue;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task CreateSyncControl(SyncControl syncControl)
        {
            try
            {
                await syncControlRepository.AddAsync(syncControl);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

    }
}
