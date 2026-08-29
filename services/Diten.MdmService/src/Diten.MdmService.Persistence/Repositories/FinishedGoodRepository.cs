using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FinishedGoodRepository : IFinishedGoodRepository
{
    private const string CollectionName = "mdm_finished_goods";
    private readonly IMongoCollection<FinishedGood> _finishedGoods;
    private readonly IMongoCollection<BsonDocument> _documents;
    private readonly IMongoCollection<Gsku> _gskus;
    private readonly IMongoCollection<ProductDefinitionRevision> _revisions;
    private readonly IMongoCollection<CodeReservation> _reservations;
    private readonly Guid _tenantId;

    public FinishedGoodRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _finishedGoods = database.GetCollection<FinishedGood>(CollectionName);
        _documents = database.GetCollection<BsonDocument>(CollectionName);
        _gskus = database.GetCollection<Gsku>("mdm_gskus");
        _revisions = database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        _reservations = database.GetCollection<CodeReservation>("mdm_code_reservations");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<FinishedGood?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _finishedGoods.Find(ActiveTenantFilter & Builders<FinishedGood>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinishedGood?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default)
        => await _finishedGoods.Find(
                TenantIncludingDeletedFilter
                & Builders<FinishedGood>.Filter.Eq(x => x.CreationCommandId, creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinishedGood?> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default)
        => await _finishedGoods.Find(
                TenantIncludingDeletedFilter
                & Builders<FinishedGood>.Filter.Eq(x => x.CodeReservationId, reservationId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FinishedGoodPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        IReadOnlyCollection<Guid>? matchingGskuIds,
        CancellationToken cancellationToken = default)
    {
        var filter = ActiveTenantFilter;
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            var codeFilter = Builders<FinishedGood>.Filter.Regex(
                x => x.CanonicalCode,
                new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch)));
            filter &= matchingGskuIds is { Count: > 0 }
                ? Builders<FinishedGood>.Filter.Or(
                    codeFilter,
                    Builders<FinishedGood>.Filter.In(x => x.GskuId, matchingGskuIds))
                : codeFilter;
        }

        var totalCount = await _finishedGoods.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _finishedGoods.Find(filter)
            .SortBy(x => x.CanonicalCode)
            .ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);
        return new(items, totalCount);
    }

    public async Task<FinishedGoodPage> GetEnforcedLegalEntityScopePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        IReadOnlyCollection<Guid>? matchingGskuIds,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        CancellationToken cancellationToken = default)
    {
        var match = new BsonDocument
        {
            { nameof(FinishedGood.TenantId), ProductLegalEntityScopeAggregation.GuidBson(_tenantId) },
            { nameof(FinishedGood.IsDeleted), false }
        };
        if (!string.IsNullOrWhiteSpace(canonicalCodeSearch))
        {
            var code = new BsonDocument(
                nameof(FinishedGood.CanonicalCode),
                new BsonRegularExpression(
                    "^" + System.Text.RegularExpressions.Regex.Escape(canonicalCodeSearch)));
            match["$or"] = matchingGskuIds is { Count: > 0 }
                ? new BsonArray
                {
                    code,
                    new BsonDocument(nameof(FinishedGood.GskuId), new BsonDocument("$in",
                        new BsonArray(matchingGskuIds.Select(ProductLegalEntityScopeAggregation.GuidBson))))
                }
                : new BsonArray { code };
        }

        var pipeline = new List<BsonDocument> { new("$match", match) };
        pipeline.AddRange(ProductLegalEntityScopeAggregation.CreateGskuRevisionResolutionStages(
            _tenantId,
            "$" + nameof(FinishedGood.GskuId)));
        pipeline.AddRange(ProductLegalEntityScopeAggregation.CreateAccessStages(
            _tenantId,
            effectiveCandidateLegalEntityIds,
            serverNowUtc));
        pipeline.Add(ProductLegalEntityScopeAggregation.CleanupStage("ScopeGskus", "ScopeRevisions"));
        pipeline.Add(new BsonDocument("$sort", new BsonDocument
        {
            { nameof(FinishedGood.CanonicalCode), 1 },
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
            .Select(item => BsonSerializer.Deserialize<FinishedGood>(item.AsBsonDocument))
            .ToArray() ?? [];
        var total = result?["summary"].AsBsonArray.FirstOrDefault()?.AsBsonDocument["total"].ToInt64() ?? 0;
        return new(items, total);
    }

    public async Task<FinishedGoodCreateResult> CreateDraftAsync(
        FinishedGood finishedGood,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByCreationCommandIdAsync(finishedGood.CreationCommandId, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDeleted)
            {
                return new(false, existing, "CREATION_COMMAND_TOMBSTONED");
            }

            return SameFacts(existing, finishedGood)
                ? new(true, existing)
                : new(false, existing, "IDEMPOTENCY_KEY_CONFLICT");
        }

        if (finishedGood.Id == Guid.Empty
            || finishedGood.GskuId == Guid.Empty
            || finishedGood.CodeReservationId == Guid.Empty
            || string.IsNullOrWhiteSpace(finishedGood.CanonicalCode)
            || string.IsNullOrWhiteSpace(finishedGood.CreationCommandId)
            || finishedGood.LifecycleStatus != ProductIdentityLifecycleStatus.Draft)
        {
            return new(false, null, "FINISHED_GOOD_CONTRACT_INVALID");
        }

        var requiredAdmissionFingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
            finishedGood.GskuId, GskuChildIdentityKind.FinishedGood, finishedGood.CreationCommandId);
        var referenceableGskuFilter = Builders<Gsku>.Filter.Eq(x => x.TenantId, _tenantId)
            & Builders<Gsku>.Filter.Eq(x => x.IsDeleted, false)
            & Builders<Gsku>.Filter.Eq(x => x.Id, finishedGood.GskuId)
            & Builders<Gsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<Gsku>.Filter.Eq(x => x.RetirementOperationId, null)
            & Builders<Gsku>.Filter.ElemMatch(x => x.ChildCreationAdmissions,
                x => x.ChildKind == GskuChildIdentityKind.FinishedGood
                     && x.CreationCommandId == finishedGood.CreationCommandId
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
            & Builders<CodeReservation>.Filter.Eq(x => x.Id, finishedGood.CodeReservationId)
            & Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservationState, CodeReservationState.Consumed)
            & Builders<CodeReservation>.Filter.Eq(x => x.ConsumedEntityId, finishedGood.Id)
            & Builders<CodeReservation>.Filter.Eq(x => x.ReservedCode, finishedGood.CanonicalCode)
            & Builders<CodeReservation>.Filter.In(
                x => x.BindingState,
                [CodeReservationBindingState.PendingIdentityWrite, CodeReservationBindingState.Confirmed]);
        if (!await _reservations.Find(reservationFilter).AnyAsync(cancellationToken))
        {
            return new(false, null, "CODE_RESERVATION_REQUIRED");
        }

        if (finishedGood.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || finishedGood.AuditIntents.Any(intent =>
                intent.TenantId != _tenantId
                || intent.AggregateType != AuditAggregateType.FinishedGood
                || intent.AggregateId != finishedGood.Id))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        finishedGood.TenantId = _tenantId;
        finishedGood.CreatedAt = DateTimeOffset.UtcNow;
        finishedGood.UpdatedAt = finishedGood.CreatedAt;
        finishedGood.IsDeleted = false;
        finishedGood.DeletedAt = null;
        finishedGood.Version = 0;

        try
        {
            await _finishedGoods.InsertOneAsync(finishedGood, cancellationToken: cancellationToken);
            return new(true, finishedGood);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByCreationCommandIdAsync(finishedGood.CreationCommandId, cancellationToken)
                ?? await GetByReservationIdAsync(finishedGood.CodeReservationId, cancellationToken);
            return existing is not null && !existing.IsDeleted && SameFacts(existing, finishedGood)
                ? new(true, existing)
                : new(false, existing, "FINISHED_GOOD_DUPLICATE_CONFLICT");
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

    public async Task<FinishedGoodCreateResult> CreateDraftWithAdmissionAsync(
        FinishedGood finishedGood, string admissionFingerprint,
        CancellationToken cancellationToken = default)
    {
        var expected = GskuChildCreationAdmission.ComputeRequestFingerprint(
            finishedGood.GskuId, GskuChildIdentityKind.FinishedGood, finishedGood.CreationCommandId);
        if (!string.Equals(admissionFingerprint, expected, StringComparison.Ordinal))
            return new(false, null, "GSKU_CHILD_ADMISSION_REQUIRED");
        return await CreateDraftAsync(finishedGood, cancellationToken);
    }

    public Task<FinishedGoodLifecycleWriteResult> SubmitIdentityAsync(
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
            ProductAuditOperation.FinishedGoodIdentitySubmitted,
            auditIntent,
            workflowBinding,
            null,
            null,
            cancellationToken);

    public Task<FinishedGoodLifecycleWriteResult> ReconcileIdentityDecisionAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityWorkflowBinding expectedWorkflowBinding,
        ProductIdentityWorkflowDecisionEvidence decisionEvidence,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedWorkflowBinding);
        ArgumentNullException.ThrowIfNull(decisionEvidence);
        var operation = decisionEvidence.Decision switch
        {
            ProductIdentityDecisionKind.Approved => ProductAuditOperation.FinishedGoodIdentityApproved,
            ProductIdentityDecisionKind.Rejected => ProductAuditOperation.FinishedGoodIdentityRejected,
            _ => (ProductAuditOperation?)null
        };
        if (operation is null || !ValidBinding(id, expectedWorkflowBinding)
            || !ValidDecision(id, decisionEvidence))
        {
            return Task.FromResult(new FinishedGoodLifecycleWriteResult(
                false, null, "FINISHED_GOOD_IDENTITY_DECISION_INVALID"));
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
            expectedWorkflowBinding,
            decisionEvidence,
            cancellationToken);
    }

    public Task<FinishedGoodLifecycleWriteResult> RetireIdentityAsync(
        Guid id,
        int expectedVersion,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        ApplyLifecycleAsync(
            id,
            expectedVersion,
            ProductIdentityLifecycleStatus.IdentityApproved,
            ProductIdentityLifecycleStatus.Retired,
            ProductAuditOperation.FinishedGoodIdentityRetired,
            auditIntent,
            null,
            null,
            null,
            cancellationToken);

    private async Task<FinishedGoodLifecycleWriteResult> ApplyLifecycleAsync(
        Guid id,
        int expectedVersion,
        ProductIdentityLifecycleStatus sourceStatus,
        ProductIdentityLifecycleStatus targetStatus,
        ProductAuditOperation operation,
        LocalAuditIntent auditIntent,
        ProductIdentityWorkflowBinding? workflowBinding,
        ProductIdentityWorkflowBinding? expectedExistingBinding,
        ProductIdentityWorkflowDecisionEvidence? decisionEvidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditIntent);
        cancellationToken.ThrowIfCancellationRequested();
        if (id == Guid.Empty || expectedVersion < 0
            || !ValidAudit(id, expectedVersion, operation, auditIntent)
            || workflowBinding is not null && !ValidBinding(id, workflowBinding)
            || expectedExistingBinding is not null && !ValidBinding(id, expectedExistingBinding))
        {
            return new(false, null, "FINISHED_GOOD_IDENTITY_LIFECYCLE_CONTRACT_INVALID");
        }

        var replay = await FindLifecycleReplayAsync(id, auditIntent, cancellationToken);
        if (replay is not null)
        {
            if (workflowBinding is not null
                && !SameBinding(replay.FinishedGood?.IdentityWorkflowBinding, workflowBinding)
                || expectedExistingBinding is not null
                && !SameBinding(replay.FinishedGood?.IdentityWorkflowBinding, expectedExistingBinding)
                || decisionEvidence is not null
                && !SameDecision(replay.FinishedGood?.IdentityWorkflowBinding?.TerminalDecision, decisionEvidence))
            {
                return new(false, replay.FinishedGood, "FINISHED_GOOD_IDENTITY_IDEMPOTENCY_CONFLICT");
            }
            return replay;
        }

        var filter = ActiveTenantFilter
                     & Builders<FinishedGood>.Filter.Eq(x => x.Id, id)
                     & Builders<FinishedGood>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<FinishedGood>.Filter.Eq(x => x.LifecycleStatus, sourceStatus)
                     & Builders<FinishedGood>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate)
                     & new BsonDocumentFilterDefinition<FinishedGood>(new BsonDocument(
                         "$expr",
                         new BsonDocument("$lt", new BsonArray
                         {
                             new BsonDocument("$bsonSize", "$$ROOT"),
                             1024 * 1024 - 4096
                         })));
        if (decisionEvidence is not null)
        {
            filter &= Builders<FinishedGood>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.WorkflowInstanceId,
                          decisionEvidence.WorkflowInstanceId)
                      & Builders<FinishedGood>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.ApprovalTaskId,
                          decisionEvidence.ApprovalTaskId)
                      & Builders<FinishedGood>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.WorkflowTemplateId,
                          decisionEvidence.WorkflowTemplateId)
                      & Builders<FinishedGood>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.WorkflowTemplateVersionId,
                          decisionEvidence.WorkflowTemplateVersionId)
                      & Builders<FinishedGood>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.ObjectType,
                          decisionEvidence.ObjectType)
                      & Builders<FinishedGood>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.ObjectId,
                          decisionEvidence.ObjectId)
                      & Builders<FinishedGood>.Filter.Eq(
                          x => x.IdentityWorkflowBinding!.TerminalDecision,
                          null)
                      & Builders<FinishedGood>.Filter.Ne(
                          x => x.IdentityWorkflowBinding!.SubmitterSubjectId,
                          decisionEvidence.DecisionActorSubjectId);
            if (expectedExistingBinding is not null)
            {
                filter &= Builders<FinishedGood>.Filter.Eq(
                              x => x.IdentityWorkflowBinding!.AssignmentSnapshotId,
                              expectedExistingBinding.AssignmentSnapshotId)
                          & Builders<FinishedGood>.Filter.Eq(
                              x => x.IdentityWorkflowBinding!.StartTransitionLogId,
                              expectedExistingBinding.StartTransitionLogId)
                          & Builders<FinishedGood>.Filter.Eq(
                              x => x.IdentityWorkflowBinding!.ObjectRef,
                              expectedExistingBinding.ObjectRef)
                          & Builders<FinishedGood>.Filter.Eq(
                              x => x.IdentityWorkflowBinding!.SubmitterSubjectId,
                              expectedExistingBinding.SubmitterSubjectId)
                          & Builders<FinishedGood>.Filter.Eq(
                              x => x.IdentityWorkflowBinding!.StartIdempotencyKey,
                              expectedExistingBinding.StartIdempotencyKey)
                          & Builders<FinishedGood>.Filter.Eq(
                              x => x.IdentityWorkflowBinding!.StartRequestFingerprint,
                              expectedExistingBinding.StartRequestFingerprint)
                          & Builders<FinishedGood>.Filter.Eq(
                              x => x.IdentityWorkflowBinding!.SubmittedAtUtc,
                              expectedExistingBinding.SubmittedAtUtc)
                          & Builders<FinishedGood>.Filter.Eq(
                              x => x.IdentityWorkflowBinding!.DueAtUtc,
                              expectedExistingBinding.DueAtUtc);
            }
        }

        var update = Builders<FinishedGood>.Update
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

        var updated = await _finishedGoods.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<FinishedGood> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, updated);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        if (current is null)
        {
            return new(false, null, "FINISHED_GOOD_IDENTITY_NOT_FOUND");
        }
        if (current.Version != expectedVersion)
        {
            return new(false, current, "FINISHED_GOOD_IDENTITY_VERSION_CONFLICT");
        }
        if (current.AuditIntents.Count >= AuditIntentLimits.MaxPerAggregate)
        {
            return new(false, current, "AUDIT_INTENT_CAPACITY_EXCEEDED");
        }
        if (current.ToBson().Length >= 1024 * 1024 - 4096)
        {
            return new(false, current, "FINISHED_GOOD_DOCUMENT_SIZE_LIMIT_EXCEEDED");
        }
        return new(false, current,
            decisionEvidence is not null
            && current.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
                ? "WORKFLOW_BINDING_CONFLICT"
                : "FINISHED_GOOD_IDENTITY_LIFECYCLE_CONFLICT");
    }

    private async Task<FinishedGoodLifecycleWriteResult?> FindLifecycleReplayAsync(
        Guid id,
        LocalAuditIntent auditIntent,
        CancellationToken cancellationToken)
    {
        var current = await _finishedGoods.Find(
                ActiveTenantFilter
                & Builders<FinishedGood>.Filter.Eq(x => x.Id, id)
                & Builders<FinishedGood>.Filter.Or(
                    Builders<FinishedGood>.Filter.ElemMatch(
                        x => x.AuditIntents,
                        intent => intent.IntentId == auditIntent.IntentId),
                    Builders<FinishedGood>.Filter.ElemMatch(
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
            : new(false, current, "FINISHED_GOOD_IDENTITY_IDEMPOTENCY_CONFLICT");
    }

    private static ProductIdentityLifecycleStatus ExpectedReplayStatus(ProductAuditOperation operation) =>
        operation switch
        {
            ProductAuditOperation.FinishedGoodIdentitySubmitted =>
                ProductIdentityLifecycleStatus.PendingIdentityApproval,
            ProductAuditOperation.FinishedGoodIdentityApproved =>
                ProductIdentityLifecycleStatus.IdentityApproved,
            ProductAuditOperation.FinishedGoodIdentityRejected =>
                ProductIdentityLifecycleStatus.Draft,
            ProductAuditOperation.FinishedGoodIdentityRetired =>
                ProductIdentityLifecycleStatus.Retired,
            _ => (ProductIdentityLifecycleStatus)(-1)
        };

    private static FinishedGoodCreateResult AmbiguousWrite()
        => new(false, null, "FINISHED_GOOD_WRITE_OUTCOME_AMBIGUOUS", WriteOutcomeAmbiguous: true);

    private static bool SameFacts(FinishedGood left, FinishedGood right)
        => left.Id == right.Id
           && left.GskuId == right.GskuId
           && left.CodeReservationId == right.CodeReservationId
           && string.Equals(left.CanonicalCode, right.CanonicalCode, StringComparison.Ordinal)
           && string.Equals(left.CreationCommandId, right.CreationCommandId, StringComparison.Ordinal);

    private bool ValidAudit(
        Guid id,
        int expectedVersion,
        ProductAuditOperation operation,
        LocalAuditIntent intent) =>
        intent.IntentId != Guid.Empty
        && intent.TenantId == _tenantId
        && intent.AggregateType == AuditAggregateType.FinishedGood
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
        && binding.ObjectType == "finished-good"
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
        && evidence.ObjectType == "finished-good"
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
        _finishedGoods.Indexes.CreateMany([
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CanonicalCode),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_goods_tenant_code" }),
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CodeReservationId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_goods_tenant_reservation" }),
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_finished_goods_tenant_command" }),
            new CreateIndexModel<FinishedGood>(
                Builders<FinishedGood>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.GskuId),
                new CreateIndexOptions { Name = "ix_mdm_finished_goods_tenant_gsku" })
        ]);
    }

    private FilterDefinition<FinishedGood> TenantIncludingDeletedFilter =>
        Builders<FinishedGood>.Filter.Eq(x => x.TenantId, _tenantId);

    private FilterDefinition<FinishedGood> ActiveTenantFilter =>
        TenantIncludingDeletedFilter & Builders<FinishedGood>.Filter.Eq(x => x.IsDeleted, false);
}
