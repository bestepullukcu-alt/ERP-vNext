using System.Globalization;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FinishedGoodIdentityWorkflowOperationRepository
    : IFinishedGoodIdentityWorkflowOperationRepository
{
    public const string CollectionName = "mdm_finished_good_identity_workflow_operations";
    private readonly IMongoCollection<FinishedGoodIdentityWorkflowOperation> _operations;
    private readonly Guid _tenantId;

    public FinishedGoodIdentityWorkflowOperationRepository(
        IMongoDatabase database,
        ITenantContext tenantContext)
    {
        _operations = database.GetCollection<FinishedGoodIdentityWorkflowOperation>(CollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<FinishedGoodIdentityWorkflowReserveResult> ReserveAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsValidForReservation(operation))
        {
            return new(false, false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_OPERATION_INVALID");
        }

        operation.TenantId = _tenantId;
        operation.Id = operation.Id == Guid.Empty ? Guid.NewGuid() : operation.Id;
        operation.IsDeleted = false;
        operation.DeletedAt = null;
        operation.Checkpoint = FinishedGoodIdentityWorkflowCheckpoint.Prepared;
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
        operation.WorkflowInstanceId = null;
        operation.WorkflowTemplateVersionId = null;
        operation.ApprovalTaskId = null;
        operation.AssignmentSnapshotId = null;
        operation.StartTransitionLogId = null;
        operation.WorkflowStartedAtUtcTicksV1 = null;
        operation.DecisionTransitionLogId = null;
        operation.DecisionKind = null;
        operation.DecisionObservedAtUtcTicksV1 = null;
        operation.DecisionActorSubjectId = null;
        operation.DecisionReasonCode = null;
        operation.DecisionObjectType = null;
        operation.DecisionObjectId = null;
        operation.DecisionObjectRef = null;
        operation.DecisionWorkflowTemplateId = null;
        operation.DecisionWorkflowTemplateVersionId = null;
        operation.DecisionTaskStatus = null;
        operation.DecisionInstanceStatus = null;
        operation.DecisionTransitionSequence = null;
        operation.DecisionAtUtcTicksV1 = null;
        operation.ApprovalParentValidatedAtUtcTicksV1 = null;
        operation.ApprovalParentProofFingerprint = null;
        operation.NextAttemptAtUtcTicksV1 = null;
        operation.LastFailureCode = null;
        operation.LeaseOwner = null;
        operation.LeaseUntilUtcTicksV1 = null;
        operation.LeaseGeneration = 0;
        operation.TemporalStorageVersion = FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion;
        operation.Version = 0;
        operation.CreatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1 > 0
            ? operation.CreatedAtUtcTicksV1
            : DateTimeOffset.UtcNow.UtcTicks;
        operation.UpdatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1;
        operation.CreatedAt = new DateTimeOffset(operation.CreatedAtUtcTicksV1, TimeSpan.Zero);
        operation.UpdatedAt = operation.CreatedAt;

        if (operation.ToBson().Length > 1024 * 1024 - 4096)
        {
            return new(false, false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_OPERATION_TOO_LARGE");
        }

        try
        {
            await _operations.InsertOneAsync(operation, cancellationToken: cancellationToken);
            return new(true, false, operation, null);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _operations.Find(ActiveTenantFilter & Builders<FinishedGoodIdentityWorkflowOperation>
                    .Filter.Or(
                        Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(item => item.OperationId, operation.OperationId),
                        Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                            item => item.StartIdempotencyKey, operation.StartIdempotencyKey),
                        Builders<FinishedGoodIdentityWorkflowOperation>.Filter.And(
                            Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                                item => item.FinishedGoodId, operation.FinishedGoodId),
                            Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ExpectedFinishedGoodVersion, operation.ExpectedFinishedGoodVersion))))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null && IsExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, null, "FINISHED_GOOD_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
        }
    }

    public Task<FinishedGoodIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                             item => item.OperationId, operationId))
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<FinishedGoodIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                             item => item.StartIdempotencyKey, startIdempotencyKey))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<FinishedGoodIdentityWorkflowClaim?> TryClaimAsync(
        FinishedGoodIdentityWorkflowClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty
            || !IsLowerHex(request.OperationFingerprint, 64)
            || request.EligibleCheckpoints.Count == 0
            || !IsExactBounded(request.LeaseOwner, 128)
            || request.NowUtcTicks <= 0
            || request.LeaseUntilUtcTicks <= request.NowUtcTicks
            || request.LeaseUntilUtcTicks - request.NowUtcTicks > TimeSpan.FromMinutes(5).Ticks)
        {
            return null;
        }

        var filter = ActiveTenantFilter
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, request.OperationId)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, request.OperationFingerprint)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.In(
                         item => item.Checkpoint, request.EligibleCheckpoints)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Or(
                         Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, request.NowUtcTicks))
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Or(
                         Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, request.NowUtcTicks));
        var update = Builders<FinishedGoodIdentityWorkflowOperation>.Update
            .Set(item => item.LeaseOwner, request.LeaseOwner)
            .Set(item => item.LeaseUntilUtcTicksV1, request.LeaseUntilUtcTicks)
            .Set(item => item.UpdatedAtUtcTicksV1, request.NowUtcTicks)
            .Set(item => item.UpdatedAt, new DateTimeOffset(request.NowUtcTicks, TimeSpan.Zero))
            .Inc(item => item.LeaseGeneration, 1)
            .Inc(item => item.Version, 1);
        var operation = await _operations.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<FinishedGoodIdentityWorkflowOperation>
            {
                ReturnDocument = ReturnDocument.After,
                IsUpsert = false
            },
            cancellationToken);
        return operation is null
            ? null
            : new FinishedGoodIdentityWorkflowClaim(
                _tenantId,
                operation.OperationId,
                operation.FinishedGoodId,
                operation.GskuId,
                operation.ProductDefinitionRevisionId,
                operation.DecisionTransitionLogId,
                operation.DecisionTransitionSequence,
                operation.OperationFingerprint,
                request.LeaseOwner,
                operation.LeaseGeneration,
                operation.Checkpoint,
                request.LeaseUntilUtcTicks);
    }

    public async Task<FinishedGoodIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        FinishedGoodIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        if (nowUtcTicks <= 0 || limit is < 1 or > 100 || after?.OperationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var terminal = new[]
        {
            FinishedGoodIdentityWorkflowCheckpoint.Completed,
            FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            FinishedGoodIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            FinishedGoodIdentityWorkflowCheckpoint.Superseded
        };
        var filter = ActiveTenantFilter
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Nin(item => item.Checkpoint, terminal)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Or(
                         Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Or(
                         Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (after is not null)
        {
            filter &= after.NextAttemptAtUtcTicksV1.HasValue
                ? Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Or(
                    Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Gt(
                        item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                    Builders<FinishedGoodIdentityWorkflowOperation>.Filter.And(
                        Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                        Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)))
                : Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Or(
                    Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Ne(
                        item => item.NextAttemptAtUtcTicksV1, null),
                    Builders<FinishedGoodIdentityWorkflowOperation>.Filter.And(
                        Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, null),
                        Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)));
        }

        var rows = await _operations.Find(filter)
            .SortBy(item => item.NextAttemptAtUtcTicksV1)
            .ThenBy(item => item.OperationId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var next = rows.Count == limit
            ? new FinishedGoodIdentityWorkflowRecoveryCursor(
                rows[^1].NextAttemptAtUtcTicksV1,
                rows[^1].OperationId)
            : null;
        return new(rows, next);
    }

    public async Task<bool> AdvanceAsync(
        FinishedGoodIdentityWorkflowClaim claim,
        FinishedGoodIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != _tenantId
            || mutation.UpdatedAtUtcTicks <= 0
            || !IsAllowedTransition(claim.Checkpoint, mutation.NextCheckpoint)
            || !IsCoherentMutation(claim, mutation))
        {
            return false;
        }

        var filter = ActiveTenantFilter
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, claim.OperationId)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, claim.OperationFingerprint)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                         item => item.Checkpoint, claim.Checkpoint)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseOwner, claim.LeaseOwner)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseGeneration, claim.LeaseGeneration)
                     & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Gt(
                         item => item.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        if (mutation.NextCheckpoint == FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated)
        {
            filter &= Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalParentValidatedAtUtcTicksV1, null)
                      & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalParentProofFingerprint, null)
                      & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                          item => item.DecisionKind, ProductIdentityDecisionKind.Approved);
        }
        if (mutation.NextCheckpoint == FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied)
        {
            filter &= claim.Checkpoint == FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved
                ? Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                    item => item.DecisionKind, ProductIdentityDecisionKind.Rejected)
                : Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                    item => item.DecisionKind, ProductIdentityDecisionKind.Approved);
        }
        if (mutation.NextCheckpoint == FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved)
        {
            filter &= Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ObjectType, mutation.DecisionObjectType)
                      & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ObjectId, mutation.DecisionObjectId);
        }
        var update = Builders<FinishedGoodIdentityWorkflowOperation>.Update
            .Set(item => item.Checkpoint, mutation.NextCheckpoint)
            .Set(item => item.RecoveryDisposition, mutation.RecoveryDisposition)
            .Set(item => item.NextAttemptAtUtcTicksV1, mutation.NextAttemptAtUtcTicksV1)
            .Set(item => item.LastFailureCode, mutation.LastFailureCode)
            .Set(item => item.UpdatedAtUtcTicksV1, mutation.UpdatedAtUtcTicks)
            .Set(item => item.UpdatedAt, new DateTimeOffset(mutation.UpdatedAtUtcTicks, TimeSpan.Zero))
            .Inc(item => item.Version, 1);
        if (mutation.WorkflowInstanceId.HasValue) update = update.Set(item => item.WorkflowInstanceId, mutation.WorkflowInstanceId);
        if (mutation.WorkflowTemplateId.HasValue) update = update.Set(item => item.WorkflowTemplateId, mutation.WorkflowTemplateId);
        if (mutation.WorkflowTemplateVersionId.HasValue) update = update.Set(item => item.WorkflowTemplateVersionId, mutation.WorkflowTemplateVersionId);
        if (mutation.ApprovalTaskId.HasValue) update = update.Set(item => item.ApprovalTaskId, mutation.ApprovalTaskId);
        if (mutation.AssignmentSnapshotId.HasValue) update = update.Set(item => item.AssignmentSnapshotId, mutation.AssignmentSnapshotId);
        if (mutation.StartTransitionLogId.HasValue) update = update.Set(item => item.StartTransitionLogId, mutation.StartTransitionLogId);
        if (mutation.WorkflowStartedAtUtcTicksV1.HasValue)
            update = update.Set(item => item.WorkflowStartedAtUtcTicksV1, mutation.WorkflowStartedAtUtcTicksV1);
        if (mutation.DecisionTransitionLogId.HasValue) update = update.Set(item => item.DecisionTransitionLogId, mutation.DecisionTransitionLogId);
        if (mutation.DecisionKind.HasValue) update = update.Set(item => item.DecisionKind, mutation.DecisionKind);
        if (mutation.DecisionObservedAtUtcTicksV1.HasValue)
            update = update.Set(item => item.DecisionObservedAtUtcTicksV1, mutation.DecisionObservedAtUtcTicksV1);
        if (mutation.DecisionActorSubjectId.HasValue) update = update.Set(item => item.DecisionActorSubjectId, mutation.DecisionActorSubjectId);
        if (mutation.DecisionReasonCode is not null) update = update.Set(item => item.DecisionReasonCode, mutation.DecisionReasonCode);
        if (mutation.DecisionObjectType is not null) update = update.Set(item => item.DecisionObjectType, mutation.DecisionObjectType);
        if (mutation.DecisionObjectId is not null) update = update.Set(item => item.DecisionObjectId, mutation.DecisionObjectId);
        if (mutation.DecisionObjectRef is not null) update = update.Set(item => item.DecisionObjectRef, mutation.DecisionObjectRef);
        if (mutation.DecisionWorkflowTemplateId.HasValue) update = update.Set(item => item.DecisionWorkflowTemplateId, mutation.DecisionWorkflowTemplateId);
        if (mutation.DecisionWorkflowTemplateVersionId.HasValue) update = update.Set(item => item.DecisionWorkflowTemplateVersionId, mutation.DecisionWorkflowTemplateVersionId);
        if (mutation.DecisionTaskStatus is not null) update = update.Set(item => item.DecisionTaskStatus, mutation.DecisionTaskStatus);
        if (mutation.DecisionInstanceStatus is not null) update = update.Set(item => item.DecisionInstanceStatus, mutation.DecisionInstanceStatus);
        if (mutation.DecisionTransitionSequence.HasValue) update = update.Set(item => item.DecisionTransitionSequence, mutation.DecisionTransitionSequence);
        if (mutation.DecisionAtUtcTicksV1.HasValue) update = update.Set(item => item.DecisionAtUtcTicksV1, mutation.DecisionAtUtcTicksV1);
        if (mutation.ApprovalParentValidatedAtUtcTicksV1.HasValue)
            update = update.Set(item => item.ApprovalParentValidatedAtUtcTicksV1, mutation.ApprovalParentValidatedAtUtcTicksV1);
        if (mutation.ApprovalParentProofFingerprint is not null)
            update = update.Set(item => item.ApprovalParentProofFingerprint, mutation.ApprovalParentProofFingerprint);
        if (mutation.ReleaseLease)
        {
            update = update.Set(item => item.LeaseOwner, null).Set(item => item.LeaseUntilUtcTicksV1, null);
        }

        var result = await _operations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private FilterDefinition<FinishedGoodIdentityWorkflowOperation> ActiveTenantFilter =>
        Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(item => item.TenantId, _tenantId)
        & Builders<FinishedGoodIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false);

    private bool IsValidForReservation(FinishedGoodIdentityWorkflowOperation operation) =>
        _tenantId != Guid.Empty
        && operation.OperationId != Guid.Empty
        && operation.FinishedGoodId != Guid.Empty
        && operation.GskuId != Guid.Empty
        && operation.ProductDefinitionRevisionId != Guid.Empty
        && operation.ExpectedFinishedGoodVersion >= 0
        && operation.MakerSubjectId != Guid.Empty
        && (operation.WorkflowTemplateId.HasValue ^ operation.WorkflowTemplateCode is not null)
        && operation.CandidatePrincipalIds is { Count: >= 1 and <= 100 }
        && operation.CandidatePrincipalIds.All(item => item != Guid.Empty)
        && operation.CandidatePrincipalIds.Distinct().Count() == operation.CandidatePrincipalIds.Count
        && IsExactBounded(operation.ReasonCode, 128)
        && operation.ObjectType == "finished-good"
        && operation.ObjectId == operation.FinishedGoodId.ToString("D")
        && IsExactBounded(operation.ObjectRef, 256)
        && IsExactBounded(operation.StartIdempotencyKey, 256)
        && IsLowerHex(operation.OperationFingerprint, 64)
        && (!operation.WorkflowTemplateId.HasValue || operation.WorkflowTemplateId != Guid.Empty)
        && (operation.WorkflowTemplateCode is null || IsExactBounded(operation.WorkflowTemplateCode, 128))
        && operation.TemporalStorageVersion is 0 or FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion;

    private static bool IsExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 }
        && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsAllowedTransition(
        FinishedGoodIdentityWorkflowCheckpoint current,
        FinishedGoodIdentityWorkflowCheckpoint next) =>
        current == next
        || (current, next) switch
        {
            (FinishedGoodIdentityWorkflowCheckpoint.Prepared,
                FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted
                or FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted,
                FinishedGoodIdentityWorkflowCheckpoint.LocalPendingApplied
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.LocalPendingApplied,
                FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision,
                FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved,
                FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated
                or FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated,
                FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied,
                FinishedGoodIdentityWorkflowCheckpoint.Completed
                or FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            _ => false
        };

    private static bool IsExactReplay(
        FinishedGoodIdentityWorkflowOperation existing,
        FinishedGoodIdentityWorkflowOperation candidate) =>
        existing.OperationId == candidate.OperationId
        && existing.FinishedGoodId == candidate.FinishedGoodId
        && existing.GskuId == candidate.GskuId
        && existing.ProductDefinitionRevisionId == candidate.ProductDefinitionRevisionId
        && existing.ExpectedFinishedGoodVersion == candidate.ExpectedFinishedGoodVersion
        && existing.MakerSubjectId == candidate.MakerSubjectId
        && existing.WorkflowTemplateId == candidate.WorkflowTemplateId
        && existing.WorkflowTemplateCode == candidate.WorkflowTemplateCode
        && existing.CandidatePrincipalIds.SequenceEqual(candidate.CandidatePrincipalIds)
        && existing.ReasonCode == candidate.ReasonCode
        && existing.CommentRequired == candidate.CommentRequired
        && existing.EvidenceRequired == candidate.EvidenceRequired
        && existing.ObjectType == candidate.ObjectType
        && existing.ObjectId == candidate.ObjectId
        && existing.ObjectRef == candidate.ObjectRef
        && existing.StartIdempotencyKey == candidate.StartIdempotencyKey
        && existing.OperationFingerprint == candidate.OperationFingerprint;

    private static bool IsCoherentMutation(
        FinishedGoodIdentityWorkflowClaim claim,
        FinishedGoodIdentityWorkflowCheckpointMutation mutation)
    {
        var current = claim.Checkpoint;
        var hasAnyStartProof = mutation.WorkflowInstanceId.HasValue
            || mutation.WorkflowTemplateId.HasValue
            || mutation.WorkflowTemplateVersionId.HasValue
            || mutation.ApprovalTaskId.HasValue
            || mutation.AssignmentSnapshotId.HasValue
            || mutation.StartTransitionLogId.HasValue
            || mutation.WorkflowStartedAtUtcTicksV1.HasValue;
        var hasCompleteStartProof = mutation.WorkflowInstanceId is { } workflowId && workflowId != Guid.Empty
            && mutation.WorkflowTemplateId is { } templateId && templateId != Guid.Empty
            && mutation.WorkflowTemplateVersionId is { } templateVersionId && templateVersionId != Guid.Empty
            && mutation.ApprovalTaskId is { } taskId && taskId != Guid.Empty
            && mutation.AssignmentSnapshotId is { } snapshotId && snapshotId != Guid.Empty
            && mutation.StartTransitionLogId is { } transitionId && transitionId != Guid.Empty
            && mutation.WorkflowStartedAtUtcTicksV1 is > 0;
        var hasAnyDecisionProof = mutation.DecisionTransitionLogId.HasValue
            || mutation.DecisionKind.HasValue
            || mutation.DecisionObservedAtUtcTicksV1.HasValue
            || mutation.DecisionActorSubjectId.HasValue
            || mutation.DecisionReasonCode is not null
            || mutation.DecisionObjectType is not null
            || mutation.DecisionObjectId is not null
            || mutation.DecisionObjectRef is not null
            || mutation.DecisionWorkflowTemplateId.HasValue
            || mutation.DecisionWorkflowTemplateVersionId.HasValue
            || mutation.DecisionTaskStatus is not null
            || mutation.DecisionInstanceStatus is not null
            || mutation.DecisionTransitionSequence.HasValue
            || mutation.DecisionAtUtcTicksV1.HasValue;
        var hasCompleteDecisionProof = mutation.DecisionKind is ProductIdentityDecisionKind.Approved or ProductIdentityDecisionKind.Rejected
            && mutation.DecisionObservedAtUtcTicksV1 is > 0
            && mutation.DecisionActorSubjectId is { } decisionActor && decisionActor != Guid.Empty
            && IsOptionalExactBounded(mutation.DecisionReasonCode, 128)
            && mutation.DecisionObjectType == "finished-good"
            && Guid.TryParseExact(mutation.DecisionObjectId, "D", out var decisionObjectId)
            && decisionObjectId != Guid.Empty
            && IsExactBounded(mutation.DecisionObjectRef, 256)
            && mutation.DecisionWorkflowTemplateId is { } decisionTemplateId && decisionTemplateId != Guid.Empty
            && mutation.DecisionWorkflowTemplateVersionId is { } decisionTemplateVersionId && decisionTemplateVersionId != Guid.Empty
            && IsExactBounded(mutation.DecisionTaskStatus, 64)
            && IsExactBounded(mutation.DecisionInstanceStatus, 64)
            && mutation.DecisionTransitionSequence is > 0
            && mutation.DecisionAtUtcTicksV1 is > 0
            && (mutation.DecisionKind == ProductIdentityDecisionKind.Approved
                && mutation.DecisionTaskStatus == "Approved"
                && mutation.DecisionInstanceStatus == "Completed"
                || mutation.DecisionKind == ProductIdentityDecisionKind.Rejected
                && mutation.DecisionTaskStatus == "Rejected"
                && mutation.DecisionInstanceStatus == "Rejected"
                && IsExactBounded(mutation.DecisionReasonCode, 128));
        var hasAnyApprovalProof = mutation.ApprovalParentValidatedAtUtcTicksV1.HasValue
            || mutation.ApprovalParentProofFingerprint is not null;

        if (mutation.NextCheckpoint == FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted)
        {
            return hasCompleteStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof;
        }
        if (mutation.NextCheckpoint == FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved)
        {
            return !hasAnyStartProof && hasCompleteDecisionProof && !hasAnyApprovalProof;
        }
        if (mutation.NextCheckpoint != FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated)
        {
            if (mutation.NextCheckpoint == current)
            {
                return !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof
                    && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                    && mutation.NextAttemptAtUtcTicksV1 > mutation.UpdatedAtUtcTicks
                    && IsExactBounded(mutation.LastFailureCode, 128)
                    && mutation.ReleaseLease;
            }
            return !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof
                && IsOptionalExactBounded(mutation.LastFailureCode, 128)
                && !mutation.NextAttemptAtUtcTicksV1.HasValue;
        }

        return current == FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved
            && !hasAnyStartProof
            && !hasAnyDecisionProof
            && mutation.ApprovalParentValidatedAtUtcTicksV1 is > 0
            && mutation.ApprovalParentProofFingerprint is { Length: 64 } fingerprint
            && string.Equals(
                fingerprint,
                ApprovalProofFingerprint(
                    claim.TenantId,
                    claim.OperationId,
                    claim.FinishedGoodId,
                    claim.GskuId,
                    claim.ProductDefinitionRevisionId,
                    claim.DecisionTransitionSequence,
                    mutation.ApprovalParentValidatedAtUtcTicksV1.Value),
                StringComparison.Ordinal);
    }

    private static string ApprovalProofFingerprint(
        Guid tenantId,
        Guid operationId,
        Guid finishedGoodId,
        Guid gskuId,
        Guid productDefinitionRevisionId,
        long? decisionTransitionSequence,
        long validatedAtUtcTicks)
    {
        var facts = string.Join('|',
            "finished-good-approval-parent-v1",
            tenantId.ToString("D"),
            operationId.ToString("D"),
            finishedGoodId.ToString("D"),
            gskuId.ToString("D"),
            productDefinitionRevisionId.ToString("D"),
            decisionTransitionSequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            validatedAtUtcTicks.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static bool IsOptionalExactBounded(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsLowerHex(string? value, int exactLength) =>
        value is not null
        && value.Length == exactLength
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private void EnsureIndexes()
    {
        var active = new BsonDocument(nameof(FinishedGoodIdentityWorkflowOperation.IsDeleted), false);
        _operations.Indexes.CreateMany(
        [
            new CreateIndexModel<FinishedGoodIdentityWorkflowOperation>(
                Builders<FinishedGoodIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.OperationId),
                new CreateIndexOptions<FinishedGoodIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_finished_good_identity_workflow_operation" }),
            new CreateIndexModel<FinishedGoodIdentityWorkflowOperation>(
                Builders<FinishedGoodIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.StartIdempotencyKey),
                new CreateIndexOptions<FinishedGoodIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_finished_good_identity_workflow_start_key" }),
            new CreateIndexModel<FinishedGoodIdentityWorkflowOperation>(
                Builders<FinishedGoodIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.FinishedGoodId)
                    .Ascending(item => item.ExpectedFinishedGoodVersion),
                new CreateIndexOptions<FinishedGoodIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_finished_good_identity_workflow_finished_good_version" }),
            new CreateIndexModel<FinishedGoodIdentityWorkflowOperation>(
                Builders<FinishedGoodIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.IsDeleted)
                    .Ascending(item => item.NextAttemptAtUtcTicksV1).Ascending(item => item.OperationId)
                    .Ascending(item => item.Checkpoint).Ascending(item => item.LeaseUntilUtcTicksV1)
                    .Ascending(item => item.CreatedAtUtcTicksV1),
                new CreateIndexOptions { Name = "ix_mdm_finished_good_identity_workflow_recovery" })
        ]);
    }
}
