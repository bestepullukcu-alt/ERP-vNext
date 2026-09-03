using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FirstGskuIdentityWorkflowOperationRepository
    : IFirstGskuIdentityWorkflowOperationRepository
{
    public const string CollectionName = "mdm_first_gsku_identity_workflow_operations";
    private readonly IMongoCollection<FirstGskuIdentityWorkflowOperation> _operations;
    private readonly Guid _tenantId;

    public FirstGskuIdentityWorkflowOperationRepository(
        IMongoDatabase database,
        ITenantContext tenantContext)
    {
        _operations = database.GetCollection<FirstGskuIdentityWorkflowOperation>(CollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<FirstGskuIdentityWorkflowReserveResult> ReserveAsync(
        FirstGskuIdentityWorkflowOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsValidForReservation(operation))
        {
            return new(false, false, null, "FIRST_GSKU_IDENTITY_WORKFLOW_OPERATION_INVALID");
        }

        operation.TenantId = _tenantId;
        operation.Id = operation.Id == Guid.Empty ? Guid.NewGuid() : operation.Id;
        operation.IsDeleted = false;
        operation.DeletedAt = null;
        operation.Checkpoint = FirstGskuIdentityWorkflowCheckpoint.Prepared;
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
        operation.WorkflowInstanceId = null;
        operation.WorkflowTemplateVersionId = null;
        operation.ApprovalTaskId = null;
        operation.AssignmentSnapshotId = null;
        operation.StartTransitionLogId = null;
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
        operation.ApprovalPackApplicabilitySelection = null;
        operation.ApprovalPackUomSelection = null;
        operation.ReferencesValidatedAtUtcTicksV1 = null;
        operation.ApprovalReferenceProofFingerprint = null;
        operation.NextAttemptAtUtcTicksV1 = null;
        operation.LastFailureCode = null;
        operation.LeaseOwner = null;
        operation.LeaseUntilUtcTicksV1 = null;
        operation.LeaseGeneration = 0;
        operation.TemporalStorageVersion = FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion;
        operation.Version = 0;
        operation.CreatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1 > 0
            ? operation.CreatedAtUtcTicksV1
            : DateTimeOffset.UtcNow.UtcTicks;
        operation.UpdatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1;
        operation.CreatedAt = new DateTimeOffset(operation.CreatedAtUtcTicksV1, TimeSpan.Zero);
        operation.UpdatedAt = operation.CreatedAt;

        try
        {
            await _operations.InsertOneAsync(operation, cancellationToken: cancellationToken);
            return new(true, false, operation, null);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _operations.Find(ActiveTenantFilter & Builders<FirstGskuIdentityWorkflowOperation>
                    .Filter.Or(
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.OperationId, operation.OperationId),
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                            item => item.StartIdempotencyKey, operation.StartIdempotencyKey),
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.And(
                            Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ProductDefinitionRevisionId, operation.ProductDefinitionRevisionId),
                            Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.GskuId, operation.GskuId),
                            Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ExpectedRevisionVersion, operation.ExpectedRevisionVersion),
                            Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ExpectedGskuVersion, operation.ExpectedGskuVersion))))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null && IsExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, null, "FIRST_GSKU_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
        }
    }

    public Task<FirstGskuIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                             item => item.OperationId, operationId))
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<FirstGskuIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                             item => item.StartIdempotencyKey, startIdempotencyKey))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<FirstGskuIdentityWorkflowClaim?> TryClaimAsync(
        FirstGskuIdentityWorkflowClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.OperationFingerprint)
            || request.EligibleCheckpoints.Count == 0
            || string.IsNullOrWhiteSpace(request.LeaseOwner)
            || request.NowUtcTicks <= 0
            || request.LeaseUntilUtcTicks <= request.NowUtcTicks)
        {
            return null;
        }

        var filter = ActiveTenantFilter
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, request.OperationId)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, request.OperationFingerprint)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.In(
                         item => item.Checkpoint, request.EligibleCheckpoints)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, request.NowUtcTicks))
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, request.NowUtcTicks));
        var update = Builders<FirstGskuIdentityWorkflowOperation>.Update
            .Set(item => item.LeaseOwner, request.LeaseOwner)
            .Set(item => item.LeaseUntilUtcTicksV1, request.LeaseUntilUtcTicks)
            .Set(item => item.UpdatedAtUtcTicksV1, request.NowUtcTicks)
            .Set(item => item.UpdatedAt, new DateTimeOffset(request.NowUtcTicks, TimeSpan.Zero))
            .Inc(item => item.LeaseGeneration, 1)
            .Inc(item => item.Version, 1);
        var operation = await _operations.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<FirstGskuIdentityWorkflowOperation>
            {
                ReturnDocument = ReturnDocument.After,
                IsUpsert = false
            },
            cancellationToken);
        return operation is null
            ? null
            : new FirstGskuIdentityWorkflowClaim(
                _tenantId,
                operation.OperationId,
                operation.OperationFingerprint,
                request.LeaseOwner,
                operation.LeaseGeneration,
                operation.Checkpoint,
                request.LeaseUntilUtcTicks);
    }

    public async Task<FirstGskuIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        FirstGskuIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        if (nowUtcTicks <= 0 || limit is < 1 or > 100 || after?.OperationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var terminal = new[]
        {
            FirstGskuIdentityWorkflowCheckpoint.Completed,
            FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            FirstGskuIdentityWorkflowCheckpoint.Superseded
        };
        var filter = ActiveTenantFilter
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Nin(item => item.Checkpoint, terminal)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (after is not null)
        {
            filter &= after.NextAttemptAtUtcTicksV1.HasValue
                ? Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                    Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(
                        item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                    Builders<FirstGskuIdentityWorkflowOperation>.Filter.And(
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)))
                : Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                    Builders<FirstGskuIdentityWorkflowOperation>.Filter.Ne(
                        item => item.NextAttemptAtUtcTicksV1, null),
                    Builders<FirstGskuIdentityWorkflowOperation>.Filter.And(
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, null),
                        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)));
        }

        var rows = await _operations.Find(filter)
            .SortBy(item => item.NextAttemptAtUtcTicksV1)
            .ThenBy(item => item.OperationId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var next = rows.Count == limit
            ? new FirstGskuIdentityWorkflowRecoveryCursor(
                rows[^1].NextAttemptAtUtcTicksV1,
                rows[^1].OperationId)
            : null;
        return new(rows, next);
    }

    public async Task<bool> AdvanceAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != _tenantId
            || mutation.UpdatedAtUtcTicks <= 0
            || !IsAllowedTransition(claim.Checkpoint, mutation.NextCheckpoint)
            || !IsMutationCoherent(claim.Checkpoint, mutation))
        {
            return false;
        }

        var filter = ActiveTenantFilter
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, claim.OperationId)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, claim.OperationFingerprint)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.Checkpoint, claim.Checkpoint)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseOwner, claim.LeaseOwner)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseGeneration, claim.LeaseGeneration)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(
                         item => item.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        if (claim.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
            && mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            filter &= Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.RecoveryDisposition,
                          ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.In(
                          item => item.LastFailureCode,
                          new[]
                          {
                              "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED",
                              "REFERENCE_UNAUTHENTICATED",
                              "REFERENCE_FORBIDDEN"
                          })
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.DecisionKind,
                          ProductIdentityDecisionKind.Approved)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalPackApplicabilitySelection, null)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalPackUomSelection, null)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ReferencesValidatedAtUtcTicksV1, null)
                      & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalReferenceProofFingerprint, null);
        }
        var update = Builders<FirstGskuIdentityWorkflowOperation>.Update
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
        if (mutation.ApprovalPackApplicabilitySelection is not null) update = update.Set(item => item.ApprovalPackApplicabilitySelection, mutation.ApprovalPackApplicabilitySelection);
        if (mutation.ApprovalPackUomSelection is not null) update = update.Set(item => item.ApprovalPackUomSelection, mutation.ApprovalPackUomSelection);
        if (mutation.ReferencesValidatedAtUtcTicksV1.HasValue) update = update.Set(item => item.ReferencesValidatedAtUtcTicksV1, mutation.ReferencesValidatedAtUtcTicksV1);
        if (mutation.ApprovalReferenceProofFingerprint is not null) update = update.Set(item => item.ApprovalReferenceProofFingerprint, mutation.ApprovalReferenceProofFingerprint);
        if (mutation.ReleaseLease)
        {
            update = update.Set(item => item.LeaseOwner, null).Set(item => item.LeaseUntilUtcTicksV1, null);
        }

        var result = await _operations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private FilterDefinition<FirstGskuIdentityWorkflowOperation> ActiveTenantFilter =>
        Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.TenantId, _tenantId)
        & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false);

    private bool IsValidForReservation(FirstGskuIdentityWorkflowOperation operation) =>
        _tenantId != Guid.Empty
        && operation.OperationId != Guid.Empty
        && operation.ProductDefinitionRevisionId != Guid.Empty
        && operation.GskuId != Guid.Empty
        && operation.GlobalProductId != Guid.Empty
        && IsExactBounded(operation.CreationCommandId, 256)
        && operation.ExpectedRevisionVersion >= 0
        && operation.ExpectedGskuVersion >= 0
        && operation.PackQuantity > 0
        && IsExactBounded(operation.PackApplicabilityCode, 128)
        && IsExactBounded(operation.PackUomCode, 128)
        && IsValidSelection(operation.PackApplicabilitySelection, operation.PackApplicabilityCode)
        && IsValidSelection(operation.PackUomSelection, operation.PackUomCode)
        && operation.MakerSubjectId != Guid.Empty
        && (operation.WorkflowTemplateId.HasValue ^ operation.WorkflowTemplateCode is not null)
        && operation.CandidatePrincipalIds is { Count: >= 1 and <= 100 }
        && operation.CandidatePrincipalIds.All(item => item != Guid.Empty)
        && operation.CandidatePrincipalIds.Distinct().Count() == operation.CandidatePrincipalIds.Count
        && IsExactBounded(operation.ReasonCode, 128)
        && operation.ObjectType == "gsku"
        && operation.ObjectId == operation.GskuId.ToString("D")
        && IsExactBounded(operation.ObjectRef, 256)
        && IsExactBounded(operation.StartIdempotencyKey, 256)
        && IsExactBounded(operation.OperationFingerprint, 256)
        && (!operation.WorkflowTemplateId.HasValue || operation.WorkflowTemplateId != Guid.Empty)
        && (operation.WorkflowTemplateCode is null || IsExactBounded(operation.WorkflowTemplateCode, 128))
        && operation.TemporalStorageVersion is 0 or FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion;

    private static bool IsValidSelection(ReferenceCatalogSelection selection, string expectedCode) =>
        selection is not null
        && IsExactBounded(selection.SetCode, 128)
        && selection.ValueCode == expectedCode
        && selection.CatalogVersionId != Guid.Empty
        && selection.CatalogVersionNumber > 0
        && selection.ResolvedAtUtc.Offset == TimeSpan.Zero;

    private static bool IsExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 }
        && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsAllowedTransition(
        FirstGskuIdentityWorkflowCheckpoint current,
        FirstGskuIdentityWorkflowCheckpoint next) =>
        current == next
        || (current, next) switch
        {
            (FirstGskuIdentityWorkflowCheckpoint.Prepared, FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown) => true,
            (FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted
                or FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted,
                FirstGskuIdentityWorkflowCheckpoint.RevisionPendingApplied
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.RevisionPendingApplied,
                FirstGskuIdentityWorkflowCheckpoint.PairPendingApplied
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.PairPendingApplied,
                FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision,
                FirstGskuIdentityWorkflowCheckpoint.DecisionObserved
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
                FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated
                or FirstGskuIdentityWorkflowCheckpoint.GskuDraftRestored
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated,
                FirstGskuIdentityWorkflowCheckpoint.RevisionApproved
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.RevisionApproved,
                FirstGskuIdentityWorkflowCheckpoint.PairApproved
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.PairApproved,
                FirstGskuIdentityWorkflowCheckpoint.Completed
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.GskuDraftRestored,
                FirstGskuIdentityWorkflowCheckpoint.PairDraftRestored
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.PairDraftRestored,
                FirstGskuIdentityWorkflowCheckpoint.Completed
                or FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
                FirstGskuIdentityWorkflowCheckpoint.DecisionObserved) => true,
            _ => false
        };

    private static bool IsMutationCoherent(
        FirstGskuIdentityWorkflowCheckpoint current,
        FirstGskuIdentityWorkflowCheckpointMutation mutation)
    {
        var hasStart = mutation.WorkflowInstanceId.HasValue || mutation.WorkflowTemplateId.HasValue
            || mutation.WorkflowTemplateVersionId.HasValue || mutation.ApprovalTaskId.HasValue
            || mutation.AssignmentSnapshotId.HasValue || mutation.StartTransitionLogId.HasValue
            || mutation.WorkflowStartedAtUtcTicksV1.HasValue;
        var completeStart = mutation.WorkflowInstanceId is { } workflowId && workflowId != Guid.Empty
            && mutation.WorkflowTemplateId is { } templateId && templateId != Guid.Empty
            && mutation.WorkflowTemplateVersionId is { } templateVersionId && templateVersionId != Guid.Empty
            && mutation.ApprovalTaskId is { } taskId && taskId != Guid.Empty
            && mutation.AssignmentSnapshotId is { } snapshotId && snapshotId != Guid.Empty
            && mutation.StartTransitionLogId is { } logId && logId != Guid.Empty
            && mutation.WorkflowStartedAtUtcTicksV1 is > 0;
        var hasDecision = mutation.DecisionTransitionLogId.HasValue || mutation.DecisionKind.HasValue
            || mutation.DecisionObservedAtUtcTicksV1.HasValue || mutation.DecisionActorSubjectId.HasValue
            || mutation.DecisionReasonCode is not null || mutation.DecisionObjectType is not null
            || mutation.DecisionObjectId is not null || mutation.DecisionObjectRef is not null
            || mutation.DecisionWorkflowTemplateId.HasValue || mutation.DecisionWorkflowTemplateVersionId.HasValue
            || mutation.DecisionTaskStatus is not null || mutation.DecisionInstanceStatus is not null
            || mutation.DecisionTransitionSequence.HasValue || mutation.DecisionAtUtcTicksV1.HasValue;
        var completeDecision = mutation.DecisionKind is ProductIdentityDecisionKind.Approved or ProductIdentityDecisionKind.Rejected
            && mutation.DecisionObservedAtUtcTicksV1 is > 0
            && mutation.DecisionActorSubjectId is { } actor && actor != Guid.Empty
            && mutation.DecisionObjectType == "gsku"
            && Guid.TryParseExact(mutation.DecisionObjectId, "D", out var decisionObjectId) && decisionObjectId != Guid.Empty
            && !string.IsNullOrWhiteSpace(mutation.DecisionObjectRef)
            && mutation.DecisionWorkflowTemplateId is { } decisionTemplate && decisionTemplate != Guid.Empty
            && mutation.DecisionWorkflowTemplateVersionId is { } decisionVersion && decisionVersion != Guid.Empty
            && mutation.DecisionTransitionSequence is > 0 && mutation.DecisionAtUtcTicksV1 is > 0
            && (mutation.DecisionKind == ProductIdentityDecisionKind.Approved
                ? mutation.DecisionTaskStatus == "Approved" && mutation.DecisionInstanceStatus == "Completed"
                : mutation.DecisionTaskStatus == "Rejected" && mutation.DecisionInstanceStatus == "Rejected");
        var hasApproval = mutation.ApprovalPackApplicabilitySelection is not null
            || mutation.ApprovalPackUomSelection is not null
            || mutation.ReferencesValidatedAtUtcTicksV1.HasValue
            || mutation.ApprovalReferenceProofFingerprint is not null;
        var completeApproval = mutation.ApprovalPackApplicabilitySelection is not null
            && mutation.ApprovalPackUomSelection is not null
            && mutation.ReferencesValidatedAtUtcTicksV1 is > 0
            && mutation.ApprovalReferenceProofFingerprint is { Length: 64 };

        if (current == mutation.NextCheckpoint)
        {
            return mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                && mutation.NextAttemptAtUtcTicksV1 is > 0
                && !string.IsNullOrWhiteSpace(mutation.LastFailureCode)
                && mutation.ReleaseLease && !hasStart && !hasDecision && !hasApproval;
        }
        if (current == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
            && mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            return mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.None
                && mutation.NextAttemptAtUtcTicksV1 is null
                && mutation.LastFailureCode is null
                && mutation.ReleaseLease
                && !hasStart && !hasDecision && !hasApproval;
        }
        if (mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted)
        {
            return completeStart && !hasDecision && !hasApproval;
        }
        if (mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            return completeDecision && !hasStart && !hasApproval;
        }
        if (mutation.NextCheckpoint == FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated)
        {
            return completeApproval && !hasStart && !hasDecision;
        }
        return !hasStart && !hasDecision && !hasApproval;
    }

    private static bool IsExactReplay(
        FirstGskuIdentityWorkflowOperation existing,
        FirstGskuIdentityWorkflowOperation candidate) =>
        existing.OperationId == candidate.OperationId
        && existing.ProductDefinitionRevisionId == candidate.ProductDefinitionRevisionId
        && existing.GskuId == candidate.GskuId
        && existing.ExpectedRevisionVersion == candidate.ExpectedRevisionVersion
        && existing.ExpectedGskuVersion == candidate.ExpectedGskuVersion
        && existing.StartIdempotencyKey == candidate.StartIdempotencyKey
        && existing.OperationFingerprint == candidate.OperationFingerprint;

    private void EnsureIndexes()
    {
        var active = new BsonDocument(nameof(FirstGskuIdentityWorkflowOperation.IsDeleted), false);
        _operations.Indexes.CreateMany(
        [
            new CreateIndexModel<FirstGskuIdentityWorkflowOperation>(
                Builders<FirstGskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.OperationId),
                new CreateIndexOptions<FirstGskuIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_first_gsku_identity_workflow_operation" }),
            new CreateIndexModel<FirstGskuIdentityWorkflowOperation>(
                Builders<FirstGskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.StartIdempotencyKey),
                new CreateIndexOptions<FirstGskuIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_first_gsku_identity_workflow_start_key" }),
            new CreateIndexModel<FirstGskuIdentityWorkflowOperation>(
                Builders<FirstGskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.ProductDefinitionRevisionId)
                    .Ascending(item => item.GskuId).Ascending(item => item.ExpectedRevisionVersion)
                    .Ascending(item => item.ExpectedGskuVersion),
                new CreateIndexOptions<FirstGskuIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_first_gsku_identity_workflow_pair_version" }),
            new CreateIndexModel<FirstGskuIdentityWorkflowOperation>(
                Builders<FirstGskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.IsDeleted)
                    .Ascending(item => item.NextAttemptAtUtcTicksV1).Ascending(item => item.OperationId)
                    .Ascending(item => item.Checkpoint).Ascending(item => item.LeaseUntilUtcTicksV1)
                    .Ascending(item => item.CreatedAtUtcTicksV1),
                new CreateIndexOptions { Name = "ix_mdm_first_gsku_identity_workflow_recovery" })
        ]);
    }
}
