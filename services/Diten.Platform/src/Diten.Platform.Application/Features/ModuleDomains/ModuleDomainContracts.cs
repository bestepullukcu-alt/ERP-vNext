using Diten.Platform.Application.Features.Tenants;

namespace Diten.Platform.Application.Features.ModuleDomains;

public sealed record ModuleDomainDto(
    Guid Id,
    string Code,
    string DisplayName,
    string? Description,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateModuleDomainRequest(
    string Code,
    string DisplayName,
    string? Description,
    int? SortOrder,
    bool IsActive);

public sealed record UpdateModuleDomainRequest(
    string Code,
    string DisplayName,
    string? Description,
    int? SortOrder,
    bool IsActive);

public sealed record ModuleDomainFilterRequest(
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20,
    string Sort = "sortOrder");

public static class ModuleDomainMapper
{
    public static ModuleDomainDto ToDto(Diten.Platform.Domain.Entities.ModuleDomain item) =>
        new(item.Id, item.Code, item.DisplayName, item.Description, item.SortOrder, item.IsActive,
            item.CreatedAt, item.UpdatedAt);

    public static PagedResult<ModuleDomainDto> ToPagedResult(
        IReadOnlyList<Diten.Platform.Domain.Entities.ModuleDomain> items,
        int page,
        int pageSize,
        long totalCount)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        return new PagedResult<ModuleDomainDto>(
            items.Select(ToDto).ToList(),
            Math.Max(page, 1),
            normalizedPageSize,
            totalCount,
            totalPages);
    }
}

public static class ModuleDomainErrorCodes
{
    public const string DomainCodeInUse = "DOMAIN_CODE_IN_USE";
}
