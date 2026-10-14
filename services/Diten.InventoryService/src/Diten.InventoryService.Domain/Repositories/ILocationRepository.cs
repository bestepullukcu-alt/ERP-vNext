using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>
/// The MOD-0173-FU02 location store. Every member is bound to the caller's resolved Tenant + LegalEntity: a location
/// of another company does not exist for it (§8). Reads run outside a transaction; every change runs through
/// <see cref="InTransactionAsync{T}"/>, whose work may be re-run by the driver on a transient write conflict (a
/// concurrent ledger posting or lifecycle change of the same document) and must therefore only read and write through
/// the given <see cref="ILocationTransaction"/>.
/// </summary>
public interface ILocationRepository
{
    Task<Location?> FindAsync(Guid locationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Location>> FindManyAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken = default);

    /// <summary>Frozen <c>listLocations</c>: every status, optional type / parent filter, ordered by code.</summary>
    Task<IReadOnlyList<Location>> ListAsync(LocationType? type, Guid? parentId, CancellationToken cancellationToken = default);

    Task<LocationSearchPage> SearchAsync(LocationSearch search, CancellationToken cancellationToken = default);

    Task<LocationHistoryPage> HistoryAsync(Guid locationId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the id names a location of the caller's TENANT in ANOTHER legal entity. Answers only that boolean — used
    /// to tell an explicit cross-company parent in a write (403 CROSS_LE_FORBIDDEN) from an unknown one (404, §8).
    /// </summary>
    Task<bool> ExistsInAnotherCompanyAsync(Guid locationId, CancellationToken cancellationToken = default);

    Task<Location?> FindByCreateKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Location>> FindByBulkKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<LocationStockPresence> StockPresenceAsync(Location location, CancellationToken cancellationToken = default);

    /// <summary>Codes (normalized) of this company that are already taken, any status.</summary>
    Task<IReadOnlySet<string>> TakenCodesAsync(IReadOnlyCollection<string> normalizedCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="work"/> in ONE multi-document transaction (snapshot read, majority write). A unique-index
    /// violation surfaces as <see cref="LocationUniqueViolationException"/>; nothing of the work is then written.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<ILocationTransaction, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes inside one location transaction (snapshot of the transaction start, plus own writes).</summary>
public interface ILocationTransaction
{
    Task<Location?> FindAsync(Guid locationId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveChildAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>The node and every descendant (AncestorIds contains the node).</summary>
    Task<IReadOnlyList<Location>> SubtreeAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>K-10 inside the transaction: ledger rows with OnHand ≠ 0 on the node / below it.</summary>
    Task<LocationStockPresence> StockPresenceAsync(Location location, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> TakenCodesAsync(IReadOnlyCollection<string> normalizedCodes, CancellationToken cancellationToken = default);

    Task InsertAsync(IReadOnlyList<Location> locations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the node if its stored <see cref="EntityBase.Version"/> still equals <paramref name="expectedVersion"/>;
    /// the stored version becomes <c>expectedVersion + 1</c>. False = a concurrent change won (→ 409).
    /// </summary>
    Task<bool> ReplaceAsync(Location location, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>Rewrites the materialized path of one descendant during a move (K-07).</summary>
    Task SetAncestorsAsync(Guid locationId, IReadOnlyList<Guid> ancestorIds, CancellationToken cancellationToken = default);

    /// <summary>Bumps <see cref="Location.StructureSeq"/> of a parent a child is attached to (serialises with K-05/K-06).</summary>
    Task TouchStructureAsync(Guid locationId, CancellationToken cancellationToken = default);

    Task AppendHistoryAsync(IReadOnlyList<LocationChangeEntry> entries, CancellationToken cancellationToken = default);
}

public sealed record LocationStockPresence(bool HasStock, bool SubtreeHasStock)
{
    public bool Any => HasStock || SubtreeHasStock;
}

public sealed record LocationSearch(
    int Page,
    int PageSize,
    string? Search,
    IReadOnlyCollection<LocationType> Types,
    IReadOnlyCollection<LocationStatus> Statuses,
    bool? IsStockable,
    Guid? ParentId,
    LocationSortField OrderBy,
    bool Descending);

public enum LocationSortField
{
    Code,
    Name,
    Type,
    Status
}

public sealed record LocationSearchPage(IReadOnlyList<Location> Items, long Total);

public sealed record LocationHistoryPage(IReadOnlyList<LocationChangeEntry> Items, long Total);

public enum LocationUniqueIndex
{
    Code,
    CreateIdempotencyKey
}

/// <summary>A unique index refused the write (code taken, or the create key committed by a concurrent request).</summary>
public sealed class LocationUniqueViolationException : Exception
{
    public LocationUniqueViolationException(LocationUniqueIndex index, Exception? innerException = null)
        : base($"Unique index violated: {index}.", innerException)
        => Index = index;

    public LocationUniqueIndex Index { get; }
}
