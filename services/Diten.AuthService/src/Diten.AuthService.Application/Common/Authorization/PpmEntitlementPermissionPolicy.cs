namespace Diten.AuthService.Application.Common.Authorization;

public interface IPpmEntitlementPermissionPolicy
{
    bool Applies(string? moduleCode);
    bool AppliesToPermission(string? permissionKey);
    bool IsCanonicalPermission(string? permissionKey);
}

public sealed class PpmEntitlementPermissionPolicy : IPpmEntitlementPermissionPolicy
{
    public const string ModuleCode = "PPM";
    private static readonly HashSet<string> CanonicalPermissions =
        PpmPermissionCatalog.All.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool Applies(string? moduleCode) =>
        string.Equals(moduleCode, ModuleCode, StringComparison.Ordinal);

    public bool AppliesToPermission(string? permissionKey) =>
        !string.IsNullOrWhiteSpace(permissionKey)
        && permissionKey.StartsWith("ppm.", StringComparison.OrdinalIgnoreCase);

    public bool IsCanonicalPermission(string? permissionKey) =>
        !string.IsNullOrWhiteSpace(permissionKey)
        && CanonicalPermissions.Contains(permissionKey);
}
