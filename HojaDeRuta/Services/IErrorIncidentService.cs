namespace HojaDeRuta.Services;

public interface IErrorIncidentService
{
    Task<string> ReportAsync(
        Exception exception,
        string errorCode,
        string userMessage,
        ErrorIncidentContext? context = null,
        CancellationToken cancellationToken = default);

    Task<ErrorIncidentReportResult> ReportOnceAsync(
        Exception exception,
        string errorCode,
        string userMessage,
        ErrorIncidentContext context,
        CancellationToken cancellationToken = default);

    Task<int> ResolveOpenIncidentsAsync(
        string endpoint,
        string errorCodePrefix,
        IReadOnlyCollection<string> activeFingerprints,
        CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}

public sealed record ErrorIncidentReportResult(string IncidentId, bool Created);
