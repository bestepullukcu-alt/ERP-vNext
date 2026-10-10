using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

// MOD-0290-FU04 S2: mdm_item_workflow_operations — the LSKU identity operation store's rules (claim, lease, checkpoint
// table, coherent mutations, "manual + retryable" refused), the item's writes in place of the LSKU's. Tenant-bounded;
// "live" = IsDeleted ≠ true (the item store's rule: a legacy document without the field is live).
public sealed class ItemWorkflowOperationRepository : IItemWorkflowOperationRepository
{
    public const string CollectionName = "mdm_item_workflow_operations";
    private readonly IMongoCollection<ItemWorkflowOperation> _operations;
    private readonly IMongoCollection<Item> _items;
    private readonly IMongoDatabase _database;
    private readonly Guid _tenantId;

    private static FilterDefinitionBuilder<ItemWorkflowOperation> Op => Builders<ItemWorkflowOperation>.Filter;

    public ItemWorkflowOperationRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _database = database;
        _operations = database.GetCollection<ItemWorkflowOperation>(CollectionName);
        _items = database.GetCollection<Item>(ItemRepository.CollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    private FilterDefinition<ItemWorkflowOperation> ActiveTenantFilter =>
        Op.Eq(x => x.TenantId, _tenantId) & Op.Ne(x => x.IsDeleted, true);

    private FilterDefinition<Item> LiveItem(Guid itemId) =>
        Builders<Item>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<Item>.Filter.Ne(x => x.IsDeleted, true)
        & Builders<Item>.Filter.Eq(x => x.Id, itemId);

    public async Task<ItemWorkflowReserveResult> ReserveAsync(ItemWorkflowOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsValidForReservation(operation)) return new(false, false, null, "ITEM_ACTIVATION_WORKFLOW_OPERATION_INVALID");
        operation.TenantId = _tenantId;
        operation.Id = operation.Id == Guid.Empty ? Guid.NewGuid() : operation.Id;
        operation.IsDeleted = false;
        operation.DeletedAt = null;
        operation.Checkpoint = ItemWorkflowCheckpoint.Prepared;
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
        operation.WorkflowInstanceId = null;
        operation.WorkflowTemplateVersionId = null;
        operation.ApprovalTaskId = null;
        operation.AssignmentSnapshotId = null;
        operation.StartTransitionLogId = null;
        operation.WorkflowStartedAtUtcTicksV1 = null;
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
        operation.ApprovalGateValidatedAtUtcTicksV1 = null;
        operation.WithdrawalCommandId = null;
        operation.WithdrawalFingerprint = null;
        operation.WithdrawalRequesterSubjectId = null;
        operation.WithdrawalExpectedItemVersion = null;
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
        operation.FailedAttemptCount = 0;
        operation.TemporalStorageVersion = ItemWorkflowOperation.CurrentTemporalStorageVersion;
        operation.Version = 0;
        operation.CreatedAtUtcTicksV1 = operation.CreatedAtUtcTicksV1 > 0 ? operation.CreatedAtUtcTicksV1 : DateTimeOffset.UtcNow.UtcTicks;
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
            var existing = await _operations.Find(ActiveTenantFilter & Op.Or(
                    Op.Eq(x => x.OperationId, operation.OperationId),
                    Op.Eq(x => x.StartIdempotencyKey, operation.StartIdempotencyKey),
                    Op.And(Op.Eq(x => x.ItemId, operation.ItemId),
                        Op.Eq(x => x.ExpectedItemVersion, operation.ExpectedItemVersion),
                        Op.Eq(x => x.ActivationKind, operation.ActivationKind))))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null && IsExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, null, "ITEM_ACTIVATION_WORKFLOW_IDEMPOTENCY_CONFLICT");
        }
    }

    public Task<ItemWorkflowOperation?> GetByOperationIdAsync(Guid operationId, CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter & Op.Eq(x => x.OperationId, operationId)).FirstOrDefaultAsync(cancellationToken)!;

    public Task<ItemWorkflowOperation?> GetByStartIdempotencyKeyAsync(string startIdempotencyKey,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter & Op.Eq(x => x.StartIdempotencyKey, startIdempotencyKey))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<ItemWorkflowClaim?> TryClaimAsync(ItemWorkflowClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OperationId == Guid.Empty || !IsLowerHex(request.OperationFingerprint, 64)
            || request.EligibleCheckpoints.Count == 0 || !IsExactBounded(request.LeaseOwner, 128)
            || request.NowUtcTicks <= 0 || request.LeaseUntilUtcTicks <= request.NowUtcTicks
            || request.LeaseUntilUtcTicks - request.NowUtcTicks > TimeSpan.FromMinutes(5).Ticks)
            return null;
        var filter = ActiveTenantFilter
                     & Op.Eq(x => x.OperationId, request.OperationId)
                     & Op.Eq(x => x.OperationFingerprint, request.OperationFingerprint)
                     & Op.Eq(x => x.TemporalStorageVersion, ItemWorkflowOperation.CurrentTemporalStorageVersion)
                     & Op.In(x => x.Checkpoint, request.EligibleCheckpoints)
                     & Op.Or(Op.Eq(x => x.NextAttemptAtUtcTicksV1, null), Op.Lte(x => x.NextAttemptAtUtcTicksV1, request.NowUtcTicks))
                     & Op.Or(Op.Eq(x => x.LeaseOwner, null), Op.Lte(x => x.LeaseUntilUtcTicksV1, request.NowUtcTicks));
        var update = Builders<ItemWorkflowOperation>.Update
            .Set(x => x.LeaseOwner, request.LeaseOwner)
            .Set(x => x.LeaseUntilUtcTicksV1, request.LeaseUntilUtcTicks)
            .Set(x => x.UpdatedAtUtcTicksV1, request.NowUtcTicks)
            .Set(x => x.UpdatedAt, new DateTimeOffset(request.NowUtcTicks, TimeSpan.Zero))
            .Inc(x => x.LeaseGeneration, 1)
            .Inc(x => x.Version, 1);
        var operation = await _operations.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<ItemWorkflowOperation> { ReturnDocument = ReturnDocument.After, IsUpsert = false },
            cancellationToken);
        return operation is null
            ? null
            : new ItemWorkflowClaim(_tenantId, operation.OperationId, operation.ItemId, operation.OperationFingerprint,
                request.LeaseOwner, operation.LeaseGeneration, operation.Checkpoint, request.LeaseUntilUtcTicks);
    }

    // The checkpoints the worker never scans: ended, or waiting for a person / the maker.
    public static readonly IReadOnlyList<ItemWorkflowCheckpoint> NotScanned =
    [
        ItemWorkflowCheckpoint.Completed, ItemWorkflowCheckpoint.AwaitingMakerReplay,
        ItemWorkflowCheckpoint.ManualReconciliationRequired, ItemWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
        ItemWorkflowCheckpoint.Superseded, ItemWorkflowCheckpoint.DecisionNotApplicable
    ];

    public async Task<ItemWorkflowRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks, int limit,
        ItemWorkflowRecoveryCursor? after = null, CancellationToken cancellationToken = default)
    {
        if (nowUtcTicks <= 0 || limit is < 1 or > 100 || after?.OperationId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(limit));
        var filter = ActiveTenantFilter
                     & Op.Eq(x => x.TemporalStorageVersion, ItemWorkflowOperation.CurrentTemporalStorageVersion)
                     & Op.Nin(x => x.Checkpoint, NotScanned)
                     & Op.Or(Op.Eq(x => x.NextAttemptAtUtcTicksV1, null), Op.Lte(x => x.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Op.Or(Op.Eq(x => x.LeaseOwner, null), Op.Lte(x => x.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (after is not null)
        {
            filter &= after.NextAttemptAtUtcTicksV1.HasValue
                ? Op.Or(Op.Gt(x => x.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                    Op.And(Op.Eq(x => x.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                        Op.Gt(x => x.OperationId, after.OperationId)))
                : Op.Or(Op.Ne(x => x.NextAttemptAtUtcTicksV1, null),
                    Op.And(Op.Eq(x => x.NextAttemptAtUtcTicksV1, null), Op.Gt(x => x.OperationId, after.OperationId)));
        }
        var rows = await _operations.Find(filter).SortBy(x => x.NextAttemptAtUtcTicksV1).ThenBy(x => x.OperationId)
            .Limit(limit).ToListAsync(cancellationToken);
        var next = rows.Count == limit ? new ItemWorkflowRecoveryCursor(rows[^1].NextAttemptAtUtcTicksV1, rows[^1].OperationId) : null;
        return new(rows, next);
    }

    public async Task<bool> AdvanceAsync(ItemWorkflowClaim claim, ItemWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != _tenantId || mutation.UpdatedAtUtcTicks <= 0
            || !IsAllowedTransition(claim.Checkpoint, mutation.NextCheckpoint)
            || !IsCoherentMutation(claim, mutation)
            // (B) the store invariant: manual reconciliation is never "retryable" — the worker does not scan it, so that
            // pair would be a wait nobody comes back to.
            || mutation.NextCheckpoint == ItemWorkflowCheckpoint.ManualReconciliationRequired
               && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable)
        {
            if (mutation.ReleaseLease && claim.TenantId == _tenantId) await ReleaseLeaseAsync(claim, cancellationToken);
            return false;
        }
        var filter = ClaimFilter(claim, mutation.UpdatedAtUtcTicks);
        var moves = claim.Checkpoint != mutation.NextCheckpoint;
        if (moves && mutation.NextCheckpoint == ItemWorkflowCheckpoint.ApprovalValidated)
            filter &= Op.Eq(x => x.ApprovalGateValidatedAtUtcTicksV1, null)
                      & Op.Eq(x => x.DecisionKind, ProductIdentityDecisionKind.Approved);
        if (moves && mutation.NextCheckpoint == ItemWorkflowCheckpoint.DecisionApplied
            && claim.Checkpoint != ItemWorkflowCheckpoint.ManualReconciliationRequired)
            filter &= claim.Checkpoint == ItemWorkflowCheckpoint.DecisionObserved
                ? Op.Eq(x => x.DecisionKind, ProductIdentityDecisionKind.Rejected)
                : Op.Eq(x => x.DecisionKind, ProductIdentityDecisionKind.Approved);
        if (moves && mutation.NextCheckpoint == ItemWorkflowCheckpoint.DecisionObserved)
            filter &= Op.Eq(x => x.ObjectType, mutation.DecisionObjectType) & Op.Eq(x => x.ObjectId, mutation.DecisionObjectId);

        var update = Builders<ItemWorkflowOperation>.Update
            .Set(x => x.Checkpoint, mutation.NextCheckpoint)
            .Set(x => x.RecoveryDisposition, mutation.RecoveryDisposition)
            .Set(x => x.NextAttemptAtUtcTicksV1, mutation.NextAttemptAtUtcTicksV1)
            .Set(x => x.LastFailureCode, mutation.LastFailureCode)
            .Set(x => x.RetryLimitCauseCode, mutation.RetryLimitCauseCode)
            .Set(x => x.UpdatedAtUtcTicksV1, mutation.UpdatedAtUtcTicks)
            .Set(x => x.UpdatedAt, new DateTimeOffset(mutation.UpdatedAtUtcTicks, TimeSpan.Zero))
            .Inc(x => x.Version, 1);
        if (mutation.WorkflowInstanceId.HasValue) update = update.Set(x => x.WorkflowInstanceId, mutation.WorkflowInstanceId);
        if (mutation.WorkflowTemplateId.HasValue) update = update.Set(x => x.WorkflowTemplateId, mutation.WorkflowTemplateId);
        if (mutation.WorkflowTemplateVersionId.HasValue) update = update.Set(x => x.WorkflowTemplateVersionId, mutation.WorkflowTemplateVersionId);
        if (mutation.ApprovalTaskId.HasValue) update = update.Set(x => x.ApprovalTaskId, mutation.ApprovalTaskId);
        if (mutation.AssignmentSnapshotId.HasValue) update = update.Set(x => x.AssignmentSnapshotId, mutation.AssignmentSnapshotId);
        if (mutation.StartTransitionLogId.HasValue) update = update.Set(x => x.StartTransitionLogId, mutation.StartTransitionLogId);
        if (mutation.WorkflowStartedAtUtcTicksV1.HasValue) update = update.Set(x => x.WorkflowStartedAtUtcTicksV1, mutation.WorkflowStartedAtUtcTicksV1);
        if (mutation.DecisionKind.HasValue) update = update.Set(x => x.DecisionKind, mutation.DecisionKind);
        if (mutation.DecisionObservedAtUtcTicksV1.HasValue) update = update.Set(x => x.DecisionObservedAtUtcTicksV1, mutation.DecisionObservedAtUtcTicksV1);
        if (mutation.DecisionActorSubjectId.HasValue) update = update.Set(x => x.DecisionActorSubjectId, mutation.DecisionActorSubjectId);
        if (mutation.DecisionReasonCode is not null) update = update.Set(x => x.DecisionReasonCode, mutation.DecisionReasonCode);
        if (mutation.DecisionObjectType is not null) update = update.Set(x => x.DecisionObjectType, mutation.DecisionObjectType);
        if (mutation.DecisionObjectId is not null) update = update.Set(x => x.DecisionObjectId, mutation.DecisionObjectId);
        if (mutation.DecisionObjectRef is not null) update = update.Set(x => x.DecisionObjectRef, mutation.DecisionObjectRef);
        if (mutation.DecisionWorkflowTemplateId.HasValue) update = update.Set(x => x.DecisionWorkflowTemplateId, mutation.DecisionWorkflowTemplateId);
        if (mutation.DecisionWorkflowTemplateVersionId.HasValue) update = update.Set(x => x.DecisionWorkflowTemplateVersionId, mutation.DecisionWorkflowTemplateVersionId);
        if (mutation.DecisionTaskStatus is not null) update = update.Set(x => x.DecisionTaskStatus, mutation.DecisionTaskStatus);
        if (mutation.DecisionInstanceStatus is not null) update = update.Set(x => x.DecisionInstanceStatus, mutation.DecisionInstanceStatus);
        if (mutation.DecisionTransitionSequence.HasValue) update = update.Set(x => x.DecisionTransitionSequence, mutation.DecisionTransitionSequence);
        if (mutation.DecisionAtUtcTicksV1.HasValue) update = update.Set(x => x.DecisionAtUtcTicksV1, mutation.DecisionAtUtcTicksV1);
        if (mutation.ApprovalGateValidatedAtUtcTicksV1.HasValue) update = update.Set(x => x.ApprovalGateValidatedAtUtcTicksV1, mutation.ApprovalGateValidatedAtUtcTicksV1);
        if (mutation.WithdrawalCommandId.HasValue) update = update.Set(x => x.WithdrawalCommandId, mutation.WithdrawalCommandId);
        if (mutation.WithdrawalFingerprint is not null) update = update.Set(x => x.WithdrawalFingerprint, mutation.WithdrawalFingerprint);
        if (mutation.WithdrawalRequesterSubjectId.HasValue) update = update.Set(x => x.WithdrawalRequesterSubjectId, mutation.WithdrawalRequesterSubjectId);
        if (mutation.WithdrawalExpectedItemVersion.HasValue) update = update.Set(x => x.WithdrawalExpectedItemVersion, mutation.WithdrawalExpectedItemVersion);
        if (mutation.WithdrawalReasonCode is not null) update = update.Set(x => x.WithdrawalReasonCode, mutation.WithdrawalReasonCode);
        if (mutation.WithdrawalComment is not null) update = update.Set(x => x.WithdrawalComment, mutation.WithdrawalComment);
        if (mutation.WithdrawalExpectedWorkflowInstanceVersion.HasValue) update = update.Set(x => x.WithdrawalExpectedWorkflowInstanceVersion, mutation.WithdrawalExpectedWorkflowInstanceVersion);
        if (mutation.WithdrawalExpectedApprovalTaskVersion.HasValue) update = update.Set(x => x.WithdrawalExpectedApprovalTaskVersion, mutation.WithdrawalExpectedApprovalTaskVersion);
        if (mutation.WithdrawalTransitionLogId.HasValue) update = update.Set(x => x.WithdrawalTransitionLogId, mutation.WithdrawalTransitionLogId);
        if (mutation.WithdrawalObservedAtUtcTicksV1.HasValue) update = update.Set(x => x.WithdrawalObservedAtUtcTicksV1, mutation.WithdrawalObservedAtUtcTicksV1);
        if (mutation.WithdrawalTransitionSequence.HasValue) update = update.Set(x => x.WithdrawalTransitionSequence, mutation.WithdrawalTransitionSequence);
        if (mutation.WithdrawalResultWorkflowInstanceVersion.HasValue) update = update.Set(x => x.WithdrawalResultWorkflowInstanceVersion, mutation.WithdrawalResultWorkflowInstanceVersion);
        if (mutation.WithdrawalResultApprovalTaskVersion.HasValue) update = update.Set(x => x.WithdrawalResultApprovalTaskVersion, mutation.WithdrawalResultApprovalTaskVersion);
        if (mutation.WithdrawalTaskStatus is not null) update = update.Set(x => x.WithdrawalTaskStatus, mutation.WithdrawalTaskStatus);
        if (mutation.WithdrawalInstanceStatus is not null) update = update.Set(x => x.WithdrawalInstanceStatus, mutation.WithdrawalInstanceStatus);
        if (mutation.WithdrawalObjectRef is not null) update = update.Set(x => x.WithdrawalObjectRef, mutation.WithdrawalObjectRef);
        if (mutation.ReleaseLease) update = update.Set(x => x.LeaseOwner, null).Set(x => x.LeaseUntilUtcTicksV1, null);
        if (mutation.WithdrawalUnansweredSinceUtcTicksV1.HasValue && !mutation.ClearWithdrawal)
            update = update.Set(x => x.WithdrawalUnansweredSinceUtcTicksV1, mutation.WithdrawalUnansweredSinceUtcTicksV1);
        if (mutation.ClearWithdrawal)
            update = update.Unset(x => x.WithdrawalUnansweredSinceUtcTicksV1).Unset(x => x.WithdrawalCommandId)
                .Unset(x => x.WithdrawalFingerprint).Unset(x => x.WithdrawalRequesterSubjectId)
                .Unset(x => x.WithdrawalExpectedItemVersion).Unset(x => x.WithdrawalReasonCode).Unset(x => x.WithdrawalComment)
                .Unset(x => x.WithdrawalExpectedWorkflowInstanceVersion).Unset(x => x.WithdrawalExpectedApprovalTaskVersion)
                .Unset(x => x.WithdrawalTransitionLogId).Unset(x => x.WithdrawalObservedAtUtcTicksV1)
                .Unset(x => x.WithdrawalTransitionSequence).Unset(x => x.WithdrawalResultWorkflowInstanceVersion)
                .Unset(x => x.WithdrawalResultApprovalTaskVersion).Unset(x => x.WithdrawalTaskStatus)
                .Unset(x => x.WithdrawalInstanceStatus).Unset(x => x.WithdrawalObjectRef);
        if (mutation.ClearDecision)
            update = update.Unset(x => x.DecisionKind).Unset(x => x.DecisionObservedAtUtcTicksV1)
                .Unset(x => x.DecisionActorSubjectId).Unset(x => x.DecisionReasonCode).Unset(x => x.DecisionObjectType)
                .Unset(x => x.DecisionObjectId).Unset(x => x.DecisionObjectRef).Unset(x => x.DecisionWorkflowTemplateId)
                .Unset(x => x.DecisionWorkflowTemplateVersionId).Unset(x => x.DecisionTaskStatus)
                .Unset(x => x.DecisionInstanceStatus).Unset(x => x.DecisionTransitionSequence)
                .Unset(x => x.DecisionAtUtcTicksV1).Unset(x => x.ApprovalGateValidatedAtUtcTicksV1);
        if (mutation.LastStartSentAtUtcTicksV1.HasValue)
            update = update.Set(x => x.LastStartSentAtUtcTicksV1, mutation.LastStartSentAtUtcTicksV1);
        update = mutation.Streak switch
        {
            RecoveryStreakWrite.Increment => update.Inc(x => x.FailedAttemptCount, 1),
            RecoveryStreakWrite.Keep => update,
            _ => update.Set(x => x.FailedAttemptCount, 0)
        };
        var result = await _operations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        // Progress the store refused gives the lease back (only this claim's own lease).
        if (result.ModifiedCount != 1 && mutation.ReleaseLease) await ReleaseLeaseAsync(claim, cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<bool> ReleaseLeaseAsync(ItemWorkflowClaim claim, CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != _tenantId) return false;
        var released = await _operations.UpdateOneAsync(
            ActiveTenantFilter & Op.Eq(x => x.OperationId, claim.OperationId) & Op.Eq(x => x.LeaseOwner, claim.LeaseOwner)
            & Op.Eq(x => x.LeaseGeneration, claim.LeaseGeneration),
            Builders<ItemWorkflowOperation>.Update.Set(x => x.LeaseOwner, null).Set(x => x.LeaseUntilUtcTicksV1, null),
            cancellationToken: cancellationToken);
        return released.ModifiedCount == 1;
    }

    private FilterDefinition<ItemWorkflowOperation> ClaimFilter(ItemWorkflowClaim claim, long nowUtcTicks) =>
        ActiveTenantFilter
        & Op.Eq(x => x.OperationId, claim.OperationId)
        & Op.Eq(x => x.OperationFingerprint, claim.OperationFingerprint)
        & Op.Eq(x => x.Checkpoint, claim.Checkpoint)
        & Op.Eq(x => x.LeaseOwner, claim.LeaseOwner)
        & Op.Eq(x => x.LeaseGeneration, claim.LeaseGeneration)
        & Op.Gt(x => x.LeaseUntilUtcTicksV1, nowUtcTicks);

    private static ItemLifecycleStatus SourceStatus(ItemActivationKind kind) =>
        kind == ItemActivationKind.Reactivation ? ItemLifecycleStatus.Deactivated : ItemLifecycleStatus.Draft;

    public async Task<ItemWorkflowWriteResult> ApplyLocalPendingAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        ProductIdentityWorkflowBinding binding, long updatedAtUtcTicks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(binding);
        if (claim.TenantId != _tenantId || claim.OperationId != operation.OperationId
            || claim.Checkpoint != ItemWorkflowCheckpoint.WorkflowStarted || updatedAtUtcTicks <= 0
            || binding.ObjectId != operation.ItemId || binding.SubmitterSubjectId != operation.MakerSubjectId
            || binding.StartIdempotencyKey != operation.StartIdempotencyKey || binding.TerminalDecision is not null
            || binding.CancellationEvidence is not null)
            return new(false, false, null, "ITEM_ACTIVATION_LOCAL_PENDING_CONTRACT_INVALID");
        var current = await _items.Find(LiveItem(operation.ItemId)).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return new(false, false, null, "ITEM_NOT_FOUND");
        if (current.Version == operation.ExpectedItemVersion + 1
            && current.LifecycleStatus == ItemLifecycleStatus.PendingActivation
            && current.ActivationWorkflowBinding?.StartIdempotencyKey == operation.StartIdempotencyKey
            && current.ActivationWorkflowBinding.WorkflowInstanceId == binding.WorkflowInstanceId)
            return new(true, true, current, null);
        try
        {
            var written = await _items.FindOneAndUpdateAsync(
                LiveItem(operation.ItemId) & Builders<Item>.Filter.Eq(x => x.Version, operation.ExpectedItemVersion)
                & Builders<Item>.Filter.Eq(x => x.LifecycleStatus, SourceStatus(operation.ActivationKind))
                & Builders<Item>.Filter.Eq(x => x.ActivationSubmitOperationId, operation.OperationId),
                Builders<Item>.Update
                    .Set(x => x.LifecycleStatus, ItemLifecycleStatus.PendingActivation)
                    .Set(x => x.ActivationKind, operation.ActivationKind)
                    .Set(x => x.ActivationWorkflowBinding, binding)
                    .Set(x => x.ActivationRequestReason, operation.RequestReason)
                    .Set(x => x.ActivationSubmitOperationId, null)
                    .Set(x => x.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(x => x.Version, 1),
                new FindOneAndUpdateOptions<Item> { ReturnDocument = ReturnDocument.After }, cancellationToken);
            return written is null
                ? new(false, false, current, current.Version != operation.ExpectedItemVersion
                    ? "ITEM_CONCURRENCY_CONFLICT" : "ITEM_ACTIVATION_STATE_CONFLICT")
                : new(true, false, written, null);
        }
        catch (MongoException exception) when (exception is not MongoWriteException)
        {
            return new(false, false, current, ProductIdentityStore.Unavailable);
        }
    }

    public async Task<ItemWorkflowWriteResult> ApplyDecisionAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        ProductIdentityWorkflowDecisionEvidence evidence, long updatedAtUtcTicks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(evidence);
        var approved = evidence.Decision == ProductIdentityDecisionKind.Approved;
        if (claim.TenantId != _tenantId || claim.OperationId != operation.OperationId || updatedAtUtcTicks <= 0
            || claim.Checkpoint != (approved ? ItemWorkflowCheckpoint.ApprovalValidated : ItemWorkflowCheckpoint.DecisionObserved)
            || evidence.Decision is not (ProductIdentityDecisionKind.Approved or ProductIdentityDecisionKind.Rejected)
            || evidence.WorkflowInstanceId != operation.WorkflowInstanceId || evidence.ObjectId != operation.ItemId
            || evidence.DecisionActorSubjectId == Guid.Empty
            // Maker ≠ approver, held by the store as well (§2.6): the maker's decision is never written.
            || evidence.DecisionActorSubjectId == operation.MakerSubjectId)
            return new(false, false, null, "ITEM_ACTIVATION_DECISION_CONTRACT_INVALID");
        var target = approved
            ? ItemLifecycleStatus.Active
            : SourceStatus(operation.ActivationKind);
        var expectedVersion = operation.ExpectedItemVersion + 1;
        var current = await _items.Find(LiveItem(operation.ItemId)).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return new(false, false, null, "ITEM_NOT_FOUND");
        // The replay is recognised by the decision it wrote (S2 FIX1 item 3), not by an exact version: a later edit of the
        // Active item moves the version on and must not turn the replay into a conflict.
        if (current.Version >= expectedVersion + 1 && current.LifecycleStatus == target
            && current.ActivationWorkflowBinding?.WorkflowInstanceId == operation.WorkflowInstanceId
            && current.ActivationWorkflowBinding.StartIdempotencyKey == operation.StartIdempotencyKey
            && current.ActivationWorkflowBinding.TerminalDecision is { } done
            && done.ApprovalTaskId == evidence.ApprovalTaskId
            && done.TransitionSequence == evidence.TransitionSequence && done.Decision == evidence.Decision
            && done.DecisionActorSubjectId == evidence.DecisionActorSubjectId)
            return new(true, true, current, null);
        try
        {
            var written = await _items.FindOneAndUpdateAsync(
                LiveItem(operation.ItemId) & Builders<Item>.Filter.Eq(x => x.Version, expectedVersion)
                & Builders<Item>.Filter.Eq(x => x.LifecycleStatus, ItemLifecycleStatus.PendingActivation)
                & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.WorkflowInstanceId, operation.WorkflowInstanceId)
                & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.StartIdempotencyKey, operation.StartIdempotencyKey)
                & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.TerminalDecision, null)
                // S2 FIX1 (minor): maker ≠ approver against the PERSISTED maker of this approval, in the write itself — a
                // caller that names the wrong maker cannot slip the maker's own decision through.
                & Builders<Item>.Filter.Ne(x => x.ActivationWorkflowBinding!.SubmitterSubjectId, evidence.DecisionActorSubjectId),
                Builders<Item>.Update
                    .Set(x => x.LifecycleStatus, target)
                    .Set(x => x.ActivationWorkflowBinding!.TerminalDecision, evidence)
                    .Set(x => x.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                    .Inc(x => x.Version, 1),
                new FindOneAndUpdateOptions<Item> { ReturnDocument = ReturnDocument.After }, cancellationToken);
            return written is null
                ? new(false, false, current, current.Version != expectedVersion
                    ? "ITEM_CONCURRENCY_CONFLICT" : "ITEM_ACTIVATION_STATE_CONFLICT")
                : new(true, false, written, null);
        }
        catch (MongoException exception) when (exception is not MongoWriteException)
        {
            return new(false, false, current, ProductIdentityStore.Unavailable);
        }
    }

    public async Task<ItemWorkflowWriteResult> ApplyWithdrawalAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        ProductIdentityWorkflowCancellationEvidence cancellationEvidence, long updatedAtUtcTicks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(cancellationEvidence);
        if (claim.TenantId != _tenantId || claim.OperationId != operation.OperationId
            || claim.OperationFingerprint != operation.OperationFingerprint
            || !WithdrawalWriterFrom.Contains(claim.Checkpoint) || updatedAtUtcTicks <= 0
            || operation.WithdrawalExpectedItemVersion is not > 0
            || operation.WithdrawalCommandId is not { } commandId || commandId == Guid.Empty
            || cancellationEvidence.RequesterSubjectId != operation.MakerSubjectId
            || cancellationEvidence.WorkflowInstanceId != operation.WorkflowInstanceId
            || cancellationEvidence.ApprovalTaskId != operation.ApprovalTaskId
            || cancellationEvidence.IdempotencyKey != commandId.ToString("D"))
            return new(false, false, null, "ITEM_ACTIVATION_WITHDRAWAL_CONTRACT_INVALID");
        var target = SourceStatus(operation.ActivationKind);
        return await EndInTransactionAsync(claim, operation, updatedAtUtcTicks, WithdrawalWriterEnd, null,
            LiveItem(operation.ItemId) & Builders<Item>.Filter.Eq(x => x.Version, operation.WithdrawalExpectedItemVersion.Value)
            & Builders<Item>.Filter.Eq(x => x.LifecycleStatus, ItemLifecycleStatus.PendingActivation)
            & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.WorkflowInstanceId, operation.WorkflowInstanceId)
            & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.ApprovalTaskId, operation.ApprovalTaskId)
            & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.SubmitterSubjectId, operation.MakerSubjectId)
            // S2 FIX1 (minor): only the persisted maker withdraws — held in the write against the binding itself.
            & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.SubmitterSubjectId, cancellationEvidence.RequesterSubjectId)
            & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.TerminalDecision, null),
            Builders<Item>.Update.Set(x => x.LifecycleStatus, target)
                .Set(x => x.ActivationWorkflowBinding!.CancellationEvidence, cancellationEvidence),
            current => current.LifecycleStatus == target && current.Version == operation.WithdrawalExpectedItemVersion + 1
                       && current.ActivationWorkflowBinding?.CancellationEvidence?.IdempotencyKey == commandId.ToString("D"),
            operation.WithdrawalExpectedItemVersion.Value, cancellationToken);
    }

    public Task<ItemWorkflowWriteResult> AbandonBeforeStartAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        string reasonCode, long updatedAtUtcTicks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (claim.TenantId != _tenantId || claim.OperationId != operation.OperationId
            || claim.OperationFingerprint != operation.OperationFingerprint
            || !AbandonWriterFrom.Contains(claim.Checkpoint) || updatedAtUtcTicks <= 0 || !IsExactBounded(reasonCode, 128))
            return Task.FromResult(new ItemWorkflowWriteResult(false, false, null, "ITEM_ACTIVATION_END_CONTRACT_INVALID"));
        // The item was never pending (the engine refused the start): its version moves so the resubmit is a new
        // operation, and the fence goes — only its own (Eq(null) also matches a legacy document).
        return EndInTransactionAsync(claim, operation, updatedAtUtcTicks, AbandonWriterEnd, reasonCode,
            LiveItem(operation.ItemId) & Builders<Item>.Filter.Eq(x => x.Version, operation.ExpectedItemVersion)
            & Builders<Item>.Filter.Eq(x => x.LifecycleStatus, SourceStatus(operation.ActivationKind))
            & (Builders<Item>.Filter.Eq(x => x.ActivationSubmitOperationId, null)
               | Builders<Item>.Filter.Eq(x => x.ActivationSubmitOperationId, operation.OperationId)),
            Builders<Item>.Update.Set(x => x.ActivationSubmitOperationId, null),
            current => current.Version == operation.ExpectedItemVersion + 1 && current.ActivationSubmitOperationId is null,
            operation.ExpectedItemVersion, cancellationToken,
            // "nothing was sent" was decided on a document; the end is written only if its start-sent mark is still
            // the one decided on (a start sent meanwhile fails it).
            Op.Eq(x => x.LastStartSentAtUtcTicksV1, operation.LastStartSentAtUtcTicksV1));
    }

    public Task<ItemWorkflowWriteResult> ApplyDecisionNotApplicableAsync(ItemWorkflowClaim claim,
        ItemWorkflowOperation operation, string reasonCode, long updatedAtUtcTicks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (claim.TenantId != _tenantId || claim.OperationId != operation.OperationId
            || claim.OperationFingerprint != operation.OperationFingerprint
            || !NotApplicableWriterFrom.Contains(claim.Checkpoint) || updatedAtUtcTicks <= 0 || !IsExactBounded(reasonCode, 128))
            return Task.FromResult(new ItemWorkflowWriteResult(false, false, null, "ITEM_ACTIVATION_END_CONTRACT_INVALID"));
        var target = SourceStatus(operation.ActivationKind);
        // The pending item of THIS approval goes back where the request found it (§8.1: a reactivation to Deactivated).
        return EndInTransactionAsync(claim, operation, updatedAtUtcTicks, NotApplicableWriterEnd, reasonCode,
            LiveItem(operation.ItemId) & Builders<Item>.Filter.Eq(x => x.Version, operation.ExpectedItemVersion + 1)
            & Builders<Item>.Filter.Eq(x => x.LifecycleStatus, ItemLifecycleStatus.PendingActivation)
            & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.WorkflowInstanceId, operation.WorkflowInstanceId)
            & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.StartIdempotencyKey, operation.StartIdempotencyKey)
            & Builders<Item>.Filter.Eq(x => x.ActivationWorkflowBinding!.TerminalDecision, null),
            Builders<Item>.Update.Set(x => x.LifecycleStatus, target),
            current => current.Version == operation.ExpectedItemVersion + 2 && current.LifecycleStatus == target,
            operation.ExpectedItemVersion + 1, cancellationToken);
    }

    /*
     * The store's own multi-step writers (they write their end checkpoint directly, without AdvanceAsync's
     * IsAllowedTransition) — their from-lists and ends are these constants, read by the writers above AND by the
     * transition matrix test.
     */
    public static readonly IReadOnlyList<ItemWorkflowCheckpoint> WithdrawalWriterFrom = [ItemWorkflowCheckpoint.WithdrawalObserved];
    public const ItemWorkflowCheckpoint WithdrawalWriterEnd = ItemWorkflowCheckpoint.WithdrawalApplied;
    public static readonly IReadOnlyList<ItemWorkflowCheckpoint> AbandonWriterFrom =
        [ItemWorkflowCheckpoint.StartOutcomeUnknown, ItemWorkflowCheckpoint.AwaitingMakerReplay,
            ItemWorkflowCheckpoint.ManualReconciliationRequired];
    public const ItemWorkflowCheckpoint AbandonWriterEnd = ItemWorkflowCheckpoint.AbandonedBeforeWorkflowStart;
    public static readonly IReadOnlyList<ItemWorkflowCheckpoint> NotApplicableWriterFrom =
        [ItemWorkflowCheckpoint.AwaitingDecision, ItemWorkflowCheckpoint.DecisionObserved, ItemWorkflowCheckpoint.ApprovalValidated];
    public const ItemWorkflowCheckpoint NotApplicableWriterEnd = ItemWorkflowCheckpoint.DecisionNotApplicable;

    // The item's move and the operation's end are ONE transaction: the item never shows a state its operation does not.
    private async Task<ItemWorkflowWriteResult> EndInTransactionAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        long updatedAtUtcTicks, ItemWorkflowCheckpoint endCheckpoint, string? reasonCode, FilterDefinition<Item> itemFilter,
        UpdateDefinition<Item> itemUpdate, Func<Item, bool> alreadyDone, int expectedItemVersion,
        CancellationToken cancellationToken, FilterDefinition<ItemWorkflowOperation>? operationGuard = null)
    {
        var current = await _items.Find(LiveItem(operation.ItemId)).FirstOrDefaultAsync(cancellationToken);
        if (current is null) return new(false, false, null, "ITEM_NOT_FOUND");
        var itemDone = alreadyDone(current);
        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        try
        {
            session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
            if (!itemDone)
            {
                var itemResult = await _items.UpdateOneAsync(session, itemFilter,
                    itemUpdate.Set(x => x.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero)).Inc(x => x.Version, 1),
                    cancellationToken: cancellationToken);
                if (itemResult.ModifiedCount != 1)
                {
                    await session.AbortTransactionAsync(cancellationToken);
                    return new(false, false, current, current.Version != expectedItemVersion
                        ? "ITEM_CONCURRENCY_CONFLICT" : "ITEM_ACTIVATION_STATE_CONFLICT");
                }
            }
            var operationUpdate = Builders<ItemWorkflowOperation>.Update
                .Set(x => x.Checkpoint, endCheckpoint)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.None)
                .Set(x => x.NextAttemptAtUtcTicksV1, null).Set(x => x.LastFailureCode, reasonCode)
                .Set(x => x.FailedAttemptCount, 0)
                .Set(x => x.LeaseOwner, null).Set(x => x.LeaseUntilUtcTicksV1, null)
                .Set(x => x.UpdatedAtUtcTicksV1, updatedAtUtcTicks)
                .Set(x => x.UpdatedAt, new DateTimeOffset(updatedAtUtcTicks, TimeSpan.Zero))
                .Inc(x => x.Version, 1);
            var operationResult = await _operations.UpdateOneAsync(session,
                ClaimFilter(claim, updatedAtUtcTicks) & (operationGuard ?? Op.Empty), operationUpdate,
                cancellationToken: cancellationToken);
            if (operationResult.ModifiedCount != 1)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new(false, false, current, "ITEM_ACTIVATION_WORKFLOW_CONCURRENCY_CONFLICT");
            }
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (MongoException)
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
            return new(false, false, current, ProductIdentityStore.Unavailable);
        }
        catch (NotSupportedException)
        {
            // A standalone mongod: no transaction — the store cannot make the two writes one, so it writes neither.
            return new(false, false, current, ProductIdentityStore.Unavailable);
        }
        current = await _items.Find(LiveItem(operation.ItemId)).FirstOrDefaultAsync(cancellationToken);
        return new(true, itemDone, current, null);
    }

    private bool IsValidForReservation(ItemWorkflowOperation operation) =>
        _tenantId != Guid.Empty
        && operation.OperationId != Guid.Empty
        && operation.ItemId != Guid.Empty
        && operation.ActivationKind is ItemActivationKind.Activation or ItemActivationKind.Reactivation
        && operation.ExpectedItemVersion >= 0
        && operation.MakerSubjectId != Guid.Empty
        && operation.ApproverPositionId != Guid.Empty
        && (operation.WorkflowTemplateId.HasValue ^ operation.WorkflowTemplateCode is not null)
        && IsExactBounded(operation.ReasonCode, 128)
        && operation.ObjectType == "Item"
        && operation.ObjectId == operation.ItemId.ToString("D")
        && IsExactBounded(operation.ObjectRef, 256)
        && IsExactBounded(operation.StartIdempotencyKey, 256)
        && IsLowerHex(operation.OperationFingerprint, 64)
        && (!operation.WorkflowTemplateId.HasValue || operation.WorkflowTemplateId != Guid.Empty)
        && (operation.WorkflowTemplateCode is null || IsExactBounded(operation.WorkflowTemplateCode, 128))
        && operation.LastStartSentAtUtcTicksV1 == 0
        && (operation.RequestReason is null || IsExactBounded(operation.RequestReason, 500))
        && operation.TemporalStorageVersion is 0 or ItemWorkflowOperation.CurrentTemporalStorageVersion;

    private static bool IsExactReplay(ItemWorkflowOperation existing, ItemWorkflowOperation candidate) =>
        existing.OperationId == candidate.OperationId
        && existing.ItemId == candidate.ItemId
        && existing.ActivationKind == candidate.ActivationKind
        && existing.ExpectedItemVersion == candidate.ExpectedItemVersion
        && existing.MakerSubjectId == candidate.MakerSubjectId
        && existing.WorkflowTemplateId == candidate.WorkflowTemplateId
        && existing.WorkflowTemplateCode == candidate.WorkflowTemplateCode
        && existing.ApproverPositionId == candidate.ApproverPositionId
        && existing.ReasonCode == candidate.ReasonCode
        && existing.RequestReason == candidate.RequestReason
        && existing.CommentRequired == candidate.CommentRequired
        && existing.EvidenceRequired == candidate.EvidenceRequired
        && existing.ObjectType == candidate.ObjectType
        && existing.ObjectId == candidate.ObjectId
        && existing.ObjectRef == candidate.ObjectRef
        && existing.StartIdempotencyKey == candidate.StartIdempotencyKey
        && existing.OperationFingerprint == candidate.OperationFingerprint;

    // Public so the exhaustive test can hold it against every move the processor makes (the LSKU table, item for LSKU).
    public static bool IsAllowedTransition(ItemWorkflowCheckpoint current, ItemWorkflowCheckpoint next) =>
        current == next
        || (current, next) switch
        {
            (ItemWorkflowCheckpoint.Prepared, ItemWorkflowCheckpoint.StartOutcomeUnknown) => true,
            (ItemWorkflowCheckpoint.StartOutcomeUnknown,
                ItemWorkflowCheckpoint.WorkflowStarted or ItemWorkflowCheckpoint.AwaitingMakerReplay
                or ItemWorkflowCheckpoint.AbandonedBeforeWorkflowStart or ItemWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (ItemWorkflowCheckpoint.AwaitingMakerReplay,
                ItemWorkflowCheckpoint.WorkflowStarted or ItemWorkflowCheckpoint.StartOutcomeUnknown
                or ItemWorkflowCheckpoint.AbandonedBeforeWorkflowStart or ItemWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (ItemWorkflowCheckpoint.AwaitingDecision or ItemWorkflowCheckpoint.DecisionObserved
                or ItemWorkflowCheckpoint.ApprovalValidated, ItemWorkflowCheckpoint.DecisionNotApplicable) => true,
            (ItemWorkflowCheckpoint.ManualReconciliationRequired,
                ItemWorkflowCheckpoint.WorkflowStarted or ItemWorkflowCheckpoint.AwaitingDecision
                or ItemWorkflowCheckpoint.DecisionApplied or ItemWorkflowCheckpoint.AbandonedBeforeWorkflowStart) => true,
            (ItemWorkflowCheckpoint.WorkflowStarted,
                ItemWorkflowCheckpoint.LocalPendingApplied or ItemWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (ItemWorkflowCheckpoint.LocalPendingApplied, ItemWorkflowCheckpoint.AwaitingDecision) => true,
            (ItemWorkflowCheckpoint.AwaitingDecision,
                ItemWorkflowCheckpoint.DecisionObserved or ItemWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (ItemWorkflowCheckpoint.DecisionObserved,
                ItemWorkflowCheckpoint.ApprovalValidated or ItemWorkflowCheckpoint.DecisionApplied
                or ItemWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (ItemWorkflowCheckpoint.ApprovalValidated,
                ItemWorkflowCheckpoint.DecisionApplied or ItemWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (ItemWorkflowCheckpoint.DecisionApplied,
                ItemWorkflowCheckpoint.Completed or ItemWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (ItemWorkflowCheckpoint.AwaitingDecision, ItemWorkflowCheckpoint.WithdrawalRequested) => true,
            (ItemWorkflowCheckpoint.WithdrawalRequested,
                ItemWorkflowCheckpoint.WithdrawalPreflightObserved or ItemWorkflowCheckpoint.AwaitingDecision) => true,
            (ItemWorkflowCheckpoint.WithdrawalPreflightObserved,
                ItemWorkflowCheckpoint.WithdrawalOutcomeUnknown or ItemWorkflowCheckpoint.WithdrawalObserved
                or ItemWorkflowCheckpoint.AwaitingDecision or ItemWorkflowCheckpoint.AwaitingMakerReplay) => true,
            (ItemWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                ItemWorkflowCheckpoint.WithdrawalObserved or ItemWorkflowCheckpoint.AwaitingDecision
                or ItemWorkflowCheckpoint.AwaitingMakerReplay) => true,
            (ItemWorkflowCheckpoint.AwaitingMakerReplay,
                ItemWorkflowCheckpoint.WithdrawalOutcomeUnknown or ItemWorkflowCheckpoint.WithdrawalObserved
                or ItemWorkflowCheckpoint.AwaitingDecision) => true,
            (ItemWorkflowCheckpoint.WithdrawalObserved,
                ItemWorkflowCheckpoint.WithdrawalApplied or ItemWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (ItemWorkflowCheckpoint.WithdrawalApplied, ItemWorkflowCheckpoint.Completed) => true,
            _ => false
        };

    // A pure function of its arguments, public so the processor tests' fake store applies exactly the real store's rule.
    public static bool IsCoherentMutation(ItemWorkflowClaim claim, ItemWorkflowCheckpointMutation mutation)
    {
        var current = claim.Checkpoint;
        var hasAnyStartProof = mutation.WorkflowInstanceId.HasValue || mutation.WorkflowTemplateId.HasValue
            || mutation.WorkflowTemplateVersionId.HasValue || mutation.ApprovalTaskId.HasValue
            || mutation.AssignmentSnapshotId.HasValue || mutation.StartTransitionLogId.HasValue
            || mutation.WorkflowStartedAtUtcTicksV1.HasValue;
        var hasCompleteStartProof = mutation.WorkflowInstanceId is { } workflowId && workflowId != Guid.Empty
            && mutation.WorkflowTemplateId is { } templateId && templateId != Guid.Empty
            && mutation.WorkflowTemplateVersionId is { } templateVersionId && templateVersionId != Guid.Empty
            && mutation.ApprovalTaskId is { } taskId && taskId != Guid.Empty
            && mutation.AssignmentSnapshotId is { } snapshotId && snapshotId != Guid.Empty
            && mutation.StartTransitionLogId is { } transitionId && transitionId != Guid.Empty
            && mutation.WorkflowStartedAtUtcTicksV1 is > 0;
        var hasAnyDecisionProof = mutation.DecisionKind.HasValue || mutation.DecisionObservedAtUtcTicksV1.HasValue
            || mutation.DecisionActorSubjectId.HasValue || mutation.DecisionReasonCode is not null
            || mutation.DecisionObjectType is not null || mutation.DecisionObjectId is not null
            || mutation.DecisionObjectRef is not null || mutation.DecisionWorkflowTemplateId.HasValue
            || mutation.DecisionWorkflowTemplateVersionId.HasValue || mutation.DecisionTaskStatus is not null
            || mutation.DecisionInstanceStatus is not null || mutation.DecisionTransitionSequence.HasValue
            || mutation.DecisionAtUtcTicksV1.HasValue;
        var hasCompleteDecisionProof = mutation.DecisionKind is ProductIdentityDecisionKind.Approved or ProductIdentityDecisionKind.Rejected
            && mutation.DecisionObservedAtUtcTicksV1 is > 0
            && mutation.DecisionActorSubjectId is { } decisionActor && decisionActor != Guid.Empty
            && IsOptionalExactBounded(mutation.DecisionReasonCode, 128)
            && mutation.DecisionObjectType == "Item"
            && Guid.TryParseExact(mutation.DecisionObjectId, "D", out var decisionObjectId) && decisionObjectId != Guid.Empty
            && IsExactBounded(mutation.DecisionObjectRef, 256)
            && mutation.DecisionWorkflowTemplateId is { } decisionTemplateId && decisionTemplateId != Guid.Empty
            && mutation.DecisionWorkflowTemplateVersionId is { } decisionTemplateVersionId && decisionTemplateVersionId != Guid.Empty
            && IsExactBounded(mutation.DecisionTaskStatus, 64) && IsExactBounded(mutation.DecisionInstanceStatus, 64)
            && mutation.DecisionTransitionSequence is > 0 && mutation.DecisionAtUtcTicksV1 is > 0
            && (mutation.DecisionKind == ProductIdentityDecisionKind.Approved
                && mutation.DecisionTaskStatus == "Approved" && mutation.DecisionInstanceStatus == "Completed"
                || mutation.DecisionKind == ProductIdentityDecisionKind.Rejected
                && mutation.DecisionTaskStatus == "Rejected" && mutation.DecisionInstanceStatus == "Rejected"
                && IsExactBounded(mutation.DecisionReasonCode, 128));
        var hasAnyApprovalProof = mutation.ApprovalGateValidatedAtUtcTicksV1.HasValue;
        var hasWithdrawalRequest = mutation.WithdrawalCommandId.HasValue || mutation.WithdrawalFingerprint is not null
            || mutation.WithdrawalRequesterSubjectId.HasValue || mutation.WithdrawalExpectedItemVersion.HasValue
            || mutation.WithdrawalReasonCode is not null || mutation.WithdrawalComment is not null;
        var completeWithdrawalRequest = mutation.WithdrawalCommandId is { } withdrawalCommandId && withdrawalCommandId != Guid.Empty
            && mutation.WithdrawalRequesterSubjectId is { } withdrawalRequester && withdrawalRequester != Guid.Empty
            && mutation.WithdrawalExpectedItemVersion is > 0 && IsLowerHex(mutation.WithdrawalFingerprint, 64)
            && IsExactBounded(mutation.WithdrawalReasonCode, 128) && IsOptionalExactBounded(mutation.WithdrawalComment, 2000);
        var hasWithdrawalPreflight = mutation.WithdrawalExpectedWorkflowInstanceVersion.HasValue
            || mutation.WithdrawalExpectedApprovalTaskVersion.HasValue || mutation.WithdrawalObjectRef is not null;
        var completeWithdrawalPreflight = mutation.WithdrawalExpectedWorkflowInstanceVersion is > 0
            && mutation.WithdrawalExpectedApprovalTaskVersion is > 0 && IsExactBounded(mutation.WithdrawalObjectRef, 256);
        var hasWithdrawalEvidence = mutation.WithdrawalTransitionLogId.HasValue || mutation.WithdrawalObservedAtUtcTicksV1.HasValue
            || mutation.WithdrawalTransitionSequence.HasValue || mutation.WithdrawalResultWorkflowInstanceVersion.HasValue
            || mutation.WithdrawalResultApprovalTaskVersion.HasValue || mutation.WithdrawalTaskStatus is not null
            || mutation.WithdrawalInstanceStatus is not null;
        var completeWithdrawalEvidence = mutation.WithdrawalTransitionLogId is { } withdrawalLogId && withdrawalLogId != Guid.Empty
            && mutation.WithdrawalObservedAtUtcTicksV1 is > 0 && mutation.WithdrawalTransitionSequence is > 0
            && mutation.WithdrawalResultWorkflowInstanceVersion is > 0 && mutation.WithdrawalResultApprovalTaskVersion is > 0
            && mutation.WithdrawalTaskStatus == "Cancelled" && mutation.WithdrawalInstanceStatus == "Cancelled";
        var nothingElse = !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof
            && !hasWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence;

        // "The start is about to be sent" — the same checkpoint, the lease kept, nothing else.
        if (mutation.LastStartSentAtUtcTicksV1.HasValue)
            return mutation.NextCheckpoint == current
                && current is ItemWorkflowCheckpoint.StartOutcomeUnknown or ItemWorkflowCheckpoint.AwaitingMakerReplay
                && mutation.LastStartSentAtUtcTicksV1 > 0 && !mutation.ReleaseLease
                && !mutation.NextAttemptAtUtcTicksV1.HasValue && IsOptionalExactBounded(mutation.LastFailureCode, 128)
                && nothingElse && !mutation.ClearDecision && !mutation.ClearWithdrawal;
        // A retry where the operation stands carries no proof of any kind.
        if (mutation.NextCheckpoint == current
            && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
            && current is ItemWorkflowCheckpoint.DecisionObserved or ItemWorkflowCheckpoint.ApprovalValidated
                or ItemWorkflowCheckpoint.WorkflowStarted or ItemWorkflowCheckpoint.WithdrawalRequested
                or ItemWorkflowCheckpoint.WithdrawalObserved)
            return mutation.NextAttemptAtUtcTicksV1 > mutation.UpdatedAtUtcTicks
                && IsExactBounded(mutation.LastFailureCode, 128) && mutation.ReleaseLease
                && nothingElse && !mutation.ClearDecision && !mutation.ClearWithdrawal;
        if (mutation.NextCheckpoint == ItemWorkflowCheckpoint.WithdrawalRequested)
            return completeWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence
                && !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof;
        if (mutation.NextCheckpoint == ItemWorkflowCheckpoint.WithdrawalPreflightObserved)
            return completeWithdrawalPreflight && !hasWithdrawalRequest && !hasWithdrawalEvidence
                && !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof;
        if (mutation.NextCheckpoint == ItemWorkflowCheckpoint.WithdrawalObserved)
            return completeWithdrawalEvidence && !hasWithdrawalRequest
                && !mutation.WithdrawalExpectedWorkflowInstanceVersion.HasValue
                && !mutation.WithdrawalExpectedApprovalTaskVersion.HasValue
                && IsExactBounded(mutation.WithdrawalObjectRef, 256)
                && !hasAnyStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof;
        if (mutation.NextCheckpoint == ItemWorkflowCheckpoint.AwaitingMakerReplay
            && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay)
            return nothingElse && IsExactBounded(mutation.LastFailureCode, 128) && mutation.ReleaseLease;
        if (mutation.NextCheckpoint == ItemWorkflowCheckpoint.WorkflowStarted)
            return hasCompleteStartProof && !hasAnyDecisionProof && !hasAnyApprovalProof
                && !hasWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence;
        if (mutation.NextCheckpoint == ItemWorkflowCheckpoint.DecisionObserved)
            return !hasAnyStartProof && hasCompleteDecisionProof && !hasAnyApprovalProof;
        if (mutation.NextCheckpoint == ItemWorkflowCheckpoint.ApprovalValidated)
            return current == ItemWorkflowCheckpoint.DecisionObserved && !hasAnyStartProof && !hasAnyDecisionProof
                && mutation.ApprovalGateValidatedAtUtcTicksV1 is > 0
                && !hasWithdrawalRequest && !hasWithdrawalPreflight && !hasWithdrawalEvidence;
        if (mutation.NextCheckpoint == current && current == ItemWorkflowCheckpoint.ManualReconciliationRequired
            && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
            // The requester's repeat that could not settle a stuck operation leaves it stuck.
            return nothingElse && !mutation.NextAttemptAtUtcTicksV1.HasValue
                && IsOptionalExactBounded(mutation.LastFailureCode, 128) && mutation.ReleaseLease
                && !mutation.ClearDecision && !mutation.ClearWithdrawal;
        if (mutation.NextCheckpoint == current)
            return nothingElse && mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                && mutation.NextAttemptAtUtcTicksV1 > mutation.UpdatedAtUtcTicks
                && IsExactBounded(mutation.LastFailureCode, 128) && mutation.ReleaseLease;
        // A move to another checkpoint may schedule its next attempt — only as a retry.
        return nothingElse && IsOptionalExactBounded(mutation.LastFailureCode, 128)
            && (mutation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                ? mutation.NextAttemptAtUtcTicksV1 > mutation.UpdatedAtUtcTicks
                  && IsExactBounded(mutation.LastFailureCode, 128) && mutation.ReleaseLease
                : !mutation.NextAttemptAtUtcTicksV1.HasValue)
            && (!mutation.ClearWithdrawal
                || mutation.NextCheckpoint == ItemWorkflowCheckpoint.AwaitingDecision
                   && current is ItemWorkflowCheckpoint.WithdrawalRequested or ItemWorkflowCheckpoint.WithdrawalPreflightObserved
                       or ItemWorkflowCheckpoint.WithdrawalOutcomeUnknown or ItemWorkflowCheckpoint.AwaitingMakerReplay)
            && (!mutation.ClearDecision
                || current == ItemWorkflowCheckpoint.ManualReconciliationRequired
                   && mutation.NextCheckpoint is ItemWorkflowCheckpoint.AwaitingDecision or ItemWorkflowCheckpoint.WorkflowStarted);
    }

    private static bool IsExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 } && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);

    private static bool IsOptionalExactBounded(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && !value.Any(char.IsControl);

    private static bool IsLowerHex(string? value, int exactLength) =>
        value is not null && value.Length == exactLength && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private void EnsureIndexes()
    {
        var active = new BsonDocument(nameof(ItemWorkflowOperation.IsDeleted), false);
        _operations.Indexes.CreateMany(
        [
            new CreateIndexModel<ItemWorkflowOperation>(
                Builders<ItemWorkflowOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OperationId),
                new CreateIndexOptions<ItemWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_item_workflow_operation" }),
            new CreateIndexModel<ItemWorkflowOperation>(
                Builders<ItemWorkflowOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.StartIdempotencyKey),
                new CreateIndexOptions<ItemWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_item_workflow_start_key" }),
            new CreateIndexModel<ItemWorkflowOperation>(
                Builders<ItemWorkflowOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ItemId)
                    .Ascending(x => x.ExpectedItemVersion).Ascending(x => x.ActivationKind),
                new CreateIndexOptions<ItemWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_item_workflow_item_version" }),
            new CreateIndexModel<ItemWorkflowOperation>(
                Builders<ItemWorkflowOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IsDeleted)
                    .Ascending(x => x.NextAttemptAtUtcTicksV1).Ascending(x => x.OperationId)
                    .Ascending(x => x.Checkpoint).Ascending(x => x.LeaseUntilUtcTicksV1),
                new CreateIndexOptions { Name = "ix_mdm_item_workflow_recovery" })
        ]);
    }
}

// The §8.4 (B) gauges and the tenants with due work, over all tenants (numbers only).
public sealed class ItemWorkflowTenantPartitionDiscoveryRepository : IItemWorkflowTenantPartitionDiscovery
{
    private readonly IMongoCollection<ItemWorkflowOperation> _operations;
    private static FilterDefinitionBuilder<ItemWorkflowOperation> Op => Builders<ItemWorkflowOperation>.Filter;

    // A withdrawal whose cancel was left unanswered and that only its requester can send again, older than this,
    // needs a person to chase it (the LSKU threshold).
    public static readonly TimeSpan WithdrawalUnansweredThreshold = TimeSpan.FromHours(1);

    // Stuck causes the requester's same submit settles (ReconcileAsync re-reads the engine); every other cause of a
    // manual reconciliation is a person's (BL-530).
    public static readonly IReadOnlyList<string> SettledByTheRequestersRepeat =
    [
        "ITEM_ACTIVATION_WORKFLOW_START_PROOF_INVALID",
        "ITEM_ACTIVATION_WORKFLOW_EVIDENCE_CONFLICT"
    ];

    public ItemWorkflowTenantPartitionDiscoveryRepository(IMongoDatabase database) =>
        _operations = database.GetCollection<ItemWorkflowOperation>(ItemWorkflowOperationRepository.CollectionName);

    public async Task<ItemWorkflowTenantPartitionPage> DiscoverAsync(Guid? afterTenantId, long nowUtcTicks, int limit,
        CancellationToken cancellationToken = default)
    {
        if (afterTenantId == Guid.Empty || nowUtcTicks <= 0 || limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit));
        var filter = Op.Ne(x => x.IsDeleted, true)
                     & Op.Eq(x => x.TemporalStorageVersion, ItemWorkflowOperation.CurrentTemporalStorageVersion)
                     & Op.Nin(x => x.Checkpoint, ItemWorkflowOperationRepository.NotScanned)
                     & Op.Or(Op.Eq(x => x.NextAttemptAtUtcTicksV1, null), Op.Lte(x => x.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Op.Or(Op.Eq(x => x.LeaseOwner, null), Op.Lte(x => x.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (afterTenantId.HasValue) filter &= Op.Gt(x => x.TenantId, afterTenantId.Value);
        var candidates = await _operations.Find(filter).SortBy(x => x.TenantId).ThenBy(x => x.NextAttemptAtUtcTicksV1)
            .ThenBy(x => x.OperationId).Project(x => x.TenantId).Limit(limit).ToListAsync(cancellationToken);
        return new(candidates.Distinct().ToArray(), candidates.Count == limit ? candidates[^1] : null);
    }

    // manual_reconciliation: manual reconciliation, waiting for the maker's replay, or a wait at its retry limit.
    public static FilterDefinition<ItemWorkflowOperation> Stuck =>
        Op.Ne(x => x.IsDeleted, true)
        & ((Op.Eq(x => x.Checkpoint, ItemWorkflowCheckpoint.ManualReconciliationRequired)
            & Op.Eq(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired))
           | Op.Eq(x => x.Checkpoint, ItemWorkflowCheckpoint.AwaitingMakerReplay)
           | (Op.Eq(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.Retryable)
              & Op.Eq(x => x.LastFailureCode,
                  Diten.MdmService.Application.Features.ItemMaster.Activation.ItemActivationWorkflowProcessor.RetryLimitReached)));

    // needing_a_person: a stuck cause the requester's repeat cannot settle, or an unanswered withdrawal past the threshold.
    public static FilterDefinition<ItemWorkflowOperation> NeedingAPerson(long nowUtcTicks)
    {
        var cutoff = nowUtcTicks - WithdrawalUnansweredThreshold.Ticks;
        return (Op.Ne(x => x.IsDeleted, true)
                & Op.Eq(x => x.Checkpoint, ItemWorkflowCheckpoint.ManualReconciliationRequired)
                & Op.Eq(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                & Op.Nin(x => x.LastFailureCode, SettledByTheRequestersRepeat))
               | (Op.Ne(x => x.IsDeleted, true) & Op.Ne(x => x.WithdrawalCommandId, null)
                  & Op.In(x => x.Checkpoint, new[] { ItemWorkflowCheckpoint.AwaitingMakerReplay, ItemWorkflowCheckpoint.WithdrawalOutcomeUnknown })
                  & (Op.Lt(x => x.WithdrawalUnansweredSinceUtcTicksV1, cutoff)
                     // A document without the field ages from its last update (never Eq on a missing field alone).
                     | (Op.Eq(x => x.WithdrawalUnansweredSinceUtcTicksV1, null) & Op.Lt(x => x.UpdatedAtUtcTicksV1, cutoff))));
    }

    public Task<long> CountManualReconciliationAsync(long nowUtcTicks, CancellationToken cancellationToken = default) =>
        _operations.CountDocumentsAsync(Stuck, cancellationToken: cancellationToken);

    public Task<long> CountNeedingAPersonAsync(long nowUtcTicks, CancellationToken cancellationToken = default) =>
        _operations.CountDocumentsAsync(NeedingAPerson(nowUtcTicks), cancellationToken: cancellationToken);

    // S2 FIX1 item 2: what the worker scans (the recovery filter's checkpoints), untouched since the cutoff. A running
    // worker touches each of them at least every retry ceiling (15 min) — so an old one means nobody is processing.
    public static FilterDefinition<ItemWorkflowOperation> Unprocessed(long untouchedSinceUtcTicks) =>
        Op.Ne(x => x.IsDeleted, true)
        & Op.Nin(x => x.Checkpoint, ItemWorkflowOperationRepository.NotScanned)
        & Op.Lt(x => x.UpdatedAtUtcTicksV1, untouchedSinceUtcTicks);

    public Task<long> CountUnprocessedAsync(long untouchedSinceUtcTicks, CancellationToken cancellationToken = default) =>
        _operations.CountDocumentsAsync(Unprocessed(untouchedSinceUtcTicks), cancellationToken: cancellationToken);
}
