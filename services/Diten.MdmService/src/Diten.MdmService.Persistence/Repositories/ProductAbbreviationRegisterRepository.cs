using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductAbbreviationRegisterRepository : IProductAbbreviationRegisterRepository
{
    private readonly IMongoCollection<ProductAbbreviationRegisterEntry> _collection;
    private readonly Guid _tenantId;

    public ProductAbbreviationRegisterRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _collection = database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
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

        var requested = Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
            x => x.LifecycleStatus,
            ProductAbbreviationLifecycleStatus.REQUESTED);
        var retirement = Builders<ProductAbbreviationRegisterEntry>.Filter.And(
            Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                x => x.LifecycleStatus,
                ProductAbbreviationLifecycleStatus.ACTIVE),
            Builders<ProductAbbreviationRegisterEntry>.Filter.Ne(x => x.RetirementRequestId, null));

        return await _collection
            .Find(TenantFilter & Builders<ProductAbbreviationRegisterEntry>.Filter.Or(requested, retirement))
            .SortBy(x => x.Id)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

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
            return SameRequest(replay, entry)
                ? new(true, replay, IsReplay: true)
                : new(false, replay, "ABBREVIATION_IDEMPOTENCY_CONFLICT");
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
                return new(true, replay, IsReplay: true);
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
            Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(item => item.Version, intent.PostVersion),
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
        CancellationToken cancellationToken = default)
    {
        var filter = TenantFilter
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Id, id)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(
                         x => x.LifecycleStatus,
                         ProductAbbreviationLifecycleStatus.ACTIVE)
                     & Builders<ProductAbbreviationRegisterEntry>.Filter.Eq(x => x.RetirementRequestId, null);
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

    public async Task<ProductAbbreviationRegisterWriteResult> ReconcileCorrectionApprovalAsync(
        Guid formerEntryId,
        int expectedFormerVersion,
        Guid replacementEntryId,
        int expectedReplacementVersion,
        string checkerSubjectId,
        string idempotencyKey,
        string? reason,
        DateTimeOffset decidedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var former = await GetByIdAsync(formerEntryId, cancellationToken);
        var replacement = await GetByIdAsync(replacementEntryId, cancellationToken);
        if (former is null || replacement is null || replacement.ReplacesEntryId != former.Id
            || former.GlobalProductId != replacement.GlobalProductId)
        {
            return new(false, replacement, "ABBREVIATION_CORRECTION_CONFLICT");
        }

        var formerResult = await TransitionAsync(
            former.Id,
            expectedFormerVersion,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            ProductAbbreviationLifecycleStatus.RETIRED,
            checkerSubjectId,
            idempotencyKey + ":former",
            reason,
            decidedAtUtc,
            cancellationToken);
        if (!formerResult.Succeeded)
        {
            return formerResult;
        }

        var replacementResult = await TransitionAsync(
            replacement.Id,
            expectedReplacementVersion,
            ProductAbbreviationLifecycleStatus.REQUESTED,
            ProductAbbreviationLifecycleStatus.ACTIVE,
            checkerSubjectId,
            idempotencyKey + ":replacement",
            reason,
            decidedAtUtc,
            cancellationToken);
        if (!replacementResult.Succeeded)
        {
            return replacementResult with { ReconciliationRequired = true };
        }

        return replacementResult;
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
