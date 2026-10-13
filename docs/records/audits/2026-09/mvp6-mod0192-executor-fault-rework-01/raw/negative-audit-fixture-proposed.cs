using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
using Diten.SupplyChainService.Persistence.Features.CapacityPlans;
using Xunit;
namespace Diten.SupplyChainService.Tests.CapacityPlans;
[Collection("CapacityMongo")]
public sealed class CapacityAtomicityTests
{
    [Fact]
    public async Task Fixture_mutations_and_fenced_terminal_are_durable_and_single_effect()
    {
        var client=new MongoClient("mongodb://127.0.0.1:57192/?replicaSet=rsmod192&serverSelectionTimeoutMS=5000");
        var db=client.GetDatabase("DitenSupplyChain_Mod0192_Test");
        foreach(var name in new[]{"capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"})
            await db.DropCollectionAsync(name);
        var schema=new CapacitySchema(db);await schema.StartAsync(CancellationToken.None);
        var demand=new DemandFixtureReader();var constraints=new ConstraintFixtureReader();
        var repo=new CapacityRepository(db,demand,constraints);
        var leases=new CapacityLeaseStore(db);
        var scope=new CapacityScope(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.NewGuid());
        var correlation=Guid.NewGuid();
        var plan=new CapacityPlan {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            Name="FY2027 Capacity Baseline",HorizonStart=new DateOnly(2027,1,1),HorizonEnd=new DateOnly(2027,12,31),
            DemandPlanId="dp-2027",DemandPlanVersion="3",SourceCapturedAt=DateTimeOffset.UtcNow,
            SourceChecksum="sha256:ee56d4f9a3c8",CreatedAt=DateTimeOffset.UtcNow,Version=1,CreatedBy=scope.ActorId};
        var created=await repo.CreatePlanAsync(scope,"plan-key","fp-plan",correlation,plan,CancellationToken.None);
        Assert.Equal(201,created.StatusCode);
        Assert.Equal(plan.Id,created.Value!.Id);
        var replay=await repo.CreatePlanAsync(scope,"plan-key","fp-plan",Guid.NewGuid(),new CapacityPlan(),CancellationToken.None);
        Assert.True(replay.Replay);
        Assert.Equal(plan.Id,replay.Value!.Id);
        var changed=await repo.CreatePlanAsync(scope,"plan-key","different",Guid.NewGuid(),new CapacityPlan(),CancellationToken.None);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED",changed.ErrorCode);
        var scenario=new CapacityScenario {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            CapacityPlanId=plan.Id,Name="Add weekend shift",CreatedAt=DateTimeOffset.UtcNow,Version=1,CreatedBy=scope.ActorId,
            ConstraintRefs=[new("line-4-hours","SUPPLY-CONSTRAINTS","8")],
            Adjustments=[new("line-4","2027-W03","80.000","HOUR")]};
        var scenarioResult=await repo.CreateScenarioAsync(scope,plan.Id,"scenario-key","fp-scenario",correlation,scenario,CancellationToken.None);
        Assert.Equal(201,scenarioResult.StatusCode);
        var evaluation=new CapacityEvaluation {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            CapacityPlanId=plan.Id,ScenarioId=scenario.Id,EvaluationMode="Finite",ResourceRefs=["line-4"],
            SubmittedAt=DateTimeOffset.UtcNow,CreatedAt=DateTimeOffset.UtcNow,Version=1,CorrelationId=correlation};
        var accepted=await repo.EvaluateAsync(scope,plan.Id,scenario.Id,"eval-key","fp-eval",correlation,evaluation,CancellationToken.None);
        Assert.Equal(202,accepted.StatusCode);
        var second=await repo.EvaluateAsync(scope,plan.Id,scenario.Id,"other-key","other-fp",correlation,evaluation,CancellationToken.None);
        Assert.Equal("EVALUATION_ALREADY_ACTIVE",second.ErrorCode);
        var lease=await leases.ClaimAsync(scope,CancellationToken.None);
        Assert.NotNull(lease);Assert.Equal(1,lease.Attempt);Assert.Equal("Running",lease.Status);
        var bottleneck=new CapacityBottleneck("line-4","2027-W03","520.000","480.000","40.000","HOUR");
        Assert.True(await leases.TerminalAsync(scope,evaluation.Id,lease.Version,lease.Fence,"Completed",[bottleneck],false,CancellationToken.None));
        Assert.False(await leases.TerminalAsync(scope,evaluation.Id,lease.Version,lease.Fence,"Completed",[bottleneck],false,CancellationToken.None));
        var terminal=await repo.GetEvaluationAsync(scope,plan.Id,evaluation.Id,CancellationToken.None);
        Assert.Equal("Completed",terminal!.Status);Assert.Equal("40.000",terminal.Bottlenecks.Single().Shortfall);
        Assert.Equal(0,await db.GetCollection<BsonDocument>("capacity_active_slots").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(1,await db.GetCollection<BsonDocument>("capacity_outbox").CountDocumentsAsync(new BsonDocument("EventType","capacity.evaluation.completed.v1")));
        Assert.Equal(1,await db.GetCollection<BsonDocument>("capacity_audit").CountDocumentsAsync(new BsonDocument("Operation","terminalCapacityEvaluation")));
        // A new key is eligible only after the first terminal transaction released its slot.
        var retryEvaluation=new CapacityEvaluation {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            CapacityPlanId=plan.Id,ScenarioId=scenario.Id,EvaluationMode="Infinite",ResourceRefs=["line-4"],
            SubmittedAt=DateTimeOffset.UtcNow,CreatedAt=DateTimeOffset.UtcNow,Version=1,CorrelationId=correlation};
        Assert.Equal(202,(await repo.EvaluateAsync(scope,plan.Id,scenario.Id,"eval-key-2","fp-eval-2",correlation,retryEvaluation,CancellationToken.None)).StatusCode);
        var competing=new CapacityLeaseStore(db);
        var claims=await Task.WhenAll(leases.ClaimAsync(scope,CancellationToken.None),competing.ClaimAsync(scope,CancellationToken.None));
        Assert.Single(claims, x=>x is not null);
        var active=claims.Single(x=>x is not null)!;
        Assert.Equal(1,active.Attempt);
        Assert.Null(await leases.RenewAsync(scope,new CapacityEvaluation {Id=active.Id,TenantId=active.TenantId,LegalEntityId=active.LegalEntityId,Version=active.Version-1,Fence=active.Fence},CancellationToken.None));
        for(var attempt=2;attempt<=3;attempt++)
        {
            await db.GetCollection<CapacityEvaluation>("capacity_evaluations").UpdateOneAsync(
                Builders<CapacityEvaluation>.Filter.Eq(x=>x.Id,retryEvaluation.Id),
                Builders<CapacityEvaluation>.Update.Set(x=>x.LeaseUntil,DateTime.UtcNow.AddMinutes(-1)));
            active=(await leases.ClaimAsync(scope,CancellationToken.None))!;
            Assert.NotNull(active);Assert.Equal(attempt,active.Attempt);
        }
        await db.GetCollection<CapacityEvaluation>("capacity_evaluations").UpdateOneAsync(
            Builders<CapacityEvaluation>.Filter.Eq(x=>x.Id,retryEvaluation.Id),
            Builders<CapacityEvaluation>.Update.Set(x=>x.LeaseUntil,DateTime.UtcNow.AddMinutes(-1)));
        Assert.Null(await leases.ClaimAsync(scope,CancellationToken.None));
        var exhausted=await repo.GetEvaluationAsync(scope,plan.Id,retryEvaluation.Id,CancellationToken.None);
        Assert.Equal("Failed",exhausted!.Status);Assert.Equal(3,exhausted.Attempt);
        Assert.Equal(0,await db.GetCollection<BsonDocument>("capacity_active_slots").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(2,await db.GetCollection<BsonDocument>("capacity_outbox").CountDocumentsAsync(new BsonDocument("EventType","capacity.evaluation.completed.v1")));
        Assert.Null(await repo.GetPlanAsync(new CapacityScope(Guid.NewGuid(),scope.LegalEntityId,scope.ActorId),plan.Id,CancellationToken.None));
    }
}

[Collection("CapacityMongo")]
public sealed class CapacityFaultTests
{
    private const string Connection = "mongodb://127.0.0.1:57192/?replicaSet=rsmod192&serverSelectionTimeoutMS=5000";
    private const string Database = "DitenSupplyChain_Mod0192_Test";
    private static readonly string[] Collections = ["capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"];
    private static readonly CapacityScope Scope = new(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.Parse("19200000-0000-4000-8000-000000000003"));

    private static async Task<IMongoDatabase> Reset(string appName)
    {
        var db=new MongoClient(Connection+"&appName="+appName).GetDatabase(Database);
        foreach(var name in Collections) await db.DropCollectionAsync(name);
        await new CapacitySchema(db).StartAsync(CancellationToken.None);
        return db;
    }
    private static async Task<BsonDocument> Fail(string appName,string command,int times,bool writeConcern=false,bool unknown=false)
    {
        var data=new BsonDocument {{"failCommands",new BsonArray {command}},{"appName",appName}};
        if(writeConcern) data.Add("writeConcernError",new BsonDocument {{"code",64},{"errmsg","injected commit acknowledgement loss"}});
        else {data.Add("errorCode",8);data.Add("errorLabels",unknown ? new BsonArray {"UnknownTransactionCommitResult"} : new BsonArray());}
        return await new MongoClient(Connection).GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument {
            {"configureFailPoint","failCommand"},{"mode",new BsonDocument("times",times)},{"data",data}});
    }
    private static async Task<int> StopFail()
    {
        var result=await new MongoClient(Connection).GetDatabase("admin").RunCommandAsync<BsonDocument>(
            new BsonDocument {{"configureFailPoint","failCommand"},{"mode","off"}});
        return result.GetValue("count",0).ToInt32();
    }
    private static async Task<(CapacityRepository repo,CapacityEvaluation evaluation,Guid planId,Guid scenarioId)> EvaluationSetup(IMongoDatabase db)
    {
        var planId=Guid.NewGuid();var scenarioId=Guid.NewGuid();
        var scenario=new CapacityScenario {Id=scenarioId,TenantId=Scope.TenantId,LegalEntityId=Scope.LegalEntityId,
            CapacityPlanId=planId,Name="Fault fixture",CreatedAt=DateTimeOffset.UtcNow,Version=1,
            CreatedEventId=Guid.NewGuid(),ConstraintRefs=[new("line-4-hours","SUPPLY-CONSTRAINTS","8")],
            Adjustments=[new("line-4","2027-W03","80.000","HOUR")]};
        await db.GetCollection<CapacityScenario>("capacity_scenarios").InsertOneAsync(scenario);
        var evaluation=new CapacityEvaluation {Id=Guid.NewGuid(),TenantId=Scope.TenantId,LegalEntityId=Scope.LegalEntityId,
            CapacityPlanId=planId,ScenarioId=scenarioId,EvaluationMode="Finite",ResourceRefs=["line-4"],
            SubmittedAt=DateTimeOffset.UtcNow,CreatedAt=DateTimeOffset.UtcNow,Version=1,CorrelationId=Guid.NewGuid()};
        return (new CapacityRepository(db,new DemandFixtureReader(),new ConstraintFixtureReader()),evaluation,planId,scenarioId);
    }
    private static async Task<(CapacityLeaseStore store,CapacityEvaluation lease,Guid id)> TerminalSetup(IMongoDatabase db)
    {
        var evaluation=new CapacityEvaluation {Id=Guid.NewGuid(),TenantId=Scope.TenantId,LegalEntityId=Scope.LegalEntityId,
            CapacityPlanId=Guid.NewGuid(),ScenarioId=Guid.NewGuid(),Status="Accepted",EvaluationMode="Finite",ResourceRefs=["line-4"],
            SubmittedAt=DateTimeOffset.UtcNow,CreatedAt=DateTimeOffset.UtcNow,Version=1,CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid()};
        await db.GetCollection<CapacityEvaluation>("capacity_evaluations").InsertOneAsync(evaluation);
        await db.GetCollection<BsonDocument>("capacity_active_slots").InsertOneAsync(new BsonDocument {
            {"_id",Guid.NewGuid().ToString()},{"TenantId",Scope.TenantId.ToString()},{"LegalEntityId",Scope.LegalEntityId.ToString()},
            {"PlanId",evaluation.CapacityPlanId.ToString()},{"ScenarioId",evaluation.ScenarioId.ToString()},{"EvaluationId",evaluation.Id.ToString()}});
        var store=new CapacityLeaseStore(db);var lease=await store.ClaimAsync(Scope,CancellationToken.None);
        Assert.NotNull(lease);
        return (store,lease,evaluation.Id);
    }
    private static async Task<long> Count(IMongoDatabase db,string collection,BsonDocument? filter=null) =>
        await db.GetCollection<BsonDocument>(collection).CountDocumentsAsync(filter??FilterDefinition<BsonDocument>.Empty);
    private static BsonDocument Target(Guid id)=>new("TargetId",id.ToString());
    private static BsonDocument Event(Guid id)=>new("AggregateId",id.ToString());

    [Fact]
    public async Task X01_Accepted_precommit_failure_has_no_partial_writes_then_same_key_recovers()
    {
        const string app="mod192-x01-before";var db=await Reset(app);
        var (repo,evaluation,planId,scenarioId)=await EvaluationSetup(db);
        await Fail(app,"commitTransaction",1);
        CapacityMutation<CapacityEvaluation> result;
        try {result=await repo.EvaluateAsync(Scope,planId,scenarioId,"x01-key","x01-fp",evaluation.CorrelationId,evaluation,CancellationToken.None);}
        finally {Assert.True(await StopFail()>=1);}
        Assert.Equal(503,result.StatusCode);
        Assert.Equal("COMMIT_RESULT_UNRESOLVED",result.ErrorCode);
        Assert.Equal(0,await Count(db,"capacity_evaluations"));Assert.Equal(0,await Count(db,"capacity_active_slots"));
        Assert.Equal(0,await Count(db,"capacity_receipts"));Assert.Equal(0,await Count(db,"capacity_audit"));
        Assert.Equal(0,await Count(db,"capacity_outbox"));
        var recovered=await repo.EvaluateAsync(Scope,planId,scenarioId,"x01-key","x01-fp",evaluation.CorrelationId,evaluation,CancellationToken.None);
        Assert.Equal(202,recovered.StatusCode);Assert.Equal(1,await Count(db,"capacity_evaluations"));
        Assert.Equal(1,await Count(db,"capacity_active_slots"));Assert.Equal(1,await Count(db,"capacity_receipts"));
        Assert.Equal(1,await Count(db,"capacity_audit"));Assert.Equal(0,await Count(db,"capacity_outbox"));
        var replay=await repo.EvaluateAsync(Scope,planId,scenarioId,"x01-key","x01-fp",Guid.NewGuid(),new CapacityEvaluation(),CancellationToken.None);
        Assert.True(replay.Replay);Assert.Equal(202,replay.StatusCode);Assert.Equal(evaluation.Id,replay.Value!.Id);
        Assert.Equal(0,await Count(db,"capacity_receipts",new BsonDocument("TenantId",Guid.NewGuid().ToString())));
    }

    [Fact]
    public async Task X06_Terminal_fault_after_staged_update_and_slot_delete_rolls_back_then_fence_recovers()
    {
        const string app="mod192-x06-insert";var db=await Reset(app);
        var (store,lease,id)=await TerminalSetup(db);
        await Fail(app,"insert",1);
        try {await Assert.ThrowsAnyAsync<MongoException>(()=>store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));}
        finally {Assert.True(await StopFail()>=1);}
        var after=await db.GetCollection<CapacityEvaluation>("capacity_evaluations").Find(x=>x.Id==id).SingleAsync();
        Assert.Equal("Running",after.Status);Assert.Equal(lease.Version,after.Version);Assert.Null(after.CompletedAt);
        Assert.Equal(1,await Count(db,"capacity_active_slots"));
        Assert.Equal(0,await Count(db,"capacity_audit",Target(id)));Assert.Equal(0,await Count(db,"capacity_outbox",Event(id)));
        Assert.True(await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));
        Assert.Equal(0,await Count(db,"capacity_active_slots"));
        Assert.Equal(1,await Count(db,"capacity_audit",Target(id)));
        Assert.Equal(1,await Count(db,"capacity_outbox",Event(id)));
        Assert.Equal(1,await Count(db,"capacity_outbox",new BsonDocument {{"AggregateId",id.ToString()},{"Status","Pending"}}));
        Assert.False(await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));
    }

    [Fact]
    public async Task X07_Unknown_terminal_commit_no_commit_is_reconciled_before_fenced_retry()
    {
        const string app="mod192-x07-before";var db=await Reset(app);
        var (store,lease,id)=await TerminalSetup(db);
        await Fail(app,"commitTransaction",2,unknown:true);
        bool result;
        try {result=await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None);}
        finally {Assert.True(await StopFail()>=1);}
        Assert.False(result);
        var after=await db.GetCollection<CapacityEvaluation>("capacity_evaluations").Find(x=>x.Id==id).SingleAsync();
        Assert.Equal("Running",after.Status);Assert.Equal(1,await Count(db,"capacity_active_slots"));
        Assert.Equal(0,await Count(db,"capacity_audit",Target(id)));Assert.Equal(0,await Count(db,"capacity_outbox",Event(id)));
        Assert.True(await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));
        Assert.Equal(1,await Count(db,"capacity_audit",Target(id)));Assert.Equal(1,await Count(db,"capacity_outbox",Event(id)));
    }

    [Fact]
    public async Task X07_Unknown_terminal_commit_ack_loss_keeps_one_scoped_terminal_effect()
    {
        const string app="mod192-x07-after";var db=await Reset(app);
        var (store,lease,id)=await TerminalSetup(db);
        await Fail(app,"commitTransaction",1,true);
        bool result;
        try {result=await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None);}
        finally {Assert.True(await StopFail()>=1);}
        Assert.True(result);
        var terminal=await db.GetCollection<CapacityEvaluation>("capacity_evaluations").Find(x=>x.Id==id && x.TenantId==Scope.TenantId && x.LegalEntityId==Scope.LegalEntityId).SingleAsync();
        Assert.Equal("Completed",terminal.Status);Assert.Equal(0,await Count(db,"capacity_active_slots"));
        Assert.Equal(1,await Count(db,"capacity_audit",new BsonDocument {{"TenantId",Scope.TenantId.ToString()},{"LegalEntityId",Scope.LegalEntityId.ToString()},{"TargetId",id.ToString()},{"Operation","terminalCapacityEvaluation"}}));
        Assert.Equal(1,await Count(db,"capacity_outbox",new BsonDocument {{"TenantId",Scope.TenantId.ToString()},{"LegalEntityId",Scope.LegalEntityId.ToString()},{"AggregateId",id.ToString()},{"EventType","capacity.evaluation.completed.v1"},{"Status","Pending"}}));
        Assert.False(await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));
        Assert.Equal(1,await Count(db,"capacity_audit",Target(id)));Assert.Equal(1,await Count(db,"capacity_outbox",Event(id)));
    }

    [Fact]
    public async Task X07_Reconciliation_negative_fixture_rejects_missing_or_wrong_terminal_audit()
    {
        var db=await Reset("mod192-x07-audit-mutant");
        var (store,lease,id)=await TerminalSetup(db);
        await db.GetCollection<CapacityEvaluation>("capacity_evaluations").UpdateOneAsync(
            x=>x.Id==id,Builders<CapacityEvaluation>.Update.Set(x=>x.Status,"Completed").Set(x=>x.CompletedAt,DateTime.UtcNow));
        await db.GetCollection<BsonDocument>("capacity_outbox").InsertOneAsync(new BsonDocument {
            {"_id",Guid.NewGuid().ToString()},{"TenantId",Scope.TenantId.ToString()},{"LegalEntityId",Scope.LegalEntityId.ToString()},
            {"AggregateId",id.ToString()},{"EventType","capacity.evaluation.completed.v1"},{"EvaluationStatus","Completed"},
            {"Status","Pending"},{"CorrelationId",lease.CorrelationId.ToString()}});
        var method=typeof(CapacityLeaseStore).GetMethod("ReconcileTerminalAsync",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        async Task<bool> Reconcile()=>await (Task<bool>)method.Invoke(store,[Scope,id,"Completed",CancellationToken.None])!;
        Assert.False(await Reconcile()); // A terminal event without its audit is not a committed four-effect result.
        var audit=new BsonDocument {{"_id",Guid.NewGuid().ToString()},{"TenantId",Scope.TenantId.ToString()},
            {"LegalEntityId",Scope.LegalEntityId.ToString()},{"TargetId",id.ToString()},
            {"Operation","terminalCapacityEvaluation"},{"Status","Completed"},{"CorrelationId",Guid.NewGuid().ToString()}};
        await db.GetCollection<BsonDocument>("capacity_audit").InsertOneAsync(audit);
        Assert.False(await Reconcile()); // Wrong correlation must not satisfy scoped reconciliation.
        await db.GetCollection<BsonDocument>("capacity_audit").UpdateOneAsync(new BsonDocument("_id",audit["_id"]),
            Builders<BsonDocument>.Update.Set("CorrelationId",lease.CorrelationId.ToString()));
        Assert.True(await Reconcile());
    }
}
