namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// The stored GSKU fields the register may be sorted by (WP-MDM-GSKU-ACCEPT-01 FIX3, B17). Only fields the GSKU document
/// itself carries are sortable: the Global Product name and the revision identifier live on other documents, so the list
/// screen declares those columns not orderable instead of sorting a page it does not hold.
/// </summary>
public enum GskuListSortField
{
    CanonicalCode,
    LifecycleStatus,
    PackQuantity
}

/// <summary>A register order. Every order ends with the document id as a stable tiebreak.</summary>
public sealed record GskuListSort(GskuListSortField Field, bool Descending)
{
    /// <summary>The register's order before sorting existed: canonical code ascending, then id.</summary>
    public static GskuListSort Default { get; } = new(GskuListSortField.CanonicalCode, false);
}
