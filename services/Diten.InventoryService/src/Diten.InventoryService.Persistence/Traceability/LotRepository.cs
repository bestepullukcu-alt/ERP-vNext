using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// MOD-0174 lot store on MongoDB. Lot identity is tenant-wide (CT-D3): every lot / history / genealogy query starts from
/// <see cref="Scope{T}"/> (Tenant + IsDeleted = false) of the resolved request scope; the caller's company is stamped on
/// what it writes (audit) and scopes only its idempotency keys and its ledger reads. Changes run in ONE multi-document
/// transaction (snapshot read, majority write): the lot, its history rows and a genealogy link commit together or not at
/// all. History rows and genealogy links are only ever inserted; nothing here updates or deletes them, and no member
/// deletes a lot.
/// </summary>
public sealed class LotRepository : ILotRepository
{
    /// <summary>The <see cref="TransactionRetryCounter"/> kind of a re-run lot transaction.</summary>
    public const string RetryKind = "lot";

    private readonly IMongoClient _client;
    private readonly IMongoCollection<Lot> _lots;
    private readonly IMongoCollection<LotChangeEntry> _history;
    private readonly IMongoCollection<LotGenealogyLink> _genealogy;
    private readonly IMongoCollection<SerialNumber> _serials;
    private readonly IMongoCollection<InventoryTransaction> _transactions;
    private readonly TransactionRetryCounter _retries;
    private readonly ILogger<LotRepository> _logger;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public LotRepository(
        IMongoClient client,
        IMongoDatabase database,
        ITenantContext tenantContext,
        TransactionRetryCounter retries,
        ILogger<LotRepository> logger)
    {
        _client = client;
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _history = database.GetCollection<LotChangeEntry>(TraceCollections.LotHistory);
        _genealogy = database.GetCollection<LotGenealogyLink>(TraceCollections.GenealogyLinks);
        _serials = database.GetCollection<SerialNumber>(TraceCollections.Serials);
        _transactions = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);
        _retries = retries;
        _logger = logger;
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Lot?> FindAsync(Guid lotId, CancellationToken cancellationToken = default)
        => await _lots.Find(Scope<Lot>() & Builders<Lot>.Filter.Eq(x => x.Id, lotId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Lot>> FindManyAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => lotIds.Count == 0
            ? []
            : await _lots.Find(Scope<Lot>() & Builders<Lot>.Filter.In(x => x.Id, lotIds)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> IdsWithStatusAsync(IReadOnlyCollection<LotStatus> statuses, CancellationToken cancellationToken = default)
        => await _lots.Find(Scope<Lot>() & Builders<Lot>.Filter.In(x => x.Status, statuses)).Project(x => x.Id).ToListAsync(cancellationToken);

    public async Task<Lot?> FindByCreateKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => await _lots.Find(Scope<Lot>()
                            & Builders<Lot>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
                            & Builders<Lot>.Filter.Eq(x => x.CreateIdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<LotPage> ListAsync(LotListQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<Lot>.Filter;
        var filter = Scope<Lot>();
        if (query.ItemId.HasValue) filter &= f.Eq(x => x.ItemId, query.ItemId.Value);
        if (query.SkuId.HasValue) filter &= f.Eq(x => x.SkuId, query.SkuId.Value);
        if (query.SkuLevel.HasValue) filter &= f.Eq(x => x.SkuLevel, query.SkuLevel.Value);
        if (query.Status.HasValue) filter &= f.Eq(x => x.Status, query.Status.Value);
        if (query.ExpiryBefore is { } before) filter &= f.Lt(x => x.ExpiryDate, Midnight(before));
        if (query.Search is { } search)
        {
            var pattern = new MongoDB.Bson.BsonRegularExpression(Regex.Escape(search), "i");
            filter &= f.Or(f.Regex(x => x.LotNumber, pattern), f.Regex(x => x.ManufacturerLot, pattern), f.Regex(x => x.SupplierLot, pattern));
        }

        if (query.Warn)
        {
            // Expired or expiring within 90 days (expiryDate ≤ today + 90), or retest date passed (retestDate < today).
            filter &= f.Or(
                f.Lte(x => x.ExpiryDate, Midnight(query.Today.AddDays(LotDateRules.ExpiringSoonDays))),
                f.Lt(x => x.RetestDate, Midnight(query.Today)));
        }

        var s = Builders<Lot>.Sort;
        var primary = query.Sort switch
        {
            LotSortField.ExpiryDate => query.Descending ? s.Descending(x => x.ExpiryDate) : s.Ascending(x => x.ExpiryDate),
            LotSortField.CreatedAt => query.Descending ? s.Descending(x => x.CreatedAt) : s.Ascending(x => x.CreatedAt),
            _ => query.Descending ? s.Descending(x => x.LotNumber) : s.Ascending(x => x.LotNumber)
        };

        // A tie-breaker never repeats the primary field: a sort document with the same field twice keeps only the last one.
        var order = query.Sort == LotSortField.LotNumber
            ? s.Combine(primary, s.Ascending(x => x.Id))
            : s.Combine(primary, s.Ascending(x => x.LotNumber), s.Ascending(x => x.Id));
        var find = _lots.Find(filter).Sort(order);
        if (query.Page is { } page && query.PageSize is { } size)
        {
            var total = await _lots.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            var items = await find.Skip((page - 1) * size).Limit(size).ToListAsync(cancellationToken);
            return new LotPage(items, total);
        }

        var all = await find.ToListAsync(cancellationToken);
        return new LotPage(all, all.Count);
    }

    public async Task<LotHistoryPage> HistoryAsync(Guid lotId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Scope<LotChangeEntry>() & Builders<LotChangeEntry>.Filter.Eq(x => x.LotId, lotId);
        var total = await _history.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _history.Find(filter)
            .Sort(Builders<LotChangeEntry>.Sort.Descending(x => x.OccurredAt).Descending(x => x.CreatedAt).Ascending(x => x.Field))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new LotHistoryPage(items, total);
    }

    public async Task<LotChangeEntry?> FindStatusChangeByKeyAsync(Guid lotId, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _history.Find(Scope<LotChangeEntry>()
                               & Builders<LotChangeEntry>.Filter.Eq(x => x.LotId, lotId)
                               & Builders<LotChangeEntry>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<LotGenealogyLink?> FindLinkByKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => await _genealogy.Find(Scope<LotGenealogyLink>()
                                 & Builders<LotGenealogyLink>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
                                 & Builders<LotGenealogyLink>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<LotGenealogyLink>> ParentLinksAsync(IReadOnlyCollection<Guid> childLotIds, CancellationToken cancellationToken = default)
        => LinksAsync(null, Builders<LotGenealogyLink>.Filter.In(x => x.ChildLotId, childLotIds), childLotIds.Count, cancellationToken);

    public Task<IReadOnlyList<LotGenealogyLink>> ChildLinksAsync(IReadOnlyCollection<Guid> parentLotIds, CancellationToken cancellationToken = default)
        => LinksAsync(null, Builders<LotGenealogyLink>.Filter.In(x => x.ParentLotId, parentLotIds), parentLotIds.Count, cancellationToken);

    public async Task<LotLedgerSource?> FirstInboundAsync(Guid lotId, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryTransaction>.Filter;
        var inbound = new[]
        {
            MovementType.OpeningBalance, MovementType.GoodsReceiptPo, MovementType.GoodsReceiptProd,
            MovementType.CustomerReturn, MovementType.CountGain
        };
        var first = await _transactions.Find(f.Eq(x => x.TenantId, (Guid?)_tenantId)
                                             & f.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
                                             & f.Eq(x => x.LotId, lotId.ToString("D"))
                                             & f.In(x => x.MovementType, inbound))
            .Sort(Builders<InventoryTransaction>.Sort.Ascending(x => x.TransactionDate).Ascending(x => x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        return first is null ? null : new LotLedgerSource(first.MovementType, first.SourceDocumentId, first.TransactionDate);
    }

    public async Task<long> SerialCountAsync(Guid lotId, CancellationToken cancellationToken = default)
        => await _serials.CountDocumentsAsync(Scope<SerialNumber>() & Builders<SerialNumber>.Filter.Eq(x => x.LotId, lotId),
            cancellationToken: cancellationToken);

    // ── the transaction ───────────────────────────────────────────────────────────────────────────────────

    public async Task<T> InTransactionAsync<T>(Func<ILotTransaction, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        var attempt = 0;
        try
        {
            return await session.WithTransactionAsync(
                (s, ct) =>
                {
                    if (++attempt > 1)
                    {
                        _retries.Retried(RetryKind);
                        _logger.LogInformation("Lot transaction re-run after a transient conflict (attempt {Attempt}).", attempt);
                    }

                    return work(new Transaction(this, s), ct);
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new LotUniqueViolationException(index, ex);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new LotUniqueViolationException(index, ex);
        }
    }

    private sealed class Transaction : ILotTransaction
    {
        private readonly LotRepository _owner;
        private readonly IClientSessionHandle _session;

        public Transaction(LotRepository owner, IClientSessionHandle session)
        {
            _owner = owner;
            _session = session;
        }

        public async Task<Lot?> FindAsync(Guid lotId, CancellationToken cancellationToken = default)
            => await _owner._lots.Find(_session, _owner.Scope<Lot>() & Builders<Lot>.Filter.Eq(x => x.Id, lotId)).FirstOrDefaultAsync(cancellationToken);

        public async Task<Lot?> FindByIdentityKeyAsync(string identityKey, CancellationToken cancellationToken = default)
            => await _owner._lots.Find(_session, _owner.Scope<Lot>() & Builders<Lot>.Filter.Eq(x => x.IdentityKey, identityKey))
                .FirstOrDefaultAsync(cancellationToken);

        public async Task InsertAsync(Lot lot, CancellationToken cancellationToken = default)
        {
            _owner.StampScope(lot);
            await _owner._lots.InsertOneAsync(_session, lot, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceAsync(Lot lot, int expectedVersion, CancellationToken cancellationToken = default)
        {
            // The creating company stays on the lot (audit); a replace never re-stamps it with the editor's company.
            lot.Version = expectedVersion + 1;
            var result = await _owner._lots.ReplaceOneAsync(
                _session,
                _owner.Scope<Lot>() & Builders<Lot>.Filter.Eq(x => x.Id, lot.Id) & Builders<Lot>.Filter.Eq(x => x.Version, expectedVersion),
                lot,
                cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public async Task AppendHistoryAsync(IReadOnlyList<LotChangeEntry> entries, CancellationToken cancellationToken = default)
        {
            if (entries.Count == 0) return;
            foreach (var entry in entries) _owner.StampScope(entry);
            await _owner._history.InsertManyAsync(_session, entries, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }

        public async Task<LotGenealogyLink?> FindLinkAsync(Guid parentLotId, Guid childLotId, string transformationReference, CancellationToken cancellationToken = default)
            => await _owner._genealogy.Find(_session, _owner.Scope<LotGenealogyLink>()
                                                      & Builders<LotGenealogyLink>.Filter.Eq(x => x.ParentLotId, parentLotId)
                                                      & Builders<LotGenealogyLink>.Filter.Eq(x => x.ChildLotId, childLotId)
                                                      & Builders<LotGenealogyLink>.Filter.Eq(x => x.TransformationReference, transformationReference))
                .FirstOrDefaultAsync(cancellationToken);

        public Task<IReadOnlyList<LotGenealogyLink>> ParentLinksAsync(IReadOnlyCollection<Guid> childLotIds, CancellationToken cancellationToken = default)
            => _owner.LinksAsync(_session, Builders<LotGenealogyLink>.Filter.In(x => x.ChildLotId, childLotIds), childLotIds.Count, cancellationToken);

        public async Task InsertLinkAsync(LotGenealogyLink link, CancellationToken cancellationToken = default)
        {
            _owner.StampScope(link);
            await _owner._genealogy.InsertOneAsync(_session, link, cancellationToken: cancellationToken);
        }
    }

    // ── shared ────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<LotGenealogyLink>> LinksAsync(
        IClientSessionHandle? session, FilterDefinition<LotGenealogyLink> ends, int count, CancellationToken ct)
    {
        if (count == 0) return [];
        var filter = Scope<LotGenealogyLink>() & ends;
        var sort = Builders<LotGenealogyLink>.Sort.Ascending(x => x.CreatedAt).Ascending(x => x.Id);
        return session is null
            ? await _genealogy.Find(filter).Sort(sort).ToListAsync(ct)
            : await _genealogy.Find(session, filter).Sort(sort).ToListAsync(ct);
    }

    /// <summary>Tenant + IsDeleted = false — the mandatory entry filter of every MOD-0174 query (identity tenant-wide, CT-D3).</summary>
    private FilterDefinition<T> Scope<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId) & Builders<T>.Filter.Eq(x => x.IsDeleted, false);

    /// <summary>The tenant, the writing company (audit) and "not deleted" are stamped on everything this store inserts.</summary>
    private void StampScope(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }

    private static DateTimeOffset Midnight(DateOnly day) => new(day.Year, day.Month, day.Day, 0, 0, 0, TimeSpan.Zero);

    private static LotUniqueIndex? UniqueIndexOf(ServerErrorCategory? category, string? message)
    {
        if (category != ServerErrorCategory.DuplicateKey || message is null) return null;
        if (message.Contains(TraceIndexConfiguration.LotIdentityIndex, StringComparison.Ordinal)) return LotUniqueIndex.Identity;
        if (message.Contains(TraceIndexConfiguration.LotCreateKeyIndex, StringComparison.Ordinal)) return LotUniqueIndex.CreateKey;
        if (message.Contains(TraceIndexConfiguration.StatusChangeKeyIndex, StringComparison.Ordinal)) return LotUniqueIndex.StatusChangeKey;
        if (message.Contains(TraceIndexConfiguration.GenealogyLinkIndex, StringComparison.Ordinal)) return LotUniqueIndex.GenealogyLink;
        if (message.Contains(TraceIndexConfiguration.GenealogyKeyIndex, StringComparison.Ordinal)) return LotUniqueIndex.GenealogyKey;
        return null;
    }
}
