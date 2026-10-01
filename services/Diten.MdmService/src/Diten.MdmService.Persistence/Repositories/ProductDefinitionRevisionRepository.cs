using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductDefinitionRevisionRepository : IProductDefinitionRevisionRepository
{
    private const string CollectionName = "mdm_product_definition_revisions";
    private const string AllocatorCollectionName = "mdm_product_definition_revision_allocators";
    private readonly IMongoCollection<ProductDefinitionRevision> _revisions;
    private readonly IMongoCollection<BsonDocument> _allocators;
    private readonly Guid _tenantId;

    public ProductDefinitionRevisionRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _revisions = database.GetCollection<ProductDefinitionRevision>(CollectionName);
        _allocators = database.GetCollection<BsonDocument>(AllocatorCollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public Task<ProductDefinitionRevision?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _revisions.Find(ActiveFilter & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductDefinitionRevision>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _revisions.Find(ActiveFilter & Builders<ProductDefinitionRevision>.Filter.In(x => x.Id, ids))
            .ToListAsync(cancellationToken);
    }

    public Task<ProductDefinitionRevision?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default)
        => _revisions.Find(
                TenantFilter & Builders<ProductDefinitionRevision>.Filter.Eq(
                    x => x.CreationCommandId,
                    creationCommandId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<FirstGskuPairAllocationResult> AllocateForFirstGskuAsync(
        Guid globalProductId,
        string creationCommandId,
        CancellationToken cancellationToken = default)
    {
        var allocatorId = $"{_tenantId:N}:{globalProductId:N}";
        var nextOrdinal = new BsonDocument("$add", new BsonArray
        {
            new BsonDocument("$ifNull", new BsonArray { "$LastOrdinal", 0 }),
            1
        });
        var allocation = new BsonDocument
        {
            { "CreationCommandId", creationCommandId },
            { "RevisionId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
            { "GskuId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
            { "Ordinal", nextOrdinal },
            { "AllocatedAt", new BsonDateTime(DateTime.UtcNow) }
        };
        var stage = new BsonDocument("$set", new BsonDocument
        {
            { "TenantId", new BsonBinaryData(_tenantId, GuidRepresentation.Standard) },
            { "GlobalProductId", new BsonBinaryData(globalProductId, GuidRepresentation.Standard) },
            { "LastOrdinal", nextOrdinal },
            { "Allocations", new BsonDocument("$concatArrays", new BsonArray
                {
                    new BsonDocument("$ifNull", new BsonArray { "$Allocations", new BsonArray() }),
                    new BsonArray { allocation }
                }) }
        });
        var filter = Builders<BsonDocument>.Filter.Eq("_id", allocatorId)
                     & Builders<BsonDocument>.Filter.Ne("Allocations.CreationCommandId", creationCommandId);

        try
        {
            await _allocators.FindOneAndUpdateAsync(
                filter,
                new PipelineUpdateDefinition<BsonDocument>(new[] { stage }),
                new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }
        catch (MongoCommandException exception) when (exception.Code == 11000)
        {
            // A same-parent or same-command contender won. The durable command allocation below is authoritative.
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Same recovery path as the command-level duplicate above.
        }

        var document = await _allocators.Find(Builders<BsonDocument>.Filter.Eq("_id", allocatorId))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("REVISION_ORDINAL_CONFLICT");
        var entry = document["Allocations"].AsBsonArray
            .Select(value => value.AsBsonDocument)
            .SingleOrDefault(value => value["CreationCommandId"].AsString == creationCommandId)
            ?? throw new InvalidOperationException("REVISION_ORDINAL_CONFLICT");
        var ordinal = entry["Ordinal"].ToInt32();
        return new FirstGskuPairAllocationResult(
            entry["RevisionId"].AsGuid,
            entry["GskuId"].AsGuid,
            ordinal,
            $"REV-{ordinal:D3}");
    }

    public async Task<ProductDefinitionRevisionCreateResult> CreateForFirstGskuAsync(
        ProductDefinitionRevision revision,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByCreationCommandIdAsync(revision.CreationCommandId, cancellationToken);
        if (existing is not null)
        {
            return SameFacts(existing, revision)
                ? new(true, existing)
                : new(false, existing, "CREATION_COMMAND_PAIR_CONFLICT");
        }

        if (revision.AuditIntents.Count is 0 or > AuditIntentLimits.MaxPerAggregate
            || revision.AuditIntents.Any(x => x.TenantId != _tenantId))
        {
            return new(false, null, "AUDIT_INTENT_CONTRACT_INVALID");
        }

        revision.TenantId = _tenantId;
        revision.CreatedAt = DateTimeOffset.UtcNow;
        revision.UpdatedAt = revision.CreatedAt;
        revision.IsDeleted = false;
        revision.Version = 0;
        try
        {
            await _revisions.InsertOneAsync(revision, cancellationToken: cancellationToken);
            return new(true, revision);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await GetByCreationCommandIdAsync(revision.CreationCommandId, cancellationToken);
            return existing is not null && SameFacts(existing, revision)
                ? new(true, existing)
                : new(false, existing, "CREATION_COMMAND_PAIR_CONFLICT");
        }
    }

    public Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> MarkIdentityPendingAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) => MutateLifecycleAsync(
            id, expectedVersion, ProductIdentityLifecycleStatus.Draft,
            ProductIdentityLifecycleStatus.PendingIdentityApproval, binding, auditIntent,
            ProductAuditOperation.ProductDefinitionRevisionIdentitySubmitted, cancellationToken);

    public Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> ApproveIdentityAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) => MutateLifecycleAsync(
            id, expectedVersion, ProductIdentityLifecycleStatus.PendingIdentityApproval,
            ProductIdentityLifecycleStatus.IdentityApproved, binding, auditIntent,
            ProductAuditOperation.ProductDefinitionRevisionIdentityApproved, cancellationToken);

    public Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> RestoreDraftAfterRejectionAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) => MutateLifecycleAsync(
            id, expectedVersion, ProductIdentityLifecycleStatus.PendingIdentityApproval,
            ProductIdentityLifecycleStatus.Draft, binding, auditIntent,
            ProductAuditOperation.ProductDefinitionRevisionIdentityRejected, cancellationToken);

    public async Task<FirstGskuIdentityRetirementWriteResult<ProductDefinitionRevision>> RetireIdentityAsync(
        Guid id, int expectedVersion, Guid operationId, string operationFingerprint,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditIntent);
        var current = await GetByIdAsync(id, cancellationToken);
        var replay = current?.AuditIntents.SingleOrDefault(x => x.IdempotencyKey == auditIntent.IdempotencyKey);
        if (replay is not null)
            return replay.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentityRetired
                   && replay.EvidenceHash == auditIntent.EvidenceHash
                   && current!.LifecycleStatus == ProductIdentityLifecycleStatus.Retired
                   && current.RetirementOperationId == operationId
                   && current.RetirementOperationFingerprint == operationFingerprint
                ? new(true, true, current, null)
                : new(false, false, current, "FIRST_GSKU_RETIREMENT_IDEMPOTENCY_CONFLICT");
        if (_tenantId == Guid.Empty || id == Guid.Empty || operationId == Guid.Empty || expectedVersion < 0
            || string.IsNullOrWhiteSpace(operationFingerprint)
            || auditIntent.TenantId != _tenantId || auditIntent.AggregateId != id
            || auditIntent.PreVersion != expectedVersion || auditIntent.PostVersion != expectedVersion + 1
            || auditIntent.Operation != ProductAuditOperation.ProductDefinitionRevisionIdentityRetired
            || string.IsNullOrWhiteSpace(auditIntent.IdempotencyKey)
            || string.IsNullOrWhiteSpace(auditIntent.EvidenceHash))
            return new(false, false, current, "FIRST_GSKU_RETIREMENT_CONTRACT_INVALID");

        var filter = ActiveFilter & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, id)
            & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Version, expectedVersion)
            & Builders<ProductDefinitionRevision>.Filter.Eq(
                x => x.LifecycleStatus, ProductIdentityLifecycleStatus.IdentityApproved)
            & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.RetirementOperationId, null)
            & Builders<ProductDefinitionRevision>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        var updated = await _revisions.FindOneAndUpdateAsync(filter,
            Builders<ProductDefinitionRevision>.Update
                .Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Retired)
                .Set(x => x.RetirementOperationId, operationId)
                .Set(x => x.RetirementOperationFingerprint, operationFingerprint)
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow).Inc(x => x.Version, 1)
                .Push(x => x.AuditIntents, auditIntent),
            new FindOneAndUpdateOptions<ProductDefinitionRevision> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return updated is not null ? new(true, false, updated, null)
            : new(false, false, current, current is null
                ? "FIRST_GSKU_REVISION_NOT_FOUND"
                : "FIRST_GSKU_REVISION_RETIREMENT_CONFLICT");
    }

    private async Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> MutateLifecycleAsync(
        Guid id, int expectedVersion, ProductIdentityLifecycleStatus sourceStatus,
        ProductIdentityLifecycleStatus targetStatus, FirstGskuIdentityWorkflowBinding binding,
        LocalAuditIntent auditIntent, ProductAuditOperation operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(auditIntent);
        var contractError = ValidateLifecycleContract(id, expectedVersion, binding, auditIntent, operation);
        if (contractError is not null)
        {
            return new(false, false, null, contractError);
        }

        var current = await GetByIdAsync(id, cancellationToken);
        var replayIntent = current?.AuditIntents.SingleOrDefault(x => x.IdempotencyKey == auditIntent.IdempotencyKey);
        if (replayIntent is not null)
        {
            var exact = replayIntent.Operation == operation
                        && replayIntent.EvidenceHash == auditIntent.EvidenceHash
                        && current!.LifecycleStatus == targetStatus
                        && SameBinding(current.IdentityWorkflowBinding, binding);
            return exact
                ? new(true, true, current, null)
                : new(false, false, current, "FIRST_GSKU_IDENTITY_IDEMPOTENCY_CONFLICT");
        }

        var filter = ActiveFilter
                     & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Id, id)
                     & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.Version, expectedVersion)
                     & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.LifecycleStatus, sourceStatus)
                     & Builders<ProductDefinitionRevision>.Filter.Where(
                         x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate);
        if (sourceStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval)
        {
            filter &= BindingFence(binding);
        }

        var update = Builders<ProductDefinitionRevision>.Update
            .Set(x => x.LifecycleStatus, targetStatus)
            .Set(x => x.IdentityWorkflowBinding, binding)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1)
            .Push(x => x.AuditIntents, auditIntent);
        var updated = await _revisions.FindOneAndUpdateAsync(
            filter, update,
            new FindOneAndUpdateOptions<ProductDefinitionRevision> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is not null)
        {
            return new(true, false, updated, null);
        }

        current = await GetByIdAsync(id, cancellationToken);
        return new(false, false, current, current is null
            ? "FIRST_GSKU_REVISION_NOT_FOUND"
            : current.Version != expectedVersion
                ? "FIRST_GSKU_REVISION_CONCURRENCY_CONFLICT"
                : current.LifecycleStatus != sourceStatus
                    ? "FIRST_GSKU_REVISION_STATE_CONFLICT"
                    : "FIRST_GSKU_WORKFLOW_BINDING_CONFLICT");
    }

    private string? ValidateLifecycleContract(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding,
        LocalAuditIntent auditIntent, ProductAuditOperation operation)
    {
        if (_tenantId == Guid.Empty || id == Guid.Empty || expectedVersion < 0
            || binding.GskuId == Guid.Empty || binding.ProductDefinitionRevisionId != id
            || binding.WorkflowInstanceId == Guid.Empty || binding.WorkflowTemplateId == Guid.Empty
            || binding.WorkflowTemplateVersionId == Guid.Empty || binding.ApprovalTaskId == Guid.Empty
            || binding.AssignmentSnapshotId == Guid.Empty || binding.StartTransitionLogId == Guid.Empty
            || binding.SubmitterSubjectId == Guid.Empty || binding.ObjectType != "gsku"
            || binding.SubmittedAtUtc.Offset != TimeSpan.Zero
            || binding.DueAtUtc.HasValue && binding.DueAtUtc.Value.Offset != TimeSpan.Zero
            || string.IsNullOrWhiteSpace(binding.ObjectRef)
            || string.IsNullOrWhiteSpace(binding.StartIdempotencyKey)
            || string.IsNullOrWhiteSpace(binding.StartRequestFingerprint))
        {
            return "FIRST_GSKU_WORKFLOW_BINDING_INVALID";
        }
        var expectedDecision = operation switch
        {
            ProductAuditOperation.ProductDefinitionRevisionIdentityApproved => ProductIdentityDecisionKind.Approved,
            ProductAuditOperation.ProductDefinitionRevisionIdentityRejected => ProductIdentityDecisionKind.Rejected,
            _ => (ProductIdentityDecisionKind?)null
        };
        if (expectedDecision.HasValue
                ? !ValidTerminalDecision(binding, expectedDecision.Value)
                : binding.TerminalDecision is not null)
        {
            return "FIRST_GSKU_WORKFLOW_DECISION_INVALID";
        }
        if (auditIntent.TenantId != _tenantId || auditIntent.AggregateId != id
            || auditIntent.PreVersion != expectedVersion || auditIntent.PostVersion != expectedVersion + 1
            || auditIntent.Operation != operation || string.IsNullOrWhiteSpace(auditIntent.IdempotencyKey)
            || string.IsNullOrWhiteSpace(auditIntent.EvidenceHash))
        {
            return "AUDIT_INTENT_CONTRACT_INVALID";
        }
        return null;
    }

    private FilterDefinition<ProductDefinitionRevision> BindingFence(FirstGskuIdentityWorkflowBinding binding) =>
        Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IdentityWorkflowBinding!.WorkflowInstanceId, binding.WorkflowInstanceId)
        & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IdentityWorkflowBinding!.ApprovalTaskId, binding.ApprovalTaskId)
        & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IdentityWorkflowBinding!.StartRequestFingerprint, binding.StartRequestFingerprint);

    private static bool SameBinding(FirstGskuIdentityWorkflowBinding? left, FirstGskuIdentityWorkflowBinding right) =>
        left is not null && left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.AssignmentSnapshotId == right.AssignmentSnapshotId
        && left.StartTransitionLogId == right.StartTransitionLogId
        && left.ObjectType == right.ObjectType
        && left.GskuId == right.GskuId
        && left.ProductDefinitionRevisionId == right.ProductDefinitionRevisionId
        && left.ObjectRef == right.ObjectRef
        && left.SubmitterSubjectId == right.SubmitterSubjectId
        && left.StartIdempotencyKey == right.StartIdempotencyKey
        && left.StartRequestFingerprint == right.StartRequestFingerprint
        && left.SubmittedAtUtc == right.SubmittedAtUtc
        && left.DueAtUtc == right.DueAtUtc
        && SameTerminalDecision(left.TerminalDecision, right.TerminalDecision);

    private static bool ValidTerminalDecision(
        FirstGskuIdentityWorkflowBinding binding, ProductIdentityDecisionKind expected)
    {
        var value = binding.TerminalDecision;
        return value is not null && value.Decision == expected
            && value.WorkflowInstanceId == binding.WorkflowInstanceId
            && value.ApprovalTaskId == binding.ApprovalTaskId
            && value.WorkflowTemplateId == binding.WorkflowTemplateId
            && value.WorkflowTemplateVersionId == binding.WorkflowTemplateVersionId
            && value.ObjectType == binding.ObjectType && value.ObjectId == binding.GskuId
            && value.ObjectRef == binding.ObjectRef && value.DecisionActorSubjectId != Guid.Empty
            && value.DecisionActorSubjectId != binding.SubmitterSubjectId
            && value.DecisionAtUtc.Offset == TimeSpan.Zero && value.TransitionSequence > 0
            && (expected == ProductIdentityDecisionKind.Approved
                ? value.TaskStatus == "Approved" && value.InstanceStatus == "Completed"
                : value.TaskStatus == "Rejected" && value.InstanceStatus == "Rejected");
    }

    private static bool SameTerminalDecision(
        ProductIdentityWorkflowDecisionEvidence? left, ProductIdentityWorkflowDecisionEvidence? right) =>
        left is null && right is null
        || left is not null && right is not null
        && left.Decision == right.Decision && left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.ApprovalTaskId == right.ApprovalTaskId && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && left.ObjectType == right.ObjectType && left.ObjectId == right.ObjectId
        && left.ObjectRef == right.ObjectRef && left.DecisionActorSubjectId == right.DecisionActorSubjectId
        && left.ReasonCode == right.ReasonCode && left.DecisionAtUtc == right.DecisionAtUtc
        && left.TransitionSequence == right.TransitionSequence && left.TaskStatus == right.TaskStatus
        && left.InstanceStatus == right.InstanceStatus;

    private static bool SameFacts(ProductDefinitionRevision left, ProductDefinitionRevision right)
        => left.Id == right.Id
           && left.GlobalProductId == right.GlobalProductId
           && left.RevisionIdentifier == right.RevisionIdentifier
           && left.CreationCommandId == right.CreationCommandId;

    private void EnsureIndexes()
    {
        _revisions.Indexes.CreateMany([
            new CreateIndexModel<ProductDefinitionRevision>(
                Builders<ProductDefinitionRevision>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.GlobalProductId).Ascending(x => x.RevisionIdentifier),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_product_definition_revisions_tenant_parent_identifier" }),
            new CreateIndexModel<ProductDefinitionRevision>(
                Builders<ProductDefinitionRevision>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.CreationCommandId),
                new CreateIndexOptions { Unique = true, Name = "ux_mdm_product_definition_revisions_tenant_command" }),
            new CreateIndexModel<ProductDefinitionRevision>(
                Builders<ProductDefinitionRevision>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.GlobalProductId),
                new CreateIndexOptions { Name = "ix_mdm_product_definition_revisions_tenant_parent" })
        ]);
    }

    private FilterDefinition<ProductDefinitionRevision> TenantFilter =>
        Builders<ProductDefinitionRevision>.Filter.Eq(x => x.TenantId, _tenantId);
    private FilterDefinition<ProductDefinitionRevision> ActiveFilter =>
        TenantFilter & Builders<ProductDefinitionRevision>.Filter.Eq(x => x.IsDeleted, false);
}

