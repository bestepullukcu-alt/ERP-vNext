using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GskuRetirementRequestOperationRepository(IMongoDatabase database, ITenantContext tenantContext)
    : IGskuRetirementRequestOperationRepository
{
    public const string CollectionName = "mdm_gsku_retirement_request_operations";
    private readonly IMongoCollection<GskuRetirementRequestOperation> rows =
        database.GetCollection<GskuRetirementRequestOperation>(CollectionName);
    private Guid TenantId => tenantContext.TenantId;
    private FilterDefinition<GskuRetirementRequestOperation> Active =>
        Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.TenantId, TenantId)
        & Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.IsDeleted, false);

    public async Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(
        Guid? afterTenantId, int limit, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var filter = Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.IsDeleted, false);
        if (afterTenantId.HasValue) filter &= Builders<GskuRetirementRequestOperation>.Filter.Gt(x => x.TenantId, afterTenantId);
        var found = await rows.Aggregate().Match(filter).AppendStage<BsonDocument>(new BsonDocument("$group",
            new BsonDocument("_id", $"${nameof(GskuRetirementRequestOperation.TenantId)}")))
            .Sort(new BsonDocumentSortDefinition<BsonDocument>(new BsonDocument("_id", 1))).Limit(limit)
            .ToListAsync(cancellationToken);
        var ids = found.Select(x => x["_id"].AsGuid).ToArray();
        return new(ids, ids.Length == limit ? ids[^1] : null);
    }

    public async Task<GskuRetirementRequestWorkflowReserveResult> ReserveAsync(
        GskuRetirementRequestOperation operation, CancellationToken cancellationToken = default)
    {
        if (!Valid(operation)) return new(false, false, null, "GSKU_RETIREMENT_REQUEST_OPERATION_INVALID");
        operation.TenantId = TenantId; operation.Id = operation.OperationId; operation.IsDeleted = false;
        operation.Checkpoint = GskuRetirementRequestCheckpoint.Prepared;
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
        operation.TemporalStorageVersion = GskuRetirementRequestOperation.CurrentTemporalStorageVersion;
        operation.CreatedAt = new(operation.CreatedAtUtcTicksV1, TimeSpan.Zero); operation.UpdatedAt = operation.CreatedAt;
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            await rows.InsertOneAsync(operation, cancellationToken: cancellationToken);
            return new(true, false, operation, null);
        }
        catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await rows.Find(Active & Builders<GskuRetirementRequestOperation>.Filter.Or(
                Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.OperationId, operation.OperationId),
                Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.StartIdempotencyKey, operation.StartIdempotencyKey)))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null && ExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, null, "GSKU_RETIREMENT_REQUEST_IDEMPOTENCY_CONFLICT");
        }
    }

    public Task<GskuRetirementRequestOperation?> GetByOperationIdAsync(Guid operationId,
        CancellationToken cancellationToken = default) => rows.Find(Active
        & Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.OperationId, operationId))
        .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<GskuRetirementRequestWorkflowClaim?> TryClaimAsync(Guid operationId, string fingerprint,
        IReadOnlyCollection<GskuRetirementRequestCheckpoint> checkpoints, string leaseOwner,
        long nowUtcTicks, long leaseUntilUtcTicks, CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty || string.IsNullOrWhiteSpace(fingerprint) || checkpoints.Count == 0
            || string.IsNullOrWhiteSpace(leaseOwner) || leaseUntilUtcTicks <= nowUtcTicks) return null;
        var filter = Active & Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.OperationId, operationId)
            & Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.OperationFingerprint, fingerprint)
            & Builders<GskuRetirementRequestOperation>.Filter.In(x => x.Checkpoint, checkpoints)
            & Builders<GskuRetirementRequestOperation>.Filter.Or(
                Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.LeaseOwner, null),
                Builders<GskuRetirementRequestOperation>.Filter.Lte(x => x.LeaseUntilUtcTicksV1, nowUtcTicks));
        var updated = await rows.FindOneAndUpdateAsync(filter,
            Builders<GskuRetirementRequestOperation>.Update.Set(x => x.LeaseOwner, leaseOwner)
                .Set(x => x.LeaseUntilUtcTicksV1, leaseUntilUtcTicks).Set(x => x.UpdatedAtUtcTicksV1, nowUtcTicks)
                .Set(x => x.UpdatedAt, new(nowUtcTicks, TimeSpan.Zero)).Inc(x => x.LeaseGeneration, 1).Inc(x => x.Version, 1),
            new() { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return updated is null ? null : new(TenantId, updated.OperationId, updated.OperationFingerprint,
            leaseOwner, updated.LeaseGeneration, updated.Checkpoint);
    }

    public async Task<bool> AdvanceAsync(GskuRetirementRequestWorkflowClaim claim,
        GskuRetirementRequestWorkflowMutation mutation, CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != TenantId || !Allowed(claim.Checkpoint, mutation.NextCheckpoint)) return false;
        var filter = Active & Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.OperationId, claim.OperationId)
            & Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.OperationFingerprint, claim.OperationFingerprint)
            & Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.Checkpoint, claim.Checkpoint)
            & Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.LeaseOwner, claim.LeaseOwner)
            & Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.LeaseGeneration, claim.LeaseGeneration)
            & Builders<GskuRetirementRequestOperation>.Filter.Gt(x => x.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        var update = Builders<GskuRetirementRequestOperation>.Update.Set(x => x.Checkpoint, mutation.NextCheckpoint)
            .Set(x => x.RecoveryDisposition, mutation.RecoveryDisposition).Set(x => x.NextAttemptAtUtcTicksV1, mutation.NextAttemptAtUtcTicksV1)
            .Set(x => x.LastFailureCode, mutation.LastFailureCode).Set(x => x.UpdatedAtUtcTicksV1, mutation.UpdatedAtUtcTicks)
            .Set(x => x.UpdatedAt, new(mutation.UpdatedAtUtcTicks, TimeSpan.Zero)).Inc(x => x.Version, 1);
        if (mutation.WorkflowInstanceId.HasValue) update = update.Set(x => x.WorkflowInstanceId, mutation.WorkflowInstanceId);
        if (mutation.WorkflowTemplateId.HasValue) update = update.Set(x => x.WorkflowTemplateId, mutation.WorkflowTemplateId);
        if (mutation.WorkflowTemplateVersionId.HasValue) update = update.Set(x => x.WorkflowTemplateVersionId, mutation.WorkflowTemplateVersionId);
        if (mutation.ApprovalTaskId.HasValue) update = update.Set(x => x.ApprovalTaskId, mutation.ApprovalTaskId);
        if (mutation.AssignmentSnapshotId.HasValue) update = update.Set(x => x.AssignmentSnapshotId, mutation.AssignmentSnapshotId);
        if (mutation.StartTransitionLogId.HasValue) update = update.Set(x => x.StartTransitionLogId, mutation.StartTransitionLogId);
        if (mutation.WorkflowStartedAtUtcTicksV1.HasValue) update = update.Set(x => x.WorkflowStartedAtUtcTicksV1, mutation.WorkflowStartedAtUtcTicksV1);
        if (mutation.DecisionKind.HasValue) update = update.Set(x => x.DecisionKind, mutation.DecisionKind);
        if (mutation.DecisionActorSubjectId.HasValue) update = update.Set(x => x.DecisionActorSubjectId, mutation.DecisionActorSubjectId);
        if (mutation.DecisionReasonCode is not null) update = update.Set(x => x.DecisionReasonCode, mutation.DecisionReasonCode);
        if (mutation.DecisionAtUtcTicksV1.HasValue) update = update.Set(x => x.DecisionAtUtcTicksV1, mutation.DecisionAtUtcTicksV1);
        if (mutation.DecisionTransitionSequence.HasValue) update = update.Set(x => x.DecisionTransitionSequence, mutation.DecisionTransitionSequence);
        if (mutation.DecisionTaskStatus is not null) update = update.Set(x => x.DecisionTaskStatus, mutation.DecisionTaskStatus);
        if (mutation.DecisionInstanceStatus is not null) update = update.Set(x => x.DecisionInstanceStatus, mutation.DecisionInstanceStatus);
        if (mutation.ReleaseLease) update = update.Set(x => x.LeaseOwner, null).Set(x => x.LeaseUntilUtcTicksV1, null);
        return (await rows.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }

    public async Task<GskuRetirementRequestWorkflowRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks, int limit,
        Guid? afterOperationId = null, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100 || nowUtcTicks <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        var filter = Active & Builders<GskuRetirementRequestOperation>.Filter.Nin(x => x.Checkpoint,
            new[] { GskuRetirementRequestCheckpoint.Completed, GskuRetirementRequestCheckpoint.ManualReconciliationRequired,
                GskuRetirementRequestCheckpoint.AwaitingMakerReplay })
            & Builders<GskuRetirementRequestOperation>.Filter.Or(
                Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.NextAttemptAtUtcTicksV1, null),
                Builders<GskuRetirementRequestOperation>.Filter.Lte(x => x.NextAttemptAtUtcTicksV1, nowUtcTicks))
            & Builders<GskuRetirementRequestOperation>.Filter.Or(
                Builders<GskuRetirementRequestOperation>.Filter.Eq(x => x.LeaseOwner, null),
                Builders<GskuRetirementRequestOperation>.Filter.Lte(x => x.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (afterOperationId.HasValue) filter &= Builders<GskuRetirementRequestOperation>.Filter.Gt(x => x.OperationId, afterOperationId);
        var found = await rows.Find(filter).SortBy(x => x.OperationId).Limit(limit).ToListAsync(cancellationToken);
        return new(found, found.Count == limit ? found[^1].OperationId : null);
    }

    private bool Valid(GskuRetirementRequestOperation x) => TenantId != Guid.Empty && x.OperationId != Guid.Empty
        && x.GskuId != Guid.Empty && x.ProductDefinitionRevisionId != Guid.Empty && x.GlobalProductId != Guid.Empty
        && x.BaseGskuVersion >= 0 && x.BaseRevisionVersion >= 0 && x.MakerSubjectId != Guid.Empty
        && GskuRetirementRequestWorkflowStartRequestFactory.HasValidReason(x.RequestReason)
        && x.ObjectType == GskuRetirementRequestWorkflowStartRequestFactory.ObjectType
        && x.ObjectId == x.OperationId.ToString("D") && Exact(x.ObjectRef, 256)
        && Exact(x.StartIdempotencyKey, 256) && Exact(x.OperationFingerprint, 256)
        && x.CandidatePrincipalIds.Count is >= 1 and <= 100 && x.CandidatePrincipalIds.All(v => v != Guid.Empty)
        && x.CandidatePrincipalIds.Distinct().Count() == x.CandidatePrincipalIds.Count && Exact(x.ReasonCode, 128)
        && (x.WorkflowTemplateId.HasValue ^ x.WorkflowTemplateCode is not null)
        && x.WorkflowTemplateId != Guid.Empty && (x.WorkflowTemplateCode is null || Exact(x.WorkflowTemplateCode, 128))
        && (x.ConfiguredDueAfterSeconds.HasValue == x.DueAtUtcTicksV1.HasValue)
        && x.ConfiguredDueAfterSeconds is null or >= 60 and <= 2_592_000
        && x.CreatedAtUtcTicksV1 > 0;
    private static bool ExactReplay(GskuRetirementRequestOperation a, GskuRetirementRequestOperation b) =>
        a.OperationId == b.OperationId && a.GskuId == b.GskuId && a.ProductDefinitionRevisionId == b.ProductDefinitionRevisionId
        && a.GlobalProductId == b.GlobalProductId && a.BaseGskuVersion == b.BaseGskuVersion
        && a.BaseRevisionVersion == b.BaseRevisionVersion && a.MakerSubjectId == b.MakerSubjectId
        && a.RequestReason == b.RequestReason
        && a.WorkflowTemplateId == b.WorkflowTemplateId && a.WorkflowTemplateCode == b.WorkflowTemplateCode
        && a.CandidatePrincipalIds.Order().SequenceEqual(b.CandidatePrincipalIds.Order())
        && a.ReasonCode == b.ReasonCode && a.CommentRequired == b.CommentRequired
        && a.EvidenceRequired == b.EvidenceRequired
        && a.ConfiguredDueAfterSeconds == b.ConfiguredDueAfterSeconds
        && a.ObjectType == b.ObjectType && a.ObjectId == b.ObjectId && a.ObjectRef == b.ObjectRef
        && a.StartIdempotencyKey == b.StartIdempotencyKey
        && (a.DueAtUtcTicksV1 == b.DueAtUtcTicksV1
            ? a.OperationFingerprint == b.OperationFingerprint
            : a.ConfiguredDueAfterSeconds.HasValue && a.DueAtUtcTicksV1.HasValue && b.DueAtUtcTicksV1.HasValue
              && b.OperationFingerprint == GskuRetirementRequestWorkflowStartRequestFactory.ComputeFingerprint(b));
    private static bool Allowed(GskuRetirementRequestCheckpoint a, GskuRetirementRequestCheckpoint b) => a == b || (a, b) switch
    {
        (GskuRetirementRequestCheckpoint.Prepared, GskuRetirementRequestCheckpoint.WorkflowStarted or GskuRetirementRequestCheckpoint.StartOutcomeUnknown or GskuRetirementRequestCheckpoint.AwaitingMakerReplay or GskuRetirementRequestCheckpoint.ManualReconciliationRequired) => true,
        (GskuRetirementRequestCheckpoint.StartOutcomeUnknown, GskuRetirementRequestCheckpoint.WorkflowStarted or GskuRetirementRequestCheckpoint.AwaitingMakerReplay or GskuRetirementRequestCheckpoint.ManualReconciliationRequired) => true,
        (GskuRetirementRequestCheckpoint.AwaitingMakerReplay, GskuRetirementRequestCheckpoint.WorkflowStarted or GskuRetirementRequestCheckpoint.ManualReconciliationRequired) => true,
        (GskuRetirementRequestCheckpoint.WorkflowStarted, GskuRetirementRequestCheckpoint.AwaitingDecision or GskuRetirementRequestCheckpoint.ManualReconciliationRequired) => true,
        (GskuRetirementRequestCheckpoint.AwaitingDecision, GskuRetirementRequestCheckpoint.DecisionObserved or GskuRetirementRequestCheckpoint.ManualReconciliationRequired) => true,
        (GskuRetirementRequestCheckpoint.DecisionObserved, GskuRetirementRequestCheckpoint.DecisionApplied or GskuRetirementRequestCheckpoint.ManualReconciliationRequired) => true,
        (GskuRetirementRequestCheckpoint.DecisionApplied, GskuRetirementRequestCheckpoint.Completed or GskuRetirementRequestCheckpoint.ManualReconciliationRequired) => true,
        _ => false
    };
    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        var active = new BsonDocument(nameof(GskuRetirementRequestOperation.IsDeleted), false);
        await rows.Indexes.CreateManyAsync([
            new(Builders<GskuRetirementRequestOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OperationId),
                new CreateIndexOptions<GskuRetirementRequestOperation> { Unique = true, PartialFilterExpression = active, Name = "ux_mdm_gsku_retirement_request_operation" }),
            new(Builders<GskuRetirementRequestOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.StartIdempotencyKey),
                new CreateIndexOptions<GskuRetirementRequestOperation> { Unique = true, PartialFilterExpression = active, Name = "ux_mdm_gsku_retirement_request_start_key" }),
            new(Builders<GskuRetirementRequestOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IsDeleted)
                .Ascending(x => x.NextAttemptAtUtcTicksV1).Ascending(x => x.OperationId), new CreateIndexOptions { Name = "ix_mdm_gsku_retirement_request_recovery" })
        ], ct);
    }
    private static bool Exact(string? x, int max) => x is { Length: > 0 } && x.Length <= max && x == x.Trim() && !x.Any(char.IsControl);
}
