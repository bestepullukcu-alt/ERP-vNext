using System.Security.Claims;

namespace Diten.Platform.Common.Authorization;

/// <summary>
/// Fail-closed, in-process evaluation of permission claims from an already authenticated principal.
/// JWT validation remains the host application's responsibility.
/// </summary>
public sealed class SignedJwtPermissionClaimEvaluator : IPermissionClaimEvaluator
{
    public const string PermissionClaimType = "permission";
    public const string TenantClaimType = "tenant_id";
    public const string SubjectClaimType = "sub";

    public bool HasPermission(ClaimsPrincipal? principal, string? permission)
    {
        if (principal?.Identity?.IsAuthenticated != true
            || string.IsNullOrWhiteSpace(permission)
            || !HasNonEmptyGuidClaim(principal, TenantClaimType)
            || !HasValidSubject(principal))
        {
            return false;
        }

        return principal.Claims.Any(claim =>
            string.Equals(claim.Type, PermissionClaimType, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(claim.Value)
            && string.Equals(claim.Value, permission, StringComparison.Ordinal));
    }

    private static bool HasValidSubject(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(SubjectClaimType);
        return subject is not null
            ? IsNonEmptyGuid(subject.Value)
            : HasNonEmptyGuidClaim(principal, ClaimTypes.NameIdentifier);
    }

    private static bool HasNonEmptyGuidClaim(ClaimsPrincipal principal, string claimType)
        => IsNonEmptyGuid(principal.FindFirst(claimType)?.Value);

    private static bool IsNonEmptyGuid(string? value)
        => Guid.TryParse(value, out var parsed) && parsed != Guid.Empty;
}
