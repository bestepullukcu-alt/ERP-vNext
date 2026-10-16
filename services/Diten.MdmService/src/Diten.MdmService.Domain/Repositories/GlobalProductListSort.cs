namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// The stored Global Product fields the register may be sorted by (WP-MDM-GP-LIST-01, the GSKU register's B17 rule):
/// only fields the product document itself carries. The name sorts by its normalized form (the same key the search and
/// the duplicate rule use), so the order does not depend on case or spacing.
/// </summary>
public enum GlobalProductListSortField
{
    GlobalProductName,
    CanonicalCode,
    LifecycleStatus
}

/// <summary>A register order. Every order ends with the canonical code and the document id as a stable tiebreak.</summary>
public sealed record GlobalProductListSort(GlobalProductListSortField Field, bool Descending)
{
    /// <summary>The register's order before sorting existed: name ascending, then canonical code, then id.</summary>
    public static GlobalProductListSort Default { get; } = new(GlobalProductListSortField.GlobalProductName, false);
}
