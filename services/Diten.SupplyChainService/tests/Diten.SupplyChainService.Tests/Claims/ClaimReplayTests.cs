using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.Claims;
using System.Text.Json;
using Diten.SupplyChainService.Application.Features.Claims;
using Xunit;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimReplayTests
{
 [Fact] public async Task NilAuthoritativeRoot_PersistsAndReplaysWithoutDefaulting()
 {
  var f=await ClaimDbFixture.Create();f.Root=Guid.Empty;var created=await f.CreateClaim("nil");Assert.Equal(201,created.StatusCode);
  var row=Assert.Single(await f.Repository.QueryAsync(f.Scope,null,null,default));Assert.Equal(Guid.Empty,row.CorrelationRoot);
  var replay=await f.CreateClaim("nil");Assert.True(replay.IdempotentReplay);Assert.Equal(created.ClaimId,replay.ClaimId);Assert.Equal(new long[]{1,1,1,1},await f.Counts());
 }

 [Fact] public async Task Scope_SoftDeleteAndActorReplay_PreserveReceiptAndAudit()
 {
  var f=await ClaimDbFixture.Create();var created=await f.CreateClaim("scoped");Assert.Equal(201,created.StatusCode);
  var original=f.Scope;var counts=await f.Counts();
  foreach(var scope in new[]{original with {TenantId=Guid.NewGuid()},original with {LegalEntityId=Guid.NewGuid()}})
  {
   Assert.Empty(await f.Repository.QueryAsync(scope,null,null,default));
   var result=await f.Repository.MutateAsync(scope,created.ClaimId,"foreign","fp",f.Root,null,ClaimStatus.Investigating,"2026-09-20T12:00:00Z",null,null,null,f.Observe,default);
   Assert.Equal(404,result.StatusCode);Assert.Equal("CLAIM_NOT_FOUND",result.ErrorCode);
  }
  Assert.Equal(counts,await f.Counts());
  await f.Db.GetCollection<BsonDocument>("claims").UpdateOneAsync(f.Filter(),Builders<BsonDocument>.Update.Set("IsDeleted",true));
  Assert.Empty(await f.Repository.QueryAsync(original,null,null,default));
  f.Scope=original with {ActorId=Guid.NewGuid()};var replay=await f.CreateClaim("scoped");Assert.Equal(201,replay.StatusCode);Assert.True(replay.IdempotentReplay);Assert.Equal(created.ClaimId,replay.ClaimId);
  var audit=await f.Db.GetCollection<BsonDocument>("claims_audit").Find(f.Filter()).SingleAsync();Assert.Equal(original.ActorId.ToString(),audit["ActorId"].AsString);Assert.Equal(counts,await f.Counts());
 }

    [Fact] public async Task Receipt_RootBeforeChangedPayload_AndOriginalResultReplay()
    {
        var f=await ClaimDbFixture.Create();var first=await f.CreateClaim("receipt");Assert.Equal(201,first.StatusCode);
        var count=f.Observations;var before=await f.Counts();
        var wrongRoot=await f.CreateClaim("receipt","changed",Guid.NewGuid(),"251");Assert.Equal(409,wrongRoot.StatusCode);Assert.Equal("CLAIM_CORRELATION_MISMATCH",wrongRoot.ErrorCode);
        var changed=await f.CreateClaim("receipt","changed",amount:"251");Assert.Equal(409,changed.StatusCode);Assert.Equal("IDEMPOTENCY_KEY_REUSED",changed.ErrorCode);
        var replay=await f.CreateClaim("receipt");Assert.Equal(first.ClaimId,replay.ClaimId);Assert.True(replay.IdempotentReplay);
        Assert.Equal(count,f.Observations);Assert.Equal(before,await f.Counts());
    }
    [Fact] public async Task Receipt_TwoIndependentTestProcesses_DurableRecovery()
    {
        var mode=Environment.GetEnvironmentVariable("CLAIMS_RESTART_MODE");
        if(mode is not ("write" or "read"))throw new InvalidOperationException("Explicit write/read restart mode required; exclude from ordinary suite.");
        var tenant=Guid.Parse(Environment.GetEnvironmentVariable("CLAIMS_RESTART_TENANT")!);
        var f=await ClaimDbFixture.Create();f.Scope=new(tenant,Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        f.Root=Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");f.Shipment=Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var result=await f.CreateClaim("restart",amount:"123456789012345678901234567890.00000000000000000000000000001");
        Assert.Equal(201,result.StatusCode);Assert.Equal(mode=="read",result.IdempotentReplay);Assert.Equal(new long[]{1,1,1,1},await f.Counts());
        var claims=await f.Repository.QueryAsync(f.Scope,null,null,default);Assert.Single(claims);Assert.Equal("123456789012345678901234567890.00000000000000000000000000001",claims[0].ClaimedAmount);
        Console.WriteLine($"PROCESS_ID={Environment.ProcessId};MODE={mode};CLAIM={result.ClaimId};REPLAY={result.IdempotentReplay}");
    }
    private static string Create(string json) { using var d=JsonDocument.Parse(json);return ClaimRequestFingerprint.Create(d.RootElement); }
    private static string Transition(string json) { using var d=JsonDocument.Parse(json);return ClaimRequestFingerprint.Transition(d.RootElement); }
    private const string Base="{\"shipmentId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"reasonCode\":\"damage\",\"claimedAmount\":\"250\",\"currency\":\"USD\"}";
    [Fact] public void CreateFingerprint_LexicalAmountChanges_Conflicts() => Assert.NotEqual(Create(Base),Create(Base.Replace("250","250.00")));
    [Fact] public void CreateFingerprint_NullableOmissionAndEmptyEvidence_Replays()
    {
        Assert.Equal(Create(Base),Create(Base[..^1]+",\"carrierId\":null,\"evidenceReferenceIds\":[]}"));
        Assert.Equal(Create(Base),Create("{\"currency\":\"USD\",\"claimedAmount\":\"250\",\"reasonCode\":\"d\\u0061mage\",\"shipmentId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}"));
    }
    [Fact] public void CreateFingerprint_EvidenceOrderAndMultiplicity_RemainSignificant()
    {
        var prefix=Base[..^1]+",\"evidenceReferenceIds\":";
        Assert.NotEqual(Create(prefix+"[\"a\",\"b\"]}"),Create(prefix+"[\"b\",\"a\"]}"));
        Assert.NotEqual(Create(prefix+"[\"a\"]}"),Create(prefix+"[\"a\",\"a\"]}"));
    }
    [Fact] public void TransitionFingerprint_EquivalentInstantsDifferentText_Conflicts()
    {
        const string x="{\"targetStatus\":\"Investigating\",\"occurredAt\":\"2026-09-20T10:00:00Z\"}";
        Assert.NotEqual(Transition(x),Transition(x.Replace("10:00:00Z","11:00:00+01:00")));
        Assert.Equal(Transition(x),Transition(x[..^1]+",\"approvedAmount\":null,\"note\":null,\"resolutionCode\":null}"));
    }
}
