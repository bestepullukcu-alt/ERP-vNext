using Xunit;
using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Persistence.Features.Returns;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Tests.Returns;
[Collection("Returns Mongo")]
public sealed class ReturnReplayTests
{
 [Fact] public async Task Receipt_RootBeforePayload_AndCurrentActorDoesNotRewriteAudit()
 {var f=await ReturnTestFixture.New();var actor=f.Scope.ActorId;var created=await f.Create();var counts=await f.Counts();Assert.Equal("CORRELATION_ROOT_MISMATCH",(await f.Create("2",root:Guid.NewGuid())).ErrorCode);Assert.Equal("IDEMPOTENCY_KEY_REUSED",(await f.Create("2")).ErrorCode);f.Offline=true;f.Scope=f.Scope with{ActorId=Guid.NewGuid()};var replay=await f.Create();Assert.True(replay.IdempotentReplay);Assert.Equal(created.ReturnId,replay.ReturnId);Assert.Equal(counts,await f.Counts());Assert.Equal(1,f.Observations);var audit=await f.Db.GetCollection<BsonDocument>("returns_audit").Find(f.Filter).FirstAsync();Assert.Equal(actor.ToString(),audit["ActorId"].AsString);}
 [Fact] public async Task Receipt_AfterLaterState_ReturnsOriginalResultWithoutReread()
 {var f=await ReturnTestFixture.New();var created=await f.Create();Assert.Equal(200,(await f.Transition(created.ReturnId,ReturnStatus.Authorized,"authorize")).StatusCode);f.Offline=true;var replay=await f.Create();Assert.Equal("Requested",replay.Status);Assert.True(replay.IdempotentReplay);Assert.Equal(1,f.Observations);}
 [Fact] public async Task RestartReceipt()
 {
  var f=await ReturnTestFixture.New();const string exactQuantity="0.12345678901234567890123456789012345678901234567890";var mode=Environment.GetEnvironmentVariable("RETURNS_RESTART_MODE");
  if(mode is not null){Guid Id(string key)=>Guid.Parse(Environment.GetEnvironmentVariable("RETURNS_RESTART_"+key)!);f.Scope=new(Id("TENANT"),Id("LE"),Id("ACTOR"));f.Root=Id("ROOT");f.Shipment=Id("SHIPMENT");}
  if(mode!="read"){var seed=await f.Create(exactQuantity);Assert.Equal(201,seed.StatusCode);Assert.False(seed.IdempotentReplay);}
  if(mode!="seed"){f.Offline=true;f.Repository= new ReturnRepository(f.Db,new ReturnTestProbe());var replay=await f.Create(exactQuantity);Assert.Equal(201,replay.StatusCode);Assert.True(replay.IdempotentReplay);Assert.Equal(new long[]{1,1,1,1,1},await f.Counts());var stored=await f.Db.GetCollection<BsonDocument>("returns").Find(f.Filter).FirstAsync();Assert.Equal(exactQuantity,stored["Lines"][0]["Quantity"].AsString);Assert.Equal(f.Root.ToString(),stored["CorrelationRoot"].AsString);}
 }
 [Fact]public async Task RawCreateAudit_PreservesUuidSpellingAndOmission_WhileFingerprintNormalizes()
 {var f=await ReturnTestFixture.New();f.Shipment=Guid.Parse("abcdefab-abcd-abcd-abcd-abcdefabcdef");var raw=$$"""{"shipmentId":"{{f.Shipment.ToString().ToUpperInvariant()}}","reasonCode":"","lines":[{"shipmentLineNumber":"1","quantity":"01.00","uomId":"EA"}]}""";var body=JsonDocument.Parse(raw).RootElement;var result=await f.Repository.MutateAsync(f.Scope,null,"raw",ReturnRequestFingerprint.Create(body),f.Root,ReturnWire.Plan(body),null,null,null,null,f.Observe,default,raw);Assert.Equal(201,result.StatusCode);var audit=await f.Db.GetCollection<BsonDocument>("returns_audit").Find(f.Filter).FirstAsync();var stored=audit["RawCreate"].AsBsonDocument;Assert.Equal(f.Shipment.ToString().ToUpperInvariant(),stored["shipmentId"].AsString);Assert.False(stored.Contains("evidenceReferenceIds"));Assert.Equal("01.00",stored["lines"][0]["quantity"].AsString);}
}
