using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.Claims;
using System.Diagnostics;
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
    // Child half of Receipt_DurableRecovery_AcrossTwoTestProcesses: skipped unless that test starts it with a mode.
    [ClaimsRestartChildFact] public async Task Receipt_TwoIndependentTestProcesses_DurableRecovery()
    {
        var mode=Environment.GetEnvironmentVariable("CLAIMS_RESTART_MODE")!;
        var tenant=Guid.Parse(Environment.GetEnvironmentVariable("CLAIMS_RESTART_TENANT")!);
        var f=await ClaimDbFixture.Create();f.Scope=new(tenant,Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        f.Root=Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");f.Shipment=Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var result=await f.CreateClaim("restart",amount:"123456789012345678901234567890.00000000000000000000000000001");
        Assert.Equal(201,result.StatusCode);Assert.Equal(mode=="read",result.IdempotentReplay);Assert.Equal(new long[]{1,1,1,1},await f.Counts());
        var claims=await f.Repository.QueryAsync(f.Scope,null,null,default);Assert.Single(claims);Assert.Equal("123456789012345678901234567890.00000000000000000000000000001",claims[0].ClaimedAmount);
        var line=$"PROCESS_ID={Environment.ProcessId};MODE={mode};CLAIM={result.ClaimId};REPLAY={result.IdempotentReplay}";
        Console.WriteLine(line);
        if(Environment.GetEnvironmentVariable("CLAIMS_RESTART_EVIDENCE") is {Length:>0} evidence)await File.WriteAllTextAsync(evidence,line);
    }
    // The ordinary suite (and CI, which runs it unfiltered) proves durability itself: a write process creates the claim and
    // exits, then a second process must replay it from MongoDB alone.
    [Fact] public async Task Receipt_DurableRecovery_AcrossTwoTestProcesses()
    {
        var tenant=Guid.NewGuid().ToString();var dir=Directory.CreateTempSubdirectory("claims-restart-").FullName;
        var write=await RunChild("write",tenant,Path.Combine(dir,"write.txt"));var read=await RunChild("read",tenant,Path.Combine(dir,"read.txt"));
        Assert.Equal("write",write["MODE"]);Assert.Equal("False",write["REPLAY"]);
        Assert.Equal("read",read["MODE"]);Assert.Equal("True",read["REPLAY"]);
        Assert.Equal(write["CLAIM"],read["CLAIM"]);
        Assert.Equal(3,new[]{write["PROCESS_ID"],read["PROCESS_ID"],Environment.ProcessId.ToString()}.Distinct().Count());
    }
    private static async Task<Dictionary<string,string>> RunChild(string mode,string tenant,string evidence)
    {
        var project=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../Diten.SupplyChainService.Tests.csproj"));
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.GetDirectoryName(project)!,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        foreach(var arg in new[]{"test",project,"--no-build","--no-restore","--filter",$"FullyQualifiedName={typeof(ClaimReplayTests).FullName}.{nameof(Receipt_TwoIndependentTestProcesses_DurableRecovery)}"})info.ArgumentList.Add(arg);
        info.Environment["DOTNET_ROLL_FORWARD"]="Major";info.Environment["CLAIMS_RESTART_MODE"]=mode;info.Environment["CLAIMS_RESTART_TENANT"]=tenant;info.Environment["CLAIMS_RESTART_EVIDENCE"]=evidence;
        using var process=Process.Start(info)!;using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw new TimeoutException($"Claims restart child '{mode}' did not exit in 3 minutes");}
        Assert.True(process.ExitCode==0,$"Claims restart child '{mode}' exit {process.ExitCode}: {await output} {await error}");
        Assert.True(File.Exists(evidence),$"Claims restart child '{mode}' ran no test (skipped or filtered out): {await output}");
        return (await File.ReadAllTextAsync(evidence)).Split(';').Select(p=>p.Split('=',2)).ToDictionary(p=>p[0],p=>p[1]);
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

/// <summary>Runs only in a child process started with CLAIMS_RESTART_MODE=write|read (xUnit 2 has no runtime skip).</summary>
public sealed class ClaimsRestartChildFactAttribute : FactAttribute
{
    public ClaimsRestartChildFactAttribute()
    {
        if(Environment.GetEnvironmentVariable("CLAIMS_RESTART_MODE") is not ("write" or "read"))
            Skip="CLAIMS_RESTART_CHILD: started by Receipt_DurableRecovery_AcrossTwoTestProcesses with CLAIMS_RESTART_MODE=write|read.";
    }
}
