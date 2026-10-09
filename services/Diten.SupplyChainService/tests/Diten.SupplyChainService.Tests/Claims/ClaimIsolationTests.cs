using Diten.SupplyChainService.Api.Features.Claims;
using Diten.SupplyChainService.Application.Features.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Infrastructure.Features.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimIsolationTests
{
 [Theory][InlineData("/api/shipment-bundle/claims",true)][InlineData("/api/shipment-bundle/claims/123/transition",true)]
 [InlineData("/api/shipment-bundle/claimsXYZ",false)][InlineData("/api/shipment-bundle/returns",false)]
 public void FamilyMatch_IsSegmentBounded(string path,bool match) {var h=new DefaultHttpContext();h.Request.Path=path;Assert.Equal(match,ClaimContextMiddleware.IsClaimPath(h));}
 [Theory][InlineData("00000000-0000-0000-0000-000000000000",true)][InlineData("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA",true)]
 [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",false)][InlineData("{aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa}",false)]
 [InlineData(" aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",false)]
 public void WireUuid_AllowsNilButRequiresExactAsciiD(string text,bool valid)=>Assert.Equal(valid,ClaimWire.Uuid(text));

 // Component checks only: the principal is preauthenticated and the compact token
 // carries synthetic payload bytes. These do not claim JWT signature/HTTP uptake.
 private static readonly Guid Tenant=Guid.Parse("11111111-1111-1111-1111-111111111111");
 private static readonly Guid LegalEntity=Guid.Parse("22222222-2222-2222-2222-222222222222");
 private static readonly Guid Actor=Guid.Parse("33333333-3333-3333-3333-333333333333");
 private static DefaultHttpContext Request()
 {
  var http=new DefaultHttpContext();
  http.RequestServices=new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
  http.Request.Path="/api/shipment-bundle/claims";
  http.Request.Method="POST";http.Request.ContentType="application/json";
  http.Request.Headers["X-Correlation-Id"]="44444444-4444-4444-4444-444444444444";
  http.Request.Headers["Idempotency-Key"]="key";
  var payload=JsonSerializer.Serialize(new {tenant_id=Tenant,legal_entity_id=LegalEntity,sub=Actor});
  var segment=Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+','-').Replace('/','_');
  http.Request.Headers.Authorization="Bearer e30."+segment+".synthetic";
  http.User=new ClaimsPrincipal(new ClaimsIdentity(new[] {
   new Claim("tenant_id",Tenant.ToString()),new Claim("legal_entity_id",LegalEntity.ToString()),
   new Claim("sub",Actor.ToString()),new Claim("permission",ClaimPermissions.Create)},"component-only"));
  http.SetEndpoint(new Endpoint(_=>Task.CompletedTask,new EndpointMetadataCollection(new ClaimPermissionAttribute(ClaimPermissions.Create)),"component-only"));
  http.Response.Body=new MemoryStream();return http;
 }
 private static async Task<(bool Called,ClaimRequestContext Context,JsonElement? Error)> Invoke(DefaultHttpContext http)
 {
  var called=false;var context=new ClaimRequestContext();
  var middleware=new ClaimContextMiddleware(_=>{called=true;return Task.CompletedTask;});
  await middleware.InvokeAsync(http,context,new RequestContext(),Diten.SupplyChainService.Tests.Common.StubLegalEntityScopeValidator.Valid);
  http.Response.Body.Position=0;
  using var reader=new StreamReader(http.Response.Body,leaveOpen:true);
  var text=await reader.ReadToEndAsync();
  if(text.Length==0)return(called,context,null);
  using var json=JsonDocument.Parse(text);return(called,context,json.RootElement.Clone());
 }
 private static void Rejected(DefaultHttpContext http,(bool Called,ClaimRequestContext Context,JsonElement? Error) result,int status,string code)
 {
  Assert.False(result.Called);Assert.Equal(status,http.Response.StatusCode);Assert.True(result.Error.HasValue);
  var error=result.Error!.Value.GetProperty("error");Assert.Equal(code,error.GetProperty("code").GetString());
  Assert.Equal(http.Response.Headers["X-Correlation-Id"].ToString(),error.GetProperty("correlationId").GetString());
  Assert.Equal("v1",result.Error.Value.GetProperty("contractVersion").GetString());
 }
 [Fact] public async Task Component_UnauthenticatedPrecedesBadHeader()
 {
  var http=Request();http.User=new ClaimsPrincipal(new ClaimsIdentity());http.Request.Headers["X-Correlation-Id"]="bad";
  var result=await Invoke(http);Rejected(http,result,401,"UNAUTHENTICATED");Assert.Equal("Bearer",http.Response.Headers.WWWAuthenticate.ToString());
 }
 [Theory][InlineData("missing")][InlineData("duplicate")][InlineData("nil")]
 public async Task Component_UnusableTrustedIdentityForbidden(string kind)
 {
  var http=Request();var identity=(ClaimsIdentity)http.User.Identity!;var claim=identity.FindFirst("sub")!;
  if(kind=="duplicate")identity.AddClaim(new Claim("sub",Actor.ToString()));
  else {identity.RemoveClaim(claim);if(kind=="nil")identity.AddClaim(new Claim("sub",Guid.Empty.ToString()));}
  Rejected(http,await Invoke(http),403,"FORBIDDEN");
 }
 [Theory][InlineData(null)][InlineData("not-a-uuid")]
 public async Task Component_InvalidCorrelationHasGeneratedRejectionTrace(string? value)
 {
  var http=Request();if(value is null)http.Request.Headers.Remove("X-Correlation-Id");else http.Request.Headers["X-Correlation-Id"]=value;
  var result=await Invoke(http);Rejected(http,result,400,"INVALID_REQUEST");Assert.NotEqual(Guid.Empty,result.Context.CorrelationId);
 }
 [Fact] public async Task Component_OptionalScopeMismatchForbidden()
 {
  var http=Request();http.Request.Headers["X-Tenant-Id"]=Guid.NewGuid().ToString();Rejected(http,await Invoke(http),403,"FORBIDDEN");
 }
 [Theory][InlineData("duplicate")][InlineData("empty")][InlineData("overlong")]
 public async Task Component_InvalidIdempotencyKeysRejectWithoutCallingDownstream(string kind)
 {
  var http=Request();http.Request.Headers["Idempotency-Key"]=kind switch {
   "duplicate"=>new StringValues(new[]{"a","b"}),"empty"=>new StringValues(""),_=>new StringValues(string.Concat(Enumerable.Repeat("😀",129)))};
  Rejected(http,await Invoke(http),400,"INVALID_REQUEST");
 }
 [Theory][InlineData("comma,inside")][InlineData("unicode")]
 public async Task Component_ScalarKeyIsNotSplitOrCountedAsUtf16(string kind)
 {
  var key=kind=="unicode"?string.Concat(Enumerable.Repeat("😀",128)):kind;
  var http=Request();http.Request.Headers["Idempotency-Key"]=key;var result=await Invoke(http);
  Assert.True(result.Called);Assert.Null(result.Error);Assert.Equal(key,result.Context.IdempotencyKey);
 }
 [Theory][InlineData("TeNaNtId")][InlineData("LEGALENTITYID")][InlineData("tenant_ID")]
 [InlineData("legal_ENTITY_id")][InlineData("X-TENANT-ID")][InlineData("x-LEGAL-entity-ID")]
 public async Task Component_QueryScopeAliasesRejectAfterSingleDecode(string key)
 {
  var http=Request();http.Request.QueryString=new QueryString("?"+Uri.EscapeDataString(key)+"=ignored");
  Rejected(http,await Invoke(http),400,"INVALID_REQUEST");
 }
 [Fact] public async Task Component_OrdinaryUnknownQueryAndNilTraceProceedWithSignedScope()
 {
  var http=Request();http.Request.QueryString=new QueryString("?ordinaryUnknown=x&tenantName=foreign");
  http.Request.Headers["X-Correlation-Id"]=Guid.Empty.ToString();var result=await Invoke(http);
  Assert.True(result.Called);Assert.Null(result.Error);Assert.Equal(Guid.Empty,result.Context.CorrelationId);
  Assert.Equal(Tenant,result.Context.Scope.TenantId);Assert.Equal(LegalEntity,result.Context.Scope.LegalEntityId);
  Assert.Equal(Guid.Empty.ToString(),http.Response.Headers["X-Correlation-Id"].ToString());
 }
}
