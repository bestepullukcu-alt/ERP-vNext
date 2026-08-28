using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Diten.Platform.API.Security;

public sealed class TrustedSourceAuditIntentServiceIdentity : ITrustedSourceAuditIntentServiceIdentity
{
    public const string AuthenticationScheme = "TrustedSourceAuditIntent";

    public async Task<ITrustedSourceAuditIntentServiceIdentity.IdentityResult> ResolveAsync(
        HttpContext httpContext,
        IReadOnlySet<Guid> allowedTenantIds)
    {
        if (httpContext.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            return ITrustedSourceAuditIntentServiceIdentity.IdentityResult.Forbidden;
        }

        var authentication = await httpContext.AuthenticateAsync(AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal?.Identity?.IsAuthenticated != true)
        {
            return ITrustedSourceAuditIntentServiceIdentity.IdentityResult.Unauthenticated;
        }

        var principal = authentication.Principal;
        var actorTypes = ClaimValues(principal, "actor_type");
        var serviceNames = ClaimValues(principal, "service_name");
        var tenantClaims = ClaimValues(principal, "tenant_id");
        var audiences = ClaimValues(principal, "aud");
        if (actorTypes.Count != 1
            || !string.Equals(actorTypes[0], "service", StringComparison.Ordinal)
            || serviceNames.Count != 1
            || !string.Equals(serviceNames[0], TrustedSourceAuditIntentCredentialAuthenticator.ConsumerService, StringComparison.Ordinal)
            || tenantClaims.Count != 1
            || !Guid.TryParseExact(tenantClaims[0], "D", out var tenantId)
            || tenantId == Guid.Empty
            || audiences.Count != 1
            || !string.Equals(audiences[0], TrustedSourceAuditIntentCredentialAuthenticator.Audience, StringComparison.Ordinal)
            || !allowedTenantIds.Contains(tenantId))
        {
            return ITrustedSourceAuditIntentServiceIdentity.IdentityResult.Forbidden;
        }

        return new(true, true, tenantId);
    }

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string claimType) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToList();
}
