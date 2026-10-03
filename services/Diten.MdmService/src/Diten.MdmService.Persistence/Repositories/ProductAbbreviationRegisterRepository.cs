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
    private readonly Guid _tenantId;

    public ProductAbbreviationRegisterRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _database = database;
        _collection = database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register");
        _history = database.GetCollection<ProductAbbreviationHistoryEntry>(
            ProductAbbreviationHistoryRepository.CollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<IReadOnlyList<ProductAbbreviationRegisterEntry>> GetPendingWorkItemsPageAsync(
        Guid? afterId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var filter = TenantFilter & PendingWorkFilter;
        if (afterId is { } after)
        {
            filter &= Builders<ProductAbbreviationRegisterEntry>.Filter.Gt(x => x.Id, after);
        }

        return await _collection.Find(filter)
            .SortBy(x => x.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

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

    public async Task<bool> ClaimPendingCorrectionAsync(
        Guid formerEntryId,
        Guid? observedClaim,
        Guid correctionEntryId,
        CancellationToken cancellationToken = default)
    {
        var filter = TenantFilter
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, formerEntryId)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                         x => x.LifecycleStatus,
                         ProductAbbreviationLifecycleStatus.ACTIVE)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.RetirementRequestId, null)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                         x => x.PendingCorrectionEntryId,
                         observedClaim);
        var result = await _collection.UpdateOneAsync(
            filter,
            Builders<ProductAbbreviationRegisterEntry>.Update.Set(x => x.PendingCorrectionEntryId, correctionEntryId),
            cancellationToken: cancellationToken);
        if (result.ModifiedCount == 1)
        {
            return true;
        }

        var current = await GetByIdAsync(formerEntryId, cancellationToken);
        return current is { LifecycleStatus: ProductAbbreviationLifecycleStatus.ACTIVE, RetirementRequestId: null }
               && current.PendingCorrectionEntryId == correctionEntryId;
    }

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
            return new(true, current, IsReplay: true);
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
            return new(true, replacement, IsReplay: true);
        }

        // Only a correction that is STILL requested is approved: a cancelled, rejected or already active one never
        // retires the code it was meant to replace.
        if (replacement.LifecycleStatus != ProductAbbreviationLifecycleStatus.REQUESTED)
        {
            return new(false, replacement, "ABBREVIATION_NOT_PENDING");
        }

        // A former entry this same approval already retired (an interrupted run written before FIX1) is finished,
        // not refused: the marker it carries is the approval's own key.
        var formerAlreadyRetired = former.LifecycleStatus == ProductAbbreviationLifecycleStatus.RETIRED
                                   && former.LastDecisionIdempotencyKey == formerKey;
        if (former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE && !formerAlreadyRetired)
        {
            return new(false, replacement, "ABBREVIATION_CORRECTION_FORMER_NOT_ACTIVE");
        }

        if (replacement.Version != approval.ExpectedReplacementVersion
            || !formerAlreadyRetired && former.Version != approval.ExpectedFormerVersion)
        {
            return new(false, replacement, "CONCURRENCY_CONFLICT");
        }

        var formerPost = formerAlreadyRetired
            ? former
            : Decided(former, ProductAbbreviationLifecycleStatus.RETIRED, approval, formerKey);
        var replacementPost = Decided(replacement, ProductAbbreviationLifecycleStatus.ACTIVE, approval, replacementKey);
        var evidence = approval.BuildEvidence(formerPost, replacementPost);
        var formerHistoryExists = formerAlreadyRetired && await _history
            .Find(Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.TenantId, _tenantId)
                  & Builders<ProductAbbreviationHistoryEntry>.Filter.Eq(x => x.Id, evidence.FormerHistory.Id))
            .AnyAsync(cancellationToken);

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(new TransactionOptions(
            ReadConcern.Snapshot,
            ReadPreference.Primary,
            WriteConcern.WMajority));
        try
        {
            if (formerAlreadyRetired)
            {
                await _collection.UpdateOneAsync(
                    session,
                    ById(former.Id) & NoIntent(evidence.FormerIntent.IntentId),
                    Builders<ProductAbbreviationRegisterEntry>.Update.Push(x => x.AuditIntents, evidence.FormerIntent),
                    cancellationToken: cancellationToken);
            }
            else
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

            var histories = formerHistoryExists
                ? new[] { evidence.ReplacementHistory }
                : new[] { evidence.FormerHistory, evidence.ReplacementHistory };
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

            return IsDuplicateKey(exception)
                ? new(false, after.Entry, "ABBREVIATION_CORRECTION_CONFLICT")
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
            return new(true, replacement, IsReplay: true);
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
                new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_register_tenant_resolution" })
        ]);
    }

    private FilterDefinition<ProductAbbreviationRegisterEntry> TenantFilter
        => Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.TenantId, _tenantId)
           & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.IsDeleted, false);
}
