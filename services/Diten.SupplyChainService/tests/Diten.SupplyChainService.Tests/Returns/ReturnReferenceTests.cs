using Xunit;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.Returns;
using Diten.SupplyChainService.Application.Features.Returns;
using Diten.SupplyChainService.Persistence.Features.Returns;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Diten.SupplyChainService.Tests.Returns;
[Collection("Returns Mongo")]
public sealed class ReturnReferenceTests
{
 private static readonly Guid Shipment=Guid.Parse("11111111-1111-1111-1111-111111111111");
 private static System.Text.Json.Nodes.JsonObject Good()=>System.Text.Json.Nodes.JsonNode.Parse($$"""{"shipmentId":"{{Shipment}}","status":"Delivered","sourceModule":"test","sourceType":"test","warehouseReferenceId":"W","shipToReference":"to","lines":[{"lineNumber":"1","quantity":"10","uomId":"EA","itemId":"22222222-2222-2222-2222-222222222222","skuId":"33333333-3333-3333-3333-333333333333"}],"contractVersion":"v1","lifecycleCorrelationId":"00000000-0000-0000-0000-000000000000"}""")!.AsObject();
 private static ReturnReferenceSnapshot Read(System.Text.Json.Nodes.JsonObject node)=>Diten.SupplyChainService.Infrastructure.Features.Returns.ReturnReferenceReader.ParseShipment(JsonDocument.Parse(node.ToJsonString()).RootElement,Shipment,DateTimeOffset.UtcNow);
 [Fact] public void Root_PersistedNil_IsPreserved(){Assert.Equal(Guid.Empty,Read(Good()).Root);}
 [Theory][InlineData("missing",503,"RETURN_SHIPMENT_ROOT_UNAVAILABLE")][InlineData("null",503,"RETURN_SHIPMENT_ROOT_UNAVAILABLE")][InlineData("bad",502,"RETURN_SHIPMENT_ROOT_INVALID")][InlineData("",502,"RETURN_SHIPMENT_ROOT_INVALID")]
 public void Root_Unusable_ExactFailure(string value,int status,string code){var n=Good();if(value=="missing")n.Remove("lifecycleCorrelationId");else n["lifecycleCorrelationId"]=value=="null"?null:value;var ex=Assert.Throws<ReturnFailureException>(()=>Read(n));Assert.Equal(status,ex.Status);Assert.Equal(code,ex.Code);}
 [Theory][InlineData("sourceModule",502)][InlineData("shipmentId",503)][InlineData("status",503)]
 public void Reference_Omission_RequiredVersusOptionalDecisionField(string key,int status){var n=Good();n.Remove(key);Assert.Equal(status,Assert.Throws<ReturnFailureException>(()=>Read(n)).Status);}
 [Fact] public void Reference_DuplicateOrNonpositiveLines_RejectsMalformed()
 {var n=Good();n["lines"]!.AsArray().Add(n["lines"]![0]!.DeepClone());Assert.Equal(502,Assert.Throws<ReturnFailureException>(()=>Read(n)).Status);n=Good();n["lines"]![0]!["quantity"]="0";Assert.Equal(502,Assert.Throws<ReturnFailureException>(()=>Read(n)).Status);}
 [Fact] public async Task Reference_MockedTransport_OnlyShipmentGetAndTrustedScope()
 {
  var scope=new ReturnScope(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());var captured=new List<HttpRequestMessage>();
  using var client=new HttpClient(new CaptureHandler(request=>{captured.Add(request);return new(System.Net.HttpStatusCode.OK){Content=new StringContent(Good().ToJsonString())};}));
  var context=new ReturnRequestContext{Scope=scope,CorrelationId=Guid.Empty,Authorization="Bearer mocked-not-validated"};
  var config=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Returns:ReferenceBaseUrl","http://producer.invalid/"}}).Build();
  var reader=new Diten.SupplyChainService.Infrastructure.Features.Returns.ReturnReferenceReader(client,config,context);
  var snapshot=await reader.ObserveAsync(new ReturnOrder{ShipmentId=Shipment},default);Assert.Equal(Guid.Empty,snapshot.Root);
  Assert.Single(captured);Assert.Equal(HttpMethod.Get,captured[0].Method);Assert.Equal("/api/shipment-bundle/shipments/"+Shipment,captured[0].RequestUri!.AbsolutePath);Assert.Equal(scope.TenantId.ToString(),captured[0].Headers.GetValues("X-Tenant-Id").Single());
 }
 [Fact] public async Task ProducerShipmentRootInvalid500_MapsToReturnsRootInvalid()
 {
  using var client=new HttpClient(new CaptureHandler(_=>new(System.Net.HttpStatusCode.InternalServerError)
  {Content=new StringContent("{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\",\"message\":\"SHIPMENT_ROOT_INVALID\",\"correlationId\":\"aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa\"},\"contractVersion\":\"v1\"}")}));
  var context=new ReturnRequestContext{Scope=new ReturnScope(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()),CorrelationId=Guid.NewGuid(),Authorization="Bearer producer-token"};
  var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Returns:ReferenceBaseUrl","http://producer.invalid/"}}).Build();
  var reader=new Diten.SupplyChainService.Infrastructure.Features.Returns.ReturnReferenceReader(client,config,context);
  var ex=await Assert.ThrowsAsync<ReturnFailureException>(()=>reader.ObserveAsync(new ReturnOrder{ShipmentId=Shipment},default));
  Assert.Equal(502,ex.Status);Assert.Equal("RETURN_SHIPMENT_ROOT_INVALID",ex.Code);
 }
 [Theory]
 [InlineData(500,"{\"error\":{\"code\":\"INTERNAL_ERROR\"},\"contractVersion\":\"v1\"}")]
 [InlineData(500,"not-json")]
 [InlineData(500,"{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\"},\"contractVersion\":\"v2\"}")]
 [InlineData(501,"{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\"},\"contractVersion\":\"v1\"}")]
 [InlineData(401,"{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\"},\"contractVersion\":\"v1\"}")]
 [InlineData(403,"{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\"},\"contractVersion\":\"v1\"}")]
 public async Task OtherProducerFailures_RemainDependencyUnavailable(int status,string body)
 {
  var ex=await ObserveFailure(new CaptureHandler(_=>new((System.Net.HttpStatusCode)status){Content=new StringContent(body)}));
  Assert.Equal(503,ex.Status);Assert.Equal("DEPENDENCY_UNAVAILABLE",ex.Code);
 }
 [Fact] public async Task RefusedProducer_RemainsDependencyUnavailable()
 {
  var ex=await ObserveFailure(new AsyncFailureHandler(_=>Task.FromException<HttpResponseMessage>(new HttpRequestException("refused"))));
  Assert.Equal(503,ex.Status);Assert.Equal("DEPENDENCY_UNAVAILABLE",ex.Code);
 }
 [Fact] public async Task TimedOutProducer_RemainsDependencyUnavailable()
 {
  var ex=await ObserveFailure(new AsyncFailureHandler(_=>Task.FromCanceled<HttpResponseMessage>(new CancellationToken(true))));
  Assert.Equal(503,ex.Status);Assert.Equal("DEPENDENCY_UNAVAILABLE",ex.Code);
 }
 [Fact] public async Task MalformedSuccessfulRoot_RemainsReturnsRootInvalid()
 {
  var body=Good();body["lifecycleCorrelationId"]="malformed-root";
  var ex=await ObserveFailure(new CaptureHandler(_=>new(System.Net.HttpStatusCode.OK){Content=new StringContent(body.ToJsonString())}));
  Assert.Equal(502,ex.Status);Assert.Equal("RETURN_SHIPMENT_ROOT_INVALID",ex.Code);
 }
 private static async Task<ReturnFailureException> ObserveFailure(HttpMessageHandler handler)
 {
  using var client=new HttpClient(handler);
  var context=new ReturnRequestContext{Scope=new ReturnScope(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()),CorrelationId=Guid.NewGuid(),Authorization="Bearer producer-token"};
  var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Returns:ReferenceBaseUrl","http://producer.invalid/"}}).Build();
  var reader=new Diten.SupplyChainService.Infrastructure.Features.Returns.ReturnReferenceReader(client,config,context);
  return await Assert.ThrowsAsync<ReturnFailureException>(()=>reader.ObserveAsync(new ReturnOrder{ShipmentId=Shipment},default));
 }
 private sealed class CaptureHandler(Func<HttpRequestMessage,HttpResponseMessage> send):HttpMessageHandler
 {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(send(request));}
 private sealed class AsyncFailureHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
 {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>send(request);}
 [Theory][InlineData("0")][InlineData("-0.00")][InlineData("-1")]
 public async Task LocalInvalidQuantity_RejectsBeforeReferenceLookup(string quantity)
 {var f=await ReturnTestFixture.New();var result=await f.Create(quantity);Assert.Equal(422,result.StatusCode);Assert.Equal("INVALID_RETURN_QUANTITY",result.ErrorCode);Assert.Equal(0,f.Observations);Assert.Equal(new long[5],await f.Counts());}
 [Fact]public async Task LocalDuplicateLines_RejectsBeforeReferenceLookup()
 {var f=await ReturnTestFixture.New();var result=await f.Create(lines:[new("1","1","EA"),new("1","1","EA")]);Assert.Equal(422,result.StatusCode);Assert.Equal("DUPLICATE_RETURN_LINE",result.ErrorCode);Assert.Equal(0,f.Observations);Assert.Equal(new long[5],await f.Counts());}
}
