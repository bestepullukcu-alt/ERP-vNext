namespace Diten.Platform.Application.Features.Navigation;

/// <summary>
/// MOD-0285 — one entitled module's navigation group. Items are the module's platform-scope
/// (TenantId=Guid.Empty) page descriptors that are navigation-visible and Active, ordered by SortOrder.
/// The frontend applies the per-item permission filter (Perms.Has) and parent/child nesting.
/// </summary>
public sealed record NavigationModuleGroupDto(
    string ModuleCode,
    string ModuleDisplayName,
    IReadOnlyList<NavigationMenuItemDto> Items);

public sealed record NavigationMenuItemDto(
    string PageCode,
    string DisplayName,
    string RoutePath,
    string? RequiredPermission,
    string? ParentPageCode,
    string? IconHint,
    int SortOrder);
