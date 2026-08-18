using System.Collections.Concurrent;
using System.Text.Json;
using HojaDeRuta.Models.DAO;
using Microsoft.Extensions.Caching.Distributed;

namespace HojaDeRuta.Services
{
    public class CatalogCacheService : ICatalogCacheService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeSpan StandardCatalogTtl = TimeSpan.FromMinutes(20);
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();
        private static readonly SemaphoreSlim FactoryExecutionGate = new(1, 1);
        private const string ClientesCacheKey = "catalog:clientes:v1";
        private const string SociosCacheKey = "catalog:socios:v1";
        private const string RevisoresCacheKey = "catalog:revisores:v1";
        private const string ContratosCacheVersionKey = "catalog:contratos:version";
        private const string DefaultContratosCacheVersion = "v1";

        private readonly IDistributedCache _cache;
        private readonly ClienteService _clienteService;
        private readonly SharedService _sharedService;
        private readonly RevisorService _revisorService;

        public CatalogCacheService(
            IDistributedCache cache,
            ClienteService clienteService,
            SharedService sharedService,
            RevisorService revisorService)
        {
            _cache = cache;
            _clienteService = clienteService;
            _sharedService = sharedService;
            _revisorService = revisorService;
        }

        public Task<List<Clientes>> GetClientesAsync(CancellationToken cancellationToken = default)
            => GetOrCreateAsync(ClientesCacheKey, StandardCatalogTtl, _clienteService.GetClientes, cancellationToken);

        public Task InvalidateClientesAsync(CancellationToken cancellationToken = default)
            => _cache.RemoveAsync(ClientesCacheKey, cancellationToken);

        public Task<List<Socios>> GetSociosAsync(CancellationToken cancellationToken = default)
            => GetOrCreateAsync(SociosCacheKey, StandardCatalogTtl, _sharedService.GetAllSocios, cancellationToken);

        public Task<List<Revisores>> GetRevisoresAsync(CancellationToken cancellationToken = default)
            => GetOrCreateAsync(RevisoresCacheKey, StandardCatalogTtl, _revisorService.GetAllRevisores, cancellationToken);

        public async Task InvalidateUsuariosAsync(CancellationToken cancellationToken = default)
        {
            await _cache.RemoveAsync(SociosCacheKey, cancellationToken);
            await _cache.RemoveAsync(RevisoresCacheKey, cancellationToken);
        }

        public Task<List<TipoDocumento>> GetTipoDocumentosAsync(CancellationToken cancellationToken = default)
            => GetOrCreateAsync("catalog:tipos-documento:v1", StandardCatalogTtl, _sharedService.GetTipoDocumentos, cancellationToken);

        public Task<List<Sector>> GetSectoresAsync(CancellationToken cancellationToken = default)
            => GetOrCreateAsync("catalog:sectores:v1", StandardCatalogTtl, _sharedService.GetSectores, cancellationToken);

        public Task<List<SubArea>> GetSubAreasAsync(CancellationToken cancellationToken = default)
            => GetOrCreateAsync("catalog:subareas:v1", StandardCatalogTtl, _sharedService.GetSubAreas, cancellationToken);

        public Task<List<Jurisdiccion>> GetJurisdiccionesAsync(CancellationToken cancellationToken = default)
            => GetOrCreateAsync("catalog:jurisdicciones:v1", StandardCatalogTtl, _sharedService.GetJurisdicciones, cancellationToken);

        public Task<List<Rutas>> GetRutasAsync(CancellationToken cancellationToken = default)
            => GetOrCreateAsync("catalog:rutas:v1", StandardCatalogTtl, _sharedService.GetRutas, cancellationToken);

        public Task<List<Contratos>> GetContratosByCodigoPlataformaAsync(string? codigoPlataforma, CancellationToken cancellationToken = default)
            => GetContratosByCodigoPlataformaInternalAsync(codigoPlataforma, cancellationToken);

        public Task InvalidateContratosAsync(CancellationToken cancellationToken = default)
            => _cache.SetStringAsync(
                ContratosCacheVersionKey,
                DateTime.UtcNow.Ticks.ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                },
                cancellationToken);

        private async Task<List<Contratos>> GetContratosByCodigoPlataformaInternalAsync(string? codigoPlataforma, CancellationToken cancellationToken)
        {
            var normalizedCode = string.IsNullOrWhiteSpace(codigoPlataforma)
                ? "all"
                : codigoPlataforma.Trim().ToLowerInvariant();
            var version = await GetContratosCacheVersionAsync(cancellationToken);

            return await GetOrCreateAsync(
                $"catalog:contratos:{normalizedCode}:{version}",
                StandardCatalogTtl,
                () => _sharedService.GetContratosByCodigoPlataforma(codigoPlataforma),
                cancellationToken);
        }

        private async Task<string> GetContratosCacheVersionAsync(CancellationToken cancellationToken)
        {
            var version = await _cache.GetStringAsync(ContratosCacheVersionKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            await _cache.SetStringAsync(
                ContratosCacheVersionKey,
                DefaultContratosCacheVersion,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                },
                cancellationToken);

            return DefaultContratosCacheVersion;
        }

        private async Task<List<T>> GetOrCreateAsync<T>(
            string cacheKey,
            TimeSpan ttl,
            Func<Task<List<T>>> factory,
            CancellationToken cancellationToken)
        {
            var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                var value = JsonSerializer.Deserialize<List<T>>(cached, SerializerOptions);
                if (value != null)
                {
                    return value;
                }
            }

            var gate = Locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    var warmedValue = JsonSerializer.Deserialize<List<T>>(cached, SerializerOptions);
                    if (warmedValue != null)
                    {
                        return warmedValue;
                    }
                }

                await FactoryExecutionGate.WaitAsync(cancellationToken);
                try
                {
                    cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(cached))
                    {
                        var warmedValue = JsonSerializer.Deserialize<List<T>>(cached, SerializerOptions);
                        if (warmedValue != null)
                        {
                            return warmedValue;
                        }
                    }

                    var created = await factory();
                    await _cache.SetStringAsync(
                        cacheKey,
                        JsonSerializer.Serialize(created, SerializerOptions),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = ttl
                        },
                        cancellationToken);

                    return created;
                }
                finally
                {
                    FactoryExecutionGate.Release();
                }
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
