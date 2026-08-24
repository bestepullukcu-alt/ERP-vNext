using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Diten.Platform.API.Security;

public interface IVerifiedGskuResolverJwtTenantContext
{
    Task<VerifiedGskuResolverJwtTenantResult> ResolveAsync(HttpContext httpContext);
}

public sealed record VerifiedGskuResolverJwtTenantResult(bool IsAuthenticated, bool IsAuthorized, Guid? TenantId)
{
    public static VerifiedGskuResolverJwtTenantResult Unauthenticated { get; } = new(false, false, null);
    public static VerifiedGskuResolverJwtTenantResult Forbidden { get; } = new(true, false, null);
}

public sealed class VerifiedGskuResolverJwtTenantContext : IVerifiedGskuResolverJwtTenantContext
{
    public async Task<VerifiedGskuResolverJwtTenantResult> ResolveAsync(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            return VerifiedGskuResolverJwtTenantResult.Forbidden;
        }

        var authentication = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal?.Identity?.IsAuthenticated != true)
        {
            return VerifiedGskuResolverJwtTenantResult.Unauthenticated;
        }

        var principal = authentication.Principal;
        var actorTypes = ClaimValues(principal, "actor_type");
        var tenantClaims = ClaimValues(principal, "tenant_id");
        if (actorTypes.Count != 1
            || !string.Equals(actorTypes[0], "tenant_user", StringComparison.Ordinal)
            || tenantClaims.Count != 1
            || !Guid.TryParse(tenantClaims[0], out var tenantId)
            || tenantId == Guid.Empty)
        {
            return VerifiedGskuResolverJwtTenantResult.Forbidden;
        }

        return new(true, true, tenantId);
    }

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string claimType) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToList();
}
