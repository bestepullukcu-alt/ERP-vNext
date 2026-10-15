using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Diten.Platform.API.Security;

public sealed class TrustedLegalEntityScopeJwtContext : ITrustedLegalEntityScopeJwtContext
{
    public async Task<TrustedLegalEntityScopeJwtResult> ResolveAsync(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey("X-Tenant-Id")
            || context.Request.Headers.ContainsKey("X-Legal-Entity-Id")
            || context.Request.Headers.ContainsKey("X-Legal-Entity-Ids"))
        {
            return TrustedLegalEntityScopeJwtResult.Forbidden;
        }

        var authentication = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal?.Identity?.IsAuthenticated != true)
        {
            return TrustedLegalEntityScopeJwtResult.Unauthenticated;
        }

        var principal = authentication.Principal;
        var tenants = ClaimValues(principal, "tenant_id");
        var subjects = ClaimValues(principal, "sub");
        var actors = ClaimValues(principal, "actor_type");
        if (tenants.Count != 1
            || subjects.Count != 1
            || actors.Count != 1
            || !string.Equals(actors[0], "tenant_user", StringComparison.Ordinal)
            || !Guid.TryParse(tenants[0], out var tenantId)
            || tenantId == Guid.Empty
            || !Guid.TryParse(subjects[0], out var subjectId)
            || subjectId == Guid.Empty)
        {
            return TrustedLegalEntityScopeJwtResult.Forbidden;
        }

        var permissions = ClaimValues(principal, "permission").ToHashSet(StringComparer.Ordinal);
        return new TrustedLegalEntityScopeJwtResult(true, true, tenantId, subjectId, permissions);
    }

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string claimType) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();
}
