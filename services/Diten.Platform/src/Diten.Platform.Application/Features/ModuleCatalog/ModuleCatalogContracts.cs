using Diten.Platform.Application.Features.Tenants;

namespace Diten.Platform.Application.Features.ModuleCatalog;

public sealed record ModuleCatalogItemDto(
    Guid Id,
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string? Description,
    string Domain,
    string Service,
    string Status,
    string ModuleVersion,
    bool IsCoreModule,
    bool IsTenantAssignable,
    int SortOrder,
    string Origin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? Icon = null, // FIX-MODULE-ICON — module sidebar icon (boxicons class), operator-editable SOFT field.
    bool IsBaseline = false); // FEAT-CATALOG-BASELINE-BADGE — entitlement-free module (HARD/code-owned; read-only).

public sealed record ModuleCatalogListItemDto(
    Guid Id,
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string Domain,
    string Service,
    string Status,
    string ModuleVersion,
    bool IsCoreModule,
    bool IsTenantAssignable,
    int SortOrder,
    string Origin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsBaseline = false); // FEAT-CATALOG-BASELINE-BADGE — entitlement-free module (HARD/code-owned; read-only).

public sealed record CreateModuleCatalogItemRequest(
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string? Description,
    string Domain,
    string Service,
    string Status,
    string ModuleVersion,
    bool IsCoreModule,
    bool IsTenantAssignable,
    int? SortOrder,
    string? Icon = null); // FIX-MODULE-ICON — optional boxicons class.

public sealed record UpdateModuleCatalogItemRequest(
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string? Description,
    string Domain,
    string Service,
    string Status,
    string ModuleVersion,
    bool IsCoreModule,
    bool IsTenantAssignable,
    int? SortOrder,
    string? Icon = null); // FIX-MODULE-ICON — optional boxicons class.

public sealed record ModuleCatalogFilterRequest(
    string? Search,
    string? Domain,
    string? Service,
    string? Status,
    bool? IsCoreModule,
    bool? IsTenantAssignable,
    int Page = 1,
    int PageSize = 20,
    string Sort = "sortOrder");

public static class ModuleCatalogMapper
{
    public static ModuleCatalogItemDto ToDto(Diten.Platform.Domain.Entities.ModuleCatalogItem item) =>
        new(item.Id, item.ModuleCode, item.ModuleName, item.DisplayName, item.Description, item.Domain, item.Service,
            item.Status.ToString(), item.ModuleVersion, item.IsCoreModule, item.IsTenantAssignable,
            item.SortOrder, item.Origin.ToString(), item.CreatedAt, item.UpdatedAt, item.Icon, item.IsBaseline);

    public static ModuleCatalogListItemDto ToListDto(Diten.Platform.Domain.Entities.ModuleCatalogItem item) =>
        new(item.Id, item.ModuleCode, item.ModuleName, item.DisplayName, item.Domain, item.Service,
            item.Status.ToString(), item.ModuleVersion, item.IsCoreModule, item.IsTenantAssignable, item.SortOrder,
            item.Origin.ToString(), item.CreatedAt, item.UpdatedAt, item.IsBaseline);

    public static PagedResult<ModuleCatalogListItemDto> ToPagedResult(
        IReadOnlyList<Diten.Platform.Domain.Entities.ModuleCatalogItem> items,
        int page,
        int pageSize,
        long totalCount)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        return new PagedResult<ModuleCatalogListItemDto>(
            items.Select(ToListDto).ToList(),
            Math.Max(page, 1),
            normalizedPageSize,
            totalCount,
            totalPages);
    }
}
