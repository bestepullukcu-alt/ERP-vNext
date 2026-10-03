using Xunit;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Persistence.Features.Returns;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Tests.Returns;
[CollectionDefinition("Returns Mongo",DisableParallelization=true)]public sealed class ReturnMongoCollection {}
[Collection("Returns Mongo")]
public sealed class ReturnAtomicityTests
{
 [Theory][InlineData("entitlement",1)][InlineData("aggregate",2)][InlineData("receipt",3)][InlineData("audit",4)][InlineData("outbox",5)]
 public async Task PrecommitFault_EachActualWriteStage_RollsBackAllFive(string stage,int reached)
 {
  var f=await ReturnTestFixture.New();f.Probe.FailAt=stage;var result=await f.Create();Assert.NotEqual(201,result.StatusCode);Assert.Equal(new long[5],await f.Counts());
  Assert.Equal(new[]{"entitlement","aggregate","receipt","audit","outbox"}.Take(reached),f.Probe.Seen);
  f.Probe.FailAt=null;Assert.Equal(201,(await f.Create()).StatusCode);Assert.Equal(new long[]{1,1,1,1,1},await f.Counts());
 }
 [Fact] public async Task MultilineFailure_SecondLineOverCap_LeavesNoFirstLineDebit()
 {var f=await ReturnTestFixture.New();f.SourceLines.Add(new("2","2","EA",Guid.NewGuid(),Guid.NewGuid()));var result=await f.Create(lines:[new("1","1","EA"),new("2","3","EA")]);Assert.Equal(422,result.StatusCode);Assert.Equal(new long[5],await f.Counts());}
 [Fact] public async Task PostCommitResponseLoss_RecoversOriginalReceipt_NotZeroWrites()
 {var f=await ReturnTestFixture.New();f.Probe.FailAt="afterCommit";var result=await f.Create();Assert.Equal(201,result.StatusCode);Assert.True(result.IdempotentReplay);Assert.Equal(new long[]{1,1,1,1,1},await f.Counts());f.Offline=true;var again=await f.Create();Assert.Equal(result.ReturnId,again.ReturnId);Assert.Equal(new long[]{1,1,1,1,1},await f.Counts());}
 [Fact] public async Task UnknownCommit_Unresolved503_AllOrNoneThenSameKeyRecovery()
 {
  var f=await ReturnTestFixture.New();var admin=f.Db.Client.GetDatabase("admin");
  await admin.RunCommandAsync<BsonDocument>(new BsonDocument{{"configureFailPoint","failCommand"},{"mode",new BsonDocument("times",5)},{"data",new BsonDocument{{"failCommands",new BsonArray{"commitTransaction"}},{"errorCode",91},{"errorLabels",new BsonArray{"UnknownTransactionCommitResult"}}}}});
  ReturnMutationResult result;
  try{result=await f.Create();}finally{await admin.RunCommandAsync<BsonDocument>(new BsonDocument{{"configureFailPoint","failCommand"},{"mode","off"}});}
  Assert.Equal(503,result.StatusCode);var counts=await f.Counts();Assert.True(counts.All(x=>x==0)||counts.All(x=>x==1));
  var deadline=DateTimeOffset.UtcNow.AddSeconds(90);ReturnMutationResult recovered;
  do{recovered=await f.Create();Console.WriteLine("Unknown commit same-key recovery status="+recovered.StatusCode);if(recovered.StatusCode==201)break;Assert.Equal(503,recovered.StatusCode);await Task.Delay(1000);}while(DateTimeOffset.UtcNow<deadline);
  Assert.Equal(201,recovered.StatusCode);Assert.Equal(new long[]{1,1,1,1,1},await f.Counts());
 }
}
public sealed class ReturnTestProbe:IReturnCommitProbe
{
 public string? FailAt;public System.Collections.Concurrent.ConcurrentQueue<string> Seen {get;}=new();public Queue<Guid> Ids {get;}=[];
 public Guid NewId()=>Ids.Count>0?Ids.Dequeue():Guid.NewGuid();
 public Task AtAsync(string stage,CancellationToken ct){Seen.Enqueue(stage);Console.WriteLine("Returns write stage="+stage);ReturnEvidence.Record(new{stage});if(FailAt==stage){FailAt=null;throw new TimeoutException("Injected Returns write-stage fault");}return Task.CompletedTask;}
}
public sealed class ReturnTestFixture
{
 public IMongoDatabase Db {get;private set;}=null!;public ReturnRepository Repository {get;set;}=null!;public ReturnTestProbe Probe {get;}=new();
 public ReturnScope Scope {get;set;}=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());public Guid Shipment=Guid.NewGuid();public Guid Root=Guid.NewGuid();public bool Offline;public int Observations;
 public List<ReturnSourceLine> SourceLines {get;}=[new("1","10","EA",Guid.NewGuid(),Guid.NewGuid())];
 public BsonDocument Filter=>new(){{"TenantId",Scope.TenantId.ToString()},{"LegalEntityId",Scope.LegalEntityId.ToString()}};
 public static async Task<ReturnTestFixture> New(string databaseName="diten_returns_tests",bool caseInsensitive=false)
 {
  var uri=Environment.GetEnvironmentVariable("RETURNS_MONGO_URI")??"mongodb://127.0.0.1:27886/?replicaSet=returns_dev";
  var url=MongoUrl.Create(uri);if(url.Servers.Any(s=>s.Port==27017)||url.Servers.Any(s=>s.Host is not("127.0.0.1" or "localhost")))throw new InvalidOperationException("Only isolated local Returns Mongo allowed");
  var services=new Microsoft.Extensions.DependencyInjection.ServiceCollection();
  var config=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Mongo:ConnectionString",uri},{"Mongo:DatabaseName",databaseName}}).Build();
  Diten.SupplyChainService.Persistence.DependencyInjection.AddPersistence(services,config);
  var provider=Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
  ReturnEvidence.Record(new{processId=Environment.ProcessId,command=Environment.GetCommandLineArgs(),binary=typeof(ReturnTestFixture).Assembly.Location,binarySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(ReturnTestFixture).Assembly.Location))).ToLowerInvariant()});
  var f=new ReturnTestFixture{Db=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IMongoDatabase>(provider)};
  if(caseInsensitive){try{await f.Db.CreateCollectionAsync("return_entitlements",new CreateCollectionOptions{Collation=new Collation("en",strength:CollationStrength.Secondary)});}catch(MongoCommandException ex)when(ex.Code==48){}}
  await new ReturnSchema(f.Db).StartAsync(default);f.Repository=new(f.Db,f.Probe);return f;
 }
 public Task<ReturnReferenceSnapshot> Observe(ReturnOrder order,CancellationToken ct){Observations++;if(Offline)throw new ReturnFailureException(503,"REFERENCE_UNAVAILABLE");return Task.FromResult(new ReturnReferenceSnapshot(order.ShipmentId,"Delivered",Root,DateTimeOffset.UtcNow,SourceLines.ToArray()));}
 public Task<ReturnMutationResult> Create(string quantity="1",string key="create",string? fingerprint=null,Guid? root=null,List<ReturnLine>? lines=null)
 {var order=new ReturnOrder{ShipmentId=Shipment,ReasonCode="reason",Lines=lines??[new("1",quantity,"EA")]};return Repository.MutateAsync(Scope,null,key,fingerprint??quantity,root??Root,order,null,null,null,null,Observe,default);}
 public Task<ReturnMutationResult> Transition(Guid id,ReturnStatus target,string key,string? inventory=null,string? disposition=null,Guid? root=null,string? fingerprint=null)=>Repository.MutateAsync(Scope,id,key,fingerprint??target.ToString(),root??Root,null,target,"2026-09-20T09:00:00.123456789Z",inventory,disposition,Observe,default);
 public async Task<long[]> Counts(){var result=new List<long>();foreach(var c in new[]{"return_entitlements","returns","returns_receipts","returns_audit","returns_outbox"})result.Add(await Db.GetCollection<BsonDocument>(c).CountDocumentsAsync(Filter));ReturnEvidence.Record(new{tenant=Scope.TenantId,legalEntity=Scope.LegalEntityId,collections=new[]{"return_entitlements","returns","returns_receipts","returns_audit","returns_outbox"},counts=result});Console.WriteLine("Returns scoped counts "+Scope.TenantId+"="+string.Join(",",result));return result.ToArray();}
 public Task<BsonDocument> Entitlement()=>Db.GetCollection<BsonDocument>("return_entitlements").Find(Filter).FirstAsync();
}

public static class ReturnEvidence
{
 private static readonly object Sync=new();
 public static void Record(object value){var path=Environment.GetEnvironmentVariable("RETURNS_PROCESS_EVIDENCE");if(path is null)return;lock(Sync)File.AppendAllText(path,JsonSerializer.Serialize(new{at=DateTimeOffset.UtcNow,pid=Environment.ProcessId,value})+Environment.NewLine);}
}
