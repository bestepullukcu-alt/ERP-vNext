using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns;
public sealed record ReturnResponse(Guid ReturnId,string RmaNumber,Guid ShipmentId,string Status,bool IdempotentReplay,string ContractVersion="v1");
public sealed record ReturnSummary(Guid ReturnId,string RmaNumber,Guid ShipmentId,string Status);
public sealed record ReturnListResponse(IReadOnlyList<ReturnSummary> Items,int Total,string ContractVersion="v1");
public static class ReturnWire
{
 public static bool Uuid(string? value)=>value is not null&&Regex.IsMatch(value,@"\A[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\z");
 public static string NormalizeInstant(string value)=>ReturnInstant.Normalize(value);
 public static bool Instant(string? value) {if(value is null)return false;try{NormalizeInstant(value);return true;}catch(FormatException){return false;}catch(ArgumentException){return false;}}
 public static bool Object(JsonElement value,string[] required,string[] allowed)=>value.ValueKind==JsonValueKind.Object&&required.All(k=>value.TryGetProperty(k,out _))&&value.EnumerateObject().All(p=>allowed.Contains(p.Name,StringComparer.Ordinal))&&value.EnumerateObject().Select(p=>p.Name).Distinct(StringComparer.Ordinal).Count()==value.EnumerateObject().Count();
 public static bool Text(JsonElement value)=>value.ValueKind==JsonValueKind.String;
 public static bool CreateValid(JsonElement b)
 {
  if(!Object(b,["shipmentId","reasonCode","lines"],["shipmentId","reasonCode","lines","evidenceReferenceIds"]))return false;
  return Text(b.GetProperty("shipmentId"))&&Uuid(b.GetProperty("shipmentId").GetString())&&Text(b.GetProperty("reasonCode"))&&
  b.GetProperty("lines").ValueKind==JsonValueKind.Array&&b.GetProperty("lines").GetArrayLength()>0&&
  b.GetProperty("lines").EnumerateArray().All(l=>Object(l,["shipmentLineNumber","quantity","uomId"],["shipmentLineNumber","quantity","uomId"])&&Text(l.GetProperty("shipmentLineNumber"))&&Text(l.GetProperty("uomId"))&&Text(l.GetProperty("quantity"))&&ReturnQuantity.TryParse(l.GetProperty("quantity").GetString(),out _))&&
  (!b.TryGetProperty("evidenceReferenceIds",out var e)||(e.ValueKind==JsonValueKind.Array&&e.EnumerateArray().All(Text)));
 }
 public static bool TransitionValid(JsonElement b)=>Object(b,["targetStatus","occurredAt"],["targetStatus","occurredAt","inventoryTransactionReferenceId","dispositionCode"])&&Text(b.GetProperty("targetStatus"))&&Enum.GetNames<ReturnStatus>().Contains(b.GetProperty("targetStatus").GetString())&&Text(b.GetProperty("occurredAt"))&&Instant(b.GetProperty("occurredAt").GetString())&&NullableText(b,"inventoryTransactionReferenceId")&&NullableText(b,"dispositionCode");
 public static bool NullableText(JsonElement b,string key)=>!b.TryGetProperty(key,out var v)||v.ValueKind is JsonValueKind.Null or JsonValueKind.String;
 public static string? OptionalText(JsonElement b,string key)=>b.TryGetProperty(key,out var v)?v.GetString():null;
 public static ReturnOrder Plan(JsonElement b)=>new() {ShipmentId=Guid.Parse(b.GetProperty("shipmentId").GetString()!),ReasonCode=b.GetProperty("reasonCode").GetString()!,Lines=b.GetProperty("lines").EnumerateArray().Select(l=>new ReturnLine(l.GetProperty("shipmentLineNumber").GetString()!,l.GetProperty("quantity").GetString()!,l.GetProperty("uomId").GetString()!)).ToList(),EvidenceReferenceIds=b.TryGetProperty("evidenceReferenceIds",out var e)?e.EnumerateArray().Select(v=>v.GetString()!).ToList():[]};
}
