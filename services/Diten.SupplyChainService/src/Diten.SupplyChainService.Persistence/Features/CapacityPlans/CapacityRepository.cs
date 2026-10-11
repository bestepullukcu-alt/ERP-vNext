using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
namespace Diten.SupplyChainService.Persistence.Features.CapacityPlans;
public sealed class CapacityRepository(IMongoDatabase db, IDemandFixtureReader demand, IConstraintFixtureReader constraints) : ICapacityRepository
{
    private readonly IMongoCollection<CapacityPlan> plans = db.GetCollection<CapacityPlan>("capacity_plans").WithReadConcern(ReadConcern.Majority);
    private readonly IMongoCollection<CapacityScenario> scenarios = db.GetCollection<CapacityScenario>("capacity_scenarios").WithReadConcern(ReadConcern.Majority);
    private readonly IMongoCollection<CapacityEvaluation> evaluations = db.GetCollection<CapacityEvaluation>("capacity_evaluations").WithReadConcern(ReadConcern.Majority);
    private readonly IMongoCollection<BsonDocument> receipts = db.GetCollection<BsonDocument>("capacity_receipts");
    private readonly IMongoCollection<BsonDocument> slots = db.GetCollection<BsonDocument>("capacity_active_slots");
    private readonly IMongoCollection<BsonDocument> audit = db.GetCollection<BsonDocument>("capacity_audit");
    private readonly IMongoCollection<BsonDocument> outbox = db.GetCollection<BsonDocument>("capacity_outbox");
    private static FilterDefinition<T> Scoped<T>(CapacityScope s) where T : Diten.SupplyChainService.Domain.Common.EntityBase
    {
        s.EnsureTrusted();
        return Builders<T>.Filter.Eq(x=>x.TenantId,s.TenantId) & Builders<T>.Filter.Eq(x=>x.IsDeleted,false) &
            Builders<T>.Filter.Eq("LegalEntityId",s.LegalEntityId);
    }
    private static BsonDocument ReceiptKey(CapacityScope s,string operation,string target,string key) => new() {
        {"TenantId",s.TenantId.ToString()},{"LegalEntityId",s.LegalEntityId.ToString()}, {"Operation",operation}, {"Target",target},{"Key",key} };
    // A server-rejected commit command without an unknown-result label is a definite failure.
    // Network, timeout and write-concern outcomes remain uncertain after commit was attempted.
    private static string CommitFailureCode(Exception ex,bool commitAttempted) =>
        !commitAttempted || (ex is MongoCommandException command && !command.HasErrorLabel("UnknownTransactionCommitResult"))
            ? "DEPENDENCY_UNAVAILABLE" : "COMMIT_RESULT_UNRESOLVED";
    private static CapacityMutation<T> Error<T>(int status,string code) => new(default,status,code);
    private async Task<CapacityMutation<T>?> Replay<T>(IClientSessionHandle? session, BsonDocument key, string fingerprint, CancellationToken ct) where T:class
    {
        var doc=session is null ? await receipts.Find(key).FirstOrDefaultAsync(ct) : await receipts.Find(session,key).FirstOrDefaultAsync(ct);
        if (doc is null) return null;
        return doc["Fingerprint"].AsString!=fingerprint ? Error<T>(409,"IDEMPOTENCY_KEY_REUSED") :
            new(JsonSerializer.Deserialize<T>(doc["Body"].AsString)!,doc["Status"].AsInt32,null,true);
    }
    private static BsonDocument Receipt(BsonDocument key,string fingerprint,object body,int status,Guid correlation)
    {
        var doc=key.DeepClone().AsBsonDocument;
        doc.Add("_id",Guid.NewGuid().ToString());doc.Add("Fingerprint",fingerprint);
        doc.Add("Body",JsonSerializer.Serialize(body));doc.Add("Status",status);doc.Add("CorrelationId",correlation.ToString());
        return doc;
    }
    private static BsonDocument Audit(CapacityScope s,Guid id,string operation,Guid correlation) => new() {
        {"_id",Guid.NewGuid().ToString()},{"TenantId",s.TenantId.ToString()},{"LegalEntityId",s.LegalEntityId.ToString()},
        {"TargetId",id.ToString()},{"Operation",operation},{"ActorId",s.ActorId.ToString()},{"CorrelationId",correlation.ToString()},
        {"OccurredAt",DateTime.UtcNow} };
    private static BsonDocument Pending(CapacityScope s,Guid eventId,string type,Guid correlation,Guid? causation,Guid id,DateTimeOffset occurredAt,BsonDocument payload) => new() {
        {"_id",eventId.ToString()},{"EventId",eventId.ToString()},{"TenantId",s.TenantId.ToString()},{"LegalEntityId",s.LegalEntityId.ToString()},
        {"EventType",type},{"AggregateId",id.ToString()},{"Status","Pending"},{"CorrelationId",correlation.ToString()},
        {"CausationId",causation?.ToString() is { } c ? c : BsonNull.Value},{"OccurredAt",occurredAt.UtcDateTime},
        {"ContractVersion","v1"},{"Payload",payload} };
    public async Task<CapacityPlan?> GetPlanAsync(CapacityScope scope,Guid id,CancellationToken ct)
    {
        try { return await plans.Find(Scoped<CapacityPlan>(scope)&Builders<CapacityPlan>.Filter.Eq(x=>x.Id,id)).FirstOrDefaultAsync(ct); }
        catch(Exception ex) when(ex is MongoException or TimeoutException) { throw new CapacityReadUnavailableException(); }
    }
    public async Task<CapacityScenario?> GetScenarioAsync(CapacityScope scope,Guid planId,Guid id,CancellationToken ct)
    {
        try { return await scenarios.Find(Scoped<CapacityScenario>(scope)&Builders<CapacityScenario>.Filter.Eq(x=>x.CapacityPlanId,planId)&Builders<CapacityScenario>.Filter.Eq(x=>x.Id,id)).FirstOrDefaultAsync(ct); }
        catch(Exception ex) when(ex is MongoException or TimeoutException) { throw new CapacityReadUnavailableException(); }
    }
    public async Task<CapacityEvaluation?> GetEvaluationAsync(CapacityScope scope,Guid planId,Guid id,CancellationToken ct)
    {
        try { return await evaluations.Find(Scoped<CapacityEvaluation>(scope)&Builders<CapacityEvaluation>.Filter.Eq(x=>x.CapacityPlanId,planId)&Builders<CapacityEvaluation>.Filter.Eq(x=>x.Id,id)).FirstOrDefaultAsync(ct); }
        catch(Exception ex) when(ex is MongoException or TimeoutException) { throw new CapacityReadUnavailableException(); }
    }
    public async Task<CapacityMutation<CapacityPlan>> CreatePlanAsync(CapacityScope scope,string key,string fingerprint,Guid correlation,CapacityPlan plan,CancellationToken ct)
    {
        scope.EnsureTrusted();var rk=ReceiptKey(scope,"createCapacityPlan","create",key);
        for(var attempt=0;attempt<5;attempt++)
        {
            var commitAttempted=false;
            try
            {
                using var session=await db.Client.StartSessionAsync(cancellationToken:ct);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority));
                try
                {
                    var replay=await Replay<CapacityPlan>(session,rk,fingerprint,ct);
                    if(replay is not null){await session.AbortTransactionAsync(ct);return replay;}
                    if(!demand.IsExact(scope,plan)){await session.AbortTransactionAsync(ct);return Error<CapacityPlan>(422,"INVALID_DEMAND_REFERENCE");}
                    var duplicate=await plans.Find(session,Scoped<CapacityPlan>(scope)&Builders<CapacityPlan>.Filter.Eq(x=>x.HorizonStart,plan.HorizonStart)&Builders<CapacityPlan>.Filter.Eq(x=>x.HorizonEnd,plan.HorizonEnd)&Builders<CapacityPlan>.Filter.Eq(x=>x.DemandPlanId,plan.DemandPlanId)&Builders<CapacityPlan>.Filter.Eq(x=>x.DemandPlanVersion,plan.DemandPlanVersion)).AnyAsync(ct);
                    if(duplicate){await session.AbortTransactionAsync(ct);return Error<CapacityPlan>(409,"CAPACITY_PLAN_ALREADY_EXISTS");}
                    plan.CreatedEventId=Guid.NewGuid();
                    await plans.InsertOneAsync(session,plan,cancellationToken:ct);
                    await receipts.InsertOneAsync(session,Receipt(rk,fingerprint,plan,201,correlation),cancellationToken:ct);
                    await audit.InsertOneAsync(session,Audit(scope,plan.Id,"createCapacityPlan",correlation),cancellationToken:ct);
                    await outbox.InsertOneAsync(session,Pending(scope,plan.CreatedEventId,"capacity.plan.created.v1",correlation,null,plan.Id,plan.CreatedAt,
                        new BsonDocument { {"capacityPlanId",plan.Id.ToString()}, {"provenance",new BsonDocument {
                            {"demandPlanId",plan.DemandPlanId},{"demandPlanVersion",plan.DemandPlanVersion},{"sourceContract","DEMAND"},
                            {"sourceContractVersion","v1"},{"sourceCapturedAt",plan.SourceCapturedAt.UtcDateTime},{"sourceChecksum",plan.SourceChecksum} }} }),cancellationToken:ct);
                    commitAttempted=true;await session.CommitTransactionAsync(ct);return new(plan,201);
                }
                catch { if(!commitAttempted && session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);throw; }
            }
            catch(MongoException ex) when(!commitAttempted && (ex.HasErrorLabel("TransientTransactionError") || ex is MongoWriteException { WriteError.Category:ServerErrorCategory.DuplicateKey }))
            { await Task.Delay(10*(attempt+1),ct); }
            catch(Exception ex) when(ex is MongoException or TimeoutException)
            {
                var recovered=await SafeReplay<CapacityPlan>(rk,fingerprint,ct);
                return recovered ?? Error<CapacityPlan>(503,CommitFailureCode(ex,commitAttempted));
            }
        }
        return Error<CapacityPlan>(503,"DEPENDENCY_UNAVAILABLE");
    }
    public async Task<CapacityMutation<CapacityScenario>> CreateScenarioAsync(CapacityScope scope,Guid planId,string key,string fingerprint,Guid correlation,CapacityScenario scenario,CancellationToken ct)
    {
        scope.EnsureTrusted();var rk=ReceiptKey(scope,"createCapacityScenario",planId.ToString(),key);
        for(var attempt=0;attempt<5;attempt++)
        {
            var commitAttempted=false;
            try
            {
                using var session=await db.Client.StartSessionAsync(cancellationToken:ct);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority));
                try
                {
                    var parent=await plans.Find(session,Scoped<CapacityPlan>(scope)&Builders<CapacityPlan>.Filter.Eq(x=>x.Id,planId)).FirstOrDefaultAsync(ct);
                    if(parent is null){await session.AbortTransactionAsync(ct);return Error<CapacityScenario>(404,"UNKNOWN_CAPACITY_PLAN");}
                    var replay=await Replay<CapacityScenario>(session,rk,fingerprint,ct);
                    if(replay is not null){await session.AbortTransactionAsync(ct);return replay;}
                    if(parent.Status!="Draft"){await session.AbortTransactionAsync(ct);return Error<CapacityScenario>(409,"CAPACITY_PLAN_STATE_CONFLICT");}
                    if(!constraints.IsExact(scope,scenario)){await session.AbortTransactionAsync(ct);return Error<CapacityScenario>(422,"INVALID_CONSTRAINT_REFERENCE");}
                    var duplicate=await scenarios.Find(session,Scoped<CapacityScenario>(scope)&Builders<CapacityScenario>.Filter.Eq(x=>x.CapacityPlanId,planId)&Builders<CapacityScenario>.Filter.Eq(x=>x.Name,scenario.Name),new FindOptions {Collation=Collation.Simple}).AnyAsync(ct);
                    if(duplicate){await session.AbortTransactionAsync(ct);return Error<CapacityScenario>(409,"CAPACITY_SCENARIO_NAME_CONFLICT");}
                    scenario.CreatedEventId=Guid.NewGuid();
                    await scenarios.InsertOneAsync(session,scenario,cancellationToken:ct);
                    await receipts.InsertOneAsync(session,Receipt(rk,fingerprint,scenario,201,correlation),cancellationToken:ct);
                    await audit.InsertOneAsync(session,Audit(scope,scenario.Id,"createCapacityScenario",correlation),cancellationToken:ct);
                    await outbox.InsertOneAsync(session,Pending(scope,scenario.CreatedEventId,"capacity.scenario.created.v1",correlation,parent.CreatedEventId,scenario.Id,scenario.CreatedAt,
                        new BsonDocument {{"capacityPlanId",planId.ToString()},{"scenarioId",scenario.Id.ToString()}}),cancellationToken:ct);
                    commitAttempted=true;await session.CommitTransactionAsync(ct);return new(scenario,201);
                }
                catch {if(!commitAttempted && session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);throw;}
            }
            catch(MongoException ex) when(!commitAttempted && (ex.HasErrorLabel("TransientTransactionError") || ex is MongoWriteException { WriteError.Category:ServerErrorCategory.DuplicateKey }))
            {await Task.Delay(10*(attempt+1),ct);}
            catch(Exception ex) when(ex is MongoException or TimeoutException)
            {var recovered=await SafeReplay<CapacityScenario>(rk,fingerprint,ct);return recovered??Error<CapacityScenario>(503,CommitFailureCode(ex,commitAttempted));}
        }
        return Error<CapacityScenario>(503,"DEPENDENCY_UNAVAILABLE");
    }
    public async Task<CapacityMutation<CapacityEvaluation>> EvaluateAsync(CapacityScope scope,Guid planId,Guid scenarioId,string key,string fingerprint,Guid correlation,CapacityEvaluation evaluation,CancellationToken ct)
    {
        scope.EnsureTrusted();var rk=ReceiptKey(scope,"evaluateCapacityScenario",planId+"/"+scenarioId,key);
        for(var attempt=0;attempt<5;attempt++)
        {
            var commitAttempted=false;
            try
            {
                using var session=await db.Client.StartSessionAsync(cancellationToken:ct);
                session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority));
                try
                {
                    var scenario=await scenarios.Find(session,Scoped<CapacityScenario>(scope)&Builders<CapacityScenario>.Filter.Eq(x=>x.CapacityPlanId,planId)&Builders<CapacityScenario>.Filter.Eq(x=>x.Id,scenarioId)).FirstOrDefaultAsync(ct);
                    if(scenario is null){await session.AbortTransactionAsync(ct);return Error<CapacityEvaluation>(404,"UNKNOWN_CAPACITY_SCENARIO");}
                    var replay=await Replay<CapacityEvaluation>(session,rk,fingerprint,ct);
                    if(replay is not null){await session.AbortTransactionAsync(ct);return replay;}
                    var sk=new BsonDocument{{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},{"PlanId",planId.ToString()},{"ScenarioId",scenarioId.ToString()}};
                    if(await slots.Find(session,sk).AnyAsync(ct)){await session.AbortTransactionAsync(ct);return Error<CapacityEvaluation>(409,"EVALUATION_ALREADY_ACTIVE");}
                    if(!constraints.CanEvaluate(scope,scenario,evaluation)){await session.AbortTransactionAsync(ct);return Error<CapacityEvaluation>(422,"INVALID_CONSTRAINT_REFERENCE");}
                    evaluation.CausationId=scenario.CreatedEventId;
                    await evaluations.InsertOneAsync(session,evaluation,cancellationToken:ct);
                    sk.Add("_id",Guid.NewGuid().ToString());sk.Add("EvaluationId",evaluation.Id.ToString());
                    await slots.InsertOneAsync(session,sk,cancellationToken:ct);
                    await receipts.InsertOneAsync(session,Receipt(rk,fingerprint,evaluation,202,correlation),cancellationToken:ct);
                    await audit.InsertOneAsync(session,Audit(scope,evaluation.Id,"evaluateCapacityScenario",correlation),cancellationToken:ct);
                    commitAttempted=true;await session.CommitTransactionAsync(ct);return new(evaluation,202);
                }
                catch {if(!commitAttempted && session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);throw;}
            }
            catch(MongoException ex) when(!commitAttempted && (ex.HasErrorLabel("TransientTransactionError") || ex is MongoWriteException { WriteError.Category:ServerErrorCategory.DuplicateKey }))
            {await Task.Delay(10*(attempt+1),ct);}
            catch(Exception ex) when(ex is MongoException or TimeoutException)
            {var recovered=await SafeReplay<CapacityEvaluation>(rk,fingerprint,ct);return recovered??Error<CapacityEvaluation>(503,CommitFailureCode(ex,commitAttempted));}
        }
        return Error<CapacityEvaluation>(503,"DEPENDENCY_UNAVAILABLE");
    }
    private async Task<CapacityMutation<T>?> SafeReplay<T>(BsonDocument key,string fingerprint,CancellationToken ct) where T:class
    {try{return await Replay<T>(null,key,fingerprint,ct);}catch(Exception ex) when(ex is MongoException or TimeoutException){return null;}}
}
