using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Domain.Traceability.Repositories;

/// <summary>
/// The MOD-0174 lot store. Lot identity is TENANT-wide (CT-D3): every member is bound to the caller's tenant and a lot of
/// another tenant does not exist for it. The caller's company matters only where the pack says so: a create /
/// genealogy idempotency key is the company's, and ledger reads (the lot's source) are the company's.
/// There is no delete member — a lot, its history and its genealogy are never removed (pack §4).
/// Every change runs through <see cref="InTransactionAsync{T}"/> (one Mongo transaction; the work may be re-run on a
/// transient conflict and must only read and write through the given <see cref="ILotTransaction"/>).
/// </summary>
public interface ILotRepository
{
    Task<Lot?> FindAsync(Guid lotId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Lot>> FindManyAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> IdsWithStatusAsync(IReadOnlyCollection<LotStatus> statuses, CancellationToken cancellationToken = default);

    Task<Lot?> FindByCreateKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<LotPage> ListAsync(LotListQuery query, CancellationToken cancellationToken = default);

    Task<LotHistoryPage> HistoryAsync(Guid lotId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<LotChangeEntry?> FindStatusChangeByKeyAsync(Guid lotId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<LotGenealogyLink?> FindLinkByKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LotGenealogyLink>> ParentLinksAsync(IReadOnlyCollection<Guid> childLotIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LotGenealogyLink>> ChildLinksAsync(IReadOnlyCollection<Guid> parentLotIds, CancellationToken cancellationToken = default);

    /// <summary>The first inbound ledger transaction of the lot in the caller's company (read-only), or null.</summary>
    Task<LotLedgerSource?> FirstInboundAsync(Guid lotId, CancellationToken cancellationToken = default);

    Task<long> SerialCountAsync(Guid lotId, CancellationToken cancellationToken = default);

    Task<T> InTransactionAsync<T>(Func<ILotTransaction, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}

/// <summary>Reads and writes inside one lot transaction.</summary>
public interface ILotTransaction
{
    Task<Lot?> FindAsync(Guid lotId, CancellationToken cancellationToken = default);

    /// <summary>The lot whose key is <paramref name="identityKey"/> — the key always comes from <c>LotIdentityPolicy</c>.</summary>
    Task<Lot?> FindByIdentityKeyAsync(string identityKey, CancellationToken cancellationToken = default);

    Task InsertAsync(Lot lot, CancellationToken cancellationToken = default);

    /// <summary>Replaces the lot if its stored version still equals <paramref name="expectedVersion"/> (then +1); false = 409.</summary>
    Task<bool> ReplaceAsync(Lot lot, int expectedVersion, CancellationToken cancellationToken = default);

    Task AppendHistoryAsync(IReadOnlyList<LotChangeEntry> entries, CancellationToken cancellationToken = default);

    Task<LotGenealogyLink?> FindLinkAsync(Guid parentLotId, Guid childLotId, string transformationReference, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LotGenealogyLink>> ParentLinksAsync(IReadOnlyCollection<Guid> childLotIds, CancellationToken cancellationToken = default);

    Task InsertLinkAsync(LotGenealogyLink link, CancellationToken cancellationToken = default);
}

public enum LotSortField
{
    LotNumber,
    ExpiryDate,
    CreatedAt
}

/// <summary>listLots filters (v1 + v1.1). <see cref="Page"/> null = the v1 unpaged answer.</summary>
public sealed record LotListQuery(
    Guid? ItemId,
    Guid? SkuId,
    SkuLevel? SkuLevel,
    LotStatus? Status,
    DateOnly? ExpiryBefore,
    string? Search,
    bool Warn,
    DateOnly Today,
    int? Page,
    int? PageSize,
    LotSortField Sort,
    bool Descending);

public sealed record LotPage(IReadOnlyList<Lot> Items, long Total);

public sealed record LotHistoryPage(IReadOnlyList<LotChangeEntry> Items, long Total);

/// <summary>The lot's first inbound ledger row: movement type wire name, source document, transaction date.</summary>
public sealed record LotLedgerSource(MovementType MovementType, string? SourceDocumentId, DateTimeOffset TransactionDate);

public enum LotUniqueIndex
{
    Identity,
    CreateKey,
    StatusChangeKey,
    GenealogyLink,
    GenealogyKey
}

/// <summary>A unique index refused the write; nothing of the transaction was written.</summary>
public sealed class LotUniqueViolationException : Exception
{
    public LotUniqueViolationException(LotUniqueIndex index, Exception? innerException = null)
        : base($"Unique index violated: {index}.", innerException)
        => Index = index;

    public LotUniqueIndex Index { get; }
}
