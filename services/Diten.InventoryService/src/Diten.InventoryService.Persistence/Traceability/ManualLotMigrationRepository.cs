using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using Diten.InventoryService.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// WP-0174-P4 — the typed-lot migration on MongoDB (pack §4 <c>MovementLotLink</c> / <c>ManualLotMigration</c>, §8
/// "Migration re-key", CT-D6, CT K4-2 / K4-5).
/// <list type="bullet">
/// <item>Where a typed lot lives today (re-measured at <c>0809644db</c>): the text itself on
/// <c>InventoryTransaction.LotId</c> and as the <c>LotId</c> member of the balance dimension (unique balance index); no lot
/// position, no SKU-position key, no reservation key (reservations are SKU-level); serials received under a typed lot have
/// <c>trace_serials.LotId = null</c>.</item>
/// <item>The group read is tenant-wide (the migration is the tenant's; lots are tenant-wide, CT-D3) and READ-ONLY on the
/// ledger collections. A transaction is "typed" when its lot text is not a registered lot id
/// (<see cref="LotPostingRules.IsRegisteredLotForm"/>) and no link covers it yet.</item>
/// <item>The commit is ONE transaction (snapshot read, majority write): the gate document first (a concurrent typed posting
/// conflicts), then the groups are re-read and must be exactly the listed ones (else <see cref="MigrationStaleException"/>,
/// nothing written), then lots, history, links, the ledger projection re-key (<see cref="LedgerProjectionReKey"/>), serial
/// lots and the completed record. <c>inventory_transactions</c> is only READ here.</item>
/// </list>
/// </summary>
public sealed class ManualLotMigrationRepository : IManualLotMigrationRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<ManualLotMigration> _migrations;
    private readonly IMongoCollection<MovementLotLink> _links;
    private readonly IMongoCollection<Lot> _lots;
    private readonly IMongoCollection<LotChangeEntry> _history;
    private readonly IMongoCollection<SerialNumber> _serials;
    private readonly IMongoCollection<InventoryTransaction> _transactions;
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly TransactionRetryCounter _retries;
    private readonly ILogger<ManualLotMigrationRepository> _logger;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public ManualLotMigrationRepository(
        IMongoClient client,
        IMongoDatabase database,
        ITenantContext tenantContext,
        TransactionRetryCounter retries,
        ILogger<ManualLotMigrationRepository> logger)
    {
        _client = client;
        _database = database;
        _migrations = database.GetCollection<ManualLotMigration>(TraceCollections.LotMigrations);
        _links = database.GetCollection<MovementLotLink>(TraceCollections.MovementLotLinks);
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _history = database.GetCollection<LotChangeEntry>(TraceCollections.LotHistory);
        _serials = database.GetCollection<SerialNumber>(TraceCollections.Serials);
        _transactions = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _retries = retries;
        _logger = logger;
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    /// <summary>The <see cref="TransactionRetryCounter"/> kind of a re-run migration commit.</summary>
    public const string RetryKind = "lot-migration";

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<ManualLotMigration?> GetAsync(CancellationToken cancellationToken = default)
        => await _migrations.Find(Builders<ManualLotMigration>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)).FirstOrDefaultAsync(cancellationToken);

    public Task<IReadOnlyList<TypedLotGroupStock>> ListTypedGroupsAsync(CancellationToken cancellationToken = default)
        => TypedGroupsAsync(null, cancellationToken);

    public async Task<IReadOnlyList<RegisteredLotRef>> LotsOfSkusAsync(IReadOnlyCollection<(Guid SkuId, SkuLevel SkuLevel)> skus, CancellationToken cancellationToken = default)
    {
        if (skus.Count == 0) return [];
        var wanted = skus.ToHashSet();
        var f = Builders<Lot>.Filter;
        var lots = await _lots.Find(f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Eq(x => x.IsDeleted, false)
                                    & f.In(x => x.SkuId, wanted.Select(s => s.SkuId).Distinct()))
            .ToListAsync(cancellationToken);
        return lots.Where(l => wanted.Contains((l.SkuId, l.SkuLevel)))
            .Select(l => new RegisteredLotRef(l.Id, l.SkuId, l.SkuLevel, l.LotNumber)).ToList();
    }

    public async Task<IReadOnlyList<MovementLotLink>> LinksAsync(string? transactionId, Guid? lotId, int limit, CancellationToken cancellationToken = default)
    {
        var f = Builders<MovementLotLink>.Filter;
        var filter = f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId) & f.Eq(x => x.IsDeleted, false);
        if (transactionId is not null) filter &= f.Eq(x => x.TransactionId, transactionId);
        if (lotId is { } lot) filter &= f.Eq(x => x.LotId, lot);
        return await _links.Find(filter).Sort(Builders<MovementLotLink>.Sort.Ascending(x => x.TransactionId)).Limit(limit).ToListAsync(cancellationToken);
    }

    // ── the commit ────────────────────────────────────────────────────────────────────────────────────────

    public async Task<ManualLotMigration> CommitAsync(ManualLotMigrationCommit commit, CancellationToken cancellationToken = default)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        var attempt = 0;
        try
        {
            return await session.WithTransactionAsync(
                async (s, ct) =>
                {
                    if (++attempt > 1)
                    {
                        _retries.Retried(RetryKind);
                        _logger.LogInformation("Typed-lot migration commit re-run after a transient conflict (attempt {Attempt}).", attempt);
                    }

                    return await CommitInTransactionAsync(s, commit, ct);
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (IsLotIdentity(ex.WriteError?.Category, ex.WriteError?.Message))
        {
            throw new LotUniqueViolationException(LotUniqueIndex.Identity, ex);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Any(e => IsLotIdentity(e.Category, e.Message)))
        {
            throw new LotUniqueViolationException(LotUniqueIndex.Identity, ex);
        }
    }

    private async Task<ManualLotMigration> CommitInTransactionAsync(IClientSessionHandle s, ManualLotMigrationCommit commit, CancellationToken ct)
    {
        var now = commit.Now;

        // 1. The gate (CT K4-2): written FIRST, so a typed posting that committed after our snapshot makes this attempt
        //    conflict and re-run — and one that commits after us sees typing closed.
        var mu = Builders<ManualLotMigration>.Update;
        var gate = await _migrations.FindOneAndUpdateAsync(
            s,
            Builders<ManualLotMigration>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            mu.Combine(
                mu.Inc(x => x.GateSeq, 1L),
                mu.SetOnInsert(x => x.Id, Guid.NewGuid()),
                mu.SetOnInsert(x => x.LegalEntityId, (Guid?)_legalEntityId),
                mu.SetOnInsert(x => x.CreatedAt, now),
                mu.SetOnInsert(x => x.IsDeleted, false),
                mu.SetOnInsert(x => x.DeletedAt, null),
                mu.SetOnInsert(x => x.ManualLotEntryClosed, false)),
            new FindOneAndUpdateOptions<ManualLotMigration> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            ct);
        if (gate.ManualLotEntryClosed)
        {
            throw new MigrationAlreadyCompletedException();
        }

        // 2. The groups NOW (this snapshot) must be exactly the ones the decisions were made on.
        var current = (await TypedGroupsAsync(s, ct)).Where(g => !commit.ExcludedItemIds.Contains(g.Group.ItemId)).ToList();
        var currentIds = current.Select(g => g.Group.GroupId).ToHashSet(StringComparer.Ordinal);
        if (!currentIds.SetEquals(commit.ExpectedGroupIds))
        {
            throw new MigrationStaleException(
                $"The typed-lot groups changed since they were listed ({commit.ExpectedGroupIds.Count} listed, {currentIds.Count} now).");
        }

        // 3. The existing target lots, re-read in this transaction (status → CT-D1 for the re-key).
        var newLotIds = commit.NewLots.Select(l => l.Id).ToHashSet();
        var existingIds = commit.Links.Select(l => l.LotId).Where(id => !newLotIds.Contains(id)).Distinct().ToList();
        var lf = Builders<Lot>.Filter;
        var existing = existingIds.Count == 0
            ? []
            : await _lots.Find(s, lf.Eq(x => x.TenantId, (Guid?)_tenantId) & lf.Eq(x => x.IsDeleted, false) & lf.In(x => x.Id, existingIds)).ToListAsync(ct);
        if (existing.Count != existingIds.Count)
        {
            throw new MigrationStaleException("A target lot of the decisions no longer exists.");
        }

        var holds = existing.ToDictionary(l => l.Id, l => LotPostingRules.HoldsStock(l)); // CT-Q16: status or stored retest hold

        // 4. New Draft lots + history rows (the history is built by the handler, stamped here).
        foreach (var lot in commit.NewLots)
        {
            Stamp(lot);
            holds[lot.Id] = false;
        }

        if (commit.NewLots.Count > 0)
        {
            await _lots.InsertManyAsync(s, commit.NewLots, new InsertManyOptions { IsOrdered = true }, ct);
        }

        foreach (var entry in commit.History) Stamp(entry);
        if (commit.History.Count > 0)
        {
            await _history.InsertManyAsync(s, commit.History, new InsertManyOptions { IsOrdered = true }, ct);
        }

        // 5. One link per typed transaction of every group (every company of the tenant).
        var tf = Builders<InventoryTransaction>.Filter;
        var links = new List<MovementLotLink>();
        var reKey = new List<LedgerReKeyGroup>();
        var sf = Builders<SerialNumber>.Filter;
        var su = Builders<SerialNumber>.Update;
        foreach (var link in commit.Links)
        {
            var g = link.Group;
            var transactions = await _transactions.Find(s,
                    tf.Eq(x => x.TenantId, (Guid?)_tenantId) & tf.Eq(x => x.IsDeleted, false)
                    & tf.Eq(x => x.ItemId, g.ItemId) & tf.Eq(x => x.SkuId, g.SkuId) & tf.Eq(x => x.SkuLevel, g.SkuLevel)
                    & tf.Eq(x => x.LotId, g.TypedText))
                .Project(x => new { x.Id, x.LegalEntityId })
                .ToListAsync(ct);
            var ids = transactions.Select(t => t.Id.ToString("D")).ToList();
            links.AddRange(transactions.Select(t => new MovementLotLink
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                LegalEntityId = t.LegalEntityId,
                CreatedAt = now,
                IsDeleted = false,
                TransactionId = t.Id.ToString("D"),
                TypedText = g.TypedText,
                LotId = link.LotId,
                MigrationId = gate.Id
            }));
            reKey.Add(new LedgerReKeyGroup(g.ItemId, g.SkuId, g.SkuLevel, g.TypedText, link.LotId, holds[link.LotId]));

            // CT K4-5: the serials those transactions received / moved carry no lot yet — they join the registered lot.
            if (ids.Count > 0)
            {
                await _serials.UpdateManyAsync(s,
                    sf.Eq(x => x.TenantId, (Guid?)_tenantId) & sf.Eq(x => x.IsDeleted, false)
                    & sf.Eq(x => x.SkuId, g.SkuId) & sf.Eq(x => x.SkuLevel, g.SkuLevel) & sf.Eq(x => x.LotId, (Guid?)null)
                    & sf.Or(sf.In(x => x.ReceivedTransactionId, ids), sf.In(x => x.LastTransactionId, ids)),
                    su.Combine(su.Set(x => x.LotId, (Guid?)link.LotId), su.Set(x => x.UpdatedAt, now), su.Inc(x => x.Version, 1)),
                    cancellationToken: ct);
            }
        }

        if (links.Count > 0)
        {
            await _links.InsertManyAsync(s, links, new InsertManyOptions { IsOrdered = true }, ct);
        }

        // 6. The ledger projections under the registered lot (CT-D6) — ledger-owned code, this transaction.
        await LedgerProjectionReKey.ApplyAsync(_database, s, _tenantId, reKey, now, ct);

        // 7. Completed + typing closed.
        var done = await _migrations.FindOneAndUpdateAsync(
            s,
            Builders<ManualLotMigration>.Filter.Eq(x => x.Id, gate.Id),
            mu.Combine(
                mu.Set(x => x.ManualLotEntryClosed, true),
                mu.Set(x => x.CompletedAt, now),
                mu.Set(x => x.CompletedBy, commit.UserId),
                mu.Set(x => x.Reason, commit.Reason),
                mu.Set(x => x.TransactionsLinked, links.Count),
                mu.Set(x => x.LotsLinked, commit.Links.Select(l => l.LotId).Distinct().Count()),
                mu.Set(x => x.LotsCreated, commit.NewLots.Count),
                mu.Set(x => x.IdempotencyKey, commit.IdempotencyKey),
                mu.Set(x => x.RequestFingerprint, commit.Fingerprint),
                mu.Set(x => x.UpdatedAt, now),
                mu.Inc(x => x.Version, 1)),
            new FindOneAndUpdateOptions<ManualLotMigration> { ReturnDocument = ReturnDocument.After },
            ct);
        return done;
    }

    // ── shared ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The typed groups (read-only on the ledger): transactions with a non-registered lot text that no link covers, grouped
    /// by SKU + text; their stock from the open balance rows of the same text. <paramref name="session"/> = inside the commit.
    /// </summary>
    private async Task<IReadOnlyList<TypedLotGroupStock>> TypedGroupsAsync(IClientSessionHandle? session, CancellationToken ct)
    {
        var lf = Builders<MovementLotLink>.Filter;
        var linkScope = lf.Eq(x => x.TenantId, (Guid?)_tenantId) & lf.Eq(x => x.IsDeleted, false);
        var linked = (session is null
                ? await _links.Find(linkScope).Project(x => x.TransactionId).ToListAsync(ct)
                : await _links.Find(session, linkScope).Project(x => x.TransactionId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var tf = Builders<InventoryTransaction>.Filter;
        var txFilter = tf.Eq(x => x.TenantId, (Guid?)_tenantId) & tf.Eq(x => x.IsDeleted, false) & tf.Ne(x => x.LotId, null);
        var txFind = session is null ? _transactions.Find(txFilter) : _transactions.Find(session, txFilter);
        var rows = await txFind.Project(x => new { x.Id, x.ItemId, x.SkuId, x.SkuLevel, x.LotId, x.PostingDate }).ToListAsync(ct);

        var bf = Builders<InventoryBalance>.Filter;
        var balanceFilter = bf.Eq(x => x.TenantId, (Guid?)_tenantId) & bf.Eq(x => x.IsDeleted, false) & bf.Ne(x => x.LotId, null);
        var balanceFind = session is null ? _balances.Find(balanceFilter) : _balances.Find(session, balanceFilter);
        var stock = (await balanceFind.Project(x => new { x.ItemId, x.SkuId, x.SkuLevel, x.LotId, x.OnHand, x.BaseUomId }).ToListAsync(ct))
            .Where(b => !LotPostingRules.IsRegisteredLotForm(b.LotId, out _))
            .GroupBy(b => (b.ItemId, b.SkuId, b.SkuLevel, Text: b.LotId!))
            .ToDictionary(g => g.Key, g => (OnHand: g.Sum(b => b.OnHand), Uom: g.Select(b => b.BaseUomId).FirstOrDefault(u => !string.IsNullOrEmpty(u))));

        var groups = rows
            .Where(r => !LotPostingRules.IsRegisteredLotForm(r.LotId, out _) && !linked.Contains(r.Id.ToString("D")))
            .GroupBy(r => (r.ItemId, r.SkuId, r.SkuLevel, Text: r.LotId!))
            .Select(g =>
            {
                var key = g.Key;
                var found = stock.TryGetValue(key, out var onHand);
                return new TypedLotGroupStock(
                    new TypedLotGroup(ManualLotMatcher.GroupIdOf(key.SkuId, key.SkuLevel, key.Text), key.ItemId, key.SkuId, key.SkuLevel, key.Text,
                        g.Count(), g.Min(r => r.PostingDate)),
                    found ? onHand.OnHand : 0m,
                    found ? onHand.Uom : null);
            })
            .ToList();
        var order = ManualLotMatcher.Order(groups.Select(g => g.Group)).Select((g, i) => (g.GroupId, i)).ToDictionary(p => p.GroupId, p => p.i, StringComparer.Ordinal);
        return groups.OrderBy(g => order[g.Group.GroupId]).ToList();
    }

    private void Stamp(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.IsDeleted = false;
        entity.DeletedAt = null;
    }

    private static bool IsLotIdentity(ServerErrorCategory? category, string? message)
        => category == ServerErrorCategory.DuplicateKey && message?.Contains(TraceIndexConfiguration.LotIdentityIndex, StringComparison.Ordinal) == true;
}
