using Diten.Platform.Common.Observability;
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
}
