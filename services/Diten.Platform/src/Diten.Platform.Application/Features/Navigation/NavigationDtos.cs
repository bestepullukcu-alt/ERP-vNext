namespace Diten.Platform.Application.Features.Navigation;

/// <summary>
/// MOD-0285 — one entitled module's navigation group. Items are the module's platform-scope
/// (TenantId=Guid.Empty) page descriptors that are navigation-visible and Active, ordered by SortOrder.
/// The frontend applies the per-item permission filter (Perms.Has) and parent/child nesting.
/// <para>FIX-3 — <see cref="Domain"/> (catalog code) + <see cref="DomainDisplayName"/> (resolved from
/// platform_module_domains, falling back to the code) let the menu group modules by DOMAIN, data-driven.</para>
/// </summary>
// FEAT-NAVPREFS-DOMAINS — DomainSortOrder is the effective order of this module's DOMAIN group (tenant
// domain-preference override, else the implicit catalog rank). The frontend orders domain groups by it; all
// modules of the same domain carry the same value. Defaulted so existing construction stays valid.
public sealed record NavigationModuleGroupDto(
    string ModuleCode,
    string ModuleDisplayName,
    string Domain,
    string DomainDisplayName,
    IReadOnlyList<NavigationMenuItemDto> Items,
    int DomainSortOrder = 0,
    // FIX-MODULE-ICON — the module's sidebar icon (boxicons class), resolved from the catalog with a "bx-box"
    // fallback. Defaulted so existing construction/`with` expressions stay valid.
    string? Icon = null,
    // FIX-CTRLK-GROUPING+CREATE — a NAVIGABLE create route for this module (a page whose route ends with "/Create"
    // and carries no {id} placeholder — i.e. a compact-module "Add New" route). Null for slim/offcanvas modules
    // (no standalone create URL). CreatePermission is that create page's RequiredPermission (per-user gate).
    string? CreateRoute = null,
    string? CreatePermission = null);

public sealed record NavigationMenuItemDto(
    string PageCode,
    string DisplayName,
    string RoutePath,
    string? RequiredPermission,
    string? ParentPageCode,
    string? IconHint,
    int SortOrder);
