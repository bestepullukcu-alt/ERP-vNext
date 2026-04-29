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
    string? Category,
    string Status,
    string ModuleVersion,
    bool IsCoreModule,
    bool IsTenantAssignable,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ModuleCatalogListItemDto(
    Guid Id,
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string Domain,
    string Service,
    string? Category,
    string Status,
    string ModuleVersion,
    bool IsCoreModule,
    bool IsTenantAssignable,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateModuleCatalogItemRequest(
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string? Description,
    string Domain,
    string Service,
    string? Category,
    string Status,
    string ModuleVersion,
    bool IsCoreModule,
    bool IsTenantAssignable,
    int? SortOrder);

public sealed record UpdateModuleCatalogItemRequest(
    string ModuleCode,
    string ModuleName,
    string DisplayName,
    string? Description,
    string Domain,
    string Service,
    string? Category,
    string Status,
    string ModuleVersion,
    bool IsCoreModule,
    bool IsTenantAssignable,
    int? SortOrder);

public sealed record ModuleCatalogFilterRequest(
    string? Search,
    string? Domain,
    string? Service,
    string? Category,
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
            item.Category, item.Status.ToString(), item.ModuleVersion, item.IsCoreModule, item.IsTenantAssignable,
            item.SortOrder, item.CreatedAt, item.UpdatedAt);

    public static ModuleCatalogListItemDto ToListDto(Diten.Platform.Domain.Entities.ModuleCatalogItem item) =>
        new(item.Id, item.ModuleCode, item.ModuleName, item.DisplayName, item.Domain, item.Service, item.Category,
            item.Status.ToString(), item.ModuleVersion, item.IsCoreModule, item.IsTenantAssignable, item.SortOrder,
            item.CreatedAt, item.UpdatedAt);

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
