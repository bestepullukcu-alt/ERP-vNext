using Diten.AuthService.Domain.Authorization;

namespace Diten.AuthService.Domain.Entities;

/// <summary>
/// İŞ3-FAZ0 — the authorization scope of a permission: the explicit signal that replaces Module-name classification
/// for the tenant-vs-platform escalation boundary. In Faz 0 it is DERIVED from the existing Module classification
/// (see <see cref="DefaultRolePermissionTemplate.ClassifyScope"/>) so the boundary stays bit-for-bit identical;
/// Module values are untouched. <c>Tenant</c> = 0 so legacy documents without the field deserialize to Tenant and
/// are then corrected by the idempotent startup Scope reconcile.
/// </summary>
public enum PermissionScope
{
    Tenant = 0,
    PlatformAdmin = 1
}

public sealed class Permission : GlobalEntityBase
{
    private Permission() { }

    // moduleOverride: FIX-PERM-MODULE-ATTRIBUTION — lets a permission's own Key stay stable
    // (module.resource.action, derived from the module/resource/action namespace it was originally
    // seeded under) while its Module attribution (used for UI grouping/filtering) points at the module
    // that actually owns the feature. Defaults to module, i.e. unaffected for every other permission.
    // scope: İŞ3-FAZ0 — optional explicit authz scope. When omitted it is DERIVED from the (effective) Module via
    // the Faz-0 transition classifier, so every existing construction site (incl. tests) keeps the exact same
    // tenant/platform classification it had under the old Module-name logic. moduleOverride is unchanged (Faz 2 removes it).
    public Permission(string module, string resource, string action, string displayName, string? description, string? moduleOverride = null, PermissionScope? scope = null)
    {
        Module = moduleOverride ?? module;
        Resource = resource;
        Action = action;
        Key = $"{module}.{resource}.{action}".ToLowerInvariant();
        DisplayName = displayName;
        Description = description;
        IsSystem = true;
        Scope = scope ?? DefaultRolePermissionTemplate.ClassifyScope(Module);
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Module { get; private set; } = string.Empty;
    public string Resource { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }

    /// <summary>İŞ3-FAZ0 — authz scope (tenant vs platform-admin). Faz 0: derived from Module, persisted, round-trips.</summary>
    public PermissionScope Scope { get; private set; }

    /// <summary>İŞ3-FAZ0 — reconcile mutator: backfills/corrects Scope on existing rows (startup idempotent reconcile).</summary>
    public void SetScope(PermissionScope scope) => Scope = scope;

    /// <summary>
    /// İŞ3-FAZ1b — refreshes the Module attribution WITHOUT touching the immutable Key (module.resource.action).
    /// Used by the catalog sync to align an existing permission's Module to the manifest ModuleCode.
    /// </summary>
    public void SetModule(string module) => Module = string.IsNullOrWhiteSpace(module) ? Module : module.Trim();

    public void MarkAsUserDefined() => IsSystem = false;
    
    public void Update(string displayName, string? description)
    {
        DisplayName = displayName;
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
