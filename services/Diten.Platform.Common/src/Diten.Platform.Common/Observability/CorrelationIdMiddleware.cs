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

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlationContext)
    {
        var correlationId = ResolveCorrelationId(context);
        correlationContext.SetCorrelationId(correlationId);
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
        if (context.Request.Headers.TryGetValue(_options.HeaderName, out var values))
        {
            var inbound = values.FirstOrDefault();
            if (IsSafeCorrelationId(inbound))
            {
                return inbound!;
            }
        }

        return Guid.NewGuid().ToString("N");
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
