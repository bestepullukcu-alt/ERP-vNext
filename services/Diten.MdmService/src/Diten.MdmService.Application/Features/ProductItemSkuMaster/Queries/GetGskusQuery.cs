using Diten.Shared.Core;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

public sealed record GetGskusQuery
    : IRequest<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GskuListItemDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public ProductIdentityLifecycleStatus? LifecycleStatus { get; init; }

    // FIX3 B17: server-side order. Only the whitelisted stored fields; anything else is a 400 (GetGskusValidator).
    public string? OrderBy { get; init; }
    public string? OrderDir { get; init; }

    public static readonly IReadOnlyDictionary<string, GskuListSortField> SortableFields =
        new Dictionary<string, GskuListSortField>(StringComparer.OrdinalIgnoreCase)
        {
            ["canonicalCode"] = GskuListSortField.CanonicalCode,
            ["lifecycleStatus"] = GskuListSortField.LifecycleStatus,
            ["packQuantity"] = GskuListSortField.PackQuantity
        };

    public static bool IsKnownOrderDir(string? orderDir) =>
        string.IsNullOrEmpty(orderDir)
        || string.Equals(orderDir, "asc", StringComparison.OrdinalIgnoreCase)
        || string.Equals(orderDir, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>The validated order; no orderBy = the register's default (canonical code ascending).</summary>
    public GskuListSort ResolveSort()
    {
        var descending = string.Equals(OrderDir, "desc", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(OrderBy))
            return descending ? GskuListSort.Default with { Descending = true } : GskuListSort.Default;
        return SortableFields.TryGetValue(OrderBy, out var field)
            ? new GskuListSort(field, descending)
            : throw new InvalidOperationException("GSKU_LIST_SORT_FIELD_UNKNOWN");
    }
}
