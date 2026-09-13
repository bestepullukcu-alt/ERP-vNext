using System.Security.Claims;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;

namespace Diten.MdmService.Infrastructure.Security;

public sealed class ProductLegalEntityScopeWriterAuthorityProvider
    : IProductLegalEntityScopeWriterAuthorityProvider
{
    private const string ActorTypeClaim = "actor_type";
    private const string TenantClaim = "tenant_id";
    private const string TenantHeader = "X-Tenant-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;

    public ProductLegalEntityScopeWriterAuthorityProvider(
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
    }

    public Task<ProductLegalEntityScopeVerifiedWriterAuthority?> ResolveForegroundReplaceAsync(
        Guid aggregateId,
        ProductLegalEntityScopeMutationIdentity mutation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = _httpContextAccessor.HttpContext;
        var principal = context?.User;
        if (aggregateId == Guid.Empty
            || mutation is null
            || mutation.CommandId == Guid.Empty
            || mutation.EnforcedGlobalProductCreateProhibited
            || !string.Equals(
                mutation.Kind,
                nameof(ReplaceProductLegalEntityScopePolicyCommand),
                StringComparison.Ordinal)
            || !IsCanonicalSha256(mutation.PayloadFingerprint)
            || principal?.Identity?.IsAuthenticated != true
            || !HasSingleExactClaim(principal, ActorTypeClaim, "tenant_user")
            || !TryResolveSubject(principal, out var subjectId)
            || !TryResolveTenant(principal, out var tenantId)
            || !_tenantContext.IsResolved
            || _tenantContext.TenantId == Guid.Empty
            || _tenantContext.TenantId != tenantId
            || !HeaderMatchesTenant(context!, tenantId)
            || !HasSingleExactPermission(principal))
        {
            return Task.FromResult<ProductLegalEntityScopeVerifiedWriterAuthority?>(null);
        }

        return Task.FromResult<ProductLegalEntityScopeVerifiedWriterAuthority?>(
            ProductLegalEntityScopeVerifiedWriterAuthority.IssueForegroundReplace(
                tenantId,
                subjectId,
                mutation.CommandId,
                aggregateId,
                mutation.Kind,
                mutation.PayloadFingerprint));
    }

    private static bool TryResolveSubject(ClaimsPrincipal principal, out Guid subjectId)
    {
        subjectId = Guid.Empty;
        var subjects = ClaimValues(principal, "sub");
        var nameIdentifiers = ClaimValues(principal, ClaimTypes.NameIdentifier);
        if (subjects.Count > 1
            || nameIdentifiers.Count > 1
            || subjects.Count == 0 && nameIdentifiers.Count == 0
            || !TryCanonicalGuid(subjects, out var subject)
            || !TryCanonicalGuid(nameIdentifiers, out var nameIdentifier)
            || subject.HasValue && nameIdentifier.HasValue && subject != nameIdentifier)
        {
            return false;
        }

        subjectId = subject ?? nameIdentifier ?? Guid.Empty;
        return subjectId != Guid.Empty;
    }

    private static bool TryResolveTenant(ClaimsPrincipal principal, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var tenants = ClaimValues(principal, TenantClaim);
        return tenants.Count == 1
            && TryCanonicalGuid(tenants[0], out tenantId);
    }

    private static bool HeaderMatchesTenant(HttpContext context, Guid tenantId)
    {
        if (!context.Request.Headers.TryGetValue(TenantHeader, out var headerValues))
        {
            return true;
        }

        var header = headerValues.Count == 1 ? headerValues[0] : null;
        return !string.IsNullOrEmpty(header)
            && !header.Contains(',')
            && TryCanonicalGuid(header, out var headerTenantId)
            && headerTenantId == tenantId;
    }

    private static bool HasSingleExactPermission(ClaimsPrincipal principal)
    {
        var candidates = principal.Claims
            .Where(claim => claim.Type is "permission" or "permissions")
            .SelectMany(claim => claim.Value.Split(
                [',', ' ', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
        return candidates.Count(candidate => string.Equals(
            candidate,
            ProductLegalEntityScopeVerifiedWriterAuthority.ForegroundReplacePermission,
            StringComparison.Ordinal)) == 1;
    }

    private static bool HasSingleExactClaim(ClaimsPrincipal principal, string type, string value)
    {
        var values = ClaimValues(principal, type);
        return values.Count == 1
            && string.Equals(values[0], value, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string type) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();

    private static bool TryCanonicalGuid(IReadOnlyList<string> values, out Guid? result)
    {
        result = null;
        if (values.Count == 0)
        {
            return true;
        }

        if (!TryCanonicalGuid(values[0], out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryCanonicalGuid(string value, out Guid result)
    {
        result = Guid.Empty;
        if (!Guid.TryParseExact(value, "D", out var parsed)
            || parsed == Guid.Empty
            || !string.Equals(value, parsed.ToString("D"), StringComparison.Ordinal))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
}
