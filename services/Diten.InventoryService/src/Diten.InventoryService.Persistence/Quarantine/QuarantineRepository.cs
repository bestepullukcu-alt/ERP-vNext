using Diten.BuildingBlocks.CentralAudit;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Quarantine;

/// <summary>
/// MOD-0175 records on MongoDB. Every query starts from the caller's Tenant + Legal Entity (block reasons: Tenant) and
/// <c>IsDeleted = false</c> — there is no overload without the scope. Writes run in ONE multi-document transaction per
/// change (<see cref="InTransactionAsync{T}"/>); a unique-index refusal surfaces as <see cref="QuarantineDuplicateException"/>.
/// Decisions, decision events and history are INSERT-ONLY: the write session only ever calls <c>InsertOne</c> /
/// <c>InsertMany</c> on them (architecture-tested). Nothing is deleted.
/// </summary>
public sealed class QuarantineRepository : IQuarantineRepository
{
    private static readonly ConcurrentDictionary<Guid, bool> SeededTenants = new();

    private readonly IMongoClient _client;
    private readonly IMongoCollection<QuarantineHold> _holds;
    private readonly IMongoCollection<QuarantineDecision> _decisions;
    private readonly IMongoCollection<QuarantineDecisionEvent> _events;
    private readonly IMongoCollection<BlockedStock> _blocked;
    private readonly IMongoCollection<QuarantineHistoryEntry> _history;
    private readonly IMongoCollection<QuarantineSettings> _settings;
    private readonly IMongoCollection<BlockReason> _reasons;
    private readonly IMongoCollection<LotReleaseClaim> _claims;
    private readonly IMongoCollection<BsonDocument> _sequences;
    private readonly IMongoCollection<QuarantineHoldUpdateAuditRecord> _audit;
    private readonly TransactionRetryCounter _retries;
    private readonly ILogger<QuarantineRepository> _logger;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public QuarantineRepository(
        IMongoClient client, IMongoDatabase database, ITenantContext tenant, TransactionRetryCounter retries, ILogger<QuarantineRepository> logger)
    {
        _client = client;
        _holds = database.GetCollection<QuarantineHold>(QuarantineCollections.Holds);
        _decisions = database.GetCollection<QuarantineDecision>(QuarantineCollections.Decisions);
        _events = database.GetCollection<QuarantineDecisionEvent>(QuarantineCollections.DecisionEvents);
        _blocked = database.GetCollection<BlockedStock>(QuarantineCollections.BlockedStock);
        _history = database.GetCollection<QuarantineHistoryEntry>(QuarantineCollections.History);
        _settings = database.GetCollection<QuarantineSettings>(QuarantineCollections.Settings);
        _reasons = database.GetCollection<BlockReason>(QuarantineCollections.BlockReasons);
        _claims = database.GetCollection<LotReleaseClaim>(QuarantineCollections.LotReleaseClaims);
        _sequences = database.GetCollection<BsonDocument>(QuarantineCollections.Sequences);
        _audit = database.GetCollection<QuarantineHoldUpdateAuditRecord>(InventoryCollections.Audit);
        _retries = retries;
        _logger = logger;
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    // ── scope ─────────────────────────────────────────────────────────────────────────────────────────────

    private FilterDefinition<T> Scope<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId)
           & Builders<T>.Filter.Eq(x => x.IsDeleted, false);

    private FilterDefinition<BlockReason> TenantScope()
        => Builders<BlockReason>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<BlockReason>.Filter.Eq(x => x.IsDeleted, false);

