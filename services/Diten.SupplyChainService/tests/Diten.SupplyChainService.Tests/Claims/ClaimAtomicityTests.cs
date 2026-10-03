using Diten.SupplyChainService.Domain.Features.Claims;
using Diten.SupplyChainService.Persistence.Features.Claims;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Diten.SupplyChainService.Persistence;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimAtomicityTests
{
 [Fact] public async Task MissingUniqueIndex_FailsClosedBeforeAnyWrite()
 {
  // Fixed suffix for a database-wide schema test, never per-run database names (DB-010).
  var f=await ClaimDbFixture.Create(databaseName:"diten_test_claims_noindexes");
  try
  {
   await f.Db.GetCollection<BsonDocument>("claims").Indexes.DropOneAsync("claim_number");
   var result=await f.CreateClaim("missing-index");Assert.Equal(503,result.StatusCode);Assert.Equal("CLAIM_STORAGE_UNAVAILABLE",result.ErrorCode);Assert.Equal(new long[]{0,0,0,0},await f.Counts());
  }
  finally {await new ClaimSchema(f.Db).StartAsync(default);}
 }

 [Fact] public async Task DriverCapacityFailure_HasMeasuredExceptionType()
 {
  var f=await ClaimDbFixture.Create();
  var ex=await Assert.ThrowsAnyAsync<Exception>(()=>f.Db.GetCollection<BsonDocument>("claims").InsertOneAsync(new BsonDocument{{"TenantId",f.Scope.TenantId.ToString()},{"LegalEntityId",f.Scope.LegalEntityId.ToString()},{"ClaimNumber","capacity-probe"},{"payload",new string('x',17*1024*1024)}}));
  Assert.IsType<FormatException>(ex);
  Assert.Contains("MaxDocumentSize",ex.Message);
  Console.WriteLine("CAPACITY_EXCEPTION="+ex.GetType().FullName+";MESSAGE="+ex.Message);
  Assert.Equal(new long[]{0,0,0,0},await f.Counts());
 }

 [Fact] public async Task Storage_CapacityExceeded_FailsWithoutPartialDocuments()
 {
  var f=await ClaimDbFixture.Create();var result=await f.Repository.MutateAsync(f.Scope,null,"capacity","capacity",f.Root,
   new Claim {ShipmentId=f.Shipment,ClaimedAmount="1",Currency="USD",ReasonCode=new string('x',17*1024*1024)},null,null,null,null,null,f.Observe,default);
  Assert.Equal(503,result.StatusCode);Assert.Equal("CLAIM_STORAGE_UNAVAILABLE",result.ErrorCode);Assert.Equal(new long[]{0,0,0,0},await f.Counts());
 }
 [Theory][InlineData("aggregate")][InlineData("receipt")][InlineData("audit")][InlineData("outbox")][InlineData("beforeCommit")]
 public async Task Mutation_FailureAfterDistinctWriteStage_RollsBackAllFourCollections(string fault)
 {
  var probe=new RecordingClaimProbe {Fault=fault};var f=await ClaimDbFixture.Create(probe);
  var result=await f.CreateClaim("fault");
  Assert.Equal(503,result.StatusCode);Assert.Equal("CLAIM_STORAGE_UNAVAILABLE",result.ErrorCode);
  var stages=new[]{"aggregate","receipt","audit","outbox","beforeCommit"};
  Assert.Equal(stages.Take(Array.IndexOf(stages,fault)+1),probe.Stages);
  Assert.Equal(new long[]{0,0,0,0},await f.Counts());
 }
 [Fact] public async Task Mutation_ResponseLostAfterCommit_RecoversOriginalReceiptWithoutSecondWrite()
 {
  var probe=new RecordingClaimProbe {Fault="afterCommit"};var f=await ClaimDbFixture.Create(probe);
  var initial=await f.CreateClaim("lost");
  Assert.Contains("afterCommit",probe.Stages);
  Assert.Equal(new long[]{1,1,1,1},await f.Counts());
  Assert.Equal(201,initial.StatusCode);Assert.True(initial.IdempotentReplay);
  probe.Fault=null;var retry=await f.CreateClaim("lost");
  Assert.Equal(201,retry.StatusCode);Assert.True(retry.IdempotentReplay);
  Assert.Equal(new long[]{1,1,1,1},await f.Counts());
 }
}
internal sealed class RecordingClaimProbe : IClaimCommitProbe
{
 public string? Fault {get;set;} public Guid? FixedId {get;set;} public List<string> Stages {get;}=[];
 public Guid NewId()=>FixedId??Guid.NewGuid();
 public Task AtAsync(string stage,CancellationToken ct) { lock(Stages) Stages.Add(stage); if(stage==Fault)throw new TimeoutException("test injected fault");return Task.CompletedTask; }
}
internal sealed class ClaimDbFixture
{
 public IMongoDatabase Db {get;private init;}=null!; public ClaimRepository Repository {get;private init;}=null!;
 public ClaimScope Scope {get;set;}=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());
 public Guid Root {get;set;}=Guid.NewGuid();public Guid Shipment {get;set;}=Guid.NewGuid();public int Observations;
 public static async Task<ClaimDbFixture> Create(IClaimCommitProbe? probe=null,string databaseName="diten_test_claims")
 {
  var url=Environment.GetEnvironmentVariable("CLAIMS_TEST_MONGO")??throw new InvalidOperationException("CLAIMS_TEST_MONGO must name authorized isolated replica set");
  var parsed=new MongoUrl(url); if(parsed.Servers.Any(s=>s.Port==27017||!(s.Host=="localhost"||s.Host=="127.0.0.1")))throw new InvalidOperationException("operational/remote Mongo forbidden");
  var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Mongo:ConnectionString"]=url,["Mongo:DatabaseName"]=databaseName }).Build();
  var services=new ServiceCollection();services.AddPersistence(configuration);
  var provider=services.BuildServiceProvider();var db=provider.GetRequiredService<IMongoDatabase>();await new ClaimSchema(db).StartAsync(default);
  return new ClaimDbFixture {Db=db,Repository=new ClaimRepository(db,probe??new RecordingClaimProbe())};
 }
 public Task<ClaimReferenceSnapshot> Observe(Claim claim,CancellationToken ct) {Interlocked.Increment(ref Observations);return Task.FromResult(new ClaimReferenceSnapshot(Shipment,"Delivered",null,null,Root,DateTimeOffset.UtcNow));}
 public Task<ClaimMutationResult> CreateClaim(string key,string fingerprint="create-fp",Guid? root=null,string amount="250") => Repository.MutateAsync(Scope,null,key,fingerprint,root??Root,new Claim {ShipmentId=Shipment,ClaimedAmount=amount,Currency="USD",ReasonCode="damage"},null,null,null,null,null,Observe,default);
 public Task<ClaimMutationResult> Transition(Guid id,ClaimStatus target,string key,string? amount=null,string fingerprint="transition-fp")=>Repository.MutateAsync(Scope,id,key,fingerprint,Root,null,target,"2026-09-20T12:34:56.123456789+03:00",amount,null,null,Observe,default);
 public FilterDefinition<BsonDocument> Filter()=>Builders<BsonDocument>.Filter.Eq("TenantId",Scope.TenantId.ToString());
 public async Task<long[]> Counts() {var r=new List<long>();foreach(var name in new[]{"claims","claims_receipts","claims_audit","claims_outbox"})r.Add(await Db.GetCollection<BsonDocument>(name).CountDocumentsAsync(Filter()));return r.ToArray();}
}
