namespace Diten.Platform.Application.Features.Navigation;

/// <summary>
/// MOD-0285 — one entitled module's navigation group. Items are the module's platform-scope
/// (TenantId=Guid.Empty) page descriptors that are navigation-visible and Active, ordered by SortOrder.
/// The frontend applies the per-item permission filter (Perms.Has) and parent/child nesting.
/// <para>FIX-3 — <see cref="Domain"/> (catalog code) + <see cref="DomainDisplayName"/> (resolved from
/// platform_module_domains, falling back to the code) let the menu group modules by DOMAIN, data-driven.</para>
/// </summary>
public sealed record NavigationModuleGroupDto(
    string ModuleCode,
    string ModuleDisplayName,
    string Domain,
    string DomainDisplayName,
    IReadOnlyList<NavigationMenuItemDto> Items);

public sealed record NavigationMenuItemDto(
    string PageCode,
    string DisplayName,
    string RoutePath,
    string? RequiredPermission,
    string? ParentPageCode,
    string? IconHint,
    int SortOrder);
