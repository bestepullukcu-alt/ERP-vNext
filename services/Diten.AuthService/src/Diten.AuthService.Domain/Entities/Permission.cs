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
        // FIX-RBAC-PERM-MODULE-ATTRIBUTION — the Module attribution and the Scope basis are now two DIFFERENT
        // values, and keeping them apart is the whole point of this change:
        //
        //   Module      = the module that owns the feature (grouping / filtering on Role Permissions). When no
        //                 explicit attribution is given it is DERIVED, so a key minted under a SERVICE namespace
        //                 ("platform") is attributed to the module in its second segment instead of the service.
        //   Scope       = the tenant/platform-admin escalation boundary. It is classified from the attribution
        //                 this permission had BEFORE the derivation (`moduleOverride ?? module`), so wiring the
        //                 derivation in changes ZERO permissions' Scope — a derived Module can never widen who
        //                 may hold the permission.
        //
        // Deleting the legacyAttribution line and classifying from the derived Module would silently downgrade
        // every platform.* key to Tenant scope. PermissionScopePreservationTests fails if that happens.
        var legacyAttribution = moduleOverride ?? module;

        // FIX-PERM-ACTION-SPELLING — the Key is computed FIRST and from the RAW arguments, because ADR-001 §1
        // froze it: "Platform.BusinessReferenceData.Version.PublishOverride" must keep resolving to
        // platform.businessreferencedata.version.publishoverride, whatever the stored segments end up spelling.
        // Only the stored Resource/Action are normalized, so the catalog stops carrying the same verb four ways
        // (PascalCase from the BRD seed, snake_case from MOD-0251, kebab everywhere else) while every
        // [HasPermission] attribute in the repository stays untouched.
        Key = $"{module}.{resource}.{action}".ToLowerInvariant();

        // Module derivation reads the RAW resource on purpose — it is the pre-existing behaviour, and feeding it
        // the normalized form would change the derived head for a PascalCase resource
        // ("businessreferencedata" → "business-reference-data") and silently re-group permissions.
        Module = moduleOverride ?? PermissionModuleAttribution.Derive(module, resource);
        Resource = PermissionSegmentNormalizer.Normalize(resource);
        Action = PermissionSegmentNormalizer.Normalize(action);
        DisplayName = displayName;
        Description = description;
        IsSystem = true;
        Scope = scope ?? DefaultRolePermissionTemplate.ClassifyScope(legacyAttribution);
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