internal static class ProductLegalEntityScopeAggregation
{
    internal const string ResolvedGlobalProductIdField = "ScopeGlobalProductId";

    internal static IReadOnlyList<BsonDocument> CreateAccessStages(
        Guid tenantId,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc) => CreatePolicyAccessStages(
            tenantId,
            effectiveCandidateLegalEntityIds,
            serverNowUtc,
            "$" + ResolvedGlobalProductIdField,
            requireGlobalProductLookup: true);

    internal static IReadOnlyList<BsonDocument> CreatePolicyAccessStages(
        Guid tenantId,
        IReadOnlyCollection<Guid> effectiveCandidateLegalEntityIds,
        DateTimeOffset serverNowUtc,
        string globalProductIdExpression,
        bool requireGlobalProductLookup)
    {
        if (serverNowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Server time must be UTC.", nameof(serverNowUtc));
        }
        if (effectiveCandidateLegalEntityIds.Count is 0)
        {
            return [new BsonDocument("$match", new BsonDocument("_id", BsonNull.Value))];
        }
        if (effectiveCandidateLegalEntityIds.Count > ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot
            || effectiveCandidateLegalEntityIds.Any(id => id == Guid.Empty)
            || effectiveCandidateLegalEntityIds.Distinct().Count() != effectiveCandidateLegalEntityIds.Count)
        {
            throw new ArgumentException(
                "Effective Legal Entity candidates must be bounded, non-empty and unique.",
                nameof(effectiveCandidateLegalEntityIds));
        }

        var tenant = GuidBson(tenantId);
        var candidates = new BsonArray(effectiveCandidateLegalEntityIds.Select(GuidBson));
        var rawPeriodIds = "$$period." + nameof(ProductLegalEntityScopePeriod.LegalEntityIds);
        var periodIds = ArrayOrEmpty(rawPeriodIds);
        var isCurrent = CreateCurrentPeriodExpression(serverNowUtc, "$$period");
        var accessiblePeriod = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("$and", new BsonArray
            {
                new BsonDocument("$eq", new BsonArray
                {
                    "$$period." + nameof(ProductLegalEntityScopePeriod.Mode),
                    (int)ProductLegalEntityScopeMode.GroupWide
                }),
                new BsonDocument("$eq", new BsonArray
                {
                    new BsonDocument("$size", periodIds),
                    0
                })
            }),
            new BsonDocument("$and", new BsonArray
            {
                new BsonDocument("$eq", new BsonArray
                {
                    "$$period." + nameof(ProductLegalEntityScopePeriod.Mode),
                    (int)ProductLegalEntityScopeMode.Scoped
                }),
                new BsonDocument("$gte", new BsonArray { new BsonDocument("$size", periodIds), 1 }),
                new BsonDocument("$lte", new BsonArray
                {
                    new BsonDocument("$size", periodIds),
                    ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot
                }),
                new BsonDocument("$eq", new BsonArray
                {
                    new BsonDocument("$size", periodIds),
                    new BsonDocument("$size", new BsonDocument("$setUnion", new BsonArray
                    {
                        periodIds,
                        new BsonArray()
                    }))
                }),
                new BsonDocument("$not", new BsonArray
                {
                    new BsonDocument("$in", new BsonArray { GuidBson(Guid.Empty), periodIds })
                }),
                new BsonDocument("$gt", new BsonArray
                {
                    new BsonDocument("$size", new BsonDocument("$setIntersection", new BsonArray
                    {
                        periodIds,
                        candidates
                    })),
                    0
                })
            })
        });

        var stages = new List<BsonDocument>();
        if (requireGlobalProductLookup)
        {
            stages.Add(LookupSingle(
                "mdm_global_products",
                "ScopeGlobalProducts",
                new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$TenantId", tenant }),
                    new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }),
                    new BsonDocument("$eq", new BsonArray { "$_id", "$$globalProductId" })
                },
                globalProductIdExpression));
        }

        stages.Add(LookupSingle(
                ProductLegalEntityScopePolicyRepository.CollectionName,
                "ScopePolicies",
                new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$TenantId", tenant }),
                    new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }),
                    new BsonDocument("$eq", new BsonArray { "$GlobalProductId", "$$globalProductId" })
                },
                globalProductIdExpression));
        stages.Add(new BsonDocument("$set", new BsonDocument("CurrentScopePeriods",
                new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray
                    {
                        new BsonDocument("$size", "$ScopePolicies"),
                        1
                    }),
                    new BsonDocument("$filter", new BsonDocument
                    {
                        { "input", ArrayOrEmpty(new BsonDocument("$getField", new BsonDocument
                            {
                                { "field", nameof(ProductLegalEntityScopePolicy.ScopePeriods) },
                                { "input", new BsonDocument("$arrayElemAt", new BsonArray { "$ScopePolicies", 0 }) }
                            })) },
                        { "as", "period" },
                        { "cond", isCurrent }
                    }),
                    new BsonArray()
                }))));
        var validityChecks = new BsonArray
            {
                new BsonDocument("$eq", new BsonArray
                {
                    new BsonDocument("$size", "$ScopePolicies"), 1
                })
            };
        if (requireGlobalProductLookup)
        {
            validityChecks.Add(new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$size", "$ScopeGlobalProducts"), 1
            }));
        }
        validityChecks.Add(new BsonDocument("$let", new BsonDocument
        {
            { "vars", new BsonDocument("policy",
                new BsonDocument("$arrayElemAt", new BsonArray { "$ScopePolicies", 0 })) },
            { "in", CreatePolicyValidityExpression(serverNowUtc) }
        }));
        validityChecks.Add(new BsonDocument("$eq", new BsonArray
        {
            new BsonDocument("$size", "$CurrentScopePeriods"), 1
        }));
        validityChecks.Add(new BsonDocument("$let", new BsonDocument
        {
            { "vars", new BsonDocument("period",
                new BsonDocument("$arrayElemAt", new BsonArray { "$CurrentScopePeriods", 0 })) },
            { "in", accessiblePeriod }
        }));

        stages.Add(new BsonDocument("$match", new BsonDocument(
            "$expr", new BsonDocument("$and", validityChecks))));
        return stages;
    }

    internal static BsonDocument CreatePolicyValidityExpression(DateTimeOffset serverNowUtc)
    {
        var nowTicks = serverNowUtc.Ticks;
        var emptyGuid = GuidBson(Guid.Empty);
        var rawPeriods = "$$policy." + nameof(ProductLegalEntityScopePolicy.ScopePeriods);
        var periods = ArrayOrEmpty(rawPeriods);
        var periodIds = Map(periods, "period", "$$period." + nameof(ProductLegalEntityScopePeriod.PeriodId));
        var commandIds = Map(periods, "period", "$$period." + nameof(ProductLegalEntityScopePeriod.CommandId));
        var endCommandIds = new BsonDocument("$map", new BsonDocument
        {
            { "input", new BsonDocument("$filter", new BsonDocument
                {
                    { "input", periods },
                    { "as", "period" },
                    { "cond", IsPresent("$$period." + nameof(ProductLegalEntityScopePeriod.EndCommandId)) }
                }) },
            { "as", "period" },
            { "in", "$$period." + nameof(ProductLegalEntityScopePeriod.EndCommandId) }
        });
        var rawLegalEntityIds = "$$period." + nameof(ProductLegalEntityScopePeriod.LegalEntityIds);
        var legalEntityIds = ArrayOrEmpty(rawLegalEntityIds);
        var hasEffectiveTo = IsPresent("$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveToUtc));
        var hasEndCommand = IsPresent("$$period." + nameof(ProductLegalEntityScopePeriod.EndCommandId));
        var hasEndedBy = IsPresent("$$period." + nameof(ProductLegalEntityScopePeriod.EndedByActorId));
        var hasEndedAt = IsPresent("$$period." + nameof(ProductLegalEntityScopePeriod.EndedAtUtc));
        var hasAnyEndFact = new BsonDocument("$or", new BsonArray
        {
            hasEndCommand, hasEndedBy, hasEndedAt
        });
        var hasCompleteEndFacts = new BsonDocument("$and", new BsonArray
        {
            hasEndCommand, hasEndedBy, hasEndedAt
        });
        var validLegalEntities = new BsonDocument("$and", new BsonArray
        {
            TypeIs(rawLegalEntityIds, "array"),
            AllElementsAreGuid(legalEntityIds),
            new BsonDocument("$lte", new BsonArray
            {
                new BsonDocument("$size", legalEntityIds),
                ProductLegalEntityScopePolicy.MaximumLegalEntityIdsPerSnapshot
            }),
            new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$size", legalEntityIds),
                new BsonDocument("$size", new BsonDocument("$setUnion", new BsonArray
                {
                    legalEntityIds,
                    new BsonArray()
                }))
            }),
            new BsonDocument("$not", new BsonArray
            {
                new BsonDocument("$in", new BsonArray { emptyGuid, legalEntityIds })
            }),
            new BsonDocument("$eq", new BsonArray
            {
                legalEntityIds,
                new BsonDocument("$sortArray", new BsonDocument
                {
                    { "input", legalEntityIds },
                    { "sortBy", 1 }
                })
            }),
            new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("$and", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray
                    {
                        "$$period." + nameof(ProductLegalEntityScopePeriod.Mode),
                        (int)ProductLegalEntityScopeMode.GroupWide
                    }),
                    new BsonDocument("$eq", new BsonArray
                    {
                        new BsonDocument("$size", legalEntityIds), 0
                    })
                }),
                new BsonDocument("$and", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray
                    {
                        "$$period." + nameof(ProductLegalEntityScopePeriod.Mode),
                        (int)ProductLegalEntityScopeMode.Scoped
                    }),
                    new BsonDocument("$gte", new BsonArray
                    {
                        new BsonDocument("$size", legalEntityIds), 1
                    })
                })
            })
        });
        var validPeriod = new BsonDocument("$and", new BsonArray
        {
            ExactGuid("$$period." + nameof(ProductLegalEntityScopePeriod.PeriodId)),
            ExactGuid("$$period." + nameof(ProductLegalEntityScopePeriod.CommandId)),
            ExactGuid("$$period." + nameof(ProductLegalEntityScopePeriod.ActorId)),
            TypeIs("$$period." + nameof(ProductLegalEntityScopePeriod.Mode), "int"),
            IsCanonicalUtcDateTimeOffset(
                "$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveFromUtc)),
            IsCanonicalUtcDateTimeOffset(
                "$$period." + nameof(ProductLegalEntityScopePeriod.CreatedAtUtc)),
            new BsonDocument("$lte", new BsonArray
            {
                DateTimeOffsetTicks("$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveFromUtc)),
                nowTicks
            }),
            new BsonDocument("$lte", new BsonArray
            {
                DateTimeOffsetTicks("$$period." + nameof(ProductLegalEntityScopePeriod.CreatedAtUtc)),
                nowTicks
            }),
            new BsonDocument("$eq", new BsonArray { hasAnyEndFact, hasCompleteEndFacts }),
            new BsonDocument("$eq", new BsonArray { hasEffectiveTo, hasCompleteEndFacts }),
            new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("$not", new BsonArray { hasCompleteEndFacts }),
                new BsonDocument("$and", new BsonArray
                {
                    ExactGuid("$$period." + nameof(ProductLegalEntityScopePeriod.EndCommandId)),
                    ExactGuid("$$period." + nameof(ProductLegalEntityScopePeriod.EndedByActorId)),
                    IsCanonicalUtcDateTimeOffset(
                        "$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveToUtc)),
                    IsCanonicalUtcDateTimeOffset(
                        "$$period." + nameof(ProductLegalEntityScopePeriod.EndedAtUtc)),
                    new BsonDocument("$gt", new BsonArray
                    {
                        DateTimeOffsetTicks("$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveToUtc)),
                        DateTimeOffsetTicks("$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveFromUtc))
                    }),
                    new BsonDocument("$lte", new BsonArray
                    {
                        DateTimeOffsetTicks("$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveToUtc)),
                        nowTicks
                    }),
                    new BsonDocument("$eq", new BsonArray
                    {
                        DateTimeOffsetTicks("$$period." + nameof(ProductLegalEntityScopePeriod.EndedAtUtc)),
                        DateTimeOffsetTicks("$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveToUtc))
                    })
                })
            }),
            validLegalEntities
        });
        var historyValid = new BsonDocument("$allElementsTrue", new BsonArray
        {
            new BsonDocument("$map", new BsonDocument
            {
                { "input", new BsonDocument("$range", new BsonArray
                    {
                        0, new BsonDocument("$size", periods)
                    }) },
                { "as", "index" },
                { "in", new BsonDocument("$let", new BsonDocument
                    {
                        { "vars", new BsonDocument
                            {
                                { "period", new BsonDocument("$arrayElemAt", new BsonArray { periods, "$$index" }) },
                                { "previous", new BsonDocument("$arrayElemAt", new BsonArray
                                    {
                                        periods,
                                        new BsonDocument("$subtract", new BsonArray { "$$index", 1 })
                                    }) },
                                { "next", new BsonDocument("$arrayElemAt", new BsonArray
                                    {
                                        periods,
                                        new BsonDocument("$add", new BsonArray { "$$index", 1 })
                                    }) }
                            }
                        },
                        { "in", new BsonDocument("$and", new BsonArray
                            {
                                new BsonDocument("$or", new BsonArray
                                {
                                    new BsonDocument("$eq", new BsonArray { "$$index", 0 }),
                                    new BsonDocument("$and", new BsonArray
                                    {
                                        IsPresent("$$previous." + nameof(ProductLegalEntityScopePeriod.EffectiveToUtc)),
                                        new BsonDocument("$lte", new BsonArray
                                        {
                                            DateTimeOffsetTicks(
                                                "$$previous." + nameof(ProductLegalEntityScopePeriod.EffectiveToUtc)),
                                            DateTimeOffsetTicks(
                                                "$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveFromUtc))
                                        })
                                    })
                                }),
                                new BsonDocument("$or", new BsonArray
                                {
                                    new BsonDocument("$not", new BsonArray
                                    {
                                        new BsonDocument("$in", new BsonArray
                                        {
                                            "$$period." + nameof(ProductLegalEntityScopePeriod.EndCommandId), commandIds
                                        })
                                    }),
                                    new BsonDocument("$and", new BsonArray
                                    {
                                        new BsonDocument("$lt", new BsonArray
                                        {
                                            new BsonDocument("$add", new BsonArray { "$$index", 1 }),
                                            new BsonDocument("$size", periods)
                                        }),
                                        new BsonDocument("$eq", new BsonArray
                                        {
                                            "$$next." + nameof(ProductLegalEntityScopePeriod.CommandId),
                                            "$$period." + nameof(ProductLegalEntityScopePeriod.EndCommandId)
                                        }),
                                        new BsonDocument("$eq", new BsonArray
                                        {
                                            DateTimeOffsetTicks(
                                                "$$next." + nameof(ProductLegalEntityScopePeriod.EffectiveFromUtc)),
                                            DateTimeOffsetTicks(
                                                "$$period." + nameof(ProductLegalEntityScopePeriod.EffectiveToUtc))
                                        })
                                    })
                                })
                            })
                        }
                    })
                }
            })
        });

        return new BsonDocument("$and", new BsonArray
        {
            ExactGuid("$$policy._id"),
            ExactGuid("$$policy." + nameof(ProductLegalEntityScopePolicy.TenantId)),
            ExactGuid("$$policy." + nameof(ProductLegalEntityScopePolicy.GlobalProductId)),
            ExactGuid("$$policy." + nameof(ProductLegalEntityScopePolicy.CreationCommandId)),
            TypeIs("$$policy." + nameof(ProductLegalEntityScopePolicy.Version), "int"),
            TypeIs(rawPeriods, "array"),
            new BsonDocument("$gte", new BsonArray
            {
                "$$policy." + nameof(ProductLegalEntityScopePolicy.Version), 0
            }),
            new BsonDocument("$lte", new BsonArray
            {
                new BsonDocument("$size", periods), ProductLegalEntityScopePolicy.MaximumPeriodsPerPolicy
            }),
            SetIsUnique(periodIds),
            SetIsUnique(commandIds),
            SetIsUnique(endCommandIds),
            new BsonDocument("$eq", new BsonArray
            {
                periods,
                new BsonDocument("$sortArray", new BsonDocument
                {
                    { "input", periods },
                    { "sortBy", new BsonDocument
                        {
                            { nameof(ProductLegalEntityScopePeriod.EffectiveFromUtc), 1 },
                            { nameof(ProductLegalEntityScopePeriod.PeriodId), 1 }
                        }
                    }
                })
            }),
            new BsonDocument("$allElementsTrue", new BsonArray
            {
                new BsonDocument("$map", new BsonDocument
                {
                    { "input", periods },
                    { "as", "period" },
                    { "in", validPeriod }
                })
            }),
            historyValid
        });
    }

    private static BsonDocument Map(BsonValue input, string alias, BsonValue expression) =>
        new("$map", new BsonDocument
        {
            { "input", input },
            { "as", alias },
            { "in", expression }
        });

    private static BsonDocument SetIsUnique(BsonValue input) => new("$eq", new BsonArray
    {
        new BsonDocument("$size", input),
        new BsonDocument("$size", new BsonDocument("$setUnion", new BsonArray
        {
            input,
            new BsonArray()
        }))
    });

    private static BsonDocument ExactGuid(string expression) => new("$and", new BsonArray
    {
        TypeIs(expression, "binData"),
        new BsonDocument("$eq", new BsonArray
        {
            new BsonDocument("$cond", new BsonArray
            {
                TypeIs(expression, "binData"),
                new BsonDocument("$binarySize", expression),
                -1
            }),
            16
        }),
        new BsonDocument("$ne", new BsonArray { expression, GuidBson(Guid.Empty) })
    });

    private static BsonDocument AllElementsAreGuid(BsonValue input) => new("$allElementsTrue", new BsonArray
    {
        new BsonDocument("$map", new BsonDocument
        {
            { "input", input },
            { "as", "candidateGuid" },
            { "in", ExactGuid("$$candidateGuid") }
        })
    });

    internal static BsonDocument ArrayOrEmpty(BsonValue input) => new("$cond", new BsonArray
    {
        TypeIs(input, "array"),
        input,
        new BsonArray()
    });

    private static BsonDocument TypeIs(BsonValue expression, string bsonType) => new("$eq", new BsonArray
    {
        new BsonDocument("$type", expression), bsonType
    });

    private static BsonDocument IsCanonicalUtcDateTimeOffset(BsonValue expression)
    {
        var value = ArrayOrEmpty(expression);
        return new BsonDocument("$and", new BsonArray
        {
            TypeIs(expression, "array"),
            new BsonDocument("$eq", new BsonArray { new BsonDocument("$size", value), 2 }),
            TypeIs(new BsonDocument("$arrayElemAt", new BsonArray { value, 0 }), "long"),
            TypeIs(new BsonDocument("$arrayElemAt", new BsonArray { value, 1 }), "int"),
            new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$arrayElemAt", new BsonArray { value, 1 }), 0
            })
        });
    }

    private static BsonDocument DateTimeOffsetTicks(BsonValue expression) => new(
        "$arrayElemAt",
        new BsonArray { ArrayOrEmpty(expression), 0 });

    internal static BsonDocument CreateCurrentPeriodExpression(
        DateTimeOffset serverNowUtc,
        string periodExpression)
    {
        if (serverNowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Server time must be UTC.", nameof(serverNowUtc));
        }

        var effectiveFrom = periodExpression + "." + nameof(ProductLegalEntityScopePeriod.EffectiveFromUtc);
        var effectiveTo = periodExpression + "." + nameof(ProductLegalEntityScopePeriod.EffectiveToUtc);
        return new BsonDocument("$and", new BsonArray
        {
            IsCanonicalUtcDateTimeOffset(effectiveFrom),
            new BsonDocument("$lte", new BsonArray
            {
                DateTimeOffsetTicks(effectiveFrom), serverNowUtc.Ticks
            }),
            new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("$eq", new BsonArray
                {
                    new BsonDocument("$type", effectiveTo), "missing"
                }),
                new BsonDocument("$eq", new BsonArray { effectiveTo, BsonNull.Value }),
                new BsonDocument("$and", new BsonArray
                {
                    IsCanonicalUtcDateTimeOffset(effectiveTo),
                    new BsonDocument("$gt", new BsonArray
                    {
                        DateTimeOffsetTicks(effectiveTo), serverNowUtc.Ticks
                    })
                })
            })
        });
    }

    private static BsonDocument IsPresent(string expression) => new("$and", new BsonArray
    {
        new BsonDocument("$ne", new BsonArray
        {
            new BsonDocument("$type", expression), "missing"
        }),
        new BsonDocument("$ne", new BsonArray { expression, BsonNull.Value })
    });

    internal static IReadOnlyList<BsonDocument> CreateGskuRevisionResolutionStages(
        Guid tenantId,
        string gskuIdExpression)
    {
        var tenant = GuidBson(tenantId);
        return
        [
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "mdm_gskus" },
                { "let", new BsonDocument("gskuId", gskuIdExpression) },
                { "pipeline", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$TenantId", tenant }),
                            new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }),
                            new BsonDocument("$eq", new BsonArray { "$_id", "$$gskuId" })
                        })))
                    }
                },
                { "as", "ScopeGskus" }
            }),
            new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$size", "$ScopeGskus"),
                1
            }))),
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "mdm_product_definition_revisions" },
                { "let", new BsonDocument("revisionId", new BsonDocument("$getField", new BsonDocument
                    {
                        { "field", nameof(Gsku.ProductDefinitionRevisionId) },
                        { "input", new BsonDocument("$arrayElemAt", new BsonArray { "$ScopeGskus", 0 }) }
                    }))
                },
                { "pipeline", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$TenantId", tenant }),
                            new BsonDocument("$eq", new BsonArray { "$IsDeleted", false }),
                            new BsonDocument("$eq", new BsonArray { "$_id", "$$revisionId" })
                        })))
                    }
                },
                { "as", "ScopeRevisions" }
            }),
            new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$eq", new BsonArray
            {
                new BsonDocument("$size", "$ScopeRevisions"),
                1
            }))),
            new BsonDocument("$set", new BsonDocument(
                ResolvedGlobalProductIdField,
                new BsonDocument("$getField", new BsonDocument
                {
                    { "field", nameof(ProductDefinitionRevision.GlobalProductId) },
                    { "input", new BsonDocument("$arrayElemAt", new BsonArray { "$ScopeRevisions", 0 }) }
                })))
        ];
    }

    internal static BsonDocument LookupSingle(
        string collection,
        string outputField,
        BsonArray expressions,
        string globalProductIdExpression) => new("$lookup", new BsonDocument
    {
        { "from", collection },
        { "let", new BsonDocument("globalProductId", globalProductIdExpression) },
        { "pipeline", new BsonArray
            {
                new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", expressions)))
            }
        },
        { "as", outputField }
    });

    internal static BsonBinaryData GuidBson(Guid value) =>
        new(value, GuidRepresentation.Standard);

    internal static BsonDocument CleanupStage(params string[] additionalFields) =>
        new("$unset", new BsonArray(new[]
        {
            "ScopeGlobalProducts",
            "ScopePolicies",
            "CurrentScopePeriods",
            ResolvedGlobalProductIdField
        }.Concat(additionalFields)));
}
