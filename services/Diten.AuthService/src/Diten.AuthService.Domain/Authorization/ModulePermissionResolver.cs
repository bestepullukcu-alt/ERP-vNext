using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// Maps a Platform entitlement <c>ModuleCode</c> (e.g. <c>"MDM"</c>) to the AuthService permissions
/// of that module (e.g. the <c>mdm.*</c> catalog entries). This is the explicit, testable form of
/// the previously-implicit convention (the DataSeeder string-matched <c>Permission.Module</c>).
///
/// <para>
/// Decision (S2): <b>convention-first + override map</b>.
/// <list type="bullet">
/// <item>Convention: <c>normalize(ModuleCode) == Permission.Module</c>, where normalize = trim +
/// lowercase (matching is case-insensitive). <c>"MDM" → "mdm"</c>.</item>
/// <item>Deviations: a small explicit <see cref="ModuleCodeOverrides"/> map takes precedence over the
/// convention. It is intentionally <b>empty by default</b>; entries are added only on confirmed
/// evidence (EA / module pack), never guessed.</item>
/// </list>
/// </para>
///
/// <para>
/// Platform permissions are never returned — consistent with
/// <see cref="DefaultRolePermissionTemplate.IsPlatform"/> (tenant privilege-escalation boundary).
/// An unmatched, empty or platform-resolving <c>ModuleCode</c> yields an empty set (fail-safe; never
/// throws). Pure/stateless so the S3 entitlement consumer can reuse it directly.
/// </para>
/// </summary>
public static class ModulePermissionResolver
{
    public const string PlatformModule = DefaultRolePermissionTemplate.PlatformModule;

    /// <summary>
    /// Explicit normalized-ModuleCode → Permission.Module overrides for cases the convention does not
    /// cover. Empty until a deviation is confirmed; keyed case-insensitively.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ModuleCodeOverrides =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Curated allow-list of tenant-scoped modules HOSTED inside Diten.Platform, whose permissions therefore live
    /// under the <c>platform.&lt;module&gt;.*</c> namespace (e.g. <c>workflow</c> → <c>platform.workflow.*</c>).
    /// These are tenant features — NOT the platform-admin umbrella — so an entitled tenant role MAY receive them;
    /// everything else under <c>platform.*</c> stays blocked by the escalation boundary. Add a code here ONLY on
    /// confirmed evidence that the module is a tenant-scoped, platform-hosted product (never guessed).
    /// </summary>
    public static readonly IReadOnlySet<string> PlatformHostedTenantModules =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "workflow" };

    /// <summary>Trim + lowercase; null/blank → empty string.</summary>
    public static string NormalizeModuleCode(string? moduleCode)
        => string.IsNullOrWhiteSpace(moduleCode) ? string.Empty : moduleCode.Trim().ToLowerInvariant();

    /// <summary>
    /// Resolves the permission-module name for a <c>ModuleCode</c> (override first, else convention).
    /// Returns empty string for null/blank input.
    /// </summary>
    public static string ResolvePermissionModule(string? moduleCode, IReadOnlyDictionary<string, string>? overrides = null)
    {
        var normalized = NormalizeModuleCode(moduleCode);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var map = overrides ?? ModuleCodeOverrides;
        return map.TryGetValue(normalized, out var mapped) ? mapped : normalized;
    }

    /// <summary>
    /// Returns the catalog permissions belonging to the given <c>ModuleCode</c>'s module. Deleted and
    /// platform permissions are excluded. An unmatched/empty/platform code yields an empty set.
    /// </summary>
    public static IReadOnlyList<Permission> ResolvePermissions(
        string? moduleCode,
        IEnumerable<Permission> catalog,
        IReadOnlyDictionary<string, string>? overrides = null)
    {
        // Curated exception (tenant-scoped, platform-hosted module): resolve its platform.<module>.* permissions
        // even though the broad platform.* exclusion below would otherwise block them.
        var normalized = NormalizeModuleCode(moduleCode);
        if (PlatformHostedTenantModules.Contains(normalized))
        {
            return catalog
                .Where(p => !p.IsDeleted
                            && string.Equals(p.Module, PlatformModule, StringComparison.OrdinalIgnoreCase)
                            && (string.Equals(p.Resource, normalized, StringComparison.OrdinalIgnoreCase)
                                || p.Resource.StartsWith(normalized + ".", StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var module = ResolvePermissionModule(moduleCode, overrides);
        if (module.Length == 0 || string.Equals(module, PlatformModule, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<Permission>();
        }

        return catalog
            .Where(p => !p.IsDeleted
                        && !DefaultRolePermissionTemplate.IsPlatform(p)
                        && string.Equals(p.Module, module, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
