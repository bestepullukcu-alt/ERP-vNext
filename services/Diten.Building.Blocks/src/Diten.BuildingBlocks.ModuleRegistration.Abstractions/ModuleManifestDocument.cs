namespace Diten.BuildingBlocks.ModuleRegistration.Abstractions;

/// <summary>
/// A module's self-registration manifest. The owning service pushes this to the Platform module-catalog at
/// startup. HARD (code-owned) identity — ModuleCode, page route/permissions, action permissions — is reconciled
/// on every push; SOFT metadata (Domain, Service, DisplayName, SortOrder, IsTenantAssignable, Icon) is seeded once
/// and thereafter owned by the operator (a re-push must NOT overwrite it).
/// </summary>
/// <param name="Icon">FIX-MODULE-ICON — the module's default sidebar icon as a boxicons class (e.g. "bx-cog").
/// SOFT: seeded once from this manifest, then operator-owned via the Module Catalog (re-push never overwrites).
/// Null when the module ships no default; the Platform stamps a "bx-box" fallback.</param>
/// <param name="IsBaseline">FEAT-BASELINE-MODULES — HARD (code-owned): when true, the module is entitlement-free —
/// every tenant automatically has access (the tenant entitlement check is bypassed). The per-user permission gate
/// (page RequiredPermission) still applies. Refreshed on every re-push.</param>
public sealed record ModuleManifestDocument(
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string Domain,
    string Service,
    string ModuleVersion,
    bool IsTenantAssignable,
    int SortOrder,
    IReadOnlyList<ModuleManifestPage> Pages,
    string? Icon = null,
    bool IsBaseline = false);

public sealed record ModuleManifestPage(
    string PageCode,
    string DisplayName,
    string RoutePath,
    string RequiredPermission,
    string? ParentPageCode,
    bool IsNavigationVisible,
    string PageType,
    int SortOrder,
    IReadOnlyList<ModuleManifestAction> Actions);

public sealed record ModuleManifestAction(
    string ActionCode,
    string DisplayName,
    string PermissionKey,
    string ActionType,
    int SortOrder,
    bool IsDangerous,
    bool IsToolbarAction,
    bool IsRowAction);
