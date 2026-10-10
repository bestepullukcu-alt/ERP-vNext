using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.CapacityPlans;
using Diten.SupplyChainService.Infrastructure.Features.CapacityPlans;
using Diten.SupplyChainService.Persistence.Features.CapacityPlans;
using Xunit;
namespace Diten.SupplyChainService.Tests.CapacityPlans;
[Collection("CapacityMongo")]
public sealed class CapacityLeaseTests
{
    [Fact]
    public async Task Renewal_advances_version_without_attempt_and_rejects_old_terminal()
    {
        var db=new MongoClient(CapacityTestMongo.Connection)
            .GetDatabase("DitenSupplyChain_Mod0192_Test");
        foreach(var name in new[]{"capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"})
            await db.DropCollectionAsync(name);
        await new CapacitySchema(db).StartAsync(CancellationToken.None);
        var scope=new CapacityScope(DemandFixtureReader.Tenant,DemandFixtureReader.LegalEntity,Guid.NewGuid());
        var evaluation=new CapacityEvaluation {Id=Guid.NewGuid(),TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
            CapacityPlanId=Guid.NewGuid(),ScenarioId=Guid.NewGuid(),Status="Accepted",EvaluationMode="Finite",ResourceRefs=["line-4"],
            SubmittedAt=DateTimeOffset.UtcNow,CreatedAt=DateTimeOffset.UtcNow,Version=1,CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid()};
        await db.GetCollection<CapacityEvaluation>("capacity_evaluations").InsertOneAsync(evaluation);
        await db.GetCollection<BsonDocument>("capacity_active_slots").InsertOneAsync(new BsonDocument {
            {"_id",Guid.NewGuid().ToString()},{"TenantId",scope.TenantId.ToString()},{"LegalEntityId",scope.LegalEntityId.ToString()},
            {"PlanId",evaluation.CapacityPlanId.ToString()},{"ScenarioId",evaluation.ScenarioId.ToString()},
            {"EvaluationId",evaluation.Id.ToString()} });
        var leases=new CapacityLeaseStore(db);
        var first=(await leases.ClaimAsync(scope,CancellationToken.None))!;
        Assert.NotNull(first);
        var renewed=(await leases.RenewAsync(scope,first,CancellationToken.None))!;
        Assert.NotNull(renewed);Assert.Equal(1,renewed.Attempt);Assert.Equal(first.Fence,renewed.Fence);
        Assert.True(renewed.Version>first.Version);
        Assert.False(await leases.TerminalAsync(scope,first.Id,first.Version,first.Fence,"Failed",[],false,CancellationToken.None));
        Assert.True(await leases.TerminalAsync(scope,renewed.Id,renewed.Version,renewed.Fence,"Failed",[],false,CancellationToken.None));
        Assert.Equal(1,await db.GetCollection<BsonDocument>("capacity_outbox").CountDocumentsAsync(new BsonDocument("EventType","capacity.evaluation.completed.v1")));
        Assert.Equal(0,await db.GetCollection<BsonDocument>("capacity_active_slots").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }
}
