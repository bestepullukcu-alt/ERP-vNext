namespace Diten.AuthService.Application.Common.Authorization;

/// <summary>
/// Closed permission contract for MOD-0117. PPM identities are deliberately
/// ordinal: entitlement module codes are not permission-key aliases.
/// </summary>
public static class PpmPermissionCatalog
{
    public const string ModuleCode = "PPM";

    private static readonly string[] Resources =
    [
        "work-records",
        "tasks",
        "workstreams",
        "calendar",
        "effort-logs",
        "meeting-reports"
    ];

    private static readonly string[] Actions =
    [
        "read",
        "create",
        "update",
        "change-lifecycle"
    ];

    private static readonly string[] PermissionValues =
        Resources.SelectMany(resource => Actions.Select(action => $"ppm.{resource}.{action}")).ToArray();

    private static readonly HashSet<string> PermissionSet =
        new(PermissionValues, StringComparer.Ordinal);

    public static IReadOnlyList<string> Permissions { get; } = Array.AsReadOnly(PermissionValues);

    public static bool IsModuleCode(string? moduleCode) =>
        string.Equals(moduleCode, ModuleCode, StringComparison.Ordinal);

    public static bool Contains(string? permissionKey) =>
        permissionKey is not null && PermissionSet.Contains(permissionKey);
}
