using System.Text.Json;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DTO;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace HojaDeRuta.Services
{
    public class RutaDocumentoService : IRutaDocumentoService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private static readonly TimeSpan ValidationTtl = TimeSpan.FromMinutes(2);

        private readonly ICatalogCacheService _catalogCacheService;
        private readonly IDistributedCache _cache;
        private readonly ILogger<RutaDocumentoService> _logger;
        private readonly PathSetings _pathSettings;

        public RutaDocumentoService(
            ICatalogCacheService catalogCacheService,
            IDistributedCache cache,
            IOptions<PathSetings> pathSettings,
            ILogger<RutaDocumentoService> logger)
        {
            _catalogCacheService = catalogCacheService;
            _cache = cache;
            _logger = logger;
            _pathSettings = pathSettings.Value;
        }

        public async Task<string> ResolveNetworkPathAsync(string? ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(_pathSettings.LocalOverridePath) && ruta.Length >= 2 && ruta[1] == ':')
            {
                return _pathSettings.LocalOverridePath + ruta.Substring(2);
            }

            if (ruta.Length < 2 || ruta[1] != ':')
            {
                return ruta;
            }

            var routes = await _catalogCacheService.GetRutasAsync();
            var route = routes.FirstOrDefault(r => string.Equals(r.Letra, ruta.Substring(0, 1), StringComparison.OrdinalIgnoreCase));

            return route == null
                ? ruta
                : route.Ruta + ruta.Substring(2);
        }

        public async Task<FileValidationResult> ValidateAttachmentAsync(string hojaId, string? rutaDoc, string? adjunto)
        {
            if (string.IsNullOrWhiteSpace(rutaDoc) || string.IsNullOrWhiteSpace(adjunto))
            {
                return new FileValidationResult
                {
                    Success = false,
                    Severity = "error",
                    Message = "Todavía faltan datos del documento adjunto para poder validarlo."
                };
            }

            var resolvedPath = await ResolveNetworkPathAsync(rutaDoc);
            var cacheKey = $"file-validation:{hojaId}:{resolvedPath}:{adjunto}".ToLowerInvariant();
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                var cachedValue = JsonSerializer.Deserialize<FileValidationResult>(cached, SerializerOptions);
                if (cachedValue != null)
                {
                    return cachedValue;
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            FileValidationResult result;

            if (!Directory.Exists(resolvedPath))
            {
                result = new FileValidationResult
                {
                    Success = false,
                    Severity = "error",
                    Message = "No pudimos acceder a la carpeta del documento adjunto. Revisá la ruta configurada e intentá nuevamente."
                };
            }
            else
            {
                var files = Directory.GetFiles(resolvedPath).Select(Path.GetFileName).ToList();
                var exists = files.Any(file => file != null && file.Equals(adjunto, StringComparison.OrdinalIgnoreCase));

                result = exists
                    ? new FileValidationResult
                    {
                        Success = true,
                        Severity = "success",
                        Message = "Archivo encontrado correctamente."
                    }
                    : new FileValidationResult
                    {
                        Success = false,
                        Severity = "error",
                        Message = "No encontramos el archivo adjunto en la carpeta indicada. Verificá el nombre y volvé a intentarlo."
                    };
            }

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result, SerializerOptions),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ValidationTtl
                });

            sw.Stop();
            if (sw.ElapsedMilliseconds > 1000)
            {
                _logger.LogWarning(
                    "Validación de archivo lenta. HojaId={HojaId} Path={Path} DurationMs={DurationMs}",
                    hojaId,
                    resolvedPath,
                    sw.ElapsedMilliseconds);
            }

            return result;
        }
    }
}
