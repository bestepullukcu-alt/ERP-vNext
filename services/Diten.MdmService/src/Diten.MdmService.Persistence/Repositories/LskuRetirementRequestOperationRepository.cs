using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class LskuRetirementRequestOperationRepository : ILskuRetirementRequestOperationRepository
{
    public const string CollectionName = "mdm_lsku_retirement_request_operations";
    private readonly IMongoCollection<LskuRetirementRequestOperation> _items;
    private readonly ITenantContext _tenant;
    private Guid TenantId => _tenant.TenantId;
    private FilterDefinition<LskuRetirementRequestOperation> Active =>
        Builders<LskuRetirementRequestOperation>.Filter.Eq(x => x.TenantId, TenantId)
        & Builders<LskuRetirementRequestOperation>.Filter.Eq(x => x.IsDeleted, false);

    public LskuRetirementRequestOperationRepository(IMongoDatabase database, ITenantContext tenant)
    {
        _tenant = tenant;
        _items = database.GetCollection<LskuRetirementRequestOperation>(CollectionName);
        var active = new BsonDocument(nameof(EntityBase.IsDeleted), false);
        var activeOperation = new BsonDocument
        {
            { nameof(EntityBase.IsDeleted), false },
            { nameof(LskuRetirementRequestOperation.IsActive), true }
        };
        _items.Indexes.CreateMany([
            new(Builders<LskuRetirementRequestOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OperationId),
                new CreateIndexOptions<LskuRetirementRequestOperation>{Unique=true,PartialFilterExpression=active,Name="ux_lsku_retirement_operation"}),
            new(Builders<LskuRetirementRequestOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LskuId),
                new CreateIndexOptions<LskuRetirementRequestOperation>{Unique=true,PartialFilterExpression=activeOperation,Name="ux_lsku_active_retirement"}),
            new(Builders<LskuRetirementRequestOperation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.NextAttemptAtUtcTicksV1).Ascending(x => x.OperationId),
                new CreateIndexOptions<LskuRetirementRequestOperation>{Name="ix_lsku_retirement_recovery"})]);
    }

    public async Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(
        Guid? afterTenantId, int limit, CancellationToken ct = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var filter = Builders<LskuRetirementRequestOperation>.Filter.Eq(x => x.IsDeleted, false);
        if (afterTenantId.HasValue)
            filter &= Builders<LskuRetirementRequestOperation>.Filter.Gt(x => x.TenantId, afterTenantId.Value);
        var rows = await _items.Aggregate().Match(filter)
            .AppendStage<BsonDocument>(new BsonDocument("$group", new BsonDocument("_id", "$TenantId")))
            .Sort(new BsonDocumentSortDefinition<BsonDocument>(new BsonDocument("_id", 1)))
            .Limit(limit).ToListAsync(ct);
        var ids = rows.Select(x => x["_id"].AsGuid).ToArray();
        return new(ids, ids.Length == limit ? ids[^1] : null);
    }

    public async Task<LskuRetirementRequestReserveResult> ReserveAsync(LskuRetirementRequestOperation x,
        CancellationToken ct = default)
    {
        if (!Valid(x))
            return new(false, false, null, "LSKU_RETIREMENT_OPERATION_INVALID");
        x.TenantId=TenantId; x.Id=x.OperationId; x.Checkpoint=LskuRetirementRequestCheckpoint.Prepared;
        x.RecoveryDisposition=ProductIdentityWorkflowRecoveryDisposition.None; x.IsActive=true;
        x.CreatedAtUtcTicksV1=x.CreatedAtUtcTicksV1>0?x.CreatedAtUtcTicksV1:DateTimeOffset.UtcNow.UtcTicks;
        x.UpdatedAtUtcTicksV1=x.CreatedAtUtcTicksV1; x.CreatedAt=new(x.CreatedAtUtcTicksV1,TimeSpan.Zero); x.UpdatedAt=x.CreatedAt;
        try { await _items.InsertOneAsync(x,cancellationToken:ct); return new(true,false,x,null); }
        catch(MongoWriteException e) when(e.WriteError?.Category==ServerErrorCategory.DuplicateKey)
        {
            var old=await _items.Find(Active & Builders<LskuRetirementRequestOperation>.Filter.Or(
                Builders<LskuRetirementRequestOperation>.Filter.Eq(y=>y.OperationId,x.OperationId),
                Builders<LskuRetirementRequestOperation>.Filter.Eq(y=>y.LskuId,x.LskuId))).FirstOrDefaultAsync(ct);
            return old is not null && ExactReplay(old,x)
                ? new(true,true,old,null):new(false,false,null,"LSKU_RETIREMENT_OPERATION_CONFLICT");
        }
    }

    public Task<LskuRetirementRequestOperation?> GetByOperationIdAsync(Guid id,CancellationToken ct=default)=>
        _items.Find(Active & Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.OperationId,id)).FirstOrDefaultAsync(ct)!;

    public async Task<LskuRetirementRequestClaim?> TryClaimAsync(Guid id,string fingerprint,
        IReadOnlyCollection<LskuRetirementRequestCheckpoint> checkpoints,string owner,long now,long until,CancellationToken ct=default)
    {
        if (id == Guid.Empty || fingerprint.Length != 64 || checkpoints.Count == 0
            || string.IsNullOrWhiteSpace(owner) || until <= now) return null;
        var filter=Active & Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.OperationId,id)
            & Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.OperationFingerprint,fingerprint)
            & Builders<LskuRetirementRequestOperation>.Filter.In(x=>x.Checkpoint,checkpoints)
            & Builders<LskuRetirementRequestOperation>.Filter.Or(
                Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.NextAttemptAtUtcTicksV1,null),
                Builders<LskuRetirementRequestOperation>.Filter.Lte(x=>x.NextAttemptAtUtcTicksV1,now))
            & Builders<LskuRetirementRequestOperation>.Filter.Or(
                Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.LeaseOwner,null),
                Builders<LskuRetirementRequestOperation>.Filter.Lte(x=>x.LeaseUntilUtcTicksV1,now));
        var row=await _items.FindOneAndUpdateAsync(filter,Builders<LskuRetirementRequestOperation>.Update
            .Set(x=>x.LeaseOwner,owner).Set(x=>x.LeaseUntilUtcTicksV1,until).Inc(x=>x.LeaseGeneration,1),
            new(){ReturnDocument=ReturnDocument.After},ct);
        return row is null?null:new(row.TenantId,row.OperationId,row.OperationFingerprint,owner,row.LeaseGeneration,row.Checkpoint,until);
    }

    public async Task<bool> AdvanceAsync(LskuRetirementRequestClaim claim,LskuRetirementRequestMutation m,CancellationToken ct=default)
    {
        if (claim.TenantId != TenantId || m.UpdatedAtUtcTicks <= 0
            || claim.LeaseUntilUtcTicksV1 <= m.UpdatedAtUtcTicks
            || !Allowed(claim.Checkpoint, m.NextCheckpoint)) return false;
        var filter=Active & Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.OperationId,claim.OperationId)
            & Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.Checkpoint,claim.Checkpoint)
            & Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.LeaseOwner,claim.LeaseOwner)
            & Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.LeaseGeneration,claim.LeaseGeneration)
            & Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.OperationFingerprint,claim.OperationFingerprint)
            & Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.LeaseUntilUtcTicksV1,claim.LeaseUntilUtcTicksV1)
            & Builders<LskuRetirementRequestOperation>.Filter.Gt(x=>x.LeaseUntilUtcTicksV1,m.UpdatedAtUtcTicks);
        var u=Builders<LskuRetirementRequestOperation>.Update.Set(x=>x.Checkpoint,m.NextCheckpoint)
            .Set(x=>x.RecoveryDisposition,m.RecoveryDisposition).Set(x=>x.NextAttemptAtUtcTicksV1,m.NextAttemptAtUtcTicksV1)
            .Set(x=>x.LastFailureCode,m.LastFailureCode).Set(x=>x.UpdatedAtUtcTicksV1,m.UpdatedAtUtcTicks).Inc(x=>x.Version,1);
        if(m.WorkflowInstanceId.HasValue)u=u.Set(x=>x.WorkflowInstanceId,m.WorkflowInstanceId);
        if(m.WorkflowTemplateId.HasValue)u=u.Set(x=>x.WorkflowTemplateId,m.WorkflowTemplateId);
        if(m.WorkflowTemplateVersionId.HasValue)u=u.Set(x=>x.WorkflowTemplateVersionId,m.WorkflowTemplateVersionId);
        if(m.ApprovalTaskId.HasValue)u=u.Set(x=>x.ApprovalTaskId,m.ApprovalTaskId);
        if(m.AssignmentSnapshotId.HasValue)u=u.Set(x=>x.AssignmentSnapshotId,m.AssignmentSnapshotId);
        if(m.StartTransitionLogId.HasValue)u=u.Set(x=>x.StartTransitionLogId,m.StartTransitionLogId);
        if(m.WorkflowStartedAtUtcTicksV1.HasValue)u=u.Set(x=>x.WorkflowStartedAtUtcTicksV1,m.WorkflowStartedAtUtcTicksV1);
        if(m.DecisionKind.HasValue)u=u.Set(x=>x.DecisionKind,m.DecisionKind);
        if(m.DecisionActorSubjectId.HasValue)u=u.Set(x=>x.DecisionActorSubjectId,m.DecisionActorSubjectId);
        if(m.DecisionReasonCode is not null)u=u.Set(x=>x.DecisionReasonCode,m.DecisionReasonCode);
        if(m.DecisionAtUtcTicksV1.HasValue)u=u.Set(x=>x.DecisionAtUtcTicksV1,m.DecisionAtUtcTicksV1);
        if(m.DecisionTransitionSequence.HasValue)u=u.Set(x=>x.DecisionTransitionSequence,m.DecisionTransitionSequence);
        if(m.DecisionTaskStatus is not null)u=u.Set(x=>x.DecisionTaskStatus,m.DecisionTaskStatus);
        if(m.DecisionInstanceStatus is not null)u=u.Set(x=>x.DecisionInstanceStatus,m.DecisionInstanceStatus);
        if(m.NextCheckpoint==LskuRetirementRequestCheckpoint.Completed)u=u.Set(x=>x.IsActive,false);
        if(m.ReleaseLease)u=u.Set(x=>x.LeaseOwner,null).Set(x=>x.LeaseUntilUtcTicksV1,null);
        return (await _items.UpdateOneAsync(filter,u,cancellationToken:ct)).ModifiedCount==1;
    }

    public async Task<LskuRetirementRequestPage> DiscoverRecoverableAsync(long now,int limit,Guid? after=null,CancellationToken ct=default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var terminal=new[]{LskuRetirementRequestCheckpoint.Completed,LskuRetirementRequestCheckpoint.ManualReconciliationRequired};
        var f=Active & Builders<LskuRetirementRequestOperation>.Filter.Nin(x=>x.Checkpoint,terminal)
            & Builders<LskuRetirementRequestOperation>.Filter.Or(Builders<LskuRetirementRequestOperation>.Filter.Eq(x=>x.NextAttemptAtUtcTicksV1,null),Builders<LskuRetirementRequestOperation>.Filter.Lte(x=>x.NextAttemptAtUtcTicksV1,now));
        if(after.HasValue)f&=Builders<LskuRetirementRequestOperation>.Filter.Gt(x=>x.OperationId,after.Value);
        var rows=await _items.Find(f).SortBy(x=>x.OperationId).Limit(limit).ToListAsync(ct);
        return new(rows,rows.Count==limit?rows[^1].OperationId:null);
    }

    private bool Valid(LskuRetirementRequestOperation x) => TenantId != Guid.Empty && x.TenantId == TenantId
        && x.OperationId != Guid.Empty && x.LskuId != Guid.Empty && x.BaseLskuVersion >= 0
        && x.MakerSubjectId != Guid.Empty && ExactReason(x.RequestReason)
        && (x.WorkflowTemplateId.HasValue ^ x.WorkflowTemplateCode is not null)
        && (x.WorkflowTemplateCode is null || Exact(x.WorkflowTemplateCode, 128))
        && x.CandidatePrincipalIds is { Count: >= 1 and <= 100 }
        && x.CandidatePrincipalIds.All(id => id != Guid.Empty)
        && x.CandidatePrincipalIds.Distinct().Count() == x.CandidatePrincipalIds.Count
        && Exact(x.ReasonCode, 128)
        && (x.ConfiguredDueAfterSeconds.HasValue == x.DueAtUtcTicksV1.HasValue)
        && x.ConfiguredDueAfterSeconds is null or >= 60 and <= 2_592_000
        && x.ObjectType == LskuRetirementRequestWorkflowStartRequestFactory.ObjectType
        && x.ObjectId == x.OperationId.ToString("D") && Exact(x.ObjectRef, 256)
        && Exact(x.StartIdempotencyKey, 256) && x.OperationFingerprint is { Length: 64 }
        && x.CreatedAtUtcTicksV1 > 0;

    private static bool ExactReplay(LskuRetirementRequestOperation a, LskuRetirementRequestOperation b) =>
        a.OperationId == b.OperationId && a.LskuId == b.LskuId && a.BaseLskuVersion == b.BaseLskuVersion
        && a.MakerSubjectId == b.MakerSubjectId && a.RequestReason == b.RequestReason
        && a.WorkflowTemplateId == b.WorkflowTemplateId && a.WorkflowTemplateCode == b.WorkflowTemplateCode
        && a.CandidatePrincipalIds.SequenceEqual(b.CandidatePrincipalIds) && a.ReasonCode == b.ReasonCode
        && a.CommentRequired == b.CommentRequired && a.EvidenceRequired == b.EvidenceRequired
        && a.ConfiguredDueAfterSeconds == b.ConfiguredDueAfterSeconds && a.ObjectType == b.ObjectType
        && a.ObjectId == b.ObjectId && a.ObjectRef == b.ObjectRef
        && a.StartIdempotencyKey == b.StartIdempotencyKey
        && (a.DueAtUtcTicksV1 == b.DueAtUtcTicksV1
            ? a.OperationFingerprint == b.OperationFingerprint
            : a.ConfiguredDueAfterSeconds.HasValue && a.DueAtUtcTicksV1.HasValue && b.DueAtUtcTicksV1.HasValue
              && b.OperationFingerprint == LskuRetirementRequestWorkflowStartRequestFactory.ComputeFingerprint(b));

    private static bool Exact(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && value == value.Trim() && !value.Any(char.IsControl);
    private static bool ExactReason(string? value) => value is not null && value == value.Trim()
        && value.EnumerateRunes().Count() is >= 1 and <= 2000 && !value.Any(char.IsControl);
    private static bool Allowed(LskuRetirementRequestCheckpoint current, LskuRetirementRequestCheckpoint next) =>
        current == next || (current, next) switch
        {
            (LskuRetirementRequestCheckpoint.Prepared, LskuRetirementRequestCheckpoint.StartOutcomeUnknown) => true,
            (LskuRetirementRequestCheckpoint.Prepared, LskuRetirementRequestCheckpoint.WorkflowStarted) => true,
            (LskuRetirementRequestCheckpoint.Prepared, LskuRetirementRequestCheckpoint.AwaitingMakerReplay) => true,
            (LskuRetirementRequestCheckpoint.StartOutcomeUnknown, LskuRetirementRequestCheckpoint.WorkflowStarted) => true,
            (LskuRetirementRequestCheckpoint.AwaitingMakerReplay, LskuRetirementRequestCheckpoint.WorkflowStarted) => true,
            (LskuRetirementRequestCheckpoint.WorkflowStarted, LskuRetirementRequestCheckpoint.AwaitingDecision) => true,
            (LskuRetirementRequestCheckpoint.AwaitingDecision, LskuRetirementRequestCheckpoint.DecisionObserved) => true,
            (LskuRetirementRequestCheckpoint.DecisionObserved, LskuRetirementRequestCheckpoint.DecisionApplied) => true,
            (LskuRetirementRequestCheckpoint.DecisionApplied, LskuRetirementRequestCheckpoint.Completed) => true,
            (_, LskuRetirementRequestCheckpoint.ManualReconciliationRequired) => true,
            _ => false
        };
}
