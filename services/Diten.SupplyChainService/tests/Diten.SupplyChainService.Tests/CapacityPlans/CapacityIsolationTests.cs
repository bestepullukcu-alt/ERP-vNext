using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
using Diten.SupplyChainService.Persistence.Features.CapacityPlans;
using Xunit;
namespace Diten.SupplyChainService.Tests.CapacityPlans;
[Collection("CapacityMongo")]
public sealed class CapacityIsolationTests
{
    [Fact]
    public async Task Invalid_fixture_and_foreign_or_deleted_scope_create_no_visible_data()
    {
        var db=new MongoClient(CapacityTestMongo.Connection)
            .GetDatabase("DitenSupplyChain_Mod0192_Test");
        foreach(var name in new[]{"capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"})
            await db.DropCollectionAsync(name);
        await new CapacitySchema(db).StartAsync(CancellationToken.None);
        var scope=new CapacityScope(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.NewGuid());
        var repo=new CapacityRepository(db,new DemandFixtureReader(),new ConstraintFixtureReader());
        CapacityPlan Plan(string checksum) => new() {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            Name="Exact fixture",HorizonStart=new DateOnly(2027,1,1),HorizonEnd=new DateOnly(2027,12,31),
            DemandPlanId="dp-2027",DemandPlanVersion="3",SourceCapturedAt=DateTimeOffset.UtcNow,
            SourceChecksum=checksum,CreatedAt=DateTimeOffset.UtcNow,Version=1,CreatedBy=scope.ActorId};
        var rejected=await repo.CreatePlanAsync(scope,"wrong","wrong",Guid.NewGuid(),Plan("sha256:wrong"),CancellationToken.None);
        Assert.Equal("INVALID_DEMAND_REFERENCE",rejected.ErrorCode);
        Assert.Equal(0,await db.GetCollection<BsonDocument>("capacity_receipts").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        Assert.Equal(0,await db.GetCollection<BsonDocument>("capacity_outbox").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        var plan=Plan("sha256:ee56d4f9a3c8");
        Assert.Equal(201,(await repo.CreatePlanAsync(scope,"right","right",Guid.NewGuid(),plan,CancellationToken.None)).StatusCode);
        var foreignTenant=new CapacityScope(Guid.NewGuid(),scope.LegalEntityId,scope.ActorId);
        var foreignLe=new CapacityScope(scope.TenantId,Guid.NewGuid(),scope.ActorId);
        Assert.Null(await repo.GetPlanAsync(foreignTenant,plan.Id,CancellationToken.None));
        Assert.Null(await repo.GetPlanAsync(foreignLe,plan.Id,CancellationToken.None));
        var invalidScenario=new CapacityScenario {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            CapacityPlanId=plan.Id,Name="Bad UoM",CreatedAt=DateTimeOffset.UtcNow,Version=1,CreatedBy=scope.ActorId,
            ConstraintRefs=[new("line-4-hours","SUPPLY-CONSTRAINTS","8")],Adjustments=[new("line-4","2027-W03","80.000","DAY")]};
        Assert.Equal("INVALID_CONSTRAINT_REFERENCE",(await repo.CreateScenarioAsync(scope,plan.Id,"bad","bad",Guid.NewGuid(),invalidScenario,CancellationToken.None)).ErrorCode);
        Assert.Equal(0,await db.GetCollection<CapacityScenario>("capacity_scenarios").CountDocumentsAsync(FilterDefinition<CapacityScenario>.Empty));
        var raw=await db.GetCollection<BsonDocument>("capacity_plans").Find(new BsonDocument()).FirstAsync();
        Assert.False(raw.Contains("DemandQuantity"));Assert.False(raw.Contains("ForecastSeries"));
        await db.GetCollection<CapacityPlan>("capacity_plans").UpdateOneAsync(x=>x.Id==plan.Id,Builders<CapacityPlan>.Update.Set(x=>x.IsDeleted,true));
        Assert.Null(await repo.GetPlanAsync(scope,plan.Id,CancellationToken.None));
    }
}
