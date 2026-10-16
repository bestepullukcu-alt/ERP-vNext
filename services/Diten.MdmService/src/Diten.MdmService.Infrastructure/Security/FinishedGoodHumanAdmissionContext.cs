using System.Security.Claims;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace Diten.MdmService.Infrastructure.Security;

public sealed class FinishedGoodHumanAdmissionContext : IFinishedGoodHumanAdmissionContext
{
    private const string ActorTypeClaim = "actor_type";
    private const string TenantClaim = "tenant_id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string SubmitPermission = "mdm.finished-goods.submit";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;

    public FinishedGoodHumanAdmissionContext(
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
    }

    public bool TryResolveSubmitter(out Guid tenantId, out Guid subjectId)
    {
        tenantId = Guid.Empty;
        subjectId = Guid.Empty;

        var context = _httpContextAccessor.HttpContext;
        var principal = context?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !HasSingleExactClaim(principal, ActorTypeClaim, "tenant_user")
            || !TryResolveSubject(principal, out var resolvedSubjectId)
            || !TryResolveTenant(principal, out var jwtTenantId)
            || !_tenantContext.IsResolved
            || _tenantContext.TenantId == Guid.Empty
            || _tenantContext.TenantId != jwtTenantId
            || !HeaderMatchesTenant(context!, jwtTenantId)
            || !HasSubmitPermission(principal))
        {
            return false;
        }

        tenantId = jwtTenantId;
        subjectId = resolvedSubjectId;
        return true;
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

    private static bool HasSubmitPermission(ClaimsPrincipal principal) =>
        principal.Claims
            .Where(claim => claim.Type is "permission" or "permissions")
            .SelectMany(claim => claim.Value.Split(
                [',', ' ', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(candidate => string.Equals(candidate, SubmitPermission, StringComparison.Ordinal));

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
}
