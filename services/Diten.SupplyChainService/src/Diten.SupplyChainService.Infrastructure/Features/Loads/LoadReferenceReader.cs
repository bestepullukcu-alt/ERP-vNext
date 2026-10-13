using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Diten.SupplyChainService.Application.Features.Loads;
using Diten.SupplyChainService.Domain.Features.Loads;
namespace Diten.SupplyChainService.Infrastructure.Features.Loads;
public sealed class LoadReferenceReader(HttpClient client,IConfiguration configuration,LoadRequestContext context):ILoadReferenceReader
{
 private static void Invalid() => throw new LoadFailureException(502,"DEPENDENCY_RESPONSE_INVALID");
 private static bool Has(JsonElement e,string n)=>e.TryGetProperty(n,out _);
 private static bool String(JsonElement e)=>e.ValueKind==JsonValueKind.String;
 private static bool Uuid(JsonElement e)=>String(e)&&LoadWire.Uuid(e.GetString());
 private static bool Date(JsonElement e)=>String(e)&&LoadWire.Instant(e.GetString());
 private static bool Nullable(JsonElement e,Func<JsonElement,bool> test)=>e.ValueKind==JsonValueKind.Null||test(e);
 private static void Required(JsonElement e,string[] keys) {if(e.ValueKind!=JsonValueKind.Object||keys.Any(x=>!Has(e,x)))Invalid();}
 private static void Property(JsonElement e,string n,Func<JsonElement,bool> test) {if(e.TryGetProperty(n,out var v)&&!test(v))Invalid();}
 private static void Strings(JsonElement e,params string[] names) {foreach(var n in names)Property(e,n,String);}
 private static bool EnumValue(JsonElement e,params string[] values)=>String(e)&&values.Contains(e.GetString());
 private static bool Array(JsonElement e,Func<JsonElement,bool> test)=>e.ValueKind==JsonValueKind.Array&&e.EnumerateArray().All(test);
 private static bool Line(JsonElement e)
 {
 if(!LoadWire.Object(e,["lineNumber","itemId","skuId","quantity","uomId"],["lineNumber","itemId","skuId","quantity","uomId","inventoryReferenceId"]))return false;
 return String(e.GetProperty("lineNumber"))&&Uuid(e.GetProperty("itemId"))&&Uuid(e.GetProperty("skuId"))&&String(e.GetProperty("uomId"))&&String(e.GetProperty("quantity"))&&Regex.IsMatch(e.GetProperty("quantity").GetString()!,@"\A-?[0-9]+(\.[0-9]+)?\z")&&(!e.TryGetProperty("inventoryReferenceId",out var r)||Nullable(r,String));
 }
 private static bool Pod(JsonElement e)
 {if(e.ValueKind==JsonValueKind.Null)return true; if(e.ValueKind!=JsonValueKind.Object||new[]{"recipientName","receivedAt","evidenceReferenceIds"}.Any(x=>!Has(e,x)))return false;return String(e.GetProperty("recipientName"))&&Date(e.GetProperty("receivedAt"))&&Array(e.GetProperty("evidenceReferenceIds"),String)&&(!e.TryGetProperty("note",out var n)||Nullable(n,String));}
 public static void ValidateShipment(JsonElement e)
 {
 Required(e,["sourceModule","sourceType","warehouseReferenceId","shipToReference","lines","contractVersion"]);
 Strings(e,"sourceModule","sourceType","warehouseReferenceId","shipToReference","shipmentNumber","sourceDocumentId");
 Property(e,"contractVersion",x=>EnumValue(x,"v1"));Property(e,"shipmentId",Uuid);Property(e,"status",x=>EnumValue(x,"Draft","Planned","Dispatched","InTransit","Delivered","Exception","Closed","Cancelled"));
 Property(e,"carrierId",x=>Nullable(x,Uuid));Property(e,"loadId",x=>Nullable(x,Uuid));Property(e,"plannedShipAt",Date);Property(e,"plannedDeliverAt",x=>Nullable(x,Date));Property(e,"actualDeliverAt",x=>Nullable(x,Date));Property(e,"lines",x=>Array(x,Line));Property(e,"pod",Pod);
 }
 public static void ValidateCarriers(JsonElement e)
 {
 Required(e,["items","total","contractVersion"]);Property(e,"contractVersion",x=>EnumValue(x,"v1"));Property(e,"total",x=>LoadWire.Integer(x,out _));
 if(e.GetProperty("items").ValueKind!=JsonValueKind.Array)Invalid();var seen=new HashSet<Guid>();
 foreach(var item in e.GetProperty("items").EnumerateArray())
 {Required(item,["carrierId","carrierCode","displayName","status","supportedModes"]);Property(item,"carrierId",Uuid);Strings(item,"carrierCode","displayName");Property(item,"status",x=>EnumValue(x,"Active"));Property(item,"supportedModes",x=>Array(x,m=>String(m)&&LoadWire.Modes.Contains(m.GetString())));if(!seen.Add(Guid.Parse(item.GetProperty("carrierId").GetString()!)))Invalid();}
 }
 private async Task<JsonElement> Get(string path,bool shipment,CancellationToken ct)
 {
 var configured=configuration["Loads:ReferenceBaseUrl"];
 if(!Uri.TryCreate(configured,UriKind.Absolute,out var root)||root.Scheme is not ("http" or "https"))throw new LoadFailureException(503,"DEPENDENCY_UNAVAILABLE");
 using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(root,path));
 request.Headers.TryAddWithoutValidation("Authorization",context.Authorization);request.Headers.Add("X-Tenant-Id",context.Scope.TenantId.ToString());request.Headers.Add("X-Legal-Entity-Id",context.Scope.LegalEntityId.ToString());request.Headers.Add("X-Correlation-Id",context.CorrelationId.ToString());
 try
 {
 using var response=await client.SendAsync(request,ct);
 if(shipment&&response.StatusCode==HttpStatusCode.NotFound)throw new LoadFailureException(404,"SHIPMENT_NOT_FOUND");
 if((int)response.StatusCode>=500||response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)throw new LoadFailureException(503,"DEPENDENCY_UNAVAILABLE");
 if(response.StatusCode!=HttpStatusCode.OK)Invalid();
 using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return json.RootElement.Clone();
 }catch(HttpRequestException){throw new LoadFailureException(503,"DEPENDENCY_UNAVAILABLE");}catch(TaskCanceledException)when(!ct.IsCancellationRequested){throw new LoadFailureException(503,"DEPENDENCY_UNAVAILABLE");}catch(JsonException){throw new LoadFailureException(502,"DEPENDENCY_RESPONSE_INVALID");}
 }
 public async Task<string> ObserveAsync(LoadPlan load,Guid? existingId,CancellationToken ct)
 {
 var snapshots=new List<LoadReferenceSnapshot>();
 var carriers=await Get("api/shipment-bundle/carriers?status=Active",false,ct);ValidateCarriers(carriers);snapshots.Add(new("Carrier",DateTimeOffset.UtcNow,carriers.GetRawText()));
 var found=carriers.GetProperty("items").EnumerateArray().Where(x=>Guid.Parse(x.GetProperty("carrierId").GetString()!)==load.CarrierId).ToArray();
 if(found.Length==0)throw new LoadFailureException(404,"CARRIER_NOT_FOUND");
 if(!found[0].GetProperty("supportedModes").EnumerateArray().Any(x=>x.GetString()==load.Mode))throw new LoadFailureException(422,"CARRIER_MODE_UNSUPPORTED");
 foreach(var id in load.ShipmentIds)
 {
 var shipment=await Get("api/shipment-bundle/shipments/"+id,true,ct);ValidateShipment(shipment);
 if(new[]{"shipmentId","status","carrierId","loadId"}.Any(x=>!Has(shipment,x)))throw new LoadFailureException(503,"REFERENCE_STATE_UNAVAILABLE");
 if(Guid.Parse(shipment.GetProperty("shipmentId").GetString()!)!=id)Invalid();
 if(shipment.GetProperty("status").GetString() is not ("Draft" or "Planned"))throw new LoadFailureException(422,"SHIPMENT_NOT_ELIGIBLE");
 if(shipment.GetProperty("carrierId").ValueKind!=JsonValueKind.Null&&Guid.Parse(shipment.GetProperty("carrierId").GetString()!)!=load.CarrierId)throw new LoadFailureException(422,"SHIPMENT_CARRIER_MISMATCH");
 if(shipment.GetProperty("loadId").ValueKind!=JsonValueKind.Null&&Guid.Parse(shipment.GetProperty("loadId").GetString()!)!=existingId)throw new LoadFailureException(409,"SHIPMENT_ALREADY_ASSIGNED");
 snapshots.Add(new("Shipment",DateTimeOffset.UtcNow,shipment.GetRawText()));
 }
 return JsonSerializer.Serialize(snapshots);
 }
}
