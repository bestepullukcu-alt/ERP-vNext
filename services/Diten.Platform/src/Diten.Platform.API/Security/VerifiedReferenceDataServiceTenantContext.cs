using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Diten.Platform.API.Security;

public interface IVerifiedReferenceDataServiceTenantContext
{
    Task<VerifiedGskuResolverJwtTenantResult> ResolveAsync(HttpContext httpContext);
}

public sealed class VerifiedReferenceDataServiceTenantContext : IVerifiedReferenceDataServiceTenantContext
{
    public async Task<VerifiedGskuResolverJwtTenantResult> ResolveAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            return VerifiedGskuResolverJwtTenantResult.Forbidden;
        }

        var authorization = httpContext.Request.Headers.Authorization;
        if (authorization.Count > 1
            || authorization.Count == 1
            && !TrustedServiceTokenValidationExtensions.TryReadExactBearer(authorization[0], out _))
        {
            return VerifiedGskuResolverJwtTenantResult.Forbidden;
        }

        var authentication = await httpContext.AuthenticateAsync(
            TrustedServiceTokenValidationExtensions.ReferenceDataAuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal?.Identity?.IsAuthenticated != true)
        {
            return VerifiedGskuResolverJwtTenantResult.Unauthenticated;
        }

        var principal = authentication.Principal;
        var actorTypes = ExactValues(principal, "actor_type");
        var serviceNames = ExactValues(principal, "service_name");
        var audiences = ExactValues(principal, "aud");
        var tenantClaims = ExactValues(principal, "tenant_id");
        if (actorTypes.Count != 1
            || !string.Equals(actorTypes[0], "service", StringComparison.Ordinal)
            || serviceNames.Count != 1
            || !string.Equals(
                serviceNames[0],
                TrustedServiceTokenValidationExtensions.RequiredServiceName,
                StringComparison.Ordinal)
            || audiences.Count != 1
            || !string.Equals(
                audiences[0],
                TrustedServiceTokenValidationExtensions.ReferenceDataRequiredAudience,
                StringComparison.Ordinal)
            || tenantClaims.Count != 1
            || !Guid.TryParseExact(tenantClaims[0], "D", out var tenantId)
            || tenantId == Guid.Empty)
        {
            return VerifiedGskuResolverJwtTenantResult.Forbidden;
        }

        return new VerifiedGskuResolverJwtTenantResult(true, true, tenantId);
    }

    private static IReadOnlyList<string> ExactValues(ClaimsPrincipal principal, string claimType) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToList();
}
