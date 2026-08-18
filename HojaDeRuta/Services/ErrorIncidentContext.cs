namespace HojaDeRuta.Services;

public sealed class ErrorIncidentContext
{
    public string? UserName { get; init; }
    public string? HojaId { get; init; }
    public string? OperationId { get; init; }
    public string? Endpoint { get; init; }
    public string? TraceId { get; init; }
    public string? Fingerprint { get; init; }
}
