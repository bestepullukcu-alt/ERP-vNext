using Xunit;using MongoDB.Bson;using MongoDB.Driver;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Tests.SandopPlans;
[CollectionDefinition("SandopMongoFailpoint", DisableParallelization = true)]
public sealed class SandopMongoFailpointCollection;

[Collection("SandopMongoFailpoint")]
public sealed class SandopAtomicityTests
{
 private static readonly string[] Effects = ["sandop_plans","sandop_snapshots","sandop_receipts","sandop_audit","sandop_outbox"];
 private static BsonDocument Scoped(SandopScope s)=>new(){{"TenantId",s.TenantId.ToString()},{"LegalEntityId",s.LegalEntityId.ToString()}};
 private static async Task<long[]> Counts(IMongoDatabase db,SandopScope s)=>await Task.WhenAll(Effects.Select(n=>db.GetCollection<BsonDocument>(n).CountDocumentsAsync(Scoped(s))));
 private static Task<BsonDocument> Configure(IMongoDatabase db,BsonDocument data,int times=1)=>db.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument{{"configureFailPoint","failCommand"},{"mode",new BsonDocument("times",times)},{"data",data}});
 private static Task<BsonDocument> Disable(IMongoDatabase db)=>db.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument{{"configureFailPoint","failCommand"},{"mode","off"}});
 private static IMongoDatabase FreshDatabase()
 {var url=new MongoUrlBuilder(Environment.GetEnvironmentVariable("MVP6_MOD0190_MONGO_URI")!){DirectConnection=true,ReplicaSetName=null,ApplicationName="mod0190-recovery-"+Guid.NewGuid()};
 return new MongoClient(url.ToMongoUrl()).GetDatabase("DitenSupplyChain_Mod0190_Test");}

 [Fact]public async Task Invalid_fixture_creates_nothing_in_five_effect_collections()
{var(h,db,s,f)=await SandopTestHost.Open();var bad=SandopTestHost.J("""{"name":"bad","horizonStart":"2027-01-01","horizonEnd":"2027-12-31","demandPlanId":"missing","demandPlanVersion":"3"}""");
 var result=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,Guid.NewGuid().ToString(),bad);Assert.Equal(422,result.Status);Assert.Equal("INVALID_DEMAND_REFERENCE",result.Code);
 foreach(var name in new[]{"sandop_plans","sandop_snapshots","sandop_receipts","sandop_audit","sandop_outbox"})Assert.Equal(0,await db.GetCollection<BsonDocument>(name).CountDocumentsAsync(new BsonDocument("TenantId",s.TenantId.ToString())));
}

 [Fact]public async Task Checksum_mismatch_produces_422_and_no_capture_effects()
 {var(h,db,s,f)=await SandopTestHost.Open();var created=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,Guid.NewGuid().ToString(),SandopTestHost.Create());Assert.Equal(201,created.Status);var id=SandopTestHost.Id(created,"sandopPlanId");
 var scoped=new BsonDocument("TenantId",s.TenantId.ToString());var names=new[]{"sandop_plans","sandop_snapshots","sandop_receipts","sandop_audit","sandop_outbox"};var before=await Task.WhenAll(names.Select(n=>db.GetCollection<BsonDocument>(n).CountDocumentsAsync(scoped)));
 var wrong=SandopTestHost.J("""{"demandPlanId":"dp-2027","demandPlanVersion":"3","sourceCapturedAt":"2026-09-15T09:45:00Z","sourceChecksum":"sha256:wrong"}""");var result=await SandopTestHost.Mutate(h,s,f,SandopAction.Capture,id,Guid.NewGuid().ToString(),wrong);Assert.Equal(422,result.Status);Assert.Equal("INVALID_DEMAND_REFERENCE",result.Code);
 var after=await Task.WhenAll(names.Select(n=>db.GetCollection<BsonDocument>(n).CountDocumentsAsync(scoped)));Assert.Equal(before,after);
 }
 [Fact]public async Task Unavailable_dependency_returns_declared_503()
 {var(h,db,s,f)=await SandopTestHost.Open();var unreachable=new MongoClient("mongodb://127.0.0.1:57191/?serverSelectionTimeoutMS=150");var repo=new Diten.SupplyChainService.Persistence.Features.SandopPlans.SandopRepository(unreachable.GetDatabase("DitenSupplyChain_Mod0190_Test"));var result=await SandopTestHost.Mutate(repo,s,f,SandopAction.Create,null,Guid.NewGuid().ToString(),SandopTestHost.Create());Assert.Equal(503,result.Status);Assert.Equal("DEPENDENCY_UNAVAILABLE",result.Code);}

 [Fact]public async Task Receipt_insert_failure_after_plan_write_rolls_back_and_same_key_retries()
 {var(h,db,s,f)=await SandopTestHost.Open();var body=SandopTestHost.Create();var key="postwrite-"+Guid.NewGuid();var before=await Counts(db,s);
 var data=new BsonDocument{{"failCommands",new BsonArray{"insert"}},{"namespace","DitenSupplyChain_Mod0190_Test.sandop_receipts"},{"errorCode",2}};
 var configured=await Configure(db,data);BsonDocument disabled;SandopResult failed;
 try{failed=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,key,body);}
 finally{disabled=await Disable(db);}
 Assert.True(disabled["count"].ToInt32()>configured["count"].ToInt32());Assert.Equal(503,failed.Status);Assert.Equal("DEPENDENCY_UNAVAILABLE",failed.Code);
 Assert.Equal(before,await Counts(db,s));
 var retried=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,key,body);Assert.Equal(201,retried.Status);
 Assert.Equal(new long[]{1,0,1,1,1},await Counts(db,s));
 }

 [Fact]public async Task Mongo_unique_index_rejects_definite_duplicate_plan_without_partial_effect()
 {var(h,db,s,f)=await SandopTestHost.Open();var body=SandopTestHost.Create();var first=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,"unique-"+Guid.NewGuid(),body);Assert.Equal(201,first.Status);
 var plans=db.GetCollection<BsonDocument>("sandop_plans");var original=await plans.Find(Scoped(s)).SingleAsync();var duplicate=original.DeepClone().AsBsonDocument;duplicate["_id"]=Guid.NewGuid().ToString();
 var ex=await Assert.ThrowsAsync<MongoWriteException>(()=>plans.InsertOneAsync(duplicate));Assert.Equal(ServerErrorCategory.DuplicateKey,ex.WriteError.Category);
 var second=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,"another-"+Guid.NewGuid(),body);Assert.Equal(409,second.Status);Assert.Equal("SANDOP_PLAN_ALREADY_EXISTS",second.Code);
 Assert.Equal(new long[]{1,0,1,1,1},await Counts(db,s));
 }

 [Fact]public async Task Unknown_commit_without_receipt_returns_unresolved_then_exact_key_recovers()
 {var(h,db,s,f)=await SandopTestHost.Open();var body=SandopTestHost.Create();var key="unknown-"+Guid.NewGuid();
 var data=new BsonDocument{{"failCommands",new BsonArray{"commitTransaction"}},{"errorCode",91},{"errorLabels",new BsonArray{"UnknownTransactionCommitResult"}}};
 var configured=await Configure(db,data,3);BsonDocument disabled;SandopResult uncertain;
 try{uncertain=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,key,body);}
 finally{disabled=await Disable(db);}
 Assert.True(disabled["count"].ToInt32()>configured["count"].ToInt32());Assert.Equal(503,uncertain.Status);Assert.Equal("COMMIT_RESULT_UNRESOLVED",uncertain.Code);
 // A failed commit can leave an inactive server transaction. Resolve the isolated test session before retrying.
 await db.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("killAllSessions",new BsonArray()));
 var recoveredDb=FreshDatabase();
 Assert.Equal(new long[]{0,0,0,0,0},await Counts(recoveredDb,s));
 var fresh=new Diten.SupplyChainService.Persistence.Features.SandopPlans.SandopRepository(recoveredDb);
 var recovered=await SandopTestHost.Mutate(fresh,s,f,SandopAction.Create,null,key,body);Assert.True(recovered.Status==201,$"Recovery returned {recovered.Status} {recovered.Code}");
 var replay=await SandopTestHost.Mutate(fresh,s,new Diten.SupplyChainService.Infrastructure.Features.SandopPlans.DemandFixtureReader([]),SandopAction.Create,null,key,body);Assert.Equal(201,replay.Status);Assert.Equal(recovered.Body,replay.Body);
 Assert.Equal(new long[]{1,0,1,1,1},await Counts(recoveredDb,s));
 }

 [Fact]public async Task Committed_write_concern_uncertainty_resolves_original_receipt()
 {var(h,db,s,f)=await SandopTestHost.Open();var body=SandopTestHost.Create();var key="committed-"+Guid.NewGuid();var originalCorrelation=Guid.NewGuid();
 var data=new BsonDocument{{"failCommands",new BsonArray{"commitTransaction"}},{"writeConcernError",new BsonDocument{{"code",91},{"errmsg","injected commit acknowledgement loss"}}},{"errorLabels",new BsonArray{"UnknownTransactionCommitResult"}}};
 var configured=await Configure(db,data);BsonDocument disabled;SandopResult resolved;
 try{resolved=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,key,body,originalCorrelation);}
 finally{disabled=await Disable(db);}
 Assert.True(disabled["count"].ToInt32()>configured["count"].ToInt32());
 Assert.True(resolved.Status is 201 or 503,$"Expected visible receipt replay or unresolved commit, got {resolved.Status} {resolved.Code}");
 if(resolved.Status==503)Assert.Equal("COMMIT_RESULT_UNRESOLVED",resolved.Code);
 var recoveredDb=FreshDatabase();
 var receipt=await recoveredDb.GetCollection<BsonDocument>("sandop_receipts").Find(Scoped(s)).SingleAsync();Assert.Equal(originalCorrelation.ToString(),receipt["OriginalCorrelationId"].AsString);
 if(resolved.Status==201)Assert.Equal(receipt["Body"].AsString,resolved.Body);
 var fresh=new Diten.SupplyChainService.Persistence.Features.SandopPlans.SandopRepository(recoveredDb);
 var replay=await SandopTestHost.Mutate(fresh,s,new Diten.SupplyChainService.Infrastructure.Features.SandopPlans.DemandFixtureReader([]),SandopAction.Create,null,key,body);
 Assert.Equal(201,replay.Status);Assert.Equal(receipt["Body"].AsString,replay.Body);
 Assert.Equal(new long[]{1,0,1,1,1},await Counts(recoveredDb,s));
 }

}
