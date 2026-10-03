using Microsoft.Extensions.Primitives;
using Serilog.Core;
using Serilog.Events;
namespace Diten.SupplyChainService.Api.Logging;
// Puts the inbound X-Correlation-Id on every event of the request, including hosting lines and middleware rejections
// that never enter the MediatR scope. Same rule as the module middleware: exactly one non-empty UUID, otherwise nothing.
// When the inbound value fails that rule, the id the middleware generated and returned to the client is used instead
// (Q280): it is read from the response header, which only service code writes, and passes the same UUID rule.
public sealed class CorrelationIdEnricher(IHttpContextAccessor accessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var http = accessor.HttpContext;
        if (http is null) return;
        if (!TryReadUuid(http.Request.Headers["X-Correlation-Id"], out var correlationId) && !TryReadUuid(http.Response.Headers["X-Correlation-Id"], out correlationId)) return;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CorrelationId", correlationId));
    }
    private static bool TryReadUuid(StringValues values, out Guid id) =>
        Guid.TryParse(values.Count == 1 ? values.ToString() : null, out id) && id != Guid.Empty;
}
