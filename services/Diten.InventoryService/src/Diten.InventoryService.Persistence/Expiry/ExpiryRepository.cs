using System.Globalization;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Expiry;

/// <summary>
/// MOD-0176 records on MongoDB. Every query starts from the caller's Tenant + LegalEntity and <c>IsDeleted = false</c> — there is
/// no overload without the scope (K3 sabotage (a): drop the company filter → the cross-company test is red). Writes run in ONE
/// multi-document transaction per change; a unique-index refusal surfaces as <see cref="ExpiryDuplicateException"/>. History,
/// events and audit rows are INSERT-ONLY: the write session only calls <c>InsertOne</c> / <c>InsertMany</c> on them. Nothing is
/// deleted.
/// </summary>
public sealed class ExpiryRepository : IExpiryRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<ExpiryPolicy> _policies;
    private readonly IMongoCollection<ExpiryPolicyHistoryEntry> _history;
    private readonly IMongoCollection<FefoExceptionCase> _cases;
    private readonly IMongoCollection<FefoExceptionEvent> _events;
    private readonly IMongoCollection<BsonDocument> _counters;
    private readonly IMongoCollection<ExpiryAuditRecord> _audit;
    private readonly IMongoCollection<LotDateOverride> _overrides;
    private readonly TransactionRetryCounter _retries;
    private readonly ILogger<ExpiryRepository> _logger;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public ExpiryRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenant, TransactionRetryCounter retries, ILogger<ExpiryRepository> logger)
    {
        _client = client;
        _policies = database.GetCollection<ExpiryPolicy>(ExpiryCollections.Policies);
        _history = database.GetCollection<ExpiryPolicyHistoryEntry>(ExpiryCollections.PolicyHistory);
        _cases = database.GetCollection<FefoExceptionCase>(ExpiryCollections.Exceptions);
        _events = database.GetCollection<FefoExceptionEvent>(ExpiryCollections.ExceptionEvents);
        _counters = database.GetCollection<BsonDocument>(ExpiryCollections.Counters);
        _audit = database.GetCollection<ExpiryAuditRecord>(InventoryCollections.Audit);
        _overrides = database.GetCollection<LotDateOverride>(ExpiryCollections.DateOverrides);
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

    public async Task<ExpiryPolicy?> FindPolicyAsync(CancellationToken cancellationToken = default)
        => await _policies.Find(Scope<ExpiryPolicy>()).FirstOrDefaultAsync(cancellationToken);

    public async Task<ExpiryPolicyHistoryPage> PolicyHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Scope<ExpiryPolicyHistoryEntry>();
        var total = await _history.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _history.Find(filter)
            .Sort(Builders<ExpiryPolicyHistoryEntry>.Sort.Descending(x => x.ChangedAtUtc).Ascending(x => x.Field))
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new ExpiryPolicyHistoryPage(items, total);
    }

    public async Task<FefoExceptionCase?> FindCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _cases.Find(Scope<FefoExceptionCase>() & Builders<FefoExceptionCase>.Filter.Eq(x => x.Id, caseId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<FefoExceptionCase?> FindCaseByCreateKeyAsync(Guid openedBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _cases.Find(Scope<FefoExceptionCase>() & Builders<FefoExceptionCase>.Filter.Eq(x => x.OpenedBy, openedBy)
                             & Builders<FefoExceptionCase>.Filter.Eq(x => x.CreateIdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<FefoExceptionCase?> FindActiveCaseByDedupKeyAsync(string dedupKey, CancellationToken cancellationToken = default)
        => await _cases.Find(Scope<FefoExceptionCase>() & Builders<FefoExceptionCase>.Filter.Eq(x => x.DedupKey, dedupKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<ExpiryExceptionPage> ListCasesAsync(ExpiryExceptionQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<FefoExceptionCase>.Filter;
        var filter = Scope<FefoExceptionCase>();
        if (query.Type is { } type) filter &= f.Eq(x => x.Type, type);
        if (query.Statuses is { Count: > 0 } statuses) filter &= f.In(x => x.Status, statuses);
        var awaiting = f.In(x => x.Status, [ExpiryExceptionStatus.Open, ExpiryExceptionStatus.InReview]);
        if (query.Late == true) filter &= awaiting & f.Lt(x => x.DueAtUtc, query.Now);
        if (query.Late == false) filter &= !(awaiting & f.Lt(x => x.DueAtUtc, query.Now));
        if (query.WarehouseId is not null) filter &= f.Eq(x => x.WarehouseId, query.WarehouseId);
        if (query.AssignedTo is { } assignee) filter &= f.Eq(x => x.AssigneeUserId, assignee);
        if (query.Search is not null)
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search), "i");
            filter &= f.Or(f.Regex(x => x.Number, pattern), f.Regex(x => x.LotNumber, pattern));
        }

        var sort = query.SortByDue
            ? Builders<FefoExceptionCase>.Sort.Ascending(x => x.DueAtUtc).Ascending(x => x.Number)
            : Builders<FefoExceptionCase>.Sort.Descending(x => x.OpenedAtUtc).Descending(x => x.Number);
        var total = await _cases.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _cases.Find(filter).Sort(sort).Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);
        return new ExpiryExceptionPage(items, total);
    }

    public async Task<IReadOnlyList<FefoExceptionEvent>> EventsAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _events.Find(Scope<FefoExceptionEvent>() & Builders<FefoExceptionEvent>.Filter.Eq(x => x.CaseId, caseId))
            .Sort(Builders<FefoExceptionEvent>.Sort.Ascending(x => x.Sequence)).ToListAsync(cancellationToken);

    public async Task<FefoExceptionEvent?> FindEventByKeyAsync(Guid caseId, Guid actor, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _events.Find(Scope<FefoExceptionEvent>() & Builders<FefoExceptionEvent>.Filter.Eq(x => x.CaseId, caseId)
                              & Builders<FefoExceptionEvent>.Filter.Eq(x => x.ActorUserId, actor)
                              & Builders<FefoExceptionEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<ExpiryAuditRecord?> FindPolicySaveAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _audit.Find(Scope<ExpiryAuditRecord>()
                             & Builders<ExpiryAuditRecord>.Filter.Eq(x => x.Action, Application.Features.Expiry.ExpiryContract.AuditPolicyUpdated)
                             & Builders<ExpiryAuditRecord>.Filter.Eq(x => x.UserId, userId)
                             & Builders<ExpiryAuditRecord>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<LotDateOverride?> FindDateOverrideByKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _overrides.Find(Scope<LotDateOverride>() & Builders<LotDateOverride>.Filter.Eq(x => x.CreatedBy, createdBy)
                                 & Builders<LotDateOverride>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<LotDateOverride?> FindDateOverrideAsync(Guid overrideId, CancellationToken cancellationToken = default)
        => await _overrides.Find(Scope<LotDateOverride>() & Builders<LotDateOverride>.Filter.Eq(x => x.Id, overrideId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> ExistsInAnotherCompanyAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _cases.Find(Builders<FefoExceptionCase>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<FefoExceptionCase>.Filter.Ne(x => x.LegalEntityId, _legalEntityId)
                             & Builders<FefoExceptionCase>.Filter.Eq(x => x.Id, caseId)).AnyAsync(cancellationToken);

    // ── writes ────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<T> InTransactionAsync<T>(Func<IExpiryWriteSession, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
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
                        _retries.Retried("expiry");
                        _logger.LogInformation("Expiry transaction re-run after a transient conflict (attempt {Attempt}).", attempt);
                    }

                    return work(new WriteSession(this, s), ct);
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new ExpiryDuplicateException(index);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new ExpiryDuplicateException(index);
        }
    }

    internal static string? UniqueIndexOf(ServerErrorCategory? category, string? message)
        => category == ServerErrorCategory.DuplicateKey && message is not null
            ? ExpiryIndexConfiguration.UniqueIndexes.FirstOrDefault(i => message.Contains(i, StringComparison.Ordinal))
            : null;

    private sealed class WriteSession : IExpiryWriteSession
    {
        private readonly ExpiryRepository _owner;
        private readonly IClientSessionHandle _session;

        public WriteSession(ExpiryRepository owner, IClientSessionHandle session)
        {
            _owner = owner;
            _session = session;
        }

        public Task<string> NextNumberAsync(int year, CancellationToken cancellationToken = default)
            => ExpiryNumbers.NextAsync(_owner._counters, _session, _owner._tenantId, year, cancellationToken);

        public async Task<bool> SavePolicyAsync(ExpiryPolicy policy, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(policy);
            if (expectedVersion == 0)
            {
                await _owner._policies.InsertOneAsync(_session, policy, cancellationToken: cancellationToken);
                return true;
            }

            var result = await _owner._policies.ReplaceOneAsync(_session,
                _owner.Scope<ExpiryPolicy>() & Builders<ExpiryPolicy>.Filter.Eq(x => x.Version, expectedVersion), policy, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertPolicyHistoryAsync(IReadOnlyList<ExpiryPolicyHistoryEntry> entries, CancellationToken cancellationToken = default)
        {
            if (entries.Count == 0) return Task.CompletedTask;
            foreach (var entry in entries) _owner.Stamp(entry);
            return _owner._history.InsertManyAsync(_session, entries, cancellationToken: cancellationToken);
        }

        public Task InsertCaseAsync(FefoExceptionCase exceptionCase, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(exceptionCase);
            return _owner._cases.InsertOneAsync(_session, exceptionCase, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceCaseAsync(FefoExceptionCase exceptionCase, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(exceptionCase);
            var result = await _owner._cases.ReplaceOneAsync(_session,
                _owner.Scope<FefoExceptionCase>() & Builders<FefoExceptionCase>.Filter.Eq(x => x.Id, exceptionCase.Id)
                & Builders<FefoExceptionCase>.Filter.Eq(x => x.Version, expectedVersion),
                exceptionCase, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertEventAsync(FefoExceptionEvent exceptionEvent, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(exceptionEvent);
            return _owner._events.InsertOneAsync(_session, exceptionEvent, cancellationToken: cancellationToken);
        }

        public Task InsertAuditAsync(ExpiryAuditRecord record, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(record);
            return _owner._audit.InsertOneAsync(_session, record, cancellationToken: cancellationToken);
        }

        public Task InsertDateOverrideAsync(LotDateOverride dateOverride, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(dateOverride);
            return _owner._overrides.InsertOneAsync(_session, dateOverride, cancellationToken: cancellationToken);
        }
    }
}

/// <summary>The <c>FX-{yyyy}-{nnnn}</c> counter, shared by the user path and the sweeper (one document per tenant + year).</summary>
internal static class ExpiryNumbers
{
    public static async Task<string> NextAsync(
        IMongoCollection<BsonDocument> counters, IClientSessionHandle session, Guid tenantId, int year, CancellationToken cancellationToken)
    {
        var y = year.ToString(CultureInfo.InvariantCulture);
        var counter = await counters.FindOneAndUpdateAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", $"{tenantId:N}:FX:{y}"),
            Builders<BsonDocument>.Update.Inc("Seq", 1L).SetOnInsert("TenantId", tenantId.ToString("D")),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return $"FX-{y}-{counter["Seq"].ToInt64().ToString("D4", CultureInfo.InvariantCulture)}";
    }
}
