namespace Diten.BuildingBlocks.ModuleRegistration.Abstractions;

/// <summary>
/// A module's self-registration manifest. The owning service pushes this to the Platform module-catalog at
/// startup. HARD (code-owned) identity — ModuleCode, page route/permissions, action permissions — is reconciled
/// on every push; SOFT metadata (Domain, Service, DisplayName, SortOrder, IsTenantAssignable) is seeded once and
/// thereafter owned by the operator (a re-push must NOT overwrite it).
/// </summary>
public sealed record ModuleManifestDocument(
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string Domain,
    string Service,
    string ModuleVersion,
    bool IsTenantAssignable,
    int SortOrder,
    IReadOnlyList<ModuleManifestPage> Pages);

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
