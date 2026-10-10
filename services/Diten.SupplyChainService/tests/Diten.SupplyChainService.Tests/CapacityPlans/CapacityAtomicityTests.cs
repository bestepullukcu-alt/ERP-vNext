using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
using Diten.SupplyChainService.Persistence.Features.CapacityPlans;
using Xunit;
using Xunit.Abstractions;
namespace Diten.SupplyChainService.Tests.CapacityPlans;
[Collection("CapacityMongo")]
public sealed class CapacityAtomicityTests
{
    [Fact]
    public async Task Fixture_mutations_and_fenced_terminal_are_durable_and_single_effect()
    {
        var client=new MongoClient(CapacityTestMongo.Connection);
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
    private readonly ITestOutputHelper output;
    public CapacityFaultTests(ITestOutputHelper output) => this.output=output;
    private static string Connection => CapacityTestMongo.Connection;
    private const string Database = "DitenSupplyChain_Mod0192_Test";
    private static readonly string[] Collections = ["capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"];
    private static readonly CapacityScope Scope = new(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.Parse("19200000-0000-4000-8000-000000000003"));

    private static async Task<IMongoDatabase> Reset(string appName)
    {
        var db=new MongoClient(CapacityTestMongo.WithApplicationName(appName)).GetDatabase(Database);
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
    private static async Task<BsonDocument> FailAfter(string appName,string command,int skip)
    {
        var data=new BsonDocument { {"failCommands",new BsonArray {command}}, {"appName",appName},
            {"errorCode",8}, {"errorLabels",new BsonArray()} };
        return await new MongoClient(Connection).GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument {
            {"configureFailPoint","failCommand"},{"mode",new BsonDocument("skip",skip)},{"data",data}});
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
    private static async Task<bool> Reconcile(CapacityLeaseStore store,Guid id)
    {
        var method=typeof(CapacityLeaseStore).GetMethod("ReconcileTerminalAsync",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        return await (Task<bool>)method.Invoke(store,[Scope,id,"Completed",CancellationToken.None])!;
    }

    [Theory]
    [InlineData("event-missing")]
    [InlineData("event-status")]
    [InlineData("event-correlation")]
    [InlineData("event-causation")]
    [InlineData("event-time")]
    [InlineData("event-payload")]
    [InlineData("event-duplicate")]
    [InlineData("audit-missing")]
    [InlineData("audit-status")]
    [InlineData("audit-correlation")]
    [InlineData("audit-time")]
    [InlineData("audit-duplicate")]
    [InlineData("slot-not-released")]
    public async Task X07_Scoped_terminal_reconciliation_rejects_inexact_effects(string mutant)
    {
        var db=await Reset("mod192-x07-exact-"+mutant);
        var (store,lease,id)=await TerminalSetup(db);
        Assert.True(await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));
        Assert.True(await Reconcile(store,id));
        var events=db.GetCollection<BsonDocument>("capacity_outbox");
        var audits=db.GetCollection<BsonDocument>("capacity_audit");
        var eventRow=await events.Find(Event(id)).SingleAsync();
        var auditRow=await audits.Find(Target(id)).SingleAsync();
        switch(mutant)
        {
            case "event-missing": await events.DeleteOneAsync(Event(id)); break;
            case "event-status": await events.UpdateOneAsync(Event(id),Builders<BsonDocument>.Update.Set("Status","Sent")); break;
            case "event-correlation": await events.UpdateOneAsync(Event(id),Builders<BsonDocument>.Update.Set("CorrelationId",Guid.NewGuid().ToString())); break;
            case "event-causation": await events.UpdateOneAsync(Event(id),Builders<BsonDocument>.Update.Set("CausationId",Guid.NewGuid().ToString())); break;
            case "event-time": await events.UpdateOneAsync(Event(id),Builders<BsonDocument>.Update.Set("OccurredAt",DateTime.UtcNow.AddMinutes(1))); break;
            case "event-payload": await events.UpdateOneAsync(Event(id),Builders<BsonDocument>.Update.Set("Payload.status","Failed")); break;
            // A corrupted fixture must bypass the unique terminal-effect index explicitly.
            // The production index remains a separate first line of defense.
            case "event-duplicate": await events.Indexes.DropOneAsync("ux_capacity_terminal_effect");
                eventRow["_id"]=Guid.NewGuid().ToString();await events.InsertOneAsync(eventRow); break;
            case "audit-missing": await audits.DeleteOneAsync(Target(id)); break;
            case "audit-status": await audits.UpdateOneAsync(Target(id),Builders<BsonDocument>.Update.Set("Status","Failed")); break;
            case "audit-correlation": await audits.UpdateOneAsync(Target(id),Builders<BsonDocument>.Update.Set("CorrelationId",Guid.NewGuid().ToString())); break;
            case "audit-time": await audits.UpdateOneAsync(Target(id),Builders<BsonDocument>.Update.Set("OccurredAt",DateTime.UtcNow.AddMinutes(1))); break;
            case "audit-duplicate": auditRow["_id"]=Guid.NewGuid().ToString();await audits.InsertOneAsync(auditRow); break;
            case "slot-not-released": await db.GetCollection<BsonDocument>("capacity_active_slots").InsertOneAsync(new BsonDocument {
                {"_id",Guid.NewGuid().ToString()},{"TenantId",Scope.TenantId.ToString()},{"LegalEntityId",Scope.LegalEntityId.ToString()},
                {"PlanId",lease.CapacityPlanId.ToString()},{"ScenarioId",lease.ScenarioId.ToString()},{"EvaluationId",id.ToString()}});break;
        }
        Assert.False(await Reconcile(store,id));
    }

    [Fact]
    public async Task X07_Scoped_reconciliation_read_failure_is_not_success()
    {
        const string app="mod192-x07-read-failure";var db=await Reset(app);
        var (store,lease,id)=await TerminalSetup(db);
        Assert.True(await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));
        var before=(await Fail(app,"find",4)).GetValue("count",0).ToInt32();
        try {await Assert.ThrowsAsync<InvalidOperationException>(()=>Reconcile(store,id));}
        finally {var hits=await StopFail()-before;output.WriteLine($"X07 reconciliation read failure hits={hits}");Assert.True(hits>=1);}
    }

    [Theory]
    [InlineData("event-broad",0)]
    [InlineData("event-exact",1)]
    [InlineData("audit-broad",2)]
    [InlineData("audit-exact",3)]
    [InlineData("active-slot",4)]
    public async Task X07_Each_later_reconciliation_read_failure_is_fail_closed(string boundary,int aggregateSkip)
    {
        var app="mod192-x07-later-"+boundary;var db=await Reset(app);
        var (store,lease,id)=await TerminalSetup(db);
        Assert.True(await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));
        Assert.True(await Reconcile(store,id));
        var beforeStatus=(await db.GetCollection<CapacityEvaluation>("capacity_evaluations").Find(x=>x.Id==id).SingleAsync()).Status;
        var beforeEvent=await Count(db,"capacity_outbox",Event(id));
        var beforeAudit=await Count(db,"capacity_audit",Target(id));
        var beforeSlot=await Count(db,"capacity_active_slots");
        var enteredBefore=(await FailAfter(app,"aggregate",aggregateSkip)).GetValue("count",0).ToInt32();
        try {await Assert.ThrowsAsync<InvalidOperationException>(()=>Reconcile(store,id));}
        finally
        {
            var entered=await StopFail()-enteredBefore;
            output.WriteLine($"X07 later read boundary={boundary} aggregateSkip={aggregateSkip} failpointEntries={entered} injectedFailures=1");
            Assert.Equal(1,entered);
        }
        Assert.Equal(beforeStatus,(await db.GetCollection<CapacityEvaluation>("capacity_evaluations").Find(x=>x.Id==id).SingleAsync()).Status);
        Assert.Equal(beforeEvent,await Count(db,"capacity_outbox",Event(id)));
        Assert.Equal(beforeAudit,await Count(db,"capacity_audit",Target(id)));
        Assert.Equal(beforeSlot,await Count(db,"capacity_active_slots"));
        Assert.True(await Reconcile(store,id));
    }

