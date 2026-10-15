using Diten.BuildingBlocks.CentralAudit;
using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// WP-0177-R5 — the drill store (pack §4.11 / §4.12, §8.10, KR-G). It holds collections of exactly four kinds: the drill collections
/// (<see cref="RecallCollections.SinceR5"/>), the trace snapshots (insert of purpose <c>Drill</c> ONLY), the recall counters and the audit
/// rows. It has no handle on cases, scope items, lot blocks, quarantine requests, notifications, consumptions, return links, ledger or
/// movements — the read-only guarantee is that a drill handler cannot reach a writer of those. Every query is bound to the caller's Tenant +
/// LegalEntity (+ <c>IsDeleted = false</c>); writes run in ONE multi-document transaction (the <see cref="RecallRepository"/> pattern).
/// </summary>
public sealed class RecallDrillRepository : IRecallDrillRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<RecallSettings> _settings;
    private readonly IMongoCollection<RecallDrillPlan> _plans;
    private readonly IMongoCollection<RecallDrillRun> _runs;
    private readonly IMongoCollection<RecallDrillEvent> _events;
    private readonly IMongoCollection<RecallTraceSnapshot> _snapshots;
    private readonly IMongoCollection<BsonDocument> _counters;
    private readonly IMongoCollection<RecallAuditRecord> _audit;
    private readonly TransactionRetryCounter _retries;
    private readonly ILogger<RecallDrillRepository> _logger;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public RecallDrillRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenant, TransactionRetryCounter retries, ILogger<RecallDrillRepository> logger)
    {
        _client = client;
        _settings = database.GetCollection<RecallSettings>(RecallCollections.Settings);
        _plans = database.GetCollection<RecallDrillPlan>(RecallCollections.DrillPlans);
        _runs = database.GetCollection<RecallDrillRun>(RecallCollections.DrillRuns);
        _events = database.GetCollection<RecallDrillEvent>(RecallCollections.DrillEvents);
        _snapshots = database.GetCollection<RecallTraceSnapshot>(RecallCollections.TraceSnapshots);
        _counters = database.GetCollection<BsonDocument>(RecallCollections.Counters);
        _audit = database.GetCollection<RecallAuditRecord>(InventoryCollections.Audit);
        _retries = retries;
        _logger = logger;
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    // ── scope ─────────────────────────────────────────────────────────────────────────────────────────────

    private FilterDefinition<T> Scope<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId)
           & Builders<T>.Filter.Eq(x => x.IsDeleted, false);

    private void Stamp(EntityBase entity)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
    }

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<RecallSettings?> FindSettingsAsync(CancellationToken cancellationToken = default)
        => await _settings.Find(Scope<RecallSettings>()).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallDrillPlan?> FindPlanAsync(Guid planId, CancellationToken cancellationToken = default)
        => await _plans.Find(Scope<RecallDrillPlan>() & Builders<RecallDrillPlan>.Filter.Eq(x => x.Id, planId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallDrillPlan?> FindPlanByCreateKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _plans.Find(Scope<RecallDrillPlan>() & Builders<RecallDrillPlan>.Filter.Eq(x => x.CreatedByUser, createdBy)
                             & Builders<RecallDrillPlan>.Filter.Eq(x => x.CreateIdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallDrillPlan>> ListPlansAsync(CancellationToken cancellationToken = default)
        => await _plans.Find(Scope<RecallDrillPlan>()).SortBy(x => x.Code).ToListAsync(cancellationToken);

    public async Task<RecallDrillRun?> FindRunAsync(Guid runId, CancellationToken cancellationToken = default)
        => await _runs.Find(Scope<RecallDrillRun>() & Builders<RecallDrillRun>.Filter.Eq(x => x.Id, runId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallDrillRun?> FindRunByCreateKeyAsync(Guid startedBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _runs.Find(Scope<RecallDrillRun>() & Builders<RecallDrillRun>.Filter.Eq(x => x.StartedBy, startedBy)
                            & Builders<RecallDrillRun>.Filter.Eq(x => x.CreateIdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallDrillRun?> FindRunningAsync(CancellationToken cancellationToken = default)
        => await _runs.Find(Scope<RecallDrillRun>() & Builders<RecallDrillRun>.Filter.Eq(x => x.Status, RecallDrillRunStatus.Running)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallDrillRunPage> ListRunsAsync(RecallDrillRunQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<RecallDrillRun>.Filter;
        var filter = Scope<RecallDrillRun>();
        if (query.Status is { } status) filter &= f.Eq(x => x.Status, status);
        if (query.From is { } from) filter &= f.Gte(x => x.StartedAtUtc, new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        if (query.To is { } to) filter &= f.Lt(x => x.StartedAtUtc, new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        var total = await _runs.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _runs.Find(filter).SortByDescending(x => x.StartedAtUtc).ThenByDescending(x => x.Number)
            .Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
        return new RecallDrillRunPage(items, total);
    }

    public async Task<RecallDrillRun?> LastCompletedAsync(CancellationToken cancellationToken = default)
        => await _runs.Find(Scope<RecallDrillRun>() & Builders<RecallDrillRun>.Filter.Eq(x => x.Status, RecallDrillRunStatus.Completed))
            .SortByDescending(x => x.CompletedAtUtc).FirstOrDefaultAsync(cancellationToken);

    // ── R6 (reads only) ──

    public async Task<IReadOnlyList<RecallDrillRun>> LastCompletedRunsAsync(int count, CancellationToken cancellationToken = default)
        => await _runs.Find(Scope<RecallDrillRun>() & Builders<RecallDrillRun>.Filter.Eq(x => x.Status, RecallDrillRunStatus.Completed))
            .SortByDescending(x => x.CompletedAtUtc).ThenByDescending(x => x.Number).Limit(count).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallDrillRun>> CaseListRunsAsync(RecallDrillCaseRowQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<RecallDrillRun>.Filter;
        var filter = Scope<RecallDrillRun>();
        if (query.Statuses is { Count: > 0 } statuses) filter &= f.In(x => x.Status, statuses);
        if (query.StartedFrom is { } from) filter &= f.Gte(x => x.StartedAtUtc, from);
        if (query.Search is not null)
        {
            var pattern = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.Search), "i");
            filter &= f.Or(f.Regex(x => x.Number, pattern), f.Regex(x => x.LotNumber, pattern));
        }

        return await _runs.Find(filter).SortByDescending(x => x.StartedAtUtc).ThenByDescending(x => x.Number).ToListAsync(cancellationToken);
    }

    public async Task<RecallDrillEvent?> FindEventByKeyAsync(Guid subjectId, Guid actor, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _events.Find(Scope<RecallDrillEvent>() & Builders<RecallDrillEvent>.Filter.Eq(x => x.SubjectId, subjectId)
                              & Builders<RecallDrillEvent>.Filter.Eq(x => x.ActorUserId, actor)
                              & Builders<RecallDrillEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ExistsInAnotherCompanyAsync(Guid id, CancellationToken cancellationToken = default)
        => await _runs.Find(Builders<RecallDrillRun>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<RecallDrillRun>.Filter.Ne(x => x.LegalEntityId, _legalEntityId)
                            & Builders<RecallDrillRun>.Filter.Eq(x => x.Id, id)).AnyAsync(cancellationToken)
           || await _plans.Find(Builders<RecallDrillPlan>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<RecallDrillPlan>.Filter.Ne(x => x.LegalEntityId, _legalEntityId)
                                & Builders<RecallDrillPlan>.Filter.Eq(x => x.Id, id)).AnyAsync(cancellationToken);

    // ── writes ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<T> InTransactionAsync<T>(Func<IRecallDrillWriteSession, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
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
                        _retries.Retried("recall");
                        _logger.LogInformation("Recall drill transaction re-run after a transient conflict (attempt {Attempt}).", attempt);
                    }

                    await CentralAuditTransaction.EnlistAsync(s, ct: ct); // BL-501: the marked command's central-audit event commits with this transaction or not at all.

                    return await work(new WriteSession(this, s), ct);
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (RecallRepository.UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new RecallDuplicateException(index);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => RecallRepository.UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new RecallDuplicateException(index);
        }
    }

    private sealed class WriteSession : IRecallDrillWriteSession
    {
        private readonly RecallDrillRepository _owner;
        private readonly IClientSessionHandle _session;

        public WriteSession(RecallDrillRepository owner, IClientSessionHandle session)
        {
            _owner = owner;
            _session = session;
        }

        public async Task<string> NextPlanCodeAsync(CancellationToken cancellationToken = default)
        {
            // one DP-NN series per company, in recall_counters (beside the RC / RCD series of the tenant)
            var counter = await _owner._counters.FindOneAndUpdateAsync(
                _session,
                Builders<BsonDocument>.Filter.Eq("_id", $"{_owner._tenantId:N}:DP:{_owner._legalEntityId:N}"),
                Builders<BsonDocument>.Update.Inc("Seq", 1L).SetOnInsert("TenantId", _owner._tenantId.ToString("D")),
                new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
                cancellationToken);
            return $"DP-{counter["Seq"].ToInt64().ToString("D2", CultureInfo.InvariantCulture)}";
        }

        public Task<string> NextRunNumberAsync(int year, CancellationToken cancellationToken = default)
            => RecallNumbers.NextAsync(_owner._counters, _session, _owner._tenantId, "RCD", year, drill: true, cancellationToken);

        public Task InsertPlanAsync(RecallDrillPlan plan, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(plan);
            return _owner._plans.InsertOneAsync(_session, plan, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplacePlanAsync(RecallDrillPlan plan, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(plan);
            var result = await _owner._plans.ReplaceOneAsync(_session,
                _owner.Scope<RecallDrillPlan>() & Builders<RecallDrillPlan>.Filter.Eq(x => x.Id, plan.Id) & Builders<RecallDrillPlan>.Filter.Eq(x => x.Version, expectedVersion),
                plan, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertRunAsync(RecallDrillRun run, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(run);
            return _owner._runs.InsertOneAsync(_session, run, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceRunAsync(RecallDrillRun run, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(run);
            var result = await _owner._runs.ReplaceOneAsync(_session,
                _owner.Scope<RecallDrillRun>() & Builders<RecallDrillRun>.Filter.Eq(x => x.Id, run.Id) & Builders<RecallDrillRun>.Filter.Eq(x => x.Version, expectedVersion),
                run, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertDrillSnapshotAsync(RecallTraceSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (snapshot.Purpose != RecallSnapshotPurpose.Drill || snapshot.DrillRunId is null || snapshot.CaseId != Guid.Empty)
                throw new InvalidOperationException("A drill writes only a trace snapshot of purpose Drill, bound to its run and to no case.");
            _owner.Stamp(snapshot);
            return _owner._snapshots.InsertOneAsync(_session, snapshot, cancellationToken: cancellationToken);
        }

        public Task InsertEventAsync(RecallDrillEvent drillEvent, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(drillEvent);
            return _owner._events.InsertOneAsync(_session, drillEvent, cancellationToken: cancellationToken);
        }

        public Task InsertAuditAsync(RecallAuditRecord record, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(record);
            return _owner._audit.InsertOneAsync(_session, record, cancellationToken: cancellationToken);
        }
    }
}
