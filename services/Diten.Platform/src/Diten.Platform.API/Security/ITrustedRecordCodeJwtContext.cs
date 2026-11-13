using System.Security.Claims;

namespace Diten.Platform.API.Security;

public interface ITrustedRecordCodeJwtContext
{
    Task<TrustedRecordCodeJwtResult> ResolveAsync(HttpContext context);
}

/// <summary>
/// The forwarded user token of a record-code request. <see cref="Principal"/> is the validated principal whenever the token
/// validated — also when it is refused for its claims — so the executor can make it the request's user.
/// <see cref="ActorType"/> and <see cref="TenantClaim"/> are the token's single <c>actor_type</c> / <c>tenant_id</c> values
/// (null when absent or repeated).
/// </summary>
public sealed record TrustedRecordCodeJwtResult(
    bool Authenticated,
    bool Authorized,
    ClaimsPrincipal? Principal,
    Guid? TenantId,
    Guid? SubjectId,
    string? ActorType,
    string? TenantClaim,
    IReadOnlySet<string> PermissionKeys)
{
    private static readonly IReadOnlySet<string> EmptyPermissions = new HashSet<string>(StringComparer.Ordinal);

    public static TrustedRecordCodeJwtResult Unauthenticated { get; } =
        new(false, false, null, null, null, null, null, EmptyPermissions);

    /// <summary>Refused before any token is read (an <c>X-Tenant-Id</c> header): authenticated in the FU21 sense, no principal.</summary>
    public static TrustedRecordCodeJwtResult Forbidden { get; } =
        new(true, false, null, null, null, null, null, EmptyPermissions);

    public static TrustedRecordCodeJwtResult ForbiddenFor(ClaimsPrincipal principal, string? actorType, string? tenantClaim) =>
        new(true, false, principal, null, null, actorType, tenantClaim, EmptyPermissions);

    /// <summary>Exact, case-sensitive, one <c>permission</c> claim per key — never <c>PermissionClaimEvaluator</c> (pack §8.5 step 8).</summary>
    public bool HasExactPermission(string permissionKey) => PermissionKeys.Contains(permissionKey);
}
