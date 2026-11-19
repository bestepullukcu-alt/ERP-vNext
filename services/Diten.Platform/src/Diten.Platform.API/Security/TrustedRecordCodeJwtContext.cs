using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0040-FU02 §8.2 — the forwarded user token, validated EXPLICITLY under the JWT bearer scheme (FU21
/// <c>TrustedLegalEntityScopeJwtContext</c>). The tenant is only ever the token's <c>tenant_id</c>: a request carrying
/// <c>X-Tenant-Id</c> is refused before the token is read.
/// </summary>
public sealed class TrustedRecordCodeJwtContext : ITrustedRecordCodeJwtContext
{
    public const string TenantUserActorType = "tenant_user";

    public async Task<TrustedRecordCodeJwtResult> ResolveAsync(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            return TrustedRecordCodeJwtResult.Forbidden;
        }

        var authentication = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal?.Identity?.IsAuthenticated != true)
        {
            return TrustedRecordCodeJwtResult.Unauthenticated;
        }

        var principal = authentication.Principal;
        var tenants = ClaimValues(principal, "tenant_id");
        var subjects = ClaimValues(principal, "sub");
        var actors = ClaimValues(principal, "actor_type");
        var actorType = actors.Count == 1 ? actors[0] : null;
        var tenantClaim = tenants.Count == 1 ? tenants[0] : null;
        if (tenants.Count != 1
            || subjects.Count != 1
            || actors.Count != 1
            || !string.Equals(actors[0], TenantUserActorType, StringComparison.Ordinal)
            || !Guid.TryParse(tenants[0], out var tenantId)
            || tenantId == Guid.Empty
            || !Guid.TryParse(subjects[0], out var subjectId)
            || subjectId == Guid.Empty)
        {
            return TrustedRecordCodeJwtResult.ForbiddenFor(principal, actorType, tenantClaim);
        }

        var permissions = ClaimValues(principal, "permission").ToHashSet(StringComparer.Ordinal);
        return new TrustedRecordCodeJwtResult(true, true, principal, tenantId, subjectId, actorType, tenantClaim, permissions);
    }

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string claimType) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();
}
