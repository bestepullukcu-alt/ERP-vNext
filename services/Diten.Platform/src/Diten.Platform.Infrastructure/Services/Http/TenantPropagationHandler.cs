using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;

namespace Diten.Platform.Infrastructure.Services.Http;

/// <summary>
/// ⚠ THIS HANDLER DOES NOT PROPAGATE THE TENANT. MEASURED 2026-08-28. Do not read its presence on a client as
/// "tenancy travels on that client" — it does not, and never has.
///
/// <para><c>IHttpClientFactory</c> builds and CACHES each client's handler chain in its OWN scope. This handler
/// injects the request-scoped <see cref="ITenantContext"/>, so the instance it holds belongs to no request: it
/// answers <c>IsResolved == false</c>, the <c>if</c> below is never entered, no header is added, and nothing
/// anywhere says so. The unit tests that "cover" it register the context as a singleton, which proves the wiring
/// and not the lifetime — which is why this survived so long.</para>
///
/// <para>The working shape is to write the header from the CALLING class, which does live in the request scope:
/// see <c>RemoteWorkItemGateway</c> (where this was first measured and rejected), and
/// <c>MdmLegalEntityReferenceValidator</c> / <c>AuthServiceUserReferenceValidator</c>, which were moved off this
/// handler on 2026-08-28. Nothing on any of the three services now depends on it working.</para>
///
/// <para>Kept only so that removing a registration shared by Platform, AuthService and DevEnablementService is one
/// deliberate decision rather than a side effect of this round. Whether it is deleted, fixed
/// (<c>IHttpContextAccessor</c> read at send time instead of a constructor-injected context) or left is CONTROL
/// TOWER's call.</para>
/// </summary>
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
