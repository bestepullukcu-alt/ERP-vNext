namespace Diten.Platform.API.Security;

public interface ITrustedLegalEntityScopeJwtContext
{
    Task<TrustedLegalEntityScopeJwtResult> ResolveAsync(HttpContext context);
}

public sealed record TrustedLegalEntityScopeJwtResult(
    bool Authenticated,
    bool Authorized,
    Guid? TenantId,
    Guid? SubjectId,
    IReadOnlySet<string> PermissionKeys)
{
    private static readonly IReadOnlySet<string> EmptyPermissions = new HashSet<string>(StringComparer.Ordinal);

    public static TrustedLegalEntityScopeJwtResult Unauthenticated { get; } =
        new(false, false, null, null, EmptyPermissions);

    public static TrustedLegalEntityScopeJwtResult Forbidden { get; } =
        new(true, false, null, null, EmptyPermissions);

    public bool HasExactPermission(string permissionKey) => PermissionKeys.Contains(permissionKey);
}
