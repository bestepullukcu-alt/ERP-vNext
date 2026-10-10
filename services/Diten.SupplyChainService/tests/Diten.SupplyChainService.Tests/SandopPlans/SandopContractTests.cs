using System.Text.Json;using Xunit;using MongoDB.Driver;using Diten.SupplyChainService.Domain.Features.SandopPlans;using Diten.SupplyChainService.Application.Features.SandopPlans;using Diten.SupplyChainService.Persistence.Features.SandopPlans;using Diten.SupplyChainService.Infrastructure.Features.SandopPlans;
namespace Diten.SupplyChainService.Tests.SandopPlans;
internal static class SandopTestHost
{ public static async Task<(SandopRepository Repo,IMongoDatabase Db,SandopScope Scope,DemandFixtureReader Fixture)> Open()
{ var uri=Environment.GetEnvironmentVariable("MVP6_MOD0190_MONGO_URI")??throw new InvalidOperationException("Set lane-local MVP6_MOD0190_MONGO_URI; operational 27017 is forbidden.");
 var client=new MongoClient(uri);var db=client.GetDatabase("DitenSupplyChain_Mod0190_Test");await SandopSchema.EnsureAsync(db);var scope=new SandopScope(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());
 return(new SandopRepository(db),db,scope,new DemandFixtureReader([new DemandFixture(scope.TenantId,scope.LegalEntityId,"dp-2027","3","sha256:abc",true)])); }
 public static JsonElement J(string json)=>JsonDocument.Parse(json).RootElement.Clone();
 public static JsonElement Create()=>J("""{"name":"FY2027","horizonStart":"2027-01-01","horizonEnd":"2027-12-31","demandPlanId":"dp-2027","demandPlanVersion":"3"}""");
 public static JsonElement Capture()=>J("""{"demandPlanId":"dp-2027","demandPlanVersion":"3","sourceCapturedAt":"2026-09-15T09:45:00Z","sourceChecksum":"sha256:abc","supplyInputRefs":[]}""");
 public static Guid Id(SandopResult result,string key)=>Guid.Parse(JsonDocument.Parse(result.Body!).RootElement.GetProperty(key).GetString()!);
 public static Task<SandopResult> Mutate(SandopRepository repo,SandopScope scope,DemandFixtureReader fixture,SandopAction action,Guid? id,string key,JsonElement body,Guid? correlation=null)=>repo.MutateAsync(scope,action,id,key,SandopRequestFingerprint.Create(body),correlation??Guid.NewGuid(),body,fixture,CancellationToken.None);
}
public sealed class SandopContractTests
{ [Fact]public void Fingerprint_normalizes_member_order_but_retains_decimal_lexeme_and_unicode()
{Assert.Equal(SandopRequestFingerprint.Create(SandopTestHost.J("""{"a":1,"b":2}""")),SandopRequestFingerprint.Create(SandopTestHost.J("""{"b":2,"a":1}""")));
 Assert.NotEqual(SandopRequestFingerprint.Create(SandopTestHost.J("""{"a":1.0}""")),SandopRequestFingerprint.Create(SandopTestHost.J("""{"a":1}""")));}
 [Fact]public void Schema_rejects_extra_fields_and_invalid_enums()
{Assert.Equal("INVALID_REQUEST",SandopWire.Validate(SandopAction.Create,SandopTestHost.J("""{"name":"a","horizonStart":"2027-01-01","horizonEnd":"2027-01-02","demandPlanId":"x","demandPlanVersion":"1","tenantId":"x"}"""))!.Code);
 Assert.Equal("INVALID_REQUEST",SandopWire.Validate(SandopAction.SignOff,SandopTestHost.J("""{"snapshotId":"aaaaaaaa-0000-4000-8000-000000000001","role":"Unknown","decision":"Approved"}"""))!.Code);}

 [Fact]public void Module_request_gate_requires_permission_and_preserves_exact_key_and_correlation()
 {var tenant=Guid.NewGuid();var le=Guid.NewGuid();var actor=Guid.NewGuid();var correlation=Guid.NewGuid();var http=new Microsoft.AspNetCore.Http.DefaultHttpContext();
 http.User=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim("tenant_id",tenant.ToString()),new System.Security.Claims.Claim("legal_entity_id",le.ToString()),new System.Security.Claims.Claim("sub",actor.ToString())},"test"));
 http.Request.Headers["X-Correlation-Id"]=correlation.ToString();http.Request.Headers["X-Tenant-Id"]=tenant.ToString();http.Request.Headers["X-Legal-Entity-Id"]=le.ToString();http.Request.Headers["Idempotency-Key"]=" key ";
 var denied=Diten.SupplyChainService.Api.Features.SandopPlans.SandopContextMiddleware.Resolve(http,Diten.SupplyChainService.Infrastructure.Features.SandopPlans.SandopPermissions.Create,true);Assert.Equal(403,denied.Status);
 http.User=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(http.User.Claims.Append(new System.Security.Claims.Claim("permission",Diten.SupplyChainService.Infrastructure.Features.SandopPlans.SandopPermissions.Create)),"test"));
 var permitted=Diten.SupplyChainService.Api.Features.SandopPlans.SandopContextMiddleware.Resolve(http,Diten.SupplyChainService.Infrastructure.Features.SandopPlans.SandopPermissions.Create,true);Assert.Equal(0,permitted.Status);Assert.Equal(" key ",permitted.Key);Assert.Equal(correlation,permitted.Correlation);Assert.Equal(correlation.ToString(),http.Response.Headers["X-Correlation-Id"].ToString());
 }
}
