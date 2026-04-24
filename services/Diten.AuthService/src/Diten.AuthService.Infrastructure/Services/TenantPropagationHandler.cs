using Diten.AuthService.Application.Common;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class TenantPropagationHandler : DelegatingHandler
{
    private const string TenantHeader = "X-Tenant-Id";
    private readonly ITenantContext _tenantContext;

    public TenantPropagationHandler(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_tenantContext.IsResolved)
        {
            request.Headers.Remove(TenantHeader);
            request.Headers.Add(TenantHeader, _tenantContext.TenantId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
