using Xunit;using System.Text.Json;using MongoDB.Driver;using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Tests.SandopPlans;
public sealed class SandopLifecycleTests
{[Fact]public async Task Create_capture_signoff_preserves_prior_snapshot_and_draft_to_inreview()
{var(h,db,s,f)=await SandopTestHost.Open();var create=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,"create-"+Guid.NewGuid(),SandopTestHost.Create());Assert.Equal(201,create.Status);using var cp=JsonDocument.Parse(create.Body!);Assert.Equal("Draft",cp.RootElement.GetProperty("status").GetString());Assert.Equal(JsonValueKind.Null,cp.RootElement.GetProperty("currentSnapshotId").ValueKind);
 var id=SandopTestHost.Id(create,"sandopPlanId");var first=await SandopTestHost.Mutate(h,s,f,SandopAction.Capture,id,"capture-"+Guid.NewGuid(),SandopTestHost.Capture());Assert.Equal(201,first.Status);var old=first.Body!;
 var second=await SandopTestHost.Mutate(h,s,f,SandopAction.Capture,id,"capture-"+Guid.NewGuid(),SandopTestHost.Capture());Assert.Equal(201,second.Status);var listed=await h.ReadAsync(s,SandopAction.ListSnapshots,id,CancellationToken.None);Assert.Equal(200,listed.Status);using var doc=JsonDocument.Parse(listed.Body!);Assert.Equal(2,doc.RootElement.GetProperty("items").GetArrayLength());Assert.Equal(old,doc.RootElement.GetProperty("items")[0].GetRawText());
 var snapshotId=SandopTestHost.Id(first,"snapshotId");var sign=SandopTestHost.J(System.Text.Json.JsonSerializer.Serialize(new { snapshotId, role="Finance", decision="Approved" }));var signed=await SandopTestHost.Mutate(h,s,f,SandopAction.SignOff,id,"sign-"+Guid.NewGuid(),sign);Assert.Equal(201,signed.Status);
 var current=await h.ReadAsync(s,SandopAction.Get,id,CancellationToken.None);using var plan=JsonDocument.Parse(current.Body!);Assert.Equal("InReview",plan.RootElement.GetProperty("status").GetString());
 var duplicate=await SandopTestHost.Mutate(h,s,f,SandopAction.SignOff,id,"different-"+Guid.NewGuid(),sign);Assert.Equal(409,duplicate.Status);Assert.Equal("SIGN_OFF_ALREADY_RECORDED",duplicate.Code);
}

 [Fact]public async Task All_five_role_decisions_leave_plan_in_review_and_outbox_pending()
 {var(h,db,s,f)=await SandopTestHost.Open();var created=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,Guid.NewGuid().ToString(),SandopTestHost.Create());Assert.Equal(201,created.Status);var id=SandopTestHost.Id(created,"sandopPlanId");
 var captured=await SandopTestHost.Mutate(h,s,f,SandopAction.Capture,id,Guid.NewGuid().ToString(),SandopTestHost.Capture());Assert.Equal(201,captured.Status);var snap=SandopTestHost.Id(captured,"snapshotId");
 foreach(var role in new[]{"DemandPlanning","SupplyPlanning","Finance","Operations","Executive"})
 {var body=SandopTestHost.J(JsonSerializer.Serialize(new{snapshotId=snap,role,decision="Approved"}));var result=await SandopTestHost.Mutate(h,s,f,SandopAction.SignOff,id,Guid.NewGuid().ToString(),body);Assert.Equal(201,result.Status);}
 var plan=await h.ReadAsync(s,SandopAction.Get,id,CancellationToken.None);Assert.Equal("InReview",JsonDocument.Parse(plan.Body!).RootElement.GetProperty("status").GetString());
 var outs=await db.GetCollection<MongoDB.Bson.BsonDocument>("sandop_outbox").Find(new MongoDB.Bson.BsonDocument("TenantId",s.TenantId.ToString())).ToListAsync();Assert.Equal(7,outs.Count);Assert.All(outs,x=>Assert.Equal("Pending",x["Status"].AsString));
 Assert.Contains(outs,x=>System.Text.Json.JsonDocument.Parse(x["Envelope"].AsString).RootElement.GetProperty("eventType").GetString()=="sandop.plan.created.v1");
 Assert.Contains(outs,x=>System.Text.Json.JsonDocument.Parse(x["Envelope"].AsString).RootElement.GetProperty("eventType").GetString()=="sandop.snapshot.captured.v1");
 Assert.Contains(outs,x=>System.Text.Json.JsonDocument.Parse(x["Envelope"].AsString).RootElement.GetProperty("eventType").GetString()=="sandop.sign-off.recorded.v1");
 }


 [Fact]public async Task Capture_persists_exact_fixture_provenance_and_stores_no_demand_series()
 {var(h,db,s,f)=await SandopTestHost.Open();var created=await SandopTestHost.Mutate(h,s,f,SandopAction.Create,null,Guid.NewGuid().ToString(),SandopTestHost.Create());Assert.Equal(201,created.Status);var id=SandopTestHost.Id(created,"sandopPlanId");
 var captured=await SandopTestHost.Mutate(h,s,f,SandopAction.Capture,id,Guid.NewGuid().ToString(),SandopTestHost.Capture());Assert.Equal(201,captured.Status);
 var raw=await db.GetCollection<MongoDB.Bson.BsonDocument>("sandop_snapshots").Find(new MongoDB.Bson.BsonDocument("TenantId",s.TenantId.ToString())).FirstAsync();
 // Provenance is persisted as a JSON string, not a nested document, and its members are camelCase.
 using var provenanceJson=JsonDocument.Parse(raw["Provenance"].AsString);var provenance=provenanceJson.RootElement;
 Assert.Equal("dp-2027",provenance.GetProperty("demandPlanId").GetString());Assert.Equal("3",provenance.GetProperty("demandPlanVersion").GetString());
 Assert.Equal("sha256:abc",provenance.GetProperty("sourceChecksum").GetString());Assert.True(provenance.TryGetProperty("sourceCapturedAt",out _));
 // Acceptance :196 also asks for the negative, and nothing asserted it: a snapshot carries provenance,
 // never a second copy of DEMAND. The whole stored document is scanned, not just its field names, because
 // Provenance and SupplyInputRefs are strings — a series smuggled inside one of them would hide otherwise.
 var stored=MongoDB.Bson.BsonExtensionMethods.ToJson(raw);
 foreach(var forbidden in new[]{"quantity","series","confidence","forecast"})
  Assert.False(stored.Contains(forbidden,StringComparison.OrdinalIgnoreCase),forbidden+" appears in the stored snapshot; it must reference DEMAND, not copy it");
 }

}
