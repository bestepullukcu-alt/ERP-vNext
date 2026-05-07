using Diten.Platform.Application.Features.Tenants;

namespace Diten.Platform.Application.Features.ModulePages;

public sealed record ModulePageDescriptorDto(
    Guid Id,
    Guid TenantId,
    string ModuleCode,
    string PageCode,
    string DisplayName,
    string RoutePath,
    string? RequiredPermission,
    string PageType,
    string Status,
    int SortOrder,
    string? Description,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy);

public sealed record ModulePageDescriptorListItemDto(
    Guid Id,
    string ModuleCode,
    string PageCode,
    string DisplayName,
    string RoutePath,
    string? RequiredPermission,
    string PageType,
    string Status,
    int SortOrder);

public sealed record CreateModulePageDescriptorRequest(
    string ModuleCode,
    string PageCode,
    string DisplayName,
    string RoutePath,
    string? RequiredPermission,
    string PageType,
    string Status,
    int? SortOrder,
    string? Description);

public sealed record UpdateModulePageDescriptorRequest(
    string PageCode,
    string DisplayName,
    string RoutePath,
    string? RequiredPermission,
    string PageType,
    string Status,
    int? SortOrder,
    string? Description);

public sealed record ModulePageDescriptorFilterRequest(
    string? Search,
    string? ModuleCode,
    string? PageType,
    string? Status,
    int Page = 1,
    int PageSize = 20,
    string Sort = "sortOrder");

public static class ModulePageDescriptorMapper
{
    public static ModulePageDescriptorDto ToDto(Diten.Platform.Domain.Entities.ModulePageDescriptor descriptor) =>
        new(
            descriptor.Id,
            descriptor.TenantId,
            descriptor.ModuleCode,
            descriptor.PageCode,
            descriptor.DisplayName,
            descriptor.RoutePath,
            descriptor.RequiredPermission,
            descriptor.PageType.ToString(),
            descriptor.Status.ToString(),
            descriptor.SortOrder,
            descriptor.Description,
            descriptor.CreatedAt,
            descriptor.CreatedBy,
            descriptor.UpdatedAt,
            descriptor.UpdatedBy);

    public static ModulePageDescriptorListItemDto ToListDto(Diten.Platform.Domain.Entities.ModulePageDescriptor descriptor) =>
        new(
            descriptor.Id,
            descriptor.ModuleCode,
            descriptor.PageCode,
            descriptor.DisplayName,
            descriptor.RoutePath,
            descriptor.RequiredPermission,
            descriptor.PageType.ToString(),
            descriptor.Status.ToString(),
            descriptor.SortOrder);

    public static PagedResult<ModulePageDescriptorListItemDto> ToPagedResult(
        IReadOnlyList<Diten.Platform.Domain.Entities.ModulePageDescriptor> items,
        int page,
        int pageSize,
        long totalCount)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        return new PagedResult<ModulePageDescriptorListItemDto>(
            items.Select(ToListDto).ToList(),
            Math.Max(page, 1),
            normalizedPageSize,
            totalCount,
            totalPages);
    }
}
