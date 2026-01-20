using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services.Repository;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using NuGet.Common;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace HojaDeRuta.Services
{
    public class SharedService
    {
        private readonly IGenericRepository<TipoDocumento> tipoDocRepository;
        private readonly IGenericRepository<Sector> sectorRepository;
        private readonly IGenericRepository<SubArea> subAreaRepository;
        private readonly IGenericRepository<Socios> sociosRepository;
        private readonly IGenericRepository<Contratos> contratosRepository;
        private readonly IGenericRepository<Jurisdiccion> jurisdiccionRepository;
        private readonly DBSettings dbSettings;

        public SharedService(
            IGenericRepository<TipoDocumento> tipoDocRepository,
            IGenericRepository<Sector> sectorRepository,
            IGenericRepository<SubArea> subAreaRepository,
            IGenericRepository<Socios> sociosRepository,
            IGenericRepository<Contratos> contratosRepository,
            IGenericRepository<Jurisdiccion> jurisdiccionRepository,
            IOptions<DBSettings> dbSettings
            )
        {
            this.tipoDocRepository = tipoDocRepository;
            this.sectorRepository = sectorRepository;
            this.subAreaRepository = subAreaRepository;
            this.sociosRepository = sociosRepository;
            this.contratosRepository = contratosRepository;
            this.jurisdiccionRepository = jurisdiccionRepository;
            this.dbSettings = dbSettings.Value;
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

        public async Task<bool> RequiereAuditoria(string nombreGenerico)
        {
            try
            {
                var tiposDoc = await GetTipoDocumentos();
                TipoDocumento tipoDoc = tiposDoc.Where(t => t.NombreGenerico == nombreGenerico).FirstOrDefault();

                return tipoDoc.Categoria == "Auditoria";
            }
            catch (Exception ex)
            {
                return false;
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

        public async Task<Sector> GetSectorByDetalle(string sectorDetalle)
        {
            try
            {                
                Expression<Func<Sector, bool>> entityName = s => s.Detalle == sectorDetalle;
                Expression<Func<Sector, Object>> order = s => s.Nombre;

                return await sectorRepository.GetFirstOrLastAsync(entityName, order, false);
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

        public async Task<List<Jurisdiccion>> GetJurisdicciones()
        {
            try
            {
                IEnumerable<Jurisdiccion> jurisdicciones = await jurisdiccionRepository.GetAllAsync();
                return jurisdicciones.OrderBy(s => s.Name).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<List<Socios>> GetAllSocios()
        {
            try
            {
                IEnumerable<Socios> socios = await sociosRepository.GetAllAsync();
                return socios.OrderBy(r => r.Detalle).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<Socios> GetSocioByCodigo(string CodSocio)
        {
            try
            {
                Expression<Func<Socios, bool>> entityName = s => s.Socio == CodSocio;
                Expression<Func<Socios, Object>> order = s => s.Socio;

                return await sociosRepository.GetFirstOrLastAsync(entityName, order, false);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<Socios> GetSocioLiderByArea(Dictionary<string, string> parameters)
        {
            try
            {
                var spName = dbSettings.Sp["GetSocioLiderDeArea"].ToString();

                IEnumerable<Socios> socios = await sociosRepository.ExecuteStoredProcedureAsync(spName, parameters);
                return socios.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<List<Contratos>> GetContratos()
        {
            try
            {
                IEnumerable<Contratos> contratos = await contratosRepository.GetAllAsync();
                return contratos.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<List<Contratos>> GetContratosByCodigoPlataforma(string? CodigoPlataforma)
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

        public async Task CreateContratosRange(List<Contratos> contratos)
        {
            try
            {
                await contratosRepository.AddRangeAsync(contratos);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task CreateContrato(Contratos contrato)
        {
            try
            {
                await contratosRepository.AddAsync(contrato);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

    }
}
