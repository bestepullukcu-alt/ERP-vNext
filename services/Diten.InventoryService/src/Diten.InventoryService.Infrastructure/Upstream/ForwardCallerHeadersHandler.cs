using Diten.InventoryService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.InventoryService.Infrastructure.Upstream;

/// <summary>
/// Forwards the caller's bearer token and correlation id to PRODUCT-MASTER / LOCATION. Both masters are
/// Tenant + LegalEntity scoped and resolve scope from that token themselves (contract descriptions) — this service
/// never sends a tenant or legal-entity value of its own choosing.
/// </summary>
public sealed class ForwardCallerHeadersHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ForwardCallerHeadersHandler(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incoming = _httpContextAccessor.HttpContext?.Request;
        if (incoming is not null)
        {
            var authorization = incoming.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authorization))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authorization);
            }

            // The resolved id (caller-supplied or server-minted) — the same one this service persists and echoes.
            var correlation = _httpContextAccessor.HttpContext!.RequestServices.GetService<CorrelationContext>();
            if (correlation is not null)
            {
                request.Headers.TryAddWithoutValidation(InventoryContract.CorrelationHeader, correlation.CorrelationId.ToString());
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
