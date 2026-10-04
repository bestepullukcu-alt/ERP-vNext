using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductAbbreviationRegisterRepository : IProductAbbreviationRegisterRepository
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<ProductAbbreviationRegisterEntry> _collection;
    private readonly IMongoCollection<ProductAbbreviationHistoryEntry> _history;
    private readonly IMongoCollection<BsonDocument> _productSlots;
    private readonly Guid _tenantId;

    public const string AlreadyDecidedByAnother = "ABBREVIATION_ALREADY_DECIDED_BY_ANOTHER";
    private const string ActiveProductIndexName = "ux_mdm_product_abbreviation_register_tenant_active_product";
    private const string Reclassify = "__RECLASSIFY__";

    public ProductAbbreviationRegisterRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _database = database;
        _collection = database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register");
        _history = database.GetCollection<ProductAbbreviationHistoryEntry>(
            ProductAbbreviationHistoryRepository.CollectionName);
        _productSlots = database.GetCollection<BsonDocument>("mdm_product_abbreviation_product_slots");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetPendingWorkItemsPageAsync(
        ProductAbbreviationPendingCursor? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        // FIX2 A3: the order work started waiting in (a stored integer), then the id as the tie-break — a stable,
        // meaningful order that a keyset cursor can resume exactly, with nothing skipped and nothing repeated.
        var filter = TenantFilter & PendingWorkFilter;
        if (after is not null)
        {
            filter &= Builders<ProductAbbreviationRegisterEntry>.Filter.Or(
                Builders<ProductAbbreviationRegisterEntry>.Filter.Gt(x => x.PendingSinceUtcTicks, after.PendingSinceUtcTicks),
                Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.PendingSinceUtcTicks, after.PendingSinceUtcTicks)
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Gt(x => x.Id, after.Id));
        }

        return await _collection.Find(filter)
            .SortBy(x => x.PendingSinceUtcTicks)
            .ThenBy(x => x.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
        => ids.Count == 0
            ? []
            : await _collection
                .Find(TenantFilter & Builders<ProductAbbreviationRegisterEntry>.Filter.In(x => x.Id, ids))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetByGlobalProductIdAsync(
        Guid globalProductId,
        CancellationToken cancellationToken = default)
        => await _collection
            .Find(TenantFilter
                  & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.GlobalProductId, globalProductId))
            .SortBy(x => x.Id)
            .Limit(500)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasOpenEntryForProductAsync(
        Guid globalProductId,
        Guid exceptEntryId,
        CancellationToken cancellationToken = default)
        => await _collection
            .Find(TenantFilter
                  & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.GlobalProductId, globalProductId)
                  & Builders<ProductAbbreviationRegisterEntry>.Filter.Ne(x => x.Id, exceptEntryId)
                  & Builders<ProductAbbreviationRegisterEntry>.Filter.In(
                      x => x.LifecycleStatus,
                      [ProductAbbreviationLifecycleStatus.ACTIVE, ProductAbbreviationLifecycleStatus.REQUESTED]))
            .Limit(1)
            .AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetInitialPendingWorkItemsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 101)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return await _collection
            .Find(
                TenantFilter
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.LifecycleStatus,
                    ProductAbbreviationLifecycleStatus.REQUESTED)
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.ReplacesEntryId, null))
            // BL-030: DateTimeOffset is stored as a BSON array in this service. An ascending
            // RequestedAtUtc sort can silently order by offset minutes instead of the instant.
            .SortBy(x => x.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetPendingWorkItemsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 101)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return await _collection
            .Find(TenantFilter & PendingWorkFilter)
            .SortBy(x => x.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    private static FilterDefinition<ProductAbbreviationRegisterEntry> PendingWorkFilter
        => Builders<ProductAbbreviationRegisterEntry>.Filter.Or(
            Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                x => x.LifecycleStatus,
                ProductAbbreviationLifecycleStatus.REQUESTED),
            Builders<ProductAbbreviationRegisterEntry>.Filter.And(
                Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.LifecycleStatus,
                    ProductAbbreviationLifecycleStatus.ACTIVE),
                Builders<ProductAbbreviationRegisterEntry>.Filter.Ne(x => x.RetirementRequestId, null)));

    public async Task<ProductAbbreviationRegisterEntry?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationRegisterEntry? result = await _collection
            .Find(TenantFilter & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<ProductAbbreviationRegisterEntry?> GetByAllocationIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationRegisterEntry? result = await _collection.Find(
                TenantFilter & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.AllocationIdempotencyKey,
                    idempotencyKey))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<ProductAbbreviationRegisterEntry?> GetActiveByGlobalProductIdAsync(
        Guid globalProductId,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationRegisterEntry? result = await _collection.Find(
                TenantFilter
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.GlobalProductId, globalProductId)
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.LifecycleStatus,
                    ProductAbbreviationLifecycleStatus.ACTIVE))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<ProductAbbreviationRegisterEntry?> ResolveActiveAsync(
        string normalizedAbbreviation,
        CancellationToken cancellationToken = default)
    {
        ProductAbbreviationRegisterEntry? result = await _collection.Find(
                TenantFilter
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.NormalizedAbbreviation,
                    normalizedAbbreviation)
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.LifecycleStatus,
                    ProductAbbreviationLifecycleStatus.ACTIVE))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    public async Task<ProductAbbreviationRegisterWriteResult> InsertRequestedAsync(
        ProductAbbreviationRegisterEntry entry,
        CancellationToken cancellationToken = default)
    {
        var replay = await GetByAllocationIdempotencyKeyAsync(entry.AllocationIdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            return ReplayOf(replay, entry);
        }

        entry.TenantId = _tenantId;
        entry.IsDeleted = false;
        entry.DeletedAt = null;
        entry.CreatedAt = entry.RequestedAtUtc;
        entry.UpdatedAt = entry.RequestedAtUtc;
        entry.Version = 0;
        entry.LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED;
        entry.PendingSinceUtcTicks = entry.RequestedAtUtc.UtcTicks;

        try
        {
            await _collection.InsertOneAsync(entry, cancellationToken: cancellationToken);
            return new(true, entry);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            replay = await GetByAllocationIdempotencyKeyAsync(entry.AllocationIdempotencyKey, cancellationToken)
                     ?? await GetByIdAsync(entry.Id, cancellationToken);
            if (replay is not null && SameRequest(replay, entry))
            {
                return ReplayOf(replay, entry);
            }

            return new(false, replay, "ABBREVIATION_REGISTER_CONFLICT");
        }
    }

    public async Task<bool> AppendAuditIntentIfAbsentAsync(
        Guid id,
        LocalAuditIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (id == Guid.Empty || intent.IntentId == Guid.Empty || intent.TenantId != _tenantId
            || intent.AggregateType != AuditAggregateType.ProductAbbreviation || intent.AggregateId != id
            || intent.SourceService != AuditIntentContract.SourceService
            || intent.ContractVersion != "mod-0290.audit-intent.v1"
            || AuditIntentTemporalStorage.Validate(intent) != AuditIntentTemporalStorageKind.Current)
        {
            return false;
        }

        var filter = Builders<ProductAbbreviationRegisterEntry>.Filter.And(
            TenantFilter,
            Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(item => item.Id, id),
            // FIX1 S1: an intent for a decision the entry has since moved past is still that decision's intent.
            Builders<ProductAbbreviationRegisterEntry>.Filter.Gte(item => item.Version, intent.PostVersion),
            Builders<ProductAbbreviationRegisterEntry>.Filter.Not(
                Builders<ProductAbbreviationRegisterEntry>.Filter.ElemMatch(
                    item => item.AuditIntents,
                    embedded => embedded.IntentId == intent.IntentId)),
            Builders<ProductAbbreviationRegisterEntry>.Filter.Not(
                Builders<ProductAbbreviationRegisterEntry>.Filter.ElemMatch(
                    item => item.AuditIntentReceipts, receipt => receipt.IntentId == intent.IntentId)));
        var result = await _collection.UpdateOneAsync(
            filter,
            Builders<ProductAbbreviationRegisterEntry>.Update.Push(item => item.AuditIntents, intent),
            cancellationToken: cancellationToken);
        if (result.ModifiedCount == 1)
        {
            return true;
        }

        var stored = await GetByIdAsync(id, cancellationToken);
        if (stored is null) return false;
        var existing = stored.AuditIntents.Where(x => x.IntentId == intent.IntentId).Take(2).ToArray();
        var receipts = stored.AuditIntentReceipts.Where(x => x.IntentId == intent.IntentId).Take(2).ToArray();
        if (existing.Length == 1 && receipts.Length == 0)
        {
            var original = existing[0];
            return original.TenantId == intent.TenantId && original.AggregateId == intent.AggregateId
                && original.AggregateType == intent.AggregateType && original.Operation == intent.Operation
                && original.SourceService == intent.SourceService && original.ContractVersion == intent.ContractVersion
                && original.PreVersion == intent.PreVersion && original.PostVersion == intent.PostVersion
                && original.ActorId == intent.ActorId && original.CorrelationId == intent.CorrelationId
                && original.CausationId == intent.CausationId && original.CommandId == intent.CommandId
                && original.Sequence == intent.Sequence && original.TimestampUtc == intent.TimestampUtc
                && original.EvidenceHash == intent.EvidenceHash && original.SnapshotReference == intent.SnapshotReference
                && original.IdempotencyKey == intent.IdempotencyKey;
        }
        if (existing.Length != 0 || receipts.Length != 1) return false;
        var receipt = receipts[0];
        return receipt.TenantId == intent.TenantId && receipt.SourceService == intent.SourceService
            && receipt.IdempotencyKey == intent.IdempotencyKey && receipt.EvidenceHash == intent.EvidenceHash
            && receipt.ContractVersion == intent.ContractVersion && !string.IsNullOrWhiteSpace(receipt.CentralAcknowledgement)
            && !string.IsNullOrWhiteSpace(receipt.CompactReceiptReference)
            && receipt.CentralIdempotencyKey == AuditIntentContract.BuildCentralIdempotencyKey(
                intent.TenantId, intent.IntentId, intent.ContractVersion);
    }

    public async Task<ProductAbbreviationRegisterWriteResult> TransitionAsync(
        Guid id,
        int expectedVersion,
        ProductAbbreviationLifecycleStatus expectedStatus,
        ProductAbbreviationLifecycleStatus targetStatus,
        string decisionActor,
        string idempotencyKey,
        string? reason,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!IsAllowedTransition(expectedStatus, targetStatus))
        {
            return new(false, null, "ABBREVIATION_TRANSITION_FORBIDDEN");
        }

        var filter = TenantFilter
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, id)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.LifecycleStatus, expectedStatus);
        UpdateDefinition<ProductAbbreviationRegisterEntry> update = Builders<ProductAbbreviationRegisterEntry>.Update
            .Set(x => x.LifecycleStatus, targetStatus)
            .Set(x => x.LastDecisionByCanonicalSubjectId, decisionActor)
            .Set(x => x.LastDecisionIdempotencyKey, idempotencyKey)
            .Set(x => x.LastDecisionReason, reason)
            .Set(x => x.LastDecisionAtUtc, decidedAtUtc)
            .Set(x => x.UpdatedAt, decidedAtUtc)
            .Inc(x => x.Version, 1);
        if (targetStatus == ProductAbbreviationLifecycleStatus.RETIRED)
        {
            update = Builders<ProductAbbreviationRegisterEntry>.Update.Combine(
                update,
                Builders<ProductAbbreviationRegisterEntry>.Update.Set(x => x.RetirementRequestId, null),
                Builders<ProductAbbreviationRegisterEntry>.Update.Set(
                    x => x.RetirementRequestedByCanonicalSubjectId,
                    null),
                Builders<ProductAbbreviationRegisterEntry>.Update.Set(x => x.RetirementRequestedAtUtc, null));
        }

        try
        {
            var updated = await _collection.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<ProductAbbreviationRegisterEntry>
                {
                    ReturnDocument = ReturnDocument.After
                },
                cancellationToken);
            if (updated is not null)
            {
                return new(true, updated);
            }
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            return new(false, null, "ACTIVE_ABBREVIATION_CONFLICT");
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return new(false, null, "ACTIVE_ABBREVIATION_CONFLICT");
        }

        var current = await GetByIdAsync(id, cancellationToken);
        if (current is null)
        {
            return new(false, null, "ABBREVIATION_NOT_FOUND");
        }

        if (current.LifecycleStatus == targetStatus
            && current.LastDecisionIdempotencyKey == idempotencyKey)
        {
            // FIX2 A1: the stored decision is a replay only for the person who made it.
            return string.Equals(current.LastDecisionByCanonicalSubjectId, decisionActor, StringComparison.Ordinal)
                ? new(true, current, IsReplay: true)
                : new(false, current, AlreadyDecidedByAnother);
        }

        return new(false, current, "CONCURRENCY_CONFLICT");
    }

    public async Task<ProductAbbreviationRegisterWriteResult> RequestRetirementAsync(
        Guid id,
        int expectedVersion,
        string retirementRequestId,
        string makerSubjectId,
        string idempotencyKey,
        string? reason,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default,
        Guid? observedPendingCorrection = null)
    {
        // FIX1 B3: the claim the caller judged stale (or absent) must still be the claim — a correction that claimed
        // the entry in between wins, and this request is refused instead of orphaning it.
        var filter = TenantFilter
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, id)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                         x => x.LifecycleStatus,
                         ProductAbbreviationLifecycleStatus.ACTIVE)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.RetirementRequestId, null)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                         x => x.PendingCorrectionEntryId,
                         observedPendingCorrection);
        var update = Builders<ProductAbbreviationRegisterEntry>.Update
            .Set(x => x.RetirementRequestId, retirementRequestId)
            .Set(x => x.RetirementRequestedByCanonicalSubjectId, makerSubjectId)
            .Set(x => x.RetirementRequestedAtUtc, requestedAtUtc)
            .Set(x => x.LastDecisionIdempotencyKey, idempotencyKey)
            .Set(x => x.LastDecisionReason, reason)
            .Set(x => x.UpdatedAt, requestedAtUtc)
            .Inc(x => x.Version, 1);
        var updated = await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<ProductAbbreviationRegisterEntry> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, updated);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        if (current is not null
            && current.RetirementRequestId == retirementRequestId
            && current.LastDecisionIdempotencyKey == idempotencyKey)
        {
            return new(true, current, IsReplay: true);
        }

        return current is null
            ? new(false, null, "ABBREVIATION_NOT_FOUND")
            : new(false, current, "CONCURRENCY_CONFLICT");
    }

    public async Task<ProductAbbreviationRegisterWriteResult> ClearRetirementRequestAsync(
        Guid id,
        int expectedVersion,
        string retirementRequestId,
        string checkerSubjectId,
        string idempotencyKey,
        string? reason,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = TenantFilter
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, id)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                         x => x.LifecycleStatus,
                         ProductAbbreviationLifecycleStatus.ACTIVE)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                         x => x.RetirementRequestId,
                         retirementRequestId);
        var update = Builders<ProductAbbreviationRegisterEntry>.Update
            .Set(x => x.RetirementRequestId, null)
            .Set(x => x.RetirementRequestedByCanonicalSubjectId, null)
            .Set(x => x.RetirementRequestedAtUtc, null)
            .Set(x => x.LastDecisionByCanonicalSubjectId, checkerSubjectId)
            .Set(x => x.LastDecisionIdempotencyKey, idempotencyKey)
            .Set(x => x.LastDecisionReason, reason)
            .Set(x => x.LastDecisionAtUtc, decidedAtUtc)
            .Set(x => x.UpdatedAt, decidedAtUtc)
            .Inc(x => x.Version, 1);
        var updated = await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<ProductAbbreviationRegisterEntry> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, updated);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        if (current is not null
            && current.RetirementRequestId is null
            && current.LastDecisionIdempotencyKey == idempotencyKey)
        {
            return new(true, current, IsReplay: true);
        }

        return current is null
            ? new(false, null, "ABBREVIATION_NOT_FOUND")
            : new(false, current, "CONCURRENCY_CONFLICT");
    }

    public async Task<ProductAbbreviationRegisterWriteResult> ApproveCorrectionAsync(
        ProductAbbreviationCorrectionApproval approval,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approval);
        var formerKey = approval.IdempotencyKey + ":former";
        var replacementKey = approval.IdempotencyKey + ":replacement";
        var former = await GetByIdAsync(approval.FormerEntryId, cancellationToken);
        var replacement = await GetByIdAsync(approval.ReplacementEntryId, cancellationToken);
        if (former is null || replacement is null || replacement.ReplacesEntryId != former.Id
            || former.GlobalProductId != replacement.GlobalProductId)
        {
            return new(false, replacement, "ABBREVIATION_CORRECTION_CONFLICT");
        }

        if (Applied(former, replacement, formerKey, replacementKey))
        {
            return MineOrAnother(replacement, approval.CheckerSubjectId);
        }

        // Only a correction that is STILL requested is approved: a cancelled, rejected or already active one never
        // retires the code it was meant to replace.
        if (replacement.LifecycleStatus != ProductAbbreviationLifecycleStatus.REQUESTED)
        {
            return new(false, replacement, "ABBREVIATION_NOT_PENDING");
        }

        // FIX2 A5: the code being replaced must be ACTIVE. The FIX1 path that "finished" a former entry recognised only
        // by a ':former' key suffix is gone — a client-chosen key could carry that suffix (measured: no such half-done
        // state exists in dev or test data; ABB was never released).
        if (former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE)
        {
            return new(false, replacement, "ABBREVIATION_CORRECTION_FORMER_NOT_ACTIVE");
        }

        if (replacement.Version != approval.ExpectedReplacementVersion
            || former.Version != approval.ExpectedFormerVersion)
        {
            return new(false, replacement, "CONCURRENCY_CONFLICT");
        }

        var formerPost = Decided(former, ProductAbbreviationLifecycleStatus.RETIRED, approval, formerKey);
        var replacementPost = Decided(replacement, ProductAbbreviationLifecycleStatus.ACTIVE, approval, replacementKey);
        var evidence = approval.BuildEvidence(formerPost, replacementPost);

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(new TransactionOptions(
            ReadConcern.Snapshot,
            ReadPreference.Primary,
            WriteConcern.WMajority));
        try
        {
            {
                var retired = await _collection.UpdateOneAsync(
                    session,
                    ById(former.Id)
                    & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Version, approval.ExpectedFormerVersion)
                    & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                        x => x.LifecycleStatus,
                        ProductAbbreviationLifecycleStatus.ACTIVE),
                    DecisionUpdate(ProductAbbreviationLifecycleStatus.RETIRED, approval, formerKey)
                        .Set(x => x.RetirementRequestId, null)
                        .Set(x => x.RetirementRequestedByCanonicalSubjectId, null)
                        .Set(x => x.RetirementRequestedAtUtc, null)
                        .Set(x => x.PendingCorrectionEntryId, null)
                        .Push(x => x.AuditIntents, evidence.FormerIntent),
                    cancellationToken: cancellationToken);
                if (retired.ModifiedCount != 1)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return await AfterAbortAsync(approval, formerKey, replacementKey, cancellationToken);
                }
            }

            var activated = await _collection.UpdateOneAsync(
                session,
                ById(replacement.Id)
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.Version,
                    approval.ExpectedReplacementVersion)
                & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                    x => x.LifecycleStatus,
                    ProductAbbreviationLifecycleStatus.REQUESTED),
                DecisionUpdate(ProductAbbreviationLifecycleStatus.ACTIVE, approval, replacementKey)
                    .Push(x => x.AuditIntents, evidence.ReplacementIntent),
                cancellationToken: cancellationToken);
            if (activated.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return await AfterAbortAsync(approval, formerKey, replacementKey, cancellationToken);
            }

            var histories = new[] { evidence.FormerHistory, evidence.ReplacementHistory };
            foreach (var history in histories)
            {
                history.TenantId = _tenantId;
                history.IsDeleted = false;
                history.CreatedAt = history.OccurredAtUtc;
                history.UpdatedAt = history.OccurredAtUtc;
                history.Version = 0;
            }

            await _history.InsertManyAsync(session, histories, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (MongoException exception)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            var after = await AfterAbortAsync(approval, formerKey, replacementKey, cancellationToken);
            if (after.Succeeded || after.ErrorCode != "CONCURRENCY_CONFLICT")
            {
                return after;
            }

            // FIX2 A4: a lost race (write conflict, transient transaction error) is a conflict the reader resolves by
            // reloading — 409 — exactly as the sibling repositories classify it. 503 only for a real outage.
            return IsDuplicateKey(exception)
                ? new(false, after.Entry, "ABBREVIATION_CORRECTION_CONFLICT")
                : IsLostRace(exception)
                    ? after
                    : new(false, after.Entry, "ABBREVIATION_PERSISTENCE_UNAVAILABLE");
        }

        return new(true, await GetByIdAsync(replacement.Id, cancellationToken) ?? replacementPost);
    }

    private async Task<ProductAbbreviationRegisterWriteResult> AfterAbortAsync(
        ProductAbbreviationCorrectionApproval approval,
        string formerKey,
        string replacementKey,
        CancellationToken cancellationToken)
    {
        var former = await GetByIdAsync(approval.FormerEntryId, cancellationToken);
        var replacement = await GetByIdAsync(approval.ReplacementEntryId, cancellationToken);
        if (former is not null && replacement is not null && Applied(former, replacement, formerKey, replacementKey))
        {
            return MineOrAnother(replacement, approval.CheckerSubjectId);
        }

        if (replacement is not null && replacement.LifecycleStatus != ProductAbbreviationLifecycleStatus.REQUESTED)
        {
            return new(false, replacement, "ABBREVIATION_NOT_PENDING");
        }

        return new(false, replacement, "CONCURRENCY_CONFLICT");
    }

    private static bool Applied(
        ProductAbbreviationRegisterEntry former,
        ProductAbbreviationRegisterEntry replacement,
        string formerKey,
        string replacementKey)
        => replacement.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE
           && replacement.LastDecisionIdempotencyKey == replacementKey
           && former.LifecycleStatus == ProductAbbreviationLifecycleStatus.RETIRED
           && former.LastDecisionIdempotencyKey == formerKey;

    private static ProductAbbreviationRegisterEntry Decided(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationLifecycleStatus status,
        ProductAbbreviationCorrectionApproval approval,
        string key)
    {
        var copy = BsonSerializer.Deserialize<ProductAbbreviationRegisterEntry>(entry.ToBsonDocument());
        copy.LifecycleStatus = status;
        copy.LastDecisionByCanonicalSubjectId = approval.CheckerSubjectId;
        copy.LastDecisionIdempotencyKey = key;
        copy.LastDecisionReason = approval.Reason;
        copy.LastDecisionAtUtc = approval.DecidedAtUtc;
        copy.UpdatedAt = approval.DecidedAtUtc;
        copy.Version = entry.Version + 1;
        if (status == ProductAbbreviationLifecycleStatus.RETIRED)
        {
            copy.RetirementRequestId = null;
            copy.RetirementRequestedByCanonicalSubjectId = null;
            copy.RetirementRequestedAtUtc = null;
            copy.PendingCorrectionEntryId = null;
        }

        return copy;
    }

    private static UpdateDefinition<ProductAbbreviationRegisterEntry> DecisionUpdate(
        ProductAbbreviationLifecycleStatus status,
        ProductAbbreviationCorrectionApproval approval,
        string key)
        => Builders<ProductAbbreviationRegisterEntry>.Update
            .Set(x => x.LifecycleStatus, status)
            .Set(x => x.LastDecisionByCanonicalSubjectId, approval.CheckerSubjectId)
            .Set(x => x.LastDecisionIdempotencyKey, key)
            .Set(x => x.LastDecisionReason, approval.Reason)
            .Set(x => x.LastDecisionAtUtc, approval.DecidedAtUtc)
            .Set(x => x.UpdatedAt, approval.DecidedAtUtc)
            .Inc(x => x.Version, 1);

    private FilterDefinition<ProductAbbreviationRegisterEntry> ById(Guid id)
        => TenantFilter & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, id);

    private static FilterDefinition<ProductAbbreviationRegisterEntry> NoIntent(Guid intentId)
        => Builders<ProductAbbreviationRegisterEntry>.Filter.Not(
            Builders<ProductAbbreviationRegisterEntry>.Filter.ElemMatch(
                item => item.AuditIntents,
                embedded => embedded.IntentId == intentId));

    private static ProductAbbreviationRegisterWriteResult MineOrAnother(
        ProductAbbreviationRegisterEntry entry,
        string actor)
        => string.Equals(entry.LastDecisionByCanonicalSubjectId, actor, StringComparison.Ordinal)
            ? new(true, entry, IsReplay: true)
            : new(false, entry, AlreadyDecidedByAnother);

    /// <summary>FIX2 A4 — the sibling repositories' classification of a lost transaction race.</summary>
    public static bool IsLostRace(MongoException exception) =>
        exception.HasErrorLabel("TransientTransactionError")
        || exception.HasErrorLabel("UnknownTransactionCommitResult")
        || exception is MongoCommandException { Code: 112 }
        || exception is MongoWriteException { WriteError.Code: 112 };

    private static bool IsActiveProductViolation(MongoException exception)
        => IsDuplicateKey(exception) && exception.Message.Contains(ActiveProductIndexName, StringComparison.Ordinal);

    // ══ FIX2 A1 — a decision, its history row and its audit intent: one transaction ══════════════════════════════

    public async Task<ProductAbbreviationRegisterWriteResult> DecideAsync(
        ProductAbbreviationDecision decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var current = await GetByIdAsync(decision.EntryId, cancellationToken);
        if (current is null)
        {
            return new(false, null, "ABBREVIATION_NOT_FOUND");
        }

        var recognised = Recognise(current, decision);
        if (recognised is not null)
        {
            return recognised;
        }

        if (current.Version != decision.ExpectedVersion
            || current.LifecycleStatus != ExpectedStatus(decision.Kind)
            || !RetirementStateHolds(current, decision))
        {
            return new(false, current, "CONCURRENCY_CONFLICT");
        }

        var post = Apply(current, decision);
        var evidence = decision.BuildEvidence(post);
        var history = Stored(evidence.History);
        var filter = ById(current.Id)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Version, decision.ExpectedVersion)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.LifecycleStatus, current.LifecycleStatus)
                     & RetirementFilter(current, decision);
        var outcome = await InTransactionAsync(
            async session =>
            {
                if (decision.Kind == ProductAbbreviationDecisionKind.RequestRetirement
                    && current.PendingCorrectionEntryId is { } claimed
                    && IsPendingCorrectionOf(
                        await _collection.Find(session, ById(claimed)).FirstOrDefaultAsync(cancellationToken),
                        current.Id))
                {
                    return new(false, current, "ABBREVIATION_CORRECTION_PENDING");
                }

                var updated = await _collection.UpdateOneAsync(
                    session,
                    filter,
                    DecisionUpdate(decision).Push(x => x.AuditIntents, evidence.Intent),
                    cancellationToken: cancellationToken);
                if (updated.ModifiedCount != 1)
                {
                    return new(false, current, Reclassify);
                }

                await _history.InsertOneAsync(session, history, cancellationToken: cancellationToken);
                return null;
            },
            async () =>
            {
                var after = await GetByIdAsync(decision.EntryId, cancellationToken);
                if (after is null)
                {
                    return new(false, null, "ABBREVIATION_NOT_FOUND");
                }

                if (Recognise(after, decision) is { } again)
                {
                    return again;
                }

                return decision.Kind == ProductAbbreviationDecisionKind.RequestRetirement
                       && after.PendingCorrectionEntryId is { } pending
                       && IsPendingCorrectionOf(await GetByIdAsync(pending, cancellationToken), after.Id)
                    ? new(false, after, "ABBREVIATION_CORRECTION_PENDING")
                    : new(false, after, "CONCURRENCY_CONFLICT");
            },
            cancellationToken);
        return outcome ?? new(true, await GetByIdAsync(current.Id, cancellationToken) ?? post);
    }

    /// <summary>
    /// The decision this key already wrote, if any: a success for the person who made it, a refusal for anyone else.
    /// Null when this key wrote nothing on the entry.
    /// </summary>
    private static ProductAbbreviationRegisterWriteResult? Recognise(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationDecision decision)
    {
        if (!string.Equals(entry.LastDecisionIdempotencyKey, decision.IdempotencyKey, StringComparison.Ordinal))
        {
            return null;
        }

        var (written, decider) = decision.Kind switch
        {
            ProductAbbreviationDecisionKind.Approve
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE, entry.LastDecisionByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.Reject
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.REJECTED, entry.LastDecisionByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.Cancel
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.CANCELLED, entry.LastDecisionByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.RequestRetirement
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE
                    && entry.RetirementRequestId == decision.IdempotencyKey,
                    entry.RetirementRequestedByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.ApproveRetirement
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.RETIRED, entry.LastDecisionByCanonicalSubjectId),
            ProductAbbreviationDecisionKind.RejectRetirement
                => (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE && entry.RetirementRequestId is null,
                    entry.LastDecisionByCanonicalSubjectId),
            _ => (false, null)
        };
        if (!written)
        {
            return null;
        }

        return string.Equals(decider, decision.ActorSubjectId, StringComparison.Ordinal)
            ? new(true, entry, IsReplay: true)
            : new(false, entry, AlreadyDecidedByAnother);
    }

    private static ProductAbbreviationLifecycleStatus ExpectedStatus(ProductAbbreviationDecisionKind kind) => kind switch
    {
        ProductAbbreviationDecisionKind.Approve
            or ProductAbbreviationDecisionKind.Reject
            or ProductAbbreviationDecisionKind.Cancel => ProductAbbreviationLifecycleStatus.REQUESTED,
        _ => ProductAbbreviationLifecycleStatus.ACTIVE
    };

    private static bool RetirementStateHolds(ProductAbbreviationRegisterEntry entry, ProductAbbreviationDecision decision)
        => decision.Kind switch
        {
            ProductAbbreviationDecisionKind.RequestRetirement => entry.RetirementRequestId is null,
            ProductAbbreviationDecisionKind.ApproveRetirement or ProductAbbreviationDecisionKind.RejectRetirement
                => entry.RetirementRequestId is not null && entry.RetirementRequestId == decision.RetirementRequestId,
            _ => true
        };

    private static FilterDefinition<ProductAbbreviationRegisterEntry> RetirementFilter(
        ProductAbbreviationRegisterEntry current,
        ProductAbbreviationDecision decision) => decision.Kind switch
    {
        // The claim the request judged must still be the claim: a correction that claimed the code meanwhile wins.
        ProductAbbreviationDecisionKind.RequestRetirement
            => Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.RetirementRequestId, null)
               & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                   x => x.PendingCorrectionEntryId,
                   current.PendingCorrectionEntryId),
        ProductAbbreviationDecisionKind.ApproveRetirement or ProductAbbreviationDecisionKind.RejectRetirement
            => Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.RetirementRequestId, decision.RetirementRequestId),
        _ => Builders<ProductAbbreviationRegisterEntry>.Filter.Empty
    };

    private static UpdateDefinition<ProductAbbreviationRegisterEntry> DecisionUpdate(ProductAbbreviationDecision decision)
    {
        var update = Builders<ProductAbbreviationRegisterEntry>.Update
            .Set(x => x.LastDecisionIdempotencyKey, decision.IdempotencyKey)
            .Set(x => x.LastDecisionReason, decision.Reason)
            .Set(x => x.UpdatedAt, decision.DecidedAtUtc)
            .Inc(x => x.Version, 1);
        return decision.Kind switch
        {
            ProductAbbreviationDecisionKind.RequestRetirement => update
                .Set(x => x.RetirementRequestId, decision.IdempotencyKey)
                .Set(x => x.RetirementRequestedByCanonicalSubjectId, decision.ActorSubjectId)
                .Set(x => x.RetirementRequestedAtUtc, decision.DecidedAtUtc)
                .Set(x => x.PendingSinceUtcTicks, decision.DecidedAtUtc.UtcTicks),
            ProductAbbreviationDecisionKind.ApproveRetirement or ProductAbbreviationDecisionKind.RejectRetirement => update
                .Set(x => x.LifecycleStatus, decision.Kind == ProductAbbreviationDecisionKind.ApproveRetirement
                    ? ProductAbbreviationLifecycleStatus.RETIRED
                    : ProductAbbreviationLifecycleStatus.ACTIVE)
                .Set(x => x.RetirementRequestId, null)
                .Set(x => x.RetirementRequestedByCanonicalSubjectId, null)
                .Set(x => x.RetirementRequestedAtUtc, null)
                .Set(x => x.LastDecisionByCanonicalSubjectId, decision.ActorSubjectId)
                .Set(x => x.LastDecisionAtUtc, decision.DecidedAtUtc),
            _ => update
                .Set(x => x.LifecycleStatus, decision.Kind switch
                {
                    ProductAbbreviationDecisionKind.Approve => ProductAbbreviationLifecycleStatus.ACTIVE,
                    ProductAbbreviationDecisionKind.Reject => ProductAbbreviationLifecycleStatus.REJECTED,
                    _ => ProductAbbreviationLifecycleStatus.CANCELLED
                })
                .Set(x => x.LastDecisionByCanonicalSubjectId, decision.ActorSubjectId)
                .Set(x => x.LastDecisionAtUtc, decision.DecidedAtUtc)
        };
    }

    /// <summary>The entry as it will read after <paramref name="decision"/> — the same fields DecisionUpdate writes.</summary>
    private static ProductAbbreviationRegisterEntry Apply(
        ProductAbbreviationRegisterEntry entry,
        ProductAbbreviationDecision decision)
    {
        var copy = BsonSerializer.Deserialize<ProductAbbreviationRegisterEntry>(entry.ToBsonDocument());
        copy.LastDecisionIdempotencyKey = decision.IdempotencyKey;
        copy.LastDecisionReason = decision.Reason;
        copy.UpdatedAt = decision.DecidedAtUtc;
        copy.Version = entry.Version + 1;
        switch (decision.Kind)
        {
            case ProductAbbreviationDecisionKind.RequestRetirement:
                copy.RetirementRequestId = decision.IdempotencyKey;
                copy.RetirementRequestedByCanonicalSubjectId = decision.ActorSubjectId;
                copy.RetirementRequestedAtUtc = decision.DecidedAtUtc;
                copy.PendingSinceUtcTicks = decision.DecidedAtUtc.UtcTicks;
                break;
            case ProductAbbreviationDecisionKind.ApproveRetirement:
            case ProductAbbreviationDecisionKind.RejectRetirement:
                copy.LifecycleStatus = decision.Kind == ProductAbbreviationDecisionKind.ApproveRetirement
                    ? ProductAbbreviationLifecycleStatus.RETIRED
                    : ProductAbbreviationLifecycleStatus.ACTIVE;
                copy.RetirementRequestId = null;
                copy.RetirementRequestedByCanonicalSubjectId = null;
                copy.RetirementRequestedAtUtc = null;
                copy.LastDecisionByCanonicalSubjectId = decision.ActorSubjectId;
                copy.LastDecisionAtUtc = decision.DecidedAtUtc;
                break;
            default:
                copy.LifecycleStatus = decision.Kind switch
                {
                    ProductAbbreviationDecisionKind.Approve => ProductAbbreviationLifecycleStatus.ACTIVE,
                    ProductAbbreviationDecisionKind.Reject => ProductAbbreviationLifecycleStatus.REJECTED,
                    _ => ProductAbbreviationLifecycleStatus.CANCELLED
                };
                copy.LastDecisionByCanonicalSubjectId = decision.ActorSubjectId;
                copy.LastDecisionAtUtc = decision.DecidedAtUtc;
                break;
        }

        return copy;
    }

    // ══ FIX2 A2 / item 6 — a new request, its claim, its history row and its audit intent: one transaction ════════

    public async Task<ProductAbbreviationRegisterWriteResult> RequestEntryAsync(
        ProductAbbreviationRequestWrite write,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        var entry = write.Entry;
        var replay = await GetByAllocationIdempotencyKeyAsync(entry.AllocationIdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            return ReplayOf(replay, entry);
        }

        entry.TenantId = _tenantId;
        entry.IsDeleted = false;
        entry.DeletedAt = null;
        entry.CreatedAt = entry.RequestedAtUtc;
        entry.UpdatedAt = entry.RequestedAtUtc;
        entry.Version = 0;
        entry.LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED;
        entry.PendingSinceUtcTicks = entry.RequestedAtUtc.UtcTicks;
        var evidence = write.BuildEvidence(entry);
        entry.AuditIntents = [evidence.Intent];
        var history = Stored(evidence.History);

        var outcome = await InTransactionAsync(
            async session =>
            {
                if (entry.ReplacesEntryId is { } formerId)
                {
                    var former = await _collection.Find(session, ById(formerId)).FirstOrDefaultAsync(cancellationToken);
                    if (former is null || former.GlobalProductId != entry.GlobalProductId)
                    {
                        return new(false, former, "ABBREVIATION_CORRECTION_CONFLICT");
                    }

                    if (former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE)
                    {
                        return new(false, former, "ABBREVIATION_CORRECTION_REQUIRES_ACTIVE");
                    }

                    if (former.RetirementRequestId is not null)
                    {
                        return new(false, former, "ABBREVIATION_RETIREMENT_PENDING");
                    }

                    if (former.PendingCorrectionEntryId is { } claimed && claimed != entry.Id
                        && IsPendingCorrectionOf(
                            await _collection.Find(session, ById(claimed)).FirstOrDefaultAsync(cancellationToken),
                            former.Id))
                    {
                        return new(false, former, "ABBREVIATION_CORRECTION_PENDING");
                    }

                    // The claim and the correction it names are written together: there is no moment in which the
                    // claim points at an entry that does not exist yet (FIX2 A2).
                    var marked = await _collection.UpdateOneAsync(
                        session,
                        ById(former.Id)
                        & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                            x => x.LifecycleStatus,
                            ProductAbbreviationLifecycleStatus.ACTIVE)
                        & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.RetirementRequestId, null)
                        & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                            x => x.PendingCorrectionEntryId,
                            former.PendingCorrectionEntryId),
                        Builders<ProductAbbreviationRegisterEntry>.Update
                            .Set(x => x.PendingCorrectionEntryId, entry.Id)
                            .Set(x => x.UpdatedAt, entry.RequestedAtUtc),
                        cancellationToken: cancellationToken);
                    if (marked.ModifiedCount != 1)
                    {
                        return new(false, former, Reclassify);
                    }
                }
                else
                {
                    var open = await _collection.Find(
                            session,
                            OpenEntryForProduct(entry.GlobalProductId, entry.Id))
                        .Limit(1)
                        .AnyAsync(cancellationToken);
                    if (open)
                    {
                        return new(false, null, "ABBREVIATION_PRODUCT_ALREADY_HAS_ENTRY");
                    }

                    // The product's slot: two requests for one product both write it, so the second one's
                    // transaction conflicts instead of both passing the check above (item 6).
                    await _productSlots.UpdateOneAsync(
                        session,
                        new BsonDocument("_id", ProductSlotId(entry.GlobalProductId)),
                        new BsonDocument("$set", new BsonDocument
                        {
                            ["TenantId"] = new BsonBinaryData(_tenantId, GuidRepresentation.Standard),
                            ["GlobalProductId"] = new BsonBinaryData(entry.GlobalProductId, GuidRepresentation.Standard),
                            ["LastRequestedEntryId"] = new BsonBinaryData(entry.Id, GuidRepresentation.Standard),
                            ["UpdatedAtUtcTicks"] = entry.RequestedAtUtc.UtcTicks
                        }),
                        new UpdateOptions { IsUpsert = true },
                        cancellationToken);
                }

                await _collection.InsertOneAsync(session, entry, cancellationToken: cancellationToken);
                await _history.InsertOneAsync(session, history, cancellationToken: cancellationToken);
                return null;
            },
            async () =>
            {
                var again = await GetByAllocationIdempotencyKeyAsync(entry.AllocationIdempotencyKey, cancellationToken);
                if (again is not null)
                {
                    return ReplayOf(again, entry);
                }

                if (entry.ReplacesEntryId is { } formerId
                    && await GetByIdAsync(formerId, cancellationToken) is { } former
                    && former.PendingCorrectionEntryId is { } pending && pending != entry.Id
                    && IsPendingCorrectionOf(await GetByIdAsync(pending, cancellationToken), formerId))
                {
                    return new(false, former, "ABBREVIATION_CORRECTION_PENDING");
                }

                return entry.ReplacesEntryId is null
                       && await _collection.Find(OpenEntryForProduct(entry.GlobalProductId, entry.Id)).AnyAsync(cancellationToken)
                    ? new(false, null, "ABBREVIATION_PRODUCT_ALREADY_HAS_ENTRY")
                    : new(false, null, "CONCURRENCY_CONFLICT");
            },
            cancellationToken);
        return outcome ?? new(true, await GetByIdAsync(entry.Id, cancellationToken) ?? entry);
    }

    /// <summary>
    /// A claim counts only while the entry it names is a REQUESTED correction OF THIS code (FIX2 A2): an entry that was
    /// decided, never inserted, or that replaces something else does not block anything.
    /// </summary>
    private static bool IsPendingCorrectionOf(ProductAbbreviationRegisterEntry? claimed, Guid formerId)
        => claimed is { LifecycleStatus: ProductAbbreviationLifecycleStatus.REQUESTED }
           && claimed.ReplacesEntryId == formerId;

    private FilterDefinition<ProductAbbreviationRegisterEntry> OpenEntryForProduct(Guid globalProductId, Guid exceptEntryId)
        => TenantFilter
           & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.GlobalProductId, globalProductId)
           & Builders<ProductAbbreviationRegisterEntry>.Filter.Ne(x => x.Id, exceptEntryId)
           & Builders<ProductAbbreviationRegisterEntry>.Filter.In(
               x => x.LifecycleStatus,
               [ProductAbbreviationLifecycleStatus.ACTIVE, ProductAbbreviationLifecycleStatus.REQUESTED]);

    private string ProductSlotId(Guid globalProductId) => $"{_tenantId:N}|{globalProductId:N}";

    private ProductAbbreviationHistoryEntry Stored(ProductAbbreviationHistoryEntry history)
    {
        history.TenantId = _tenantId;
        history.IsDeleted = false;
        history.CreatedAt = history.OccurredAtUtc;
        history.UpdatedAt = history.OccurredAtUtc;
        history.Version = 0;
        return history;
    }

    /// <summary>
    /// Runs <paramref name="body"/> in one transaction. A non-null answer from the body aborts with that answer (or, for
    /// the internal "reclassify" marker, with <paramref name="afterAbort"/>'s reading of the stored state). A lost race is
    /// read the same way (409); only a real outage is ABBREVIATION_PERSISTENCE_UNAVAILABLE (FIX2 A4). Null = committed.
    /// </summary>
    private async Task<ProductAbbreviationRegisterWriteResult?> InTransactionAsync(
        Func<IClientSessionHandle, Task<ProductAbbreviationRegisterWriteResult?>> body,
        Func<Task<ProductAbbreviationRegisterWriteResult>> afterAbort,
        CancellationToken cancellationToken)
    {
        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(new TransactionOptions(
            ReadConcern.Snapshot,
            ReadPreference.Primary,
            WriteConcern.WMajority));
        try
        {
            var refused = await body(session);
            if (refused is not null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return refused.ErrorCode == Reclassify ? await afterAbort() : refused;
            }

            await session.CommitTransactionAsync(cancellationToken);
            return null;
        }
        catch (OperationCanceledException)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (MongoException exception)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            if (IsActiveProductViolation(exception))
            {
                return new(false, null, "ACTIVE_ABBREVIATION_CONFLICT");
            }

            return IsLostRace(exception) || IsDuplicateKey(exception)
                ? await afterAbort()
                : new(false, null, "ABBREVIATION_PERSISTENCE_UNAVAILABLE");
        }
    }

    private static bool IsDuplicateKey(MongoException exception) => exception switch
    {
        MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoBulkWriteException bulk => bulk.WriteErrors.Any(error => error.Category == ServerErrorCategory.DuplicateKey),
        MongoCommandException command => command.Code == 11000,
        _ => false
    };

    /// <summary>
    /// FIX1 S4 — the same request sent again is a replay only while it is still open. A request that was rejected,
    /// cancelled, approved or retired is CLOSED: sending it again is not a new request and not a success either.
    /// </summary>
    private static ProductAbbreviationRegisterWriteResult ReplayOf(
        ProductAbbreviationRegisterEntry persisted,
        ProductAbbreviationRegisterEntry requested)
    {
        if (!SameRequest(persisted, requested))
        {
            return new(false, persisted, "ABBREVIATION_IDEMPOTENCY_CONFLICT");
        }

        return persisted.LifecycleStatus == ProductAbbreviationLifecycleStatus.REQUESTED
            ? new(true, persisted, IsReplay: true)
            : new(false, persisted, "ABBREVIATION_REQUEST_ALREADY_CLOSED");
    }

    private static bool SameRequest(
        ProductAbbreviationRegisterEntry persisted,
        ProductAbbreviationRegisterEntry requested)
        => persisted.Id == requested.Id
           && persisted.GlobalProductId == requested.GlobalProductId
           && persisted.NormalizedAbbreviation == requested.NormalizedAbbreviation
           && persisted.AllocationLedgerId == requested.AllocationLedgerId
           && persisted.ReplacesEntryId == requested.ReplacesEntryId;

    private static bool IsAllowedTransition(
        ProductAbbreviationLifecycleStatus source,
        ProductAbbreviationLifecycleStatus target)
        => source == ProductAbbreviationLifecycleStatus.REQUESTED
               && target is ProductAbbreviationLifecycleStatus.ACTIVE
                   or ProductAbbreviationLifecycleStatus.REJECTED
                   or ProductAbbreviationLifecycleStatus.CANCELLED
           || source == ProductAbbreviationLifecycleStatus.ACTIVE
               && target == ProductAbbreviationLifecycleStatus.RETIRED;

    private void EnsureIndexes()
    {
        var activeFilter = Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.IsDeleted, false)
                           & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                               x => x.LifecycleStatus,
                               ProductAbbreviationLifecycleStatus.ACTIVE);
        _collection.Indexes.CreateMany(
        [
            new CreateIndexModel<ProductAbbreviationRegisterEntry>(
                Builders<ProductAbbreviationRegisterEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.GlobalProductId),
                new CreateIndexOptions<ProductAbbreviationRegisterEntry>
                {
                    Unique = true,
                    Name = "ux_mdm_product_abbreviation_register_tenant_active_product",
                    PartialFilterExpression = activeFilter
                }),
            new CreateIndexModel<ProductAbbreviationRegisterEntry>(
                Builders<ProductAbbreviationRegisterEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.AllocationIdempotencyKey),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_mdm_product_abbreviation_register_tenant_allocation_command"
                }),
            new CreateIndexModel<ProductAbbreviationRegisterEntry>(
                Builders<ProductAbbreviationRegisterEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.NormalizedAbbreviation)
                    .Ascending(x => x.LifecycleStatus),
                new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_register_tenant_resolution" }),
            // FIX2 A3: the Task Center reads pending work in the order it started waiting.
            new CreateIndexModel<ProductAbbreviationRegisterEntry>(
                Builders<ProductAbbreviationRegisterEntry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PendingSinceUtcTicks)
                    .Ascending(x => x.Id),
                new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_register_tenant_pending_order" })
        ]);
        // Created up front: the first request of a product upserts its slot inside a transaction.
        _productSlots.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("TenantId").Ascending("GlobalProductId"),
            new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_product_slots_tenant_product" }));
    }

    private FilterDefinition<ProductAbbreviationRegisterEntry> TenantFilter
        => Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.TenantId, _tenantId)
           & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.IsDeleted, false);
}
