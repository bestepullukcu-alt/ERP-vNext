using Diten.Platform.Application.Features.Tenants;

namespace Diten.Platform.Application.Features.ModuleServices;

public sealed record ModuleServiceDto(
    Guid Id,
    string Code,
    string DisplayName,
    string? Description,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateModuleServiceRequest(
    string Code,
    string DisplayName,
    string? Description,
    int? SortOrder,
    bool IsActive);

public sealed record UpdateModuleServiceRequest(
    string Code,
    string DisplayName,
    string? Description,
    int? SortOrder,
    bool IsActive);

public sealed record ModuleServiceFilterRequest(
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20,
    string Sort = "sortOrder");

public static class ModuleServiceMapper
{
    public static ModuleServiceDto ToDto(Diten.Platform.Domain.Entities.ModuleService item) =>
        new(item.Id, item.Code, item.DisplayName, item.Description, item.SortOrder, item.IsActive,
            item.CreatedAt, item.UpdatedAt);

    public static PagedResult<ModuleServiceDto> ToPagedResult(
        IReadOnlyList<Diten.Platform.Domain.Entities.ModuleService> items,
        int page,
        int pageSize,
        long totalCount)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        return new PagedResult<ModuleServiceDto>(
            items.Select(ToDto).ToList(),
            Math.Max(page, 1),
            normalizedPageSize,
            totalCount,
            totalPages);
    }
}

public static class ModuleServiceErrorCodes
{
    public const string ServiceCodeInUse = "SERVICE_CODE_IN_USE";
}
