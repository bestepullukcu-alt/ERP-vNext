using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace Diten.Platform.Common.Observability;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CorrelationOptions _options;

    public CorrelationIdMiddleware(RequestDelegate next, IOptions<ObservabilityOptions> options)
    {
        _next = next;
        _options = options.Value.Correlation;
    }

    /// <summary>Where the client's own correlation value is kept for the rest of the request (the gateway forwards it).</summary>
    public const string ClientCorrelationItemKey = "Diten.ClientCorrelationId";

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var correlationId = ResolveCorrelationId(context);
        correlationContext.SetCorrelationId(correlationId);
        var clientCorrelation = ResolveClientCorrelationId(context);
        if (clientCorrelation is not null)
        {
            correlationContext.SetClientCorrelationId(clientCorrelation);
            context.Items[ClientCorrelationItemKey] = clientCorrelation;
        }
        context.TraceIdentifier = correlationId;
        context.Response.Headers[_options.HeaderName] = correlationId;

        Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString()))
        {
            await _next(context);
        }
    }

    private string ResolveCorrelationId(HttpContext context)
    {
        // INTX FIX2 — at the edge (TrustInboundCorrelation off) the caller's value is never adopted.
        if (_options.TrustInboundCorrelation && context.Request.Headers.TryGetValue(_options.HeaderName, out var values))
        {
            var inbound = values.FirstOrDefault();
            if (IsSafeCorrelationId(inbound))
            {
                return inbound!;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// The client's own value: at the edge it is what the caller sent as the correlation header; behind the edge it is
    /// what the gateway forwarded on the client header. Either way only a safe, bounded value is kept.
    /// </summary>
    private string? ResolveClientCorrelationId(HttpContext context)
    {
        var header = _options.TrustInboundCorrelation ? _options.ClientHeaderName : _options.HeaderName;
        if (!context.Request.Headers.TryGetValue(header, out var values))
        {
            return null;
        }

        var inbound = values.FirstOrDefault();
        return IsSafeCorrelationId(inbound) ? inbound : null;
    }

    private bool IsSafeCorrelationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > _options.MaxLength)
        {
            return false;
        }

        return value.All(static c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.');
    }
}
