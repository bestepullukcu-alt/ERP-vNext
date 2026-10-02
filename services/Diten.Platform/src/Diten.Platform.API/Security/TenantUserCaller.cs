using System.Security.Claims;

namespace Diten.Platform.API.Security;

/// <summary>
/// WP-BRD-TENANT-CRM-SETS — the caller's tenant on a route that <c>TenantResolutionMiddleware</c> does NOT resolve.
///
/// <para><b>Why it is needed.</b> <c>/api/lookups/*</c> is a bypass path of the tenant middleware, so the ambient
/// <c>ITenantContext</c> is never set there. A lookup that reads tenant data must therefore resolve the tenant itself —
/// and it must do so from the VALIDATED token, not from a header the client can write.</para>
///
/// <para><b>The rule.</b> The principal (already authenticated by <c>[Authorize]</c>) must carry exactly one
/// <c>actor_type</c> = <c>tenant_user</c> and exactly one non-empty <c>tenant_id</c>. A platform actor, or a token with no
/// tenant, has no tenant to read for (<c>tenant_context_required</c>). An <c>X-Tenant-Id</c> header is not a source: it is
/// only checked, and one naming a DIFFERENT tenant than the token is refused (<c>tenant_mismatch</c>) — the same
/// contradiction rule the middleware applies on tenant routes (BL-324).</para>
/// </summary>
public static class TenantUserCaller
{
    public const string TenantHeader = "X-Tenant-Id";
    public const string TenantContextRequired = "tenant_context_required";
    public const string TenantMismatch = "tenant_mismatch";

    public readonly record struct Resolution(Guid? TenantId, string? Refusal);

    public static Resolution Resolve(ClaimsPrincipal? user, IHeaderDictionary headers)
    {
        if (user?.Identities.Any(identity => identity.IsAuthenticated) != true)
        {
            return new Resolution(null, TenantContextRequired);
        }

        var actorTypes = ClaimValues(user, "actor_type");
        var tenantClaims = ClaimValues(user, "tenant_id");
        if (actorTypes.Count != 1
            || !string.Equals(actorTypes[0], "tenant_user", StringComparison.OrdinalIgnoreCase)
            || tenantClaims.Count != 1
            || !Guid.TryParse(tenantClaims[0], out var tenantId)
            || tenantId == Guid.Empty)
        {
            return new Resolution(null, TenantContextRequired);
        }

        if (headers.TryGetValue(TenantHeader, out var header)
            && Guid.TryParse(header.ToString(), out var headerTenant)
            && headerTenant != tenantId)
        {
            return new Resolution(null, TenantMismatch);
        }

        return new Resolution(tenantId, null);
    }

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string claimType) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToList();
}
