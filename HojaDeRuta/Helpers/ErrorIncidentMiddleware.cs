using HojaDeRuta.Services;

namespace HojaDeRuta.Helpers;

public sealed class ErrorIncidentMiddleware
{
    private const string GenericErrorCode = "HDR-UNEXPECTED-001";
    private const string GenericUserMessage = "Ocurrió un error inesperado al procesar la solicitud. Intentá nuevamente en unos instantes.";
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorIncidentMiddleware> _logger;

    public ErrorIncidentMiddleware(RequestDelegate next, ILogger<ErrorIncidentMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IErrorIncidentService errorIncidentService)
    {
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = context.TraceIdentifier,
            ["Endpoint"] = context.Request.Path.Value ?? string.Empty,
            ["HttpMethod"] = context.Request.Method,
            ["UserName"] = context.User.Identity?.Name
        }))
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (!context.Response.HasStarted)
            {
                var incidentId = await errorIncidentService.ReportAsync(
                    ex,
                    GenericErrorCode,
                    GenericUserMessage,
                    new ErrorIncidentContext
                    {
                        Endpoint = context.Request.Path.Value,
                        UserName = context.User.Identity?.Name,
                        TraceId = context.TraceIdentifier
                    },
                    context.RequestAborted);

                context.Response.Clear();
                if (IsJsonRequest(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsJsonAsync(new { success = false, message = GenericUserMessage, incidentId }, cancellationToken: context.RequestAborted);
                    return;
                }

                context.Response.Redirect($"/Error?incidentId={Uri.EscapeDataString(incidentId)}");
            }
        }
    }

    private static bool IsJsonRequest(HttpRequest request) =>
        request.Headers.Accept.Any(value => value.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        || string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
}
