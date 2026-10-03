using Diten.Platform.Common.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.ApiGateway.Observability;

public sealed class CorrelationPropagationDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CorrelationOptions _options;

    public CorrelationPropagationDelegatingHandler(
        IHttpContextAccessor httpContextAccessor,
        IOptions<ObservabilityOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value.Correlation;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // INTX FIX3 — what a caller put on X-Correlation-Id never travels on, whatever else holds; the value forwarded is
        // the one CorrelationIdMiddleware minted — not TraceIdentifier, which Ocelot overwrites with a caller's RequestId.
        request.Headers.Remove(_options.HeaderName);
        if (EdgeCorrelation.CorrelationIdOf(_httpContextAccessor.HttpContext) is { } correlationId)
        {
            request.Headers.TryAddWithoutValidation(_options.HeaderName, correlationId);
        }

        // INTX FIX2 — the client's own value travels apart from the gateway's correlation, and only as the gateway read
        // it (safe characters, bounded): whatever the caller put on the client header itself is dropped.
        request.Headers.Remove(_options.ClientHeaderName);
        if (_httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ClientCorrelationItemKey] is string clientCorrelation
            && !string.IsNullOrWhiteSpace(clientCorrelation))
        {
            request.Headers.TryAddWithoutValidation(_options.ClientHeaderName, clientCorrelation);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
