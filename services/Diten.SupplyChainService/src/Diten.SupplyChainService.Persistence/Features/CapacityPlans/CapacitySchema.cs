using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Hosting;
namespace Diten.SupplyChainService.Persistence.Features.CapacityPlans;
public sealed class CapacitySchema(IMongoDatabase db) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var hello=await db.RunCommandAsync<BsonDocument>(new BsonDocument("hello",1),cancellationToken:ct);
        if(!hello.Contains("setName") && hello.GetValue("msg","").AsString!="isdbgrid")
            throw new InvalidOperationException("Capacity persistence requires Mongo transactions.");
        async Task Index(string collection,string name,BsonDocument keys,bool unique=false,bool activeOnly=false)
        {
            await db.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(keys,new CreateIndexOptions<BsonDocument> {Name=name,Unique=unique,Collation=Collation.Simple,PartialFilterExpression=activeOnly ? new BsonDocument("IsDeleted",false) : null}),cancellationToken:ct);
        }
        var scope=new BsonDocument{{"TenantId",1},{"LegalEntityId",1}};
        foreach(var collection in new[]{"capacity_plans","capacity_scenarios","capacity_evaluations","capacity_receipts","capacity_active_slots","capacity_audit","capacity_outbox"})
            await Index(collection,"scope",scope);
        await Index("capacity_plans","ux_capacity_plan_active",new BsonDocument{{"TenantId",1},{"LegalEntityId",1},{"HorizonStart",1},{"HorizonEnd",1},{"DemandPlanId",1},{"DemandPlanVersion",1}},true,true);
        await Index("capacity_scenarios","ux_capacity_scenario_name",new BsonDocument{{"TenantId",1},{"LegalEntityId",1},{"CapacityPlanId",1},{"Name",1}},true,true);
        await Index("capacity_active_slots","ux_capacity_active_slot",new BsonDocument{{"TenantId",1},{"LegalEntityId",1},{"PlanId",1},{"ScenarioId",1}},true);
        await Index("capacity_receipts","ux_capacity_receipt",new BsonDocument{{"TenantId",1},{"LegalEntityId",1},{"Operation",1},{"Target",1},{"Key",1}},true);
        await Index("capacity_evaluations","ix_capacity_eval_claim",new BsonDocument{{"TenantId",1},{"LegalEntityId",1},{"Status",1},{"LeaseUntil",1},{"SubmittedAt",1}});
        await Index("capacity_outbox","ux_capacity_terminal_effect",new BsonDocument{{"TenantId",1},{"LegalEntityId",1},{"AggregateId",1},{"EventType",1}},true);
    }
    public Task StopAsync(CancellationToken ct)=>Task.CompletedTask;
}