    private void Stamp(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
    }

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<QuarantineHold?> FindHoldAsync(Guid holdId, CancellationToken cancellationToken = default)
        => await _holds.Find(Scope<QuarantineHold>() & Builders<QuarantineHold>.Filter.Eq(x => x.Id, holdId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<QuarantineHold?> FindHoldBySourceMovementAsync(string transactionId, CancellationToken cancellationToken = default)
        => await _holds.Find(Scope<QuarantineHold>() & Builders<QuarantineHold>.Filter.Eq(x => x.SourceMovementId, transactionId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<QuarantineHoldUpdateAuditRecord?> FindHoldUpdateAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _audit.Find(Scope<QuarantineHoldUpdateAuditRecord>()
                             & Builders<QuarantineHoldUpdateAuditRecord>.Filter.Eq(x => x.Action, Application.Features.Quarantine.QuarantineContract.HoldUpdatedAuditAction)
                             & Builders<QuarantineHoldUpdateAuditRecord>.Filter.Eq(x => x.UserId, userId)
                             & Builders<QuarantineHoldUpdateAuditRecord>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<QuarantineHold>> FindHoldsByCreateKeysAsync(Guid createdBy, IReadOnlyCollection<string> createKeys, CancellationToken cancellationToken = default)
        => await _holds.Find(Scope<QuarantineHold>() & Builders<QuarantineHold>.Filter.Eq(x => x.CreatedBy, createdBy)
                             & Builders<QuarantineHold>.Filter.In(x => x.CreateIdempotencyKey, createKeys)).ToListAsync(cancellationToken);

    public async Task<HoldPage> ListHoldsAsync(HoldQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<QuarantineHold>.Filter;
        var filter = Scope<QuarantineHold>();
        if (query.WarehouseId is not null) filter &= f.Eq(x => x.WarehouseId, query.WarehouseId);
        if (query.StockStatus is { } status) filter &= f.Eq(x => x.StockStatus, status);
        if (query.Source is { } source) filter &= f.Eq(x => x.Source, source);
        if (query.Status is { } holdStatus) filter &= f.Eq(x => x.Status, holdStatus);
        if (query.ReviewState is { } review) filter &= f.Eq(x => x.ReviewState, review);
        if (query.LotId is not null) filter &= f.Eq(x => x.LotId, query.LotId);
        if (query.EnteredBefore is { } before) filter &= f.Lt(x => x.EnteredAtUtc, before) & f.Ne(x => x.Status, HoldStatus.Decided);
        if (!string.IsNullOrEmpty(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search), "i");
            filter &= f.Or(f.Regex(x => x.HoldNumber, pattern), f.Regex(x => x.LotId, pattern));
        }

        var total = await _holds.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _holds.Find(filter).Sort(Builders<QuarantineHold>.Sort.Ascending(x => x.EnteredAtUtc))
            .Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
        return new HoldPage(items, total);
    }

    public async Task<BlockedStock?> FindBlockedAsync(Guid blockedId, CancellationToken cancellationToken = default)
        => await _blocked.Find(Scope<BlockedStock>() & Builders<BlockedStock>.Filter.Eq(x => x.Id, blockedId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<BlockedStock?> FindBlockedBySourceMovementAsync(string transactionId, CancellationToken cancellationToken = default)
        => await _blocked.Find(Scope<BlockedStock>() & Builders<BlockedStock>.Filter.Eq(x => x.SourceMovementId, transactionId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BlockedStock>> FindBlockedByCreateKeysAsync(Guid createdBy, IReadOnlyCollection<string> createKeys, CancellationToken cancellationToken = default)
        => await _blocked.Find(Scope<BlockedStock>() & Builders<BlockedStock>.Filter.Eq(x => x.CreatedBy, createdBy)
                               & Builders<BlockedStock>.Filter.In(x => x.CreateIdempotencyKey, createKeys)).ToListAsync(cancellationToken);

    public async Task<BlockedPage> ListBlockedAsync(BlockedQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<BlockedStock>.Filter;
        var filter = Scope<BlockedStock>();
        if (query.BlockReasonCode is not null) filter &= f.Eq(x => x.BlockReasonCode, query.BlockReasonCode);
        if (query.PendingOnly) filter &= f.Eq(x => x.PendingDisposition, true);
        if (query.Origin is { } origin) filter &= f.Eq(x => x.Origin, origin);
        if (query.Status is { } status) filter &= f.Eq(x => x.Status, status);
        if (query.WarehouseId is not null) filter &= f.Eq(x => x.WarehouseId, query.WarehouseId);
        if (!string.IsNullOrEmpty(query.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search), "i");
            filter &= f.Or(f.Regex(x => x.BlockedNumber, pattern), f.Regex(x => x.LotId, pattern));
        }

        var total = await _blocked.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _blocked.Find(filter).Sort(Builders<BlockedStock>.Sort.Descending(x => x.BlockedAtUtc))
            .Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
        return new BlockedPage(items, total);
    }

    public async Task<bool> ExistsInAnotherCompanyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        FilterDefinition<T> Elsewhere<T>() where T : EntityBase
            => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Ne(x => x.LegalEntityId, _legalEntityId)
               & Builders<T>.Filter.Eq(x => x.Id, id);
        return await _holds.Find(Elsewhere<QuarantineHold>()).AnyAsync(cancellationToken)
               || await _blocked.Find(Elsewhere<BlockedStock>()).AnyAsync(cancellationToken)
               || await _decisions.Find(Elsewhere<QuarantineDecision>()).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QuarantineHistoryEntry>> HistoryAsync(HistorySubject subject, Guid subjectId, CancellationToken cancellationToken = default)
        => await _history.Find(Scope<QuarantineHistoryEntry>() & Builders<QuarantineHistoryEntry>.Filter.Eq(x => x.SubjectType, subject)
                               & Builders<QuarantineHistoryEntry>.Filter.Eq(x => x.SubjectId, subjectId))
            .Sort(Builders<QuarantineHistoryEntry>.Sort.Ascending(x => x.AtUtc)).ToListAsync(cancellationToken);

    public async Task<QuarantineSettings?> FindSettingsAsync(CancellationToken cancellationToken = default)
        => await _settings.Find(Scope<QuarantineSettings>()).FirstOrDefaultAsync(cancellationToken);

    public async Task<LotReleaseClaim?> FindLotReleaseClaimAsync(string lotId, CancellationToken cancellationToken = default)
        => await _claims.Find(Scope<LotReleaseClaim>() & Builders<LotReleaseClaim>.Filter.Eq(x => x.LotId, lotId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BlockReason>> BlockReasonsAsync(CancellationToken cancellationToken = default)
    {
        if (!SeededTenants.ContainsKey(_tenantId))
        {
            await SeedAsync(cancellationToken);
            SeededTenants[_tenantId] = true;
        }

        return await _reasons.Find(TenantScope()).ToListAsync(cancellationToken);
    }

    /// <summary>CT-Q8: the five mockup reasons, inserted once per tenant (insert-if-absent; an edited seed is never overwritten).</summary>
    private async Task SeedAsync(CancellationToken ct)
    {
        foreach (var (code, names) in BlockReasonSeeds.All)
        {
            var now = DateTimeOffset.UtcNow;
            var u = Builders<BlockReason>.Update;
            try
            {
                await _reasons.UpdateOneAsync(
                    TenantScope() & Builders<BlockReason>.Filter.Eq(x => x.Code, code),
                    u.SetOnInsert(x => x.Id, Guid.NewGuid())
                        .SetOnInsert(x => x.LegalEntityId, null)
                        .SetOnInsert(x => x.CreatedAt, now)
                        .SetOnInsert(x => x.Version, 0)
                        .SetOnInsert(x => x.Names, names.ToDictionary(n => n.Key, n => n.Value))
                        .SetOnInsert(x => x.IsActive, true)
                        .SetOnInsert(x => x.DeletedAt, null),
                    new UpdateOptions { IsUpsert = true },
                    ct);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // a concurrent first read inserted it
            }
        }
    }

    // ── writes ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<T> InTransactionAsync<T>(Func<IQuarantineWriteSession, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
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
                        _retries.Retried("quarantine");
                        _logger.LogInformation("Quarantine transaction re-run after a transient conflict (attempt {Attempt}).", attempt);
                    }

                    await CentralAuditTransaction.EnlistAsync(s, ct: ct); // BL-501: the marked command's central-audit event commits with this transaction or not at all.

                    return await work(new WriteSession(this, s), ct);
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new QuarantineDuplicateException(index);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new QuarantineDuplicateException(index);
        }
    }

    private static string? UniqueIndexOf(ServerErrorCategory? category, string? message)
        => category == ServerErrorCategory.DuplicateKey && message is not null
            ? QuarantineIndexConfiguration.UniqueIndexes.FirstOrDefault(i => message.Contains(i, StringComparison.Ordinal))
            : null;

    private sealed class WriteSession : IQuarantineWriteSession
    {
        private readonly QuarantineRepository _owner;
        private readonly IClientSessionHandle _session;

        public WriteSession(QuarantineRepository owner, IClientSessionHandle session)
        {
            _owner = owner;
            _session = session;
        }

        public async Task<string> NextNumberAsync(string prefix, int year, CancellationToken cancellationToken = default)
        {
            var id = $"{_owner._tenantId:N}:{prefix}:{year.ToString(CultureInfo.InvariantCulture)}";
            var counter = await _owner._sequences.FindOneAndUpdateAsync(
                _session,
                Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Inc("Seq", 1L).SetOnInsert("TenantId", _owner._tenantId.ToString("D")),
                new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
                cancellationToken);
            return $"{prefix}-{year.ToString(CultureInfo.InvariantCulture)}-{counter["Seq"].ToInt64().ToString("D4", CultureInfo.InvariantCulture)}";
        }

        public Task InsertHoldAsync(QuarantineHold hold, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(hold);
            return _owner._holds.InsertOneAsync(_session, hold, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceHoldAsync(QuarantineHold hold, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(hold);
            var result = await _owner._holds.ReplaceOneAsync(_session,
                _owner.Scope<QuarantineHold>() & Builders<QuarantineHold>.Filter.Eq(x => x.Id, hold.Id) & Builders<QuarantineHold>.Filter.Eq(x => x.Version, expectedVersion),
                hold, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertHoldUpdateAuditAsync(QuarantineHoldUpdateAuditRecord record, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(record);
            return _owner._audit.InsertOneAsync(_session, record, cancellationToken: cancellationToken);
        }

        public Task InsertBlockedAsync(BlockedStock blocked, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(blocked);
            return _owner._blocked.InsertOneAsync(_session, blocked, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceBlockedAsync(BlockedStock blocked, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(blocked);
            var result = await _owner._blocked.ReplaceOneAsync(_session,
                _owner.Scope<BlockedStock>() & Builders<BlockedStock>.Filter.Eq(x => x.Id, blocked.Id) & Builders<BlockedStock>.Filter.Eq(x => x.Version, expectedVersion),
                blocked, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertDecisionAsync(QuarantineDecision decision, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(decision);
            return _owner._decisions.InsertOneAsync(_session, decision, cancellationToken: cancellationToken);
        }

        public Task InsertLotReleaseClaimAsync(LotReleaseClaim claim, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(claim);
            return _owner._claims.InsertOneAsync(_session, claim, cancellationToken: cancellationToken);
        }

        public async Task<bool> DropLotReleaseClaimAsync(string lotId, Guid decisionId, CancellationToken cancellationToken = default)
            => (await _owner._claims.DeleteOneAsync(_session, _owner.Scope<LotReleaseClaim>() & Builders<LotReleaseClaim>.Filter.Eq(x => x.LotId, lotId)
                                                             & Builders<LotReleaseClaim>.Filter.Eq(x => x.DecisionId, decisionId),
                cancellationToken: cancellationToken)).DeletedCount == 1;

        public Task InsertDecisionEventAsync(QuarantineDecisionEvent decisionEvent, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(decisionEvent);
            return _owner._events.InsertOneAsync(_session, decisionEvent, cancellationToken: cancellationToken);
        }

        public Task InsertHistoryAsync(IReadOnlyList<QuarantineHistoryEntry> entries, CancellationToken cancellationToken = default)
        {
            if (entries.Count == 0)
            {
                return Task.CompletedTask;
            }

            foreach (var entry in entries) _owner.Stamp(entry);
            return _owner._history.InsertManyAsync(_session, entries, cancellationToken: cancellationToken);
        }

        public async Task<IReadOnlyList<QuarantineDecisionEvent>> DecisionEventsAsync(Guid decisionId, CancellationToken cancellationToken = default)
            => await _owner._events.Find(_session, _owner.Scope<QuarantineDecisionEvent>() & Builders<QuarantineDecisionEvent>.Filter.Eq(x => x.DecisionId, decisionId))
                .ToListAsync(cancellationToken);

        public async Task<bool> SaveSettingsAsync(QuarantineSettings settings, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(settings);
            if (expectedVersion == 0)
            {
                await _owner._settings.InsertOneAsync(_session, settings, cancellationToken: cancellationToken);
                return true;
            }

            var result = await _owner._settings.ReplaceOneAsync(_session,
                _owner.Scope<QuarantineSettings>() & Builders<QuarantineSettings>.Filter.Eq(x => x.Version, expectedVersion), settings, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertBlockReasonAsync(BlockReason reason, CancellationToken cancellationToken = default)
        {
            reason.TenantId = _owner._tenantId;
            reason.LegalEntityId = null;
            return _owner._reasons.InsertOneAsync(_session, reason, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceBlockReasonAsync(BlockReason reason, int expectedVersion, CancellationToken cancellationToken = default)
        {
            reason.TenantId = _owner._tenantId;
            reason.LegalEntityId = null;
            var result = await _owner._reasons.ReplaceOneAsync(_session,
                _owner.TenantScope() & Builders<BlockReason>.Filter.Eq(x => x.Code, reason.Code) & Builders<BlockReason>.Filter.Eq(x => x.Version, expectedVersion),
                reason, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task MarkBlockReasonUsedAsync(string code, DateTimeOffset at, CancellationToken cancellationToken = default)
            => _owner._reasons.UpdateOneAsync(_session,
                _owner.TenantScope() & Builders<BlockReason>.Filter.Eq(x => x.Code, code) & Builders<BlockReason>.Filter.Eq(x => x.FirstUsedAtUtc, null),
                Builders<BlockReason>.Update.Set(x => x.FirstUsedAtUtc, at), cancellationToken: cancellationToken);
    }
}

/// <summary>Insert-only decision reads (the inserts are on the write session).</summary>
public sealed class QuarantineDecisionRepository : IQuarantineDecisionRepository
{
    private readonly IMongoCollection<QuarantineDecision> _decisions;
    private readonly IMongoCollection<QuarantineDecisionEvent> _events;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public QuarantineDecisionRepository(IMongoDatabase database, ITenantContext tenant)
    {
        _decisions = database.GetCollection<QuarantineDecision>(QuarantineCollections.Decisions);
        _events = database.GetCollection<QuarantineDecisionEvent>(QuarantineCollections.DecisionEvents);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<T> Scope<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId)
           & Builders<T>.Filter.Eq(x => x.IsDeleted, false);

    public async Task<QuarantineDecision?> FindAsync(Guid decisionId, CancellationToken cancellationToken = default)
        => await _decisions.Find(Scope<QuarantineDecision>() & Builders<QuarantineDecision>.Filter.Eq(x => x.Id, decisionId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<QuarantineDecision?> FindByIdempotencyKeyAsync(Guid deciderUserId, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _decisions.Find(Scope<QuarantineDecision>() & Builders<QuarantineDecision>.Filter.Eq(x => x.DeciderUserId, deciderUserId)
                                 & Builders<QuarantineDecision>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<QuarantineDecision>> ListByTargetAsync(Guid targetId, CancellationToken cancellationToken = default)
        => await _decisions.Find(Scope<QuarantineDecision>() & Builders<QuarantineDecision>.Filter.Or(
                Builders<QuarantineDecision>.Filter.Eq(x => x.HoldId, targetId), Builders<QuarantineDecision>.Filter.Eq(x => x.BlockedStockId, targetId)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QuarantineDecision>> ListLotReleasesAsync(string lotId, CancellationToken cancellationToken = default)
        => await _decisions.Find(Scope<QuarantineDecision>() & Builders<QuarantineDecision>.Filter.Eq(x => x.Kind, DecisionKind.LotRelease)
                                 & Builders<QuarantineDecision>.Filter.Eq(x => x.LotId, lotId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QuarantineDecisionEvent>> EventsAsync(IReadOnlyCollection<Guid> decisionIds, CancellationToken cancellationToken = default)
        => decisionIds.Count == 0
            ? []
            : await _events.Find(Scope<QuarantineDecisionEvent>() & Builders<QuarantineDecisionEvent>.Filter.In(x => x.DecisionId, decisionIds))
                .ToListAsync(cancellationToken);
}
