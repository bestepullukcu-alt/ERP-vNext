using System.Security.Claims;
using Diten.MdmService.Application.Contracts;
using Microsoft.AspNetCore.Http;

namespace Diten.MdmService.Infrastructure.Security;

public sealed class ProductIdentityLifecycleActorContext : IProductIdentityLifecycleActorContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProductIdentityLifecycleActorContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool TryResolveCanonicalHumanSubject(out Guid subjectId)
    {
        subjectId = Guid.Empty;
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !IsHumanActorType(SingleClaim(principal, "actor_type")))
        {
            return false;
        }

        var subjects = ClaimValues(principal, "sub");
        var nameIdentifiers = ClaimValues(principal, ClaimTypes.NameIdentifier);
        if (subjects.Count > 1 || nameIdentifiers.Count > 1
            || subjects.Count == 0 && nameIdentifiers.Count == 0
            || !TryGuid(subjects, out var subject)
            || !TryGuid(nameIdentifiers, out var nameIdentifier)
            || subject.HasValue && nameIdentifier.HasValue && subject != nameIdentifier)
        {
            return false;
        }

        subjectId = subject ?? nameIdentifier ?? Guid.Empty;
        return subjectId != Guid.Empty;
    }

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var principal = _httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && principal.Claims
                .Where(claim => string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase))
                .SelectMany(claim => claim.Value.Split(
                    [',', ' ', ';'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Any(candidate => string.Equals(candidate, permission, StringComparison.OrdinalIgnoreCase));
    }

    private static string? SingleClaim(ClaimsPrincipal principal, string type)
    {
        var values = ClaimValues(principal, type);
        return values.Count == 1 ? values[0] : null;
    }

    private static bool IsHumanActorType(string? actorType) => actorType is
        "tenant_user" or "platform_admin" or "partner_admin";

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string type) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();

    private static bool TryGuid(IReadOnlyList<string> values, out Guid? result)
    {
        result = null;
        if (values.Count == 0)
        {
            return true;
        }

        if (!Guid.TryParseExact(values[0], "D", out var parsed) || parsed == Guid.Empty)
        {
            return false;
        }

        result = parsed;
        return true;
    }
}
