using Diten.DevEnablementService.Domain.Entities;

namespace Diten.DevEnablementService.Domain.Repositories;

/// <summary>
/// The columns a server-mode list may be sorted by (WP-UI-LIST-SERVER-01, BL-440 package 3). A closed set on purpose:
/// the wire carries a column NAME, and a name that is not in this set is refused 400 before any query is built —
/// never passed through to Mongo as a field path (sorting by `TenantId` or `IsDeleted` is not a user feature).
/// </summary>
public enum GoldenReferenceCompactSortField
{
    Code,
    Name,
    ReferenceType,
    Category,
    Owner,
    Version,
    Priority,
    IsActive
}

/// <summary>
/// One page request of the Golden Compact list. Every field is optional: an empty criteria is "the whole tenant list,
/// sorted by code" — the parameterless behaviour the list had before server mode, kept for its other callers.
/// <see cref="Length"/> null = no paging.
/// </summary>
public sealed record GoldenReferenceCompactListCriteria(
    int Start = 0,
    int? Length = null,
    string? Search = null,
    GoldenReferenceCompactSortField SortField = GoldenReferenceCompactSortField.Code,
    bool Descending = false,
    IReadOnlyList<bool>? IsActive = null,
    IReadOnlyList<string>? ReferenceTypes = null,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<string>? Owners = null,
    int? Priority = null);

/// <summary>
/// A page: the rows, the tenant's whole list count (<see cref="Total"/>) and the count after search + filters
/// (<see cref="FilteredTotal"/>). `FilteredTotal` greater than the rows returned is paging, not loss.
/// </summary>
public sealed record GoldenReferenceCompactListPage(
    IReadOnlyList<GoldenReferenceCompact> Items,
    long Total,
    long FilteredTotal);