    [Fact]
    public async Task X01_Unknown_commit_without_receipt_stays_unresolved()
    {
        const string app="mod192-x01-unknown";var db=await Reset(app);
        var (repo,evaluation,planId,scenarioId)=await EvaluationSetup(db);
        var before=(await Fail(app,"commitTransaction",2,unknown:true)).GetValue("count",0).ToInt32();
        CapacityMutation<CapacityEvaluation> result;
        try {result=await repo.EvaluateAsync(Scope,planId,scenarioId,"x01-unknown-key","x01-unknown-fp",evaluation.CorrelationId,evaluation,CancellationToken.None);}
        finally {var hits=await StopFail()-before;output.WriteLine($"X01 uncertain commit hits={hits}");Assert.True(hits>=1);}
        Assert.Equal(503,result.StatusCode);Assert.Equal("COMMIT_RESULT_UNRESOLVED",result.ErrorCode);
        Assert.Equal(0,await Count(db,"capacity_evaluations"));Assert.Equal(0,await Count(db,"capacity_active_slots"));
        Assert.Equal(0,await Count(db,"capacity_receipts"));Assert.Equal(0,await Count(db,"capacity_audit"));Assert.Equal(0,await Count(db,"capacity_outbox"));
    }

    [Fact]
    public async Task X01_Accepted_precommit_failure_has_no_partial_writes_then_same_key_recovers()
    {
        const string app="mod192-x01-before";var db=await Reset(app);
        var (repo,evaluation,planId,scenarioId)=await EvaluationSetup(db);
        var before=(await Fail(app,"commitTransaction",1)).GetValue("count",0).ToInt32();
        CapacityMutation<CapacityEvaluation> result;
        try {result=await repo.EvaluateAsync(Scope,planId,scenarioId,"x01-key","x01-fp",evaluation.CorrelationId,evaluation,CancellationToken.None);}
        finally {var hits=await StopFail()-before;output.WriteLine($"X01 definite commit cut hits={hits}");Assert.True(hits>=1);}
        Assert.Equal(503,result.StatusCode);
        Assert.Equal("DEPENDENCY_UNAVAILABLE",result.ErrorCode);
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
    public async Task X01_Accepted_commit_ack_loss_recovers_original_scoped_receipt()
    {
        const string app="mod192-x01-after";var db=await Reset(app);
        var (repo,evaluation,planId,scenarioId)=await EvaluationSetup(db);
        var before=(await Fail(app,"commitTransaction",1,true)).GetValue("count",0).ToInt32();
        CapacityMutation<CapacityEvaluation> result;
        try {result=await repo.EvaluateAsync(Scope,planId,scenarioId,"x01-after-key","x01-after-fp",evaluation.CorrelationId,evaluation,CancellationToken.None);}
        finally {var hits=await StopFail()-before;output.WriteLine($"X01 commit acknowledgment loss hits={hits}");Assert.True(hits>=1);}
        Assert.Equal(202,result.StatusCode);Assert.Equal(evaluation.Id,result.Value!.Id);
        Assert.Equal(1,await Count(db,"capacity_evaluations"));Assert.Equal(1,await Count(db,"capacity_active_slots"));
        Assert.Equal(1,await Count(db,"capacity_receipts"));Assert.Equal(1,await Count(db,"capacity_audit"));
        Assert.Equal(0,await Count(db,"capacity_outbox"));
        var replay=await repo.EvaluateAsync(Scope,planId,scenarioId,"x01-after-key","x01-after-fp",Guid.NewGuid(),new CapacityEvaluation(),CancellationToken.None);
        Assert.True(replay.Replay);Assert.Equal(202,replay.StatusCode);Assert.Equal(evaluation.Id,replay.Value!.Id);
        Assert.Equal(1,await Count(db,"capacity_receipts",new BsonDocument {{"TenantId",Scope.TenantId.ToString()},
            {"LegalEntityId",Scope.LegalEntityId.ToString()},{"Operation","evaluateCapacityScenario"},{"Key","x01-after-key"}}));
    }

    [Fact]
    public async Task X06_Terminal_fault_after_staged_update_and_slot_delete_rolls_back_then_fence_recovers()
    {
        const string app="mod192-x06-insert";var db=await Reset(app);
        var (store,lease,id)=await TerminalSetup(db);
        var before=(await Fail(app,"insert",1)).GetValue("count",0).ToInt32();
        try {await Assert.ThrowsAnyAsync<MongoException>(()=>store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));}
        finally {var hits=await StopFail()-before;output.WriteLine($"X06 definite audit-insert cut hits={hits}");Assert.True(hits>=1);}
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
        var before=(await Fail(app,"commitTransaction",2,unknown:true)).GetValue("count",0).ToInt32();
        bool result;
        try {result=await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None);}
        finally {var hits=await StopFail()-before;output.WriteLine($"X07 uncommitted unknown result hits={hits}");Assert.True(hits>=1);}
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
        var before=(await Fail(app,"commitTransaction",1,true)).GetValue("count",0).ToInt32();
        bool result;
        try {result=await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None);}
        finally {var hits=await StopFail()-before;output.WriteLine($"X07 committed acknowledgment loss hits={hits}");Assert.True(hits>=1);}
        Assert.True(result);
        var terminal=await db.GetCollection<CapacityEvaluation>("capacity_evaluations").Find(x=>x.Id==id && x.TenantId==Scope.TenantId && x.LegalEntityId==Scope.LegalEntityId).SingleAsync();
        Assert.Equal("Completed",terminal.Status);Assert.Equal(0,await Count(db,"capacity_active_slots"));
        Assert.Equal(1,await Count(db,"capacity_audit",new BsonDocument {{"TenantId",Scope.TenantId.ToString()},{"LegalEntityId",Scope.LegalEntityId.ToString()},{"TargetId",id.ToString()},{"Operation","terminalCapacityEvaluation"}}));
        Assert.Equal(1,await Count(db,"capacity_outbox",new BsonDocument {{"TenantId",Scope.TenantId.ToString()},{"LegalEntityId",Scope.LegalEntityId.ToString()},{"AggregateId",id.ToString()},{"EventType","capacity.evaluation.completed.v1"},{"Status","Pending"}}));
        Assert.False(await store.TerminalAsync(Scope,id,lease.Version,lease.Fence,"Completed",[],false,CancellationToken.None));
        Assert.Equal(1,await Count(db,"capacity_audit",Target(id)));Assert.Equal(1,await Count(db,"capacity_outbox",Event(id)));
    }

}
