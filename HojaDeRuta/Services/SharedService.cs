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
        private readonly IGenericRepository<Rutas> rutasRepository;
        private readonly DBSettings dbSettings;
        private readonly ILogger<SharedService> _logger;

        public SharedService(
            IGenericRepository<TipoDocumento> tipoDocRepository,
            IGenericRepository<Sector> sectorRepository,
            IGenericRepository<SubArea> subAreaRepository,
            IGenericRepository<Socios> sociosRepository,
            IGenericRepository<Contratos> contratosRepository,
            IGenericRepository<Jurisdiccion> jurisdiccionRepository,
            IGenericRepository<Rutas> rutasRepository,
            IOptions<DBSettings> dbSettings,
            ILogger<SharedService> logger
            )
        {
            this.tipoDocRepository = tipoDocRepository;
            this.sectorRepository = sectorRepository;
            this.subAreaRepository = subAreaRepository;
            this.sociosRepository = sociosRepository;
            this.contratosRepository = contratosRepository;
            this.jurisdiccionRepository = jurisdiccionRepository;
            this.rutasRepository = rutasRepository;
            this.dbSettings = dbSettings.Value;
            this._logger = logger;
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
                _logger.LogError(ex, "Error al obtener tipos de documento.");
                throw new Exception("No se pudo cargar el catálogo de tipos de documento.", ex);
            }
        }

        public async Task<bool> RequiereAuditoria(string nombreGenerico)
        {
            try
            {
                await Task.CompletedTask;
                return string.Equals(nombreGenerico?.Trim(), "Informe del auditor", StringComparison.OrdinalIgnoreCase);
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
                _logger.LogError(ex, "Error al obtener listado de sectores.");
                throw new Exception("No se pudo cargar el listado de sectores/áreas.", ex);
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
                _logger.LogError(ex, "Error al obtener sector por detalle: {Sector}", sectorDetalle);
                throw new Exception("Error al intentar recuperar la información del sector especificado.", ex);
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
                _logger.LogError(ex, "Error al obtener listado de subáreas.");
                throw new Exception("No se pudo cargar el catálogo de subáreas.", ex);
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
                _logger.LogError(ex, "Error al obtener listado de jurisdicciones.");
                throw new Exception("No se pudo cargar el catálogo de jurisdicciones.", ex);
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
                _logger.LogError(ex, "Error al obtener el listado de socios.");
                throw new Exception("No se pudo cargar el catálogo de socios.", ex);
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
                _logger.LogError(ex, "Error al buscar socio por código: {Socio}", CodSocio);
                throw new Exception("No se pudo recuperar la información del socio.", ex);
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
                _logger.LogError(ex, "Error al obtener socio líder mediante procedimiento almacenado.");
                throw new Exception("No se pudo identificar al socio líder del área.", ex);
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
                _logger.LogError(ex, "Error al obtener el catálogo de contratos.");
                throw new Exception("No se pudo cargar el listado de contratos.", ex);
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
                _logger.LogError(ex, "Error al buscar contratos por código de plataforma: {Codigo}", CodigoPlataforma);
                throw new Exception("Error al intentar recuperar los contratos vinculados.", ex);
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
                _logger.LogError(ex, "Error al crear rango de contratos.");
                throw new Exception("No se pudieron registrar los nuevos contratos en la base de datos.", ex);
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
                _logger.LogError(ex, "Error al crear un contrato individual.");
                throw new Exception("No se pudo persistir el contrato en la base de datos.", ex);
            }
        }

        public async Task<Rutas> GetRutaByLetra(string letra)
        {
            try
            {
                Expression<Func<Rutas, bool>> entityName = s => s.Letra == letra;
                Expression<Func<Rutas, Object>> order = s => s.Area;

                return await rutasRepository.GetFirstOrLastAsync(entityName, order, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la ruta de red para la letra: {Letra}", letra);
                throw new Exception("No se pudo identificar la ruta de red correspondiente a la unidad especificada.", ex);
            }
        }

        public async Task<List<Rutas>> GetRutas()
        {
            try
            {
                IEnumerable<Rutas> rutas = await rutasRepository.GetAllAsync();
                return rutas
                    .OrderBy(r => r.Letra)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el catálogo completo de rutas.");
                throw new Exception("No se pudo cargar el catálogo de rutas de red.", ex);
            }
        }

    }
}
