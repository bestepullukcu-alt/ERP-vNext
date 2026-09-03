using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GlobalProductIdentityWorkflowOperationRepository
    : IGlobalProductIdentityWorkflowOperationRepository
{
    public const string CollectionName = "mdm_global_product_identity_workflow_operations";
    private readonly IMongoCollection<GlobalProductIdentityWorkflowOperation> _operations;
    private readonly Guid _tenantId;

    public GlobalProductIdentityWorkflowOperationRepository(
        IMongoDatabase database,
        ITenantContext tenantContext)
    {
        _operations = database.GetCollection<GlobalProductIdentityWorkflowOperation>(CollectionName);
        _tenantId = tenantContext.TenantId;
        EnsureIndexes();
    }

    public async Task<GlobalProductIdentityWorkflowReserveResult> ReserveAsync(
        GlobalProductIdentityWorkflowOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!IsValidForReservation(operation))
        {
            return new(false, false, null, "PRODUCT_IDENTITY_WORKFLOW_OPERATION_INVALID");
        }

        operation.TenantId = _tenantId;
        operation.Id = operation.Id == Guid.Empty ? Guid.NewGuid() : operation.Id;
        operation.IsDeleted = false;
        operation.DeletedAt = null;
        operation.Checkpoint = GlobalProductIdentityWorkflowCheckpoint.Prepared;
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
        operation.NextAttemptAtUtcTicksV1 = null;
        operation.LastFailureCode = null;
        operation.LeaseOwner = null;
        operation.LeaseUntilUtcTicksV1 = null;
        operation.LeaseGeneration = 0;
        operation.TemporalStorageVersion = GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion;
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
            var existing = await _operations.Find(ActiveTenantFilter & Builders<GlobalProductIdentityWorkflowOperation>
                    .Filter.Or(
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.OperationId, operation.OperationId),
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                            item => item.StartIdempotencyKey, operation.StartIdempotencyKey),
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.And(
                            Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                                item => item.GlobalProductId, operation.GlobalProductId),
                            Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                                item => item.ExpectedProductVersion, operation.ExpectedProductVersion))))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null && IsExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, null, "PRODUCT_IDENTITY_WORKFLOW_IDEMPOTENCY_CONFLICT");
        }
    }

    public Task<GlobalProductIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                             item => item.OperationId, operationId))
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task<GlobalProductIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default) =>
        _operations.Find(ActiveTenantFilter
                         & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                             item => item.StartIdempotencyKey, startIdempotencyKey))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<GlobalProductIdentityWorkflowClaim?> TryClaimAsync(
        GlobalProductIdentityWorkflowClaimRequest request,
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
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, request.OperationId)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, request.OperationFingerprint)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.In(
                         item => item.Checkpoint, request.EligibleCheckpoints)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, request.NowUtcTicks))
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, request.NowUtcTicks));
        var update = Builders<GlobalProductIdentityWorkflowOperation>.Update
            .Set(item => item.LeaseOwner, request.LeaseOwner)
            .Set(item => item.LeaseUntilUtcTicksV1, request.LeaseUntilUtcTicks)
            .Set(item => item.UpdatedAtUtcTicksV1, request.NowUtcTicks)
            .Set(item => item.UpdatedAt, new DateTimeOffset(request.NowUtcTicks, TimeSpan.Zero))
            .Inc(item => item.LeaseGeneration, 1)
            .Inc(item => item.Version, 1);
        var operation = await _operations.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<GlobalProductIdentityWorkflowOperation>
            {
                ReturnDocument = ReturnDocument.After,
                IsUpsert = false
            },
            cancellationToken);
        return operation is null
            ? null
            : new GlobalProductIdentityWorkflowClaim(
                _tenantId,
                operation.OperationId,
                operation.OperationFingerprint,
                request.LeaseOwner,
                operation.LeaseGeneration,
                operation.Checkpoint,
                request.LeaseUntilUtcTicks);
    }

    public async Task<GlobalProductIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        GlobalProductIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default)
    {
        if (nowUtcTicks <= 0 || limit is < 1 or > 100 || after?.OperationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var terminal = new[]
        {
            GlobalProductIdentityWorkflowCheckpoint.Completed,
            GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            GlobalProductIdentityWorkflowCheckpoint.Superseded
        };
        var filter = ActiveTenantFilter
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Nin(item => item.Checkpoint, terminal)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (after is not null)
        {
            filter &= after.NextAttemptAtUtcTicksV1.HasValue
                ? Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                    Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(
                        item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                    Builders<GlobalProductIdentityWorkflowOperation>.Filter.And(
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, after.NextAttemptAtUtcTicksV1),
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)))
                : Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                    Builders<GlobalProductIdentityWorkflowOperation>.Filter.Ne(
                        item => item.NextAttemptAtUtcTicksV1, null),
                    Builders<GlobalProductIdentityWorkflowOperation>.Filter.And(
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                            item => item.NextAttemptAtUtcTicksV1, null),
                        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(
                            item => item.OperationId, after.OperationId)));
        }

        var rows = await _operations.Find(filter)
            .SortBy(item => item.NextAttemptAtUtcTicksV1)
            .ThenBy(item => item.OperationId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var next = rows.Count == limit
            ? new GlobalProductIdentityWorkflowRecoveryCursor(
                rows[^1].NextAttemptAtUtcTicksV1,
                rows[^1].OperationId)
            : null;
        return new(rows, next);
    }

    public async Task<bool> AdvanceAsync(
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != _tenantId
            || mutation.UpdatedAtUtcTicks <= 0
            || !IsAllowedTransition(claim.Checkpoint, mutation.NextCheckpoint))
        {
            return false;
        }

        var filter = ActiveTenantFilter
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationId, claim.OperationId)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.OperationFingerprint, claim.OperationFingerprint)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.Checkpoint, claim.Checkpoint)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseOwner, claim.LeaseOwner)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.LeaseGeneration, claim.LeaseGeneration)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(
                         item => item.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        var update = Builders<GlobalProductIdentityWorkflowOperation>.Update
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
        if (mutation.ReleaseLease)
        {
            update = update.Set(item => item.LeaseOwner, null).Set(item => item.LeaseUntilUtcTicksV1, null);
        }

        var result = await _operations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private FilterDefinition<GlobalProductIdentityWorkflowOperation> ActiveTenantFilter =>
        Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.TenantId, _tenantId)
        & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false);

    private bool IsValidForReservation(GlobalProductIdentityWorkflowOperation operation) =>
        _tenantId != Guid.Empty
        && operation.OperationId != Guid.Empty
        && operation.GlobalProductId != Guid.Empty
        && operation.ExpectedProductVersion >= 0
        && operation.MakerSubjectId != Guid.Empty
        && (operation.WorkflowTemplateId.HasValue ^ operation.WorkflowTemplateCode is not null)
        && operation.CandidatePrincipalIds is { Count: >= 1 and <= 100 }
        && operation.CandidatePrincipalIds.All(item => item != Guid.Empty)
        && operation.CandidatePrincipalIds.Distinct().Count() == operation.CandidatePrincipalIds.Count
        && IsExactBounded(operation.ReasonCode, 128)
        && operation.ObjectType == "GlobalProduct"
        && operation.ObjectId == operation.GlobalProductId.ToString("D")
        && IsExactBounded(operation.ObjectRef, 256)
        && IsExactBounded(operation.StartIdempotencyKey, 256)
        && IsExactBounded(operation.OperationFingerprint, 256)
        && (!operation.WorkflowTemplateId.HasValue || operation.WorkflowTemplateId != Guid.Empty)
        && (operation.WorkflowTemplateCode is null || IsExactBounded(operation.WorkflowTemplateCode, 128))
        && operation.TemporalStorageVersion is 0 or GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion;

    private static bool IsExactBounded(string? value, int maximumLength) =>
        value is { Length: > 0 }
        && value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool IsAllowedTransition(
        GlobalProductIdentityWorkflowCheckpoint current,
        GlobalProductIdentityWorkflowCheckpoint next) =>
        current == next
        || (current, next) switch
        {
            (GlobalProductIdentityWorkflowCheckpoint.Prepared,
                GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown) => true,
            (GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted
                or GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted,
                GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied,
                GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                GlobalProductIdentityWorkflowCheckpoint.DecisionObserved
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.DecisionObserved,
                GlobalProductIdentityWorkflowCheckpoint.DecisionApplied
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductIdentityWorkflowCheckpoint.DecisionApplied,
                GlobalProductIdentityWorkflowCheckpoint.Completed
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired) => true,
            _ => false
        };

    private static bool IsExactReplay(
        GlobalProductIdentityWorkflowOperation existing,
        GlobalProductIdentityWorkflowOperation candidate) =>
        existing.OperationId == candidate.OperationId
        && existing.GlobalProductId == candidate.GlobalProductId
        && existing.ExpectedProductVersion == candidate.ExpectedProductVersion
        && existing.StartIdempotencyKey == candidate.StartIdempotencyKey
        && existing.OperationFingerprint == candidate.OperationFingerprint;

    private void EnsureIndexes()
    {
        var active = new BsonDocument(nameof(GlobalProductIdentityWorkflowOperation.IsDeleted), false);
        _operations.Indexes.CreateMany(
        [
            new CreateIndexModel<GlobalProductIdentityWorkflowOperation>(
                Builders<GlobalProductIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.OperationId),
                new CreateIndexOptions<GlobalProductIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_gp_identity_workflow_operation" }),
            new CreateIndexModel<GlobalProductIdentityWorkflowOperation>(
                Builders<GlobalProductIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.StartIdempotencyKey),
                new CreateIndexOptions<GlobalProductIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_gp_identity_workflow_start_key" }),
            new CreateIndexModel<GlobalProductIdentityWorkflowOperation>(
                Builders<GlobalProductIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.GlobalProductId)
                    .Ascending(item => item.ExpectedProductVersion),
                new CreateIndexOptions<GlobalProductIdentityWorkflowOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_gp_identity_workflow_product_version" }),
            new CreateIndexModel<GlobalProductIdentityWorkflowOperation>(
                Builders<GlobalProductIdentityWorkflowOperation>.IndexKeys
                    .Ascending(item => item.TenantId).Ascending(item => item.IsDeleted)
                    .Ascending(item => item.NextAttemptAtUtcTicksV1).Ascending(item => item.OperationId)
                    .Ascending(item => item.Checkpoint).Ascending(item => item.LeaseUntilUtcTicksV1)
                    .Ascending(item => item.CreatedAtUtcTicksV1),
                new CreateIndexOptions { Name = "ix_mdm_gp_identity_workflow_recovery" })
        ]);
    }
}
