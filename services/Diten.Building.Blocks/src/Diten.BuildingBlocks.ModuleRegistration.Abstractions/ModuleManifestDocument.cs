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
    bool IsBaseline = false,
    // MOD-0027-FU03 — Notification Event Catalog. ADDITIVE + OPTIONAL, appended LAST so existing named/positional
    // callers and existing serialized payloads are unaffected (default null → old JSON deserializes with no field,
    // no exception). Consumers MUST coalesce null → empty (?? Array.Empty<ModuleManifestNotificationEvent>()).
    // Do not reorder/retype existing params; do not remove Icon/IsBaseline.
    IReadOnlyList<ModuleManifestNotificationEvent>? NotificationEvents = null);

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

/// <summary>
/// MOD-0027-FU03 — a producer module's declaration that it emits a notification event bound to a template slot.
/// TOLERANT/default-heavy by design: only EventCode + Channel + DefaultTemplateKey are required identity; every other
/// field is optional with a safe default so partial/older declarations deserialize without throwing. The Notification
/// Event Catalog validates OwnerModuleId/TargetPageCode/RequiredPermissionKey against the Module Catalog / ModulePages /
/// Permission Catalog at sync time; unresolved references keep the event Draft with a validation issue.
/// </summary>
public sealed record ModuleManifestNotificationEvent(
    string EventCode,
    string Channel,
    string DefaultTemplateKey,
    string? DisplayNameKey = null,
    string? FallbackDisplayName = null,
    string? Description = null,
    IReadOnlyList<ModuleManifestNotificationVariable>? RequiredVariables = null,
    IReadOnlyList<ModuleManifestNotificationVariable>? OptionalVariables = null,
    string? TargetPageCode = null,
    string? TargetRouteDescriptorId = null,
    string? RequiredPermissionKey = null,
    bool CanTenantOverride = false,
    string UsageType = "SystemEvent",
    string? SeverityDefault = null,
    string? LinkPolicy = null,
    string Status = "Draft");

public sealed record ModuleManifestNotificationVariable(
    string Name,
    string Type = "String",
    bool IsRequired = true);
