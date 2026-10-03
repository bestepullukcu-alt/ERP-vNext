using System.Collections.Concurrent;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
using Diten.SupplyChainService.Persistence.Features.CapacityPlans;
using Xunit;
namespace Diten.SupplyChainService.Tests.CapacityPlans;
[Collection("CapacityMongo")]
public sealed class CapacityConcurrencyTests
{
    private static string Connection => CapacityTestMongo.Connection;
    private const string Database = "DitenSupplyChain_Mod0192_Test";

    [Fact]
    public async Task Proven_name_index_race_loser_converges_to_name_conflict_without_extra_write_set()
    {
        var plainDb=new MongoClient(Connection).GetDatabase(Database);
        foreach(var name in new[]{"capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"})
            await plainDb.DropCollectionAsync(name);
        await new CapacitySchema(plainDb).StartAsync(CancellationToken.None);
        var scope=new CapacityScope(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.NewGuid());
        var plainRepo=new CapacityRepository(plainDb,new DemandFixtureReader(),new ConstraintFixtureReader());
        var plan=Plan(scope,"Race plan",2027);
        Assert.Equal(201,(await plainRepo.CreatePlanAsync(scope,"plan","p",Guid.NewGuid(),plan,CancellationToken.None)).StatusCode);

        var winner=Scenario(scope,plan.Id,Guid.NewGuid(),"Exact name");
        var loser=Scenario(scope,plan.Id,Guid.NewGuid(),"Exact name");
        var watched=new ConcurrentDictionary<int,byte>();
        var scenarioFinds=0;
        CapacityMutation<CapacityScenario>? winnerResult=null;
        var settings=MongoClientSettings.FromConnectionString(Connection);
        settings.ClusterConfigurator=cb=>
        {
            cb.Subscribe<CommandStartedEvent>(e=>
            {
                if(e.CommandName=="find" && e.Command.TryGetValue("find",out var collection) && collection.AsString=="capacity_scenarios")
                    watched.TryAdd(e.RequestId,0);
            });
            cb.Subscribe<CommandSucceededEvent>(e=>
            {
                if(!watched.TryRemove(e.RequestId,out _) || Interlocked.Increment(ref scenarioFinds)!=1) return;
                winnerResult=plainRepo.CreateScenarioAsync(scope,plan.Id,"winner","winner-fp",Guid.NewGuid(),winner,CancellationToken.None).GetAwaiter().GetResult();
            });
        };
        var observedRepo=new CapacityRepository(new MongoClient(settings).GetDatabase(Database),new DemandFixtureReader(),new ConstraintFixtureReader());
        var result=await observedRepo.CreateScenarioAsync(scope,plan.Id,"loser","loser-fp",Guid.NewGuid(),loser,CancellationToken.None);

        Assert.NotNull(winnerResult);Assert.Equal(201,winnerResult!.StatusCode);
        Assert.Equal(409,result.StatusCode);Assert.Equal("CAPACITY_SCENARIO_NAME_CONFLICT",result.ErrorCode);
        Assert.True(scenarioFinds>=2); // The first snapshot was empty; retry observed the committed exact-name winner.
        Assert.Equal(1,await plainDb.GetCollection<CapacityScenario>("capacity_scenarios").CountDocumentsAsync(FilterDefinition<CapacityScenario>.Empty));
        Assert.Equal(2,await plainDb.GetCollection<BsonDocument>("capacity_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(2,await plainDb.GetCollection<BsonDocument>("capacity_audit").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(2,await plainDb.GetCollection<BsonDocument>("capacity_outbox").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task Duplicate_policy_preserves_replay_precedence_scope_and_non_name_duplicate_failures()
    {
        var db=new MongoClient(Connection).GetDatabase(Database);
        foreach(var name in new[]{"capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"})
            await db.DropCollectionAsync(name);
        await new CapacitySchema(db).StartAsync(CancellationToken.None);
        var scope=new CapacityScope(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.NewGuid());
        var repo=new CapacityRepository(db,new DemandFixtureReader(),new ConstraintFixtureReader());
        var firstPlan=Plan(scope,"First",2027);var secondPlan=Plan(scope,"Second",2028);
        Assert.Equal(201,(await repo.CreatePlanAsync(scope,"plan-1","p1",Guid.NewGuid(),firstPlan,CancellationToken.None)).StatusCode);
        await db.GetCollection<CapacityPlan>("capacity_plans").InsertOneAsync(secondPlan);
        var original=Scenario(scope,firstPlan.Id,Guid.NewGuid(),"Shift");
        Assert.Equal(201,(await repo.CreateScenarioAsync(scope,firstPlan.Id,"scenario","same-fp",Guid.NewGuid(),original,CancellationToken.None)).StatusCode);
        var replay=await repo.CreateScenarioAsync(scope,firstPlan.Id,"scenario","same-fp",Guid.NewGuid(),original,CancellationToken.None);
        Assert.Equal(201,replay.StatusCode);Assert.True(replay.Replay);Assert.Equal(original.Id,replay.Value!.Id);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED",(await repo.CreateScenarioAsync(scope,firstPlan.Id,"scenario","changed-fp",Guid.NewGuid(),original,CancellationToken.None)).ErrorCode);
        Assert.Equal("CAPACITY_SCENARIO_NAME_CONFLICT",(await repo.CreateScenarioAsync(scope,firstPlan.Id,"exact-duplicate","exact-fp",Guid.NewGuid(),Scenario(scope,firstPlan.Id,Guid.NewGuid(),"Shift"),CancellationToken.None)).ErrorCode);
        Assert.Equal(201,(await repo.CreateScenarioAsync(scope,firstPlan.Id,"different-case","case-fp",Guid.NewGuid(),Scenario(scope,firstPlan.Id,Guid.NewGuid(),"shift"),CancellationToken.None)).StatusCode);
        Assert.Equal(201,(await repo.CreateScenarioAsync(scope,firstPlan.Id,"different-space","space-fp",Guid.NewGuid(),Scenario(scope,firstPlan.Id,Guid.NewGuid()," Shift"),CancellationToken.None)).StatusCode);
        Assert.Equal(201,(await repo.CreateScenarioAsync(scope,secondPlan.Id,"different-plan","plan-fp",Guid.NewGuid(),Scenario(scope,secondPlan.Id,Guid.NewGuid(),"Shift"),CancellationToken.None)).StatusCode);
        await db.GetCollection<CapacityPlan>("capacity_plans").UpdateOneAsync(Builders<CapacityPlan>.Filter.Eq(x=>x.Id,firstPlan.Id),Builders<CapacityPlan>.Update.Set(x=>x.Status,"Approved"));
        Assert.Equal("CAPACITY_PLAN_STATE_CONFLICT",(await repo.CreateScenarioAsync(scope,firstPlan.Id,"state-first","state-fp",Guid.NewGuid(),Scenario(scope,firstPlan.Id,Guid.NewGuid(),"Shift"),CancellationToken.None)).ErrorCode);
        await db.GetCollection<CapacityPlan>("capacity_plans").UpdateOneAsync(Builders<CapacityPlan>.Filter.Eq(x=>x.Id,firstPlan.Id),Builders<CapacityPlan>.Update.Set(x=>x.Status,"Draft"));
        var invalidFixture=Scenario(scope,firstPlan.Id,Guid.NewGuid(),"Shift");invalidFixture.ConstraintRefs=[new("unknown","SUPPLY-CONSTRAINTS","8")];
        Assert.Equal("INVALID_CONSTRAINT_REFERENCE",(await repo.CreateScenarioAsync(scope,firstPlan.Id,"fixture-first","fixture-fp",Guid.NewGuid(),invalidFixture,CancellationToken.None)).ErrorCode);
        var unrelatedDuplicate=Scenario(scope,firstPlan.Id,original.Id,"Different name");
        var unrelated=await repo.CreateScenarioAsync(scope,firstPlan.Id,"duplicate-id","duplicate-id-fp",Guid.NewGuid(),unrelatedDuplicate,CancellationToken.None);
        Assert.Equal(503,unrelated.StatusCode);Assert.NotEqual("CAPACITY_SCENARIO_NAME_CONFLICT",unrelated.ErrorCode);
        await db.GetCollection<CapacityScenario>("capacity_scenarios").InsertOneAsync(Scenario(new CapacityScope(Guid.NewGuid(),scope.LegalEntityId,scope.ActorId),firstPlan.Id,Guid.NewGuid(),"Shift"));
        await db.GetCollection<CapacityScenario>("capacity_scenarios").InsertOneAsync(Scenario(new CapacityScope(scope.TenantId,Guid.NewGuid(),scope.ActorId),firstPlan.Id,Guid.NewGuid(),"Shift"));
    }

    private static CapacityPlan Plan(CapacityScope scope,string name,int year)=>new() {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
        Name=name,HorizonStart=new DateOnly(year,1,1),HorizonEnd=new DateOnly(year,12,31),DemandPlanId="dp-2027",DemandPlanVersion="3",
        SourceCapturedAt=DateTimeOffset.UtcNow,SourceChecksum="sha256:ee56d4f9a3c8",CreatedAt=DateTimeOffset.UtcNow,Version=1,CreatedBy=scope.ActorId};
    private static CapacityScenario Scenario(CapacityScope scope,Guid planId,Guid id,string name)=>new() {Id=id,TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
        CapacityPlanId=planId,Name=name,CreatedAt=DateTimeOffset.UtcNow,Version=1,CreatedBy=scope.ActorId,
        ConstraintRefs=[new("line-4-hours","SUPPLY-CONSTRAINTS","8")],Adjustments=[new("line-4","2027-W03","80.000","HOUR")]};

    [Fact]
    public async Task Twenty_distinct_keys_produce_one_active_evaluation()
    {
        var db=new MongoClient(Connection).GetDatabase(Database);
        foreach(var name in new[]{"capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"})
            await db.DropCollectionAsync(name);
        await new CapacitySchema(db).StartAsync(CancellationToken.None);
        var scope=new CapacityScope(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.NewGuid());
        var repo=new CapacityRepository(db,new DemandFixtureReader(),new ConstraintFixtureReader());
        var plan=new CapacityPlan {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            Name="Race plan",HorizonStart=new DateOnly(2027,1,1),HorizonEnd=new DateOnly(2027,12,31),
            DemandPlanId="dp-2027",DemandPlanVersion="3",SourceCapturedAt=DateTimeOffset.UtcNow,
            SourceChecksum="sha256:ee56d4f9a3c8",CreatedAt=DateTimeOffset.UtcNow,Version=1,CreatedBy=scope.ActorId};
        Assert.Equal(201,(await repo.CreatePlanAsync(scope,"plan","p",Guid.NewGuid(),plan,CancellationToken.None)).StatusCode);
        var scenario=new CapacityScenario {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            CapacityPlanId=plan.Id,Name="Race scenario",CreatedAt=DateTimeOffset.UtcNow,Version=1,CreatedBy=scope.ActorId,
            ConstraintRefs=[new("line-4-hours","SUPPLY-CONSTRAINTS","8")],Adjustments=[new("line-4","2027-W03","80.000","HOUR")]};
        Assert.Equal(201,(await repo.CreateScenarioAsync(scope,plan.Id,"scenario","s",Guid.NewGuid(),scenario,CancellationToken.None)).StatusCode);
        var requests=Enumerable.Range(0,20).Select(async i =>
        {
            var eval=new CapacityEvaluation {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                CapacityPlanId=plan.Id,ScenarioId=scenario.Id,EvaluationMode="Finite",ResourceRefs=["line-4"],
                SubmittedAt=DateTimeOffset.UtcNow,CreatedAt=DateTimeOffset.UtcNow,Version=1,CorrelationId=Guid.NewGuid()};
            return await repo.EvaluateAsync(scope,plan.Id,scenario.Id,$"key-{i}",$"fingerprint-{i}",eval.CorrelationId,eval,CancellationToken.None);
        });
        var results=await Task.WhenAll(requests);
        Assert.Single(results,x=>x.StatusCode==202);
        Assert.Equal(19,results.Count(x=>x.StatusCode==409 && x.ErrorCode=="EVALUATION_ALREADY_ACTIVE"));
        Assert.Equal(1,await db.GetCollection<BsonDocument>("capacity_active_slots").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(1,await db.GetCollection<CapacityEvaluation>("capacity_evaluations").CountDocumentsAsync(FilterDefinition<CapacityEvaluation>.Empty));
        Assert.Equal(3,await db.GetCollection<BsonDocument>("capacity_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }
}
