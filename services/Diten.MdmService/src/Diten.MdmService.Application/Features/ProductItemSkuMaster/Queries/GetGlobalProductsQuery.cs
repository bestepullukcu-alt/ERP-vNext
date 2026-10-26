using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

public sealed record GetGlobalProductsQuery : IRequest<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductListItemDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public ProductIdentityLifecycleStatus? LifecycleStatus { get; init; }
    // MOD-0290-FU05: only this brand's products (the stored link; null = every product).
    public Guid? BrandId { get; init; }

    // WP-MDM-GP-LIST-01: server-side order. Only the whitelisted stored fields; anything else is a 400
    // (GetGlobalProductsValidator). No orderBy = the register's order before sorting existed.
    public string? OrderBy { get; init; }
    public string? OrderDir { get; init; }

    public static readonly IReadOnlyDictionary<string, GlobalProductListSortField> SortableFields =
        new Dictionary<string, GlobalProductListSortField>(StringComparer.OrdinalIgnoreCase)
        {
            ["globalProductName"] = GlobalProductListSortField.GlobalProductName,
            ["canonicalCode"] = GlobalProductListSortField.CanonicalCode,
            ["lifecycleStatus"] = GlobalProductListSortField.LifecycleStatus
        };

    public static bool IsKnownOrderDir(string? orderDir) =>
        string.IsNullOrEmpty(orderDir)
        || string.Equals(orderDir, "asc", StringComparison.OrdinalIgnoreCase)
        || string.Equals(orderDir, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>The validated order; no orderBy = the register's default (name ascending).</summary>
    public GlobalProductListSort ResolveSort()
    {
        var descending = string.Equals(OrderDir, "desc", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(OrderBy))
            return descending ? GlobalProductListSort.Default with { Descending = true } : GlobalProductListSort.Default;
        return SortableFields.TryGetValue(OrderBy, out var field)
            ? new GlobalProductListSort(field, descending)
            : throw new InvalidOperationException("GLOBAL_PRODUCT_LIST_SORT_FIELD_UNKNOWN");
    }
}
