using Xunit;using MongoDB.Bson;using MongoDB.Driver;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Tests.SandopPlans;
public sealed class SandopReplayTests
{[Fact]public async Task Exact_key_replays_original_body_without_second_effect_and_changed_payload_conflicts()
{var(h,db,s,f)=await SandopTestHost.Open();var key="key "+Guid.NewGuid();var body=SandopTestHost.Create();var firstCorrelation=Guid.NewGuid();var first=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,key,body,firstCorrelation);Assert.Equal(201,first.Status);
 var count=await db.GetCollection<BsonDocument>("sandop_outbox").CountDocumentsAsync(new BsonDocument("TenantId",s.TenantId.ToString()));Assert.Equal(1,count);
 var empty=new Diten.SupplyChainService.Infrastructure.Features.SandopPlans.DemandFixtureReader([]);var second=await SandopTestHost.Mutate(h,s,empty,SandopAction.Create,null,key,body,Guid.NewGuid());Assert.Equal(201,second.Status);Assert.Equal(first.Body,second.Body);
 Assert.Equal(count,await db.GetCollection<BsonDocument>("sandop_outbox").CountDocumentsAsync(new BsonDocument("TenantId",s.TenantId.ToString())));
 var audit=await db.GetCollection<BsonDocument>("sandop_audit").Find(new BsonDocument("TenantId",s.TenantId.ToString())).FirstAsync();Assert.Equal(firstCorrelation.ToString(),audit["CorrelationId"].AsString);
 var changed=SandopTestHost.J("""{"name":"Other","horizonStart":"2027-01-01","horizonEnd":"2027-12-31","demandPlanId":"dp-2027","demandPlanVersion":"3"}""");var conflict=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,key,changed);Assert.Equal(409,conflict.Status);Assert.Equal("IDEMPOTENCY_KEY_REUSED",conflict.Code);
}

 [Fact]public async Task Replay_survives_new_client_and_does_not_reread_fixture()
 {var(h,db,s,f)=await SandopTestHost.Open();var key="persist-"+Guid.NewGuid();var body=SandopTestHost.Create();var created=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,key,body);Assert.Equal(201,created.Status);
 var uri=Environment.GetEnvironmentVariable("MVP6_MOD0190_MONGO_URI")!;var client=new MongoClient(uri);var replacement=new Diten.SupplyChainService.Persistence.Features.SandopPlans.SandopRepository(client.GetDatabase("DitenSupplyChain_Mod0190_Test"));
 var empty=new Diten.SupplyChainService.Infrastructure.Features.SandopPlans.DemandFixtureReader([]);var replay=await SandopTestHost.Mutate(replacement,s,empty,SandopAction.Create,null,key,body);Assert.Equal(created.Body,replay.Body);Assert.Equal(201,replay.Status);
 var scoped=new BsonDocument("TenantId",s.TenantId.ToString());Assert.Equal(1,await db.GetCollection<BsonDocument>("sandop_receipts").CountDocumentsAsync(scoped));Assert.Equal(1,await db.GetCollection<BsonDocument>("sandop_outbox").CountDocumentsAsync(scoped));
 }
 [Fact]public async Task Exact_key_is_not_trimmed()
 {var(h,db,s,f)=await SandopTestHost.Open();var token="exact-"+Guid.NewGuid();var key=" "+token+" ";var body=SandopTestHost.Create();
 var first=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,key,body);Assert.Equal(201,first.Status);
 var noFixture=new Diten.SupplyChainService.Infrastructure.Features.SandopPlans.DemandFixtureReader([]);
 var replay=await SandopTestHost.Mutate(h,s,noFixture,SandopAction.Create,null,key,body);Assert.Equal(201,replay.Status);Assert.Equal(first.Body,replay.Body);
 foreach(var otherKey in new[]{token," "+token,token+" "})
 {var distinct=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,otherKey,body);
 Assert.Equal(409,distinct.Status);Assert.Equal("SANDOP_PLAN_ALREADY_EXISTS",distinct.Code);}
 var scoped=new BsonDocument{{"TenantId",s.TenantId.ToString()},{"LegalEntityId",s.LegalEntityId.ToString()}};
 var receipt=await db.GetCollection<BsonDocument>("sandop_receipts").Find(scoped).SingleAsync();
 Assert.Equal(key,receipt["Key"].AsString);
 Assert.Equal(1,await db.GetCollection<BsonDocument>("sandop_plans").CountDocumentsAsync(scoped));
 foreach(var name in new[]{"sandop_receipts","sandop_audit","sandop_outbox"})Assert.Equal(1,await db.GetCollection<BsonDocument>(name).CountDocumentsAsync(scoped));
 }


 [Fact]public async Task All_three_mutations_replay_without_duplicate_effects_or_fixture_reread()
 {var(h,db,s,f)=await SandopTestHost.Open();var createBody=SandopTestHost.Create();var createKey="c-"+Guid.NewGuid();var create=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,createKey,createBody);Assert.Equal(201,create.Status);var id=SandopTestHost.Id(create,"sandopPlanId");
 var captureBody=SandopTestHost.Capture();var captureKey="s-"+Guid.NewGuid();var capture=await SandopTestHost.Mutate(h,s,f,SandopAction.Capture,id,captureKey,captureBody);Assert.Equal(201,capture.Status);var snapshotId=SandopTestHost.Id(capture,"snapshotId");
 var signBody=SandopTestHost.J(System.Text.Json.JsonSerializer.Serialize(new{snapshotId,role="Finance",decision="Approved"}));var signKey="o-"+Guid.NewGuid();var sign=await SandopTestHost.Mutate(h,s,f,SandopAction.SignOff,id,signKey,signBody);Assert.Equal(201,sign.Status);
 var noFixture=new Diten.SupplyChainService.Infrastructure.Features.SandopPlans.DemandFixtureReader([]);
 Assert.Equal(create.Body,(await SandopTestHost.Mutate(h,s,noFixture,SandopAction.Create,null,createKey,createBody)).Body);
 Assert.Equal(capture.Body,(await SandopTestHost.Mutate(h,s,noFixture,SandopAction.Capture,id,captureKey,captureBody)).Body);
 Assert.Equal(sign.Body,(await SandopTestHost.Mutate(h,s,noFixture,SandopAction.SignOff,id,signKey,signBody)).Body);
 var scoped=new BsonDocument("TenantId",s.TenantId.ToString());foreach(var name in new[]{"sandop_receipts","sandop_audit","sandop_outbox"})Assert.Equal(3,await db.GetCollection<BsonDocument>(name).CountDocumentsAsync(scoped));
 }
}
