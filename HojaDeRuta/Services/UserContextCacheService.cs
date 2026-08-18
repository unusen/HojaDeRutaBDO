using System.Text.Json;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Services.LoginService;
using Microsoft.Extensions.Caching.Distributed;

namespace HojaDeRuta.Services
{
    public class UserContextCacheService : IUserContextCacheService
    {
        private const string HttpContextItemKey = "HDR.CurrentUserContext";
        private const string HttpContextSourceKey = "HDR.CurrentUserContextSource";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        private readonly IDistributedCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILoginService _loginService;
        private readonly ILogger<UserContextCacheService> _logger;

        public UserContextCacheService(
            IDistributedCache cache,
            IHttpContextAccessor httpContextAccessor,
            ILoginService loginService,
            ILogger<UserContextCacheService> logger)
        {
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
            _loginService = loginService;
            _logger = logger;
        }

        public async Task<UserContext> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Items[HttpContextItemKey] is UserContext cachedContext)
            {
                return cachedContext;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cacheKey = GetCacheKey();
            var json = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrWhiteSpace(json))
            {
                var distributedContext = JsonSerializer.Deserialize<UserContext>(json, SerializerOptions);
                if (distributedContext != null)
                {
                    SetRequestCache(distributedContext, "cache");
                    sw.Stop();
                    LogDuration(sw.ElapsedMilliseconds, "cache", distributedContext);
                    return distributedContext;
                }
            }

            var userContext = await BuildUserContextAsync();
            userContext.HighestRole = userContext.Roles?
                .OrderByDescending(role => role.Nivel)
                .FirstOrDefault()
                ?.Nivel ?? 0;
            userContext.FetchedAtUtc = DateTimeOffset.UtcNow;

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(userContext, SerializerOptions),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                },
                cancellationToken);

            SetRequestCache(userContext, "live");
            sw.Stop();
            LogDuration(sw.ElapsedMilliseconds, "live", userContext);
            return userContext;
        }

        public string GetLastSource()
        {
            return _httpContextAccessor.HttpContext?.Items[HttpContextSourceKey]?.ToString() ?? "unknown";
        }

        private async Task<UserContext> BuildUserContextAsync()
        {
            var email = _loginService.GetUserEmail();
            var roles = await _loginService.GetUserGroupsAsync();

            return new UserContext
            {
                UserName = _loginService.GetUserName(),
                Empleado = email.Split('@')[0].ToUpperInvariant(),
                Email = email,
                Area = await _loginService.GetUserAreaAsync(),
                Roles = roles
            };
        }

        private string GetCacheKey()
        {
            var email = _loginService.GetUserEmail();
            return $"user-context:{email.Trim().ToLowerInvariant()}";
        }

        private void SetRequestCache(UserContext userContext, string source)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return;
            }

            httpContext.Items[HttpContextItemKey] = userContext;
            httpContext.Items[HttpContextSourceKey] = source;
        }

        private void LogDuration(long durationMs, string source, UserContext userContext)
        {
            if (durationMs > 250)
            {
                _logger.LogWarning(
                    "Carga de contexto de usuario lenta. User={User} Source={Source} DurationMs={DurationMs}",
                    userContext.UserName,
                    source,
                    durationMs);
                return;
            }

            _logger.LogInformation(
                "Contexto de usuario resuelto. User={User} Source={Source} DurationMs={DurationMs}",
                userContext.UserName,
                source,
                durationMs);
        }
    }
}
