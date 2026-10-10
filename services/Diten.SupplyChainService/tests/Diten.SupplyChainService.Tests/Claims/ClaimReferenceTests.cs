using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Features.Claims;
using Diten.SupplyChainService.Domain.Features.Claims;
using Diten.SupplyChainService.Infrastructure.Features.Claims;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Diten.SupplyChainService.Tests.Claims;
public sealed class ClaimReferenceTests
{
 [Theory][InlineData("Draft",false)][InlineData("Planned",false)][InlineData("Dispatched",true)][InlineData("InTransit",true)][InlineData("Delivered",true)][InlineData("Exception",true)][InlineData("Closed",true)][InlineData("Cancelled",false)]
 public void Eligibility_AllEightShipmentStates_MatchesPolicy(string status,bool eligible)
 {
  var claim=new Claim {ClaimedAmount="1"};var snapshot=new ClaimReferenceSnapshot(Guid.NewGuid(),status,null,null,Guid.NewGuid(),DateTimeOffset.UtcNow);
  var ex=Record.Exception(()=>ClaimLifecycle.ValidateCreate(claim,snapshot));
  if(eligible)Assert.Null(ex);else Assert.Equal("CLAIM_SHIPMENT_INELIGIBLE",Assert.IsType<ClaimFailureException>(ex).Code);
 }
 [Theory][InlineData("missing",503)][InlineData("null",503)][InlineData("empty",503)][InlineData("malformed",502)]
 public async Task Reader_UnusableRoot_DistinguishesIncompleteFromMalformed(string mode,int status)
 {
  var id=Guid.NewGuid();var json=Shipment(id);if(mode=="missing")json.Remove("lifecycleCorrelationId");else json["lifecycleCorrelationId"]=mode=="null"?null:mode=="empty"?"":"bad";
  var reader=Reader(json,out var transport,out _);var ex=await Assert.ThrowsAsync<ClaimFailureException>(()=>reader.ObserveAsync(new Claim{ShipmentId=id},default));
  Assert.Equal(status,ex.Status);Assert.Equal(status==502?"CLAIM_REFERENCE_INVALID":"CLAIM_REFERENCE_INCOMPLETE",ex.Code);Assert.Single(transport.Requests);
 }
 [Fact] public async Task Reader_RealNilRoot_PreservesNilAndUsesSeparateNonNilDependencyTrace()
 {
  var id=Guid.NewGuid();var reader=Reader(Shipment(id),out var transport,out var context);
  context.CorrelationId=Guid.Empty;
  var result=await reader.ObserveAsync(new Claim{ShipmentId=id},default);Assert.Equal(Guid.Empty,result.Root);
  Assert.Single(transport.Requests);var request=transport.Requests[0];Assert.Equal("GET",request.Method);
  Assert.Equal("/api/shipment-bundle/shipments/"+id,request.Path);Assert.Equal(context.Scope.TenantId.ToString(),request.Tenant);Assert.Equal(context.Scope.LegalEntityId.ToString(),request.Le);
  Assert.NotEqual(Guid.Empty,Guid.Parse(request.Correlation));Assert.NotEqual(context.CorrelationId,Guid.Parse(request.Correlation));
 }
 [Fact] public async Task Reader_MismatchedIdentity_IsMalformedNotNotFound()
 {
  var reader=Reader(Shipment(Guid.NewGuid()),out _,out _);var ex=await Assert.ThrowsAsync<ClaimFailureException>(()=>reader.ObserveAsync(new Claim{ShipmentId=Guid.NewGuid()},default));Assert.Equal(502,ex.Status);
 }
 [Theory][InlineData("Active")][InlineData("Suspended")][InlineData("Retired")]
 public async Task Reader_ExplicitMatchingCarrier_AllHistoricalStatusesQualify(string status)
 {
  var id=Guid.NewGuid();var carrier=Guid.NewGuid();var shipment=Shipment(id);shipment["carrierId"]=carrier.ToString();
  var handler=new BranchTransport(shipment.ToJsonString(),Carriers(carrier,status).ToJsonString());var reader=BranchReader(handler);
  var claim=new Claim {ShipmentId=id,CarrierId=carrier,ClaimedAmount="1"};var snapshot=await reader.ObserveAsync(claim,default);
  ClaimLifecycle.ValidateCreate(claim,snapshot);Assert.Equal(status,snapshot.CarrierStatus);Assert.Equal(carrier,snapshot.ShipmentCarrierId);
  Assert.Equal(new[]{"/api/shipment-bundle/shipments/"+id,"/api/shipment-bundle/carriers"},handler.Paths);
 }
 [Theory][InlineData("carrierCode")][InlineData("displayName")][InlineData("status")][InlineData("supportedModes")][InlineData("carrierId")]
 public async Task Reader_RequiredCarrierFieldMissing_IsMalformed502(string field)
 {
  var id=Guid.NewGuid();var carrier=Guid.NewGuid();var shipment=Shipment(id);shipment["carrierId"]=carrier.ToString();var carriers=Carriers(carrier,"Active");
  carriers["items"]![0]!.AsObject().Remove(field);var handler=new BranchTransport(shipment.ToJsonString(),carriers.ToJsonString());
  var ex=await Assert.ThrowsAsync<ClaimFailureException>(()=>BranchReader(handler).ObserveAsync(new Claim {ShipmentId=id,CarrierId=carrier},default));
  Assert.Equal(502,ex.Status);Assert.Equal("CLAIM_REFERENCE_INVALID",ex.Code);Assert.Equal(2,handler.Paths.Count);
 }
 [Fact] public async Task Reader_ExplicitCarrierNotInList_Generic404()
 {
  var id=Guid.NewGuid();var carrier=Guid.NewGuid();var shipment=Shipment(id);shipment["carrierId"]=carrier.ToString();
  var handler=new BranchTransport(shipment.ToJsonString(),Carriers(Guid.NewGuid(),"Active").ToJsonString());
  var ex=await Assert.ThrowsAsync<ClaimFailureException>(()=>BranchReader(handler).ObserveAsync(new Claim {ShipmentId=id,CarrierId=carrier},default));
  Assert.Equal(404,ex.Status);Assert.Equal("CLAIM_NOT_FOUND",ex.Code);
 }
/*
 * D187-01, owner ruling 2026-10-11. This test used to assert the OPPOSITE - a null shipment carrier
 * plus an explicit claim carrier was 422 - and that assertion was the undecided question in code form.
 * Nothing writes Shipment.CarrierId (MOD-0183:95 "no new assignment API"; :499 holds the assignment
 * surface), so the old rule made a claim against the carrier that actually carried the shipment
 * impossible: measured live 2026-10-10, 422 with the real carrier and 201 without one.
 *
 * The carrier is still not taken on trust - the reader resolves it against the scoped carrier list and
 * answers 404 when it is absent, which is what sets CarrierStatus.
 *
 * SABOTAGE: restore the old condition in ClaimLifecycle (compare to ShipmentCarrierId unconditionally)
 * and these three cases go red while the mismatch test below stays green.
 */
 [Theory][InlineData("Active")][InlineData("Suspended")][InlineData("Retired")]
 public async Task Reader_ExplicitCarrierWithAbsentShipmentCarrier_IsAccepted(string status)
 {
  var id=Guid.NewGuid();var carrier=Guid.NewGuid();var shipment=Shipment(id);shipment["carrierId"]=null;
  var handler=new BranchTransport(shipment.ToJsonString(),Carriers(carrier,status).ToJsonString());var claim=new Claim {ShipmentId=id,CarrierId=carrier,ClaimedAmount="1"};
  var snapshot=await BranchReader(handler).ObserveAsync(claim,default);Assert.Null(snapshot.ShipmentCarrierId);Assert.Equal(status,snapshot.CarrierStatus);
  ClaimLifecycle.ValidateCreate(claim,snapshot);Assert.Equal(2,handler.Paths.Count);
 }
 // The other half of the ruling: a shipment carrier that is PRESENT and DIFFERENT is still a mismatch,
 // so the guard keeps its full meaning the day an assignment surface lands.
 [Fact] public async Task Reader_ExplicitCarrierDiffersFromPresentShipmentCarrier_Mismatch422()
 {
  var id=Guid.NewGuid();var shipmentCarrier=Guid.NewGuid();var claimed=Guid.NewGuid();
  var shipment=Shipment(id);shipment["carrierId"]=shipmentCarrier.ToString();
  var handler=new BranchTransport(shipment.ToJsonString(),Carriers(claimed,"Active").ToJsonString());
  var claim=new Claim {ShipmentId=id,CarrierId=claimed,ClaimedAmount="1"};
  var snapshot=await BranchReader(handler).ObserveAsync(claim,default);Assert.Equal(shipmentCarrier,snapshot.ShipmentCarrierId);
  var ex=Assert.Throws<ClaimFailureException>(()=>ClaimLifecycle.ValidateCreate(claim,snapshot));
  Assert.Equal(422,ex.Status);Assert.Equal("CLAIM_CARRIER_MISMATCH",ex.Code);
 }
 [Fact] public async Task Reader_ExplicitCarrierWithMissingShipmentField_IncompleteBeforeCarrierRead()
 {
  var id=Guid.NewGuid();var handler=new BranchTransport(Shipment(id).ToJsonString(),Carriers(Guid.NewGuid(),"Active").ToJsonString());
  var ex=await Assert.ThrowsAsync<ClaimFailureException>(()=>BranchReader(handler).ObserveAsync(new Claim {ShipmentId=id,CarrierId=Guid.NewGuid()},default));
  Assert.Equal(503,ex.Status);Assert.Equal("CLAIM_REFERENCE_INCOMPLETE",ex.Code);Assert.Single(handler.Paths);
 }
 [Theory][InlineData("refusal",503,"CLAIM_REFERENCE_UNAVAILABLE")][InlineData("timeout",503,"CLAIM_REFERENCE_UNAVAILABLE")][InlineData("invalid-json",502,"CLAIM_REFERENCE_INVALID")][InlineData("not-found",404,"CLAIM_NOT_FOUND")]
 public async Task Reader_TransportFailures_MapWithoutRawErrorLeak(string fault,int status,string code)
 {
  var handler=new BranchTransport("{", "{}") {Fault=fault};
  var ex=await Assert.ThrowsAsync<ClaimFailureException>(()=>BranchReader(handler).ObserveAsync(new Claim {ShipmentId=Guid.NewGuid()},default));
  Assert.Equal(status,ex.Status);Assert.Equal(code,ex.Code);Assert.DoesNotContain("private-source-error",ex.Message);
 }
 [Fact] public async Task Reader_ProducerShipmentRootInvalid500_MapsToClaimReferenceInvalid()
 {
  var handler=new ProducerFailureTransport(HttpStatusCode.InternalServerError,
   "{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\",\"message\":\"SHIPMENT_ROOT_INVALID\",\"correlationId\":\"aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa\"},\"contractVersion\":\"v1\"}");
  var ex=await Assert.ThrowsAsync<ClaimFailureException>(()=>BranchReader(handler).ObserveAsync(new Claim{ShipmentId=Guid.NewGuid()},default));
  Assert.Equal(502,ex.Status);Assert.Equal("CLAIM_REFERENCE_INVALID",ex.Code);Assert.DoesNotContain("SHIPMENT_ROOT_INVALID",ex.Message);
 }
 [Theory]
 [InlineData(500,"{\"error\":{\"code\":\"INTERNAL_ERROR\",\"message\":\"private-source-error\",\"correlationId\":\"aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa\"},\"contractVersion\":\"v1\"}")]
 [InlineData(500,"not-json")]
 [InlineData(500,"{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\"},\"contractVersion\":\"v1\"}")]
 [InlineData(500,"{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\",\"message\":\"SHIPMENT_ROOT_INVALID\",\"correlationId\":\"not-a-uuid\"},\"contractVersion\":\"v1\"}")]
 [InlineData(500,"{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\",\"message\":\"SHIPMENT_ROOT_INVALID\",\"correlationId\":\"aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa\"},\"contractVersion\":\"v2\"}")]
 [InlineData(501,"{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\",\"message\":\"SHIPMENT_ROOT_INVALID\",\"correlationId\":\"aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa\"},\"contractVersion\":\"v1\"}")]
 public async Task Reader_OtherProducer5xx_RemainReferenceUnavailable(int status,string body)
 {
  var handler=new ProducerFailureTransport((HttpStatusCode)status,body);
  var ex=await Assert.ThrowsAsync<ClaimFailureException>(()=>BranchReader(handler).ObserveAsync(new Claim{ShipmentId=Guid.NewGuid()},default));
  Assert.Equal(503,ex.Status);Assert.Equal("CLAIM_REFERENCE_UNAVAILABLE",ex.Code);Assert.DoesNotContain("private-source-error",ex.Message);
 }
 // Q447: a 403 from either dependency read is the caller lacking supplychain.shipments.read or supplychain.carriers.read,
 // so it is a denial and not an outage (Q420). Claims answers it with its annex's code: claims-semantics-v3.0.0.md:163
 // "403 FORBIDDEN includes scope/identity/action failures". A 401 stays 503 on purpose: Claims has already validated the
 // same token with the same key, so a dependency 401 means the two services disagree about trust.
 [Theory]
 [InlineData("shipment",HttpStatusCode.Forbidden,403,"FORBIDDEN",1)]
 [InlineData("carrier",HttpStatusCode.Forbidden,403,"FORBIDDEN",2)]
 [InlineData("shipment",HttpStatusCode.Unauthorized,503,"CLAIM_REFERENCE_UNAVAILABLE",1)]
 [InlineData("carrier",HttpStatusCode.Unauthorized,503,"CLAIM_REFERENCE_UNAVAILABLE",2)]
 public async Task Reader_DependencyRefusesCaller_403IsDenial401StaysUnavailable(string refusingRead,HttpStatusCode refusal,int status,string code,int reads)
 {
  var id=Guid.NewGuid();var carrier=Guid.NewGuid();var shipment=Shipment(id);shipment["carrierId"]=carrier.ToString();
  var handler=new RefusingTransport(shipment.ToJsonString(),Carriers(carrier,"Active").ToJsonString(),refusingRead=="shipment"?"/api/shipment-bundle/shipments/"+id:"/api/shipment-bundle/carriers",refusal);
  var ex=await Assert.ThrowsAsync<ClaimFailureException>(()=>BranchReader(handler).ObserveAsync(new Claim {ShipmentId=id,CarrierId=carrier},default));
  Assert.Equal(status,ex.Status);Assert.Equal(code,ex.Code);Assert.Equal(reads,handler.Paths.Count);Assert.DoesNotContain("private-source-error",ex.Message);
 }
 private sealed class RefusingTransport(string shipment,string carriers,string refusedPath,HttpStatusCode refusal):HttpMessageHandler
 {
  public List<string> Paths {get;}=[];
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   var path=request.RequestUri!.PathAndQuery;Paths.Add(path);
   if(path==refusedPath)return Task.FromResult(new HttpResponseMessage(refusal){Content=new StringContent("{\"error\":{\"code\":\"FORBIDDEN\",\"message\":\"private-source-error\"},\"contractVersion\":\"v1\"}")});
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(path=="/api/shipment-bundle/carriers"?carriers:shipment)});
  }
 }
 private static JsonObject Carriers(Guid id,string status)=>new(){["items"]=new JsonArray(new JsonObject{["carrierId"]=id.ToString(),["carrierCode"]="C1",["displayName"]="Carrier",["status"]=status,["supportedModes"]=new JsonArray("Road")}),["total"]=1,["contractVersion"]="v1"};
 private static ClaimReferenceReader BranchReader(HttpMessageHandler handler)=>new(new HttpClient(handler),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Claims:ReferenceBaseUrl"]="http://reference.invalid/"}).Build(),new ClaimRequestContext {Scope=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()),CorrelationId=Guid.NewGuid(),Authorization="Bearer test-only"});
 private sealed class BranchTransport(string shipment,string carriers):HttpMessageHandler
 {
  public string? Fault {get;init;} public List<string> Paths {get;}=[];
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   Assert.Equal(HttpMethod.Get,request.Method);var path=request.RequestUri!.PathAndQuery;Paths.Add(path);
   if(Fault=="refusal")throw new HttpRequestException("private-source-error");
   if(Fault=="timeout")throw new TaskCanceledException("private-source-error");
   var status=Fault=="not-found"?HttpStatusCode.NotFound:HttpStatusCode.OK;
   var body=path=="/api/shipment-bundle/carriers"?carriers:shipment;
   return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(body)});
  }
 }
 private sealed class ProducerFailureTransport(HttpStatusCode status,string body):HttpMessageHandler
 {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>
   Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(body)});
 }
 private static JsonObject Shipment(Guid id)=>new(){["shipmentId"]=id.ToString(),["status"]="Delivered",["sourceModule"]="MOD-0141",["sourceType"]="SALES_ORDER",["warehouseReferenceId"]="w",["shipToReference"]="s",["lines"]=new JsonArray(),["contractVersion"]="v1",["lifecycleCorrelationId"]=Guid.Empty.ToString()};
 private static ClaimReferenceReader Reader(JsonObject shipment,out ReferenceTransport transport,out ClaimRequestContext context)
 {
  transport=new ReferenceTransport(shipment.ToJsonString());context=new(){Scope=new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()),CorrelationId=Guid.NewGuid(),Authorization="Bearer fixture-not-authentication"};
  return new(new HttpClient(transport),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Claims:ReferenceBaseUrl"]="http://reference.invalid/"}).Build(),context);
 }
 internal sealed class ReferenceTransport(string body):HttpMessageHandler
 {
  public List<(string Method,string Path,string Tenant,string Le,string Correlation)> Requests {get;}=[];
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){Requests.Add((r.Method.Method,r.RequestUri!.PathAndQuery,r.Headers.GetValues("X-Tenant-Id").Single(),r.Headers.GetValues("X-Legal-Entity-Id").Single(),r.Headers.GetValues("X-Correlation-Id").Single()));return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)});}
 }
}
