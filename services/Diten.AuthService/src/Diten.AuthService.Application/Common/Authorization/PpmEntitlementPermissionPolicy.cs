namespace Diten.AuthService.Application.Common.Authorization;

public interface IPpmEntitlementPermissionPolicy
{
    bool Applies(string? moduleCode);
    bool IsPpmModuleCodeAnyCase(string? moduleCode);
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

    // BL-360 — narrow, case-insensitive PPM module-code recognizer for the entitlement-sync grant/revoke
    // gates only. Deliberately separate from Applies (Ordinal "PPM"), which stays exact-case for the
    // resolver's canonical-module acceptance; broadening Applies itself would change resolver behavior.
    public bool IsPpmModuleCodeAnyCase(string? moduleCode) =>
        // Trim as well: the sync service hands this the normalized code, but a recognizer that says "any case"
        // must not be defeated by padding if a future caller passes the raw code.
        string.Equals(moduleCode?.Trim(), ModuleCode, StringComparison.OrdinalIgnoreCase);

    public bool AppliesToPermission(string? permissionKey) =>
        !string.IsNullOrWhiteSpace(permissionKey)
        && permissionKey.StartsWith("ppm.", StringComparison.OrdinalIgnoreCase);

    public bool IsCanonicalPermission(string? permissionKey) =>
        !string.IsNullOrWhiteSpace(permissionKey)
        && CanonicalPermissions.Contains(permissionKey);
}
