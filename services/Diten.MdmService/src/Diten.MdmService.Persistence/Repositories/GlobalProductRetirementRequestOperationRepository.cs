using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using System.Text;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GlobalProductRetirementRequestOperationRepository(
    IMongoDatabase database, ITenantContext tenantContext)
    : IGlobalProductRetirementRequestOperationRepository
{
    public const string CollectionName = "mdm_global_product_retirement_request_operations";
    private readonly IMongoCollection<GlobalProductRetirementRequestOperation> _operations =
        database.GetCollection<GlobalProductRetirementRequestOperation>(CollectionName);
    private Guid TenantId => tenantContext.TenantId;

    private FilterDefinition<GlobalProductRetirementRequestOperation> Active =>
        Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.TenantId, TenantId)
        & Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.IsDeleted, false);

    public async Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(
        Guid? afterTenantId, int limit, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var filter = Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.IsDeleted, false);
        if (afterTenantId.HasValue)
            filter &= Builders<GlobalProductRetirementRequestOperation>.Filter.Gt(x => x.TenantId, afterTenantId);
        var rows = await _operations.Aggregate().Match(filter)
            .AppendStage<BsonDocument>(new BsonDocument("$group",
                new BsonDocument("_id", $"${nameof(GlobalProductRetirementRequestOperation.TenantId)}")))
            .Sort(new BsonDocumentSortDefinition<BsonDocument>(new BsonDocument("_id", 1)))
            .Limit(limit).ToListAsync(cancellationToken);
        var ids = rows.Select(x => x["_id"].AsGuid).ToList();
        return new(ids, ids.Count == limit ? ids[^1] : null);
    }

    public async Task<GlobalProductRetirementRequestReserveResult> ReserveAsync(
        GlobalProductRetirementRequestOperation operation, CancellationToken cancellationToken = default)
    {
        if (!Valid(operation)) return new(false, false, null, "GLOBAL_PRODUCT_RETIREMENT_OPERATION_INVALID");
        operation.TenantId = TenantId; operation.Id = operation.OperationId; operation.IsDeleted = false;
        operation.Checkpoint = GlobalProductRetirementRequestCheckpoint.Prepared;
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
        operation.CreatedAt = new(operation.CreatedAtUtcTicksV1, TimeSpan.Zero);
        operation.UpdatedAt = operation.CreatedAt; operation.Version = 0;
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            await _operations.InsertOneAsync(operation, cancellationToken: cancellationToken);
            return new(true, false, operation, null);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _operations.Find(Active & Builders<GlobalProductRetirementRequestOperation>.Filter.Or(
                Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.OperationId, operation.OperationId),
                Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.StartIdempotencyKey,
                    operation.StartIdempotencyKey))).FirstOrDefaultAsync(cancellationToken);
            return existing is not null && ExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, null, "GLOBAL_PRODUCT_RETIREMENT_IDEMPOTENCY_CONFLICT");
        }
    }

    public Task<GlobalProductRetirementRequestOperation?> GetByOperationIdAsync(Guid operationId,
        CancellationToken cancellationToken = default) => _operations.Find(Active
        & Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.OperationId, operationId))
        .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<GlobalProductRetirementRequestClaim?> TryClaimAsync(Guid operationId, string fingerprint,
        IReadOnlyCollection<GlobalProductRetirementRequestCheckpoint> checkpoints, string leaseOwner,
        long nowUtcTicks, long leaseUntilUtcTicks, CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty || string.IsNullOrWhiteSpace(fingerprint) || checkpoints.Count == 0
            || string.IsNullOrWhiteSpace(leaseOwner) || leaseUntilUtcTicks <= nowUtcTicks) return null;
        var filter = Active
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.OperationId, operationId)
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.OperationFingerprint, fingerprint)
            & Builders<GlobalProductRetirementRequestOperation>.Filter.In(x => x.Checkpoint, checkpoints)
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Or(
                Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.NextAttemptAtUtcTicksV1, null),
                Builders<GlobalProductRetirementRequestOperation>.Filter.Lte(x => x.NextAttemptAtUtcTicksV1, nowUtcTicks))
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Or(
                Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.LeaseOwner, null),
                Builders<GlobalProductRetirementRequestOperation>.Filter.Lte(x => x.LeaseUntilUtcTicksV1, nowUtcTicks));
        var update = Builders<GlobalProductRetirementRequestOperation>.Update.Set(x => x.LeaseOwner, leaseOwner)
            .Set(x => x.LeaseUntilUtcTicksV1, leaseUntilUtcTicks).Set(x => x.UpdatedAtUtcTicksV1, nowUtcTicks)
            .Set(x => x.UpdatedAt, new DateTimeOffset(nowUtcTicks, TimeSpan.Zero))
            .Inc(x => x.LeaseGeneration, 1).Inc(x => x.Version, 1);
        var row = await _operations.FindOneAndUpdateAsync(filter, update,
            new() { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return row is null ? null : new(TenantId, row.OperationId, row.OperationFingerprint,
            leaseOwner, row.LeaseGeneration, row.Checkpoint);
    }

    public async Task<bool> AdvanceAsync(GlobalProductRetirementRequestClaim claim,
        GlobalProductRetirementRequestMutation mutation, CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != TenantId || !Allowed(claim.Checkpoint, mutation.NextCheckpoint)) return false;
        var filter = Active
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.OperationId, claim.OperationId)
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.OperationFingerprint, claim.OperationFingerprint)
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.Checkpoint, claim.Checkpoint)
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.LeaseOwner, claim.LeaseOwner)
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.LeaseGeneration, claim.LeaseGeneration)
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Gt(x => x.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        var update = Builders<GlobalProductRetirementRequestOperation>.Update
            .Set(x => x.Checkpoint, mutation.NextCheckpoint).Set(x => x.RecoveryDisposition, mutation.RecoveryDisposition)
            .Set(x => x.NextAttemptAtUtcTicksV1, mutation.NextAttemptAtUtcTicksV1)
            .Set(x => x.LastFailureCode, mutation.LastFailureCode).Set(x => x.UpdatedAtUtcTicksV1, mutation.UpdatedAtUtcTicks)
            .Set(x => x.UpdatedAt, new DateTimeOffset(mutation.UpdatedAtUtcTicks, TimeSpan.Zero)).Inc(x => x.Version, 1);
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
        return (await _operations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken)).ModifiedCount == 1;
    }

    public async Task<GlobalProductRetirementRequestRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks,
        int limit, Guid? afterOperationId = null, CancellationToken cancellationToken = default)
    {
        if (nowUtcTicks <= 0 || limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var terminal = new[] { GlobalProductRetirementRequestCheckpoint.Completed,
            GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired,
            GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay };
        var filter = Active & Builders<GlobalProductRetirementRequestOperation>.Filter.Nin(x => x.Checkpoint, terminal)
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Or(
                Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.NextAttemptAtUtcTicksV1, null),
                Builders<GlobalProductRetirementRequestOperation>.Filter.Lte(x => x.NextAttemptAtUtcTicksV1, nowUtcTicks))
            & Builders<GlobalProductRetirementRequestOperation>.Filter.Or(
                Builders<GlobalProductRetirementRequestOperation>.Filter.Eq(x => x.LeaseOwner, null),
                Builders<GlobalProductRetirementRequestOperation>.Filter.Lte(x => x.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (afterOperationId.HasValue)
            filter &= Builders<GlobalProductRetirementRequestOperation>.Filter.Gt(x => x.OperationId, afterOperationId);
        var rows = await _operations.Find(filter).SortBy(x => x.OperationId).Limit(limit).ToListAsync(cancellationToken);
        return new(rows, rows.Count == limit ? rows[^1].OperationId : null);
    }

    private bool Valid(GlobalProductRetirementRequestOperation x) => TenantId != Guid.Empty
        && x.OperationId != Guid.Empty && x.GlobalProductId != Guid.Empty && x.BaseProductVersion >= 0
        && x.MakerSubjectId != Guid.Empty && ExactReason(x.RequestReason)
        && (x.WorkflowTemplateId.HasValue ^ x.WorkflowTemplateCode is not null)
        && (x.WorkflowTemplateCode is null || Exact(x.WorkflowTemplateCode, 128))
        && x.CandidatePrincipalIds is { Count: >= 1 and <= 100 } && x.CandidatePrincipalIds.All(id => id != Guid.Empty)
        && x.CandidatePrincipalIds.Distinct().Count() == x.CandidatePrincipalIds.Count
        && Exact(x.ReasonCode, 128)
        && (x.ConfiguredDueAfterSeconds.HasValue == x.DueAtUtcTicksV1.HasValue)
        && x.ConfiguredDueAfterSeconds is null or >= 60 and <= 2_592_000
        && x.ObjectType == "GlobalProductRetirement" && x.ObjectId == x.OperationId.ToString("D")
        && Exact(x.ObjectRef, 256) && Exact(x.StartIdempotencyKey, 256)
        && Exact(x.OperationFingerprint, 256) && x.CreatedAtUtcTicksV1 > 0;
    private static bool ExactReplay(GlobalProductRetirementRequestOperation a, GlobalProductRetirementRequestOperation b) =>
        a.OperationId == b.OperationId && a.GlobalProductId == b.GlobalProductId
        && a.BaseProductVersion == b.BaseProductVersion && a.MakerSubjectId == b.MakerSubjectId
        && a.RequestReason == b.RequestReason
        && a.WorkflowTemplateId == b.WorkflowTemplateId && a.WorkflowTemplateCode == b.WorkflowTemplateCode
        && a.CandidatePrincipalIds.SequenceEqual(b.CandidatePrincipalIds)
        && a.ReasonCode == b.ReasonCode && a.CommentRequired == b.CommentRequired
        && a.EvidenceRequired == b.EvidenceRequired
        && a.ConfiguredDueAfterSeconds == b.ConfiguredDueAfterSeconds
        && a.ObjectType == b.ObjectType && a.ObjectId == b.ObjectId && a.ObjectRef == b.ObjectRef
        && a.StartIdempotencyKey == b.StartIdempotencyKey
        && (a.DueAtUtcTicksV1 == b.DueAtUtcTicksV1
            ? a.OperationFingerprint == b.OperationFingerprint
            : a.ConfiguredDueAfterSeconds.HasValue
              && a.DueAtUtcTicksV1.HasValue && b.DueAtUtcTicksV1.HasValue
              && b.OperationFingerprint == GlobalProductRetirementRequestWorkflowStartRequestFactory.ComputeFingerprint(b));
    private static bool Exact(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && value == value.Trim() && !value.Any(char.IsControl);
    private static bool ExactReason(string? value) => value is not null && value == value.Trim()
        && value.EnumerateRunes().Count() is >= 1 and <= 2000 && !value.Any(char.IsControl);
    private static bool Allowed(GlobalProductRetirementRequestCheckpoint current,
        GlobalProductRetirementRequestCheckpoint next) => current == next || (current, next) switch
    {
        (GlobalProductRetirementRequestCheckpoint.Prepared, GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown) => true,
        (GlobalProductRetirementRequestCheckpoint.Prepared, GlobalProductRetirementRequestCheckpoint.WorkflowStarted) => true,
        (GlobalProductRetirementRequestCheckpoint.Prepared, GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay) => true,
        (GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown, GlobalProductRetirementRequestCheckpoint.WorkflowStarted) => true,
        (GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay, GlobalProductRetirementRequestCheckpoint.WorkflowStarted) => true,
        (GlobalProductRetirementRequestCheckpoint.WorkflowStarted, GlobalProductRetirementRequestCheckpoint.AwaitingDecision) => true,
        (GlobalProductRetirementRequestCheckpoint.AwaitingDecision, GlobalProductRetirementRequestCheckpoint.DecisionObserved) => true,
        (GlobalProductRetirementRequestCheckpoint.DecisionObserved, GlobalProductRetirementRequestCheckpoint.DecisionApplied) => true,
        (GlobalProductRetirementRequestCheckpoint.DecisionApplied, GlobalProductRetirementRequestCheckpoint.Completed) => true,
        (_, GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired) => true,
        _ => false
    };

    private Task EnsureIndexesAsync(CancellationToken cancellationToken) => _operations.Indexes.CreateManyAsync([
        new(Builders<GlobalProductRetirementRequestOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OperationId),
            new() { Unique = true, Name = "ux_gp_retirement_tenant_operation" }),
        new(Builders<GlobalProductRetirementRequestOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.StartIdempotencyKey),
            new() { Unique = true, Name = "ux_gp_retirement_tenant_idempotency" }),
        new(Builders<GlobalProductRetirementRequestOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.NextAttemptAtUtcTicksV1).Ascending(x => x.OperationId),
            new() { Name = "ix_gp_retirement_tenant_recovery" })], cancellationToken);
}
