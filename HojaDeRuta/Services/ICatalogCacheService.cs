using HojaDeRuta.Models.DAO;

namespace HojaDeRuta.Services
{
    public interface ICatalogCacheService
    {
        Task<List<Clientes>> GetClientesAsync(CancellationToken cancellationToken = default);
        Task InvalidateClientesAsync(CancellationToken cancellationToken = default);
        Task<List<Socios>> GetSociosAsync(CancellationToken cancellationToken = default);
        Task<List<Revisores>> GetRevisoresAsync(CancellationToken cancellationToken = default);
        Task<List<TipoDocumento>> GetTipoDocumentosAsync(CancellationToken cancellationToken = default);
        Task<List<Sector>> GetSectoresAsync(CancellationToken cancellationToken = default);
        Task<List<SubArea>> GetSubAreasAsync(CancellationToken cancellationToken = default);
        Task<List<Jurisdiccion>> GetJurisdiccionesAsync(CancellationToken cancellationToken = default);
        Task<List<Rutas>> GetRutasAsync(CancellationToken cancellationToken = default);
        Task<List<Contratos>> GetContratosByCodigoPlataformaAsync(string? codigoPlataforma, CancellationToken cancellationToken = default);
        Task InvalidateContratosAsync(CancellationToken cancellationToken = default);
    }
}
