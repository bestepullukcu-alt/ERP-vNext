using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class LskuRepository : ILskuRepository
{
    private const string CollectionName = "mdm_lskus";
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly IMongoCollection<BsonDocument> _documents;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<ProductDefinitionRevision> _revisions;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly Guid _tenantId;

    public LskuRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _lskus = database.GetCollection<Lsku>(CollectionName);
        _documents = database.GetCollection<BsonDocument>(CollectionName);
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _revisions = database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<Lsku?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        (Lsku?)await _lskus.Find(ActiveFilter & Builders<Lsku>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<LskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? search,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        CancellationToken cancellationToken = default)
    {
        var filter = ActiveFilter;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(search);
            filter &= Builders<Lsku>.Filter.Or(
                Builders<Lsku>.Filter.Regex(
                    x => x.CanonicalCode,
                    new BsonRegularExpression("^" + escaped)),
                Builders<Lsku>.Filter.Regex(
                    x => x.MarketCode,
                    new BsonRegularExpression("^" + escaped)));
        }
        if (lifecycleStatus.HasValue)
        {
            filter &= Builders<Lsku>.Filter.Eq(x => x.LifecycleStatus, lifecycleStatus.Value);
        }

        var totalCount = await _lskus.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _lskus.Find(filter)
            .SortBy(x => x.CanonicalCode)
            .ThenBy(x => x.MarketCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new(items, totalCount);
    }

    public async Task<LskuPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? search,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default)
    {
        var match = new BsonDocument
        {
            { nameof(Lsku.TenantId), ProductLegalEntityScopeAggregation.GuidBson(_tenantId) },
            { nameof(Lsku.IsDeleted), false }
        };
        if (lifecycleStatus.HasValue)
        {
            match[nameof(Lsku.LifecycleStatus)] = (int)lifecycleStatus.Value;
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var escaped = System.Text.RegularExpressions.Regex.Escape(search);
            match["$or"] = new BsonArray
            {
                new BsonDocument(nameof(Lsku.CanonicalCode), new BsonRegularExpression("^" + escaped)),
                new BsonDocument(nameof(Lsku.MarketCode), new BsonRegularExpression("^" + escaped))
            };
        }

        var pipeline = new List<BsonDocument> { new("$match", match) };
        pipeline.AddRange(ProductLegalEntityScopeAggregation.CreateGskuRevisionResolutionStages(
            _tenantId,
            "$" + nameof(Lsku.GskuId)));
        pipeline.AddRange(ProductLegalEntityScopeAggregation.CreateAccessStages(
            _tenantId,
            effectiveCandidateLegalEntityIds,
            serverNowUtc));
        pipeline.Add(ProductLegalEntityScopeAggregation.CleanupStage("ScopeGskus", "ScopeRevisions"));
        pipeline.Add(new BsonDocument("$sort", new BsonDocument
        {
            { nameof(Lsku.CanonicalCode), 1 },
            { nameof(Lsku.MarketCode), 1 },
            { "_id", 1 }
        }));
        pipeline.Add(new BsonDocument("$facet", new BsonDocument
        {
            { "items", new BsonArray
                {
                    new BsonDocument("$skip", (pageNumber - 1) * pageSize),
                    new BsonDocument("$limit", pageSize)
                }
            },
            { "summary", new BsonArray { new BsonDocument("$count", "total") } }
        }));

        var result = await _documents.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync(cancellationToken);
        var items = result?["items"].AsBsonArray
            .Select(item => BsonSerializer.Deserialize<Lsku>(item.AsBsonDocument))
            .ToArray() ?? [];
        var total = result?["summary"].AsBsonArray.FirstOrDefault()?.AsBsonDocument["total"].ToInt64() ?? 0;
        return new(items, total);
    }

    public async Task<Lsku?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default) =>
        await _lskus.Find(
                TenantIncludingDeletedFilter
                & Builders<Lsku>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Lsku?> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        await _lskus.Find(
                TenantIncludingDeletedFilter
                & Builders<Lsku>.Filter.Eq(x => x.CodeReservationId, reservationId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Lsku?> GetByIdentityKeyAsync(
        Guid gskuId,
        string marketCode,
        CancellationToken cancellationToken = default) =>
        await _lskus.Find(
                TenantIncludingDeletedFilter
                & Builders<Lsku>.Filter.Eq(x => x.GskuId, gskuId)
                & Builders<Lsku>.Filter.Eq(x => x.MarketCode, marketCode))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<LskuCreateResult> CreateDraftAsync(
        Lsku lsku,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByCreationCommandIdAsync(lsku.CreationCommandId, cancellationToken);
        if (existing is not null)
        {
            return ExistingResult(existing, lsku);
        }

        if (!IsValidContract(lsku))
        {
            return new(false, null, "LSKU_CONTRACT_INVALID");
        }

        var requiredAdmissionFingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            lsku.GskuId, GskuChildIdentityKind.Lsku, lsku.CreationCommandId, lsku.MarketCode);
        var referenceableGskuFilter = Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<Gsku>.Filter.Eq(x => x.Id, lsku.GskuId)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.RetirementOperationId, null)
            & Builders<Gsku>.Filter.ElemMatch(x => x.ChildCreationAdmissions,
                x => x.ChildKind == GskuChildIdentityKind.Lsku
                     && x.CreationCommandId == lsku.CreationCommandId
                     && x.RequestFingerprint == requiredAdmissionFingerprint);
        var parent = await _gskus.Find(referenceableGskuFilter).FirstOrDefaultAsync(cancellationToken);
        if (parent is null || !await _revisions.Find(
                Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, _tenantId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IsDeleted, false)
                & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, parent.ProductDefinitionRevisionId)
                & Builders<ProductDefinitionRevision>.Filter.Eq(
                    x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved))
            .AnyAsync(cancellationToken))
        {
            return new(false, null, "GSKU_NOT_REFERENCEABLE");
        }

        var reservationFilter = Builders<CodeReservation>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<CodeReservation>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, lsku.CodeReservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.Lsku)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, lsku.Id)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, lsku.CanonicalCode)
            & Builders<CodeReservation>.Filter.In(
                x => x.BindingState,
                [CodeReservationBindingState.PendingIdentityWrite, CodeReservationBindingState.Confirmed]);
        if (!await _reservations.Find(reservationFilter).AnyAsync(cancellationToken))
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (lsku.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || lsku.AuditIntents.Any(intent =>
                intent.TenantId != _tenantId
                || intent.AggregateType != AuditAggregateType.Lsku
                || intent.AggregateId != lsku.Id))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        lsku.TenantId = _tenantId;
        lsku.CreatedAt = DateTimeOffset.UtcNow;
        lsku.UpdatedAt = lsku.CreatedAt;
        lsku.IsDeleted = false;
        lsku.DeletedAt = null;
        lsku.Version = 0;

        try
        {
            await _lskus.InsertOneAsync(lsku, cancellationToken: cancellationToken);
            return new(true, lsku);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByCreationCommandIdAsync(lsku.CreationCommandId, cancellationToken);
            if (existing is not null)
            {
                return ExistingResult(existing, lsku);
            }

            existing = await GetByReservationIdAsync(lsku.CodeReservationId, cancellationToken);
            if (existing is not null)
            {
                return ExistingResult(existing, lsku);
            }

            existing = await GetByIdentityKeyAsync(lsku.GskuId, lsku.MarketCode, cancellationToken);
            if (existing is not null)
            {
                return existing.IsDeleted
                    ? new(false, existing, "LSKU_IDENTITY_TOMBSTONED")
                    : new(
                        false,
                        existing,
                        "LSKU_IDENTITY_KEY_CONFLICT",
                        ConflictKind: LskuCreateConflictKind.IdentityKey);
            }

            return new(false, null, "LSKU_DUPLICATE_CONFLICT");
        }
        catch (MongoConnectionException)
        {
            return AmbiguousWrite();
        }
        catch (MongoExecutionTimeoutException)
        {
            return AmbiguousWrite();
        }
        catch (MongoWriteConcernException)
        {
            return AmbiguousWrite();
        }
    }

    public async Task<LskuCreateResult> CreateDraftWithAdmissionAsync(
        Lsku lsku, string admissionFingerprint, CancellationToken cancellationToken = default)
    {
        var expected = GskuChildCreationAdmission.ComputeRequestFingerprint(
            lsku.GskuId, GskuChildIdentityKind.Lsku, lsku.CreationCommandId, lsku.MarketCode);
        if (!string.Equals(admissionFingerprint, expected, StringComparison.Ordinal))
            return new(false, null, "GSKU_CHILD_ADMISSION_REQUIRED");
        return await CreateDraftAsync(lsku, cancellationToken);
    }

    public Task<LskuLifecycleWriteResult> SubmitIdentityAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityWorkflowBinding workflowBinding,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        ApplyLifecycleAsync(
            id,
            expectedVersion,
            ProductIdentityLifecycleStatus.Draft,
            ProductIdentityLifecycleStatus.PendingIdentityApproval,
            ProductAuditOperation.LskuIdentitySubmitted,
            auditIntent,
            workflowBinding,
            null,
            cancellationToken);

    public Task<LskuLifecycleWriteResult> ReconcileIdentityDecisionAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityWorkflowDecisionEvidence decisionEvidence,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisionEvidence);
        var operation = decisionEvidence.Decision switch
        {
            ProductIdentityDecisionKind.Approved => ProductAuditOperation.LskuIdentityApproved,
            ProductIdentityDecisionKind.Rejected => ProductAuditOperation.LskuIdentityRejected,
            _ => (ProductAuditOperation?)null
        };
        if (operation is null || !ValidDecision(id, decisionEvidence))
        {
            return Task.FromResult(new LskuLifecycleWriteResult(
                false, null, "LSKU_IDENTITY_DECISION_INVALID"));
        }

        return ApplyLifecycleAsync(
            id,
            expectedVersion,
            ProductIdentityLifecycleStatus.PendingIdentityApproval,
            decisionEvidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft,
            operation.Value,
            auditIntent,
            null,
            decisionEvidence,
            cancellationToken);
    }

    public Task<LskuLifecycleWriteResult> RetireIdentityAsync(
        Guid id,
        int expectedVersion,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        RetireIdentityCoreAsync(id, expectedVersion, null, auditIntent, cancellationToken);

    public async Task<LskuLifecycleWriteResult> AcquireLifecycleOperationAsync(
        Guid id, int expectedVersion, LskuActiveLifecycleOperationBinding binding,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || binding.OperationId == Guid.Empty || binding.BaseLskuVersion != expectedVersion
            || binding.Kind != LskuLifecycleOperationKind.Retirement
            || !ValidAudit(id, expectedVersion, ProductAuditOperation.LskuRetirementRequested, auditIntent))
            return new(false, null, "LSKU_LIFECYCLE_OPERATION_CONTRACT_INVALID");
        var replay = await FindLifecycleReplayAsync(id, auditIntent, cancellationToken);
        if (replay is not null)
            return replay.Succeeded && replay.Lsku?.ActiveLifecycleOperation != binding
                ? new(false, replay.Lsku, "LSKU_LIFECYCLE_OPERATION_IDEMPOTENCY_CONFLICT")
                : replay;
        var updated = await _lskus.FindOneAndUpdateAsync(
            ActiveFilter & Builders<Lsku>.Filter.Eq(x => x.Id, id)
            & Builders<Lsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Lsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Lsku>.Filter.Eq(x => x.ActiveLifecycleOperation, null)
            & Builders<Lsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate)
            & new BsonDocumentFilterDefinition<Lsku>(new BsonDocument("$expr", new BsonDocument("$lt",
                new BsonArray { new BsonDocument("$bsonSize", "$$ROOT"), 1024 * 1024 - 4096 }))),
            Builders<Lsku>.Update.Set(x => x.ActiveLifecycleOperation, binding)
                .Set(x => x.UpdatedAt, auditIntent.TimestampUtc).Inc(x => x.Version, 1)
                .Push(x => x.AuditIntents, auditIntent),
            new FindOneAndUpdateOptions<Lsku> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is null ? new(false, await GetByIdAsync(id, cancellationToken),
            "LSKU_LIFECYCLE_OPERATION_CONFLICT") : new(true, updated);
    }

    public async Task<LskuLifecycleWriteResult> ApplyRetirementDecisionAsync(
        Guid id, int expectedVersion, LskuActiveLifecycleOperationBinding binding, bool approved,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        var operation = approved ? ProductAuditOperation.LskuIdentityRetired : ProductAuditOperation.LskuRetirementRejected;
        if (id == Guid.Empty || binding.OperationId == Guid.Empty
            || !ValidAudit(id, expectedVersion, operation, auditIntent))
            return new(false, null, "LSKU_RETIREMENT_DECISION_CONTRACT_INVALID");
        if (approved)
            return await RetireIdentityCoreAsync(id, expectedVersion, binding, auditIntent, cancellationToken);
        var replay = await FindLifecycleReplayAsync(id, auditIntent, cancellationToken);
        if (replay is not null) return replay;
        var updated = await _lskus.FindOneAndUpdateAsync(
            ActiveFilter & Builders<Lsku>.Filter.Eq(x => x.Id, id)
            & Builders<Lsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Lsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Lsku>.Filter.Eq(x => x.ActiveLifecycleOperation, binding)
            & Builders<Lsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate)
            & new BsonDocumentFilterDefinition<Lsku>(new BsonDocument("$expr", new BsonDocument("$lt",
                new BsonArray { new BsonDocument("$bsonSize", "$$ROOT"), 1024 * 1024 - 4096 }))),
            Builders<Lsku>.Update
                .Set(x => x.LifecycleStatus, approved ? ProductIdentityLifecycleStatus.Retired
                    : ProductIdentityLifecycleStatus.IdentityApproved)
                .Set(x => x.ActiveLifecycleOperation, null)
                .Set(x => x.UpdatedAt, auditIntent.TimestampUtc).Inc(x => x.Version, 1)
                .Push(x => x.AuditIntents, auditIntent),
            new FindOneAndUpdateOptions<Lsku> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is null ? new(false, await GetByIdAsync(id, cancellationToken),
            "LSKU_RETIREMENT_DECISION_CONFLICT") : new(true, updated);
    }

    private async Task<LskuLifecycleWriteResult> RetireIdentityCoreAsync(Guid id, int expectedVersion,
        LskuActiveLifecycleOperationBinding? requiredBinding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || expectedVersion < 0
            || requiredBinding is not null && (requiredBinding.OperationId == Guid.Empty
                || requiredBinding.Kind != LskuLifecycleOperationKind.Retirement)
            || !ValidAudit(id, expectedVersion, ProductAuditOperation.LskuIdentityRetired, auditIntent))
            return new(false, null, "LSKU_IDENTITY_LIFECYCLE_CONTRACT_INVALID");
        var replay = await FindLifecycleReplayAsync(id, auditIntent, cancellationToken);
        if (replay is not null) return replay;
        var filter = ActiveFilter & Builders<Lsku>.Filter.Eq(x => x.Id, id)
            & Builders<Lsku>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<Lsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & (requiredBinding is null
                ? Builders<Lsku>.Filter.Eq(x => x.ActiveLifecycleOperation, null)
                : Builders<Lsku>.Filter.Eq(x => x.ActiveLifecycleOperation, requiredBinding))
            & Builders<Lsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate)
            & new BsonDocumentFilterDefinition<Lsku>(new BsonDocument("$expr", new BsonDocument("$lt",
                new BsonArray { new BsonDocument("$bsonSize", "$$ROOT"), 1024 * 1024 - 4096 })));
        var update = Builders<Lsku>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired)
            .Set(x => x.ActiveLifecycleOperation, null).Set(x => x.UpdatedAt, auditIntent.TimestampUtc)
            .Inc(x => x.Version, 1).Push(x => x.AuditIntents, auditIntent);
        var updated = await _lskus.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<Lsku> { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is null ? new(false, await GetByIdAsync(id, cancellationToken),
            "LSKU_IDENTITY_LIFECYCLE_CONFLICT") : new(true, updated);
    }

    private async Task<LskuLifecycleWriteResult> ApplyLifecycleAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityLifecycleStatus sourceStatus,
        ProductIdentityLifecycleStatus targetStatus,
        ProductAuditOperation operation,
        LocalAuditIntent auditIntent,
        ProductIdentityWorkflowBinding? workflowBinding,
        ProductIdentityWorkflowDecisionEvidence? decisionEvidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditIntent);
        cancellationToken.ThrowIfCancellationRequested();
        if (id == Guid.Empty || expectedVersion < 0
            || !ValidAudit(id, expectedVersion, operation, auditIntent)
            || workflowBinding is not null && !ValidBinding(id, workflowBinding))
        {
            return new(false, null, "LSKU_IDENTITY_LIFECYCLE_CONTRACT_INVALID");
        }

        var replay = await FindLifecycleReplayAsync(id, auditIntent, cancellationToken);
        if (replay is not null)
        {
            if (workflowBinding is not null
                && !SameBinding(replay.Lsku?.IdentityWorkflowBinding, workflowBinding)
                || decisionEvidence is not null
                && !SameDecision(replay.Lsku?.IdentityWorkflowBinding?.TerminalDecision, decisionEvidence))
            {
                return new(false, replay.Lsku, "LSKU_IDENTITY_IDEMPOTENCY_CONFLICT");
            }
            return replay;
        }

        var filter = ActiveFilter
                     & Builders<Lsku>.Filter.Eq(x => x.Id, id)
                     & Builders<Lsku>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<Lsku>.Filter.Eq(x => x.LifecycleStatus, sourceStatus)
                     & Builders<Lsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate)
                     & new BsonDocumentFilterDefinition<Lsku>(new BsonDocument(
                         "$expr",
                         new BsonDocument("$lt", new BsonArray
                         {
                             new BsonDocument("$bsonSize", "$$ROOT"),
                             1024 * 1024 - 4096
                         })));
        if (decisionEvidence is not null)
        {
            filter &= Builders<Lsku>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.WorkflowInstanceId,
                          decisionEvidence.WorkflowInstanceId)
                      & Builders<Lsku>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.ApprovalTaskId,
                          decisionEvidence.ApprovalTaskId)
                      & Builders<Lsku>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.WorkflowTemplateId,
                          decisionEvidence.WorkflowTemplateId)
                      & Builders<Lsku>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.WorkflowTemplateVersionId,
                          decisionEvidence.WorkflowTemplateVersionId)
                      & Builders<Lsku>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.ObjectType,
                          decisionEvidence.ObjectType)
                      & Builders<Lsku>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.ObjectId,
                          decisionEvidence.ObjectId)
                      & Builders<Lsku>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.TerminalDecision,
                          null)
                      & Builders<Lsku>.Filter.Ne(
                          x => x.IdentityWorkflowBinding!.SubmitterSubjectId,
                          decisionEvidence.DecisionActorSubjectId);
        }

        var update = Builders<Lsku>.Update
            .Set(x => x.LifecycleStatus, targetStatus)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, auditIntent);
        if (workflowBinding is not null)
        {
            update = update.Set(x => x.IdentityWorkflowBinding, workflowBinding);
        }
        if (decisionEvidence is not null)
        {
            update = update.Set(x => x.IdentityWorkflowBinding!.TerminalDecision, decisionEvidence);
        }

        var updated = await _lskus.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Lsku> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, updated);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        if (current is null)
        {
            return new(false, null, "LSKU_IDENTITY_NOT_FOUND");
        }
        if (current.Version != expectedVersion)
        {
            return new(false, current, "LSKU_IDENTITY_VERSION_CONFLICT");
        }
        if (current.AuditIntents.Count >= AuditIntentLimits.MaxPerAggregate)
        {
            return new(false, current, "AUDIT_INTENT_CAPACITY_EXCEEDED");
        }
        if (current.ToBson().Length >= 1024 * 1024 - 4096)
        {
            return new(false, current, "LSKU_DOCUMENT_SIZE_LIMIT_EXCEEDED");
        }
        return new(false, current,
            decisionEvidence is not null
            && current.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
                ? "WORKFLOW_BINDING_CONFLICT"
                : "LSKU_IDENTITY_LIFECYCLE_CONFLICT");
    }

    private async Task<LskuLifecycleWriteResult?> FindLifecycleReplayAsync(
        Guid id,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken)
    {
        var current = await _lskus.Find(
                ActiveFilter
                & Builders<Lsku>.Filter.Eq(x => x.Id, id)
                & Builders<Lsku>.Filter.Or(
                    Builders<Lsku>.Filter.ElemMatch(
                        x => x.AuditIntents,
                        intent => intent.IntentId == auditIntent.IntentId),
                    Builders<Lsku>.Filter.ElemMatch(
                        x => x.AuditIntentReceipts,
                        receipt => receipt.IntentId == auditIntent.IntentId)))
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            return null;
        }
        var stored = current.AuditIntents.Where(x => x.IntentId == auditIntent.IntentId).Take(2).ToArray();
        var receipts = current.AuditIntentReceipts
            .Where(x => x.IntentId == auditIntent.IntentId).Take(2).ToArray();
        var exactIntent = stored.Length == 1 && receipts.Length == 0 && SameAudit(stored[0], auditIntent);
        var exactReceipt = stored.Length == 0 && receipts.Length == 1
            && receipts[0].TenantId == auditIntent.TenantId
            && receipts[0].SourceService == auditIntent.SourceService
            && receipts[0].IdempotencyKey == auditIntent.IdempotencyKey
            && receipts[0].EvidenceHash == auditIntent.EvidenceHash
            && ExpectedReplayStatus(auditIntent.Operation) == current.LifecycleStatus;
        return exactIntent || exactReceipt
            ? new(true, current, IsReplay: true)
            : new(false, current, "LSKU_IDENTITY_IDEMPOTENCY_CONFLICT");
    }

    private static ProductIdentityLifecycleStatus ExpectedReplayStatus(ProductAuditOperation operation) =>
        operation switch
        {
            ProductAuditOperation.LskuIdentitySubmitted =>
                ProductIdentityLifecycleStatus.PendingIdentityApproval,
            ProductAuditOperation.LskuIdentityApproved =>
                ProductIdentityLifecycleStatus.IdentityApproved,
            ProductAuditOperation.LskuIdentityRejected =>
                ProductIdentityLifecycleStatus.Draft,
            ProductAuditOperation.LskuIdentityRetired =>
                ProductIdentityLifecycleStatus.Retired,
            ProductAuditOperation.LskuRetirementRequested =>
                ProductIdentityLifecycleStatus.IdentityApproved,
            ProductAuditOperation.LskuRetirementRejected =>
                ProductIdentityLifecycleStatus.IdentityApproved,
            _ => (ProductIdentityLifecycleStatus)(-1)
        };

    private static LskuCreateResult ExistingResult(Lsku existing, Lsku requested)
    {
        if (existing.IsDeleted)
        {
            return new(false, existing, "LSKU_IDENTITY_TOMBSTONED");
        }

        return SameFacts(existing, requested)
            ? new(true, existing)
            : new(
                false,
                existing,
                "LSKU_DUPLICATE_CONFLICT",
                ConflictKind: LskuCreateConflictKind.CommandOrPayload);
    }

    private static bool IsValidContract(Lsku lsku) =>
        lsku.Id != Guid.Empty
        && lsku.GskuId != Guid.Empty
        && lsku.CodeReservationId != Guid.Empty
        && !string.IsNullOrWhiteSpace(lsku.CanonicalCode)
        && !string.IsNullOrWhiteSpace(lsku.CreationCommandId)
        && IsExactIsoAlpha2(lsku.MarketCode)
        && lsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
        && string.Equals(lsku.MarketSelection.SetCode, "market", StringComparison.Ordinal)
        && string.Equals(lsku.MarketSelection.ValueCode, lsku.MarketCode, StringComparison.Ordinal)
        && lsku.MarketSelection.CatalogVersionId != Guid.Empty
        && lsku.MarketSelection.CatalogVersionNumber > 0
        && lsku.MarketSelection.ResolutionMode == ReferenceCatalogResolutionMode.Latest
        && lsku.MarketSelection.ResolvedAtUtc != default;

    private static bool IsExactIsoAlpha2(string? value) =>
        value is { Length: 2 }
        && value[0] is >= 'A' and <= 'Z'
        && value[1] is >= 'A' and <= 'Z';

    private static LskuCreateResult AmbiguousWrite() =>
        new(false, null, "LSKU_WRITE_OUTCOME_AMBIGUOUS", WriteOutcomeAmbiguous: true);

    private static bool SameFacts(Lsku left, Lsku right) =>
        left.Id == right.Id
        && left.GskuId == right.GskuId
        && left.CodeReservationId == right.CodeReservationId
        && string.Equals(left.CanonicalCode, right.CanonicalCode, StringComparison.Ordinal)
        && string.Equals(left.CreationCommandId, right.CreationCommandId, StringComparison.Ordinal)
        && string.Equals(left.MarketCode, right.MarketCode, StringComparison.Ordinal)
        && SameSelection(left.MarketSelection, right.MarketSelection);

    private static bool SameSelection(ReferenceCatalogSelection left, ReferenceCatalogSelection right) =>
        string.Equals(left.SetCode, right.SetCode, StringComparison.Ordinal)
        && string.Equals(left.ValueCode, right.ValueCode, StringComparison.Ordinal)
        && left.CatalogVersionId == right.CatalogVersionId
        && left.CatalogVersionNumber == right.CatalogVersionNumber
        && left.ResolutionMode == right.ResolutionMode
        && left.ResolvedAtUtc == right.ResolvedAtUtc;

    private bool ValidAudit(
        Guid id,
        int expectedVersion,
        ProductAuditOperation operation,
        LocalAuditIntent intent) =>
        intent.IntentId != Guid.Empty
        && intent.TenantId == _tenantId
        && intent.AggregateType == AuditAggregateType.Lsku
        && intent.AggregateId == id
        && intent.PreVersion == expectedVersion
        && intent.PostVersion == expectedVersion + 1
        && intent.Operation == operation
        && intent.Sequence == expectedVersion + 1
        && intent.TimestampUtc != default
        && intent.TimestampUtc.Offset == TimeSpan.Zero
        && intent.TimestampUtcTicksV1 == intent.TimestampUtc.UtcTicks
        && intent.TemporalStorageVersion == AuditIntentTemporalStorage.CurrentVersion
        && intent.SourceService == AuditIntentContract.SourceService
        && intent.SchemaVersion == 1
        && intent.ContractVersion is null
        && intent.DeliveryState == AuditIntentDeliveryState.Pending
        && intent.AttemptCount == 0
        && intent.LastAttemptAt is null
        && intent.NextRetryAt is null
        && intent.NextRetryAtUtcTicksV1 is null
        && intent.CentralAcknowledgement is null
        && intent.CentralIdempotencyKey is null
        && intent.AcknowledgedContractVersion is null
        && intent.AcknowledgedAt is null
        && intent.LastError is null
        && intent.LeaseOwner is null
        && intent.ClaimToken is null
        && intent.ClaimGeneration == 0
        && intent.ClaimedAt is null
        && intent.LeaseUntil is null
        && intent.LeaseUntilUtcTicksV1 is null
        && intent.DeliveredAt is null
        && intent.DeadLetteredAt is null
        && intent.CompactedAt is null
        && intent.CompactReceiptReference is null
        && intent.FailureClass == AuditIntentFailureClass.None
        && intent.FailureReason is null
        && ExactBounded(intent.ActorId, 128)
        && ExactBounded(intent.CorrelationId, 256)
        && ExactBounded(intent.CausationId, 256)
        && ExactBounded(intent.CommandId, 256)
        && ExactBounded(intent.EvidenceHash, 128)
        && ExactBounded(intent.SnapshotReference, 256)
        && ExactBounded(intent.IdempotencyKey, 256);

    private static bool ValidBinding(Guid id, ProductIdentityWorkflowBinding binding) =>
        binding.WorkflowInstanceId != Guid.Empty
        && binding.WorkflowTemplateId != Guid.Empty
        && binding.WorkflowTemplateVersionId != Guid.Empty
        && binding.ApprovalTaskId != Guid.Empty
        && binding.AssignmentSnapshotId != Guid.Empty
        && binding.StartTransitionLogId != Guid.Empty
        && binding.ObjectType == "lsku"
        && binding.ObjectId == id
        && ExactBounded(binding.ObjectRef, 256)
        && binding.SubmitterSubjectId != Guid.Empty
        && ExactBounded(binding.StartIdempotencyKey, 256)
        && IsLowerHex(binding.StartRequestFingerprint, 64)
        && binding.SubmittedAtUtc != default
        && binding.SubmittedAtUtc.Offset == TimeSpan.Zero
        && (!binding.DueAtUtc.HasValue || binding.DueAtUtc.Value.Offset == TimeSpan.Zero)
        && binding.TerminalDecision is null;

    private static bool ValidDecision(Guid id, ProductIdentityWorkflowDecisionEvidence evidence) =>
        evidence.WorkflowInstanceId != Guid.Empty
        && evidence.ApprovalTaskId != Guid.Empty
        && evidence.WorkflowTemplateId != Guid.Empty
        && evidence.WorkflowTemplateVersionId != Guid.Empty
        && evidence.ObjectType == "lsku"
        && evidence.ObjectId == id
        && ExactBounded(evidence.ObjectRef, 256)
        && evidence.DecisionActorSubjectId != Guid.Empty
        && evidence.DecisionAtUtc != default
        && evidence.DecisionAtUtc.Offset == TimeSpan.Zero
        && evidence.TransitionSequence > 0
        && ExactBounded(evidence.TaskStatus, 64)
        && ExactBounded(evidence.InstanceStatus, 64)
        && (evidence.Decision == ProductIdentityDecisionKind.Approved
            && evidence.TaskStatus == "Approved"
            && evidence.InstanceStatus == "Completed"
            || evidence.Decision == ProductIdentityDecisionKind.Rejected
            && evidence.TaskStatus == "Rejected"
            && evidence.InstanceStatus == "Rejected"
            && ExactBounded(evidence.ReasonCode, 128));

    private static bool SameBinding(
        ProductIdentityWorkflowBinding? left,
        ProductIdentityWorkflowBinding right) =>
        left is not null
        && left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.AssignmentSnapshotId == right.AssignmentSnapshotId
        && left.StartTransitionLogId == right.StartTransitionLogId
        && left.ObjectType == right.ObjectType
        && left.ObjectId == right.ObjectId
        && left.ObjectRef == right.ObjectRef
        && left.SubmitterSubjectId == right.SubmitterSubjectId
        && left.StartIdempotencyKey == right.StartIdempotencyKey
        && left.StartRequestFingerprint == right.StartRequestFingerprint
        && left.SubmittedAtUtc == right.SubmittedAtUtc
        && left.DueAtUtc == right.DueAtUtc;

    private static bool SameDecision(
        ProductIdentityWorkflowDecisionEvidence? left,
        ProductIdentityWorkflowDecisionEvidence right) =>
        left is not null
        && left.Decision == right.Decision
        && left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && left.ObjectType == right.ObjectType
        && left.ObjectId == right.ObjectId
        && left.ObjectRef == right.ObjectRef
        && left.DecisionActorSubjectId == right.DecisionActorSubjectId
        && left.ReasonCode == right.ReasonCode
        && left.DecisionAtUtc == right.DecisionAtUtc
        && left.TransitionSequence == right.TransitionSequence
        && left.TaskStatus == right.TaskStatus
        && left.InstanceStatus == right.InstanceStatus;

    private static bool SameAudit(LocalAuditIntent left, LocalAuditIntent right) =>
        left.SourceService == right.SourceService
        && left.SchemaVersion == right.SchemaVersion
        && left.ContractVersion == right.ContractVersion
        && left.IntentId == right.IntentId
        && left.TenantId == right.TenantId
        && left.AggregateType == right.AggregateType
        && left.AggregateId == right.AggregateId
        && left.PreVersion == right.PreVersion
        && left.PostVersion == right.PostVersion
        && left.Operation == right.Operation
        && left.ActorId == right.ActorId
        && left.CorrelationId == right.CorrelationId
        && left.CausationId == right.CausationId
        && left.CommandId == right.CommandId
        && left.Sequence == right.Sequence
        && left.TimestampUtc == right.TimestampUtc
        && left.TimestampUtcTicksV1 == right.TimestampUtcTicksV1
        && left.TemporalStorageVersion == right.TemporalStorageVersion
        && left.EvidenceHash == right.EvidenceHash
        && left.SnapshotReference == right.SnapshotReference
        && left.IdempotencyKey == right.IdempotencyKey;

    private static bool ExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 }
        && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsLowerHex(string? value, int exactLength) =>
        value is not null
        && value.Length == exactLength
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private void EnsureIndexes()
    {
        _lskus.Indexes.CreateMany([
            new CreateIndexModel<Lsku>(
                Builders<Lsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_lskus_tenant_code" }),
            new CreateIndexModel<Lsku>(
                Builders<Lsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CodeReservationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_lskus_tenant_reservation" }),
            new CreateIndexModel<Lsku>(
                Builders<Lsku>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_lskus_tenant_command" }),
            new CreateIndexModel<Lsku>(
                Builders<Lsku>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.GskuId)
                    .Ascending(x => x.MarketCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_lskus_tenant_gsku_market" })
        ]);
    }

    private FilterDefinition<Lsku> TenantIncludingDeletedFilter =>
        Builders<Lsku>.Filter.Eq(x => x.TenantId, _tenantId);

    private FilterDefinition<Lsku> ActiveFilter =>
        TenantIncludingDeletedFilter & Builders<Lsku>.Filter.Eq(x => x.IsDeleted, false);
}
