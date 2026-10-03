using Xunit;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Diten.SupplyChainService.Api.Features.Returns;
using Diten.SupplyChainService.Infrastructure.Features.Returns;

using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Persistence.Features.Returns;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Tests.Returns;
[Collection("Returns Mongo")]
public sealed class ReturnIsolationTests
{
 [Fact] public async Task Scope_AnotherTenantOrLE_CannotReadMutateOrReplay()
 {var f=await ReturnTestFixture.New();var original=f.Scope;var created=await f.Create();foreach(var foreign in new[]{new ReturnScope(Guid.NewGuid(),original.LegalEntityId,original.ActorId),new ReturnScope(original.TenantId,Guid.NewGuid(),original.ActorId)}){f.Scope=foreign;Assert.Empty(await f.Repository.QueryAsync(f.Scope,null,null,default));Assert.Equal(404,(await f.Transition(created.ReturnId,ReturnStatus.Authorized,"foreign")).StatusCode);}f.Scope=original;Assert.Single(await f.Repository.QueryAsync(f.Scope,null,null,default));}
 [Fact] public async Task SoftDelete_HidesAggregate_PreservesReceiptAndEntitlement()
 {var f=await ReturnTestFixture.New();var created=await f.Create("10");await f.Db.GetCollection<BsonDocument>("returns").UpdateOneAsync(f.Filter,new BsonDocument("$set",new BsonDocument("IsDeleted",true)));Assert.Empty(await f.Repository.QueryAsync(f.Scope,null,null,default));Assert.True((await f.Create("10")).IdempotentReplay);Assert.Equal(404,(await f.Transition(created.ReturnId,ReturnStatus.Authorized,"hidden")).StatusCode);Assert.Equal("RETURN_QUANTITY_EXCEEDED",(await f.Create("1","new")).ErrorCode);Assert.Equal("10",(await f.Entitlement())["UsedQuantity"].AsString);}
 // This is middleware unit evidence with an already-authenticated fabricated principal, NOT JWT validation.
 [Theory][InlineData("valid",200)][InlineData("unknown-query",200)][InlineData("scope-query",400)][InlineData("missing-scope",400)][InlineData("foreign-scope",404)][InlineData("duplicate-root",400)][InlineData("missing-root",400)][InlineData("untrusted",403)][InlineData("unauthenticated",401)]
 public async Task Middleware_PostAuthenticationUnit_OrderedContextMatrix(string variant,int expected)
 {
  var tenant=Guid.NewGuid();var le=Guid.NewGuid();var actor=Guid.NewGuid();var trace=Guid.Empty;
  var http=new DefaultHttpContext();http.Response.Body=new MemoryStream();http.RequestServices=new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
  http.Request.Path="/api/shipment-bundle/returns";http.Request.Method="GET";http.SetEndpoint(new Endpoint(_=>Task.CompletedTask,new EndpointMetadataCollection(new ReturnPermissionAttribute("supplychain.returns.read")),"unit"));
  var payload=JsonSerializer.Serialize(new{tenant_id=tenant,legal_entity_id=le,sub=actor});var token=Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+','-').Replace('/','_');
  http.Request.Headers.Authorization="Bearer unit."+token+".not-a-signature";http.Request.Headers["X-Correlation-Id"]=trace.ToString();http.Request.Headers["X-Tenant-Id"]=tenant.ToString();http.Request.Headers["X-Legal-Entity-Id"]=le.ToString();
  http.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new System.Security.Claims.Claim("tenant_id",tenant.ToString()),new System.Security.Claims.Claim("legal_entity_id",le.ToString()),new System.Security.Claims.Claim("sub",actor.ToString()),new System.Security.Claims.Claim("permission","supplychain.returns.read")},"unit-authenticated"));
  if(variant=="unknown-query")http.Request.QueryString=new QueryString("?ordinary=fine");
  if(variant=="scope-query")http.Request.QueryString=new QueryString("?TeNaNt_Id=x");
  if(variant=="missing-scope")http.Request.Headers.Remove("X-Tenant-Id");
  if(variant=="foreign-scope")http.Request.Headers["X-Tenant-Id"]=Guid.NewGuid().ToString();
  if(variant=="duplicate-root")http.Request.Headers["X-Correlation-Id"]=new Microsoft.Extensions.Primitives.StringValues(new[]{trace.ToString(),trace.ToString()});
  if(variant=="missing-root")http.Request.Headers.Remove("X-Correlation-Id");
  if(variant=="untrusted")((ClaimsIdentity)http.User.Identity!).AddClaim(new System.Security.Claims.Claim("sub",actor.ToString()));
  if(variant=="unauthenticated")http.User=new ClaimsPrincipal(new ClaimsIdentity());
  bool next=false;var middleware=new ReturnContextMiddleware(_=>{next=true;return Task.CompletedTask;},NullLogger<ReturnContextMiddleware>.Instance);
  await middleware.InvokeAsync(http,new ReturnRequestContext(),new Diten.SupplyChainService.Application.Common.RequestContext());
  Assert.Equal(expected,http.Response.StatusCode);Assert.Equal(expected==200,next);
  if(expected!=200){http.Response.Body.Position=0;var response=await JsonDocument.ParseAsync(http.Response.Body);Assert.Equal(http.Response.Headers["X-Correlation-Id"].ToString(),response.RootElement.GetProperty("error").GetProperty("correlationId").GetString());}
  if(expected==401)Assert.Equal("Bearer",http.Response.Headers.WWWAuthenticate.ToString());
 }
 [Fact]public void Permissions_ReturnsSpecificTargets_HaveNoClaimsInheritance()
 {var expected=new Dictionary<string,string?>{{"Authorized","authorize"},{"Rejected","authorize"},{"InTransit","transit"},{"Cancelled","cancel"},{"Received","receive"},{"Dispositioned","disposition"},{"Closed","close"},{"Requested",null}};foreach(var row in expected)Assert.Equal(row.Value is null?null:"supplychain.returns."+row.Value,ReturnPermissions.ForTarget(row.Key));}
 [Fact]public async Task Receipt_SameKeyDifferentLegalEntity_IsIndependent()
 {var f=await ReturnTestFixture.New();var first=await f.Create();var original=f.Scope;f.Scope=original with{LegalEntityId=Guid.NewGuid()};var second=await f.Create();Assert.Equal(201,second.StatusCode);Assert.False(second.IdempotentReplay);Assert.NotEqual(first.ReturnId,second.ReturnId);Assert.Equal(new long[]{1,1,1,1,1},await f.Counts());f.Scope=original;Assert.Equal(first.ReturnId,(await f.Create()).ReturnId);}
}
