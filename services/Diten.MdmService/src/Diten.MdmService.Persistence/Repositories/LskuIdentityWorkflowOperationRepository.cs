using System.Globalization;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class LskuIdentityWorkflowOperationRepository
    : ILskuIdentityWorkflowOperationRepository
{
    public const string CollectionName = "mdm_lsku_identity_workflow_operations";
    private readonly IMongoCollection<LskuIdentityWorkflowOperation> _operations;
    private readonly IMongoCollection<Lsku> _lskus;
    private readonly IMongoDatabase _database;
    private readonly Guid _tenantId;

    public LskuIdentityWorkflowOperationRepository(
        IMongoDatabase database,
        ITenantContext tenantContext)
    {
        _database = database;
        _operations = database.GetCollection<LskuIdentityWorkflowOperation>(CollectionName);
        _lskus = database.GetCollection<Lsku>("mdm_lskus");
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<LskuIdentityWorkflowReserveResult> ReserveAsync(
        LskuIdentityWorkflowOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsValidForReservation(operation))
        {
            return new(false, false, null, "LSKU_IDENTITY_WORKFLOW_OPERATION_INVALID");
        }

        operation.TenantId = _tenantId;
        operation.Id = operation.Id == Guid.Empty ? Guid.NewGuid() : operation.Id;
        operation.IsDeleted = false;
        operation.DeletedAt = null;
        operation.Checkpoint = LskuIdentityWorkflowCheckpoint.Prepared;
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
        operation.ApprovalMarketSelection = null;
        operation.MarketValidatedAtUtcTicksV1 = null;
        operation.ApprovalMarketProofFingerprint = null;
        operation.WithdrawalCommandId = null;
        operation.WithdrawalFingerprint = null;
        operation.WithdrawalRequesterSubjectId = null;
        operation.WithdrawalExpectedLskuVersion = null;
        operation.WithdrawalReasonCode = null;
        operation.WithdrawalComment = null;
        operation.WithdrawalExpectedWorkflowInstanceVersion = null;
        operation.WithdrawalExpectedApprovalTaskVersion = null;
        operation.WithdrawalTransitionLogId = null;
        operation.WithdrawalObservedAtUtcTicksV1 = null;
        operation.WithdrawalTransitionSequence = null;
        operation.WithdrawalResultWorkflowInstanceVersion = null;
        operation.WithdrawalResultApprovalTaskVersion = null;
        operation.WithdrawalTaskStatus = null;
        operation.WithdrawalInstanceStatus = null;
        operation.WithdrawalObjectRef = null;
        operation.NextAttemptAtUtcTicksV1 = null;
        operation.LastFailureCode = null;
        operation.LeaseOwner = null;
        operation.LeaseUntilUtcTicksV1 = null;
        operation.LeaseGeneration = 0;
        operation.TemporalStorageVersion = LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion;
        operation.Version = 0;
        operation.CreatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1 > 0
            ? operation.CreatedAtUtcTicksV1
            : DateTimeOffset.UtcNow.UtcTicks;
        operation.UpdatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1;
        operation.CreatedAt = new DateTimeOffset(operation.CreatedAtUtcTicksV1, TimeSpan.Zero);
        operation.UpdatedAt = operation.CreatedAt;

        if (operation.ToBson().Length > 1024 * 1024 - 4096)
        {
            return new(false, false, null, "LSKU_IDENTITY_WORKFLOW_OPERATION_TOO_LARGE");
        }

        try
        {
            await _operations.InsertOneAsync(operation, cancellationToken: cancellationToken);
            return new(true, false, operation, null);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _operations.Find(ActiveTenantFilter & Builders<LskuIdentityWorkflowOperation>
                    .Filter.Or(
                        Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.OperationId, operation.OperationId),
                        Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                            item => item.StartIdempotencyKey, operation.StartIdempotencyKey),
                        Builders<LskuIdentityWorkflowOperation>.Filter.And(
                            Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.LskuId, operation.LskuId),
                            Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ExpectedLskuVersion, operation.ExpectedLskuVersion))))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null && IsExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, null, "LSKU_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
        }
    }

    public Task<LskuIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                             item => item.OperationId, operationId))
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<LskuIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                             item => item.StartIdempotencyKey, startIdempotencyKey))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<LskuIdentityWorkflowClaim?> TryClaimAsync(
        LskuIdentityWorkflowClaimRequest request,
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
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, request.OperationId)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, request.OperationFingerprint)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.In(
                         item => item.Checkpoint, request.EligibleCheckpoints)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<LskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, request.NowUtcTicks))
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<LskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, request.NowUtcTicks));
        var update = Builders<LskuIdentityWorkflowOperation>.Update
            .Set(item => item.LeaseOwner, request.LeaseOwner)
            .Set(item => item.LeaseUntilUtcTicksV1, request.LeaseUntilUtcTicks)
            .Set(item => item.UpdatedAtUtcTicksV1, request.NowUtcTicks)
            .Set(item => item.UpdatedAt, new DateTimeOffset(request.NowUtcTicks, TimeSpan.Zero))
            .Inc(item => item.LeaseGeneration, 1)
            .Inc(item => item.Version, 1);
        var operation = await _operations.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<LskuIdentityWorkflowOperation>
            {
                ReturnDocument = ReturnDocument.After,
                IsUpsert = false
            },
            cancellationToken);
        return operation is null
            ? null
            : new LskuIdentityWorkflowClaim(
                _tenantId,
                operation.OperationId,
                operation.LskuId,
                operation.MarketCode,
                operation.DecisionTransitionLogId,
                operation.DecisionTransitionSequence,
                operation.OperationFingerprint,
                request.LeaseOwner,
                operation.LeaseGeneration,
                operation.Checkpoint,
                request.LeaseUntilUtcTicks);
    }

    public async Task<LskuIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        LskuIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        if (nowUtcTicks <= 0 || limit is < 1 or > 100 || after?.OperationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var terminal = new[]
        {
            LskuIdentityWorkflowCheckpoint.Completed,
            LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            LskuIdentityWorkflowCheckpoint.Superseded
        };
        var filter = ActiveTenantFilter
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Nin(item => item.Checkpoint, terminal)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<LskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<LskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (after is not null)
        {
            filter &= after.NextAttemptAtUtcTicksV1.HasValue
                ? Builders<LskuIdentityWorkflowOperation>.Filter.Or(
                    Builders<LskuIdentityWorkflowOperation>.Filter.Gt(
                        item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                    Builders<LskuIdentityWorkflowOperation>.Filter.And(
                        Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                        Builders<LskuIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)))
                : Builders<LskuIdentityWorkflowOperation>.Filter.Or(
                    Builders<LskuIdentityWorkflowOperation>.Filter.Ne(
                        item => item.NextAttemptAtUtcTicksV1, null),
                    Builders<LskuIdentityWorkflowOperation>.Filter.And(
                        Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, null),
                        Builders<LskuIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)));
        }

        var rows = await _operations.Find(filter)
            .SortBy(item => item.NextAttemptAtUtcTicksV1)
            .ThenBy(item => item.OperationId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var next = rows.Count == limit
            ? new LskuIdentityWorkflowRecoveryCursor(
                rows[^1].NextAttemptAtUtcTicksV1,
                rows[^1].OperationId)
            : null;
        return new(rows, next);
    }

    public async Task<bool> AdvanceAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowCheckpointMutation mutation,
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
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, claim.OperationId)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, claim.OperationFingerprint)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.Checkpoint, claim.Checkpoint)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseOwner, claim.LeaseOwner)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseGeneration, claim.LeaseGeneration)
                     & Builders<LskuIdentityWorkflowOperation>.Filter.Gt(
                         item => item.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        if (mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.ApprovalValidated)
        {
            filter &= Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalMarketSelection, null)
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.MarketValidatedAtUtcTicksV1, null)
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalMarketProofFingerprint, null)
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.MarketCode, mutation.ApprovalMarketSelection!.ValueCode)
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.DecisionKind, ProductIdentityDecisionKind.Approved);
        }
        if (mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.DecisionApplied)
        {
            filter &= claim.Checkpoint == LskuIdentityWorkflowCheckpoint.DecisionObserved
                ? Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                    item => item.DecisionKind, ProductIdentityDecisionKind.Rejected)
                : Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                    item => item.DecisionKind, ProductIdentityDecisionKind.Approved);
        }
        if (claim.Checkpoint != LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
            && mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            filter &= Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ObjectType, mutation.DecisionObjectType)
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ObjectId, mutation.DecisionObjectId);
        }

        if (claim.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
            && mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            filter &= Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.RecoveryDisposition,
                          ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.LastFailureCode,
                          "REFERENCE_CONTRACT_MISMATCH")
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.DecisionKind,
                          ProductIdentityDecisionKind.Approved)
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalMarketSelection,
                          null)
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.MarketValidatedAtUtcTicksV1,
                          null)
                      & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(
                          item => item.ApprovalMarketProofFingerprint,
                          null);
        }
        var update = Builders<LskuIdentityWorkflowOperation>.Update
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
        if (mutation.ApprovalMarketSelection is not null)
            update = update.Set(item => item.ApprovalMarketSelection, mutation.ApprovalMarketSelection);
        if (mutation.MarketValidatedAtUtcTicksV1.HasValue)
            update = update.Set(item => item.MarketValidatedAtUtcTicksV1, mutation.MarketValidatedAtUtcTicksV1);
        if (mutation.ApprovalMarketProofFingerprint is not null)
            update = update.Set(item => item.ApprovalMarketProofFingerprint, mutation.ApprovalMarketProofFingerprint);
        if (mutation.WithdrawalCommandId.HasValue) update = update.Set(item => item.WithdrawalCommandId, mutation.WithdrawalCommandId);
        if (mutation.WithdrawalFingerprint is not null) update = update.Set(item => item.WithdrawalFingerprint, mutation.WithdrawalFingerprint);
        if (mutation.WithdrawalRequesterSubjectId.HasValue) update = update.Set(item => item.WithdrawalRequesterSubjectId, mutation.WithdrawalRequesterSubjectId);
        if (mutation.WithdrawalExpectedLskuVersion.HasValue) update = update.Set(item => item.WithdrawalExpectedLskuVersion, mutation.WithdrawalExpectedLskuVersion);
        if (mutation.WithdrawalReasonCode is not null) update = update.Set(item => item.WithdrawalReasonCode, mutation.WithdrawalReasonCode);
        if (mutation.WithdrawalComment is not null) update = update.Set(item => item.WithdrawalComment, mutation.WithdrawalComment);
        if (mutation.WithdrawalExpectedWorkflowInstanceVersion.HasValue) update = update.Set(item => item.WithdrawalExpectedWorkflowInstanceVersion, mutation.WithdrawalExpectedWorkflowInstanceVersion);
        if (mutation.WithdrawalExpectedApprovalTaskVersion.HasValue) update = update.Set(item => item.WithdrawalExpectedApprovalTaskVersion, mutation.WithdrawalExpectedApprovalTaskVersion);
        if (mutation.WithdrawalTransitionLogId.HasValue) update = update.Set(item => item.WithdrawalTransitionLogId, mutation.WithdrawalTransitionLogId);
        if (mutation.WithdrawalObservedAtUtcTicksV1.HasValue) update = update.Set(item => item.WithdrawalObservedAtUtcTicksV1, mutation.WithdrawalObservedAtUtcTicksV1);
        if (mutation.WithdrawalTransitionSequence.HasValue) update = update.Set(item => item.WithdrawalTransitionSequence, mutation.WithdrawalTransitionSequence);
        if (mutation.WithdrawalResultWorkflowInstanceVersion.HasValue) update = update.Set(item => item.WithdrawalResultWorkflowInstanceVersion, mutation.WithdrawalResultWorkflowInstanceVersion);
        if (mutation.WithdrawalResultApprovalTaskVersion.HasValue) update = update.Set(item => item.WithdrawalResultApprovalTaskVersion, mutation.WithdrawalResultApprovalTaskVersion);
        if (mutation.WithdrawalTaskStatus is not null) update = update.Set(item => item.WithdrawalTaskStatus, mutation.WithdrawalTaskStatus);
        if (mutation.WithdrawalInstanceStatus is not null) update = update.Set(item => item.WithdrawalInstanceStatus, mutation.WithdrawalInstanceStatus);
        if (mutation.WithdrawalObjectRef is not null) update = update.Set(item => item.WithdrawalObjectRef, mutation.WithdrawalObjectRef);
        if (mutation.ReleaseLease)
        {
            update = update.Set(item => item.LeaseOwner, null).Set(item => item.LeaseUntilUtcTicksV1, null);
        }

        var result = await _operations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<LskuIdentityWithdrawalWriteResult> ApplyWithdrawalAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowOperation operation,
        ProductIdentityWorkflowCancellationEvidence cancellationEvidence,
        LocalAuditIntent auditIntent,
        long updatedAtUtcTicks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(cancellationEvidence);
        ArgumentNullException.ThrowIfNull(auditIntent);
        if (claim.TenantId != _tenantId || claim.OperationId != operation.OperationId
            || claim.OperationFingerprint != operation.OperationFingerprint
            || claim.Checkpoint != LskuIdentityWorkflowCheckpoint.WithdrawalObserved
            || updatedAtUtcTicks <= 0 || operation.WithdrawalExpectedLskuVersion is not > 0
            || operation.WithdrawalCommandId is not { } commandId || commandId == Guid.Empty
            || cancellationEvidence.RequesterSubjectId != operation.MakerSubjectId
            || cancellationEvidence.WorkflowInstanceId != operation.WorkflowInstanceId
            || cancellationEvidence.ApprovalTaskId != operation.ApprovalTaskId
            || cancellationEvidence.IdempotencyKey != commandId.ToString("D")
            || auditIntent.Operation != ProductAuditOperation.LskuIdentityApprovalWithdrawn
            || auditIntent.TenantId != _tenantId || auditIntent.AggregateType != AuditAggregateType.Lsku
            || auditIntent.AggregateId != operation.LskuId
            || auditIntent.PreVersion != operation.WithdrawalExpectedLskuVersion
            || auditIntent.PostVersion != operation.WithdrawalExpectedLskuVersion + 1
            || auditIntent.ActorId != operation.MakerSubjectId.ToString("D")
            || auditIntent.IdempotencyKey != commandId.ToString("D"))
            return new(false, false, null, "LSKU_IDENTITY_WITHDRAWAL_CONTRACT_INVALID");

        var active = Builders<Lsku>.Filter.Eq(x => x.TenantId, _tenantId)
                     & Builders<Lsku>.Filter.Eq(x => x.IsDeleted, false)
                     & Builders<Lsku>.Filter.Eq(x => x.Id, operation.LskuId);
        var current = await _lskus.Find(active).FirstOrDefaultAsync(cancellationToken);
        if (ExactWithdrawalReplay(current, operation, commandId, auditIntent))
            return new(true, true, current, null);

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
        try
        {
            var result = await _lskus.UpdateOneAsync(session,
                active
                & Builders<Lsku>.Filter.Eq(x => x.Version, operation.WithdrawalExpectedLskuVersion.Value)
                & Builders<Lsku>.Filter.Eq(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.PendingIdentityApproval)
                & Builders<Lsku>.Filter.Eq(x => x.IdentityWorkflowBinding!.WorkflowInstanceId, operation.WorkflowInstanceId)
                & Builders<Lsku>.Filter.Eq(x => x.IdentityWorkflowBinding!.ApprovalTaskId, operation.ApprovalTaskId)
                & Builders<Lsku>.Filter.Eq(x => x.IdentityWorkflowBinding!.SubmitterSubjectId, operation.MakerSubjectId)
                & Builders<Lsku>.Filter.Where(x => x.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate),
                Builders<Lsku>.Update
                    .Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Draft)
                    .Set(x => x.IdentityWorkflowBinding!.CancellationEvidence, cancellationEvidence)
                    .Set(x => x.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(x => x.Version, 1)
                    .Push(x => x.AuditIntents, auditIntent), cancellationToken: cancellationToken);
            if (result.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(false, false, current, "LSKU_IDENTITY_WITHDRAWAL_STATE_CONFLICT");
            }
            var operationFilter = ActiveTenantFilter
                & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(x => x.OperationId, claim.OperationId)
                & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(x => x.OperationFingerprint, claim.OperationFingerprint)
                & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(x => x.Checkpoint, claim.Checkpoint)
                & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(x => x.LeaseOwner, claim.LeaseOwner)
                & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(x => x.LeaseGeneration, claim.LeaseGeneration)
                & Builders<LskuIdentityWorkflowOperation>.Filter.Gt(x => x.LeaseUntilUtcTicksV1, updatedAtUtcTicks);
            var operationResult = await _operations.UpdateOneAsync(session, operationFilter,
                Builders<LskuIdentityWorkflowOperation>.Update
                    .Set(x => x.Checkpoint, LskuIdentityWorkflowCheckpoint.WithdrawalApplied)
                    .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.None)
                    .Set(x => x.NextAttemptAtUtcTicksV1, null).Set(x => x.LastFailureCode, null)
                    .Set(x => x.LeaseOwner, null).Set(x => x.LeaseUntilUtcTicksV1, null)
                    .Set(x => x.UpdatedAtUtcTicksV1, updatedAtUtcTicks)
                    .Set(x => x.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(x => x.Version, 1), cancellationToken: cancellationToken);
            if (operationResult.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(false, false, current, "LSKU_IDENTITY_WITHDRAWAL_CONCURRENCY_CONFLICT");
            }
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch (OperationCanceledException) { if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None); throw; }
        catch (MongoException)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            return new(false, false, current, "LSKU_IDENTITY_WITHDRAWAL_PERSISTENCE_UNAVAILABLE");
        }
        current = await _lskus.Find(active).FirstOrDefaultAsync(cancellationToken);
        return ExactWithdrawalReplay(current, operation, commandId, auditIntent)
            ? new(true, false, current, null)
            : new(false, false, current, "LSKU_IDENTITY_WITHDRAWAL_LOCAL_STATE_INCONSISTENT");
    }

    private bool ExactWithdrawalReplay(Lsku? lsku, LskuIdentityWorkflowOperation operation,
        Guid commandId, LocalAuditIntent audit) =>
        lsku is not null && lsku.TenantId == _tenantId
        && lsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
        && lsku.Version == operation.WithdrawalExpectedLskuVersion + 1
        && lsku.IdentityWorkflowBinding?.CancellationEvidence?.IdempotencyKey == commandId.ToString("D")
        && lsku.AuditIntents.Count(x => x.Operation == ProductAuditOperation.LskuIdentityApprovalWithdrawn
            && x.IdempotencyKey == commandId.ToString("D") && x.EvidenceHash == audit.EvidenceHash) == 1;

    private FilterDefinition<LskuIdentityWorkflowOperation> ActiveTenantFilter =>
        Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.TenantId, _tenantId)
        & Builders<LskuIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false);

    private bool IsValidForReservation(LskuIdentityWorkflowOperation operation) =>
        _tenantId != Guid.Empty
        && operation.OperationId != Guid.Empty
        && operation.LskuId != Guid.Empty
        && operation.GskuId != Guid.Empty
        && operation.ProductDefinitionRevisionId != Guid.Empty
        && operation.ExpectedLskuVersion >= 0
        && operation.MakerSubjectId != Guid.Empty
        && (operation.WorkflowTemplateId.HasValue ^ operation.WorkflowTemplateCode is not null)
        && operation.CandidatePrincipalIds is { Count: >= 1 and <= 100 }
        && operation.CandidatePrincipalIds.All(item => item != Guid.Empty)
        && operation.CandidatePrincipalIds.Distinct().Count() == operation.CandidatePrincipalIds.Count
        && IsExactBounded(operation.ReasonCode, 128)
        && operation.ObjectType == "lsku"
        && operation.ObjectId == operation.LskuId.ToString("D")
        && operation.MarketCode is { Length: 2 }
        && operation.MarketCode.All(character => character is >= 'A' and <= 'Z')
        && operation.MarketSelection.SetCode == "market"
        && operation.MarketSelection.ValueCode == operation.MarketCode
        && operation.MarketSelection.CatalogVersionId != Guid.Empty
        && operation.MarketSelection.CatalogVersionNumber > 0
        && operation.MarketSelection.ResolutionMode == ReferenceCatalogResolutionMode.Latest
        && operation.MarketSelection.ResolvedAtUtc != default
        && operation.MarketSelection.ResolvedAtUtc.Offset == TimeSpan.Zero
        && IsExactBounded(operation.ObjectRef, 256)
        && IsExactBounded(operation.StartIdempotencyKey, 256)
        && IsLowerHex(operation.OperationFingerprint, 64)
        && (!operation.WorkflowTemplateId.HasValue || operation.WorkflowTemplateId != Guid.Empty)
        && (operation.WorkflowTemplateCode is null || IsExactBounded(operation.WorkflowTemplateCode, 128))
        && operation.TemporalStorageVersion is 0 or LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion;

    private static bool IsExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 }
        && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsAllowedTransition(
        LskuIdentityWorkflowCheckpoint current,
        LskuIdentityWorkflowCheckpoint next) =>
        current == next
        || (current, next) switch
        {
            (LskuIdentityWorkflowCheckpoint.Prepared,
                LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown) => true,
            (LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                LskuIdentityWorkflowCheckpoint.WorkflowStarted
                or LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                LskuIdentityWorkflowCheckpoint.WorkflowStarted
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.WorkflowStarted,
                LskuIdentityWorkflowCheckpoint.LocalPendingApplied
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.LocalPendingApplied,
                LskuIdentityWorkflowCheckpoint.AwaitingDecision
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.AwaitingDecision,
                LskuIdentityWorkflowCheckpoint.DecisionObserved
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.DecisionObserved,
                LskuIdentityWorkflowCheckpoint.ApprovalValidated
                or LskuIdentityWorkflowCheckpoint.DecisionApplied
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.ApprovalValidated,
                LskuIdentityWorkflowCheckpoint.DecisionApplied
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.DecisionApplied,
                LskuIdentityWorkflowCheckpoint.Completed
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
                LskuIdentityWorkflowCheckpoint.DecisionObserved) => true,
            (LskuIdentityWorkflowCheckpoint.AwaitingDecision,
                LskuIdentityWorkflowCheckpoint.WithdrawalRequested) => true,
            (LskuIdentityWorkflowCheckpoint.WithdrawalRequested,
                LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
                or LskuIdentityWorkflowCheckpoint.AwaitingDecision
                or LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved,
                LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
                or LskuIdentityWorkflowCheckpoint.WithdrawalObserved
                or LskuIdentityWorkflowCheckpoint.AwaitingDecision
                or LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                LskuIdentityWorkflowCheckpoint.WithdrawalObserved
                or LskuIdentityWorkflowCheckpoint.AwaitingDecision
                or LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                LskuIdentityWorkflowCheckpoint.WithdrawalRequested
                or LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
                or LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown) => true,
            (LskuIdentityWorkflowCheckpoint.WithdrawalObserved,
                LskuIdentityWorkflowCheckpoint.WithdrawalApplied
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (LskuIdentityWorkflowCheckpoint.WithdrawalApplied,
                LskuIdentityWorkflowCheckpoint.Completed
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            _ => false
        };

    private static bool IsExactReplay(
        LskuIdentityWorkflowOperation existing,
        LskuIdentityWorkflowOperation candidate) =>
        existing.OperationId == candidate.OperationId
        && existing.LskuId == candidate.LskuId
        && existing.GskuId == candidate.GskuId
        && existing.ProductDefinitionRevisionId == candidate.ProductDefinitionRevisionId
        && existing.ExpectedLskuVersion == candidate.ExpectedLskuVersion
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
        && existing.MarketCode == candidate.MarketCode
        && SameSelection(existing.MarketSelection, candidate.MarketSelection)
        && existing.StartIdempotencyKey == candidate.StartIdempotencyKey
        && existing.OperationFingerprint == candidate.OperationFingerprint;

    private static bool IsCoherentMutation(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowCheckpointMutation mutation)
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
            && mutation.DecisionObjectType == "lsku"
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
        var hasAnyApprovalProof = mutation.ApprovalMarketSelection is not null
            || mutation.MarketValidatedAtUtcTicksV1.HasValue
            || mutation.ApprovalMarketProofFingerprint is not null;
        var hasWithdrawalRequest = mutation.WithdrawalCommandId.HasValue
            || mutation.WithdrawalFingerprint is not null || mutation.WithdrawalRequesterSubjectId.HasValue
            || mutation.WithdrawalExpectedLskuVersion.HasValue || mutation.WithdrawalReasonCode is not null
            || mutation.WithdrawalComment is not null;
        var completeWithdrawalRequest = mutation.WithdrawalCommandId is { } withdrawalCommandId
            && withdrawalCommandId != Guid.Empty
            && mutation.WithdrawalRequesterSubjectId is { } withdrawalRequester && withdrawalRequester != Guid.Empty
            && mutation.WithdrawalExpectedLskuVersion is > 0
            && IsLowerHex(mutation.WithdrawalFingerprint, 64)
            && IsExactBounded(mutation.WithdrawalReasonCode, 128)
            && IsOptionalExactBounded(mutation.WithdrawalComment, 2000);
        var hasWithdrawalPreflight = mutation.WithdrawalExpectedWorkflowInstanceVersion.HasValue
            || mutation.WithdrawalExpectedApprovalTaskVersion.HasValue || mutation.WithdrawalObjectRef is not null;
        var completeWithdrawalPreflight = mutation.WithdrawalExpectedWorkflowInstanceVersion is > 0
            && mutation.WithdrawalExpectedApprovalTaskVersion is > 0
            && IsExactBounded(mutation.WithdrawalObjectRef, 256);
        var hasWithdrawalEvidence = mutation.WithdrawalTransitionLogId.HasValue
            || mutation.WithdrawalObservedAtUtcTicksV1.HasValue || mutation.WithdrawalTransitionSequence.HasValue
            || mutation.WithdrawalResultWorkflowInstanceVersion.HasValue
            || mutation.WithdrawalResultApprovalTaskVersion.HasValue
            || mutation.WithdrawalTaskStatus is not null || mutation.WithdrawalInstanceStatus is not null;
        var completeWithdrawalEvidence = mutation.WithdrawalTransitionLogId is { } withdrawalLogId
            && withdrawalLogId != Guid.Empty && mutation.WithdrawalObservedAtUtcTicksV1 is > 0
            && mutation.WithdrawalTransitionSequence is > 0
            && mutation.WithdrawalResultWorkflowInstanceVersion is > 0
            && mutation.WithdrawalResultApprovalTaskVersion is > 0
            && mutation.WithdrawalTaskStatus == "Cancelled" && mutation.WithdrawalInstanceStatus == "Cancelled";

        if (mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.WithdrawalRequested)
            return completeWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence
                && !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof;
        if (mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved)
            return completeWithdrawalPreflight && !hasWithdrawalRequest && !hasWithdrawalEvidence
                && !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof;
        if (mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.WithdrawalObserved)
            return completeWithdrawalEvidence && !hasWithdrawalRequest
                && !mutation.WithdrawalExpectedWorkflowInstanceVersion.HasValue
                && !mutation.WithdrawalExpectedApprovalTaskVersion.HasValue
                && IsExactBounded(mutation.WithdrawalObjectRef, 256)
                && !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof;
        if (mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
            && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay)
            return !hasWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence
                && !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof
                && IsExactBounded(mutation.LastFailureCode, 128) && mutation.ReleaseLease;

        if (mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.WorkflowStarted)
        {
            return hasCompleteStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof
                && !hasWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence;
        }
        if (current == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
            && mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            return mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.None
                && mutation.NextAttemptAtUtcTicksV1 is null
                && mutation.LastFailureCode is null
                && mutation.ReleaseLease
                && !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof;
        }
        if (mutation.NextCheckpoint == LskuIdentityWorkflowCheckpoint.DecisionObserved)
        {
            return !hasAnyStartProof && hasCompleteDecisionProof && !hasAnyApprovalProof;
        }
        if (mutation.NextCheckpoint != LskuIdentityWorkflowCheckpoint.ApprovalValidated)
        {
            if (mutation.NextCheckpoint == current)
            {
            return !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof
                && !hasWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence
                && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                    && mutation.NextAttemptAtUtcTicksV1 > mutation.UpdatedAtUtcTicks
                    && IsExactBounded(mutation.LastFailureCode, 128)
                    && mutation.ReleaseLease;
            }
            return !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof
                && !hasWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence
                && IsOptionalExactBounded(mutation.LastFailureCode, 128)
                && !mutation.NextAttemptAtUtcTicksV1.HasValue;
        }

        var selection = mutation.ApprovalMarketSelection;
        return current == LskuIdentityWorkflowCheckpoint.DecisionObserved
            && !hasAnyStartProof
            && !hasAnyDecisionProof
            && selection is not null
            && selection.SetCode == "market"
            && selection.ValueCode is { Length: 2 }
            && selection.ValueCode.All(character => character is >= 'A' and <= 'Z')
            && selection.CatalogVersionId != Guid.Empty
            && selection.CatalogVersionNumber > 0
            && selection.ResolutionMode == ReferenceCatalogResolutionMode.Latest
            && selection.ResolvedAtUtc != default
            && selection.ResolvedAtUtc.Offset == TimeSpan.Zero
            && mutation.MarketValidatedAtUtcTicksV1 is > 0
            && mutation.ApprovalMarketProofFingerprint is { Length: 64 } fingerprint
            && string.Equals(
                fingerprint,
                ApprovalProofFingerprint(
                    claim.TenantId,
                    claim.OperationId,
                    claim.LskuId,
                    claim.MarketCode,
                    claim.DecisionTransitionSequence,
                    selection,
                    mutation.MarketValidatedAtUtcTicksV1.Value),
                StringComparison.Ordinal);
    }

    private static string ApprovalProofFingerprint(
        Guid tenantId,
        Guid operationId,
        Guid lskuId,
        string marketCode,
        long? decisionTransitionSequence,
        Diten.MdmService.Domain.ValueObjects.ReferenceCatalogSelection selection,
        long validatedAtUtcTicks)
    {
        var facts = string.Join('|',
            "lsku-approval-market-v1",
            tenantId.ToString("D"),
            operationId.ToString("D"),
            lskuId.ToString("D"),
            marketCode,
            decisionTransitionSequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            selection.SetCode,
            selection.ValueCode,
            selection.CatalogVersionId.ToString("D"),
            selection.CatalogVersionNumber.ToString(CultureInfo.InvariantCulture),
            ((int)selection.ResolutionMode).ToString(CultureInfo.InvariantCulture),
            selection.ResolvedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
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

    private static bool SameSelection(
        Diten.MdmService.Domain.ValueObjects.ReferenceCatalogSelection left,
        Diten.MdmService.Domain.ValueObjects.ReferenceCatalogSelection right) =>
        left.SetCode == right.SetCode
        && left.ValueCode == right.ValueCode
        && left.CatalogVersionId == right.CatalogVersionId
        && left.CatalogVersionNumber == right.CatalogVersionNumber
        && left.ResolutionMode == right.ResolutionMode
        && left.ResolvedAtUtc == right.ResolvedAtUtc;

    private void EnsureIndexes()
    {
        var active = new BsonDocument(nameof(LskuIdentityWorkflowOperation.IsDeleted), false);
        _operations.Indexes.CreateMany(
        [
            new CreateIndexModel<LskuIdentityWorkflowOperation>(
                Builders<LskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.OperationId),
                new CreateIndexOptions<LskuIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_lsku_identity_workflow_operation" }),
            new CreateIndexModel<LskuIdentityWorkflowOperation>(
                Builders<LskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.StartIdempotencyKey),
                new CreateIndexOptions<LskuIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_lsku_identity_workflow_start_key" }),
            new CreateIndexModel<LskuIdentityWorkflowOperation>(
                Builders<LskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.LskuId)
                    .Ascending(item => item.ExpectedLskuVersion),
                new CreateIndexOptions<LskuIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_lsku_identity_workflow_lsku_version" }),
            new CreateIndexModel<LskuIdentityWorkflowOperation>(
                Builders<LskuIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.IsDeleted)
                    .Ascending(item => item.NextAttemptAtUtcTicksV1).Ascending(item => item.OperationId)
                    .Ascending(item => item.Checkpoint).Ascending(item => item.LeaseUntilUtcTicksV1)
                    .Ascending(item => item.CreatedAtUtcTicksV1),
                new CreateIndexOptions { Name = "ix_mdm_lsku_identity_workflow_recovery" })
        ]);
    }
}
