using Diten.Application.EnterpriseStrategy.Shared;

namespace Diten.WebAPI.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContextAccessor correlationContext)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var values) && !string.IsNullOrWhiteSpace(values)
            ? values.ToString()
            : Guid.NewGuid().ToString("N");

        correlationContext.CorrelationId = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context);
    }
}
