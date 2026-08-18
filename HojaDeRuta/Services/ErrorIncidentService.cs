using HojaDeRuta.DBContext;
using HojaDeRuta.Models.DAO;
using Microsoft.EntityFrameworkCore;

namespace HojaDeRuta.Services;

public sealed class ErrorIncidentService : IErrorIncidentService
{
    public const int IncidentIdMaxLength = 12;
    public const int ErrorCodeMaxLength = 80;
    public const int UserNameMaxLength = 256;
    public const int HojaIdMaxLength = 128;
    public const int OperationIdMaxLength = 64;
    public const int EndpointMaxLength = 512;
    public const int UserMessageMaxLength = 500;
    public const int ExceptionMessageMaxLength = 4000;

    private readonly HojasDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ErrorIncidentService> _logger;

    public ErrorIncidentService(HojasDbContext db, IHttpContextAccessor httpContextAccessor, ILogger<ErrorIncidentService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<string> ReportAsync(Exception exception, string errorCode, string userMessage, ErrorIncidentContext? context = null, CancellationToken cancellationToken = default)
    {
        var result = await ReportCoreAsync(exception, errorCode, userMessage, context ?? new ErrorIncidentContext(), null, cancellationToken);
        return result.IncidentId;
    }

    public async Task<ErrorIncidentReportResult> ReportOnceAsync(
        Exception exception,
        string errorCode,
        string userMessage,
        ErrorIncidentContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Fingerprint))
        {
            throw new ArgumentException("Las incidencias deduplicadas requieren Fingerprint.", nameof(context));
        }

        var fingerprint = Truncate(context.Fingerprint, 64);
        var existing = await _db.ErrorLogs.AsNoTracking()
            .Where(error => error.Fingerprint == fingerprint && error.ResolvedAt == null)
            .OrderBy(error => error.Id)
            .Select(error => error.IncidentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return new ErrorIncidentReportResult(existing, false);
        }

        return await ReportCoreAsync(exception, errorCode, userMessage, context, fingerprint, cancellationToken);
    }

    public Task<int> ResolveOpenIncidentsAsync(
        string endpoint,
        string errorCodePrefix,
        IReadOnlyCollection<string> activeFingerprints,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ErrorLogs.Where(error =>
            error.Endpoint == endpoint &&
            error.ErrorCode.StartsWith(errorCodePrefix) &&
            error.Fingerprint != null &&
            error.ResolvedAt == null);

        if (activeFingerprints.Count > 0)
        {
            query = query.Where(error => !activeFingerprints.Contains(error.Fingerprint!));
        }

        return query.ExecuteUpdateAsync(
            setters => setters.SetProperty(error => error.ResolvedAt, DateTime.UtcNow),
            cancellationToken);
    }

    private async Task<ErrorIncidentReportResult> ReportCoreAsync(
        Exception exception,
        string errorCode,
        string userMessage,
        ErrorIncidentContext context,
        string? fingerprint,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var resolvedContext = context;
        var incidentId = CreateIncidentId();
        var endpoint = resolvedContext.Endpoint ?? httpContext?.Request.Path.Value ?? "background-job";
        var userName = resolvedContext.UserName ?? httpContext?.User.Identity?.Name;
        var traceId = resolvedContext.TraceId ?? httpContext?.TraceIdentifier;

        _logger.LogError(exception, "HDR_ERROR IncidentId={IncidentId} ErrorCode={ErrorCode} TraceId={TraceId} Endpoint={Endpoint} UserName={UserName} HojaId={HojaId} OperationId={OperationId}", incidentId, errorCode, traceId ?? "(none)", endpoint, userName ?? "(anonymous)", resolvedContext.HojaId ?? "(none)", resolvedContext.OperationId ?? "(none)");

        var errorLog = new ErrorLog
        {
            OccurredAt = DateTime.UtcNow,
            ErrorCode = Truncate(errorCode, ErrorCodeMaxLength),
            UserName = TruncateOrNull(userName, UserNameMaxLength),
            HojaId = TruncateOrNull(resolvedContext.HojaId, HojaIdMaxLength),
            OperationId = TruncateOrNull(resolvedContext.OperationId, OperationIdMaxLength),
            Endpoint = Truncate(endpoint, EndpointMaxLength),
            UserMessage = Truncate(userMessage, UserMessageMaxLength),
            ExceptionMessage = Truncate(exception.ToString(), ExceptionMessageMaxLength),
            Fingerprint = fingerprint
        };

        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                errorLog.IncidentId = attempt == 0 ? incidentId : CreateIncidentId();
                _db.ErrorLogs.Add(errorLog);
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    return new ErrorIncidentReportResult(errorLog.IncidentId, true);
                }
                catch (DbUpdateException ex) when (attempt < 2 && IsIncidentIdCollision(ex))
                {
                    _db.Entry(errorLog).State = EntityState.Detached;
                    _logger.LogWarning(ex, "Colision de IncidentId al guardar ErrorLog. Se reintentara. IncidentId={IncidentId}", errorLog.IncidentId);
                }
            }
        }
        catch (Exception persistenceException)
        {
            _logger.LogCritical(persistenceException, "HDR_ERRORLOG_PERSISTENCE_FAILED IncidentId={IncidentId} ErrorCode={ErrorCode}. La incidencia original no pudo guardarse en dbo.ErrorLog.", incidentId, errorCode);
        }

        return new ErrorIncidentReportResult(incidentId, true);
    }

    public Task<int> DeleteExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        _db.ErrorLogs.Where(error =>
            (error.Fingerprint == null && error.OccurredAt < cutoffUtc) ||
            (error.ResolvedAt != null && error.ResolvedAt < cutoffUtc))
            .ExecuteDeleteAsync(cancellationToken);

    internal static string CreateIncidentId() => Guid.NewGuid().ToString("N")[..IncidentIdMaxLength].ToUpperInvariant();

    internal static string Truncate(string? value, int maxLength)
    {
        var normalized = value ?? string.Empty;
        if (normalized.Length <= maxLength) return normalized;
        return maxLength == 1 ? "\u2026" : string.Concat(normalized.AsSpan(0, maxLength - 1), "\u2026");
    }

    private static string? TruncateOrNull(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) ? null : Truncate(value, maxLength);

    private static bool IsIncidentIdCollision(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("UX_ErrorLog_IncidentId", StringComparison.OrdinalIgnoreCase) == true;
}
