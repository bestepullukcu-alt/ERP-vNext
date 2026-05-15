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
        var correlationId = _httpContextAccessor.HttpContext?.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.Remove(_options.HeaderName);
            request.Headers.TryAddWithoutValidation(_options.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
