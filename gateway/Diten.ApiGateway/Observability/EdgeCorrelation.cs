using Diten.Platform.Common.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ApiGateway.Observability;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX2 — the gateway is the edge: it does not adopt a caller's X-Correlation-Id as the
/// request's correlation (a caller could otherwise make its change look like part of another request, or gather a
/// tenant's records under one id). It mints its own and forwards the caller's value apart, on X-Client-Correlation-Id
/// (CorrelationPropagationDelegatingHandler). Services behind the gateway keep the Common default and trust the
/// gateway's value — this is set here, for the gateway only.
/// </summary>
public static class EdgeCorrelation
{
    public static IServiceCollection AddEdgeCorrelation(this IServiceCollection services) =>
        services.PostConfigure<ObservabilityOptions>(options => options.Correlation.TrustInboundCorrelation = false);

    /// <summary>
    /// INTX FIX3 — the request's correlation as CorrelationIdMiddleware minted it, or null before it ran. Never
    /// <see cref="HttpContext.TraceIdentifier"/>: Ocelot's RequestId middleware writes a caller's <c>RequestId</c> header
    /// there, unchecked. Forwarded downstream and written to the request log from here.
    /// </summary>
    public static string? CorrelationIdOf(HttpContext? context) =>
        context?.Items[CorrelationIdMiddleware.CorrelationItemKey] as string is { Length: > 0 } value ? value : null;
}
