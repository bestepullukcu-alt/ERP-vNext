using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Persistence.Features.CapacityPlans;
public sealed class CapacityLeaseStore(IMongoDatabase db) : ICapacityLeaseStore
{
    private readonly IMongoCollection<CapacityEvaluation> evaluations = db.GetCollection<CapacityEvaluation>("capacity_evaluations");
    private readonly IMongoCollection<BsonDocument> slots = db.GetCollection<BsonDocument>("capacity_active_slots");
    private readonly IMongoCollection<BsonDocument> audit = db.GetCollection<BsonDocument>("capacity_audit");
    private readonly IMongoCollection<BsonDocument> outbox = db.GetCollection<BsonDocument>("capacity_outbox");
    private static readonly Guid ExecutorActor = Guid.Parse("19200000-0000-4000-8000-000000000003");
    private static FilterDefinition<CapacityEvaluation> Scope(CapacityScope s) =>
        Builders<CapacityEvaluation>.Filter.Eq(x=>x.TenantId,s.TenantId) &
        Builders<CapacityEvaluation>.Filter.Eq(x=>x.LegalEntityId,s.LegalEntityId) &
        Builders<CapacityEvaluation>.Filter.Eq(x=>x.IsDeleted,false);
    private static FilterDefinition<CapacityEvaluation> Expired => new BsonDocument("$expr",
        new BsonDocument("$lte",new BsonArray { "$LeaseUntil", "$$NOW" }));
    private static FilterDefinition<CapacityEvaluation> Unexpired => new BsonDocument("$expr",
        new BsonDocument("$gt",new BsonArray { "$LeaseUntil", "$$NOW" }));
    private static FilterDefinition<CapacityEvaluation> Claimable =>
        Builders<CapacityEvaluation>.Filter.Eq(x=>x.Status,"Accepted") |
        (Builders<CapacityEvaluation>.Filter.Eq(x=>x.Status,"Running") & Expired);
    public async Task<IReadOnlyList<CapacityScope>> FindPendingScopesAsync(CancellationToken ct)
    {
        // Internal worker discovery only. Every mutation below is scoped and fenced.
        var filter=Builders<CapacityEvaluation>.Filter.Eq(x=>x.IsDeleted,false) & Claimable;
        var rows=await evaluations.Find(filter).Limit(256).ToListAsync(ct);
        return rows.Select(x=>new CapacityScope(x.TenantId,x.LegalEntityId,ExecutorActor)).Distinct().ToArray();
    }
    public async Task<CapacityEvaluation?> ClaimAsync(CapacityScope scope,CancellationToken ct)
    {
        scope.EnsureTrusted();
        var candidate=await evaluations.Find(Scope(scope)&Claimable).SortBy(x=>x.SubmittedAt).FirstOrDefaultAsync(ct);
        if(candidate is null) return null;
        if(candidate.Status=="Running" && candidate.Attempt>=3)
        {
            await TerminalAsync(scope,candidate.Id,candidate.Version,candidate.Fence,"Failed",[],true,ct);
            return null;
        }
        var update=Builders<CapacityEvaluation>.Update.Pipeline(PipelineDefinition<CapacityEvaluation,CapacityEvaluation>.Create(new[] {
            new BsonDocument("$set",new BsonDocument {
                {"Status","Running"}, {"Attempt",new BsonDocument("$add",new BsonArray {"$Attempt",1})},
                {"Fence",new BsonDocument("$add",new BsonArray {"$Fence",1})},
                {"Version",new BsonDocument("$add",new BsonArray {"$Version",1})},
                {"LeaseUntil",new BsonDocument("$dateAdd",new BsonDocument {{"startDate","$$NOW"},{"unit","second"},{"amount",30}})}
            }) }));
        var filter=Scope(scope)&Builders<CapacityEvaluation>.Filter.Eq(x=>x.Id,candidate.Id)&
            Builders<CapacityEvaluation>.Filter.Eq(x=>x.Version,candidate.Version)&Claimable&
            Builders<CapacityEvaluation>.Filter.Lt(x=>x.Attempt,3);
        return await evaluations.FindOneAndUpdateAsync(filter,update,new FindOneAndUpdateOptions<CapacityEvaluation> {ReturnDocument=ReturnDocument.After},ct);
    }
    public async Task<CapacityEvaluation?> RenewAsync(CapacityScope scope,CapacityEvaluation lease,CancellationToken ct)
    {
        var filter=Scope(scope)&Builders<CapacityEvaluation>.Filter.Eq(x=>x.Id,lease.Id)&
            Builders<CapacityEvaluation>.Filter.Eq(x=>x.Status,"Running")&
            Builders<CapacityEvaluation>.Filter.Eq(x=>x.Version,lease.Version)&
            Builders<CapacityEvaluation>.Filter.Eq(x=>x.Fence,lease.Fence)&Unexpired;
        var update=Builders<CapacityEvaluation>.Update.Pipeline(PipelineDefinition<CapacityEvaluation,CapacityEvaluation>.Create(new[] {
            new BsonDocument("$set",new BsonDocument {
                {"LeaseUntil",new BsonDocument("$dateAdd",new BsonDocument {{"startDate","$$NOW"},{"unit","second"},{"amount",30}})},
                {"Version",new BsonDocument("$add",new BsonArray {"$Version",1})}
            }) }));
        return await evaluations.FindOneAndUpdateAsync(filter,update,new FindOneAndUpdateOptions<CapacityEvaluation> {ReturnDocument=ReturnDocument.After},ct);
    }
    public async Task<bool> TerminalAsync(CapacityScope scope,Guid id,int version,long fence,string status,IReadOnlyList<CapacityBottleneck> bottlenecks,bool expired,CancellationToken ct)
    {
        scope.EnsureTrusted();
        using var session=await db.Client.StartSessionAsync(cancellationToken:ct);
        session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority));
        var committed=false;
        try
        {
            var filter=Scope(scope)&Builders<CapacityEvaluation>.Filter.Eq(x=>x.Id,id)&
                Builders<CapacityEvaluation>.Filter.Eq(x=>x.Status,"Running")&
                Builders<CapacityEvaluation>.Filter.Eq(x=>x.Version,version)&
                Builders<CapacityEvaluation>.Filter.Eq(x=>x.Fence,fence)&(expired?Expired:Unexpired);
            if(expired) filter &= Builders<CapacityEvaluation>.Filter.Eq(x=>x.Attempt,3);
            var literal=new BsonArray(bottlenecks.Select(x=>new BsonDocument {
                {"ResourceRef",x.ResourceRef},{"Period",x.Period},{"RequiredCapacity",x.RequiredCapacity},
                {"AvailableCapacity",x.AvailableCapacity},{"Shortfall",x.Shortfall},{"UomId",x.UomId}}));
            var update=Builders<CapacityEvaluation>.Update.Pipeline(PipelineDefinition<CapacityEvaluation,CapacityEvaluation>.Create(new[] {
                new BsonDocument("$set",new BsonDocument {
                    {"Status",status},{"Bottlenecks",literal},{"CompletedAt","$$NOW"},
                    {"Version",new BsonDocument("$add",new BsonArray {"$Version",1})}, {"LeaseUntil",BsonNull.Value}
                }) }));
            var result=await evaluations.FindOneAndUpdateAsync(session,filter,update,
                new FindOneAndUpdateOptions<CapacityEvaluation> {ReturnDocument=ReturnDocument.After},ct);
            if(result is null){await session.AbortTransactionAsync(ct);return false;}
            var slotFilter=new BsonDocument{{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
                {"PlanId",result.CapacityPlanId.ToString()},{"ScenarioId",result.ScenarioId.ToString()},{"EvaluationId",id.ToString()}};
            var released=await slots.DeleteOneAsync(session,slotFilter,null,ct);
            if(released.DeletedCount!=1) throw new InvalidOperationException("Active slot missing during terminal commit.");
            var completed=result.CompletedAt!.Value;
            await audit.InsertOneAsync(session,new BsonDocument {
                {"_id",Guid.NewGuid().ToString()},{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
                {"TargetId",id.ToString()},{"Operation","terminalCapacityEvaluation"},{"Status",status},
                {"CorrelationId",result.CorrelationId.ToString()},{"OccurredAt",completed}
            },cancellationToken:ct);
            await outbox.InsertOneAsync(session,new BsonDocument {
                {"_id",Guid.NewGuid().ToString()},{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
                {"AggregateId",id.ToString()},{"EventType","capacity.evaluation.completed.v1"},{"Status","Pending"},
                {"EvaluationStatus",status},{"CorrelationId",result.CorrelationId.ToString()},
                {"CausationId",result.CausationId.ToString()},{"OccurredAt",completed},{"ContractVersion","v1"},
                {"Payload",new BsonDocument {{"capacityPlanId",result.CapacityPlanId.ToString()},
                    {"scenarioId",result.ScenarioId.ToString()},{"evaluationId",id.ToString()},{"status",status}}}
            },cancellationToken:ct);
            await session.CommitTransactionAsync(ct);committed=true;return true;
        }
        catch(MongoException ex) when(ex.HasErrorLabel("UnknownTransactionCommitResult"))
        {
            return await ReconcileTerminalAsync(scope,id,status,ct);
        }
        finally
        {
            if(!committed && session.IsInTransaction)
                try {await session.AbortTransactionAsync(CancellationToken.None);} catch(MongoException) { }
        }
    }
    private async Task<bool> ReconcileTerminalAsync(CapacityScope scope,Guid id,string status,CancellationToken ct)
    {
        try
        {
            var persisted=await evaluations.Find(Scope(scope)&Builders<CapacityEvaluation>.Filter.Eq(x=>x.Id,id)).FirstOrDefaultAsync(ct);
            if(persisted is null || persisted.Status!=status || persisted.CompletedAt is null) return false;
            var eventScope=new BsonDocument{{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
                {"AggregateId",id.ToString()},{"EventType","capacity.evaluation.completed.v1"}};
            var eventFilter=new BsonDocument(eventScope){{"EvaluationStatus",status},{"Status","Pending"},
                {"CorrelationId",persisted.CorrelationId.ToString()},{"CausationId",persisted.CausationId.ToString()},
                {"OccurredAt",persisted.CompletedAt.Value},{"ContractVersion","v1"},
                {"Payload",new BsonDocument{{"capacityPlanId",persisted.CapacityPlanId.ToString()},
                    {"scenarioId",persisted.ScenarioId.ToString()},{"evaluationId",id.ToString()},{"status",status}}}};
            var auditScope=new BsonDocument{{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
                {"TargetId",id.ToString()},{"Operation","terminalCapacityEvaluation"}};
            var auditFilter=new BsonDocument(auditScope){{"Status",status},
                {"CorrelationId",persisted.CorrelationId.ToString()},{"OccurredAt",persisted.CompletedAt.Value}};
            var slotFilter=new BsonDocument{{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
                {"PlanId",persisted.CapacityPlanId.ToString()},{"ScenarioId",persisted.ScenarioId.ToString()},
                {"EvaluationId",id.ToString()}};
            return await outbox.CountDocumentsAsync(eventScope,cancellationToken:ct)==1 &&
                await outbox.CountDocumentsAsync(eventFilter,cancellationToken:ct)==1 &&
                await audit.CountDocumentsAsync(auditScope,cancellationToken:ct)==1 &&
                await audit.CountDocumentsAsync(auditFilter,cancellationToken:ct)==1 &&
                await slots.CountDocumentsAsync(slotFilter,cancellationToken:ct)==0;
        }
        catch(Exception ex) when(ex is MongoException or TimeoutException)
        {
            throw new InvalidOperationException("Terminal commit outcome unresolved: scoped reconciliation read failed.",ex);
        }
    }
}
