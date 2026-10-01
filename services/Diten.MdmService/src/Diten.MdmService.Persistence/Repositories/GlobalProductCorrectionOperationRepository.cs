using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GlobalProductCorrectionOperationRepository(
    IMongoDatabase database,
    ITenantContext tenantContext) : IGlobalProductCorrectionOperationRepository
{
    public const string CollectionName = "mdm_global_product_correction_operations";
    private readonly IMongoCollection<GlobalProductCorrectionOperation> _operations =
        database.GetCollection<GlobalProductCorrectionOperation>(CollectionName);
    private Guid TenantId => tenantContext.TenantId;

    public async Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(
        Guid? afterTenantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var filter = Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.IsDeleted, false);
        if (afterTenantId.HasValue)
            filter &= Builders<GlobalProductCorrectionOperation>.Filter.Gt(x => x.TenantId, afterTenantId.Value);
        var rows = await _operations.Aggregate()
            .Match(filter)
            .AppendStage<BsonDocument>(new BsonDocument("$group",
                new BsonDocument("_id", $"${nameof(GlobalProductCorrectionOperation.TenantId)}")))
            .Sort(new BsonDocumentSortDefinition<BsonDocument>(new BsonDocument("_id", 1)))
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var page = rows.Select(row => row["_id"].AsGuid).ToList();
        return new(page, page.Count == limit ? page[^1] : null);
    }

    public async Task<GlobalProductCorrectionReserveResult> ReserveAsync(
        GlobalProductCorrectionOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!Valid(operation))
            return new(false, false, null, "GLOBAL_PRODUCT_CORRECTION_OPERATION_INVALID");

        operation.TenantId = TenantId;
        operation.Id = operation.OperationId;
        operation.Checkpoint = GlobalProductCorrectionCheckpoint.Prepared;
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
        operation.TemporalStorageVersion = GlobalProductCorrectionOperation.CurrentTemporalStorageVersion;
        operation.IsDeleted = false;
        operation.Version = 0;
        operation.CreatedAt = new(operation.CreatedAtUtcTicksV1, TimeSpan.Zero);
        operation.UpdatedAt = operation.CreatedAt;
        try
        {
            await EnsureIndexesAsync(cancellationToken);
            await _operations.InsertOneAsync(operation, cancellationToken: cancellationToken);
            return new(true, false, operation, null);
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _operations.Find(Active & Builders<GlobalProductCorrectionOperation>.Filter.Or(
                    Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.OperationId, operation.OperationId),
                    Builders<GlobalProductCorrectionOperation>.Filter.Eq(
                        x => x.StartIdempotencyKey, operation.StartIdempotencyKey)))
                .FirstOrDefaultAsync(cancellationToken);
            return existing is not null && ExactReplay(existing, operation)
                ? new(true, true, existing, null)
                : new(false, false, null, "GLOBAL_PRODUCT_CORRECTION_IDEMPOTENCY_CONFLICT");
        }
    }

    public Task<GlobalProductCorrectionOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        _operations.Find(Active & Builders<GlobalProductCorrectionOperation>.Filter.Eq(
                x => x.OperationId, operationId))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<GlobalProductCorrectionClaim?> TryClaimAsync(
        Guid operationId,
        string fingerprint,
        IReadOnlyCollection<GlobalProductCorrectionCheckpoint> checkpoints,
        string leaseOwner,
        long nowUtcTicks,
        long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty || string.IsNullOrWhiteSpace(fingerprint)
            || checkpoints.Count == 0 || string.IsNullOrWhiteSpace(leaseOwner)
            || leaseUntilUtcTicks <= nowUtcTicks)
            return null;
        var filter = Active
            & Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.OperationId, operationId)
            & Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.OperationFingerprint, fingerprint)
            & Builders<GlobalProductCorrectionOperation>.Filter.In(x => x.Checkpoint, checkpoints)
            & Builders<GlobalProductCorrectionOperation>.Filter.Or(
                Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.LeaseOwner, null),
                Builders<GlobalProductCorrectionOperation>.Filter.Lte(x => x.LeaseUntilUtcTicksV1, nowUtcTicks));
        var update = Builders<GlobalProductCorrectionOperation>.Update
            .Set(x => x.LeaseOwner, leaseOwner)
            .Set(x => x.LeaseUntilUtcTicksV1, leaseUntilUtcTicks)
            .Set(x => x.UpdatedAtUtcTicksV1, nowUtcTicks)
            .Set(x => x.UpdatedAt, new(nowUtcTicks, TimeSpan.Zero))
            .Inc(x => x.LeaseGeneration, 1)
            .Inc(x => x.Version, 1);
        var row = await _operations.FindOneAndUpdateAsync(filter, update,
            new() { ReturnDocument = ReturnDocument.After }, cancellationToken);
        return row is null ? null : new(TenantId, row.OperationId, row.OperationFingerprint,
            leaseOwner, row.LeaseGeneration, row.Checkpoint);
    }

    public async Task<bool> AdvanceAsync(
        GlobalProductCorrectionClaim claim,
        GlobalProductCorrectionMutation mutation,
        CancellationToken cancellationToken = default)
    {
        if (claim.TenantId != TenantId || !Allowed(claim.Checkpoint, mutation.NextCheckpoint)) return false;
        var filter = Active
            & Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.OperationId, claim.OperationId)
            & Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.OperationFingerprint, claim.OperationFingerprint)
            & Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.Checkpoint, claim.Checkpoint)
            & Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.LeaseOwner, claim.LeaseOwner)
            & Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.LeaseGeneration, claim.LeaseGeneration)
            & Builders<GlobalProductCorrectionOperation>.Filter.Gt(x => x.LeaseUntilUtcTicksV1, mutation.UpdatedAtUtcTicks);
        var update = Builders<GlobalProductCorrectionOperation>.Update
            .Set(x => x.Checkpoint, mutation.NextCheckpoint)
            .Set(x => x.RecoveryDisposition, mutation.RecoveryDisposition)
            .Set(x => x.NextAttemptAtUtcTicksV1, mutation.NextAttemptAtUtcTicksV1)
            .Set(x => x.LastFailureCode, mutation.LastFailureCode)
            .Set(x => x.UpdatedAtUtcTicksV1, mutation.UpdatedAtUtcTicks)
            .Set(x => x.UpdatedAt, new(mutation.UpdatedAtUtcTicks, TimeSpan.Zero))
            .Inc(x => x.Version, 1);
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
        var result = await _operations.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task<GlobalProductCorrectionRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        Guid? afterOperationId = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100 || nowUtcTicks <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        var terminal = new[] { GlobalProductCorrectionCheckpoint.Completed,
            GlobalProductCorrectionCheckpoint.ManualReconciliationRequired,
            GlobalProductCorrectionCheckpoint.AwaitingMakerReplay };
        var filter = Active
            & Builders<GlobalProductCorrectionOperation>.Filter.Nin(x => x.Checkpoint, terminal)
            & Builders<GlobalProductCorrectionOperation>.Filter.Or(
                Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.NextAttemptAtUtcTicksV1, null),
                Builders<GlobalProductCorrectionOperation>.Filter.Lte(x => x.NextAttemptAtUtcTicksV1, nowUtcTicks))
            & Builders<GlobalProductCorrectionOperation>.Filter.Or(
                Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.LeaseOwner, null),
                Builders<GlobalProductCorrectionOperation>.Filter.Lte(x => x.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (afterOperationId.HasValue)
            filter &= Builders<GlobalProductCorrectionOperation>.Filter.Gt(x => x.OperationId, afterOperationId.Value);
        var rows = await _operations.Find(filter).SortBy(x => x.OperationId).Limit(limit).ToListAsync(cancellationToken);
        return new(rows, rows.Count == limit ? rows[^1].OperationId : null);
    }

    private FilterDefinition<GlobalProductCorrectionOperation> Active =>
        Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.TenantId, TenantId)
        & Builders<GlobalProductCorrectionOperation>.Filter.Eq(x => x.IsDeleted, false);

    private bool Valid(GlobalProductCorrectionOperation x) => TenantId != Guid.Empty
        && x.OperationId != Guid.Empty && x.GlobalProductId != Guid.Empty && x.BaseProductVersion >= 0
        && x.MakerSubjectId != Guid.Empty && Exact(x.ProposedGlobalProductName, 200)
        && Exact(x.ProposedGlobalProductNameNormalized, 200)
        && (x.WorkflowTemplateId.HasValue ^ x.WorkflowTemplateCode is not null)
        && (x.WorkflowTemplateCode is null || Exact(x.WorkflowTemplateCode, 128))
        && x.WorkflowTemplateId != Guid.Empty && x.CandidatePrincipalIds is { Count: >= 1 and <= 100 }
        && x.CandidatePrincipalIds.All(id => id != Guid.Empty)
        && x.CandidatePrincipalIds.Distinct().Count() == x.CandidatePrincipalIds.Count
        && Exact(x.ReasonCode, 128)
        && (x.ConfiguredDueAfterSeconds.HasValue == x.DueAtUtcTicksV1.HasValue)
        && x.ConfiguredDueAfterSeconds is null or >= 60 and <= 2_592_000
        && x.ObjectType == "GlobalProductCorrection" && x.ObjectId == x.OperationId.ToString("D")
        && Exact(x.ObjectRef, 256) && Exact(x.StartIdempotencyKey, 256)
        && Exact(x.OperationFingerprint, 256) && x.CreatedAtUtcTicksV1 > 0;

    private static bool Exact(string? value, int max) => value is { Length: > 0 }
        && value.Length <= max && value == value.Trim() && !value.Any(char.IsControl);
    private static bool ExactReplay(GlobalProductCorrectionOperation a, GlobalProductCorrectionOperation b) =>
        a.OperationId == b.OperationId && a.GlobalProductId == b.GlobalProductId
        && a.BaseProductVersion == b.BaseProductVersion && a.MakerSubjectId == b.MakerSubjectId
        && a.ProposedGlobalProductName == b.ProposedGlobalProductName
        && a.ProposedGlobalProductNameNormalized == b.ProposedGlobalProductNameNormalized
        && a.WorkflowTemplateId == b.WorkflowTemplateId
        && a.WorkflowTemplateCode == b.WorkflowTemplateCode
        && a.CandidatePrincipalIds.Order().SequenceEqual(b.CandidatePrincipalIds.Order())
        && a.ReasonCode == b.ReasonCode
        && a.CommentRequired == b.CommentRequired
        && a.EvidenceRequired == b.EvidenceRequired
        && a.ConfiguredDueAfterSeconds == b.ConfiguredDueAfterSeconds
        && a.ObjectType == b.ObjectType && a.ObjectId == b.ObjectId && a.ObjectRef == b.ObjectRef
        && a.StartIdempotencyKey == b.StartIdempotencyKey
        && (a.DueAtUtcTicksV1 == b.DueAtUtcTicksV1
            ? a.OperationFingerprint == b.OperationFingerprint
            : a.ConfiguredDueAfterSeconds.HasValue
              && a.DueAtUtcTicksV1.HasValue && b.DueAtUtcTicksV1.HasValue
              && b.OperationFingerprint == GlobalProductCorrectionWorkflowStartRequestFactory.ComputeFingerprint(b));
    private static bool Allowed(GlobalProductCorrectionCheckpoint current, GlobalProductCorrectionCheckpoint next) =>
        current == next || (current, next) switch
        {
            (GlobalProductCorrectionCheckpoint.Prepared, GlobalProductCorrectionCheckpoint.StartOutcomeUnknown
                or GlobalProductCorrectionCheckpoint.WorkflowStarted
                or GlobalProductCorrectionCheckpoint.AwaitingMakerReplay
                or GlobalProductCorrectionCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductCorrectionCheckpoint.StartOutcomeUnknown, GlobalProductCorrectionCheckpoint.WorkflowStarted
                or GlobalProductCorrectionCheckpoint.AwaitingMakerReplay
                or GlobalProductCorrectionCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductCorrectionCheckpoint.AwaitingMakerReplay, GlobalProductCorrectionCheckpoint.WorkflowStarted
                or GlobalProductCorrectionCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductCorrectionCheckpoint.WorkflowStarted, GlobalProductCorrectionCheckpoint.AwaitingDecision
                or GlobalProductCorrectionCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductCorrectionCheckpoint.AwaitingDecision, GlobalProductCorrectionCheckpoint.DecisionObserved
                or GlobalProductCorrectionCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductCorrectionCheckpoint.DecisionObserved, GlobalProductCorrectionCheckpoint.DecisionApplied
                or GlobalProductCorrectionCheckpoint.ManualReconciliationRequired) => true,
            (GlobalProductCorrectionCheckpoint.DecisionApplied, GlobalProductCorrectionCheckpoint.Completed
                or GlobalProductCorrectionCheckpoint.ManualReconciliationRequired) => true,
            _ => false
        };

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var active = new BsonDocument(nameof(GlobalProductCorrectionOperation.IsDeleted), false);
        await _operations.Indexes.CreateManyAsync([
            new(Builders<GlobalProductCorrectionOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OperationId),
                new CreateIndexOptions<GlobalProductCorrectionOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_gp_correction_operation" }),
            new(Builders<GlobalProductCorrectionOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.StartIdempotencyKey),
                new CreateIndexOptions<GlobalProductCorrectionOperation> { Unique = true, PartialFilterExpression = active,
                    Name = "ux_mdm_gp_correction_start_key" }),
            new(Builders<GlobalProductCorrectionOperation>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.IsDeleted).Ascending(x => x.NextAttemptAtUtcTicksV1)
                    .Ascending(x => x.OperationId).Ascending(x => x.Checkpoint).Ascending(x => x.LeaseUntilUtcTicksV1),
                new CreateIndexOptions { Name = "ix_mdm_gp_correction_recovery" })
        ], cancellationToken);
    }
}
