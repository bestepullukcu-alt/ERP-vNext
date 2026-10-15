using Diten.BuildingBlocks.CentralAudit;
using System.Globalization;
using System.Text.RegularExpressions;
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
/// MOD-0177 records on MongoDB (the MOD-0176 <c>ExpiryRepository</c> pattern). Every query starts from the caller's Tenant + LegalEntity
/// and <c>IsDeleted = false</c> — there is no overload without the scope (K3 sabotage (a): drop the company filter → the cross-company test
/// is red). The ONE tenant-only read is <see cref="ActiveLotBlocksAsync"/> (CT-R7). Writes run in ONE multi-document transaction per
/// change; a unique-index refusal surfaces as <see cref="RecallDuplicateException"/>. History, events, records and audit rows are
/// INSERT-ONLY: the write session only calls <c>InsertOne</c> / <c>InsertMany</c> on them. Nothing is deleted.
/// </summary>
public sealed class RecallRepository : IRecallRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<RecallSettings> _settings;
    private readonly IMongoCollection<RecallSettingsHistoryEntry> _history;
    private readonly IMongoCollection<RecallCase> _cases;
    private readonly IMongoCollection<RecallCaseEvent> _events;
    private readonly IMongoCollection<RecallScopeItem> _items;
    private readonly IMongoCollection<RecallLotBlock> _blocks;
    private readonly IMongoCollection<RecallCustomerNotification> _notifications;
    private readonly IMongoCollection<RecallConsumptionDeclaration> _consumptions;
    private readonly IMongoCollection<RecallSegregationConfirmation> _segregations;
    private readonly IMongoCollection<BsonDocument> _counters;
    private readonly IMongoCollection<RecallAuditRecord> _audit;
    private readonly IMongoCollection<RecallTraceSnapshot> _snapshots;
    private readonly IMongoCollection<RecallQuarantineRequest> _requests;
    private readonly IMongoCollection<RecallReturnLink> _returnLinks;
    private readonly IMongoCollection<RecallSettingsChange> _settingsChanges;
    private readonly IMongoCollection<RecallDrillEvent> _drillEvents;
    private readonly TransactionRetryCounter _retries;
    private readonly ILogger<RecallRepository> _logger;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public RecallRepository(IMongoClient client, IMongoDatabase database, ITenantContext tenant, TransactionRetryCounter retries, ILogger<RecallRepository> logger)
    {
        _client = client;
        _settings = database.GetCollection<RecallSettings>(RecallCollections.Settings);
        _history = database.GetCollection<RecallSettingsHistoryEntry>(RecallCollections.SettingsHistory);
        _cases = database.GetCollection<RecallCase>(RecallCollections.Cases);
        _events = database.GetCollection<RecallCaseEvent>(RecallCollections.CaseEvents);
        _items = database.GetCollection<RecallScopeItem>(RecallCollections.ScopeItems);
        _blocks = database.GetCollection<RecallLotBlock>(RecallCollections.LotBlocks);
        _notifications = database.GetCollection<RecallCustomerNotification>(RecallCollections.Notifications);
        _consumptions = database.GetCollection<RecallConsumptionDeclaration>(RecallCollections.ConsumptionDeclarations);
        _segregations = database.GetCollection<RecallSegregationConfirmation>(RecallCollections.Segregations);
        _counters = database.GetCollection<BsonDocument>(RecallCollections.Counters);
        _audit = database.GetCollection<RecallAuditRecord>(InventoryCollections.Audit);
        _snapshots = database.GetCollection<RecallTraceSnapshot>(RecallCollections.TraceSnapshots);
        _requests = database.GetCollection<RecallQuarantineRequest>(RecallCollections.QuarantineRequests);
        _returnLinks = database.GetCollection<RecallReturnLink>(RecallCollections.ReturnLinks);
        _settingsChanges = database.GetCollection<RecallSettingsChange>(RecallCollections.SettingsChanges);
        _drillEvents = database.GetCollection<RecallDrillEvent>(RecallCollections.DrillEvents);
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

    public async Task<RecallSettingsHistoryPage> ListSettingsHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Scope<RecallSettingsHistoryEntry>();
        var total = await _history.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _history.Find(filter)
            .Sort(Builders<RecallSettingsHistoryEntry>.Sort.Descending(x => x.ChangedAtUtc).Ascending(x => x.Field))
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new RecallSettingsHistoryPage(items, total);
    }

    public async Task<RecallAuditRecord?> FindSettingsSaveAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _audit.Find(Scope<RecallAuditRecord>()
                             & Builders<RecallAuditRecord>.Filter.Eq(x => x.Action, Application.Features.Recall.RecallContract.AuditSettingsUpdated)
                             & Builders<RecallAuditRecord>.Filter.Eq(x => x.UserId, userId)
                             & Builders<RecallAuditRecord>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallCase?> FindCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _cases.Find(Scope<RecallCase>() & Builders<RecallCase>.Filter.Eq(x => x.Id, caseId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallCase?> FindCaseByCreateKeyAsync(Guid openedBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _cases.Find(Scope<RecallCase>() & Builders<RecallCase>.Filter.Eq(x => x.OwnerUserId, openedBy)
                             & Builders<RecallCase>.Filter.Eq(x => x.CreateIdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallCase?> FindActiveCaseByRootAsync(string activeRootKey, CancellationToken cancellationToken = default)
        => await _cases.Find(Scope<RecallCase>() & Builders<RecallCase>.Filter.Eq(x => x.ActiveRootKey, activeRootKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallCasePage> ListCasesAsync(RecallCaseQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<RecallCase>.Filter;
        var common = Scope<RecallCase>();
        if (query.Class is { } @class) common &= f.Eq(x => x.Class, @class);
        if (query.SkuId is { } sku) common &= f.Eq(x => x.SkuId, sku);
        if (query.OpenedFrom is { } from) common &= f.Gte(x => x.OpenedAtUtc, from);
        if (query.Search is not null)
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search), "i");
            common &= f.Or(f.Regex(x => x.Number, pattern), f.Regex(x => x.RootLotNumber, pattern));
        }

        var filter = query.Statuses is { Count: > 0 } statuses ? common & f.In(x => x.Status, statuses) : common;
        var sort = query.SortByNumber
            ? Builders<RecallCase>.Sort.Ascending(x => x.Number)
            : Builders<RecallCase>.Sort.Descending(x => x.OpenedAtUtc).Descending(x => x.Number);
        var total = await _cases.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _cases.Find(filter).Sort(sort).Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken);

        // the status chips count every status under the other filters (mockup "stCounts")
        var counts = new Dictionary<RecallCaseStatus, long>();
        foreach (var status in Enum.GetValues<RecallCaseStatus>())
        {
            counts[status] = await _cases.CountDocumentsAsync(common & f.Eq(x => x.Status, status), cancellationToken: cancellationToken);
        }

        return new RecallCasePage(items, total, counts);
    }

    public async Task<bool> ExistsInAnotherCompanyAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _cases.Find(Builders<RecallCase>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<RecallCase>.Filter.Ne(x => x.LegalEntityId, _legalEntityId)
                             & Builders<RecallCase>.Filter.Eq(x => x.Id, caseId)).AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallScopeItem>> ScopeItemsAsync(Guid caseId, int revision, CancellationToken cancellationToken = default)
        => await _items.Find(Scope<RecallScopeItem>() & Builders<RecallScopeItem>.Filter.Eq(x => x.CaseId, caseId)
                             & Builders<RecallScopeItem>.Filter.Eq(x => x.Revision, revision))
            .Sort(Builders<RecallScopeItem>.Sort.Ascending(x => x.NodeKey)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallScopeItem>> ScopeItemsWithRemovedAsync(Guid caseId, int revision, CancellationToken cancellationToken = default)
        => await _items.Find(Builders<RecallScopeItem>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<RecallScopeItem>.Filter.Eq(x => x.LegalEntityId, _legalEntityId)
                             & Builders<RecallScopeItem>.Filter.Eq(x => x.CaseId, caseId) & Builders<RecallScopeItem>.Filter.Eq(x => x.Revision, revision))
            .Sort(Builders<RecallScopeItem>.Sort.Ascending(x => x.NodeKey)).ToListAsync(cancellationToken);

    public async Task<RecallTraceSnapshot?> FindSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken = default)
        => await _snapshots.Find(Scope<RecallTraceSnapshot>() & Builders<RecallTraceSnapshot>.Filter.Eq(x => x.Id, snapshotId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallScopeItem>> ScopeItemsOfCasesAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken = default)
        => caseIds.Count == 0
            ? []
            : await _items.Find(Scope<RecallScopeItem>() & Builders<RecallScopeItem>.Filter.In(x => x.CaseId, caseIds)).ToListAsync(cancellationToken);

    public async Task<RecallEventPage> EventsAsync(Guid caseId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = Scope<RecallCaseEvent>() & Builders<RecallCaseEvent>.Filter.Eq(x => x.CaseId, caseId);
        var total = await _events.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _events.Find(filter).Sort(Builders<RecallCaseEvent>.Sort.Ascending(x => x.Sequence))
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return new RecallEventPage(items, total);
    }

    public async Task<IReadOnlyList<RecallCaseEvent>> AllEventsAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _events.Find(Scope<RecallCaseEvent>() & Builders<RecallCaseEvent>.Filter.Eq(x => x.CaseId, caseId))
            .Sort(Builders<RecallCaseEvent>.Sort.Ascending(x => x.Sequence)).ToListAsync(cancellationToken);

    public async Task<RecallCaseEvent?> FindEventByKeyAsync(Guid caseId, Guid actor, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _events.Find(Scope<RecallCaseEvent>() & Builders<RecallCaseEvent>.Filter.Eq(x => x.CaseId, caseId)
                              & Builders<RecallCaseEvent>.Filter.Eq(x => x.ActorUserId, actor)
                              & Builders<RecallCaseEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallCustomerNotification>> NotificationsAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _notifications.Find(Scope<RecallCustomerNotification>() & Builders<RecallCustomerNotification>.Filter.Eq(x => x.CaseId, caseId))
            .Sort(Builders<RecallCustomerNotification>.Sort.Ascending(x => x.RecordedAtUtc)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallConsumptionDeclaration>> ConsumptionsAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _consumptions.Find(Scope<RecallConsumptionDeclaration>() & Builders<RecallConsumptionDeclaration>.Filter.Eq(x => x.CaseId, caseId))
            .Sort(Builders<RecallConsumptionDeclaration>.Sort.Ascending(x => x.RecordedAtUtc)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallSegregationConfirmation>> SegregationsAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _segregations.Find(Scope<RecallSegregationConfirmation>() & Builders<RecallSegregationConfirmation>.Filter.Eq(x => x.CaseId, caseId))
            .Sort(Builders<RecallSegregationConfirmation>.Sort.Ascending(x => x.ConfirmedAtUtc)).ToListAsync(cancellationToken);

    public async Task<RecallCustomerNotification?> FindNotificationByKeyAsync(Guid recordedBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _notifications.Find(Scope<RecallCustomerNotification>() & Builders<RecallCustomerNotification>.Filter.Eq(x => x.RecordedBy, recordedBy)
                                     & Builders<RecallCustomerNotification>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallConsumptionDeclaration?> FindConsumptionByKeyAsync(Guid recordedBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _consumptions.Find(Scope<RecallConsumptionDeclaration>() & Builders<RecallConsumptionDeclaration>.Filter.Eq(x => x.RecordedBy, recordedBy)
                                    & Builders<RecallConsumptionDeclaration>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallSegregationConfirmation?> FindSegregationByKeyAsync(Guid confirmedBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _segregations.Find(Scope<RecallSegregationConfirmation>() & Builders<RecallSegregationConfirmation>.Filter.Eq(x => x.ConfirmedBy, confirmedBy)
                                    & Builders<RecallSegregationConfirmation>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// CT-R7 — tenant-bound ONLY (no company filter, by design): a recalled lot is recalled in every company of the tenant. The case of each
    /// block is read tenant-wide too, for its status and its company (the caller decides what of it another company may see).
    /// </summary>
    public async Task<IReadOnlyList<RecallLotBlockView>> ActiveLotBlocksAsync(string lotId, CancellationToken cancellationToken = default)
    {
        var blocks = await _blocks.Find(Builders<RecallLotBlock>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<RecallLotBlock>.Filter.Eq(x => x.LotId, lotId)
                                        & Builders<RecallLotBlock>.Filter.Eq(x => x.Active, true) & Builders<RecallLotBlock>.Filter.Eq(x => x.IsDeleted, false))
            .Sort(Builders<RecallLotBlock>.Sort.Ascending(x => x.CaseNumber)).ToListAsync(cancellationToken);
        if (blocks.Count == 0) return [];

        var ids = blocks.Select(b => b.CaseId).Distinct().ToList();
        var cases = (await _cases.Find(Builders<RecallCase>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<RecallCase>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken)).ToDictionary(c => c.Id);
        return blocks.Select(b => new RecallLotBlockView(b.LotId, b.CaseId, b.CaseNumber,
            cases.TryGetValue(b.CaseId, out var c) ? c.LegalEntityId : null, cases.TryGetValue(b.CaseId, out var s) ? s.Status : null)).ToList();
    }

    // ── R3 reads ──────────────────────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RecallLotBlock>> LotBlocksOfCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _blocks.Find(Scope<RecallLotBlock>() & Builders<RecallLotBlock>.Filter.Eq(x => x.CaseId, caseId))
            .Sort(Builders<RecallLotBlock>.Sort.Ascending(x => x.LotId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallQuarantineRequest>> QuarantineRequestsAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _requests.Find(Scope<RecallQuarantineRequest>() & Builders<RecallQuarantineRequest>.Filter.Eq(x => x.CaseId, caseId))
            .Sort(Builders<RecallQuarantineRequest>.Sort.Ascending(x => x.Revision).Ascending(x => x.NodeKey)).ToListAsync(cancellationToken);

    public async Task<RecallQuarantineRequest?> FindQuarantineRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        => await _requests.Find(Scope<RecallQuarantineRequest>() & Builders<RecallQuarantineRequest>.Filter.Eq(x => x.Id, requestId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallReturnLink>> ReturnLinksAsync(Guid caseId, CancellationToken cancellationToken = default)
        => await _returnLinks.Find(Scope<RecallReturnLink>() & Builders<RecallReturnLink>.Filter.Eq(x => x.CaseId, caseId))
            .Sort(Builders<RecallReturnLink>.Sort.Ascending(x => x.LinkedAtUtc)).ToListAsync(cancellationToken);

    // ── R6 (Hazırlık Paneli) ──

    public async Task<IReadOnlyList<RecallCase>> OpenCasesAsync(CancellationToken cancellationToken = default)
        => await _cases.Find(Scope<RecallCase>() & Builders<RecallCase>.Filter.Nin(x => x.Status, [RecallCaseStatus.Closed, RecallCaseStatus.Cancelled]))
            .Sort(Builders<RecallCase>.Sort.Descending(x => x.OpenedAtUtc).Descending(x => x.Number)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecallReturnLink>> ReturnLinksOfCasesAsync(IReadOnlyCollection<Guid> caseIds, CancellationToken cancellationToken = default)
        => caseIds.Count == 0
            ? []
            : await _returnLinks.Find(Scope<RecallReturnLink>() & Builders<RecallReturnLink>.Filter.In(x => x.CaseId, caseIds))
                .Sort(Builders<RecallReturnLink>.Sort.Ascending(x => x.LinkedAtUtc)).ToListAsync(cancellationToken);

    public async Task<RecallReturnLink?> FindReturnLinkByTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => await _returnLinks.Find(Scope<RecallReturnLink>() & Builders<RecallReturnLink>.Filter.Eq(x => x.TransactionId, transactionId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallReturnLink?> FindReturnLinkByKeyAsync(Guid linkedBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _returnLinks.Find(Scope<RecallReturnLink>() & Builders<RecallReturnLink>.Filter.Eq(x => x.LinkedBy, linkedBy)
                                   & Builders<RecallReturnLink>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    // ── writes ────────────────────────────────────────────────────────────────────────────────────────────

    // ── R4: prepared settings changes, signature reuse ─────────────────────────────────────────────────────

    public async Task<RecallSettingsChange?> FindSettingsChangeAsync(Guid changeId, CancellationToken cancellationToken = default)
        => await _settingsChanges.Find(Scope<RecallSettingsChange>() & Builders<RecallSettingsChange>.Filter.Eq(x => x.Id, changeId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallSettingsChange?> FindSettingsChangeByKeyAsync(Guid setBy, string idempotencyKey, CancellationToken cancellationToken = default)
        => await _settingsChanges.Find(Scope<RecallSettingsChange>() & Builders<RecallSettingsChange>.Filter.Eq(x => x.SetBy, setBy)
            & Builders<RecallSettingsChange>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey)).FirstOrDefaultAsync(cancellationToken);

    public async Task<RecallSettingsChange?> LatestSettingsChangeAsync(Guid setBy, CancellationToken cancellationToken = default)
        => await _settingsChanges.Find(Scope<RecallSettingsChange>() & Builders<RecallSettingsChange>.Filter.Eq(x => x.SetBy, setBy))
            .SortByDescending(x => x.SetAtUtc).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> SignatureUsedAsync(string signatureId, CancellationToken cancellationToken = default)
        => await _events.Find(Scope<RecallCaseEvent>() & Builders<RecallCaseEvent>.Filter.Eq(x => x.SignatureId, signatureId)).AnyAsync(cancellationToken)
           || await _history.Find(Scope<RecallSettingsHistoryEntry>() & Builders<RecallSettingsHistoryEntry>.Filter.Eq(x => x.SignatureId, signatureId)).AnyAsync(cancellationToken)
           || await _drillEvents.Find(Scope<RecallDrillEvent>() & Builders<RecallDrillEvent>.Filter.Eq(x => x.SignatureId, signatureId)).AnyAsync(cancellationToken); // R5: a drill result

    public async Task<T> InTransactionAsync<T>(Func<IRecallWriteSession, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
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
                        _logger.LogInformation("Recall transaction re-run after a transient conflict (attempt {Attempt}).", attempt);
                    }

                    await CentralAuditTransaction.EnlistAsync(s, ct: ct); // BL-501: the marked command's central-audit event commits with this transaction or not at all.

                    return await work(new WriteSession(this, s), ct);
                },
                new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority),
                cancellationToken);
        }
        catch (MongoWriteException ex) when (UniqueIndexOf(ex.WriteError?.Category, ex.WriteError?.Message) is { } index)
        {
            throw new RecallDuplicateException(index);
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.Select(e => UniqueIndexOf(e.Category, e.Message)).FirstOrDefault(i => i is not null) is { } index)
        {
            throw new RecallDuplicateException(index);
        }
    }

    internal static string? UniqueIndexOf(ServerErrorCategory? category, string? message)
        => category == ServerErrorCategory.DuplicateKey && message is not null
            ? RecallIndexConfiguration.UniqueIndexes.FirstOrDefault(i => message.Contains(i, StringComparison.Ordinal))
            : null;

    private sealed class WriteSession : IRecallWriteSession
    {
        private readonly RecallRepository _owner;
        private readonly IClientSessionHandle _session;

        public WriteSession(RecallRepository owner, IClientSessionHandle session)
        {
            _owner = owner;
            _session = session;
        }

        public Task<string> NextCaseNumberAsync(int year, CancellationToken cancellationToken = default)
            => RecallNumbers.NextAsync(_owner._counters, _session, _owner._tenantId, "RC", year, drill: false, cancellationToken);

        public Task<string> NextDrillNumberAsync(int year, CancellationToken cancellationToken = default)
            => RecallNumbers.NextAsync(_owner._counters, _session, _owner._tenantId, "RCD", year, drill: true, cancellationToken);

        public async Task<bool> SaveSettingsAsync(RecallSettings settings, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(settings);
            if (expectedVersion == 0)
            {
                await _owner._settings.InsertOneAsync(_session, settings, cancellationToken: cancellationToken);
                return true;
            }

            var result = await _owner._settings.ReplaceOneAsync(_session,
                _owner.Scope<RecallSettings>() & Builders<RecallSettings>.Filter.Eq(x => x.Version, expectedVersion), settings, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertSettingsHistoryAsync(IReadOnlyList<RecallSettingsHistoryEntry> entries, CancellationToken cancellationToken = default)
        {
            if (entries.Count == 0) return Task.CompletedTask;
            foreach (var entry in entries) _owner.Stamp(entry);
            return _owner._history.InsertManyAsync(_session, entries, cancellationToken: cancellationToken);
        }

        public Task InsertCaseAsync(RecallCase recallCase, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(recallCase);
            return _owner._cases.InsertOneAsync(_session, recallCase, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceCaseAsync(RecallCase recallCase, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(recallCase);
            var result = await _owner._cases.ReplaceOneAsync(_session,
                _owner.Scope<RecallCase>() & Builders<RecallCase>.Filter.Eq(x => x.Id, recallCase.Id) & Builders<RecallCase>.Filter.Eq(x => x.Version, expectedVersion),
                recallCase, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertScopeItemsAsync(IReadOnlyList<RecallScopeItem> items, CancellationToken cancellationToken = default)
        {
            if (items.Count == 0) return Task.CompletedTask;
            foreach (var item in items) _owner.Stamp(item);
            return _owner._items.InsertManyAsync(_session, items, cancellationToken: cancellationToken);
        }

        public Task ReplaceScopeItemAsync(RecallScopeItem item, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(item);
            // R2: no IsDeleted filter — a re-trace soft-removes an item and may restore it (tenant + company still bind it)
            return _owner._items.ReplaceOneAsync(_session,
                Builders<RecallScopeItem>.Filter.Eq(x => x.TenantId, _owner._tenantId) & Builders<RecallScopeItem>.Filter.Eq(x => x.LegalEntityId, _owner._legalEntityId)
                & Builders<RecallScopeItem>.Filter.Eq(x => x.Id, item.Id), item, cancellationToken: cancellationToken);
        }

        public Task InsertLotBlocksAsync(IReadOnlyList<RecallLotBlock> blocks, CancellationToken cancellationToken = default)
        {
            if (blocks.Count == 0) return Task.CompletedTask;
            foreach (var block in blocks) _owner.Stamp(block);
            return _owner._blocks.InsertManyAsync(_session, blocks, cancellationToken: cancellationToken);
        }

        public async Task<long> ReleaseLotBlocksAsync(Guid caseId, DateTimeOffset at, string reason, CancellationToken cancellationToken = default)
        {
            var result = await _owner._blocks.UpdateManyAsync(_session,
                _owner.Scope<RecallLotBlock>() & Builders<RecallLotBlock>.Filter.Eq(x => x.CaseId, caseId) & Builders<RecallLotBlock>.Filter.Eq(x => x.Active, true),
                Builders<RecallLotBlock>.Update.Set(x => x.Active, false).Set(x => x.ReleasedAtUtc, at).Set(x => x.ReleasedReason, reason).Set(x => x.UpdatedAt, at),
                cancellationToken: cancellationToken);
            return result.ModifiedCount;
        }

        public Task InsertEventAsync(RecallCaseEvent caseEvent, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(caseEvent);
            return _owner._events.InsertOneAsync(_session, caseEvent, cancellationToken: cancellationToken);
        }

        public Task InsertNotificationAsync(RecallCustomerNotification notification, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(notification);
            return _owner._notifications.InsertOneAsync(_session, notification, cancellationToken: cancellationToken);
        }

        public Task InsertConsumptionAsync(RecallConsumptionDeclaration declaration, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(declaration);
            return _owner._consumptions.InsertOneAsync(_session, declaration, cancellationToken: cancellationToken);
        }

        public Task InsertSegregationAsync(RecallSegregationConfirmation confirmation, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(confirmation);
            return _owner._segregations.InsertOneAsync(_session, confirmation, cancellationToken: cancellationToken);
        }

        public Task InsertAuditAsync(RecallAuditRecord record, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(record);
            return _owner._audit.InsertOneAsync(_session, record, cancellationToken: cancellationToken);
        }

        public Task InsertSnapshotAsync(RecallTraceSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(snapshot);
            return _owner._snapshots.InsertOneAsync(_session, snapshot, cancellationToken: cancellationToken);
        }

        public Task InsertQuarantineRequestsAsync(IReadOnlyList<RecallQuarantineRequest> requests, CancellationToken cancellationToken = default)
        {
            if (requests.Count == 0) return Task.CompletedTask;
            foreach (var request in requests) _owner.Stamp(request);
            return _owner._requests.InsertManyAsync(_session, requests, cancellationToken: cancellationToken);
        }

        public async Task<bool> ReplaceQuarantineRequestAsync(RecallQuarantineRequest request, int expectedVersion, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(request);
            var result = await _owner._requests.ReplaceOneAsync(_session,
                _owner.Scope<RecallQuarantineRequest>() & Builders<RecallQuarantineRequest>.Filter.Eq(x => x.Id, request.Id)
                & Builders<RecallQuarantineRequest>.Filter.Eq(x => x.Version, expectedVersion),
                request, cancellationToken: cancellationToken);
            return result.MatchedCount == 1;
        }

        public Task InsertReturnLinkAsync(RecallReturnLink link, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(link);
            return _owner._returnLinks.InsertOneAsync(_session, link, cancellationToken: cancellationToken);
        }

        public Task InsertSettingsChangeAsync(RecallSettingsChange change, CancellationToken cancellationToken = default)
        {
            _owner.Stamp(change);
            return _owner._settingsChanges.InsertOneAsync(_session, change, cancellationToken: cancellationToken);
        }
    }
}

/// <summary>
/// The <c>RC-{yyyy}-{nnnn}</c> (cases) and <c>RC-{yyyy}-D{nnn}</c> (drill runs, pack §4.12) counters — one document per tenant + series +
/// year. The two series are separate (mockup: RC-2026-0004 and RC-2026-D011 coexist).
/// </summary>
internal static class RecallNumbers
{
    public static async Task<string> NextAsync(
        IMongoCollection<BsonDocument> counters, IClientSessionHandle session, Guid tenantId, string series, int year, bool drill, CancellationToken cancellationToken)
    {
        var y = year.ToString(CultureInfo.InvariantCulture);
        var counter = await counters.FindOneAndUpdateAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", $"{tenantId:N}:{series}:{y}"),
            Builders<BsonDocument>.Update.Inc("Seq", 1L).SetOnInsert("TenantId", tenantId.ToString("D")),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken);
        var seq = counter["Seq"].ToInt64();
        return drill
            ? $"RC-{y}-D{seq.ToString("D3", CultureInfo.InvariantCulture)}"
            : $"RC-{y}-{seq.ToString("D4", CultureInfo.InvariantCulture)}";
    }
}
