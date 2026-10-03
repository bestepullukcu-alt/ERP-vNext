using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Diten.SupplyChainService.Domain.Features.Claims;
namespace Diten.SupplyChainService.Application.Features.Claims;
public sealed record ClaimResponse(Guid ClaimId,string ClaimNumber,Guid ShipmentId,string Status,string? ApprovedAmount,bool IdempotentReplay,string ContractVersion="v1");
public sealed record ClaimSummary(Guid ClaimId,string ClaimNumber,Guid ShipmentId,string Status,string ClaimedAmount,string Currency);
public sealed record ClaimListResponse(IReadOnlyList<ClaimSummary> Items,int Total,string ContractVersion="v1");
public static class ClaimWire
{
 public static bool Uuid(string? s)=>s is not null&&Regex.IsMatch(s,@"\A[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\z");
 public static bool Instant(string? s)
 {
  if(s is null)return false;
  var m=Regex.Match(s,@"\A([0-9]{4}-[0-9]{2}-[0-9]{2})[Tt]([0-9]{2}:[0-9]{2}:[0-9]{2})(?:\.[0-9]+)?([Zz]|[+-][0-9]{2}:[0-9]{2})\z");
  if(!m.Success)return false;
  // Validate calendar and RFC3339 offset without converting, truncating, or normalizing original text.
  var clock=m.Groups[2].Value;
  // RFC3339 permits a leap-second representation; calendar validation uses 59 only as a probe.
  // The original 60 text is retained in the request, fingerprint, audit and event.
  if(clock.EndsWith(":60",StringComparison.Ordinal))clock=clock[..^2]+"59";
  if(!DateTime.TryParseExact(m.Groups[1].Value+"T"+clock,"yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))return false;
  var zone=m.Groups[3].Value;return zone is "Z" or "z" || (int.Parse(zone.Substring(1,2),CultureInfo.InvariantCulture)<=23&&int.Parse(zone.Substring(4,2),CultureInfo.InvariantCulture)<=59);
 }
 public static bool Text(JsonElement e)=>e.ValueKind==JsonValueKind.String;
 public static bool Object(JsonElement e,string[] required,string[] allowed)=>e.ValueKind==JsonValueKind.Object&&required.All(x=>e.TryGetProperty(x,out _))&&e.EnumerateObject().All(x=>allowed.Contains(x.Name))&&e.EnumerateObject().Select(x=>x.Name).Distinct().Count()==e.EnumerateObject().Count();
 public static string? Optional(JsonElement e,string key)=>e.TryGetProperty(key,out var v)&&v.ValueKind!=JsonValueKind.Null?v.GetString():null;
 private static bool OptionalText(JsonElement e,string key)=>!e.TryGetProperty(key,out var v)||v.ValueKind is JsonValueKind.Null or JsonValueKind.String;
 public static bool CreateValid(JsonElement b)
 {
  if(!Object(b,["shipmentId","reasonCode","claimedAmount","currency"],["shipmentId","carrierId","reasonCode","claimedAmount","currency","evidenceReferenceIds"]))return false;
  return Text(b.GetProperty("shipmentId"))&&Uuid(b.GetProperty("shipmentId").GetString())&&Text(b.GetProperty("reasonCode"))&&Text(b.GetProperty("claimedAmount"))&&ExactClaimAmount.TryParse(b.GetProperty("claimedAmount").GetString(),out _)&&Text(b.GetProperty("currency"))&&Regex.IsMatch(b.GetProperty("currency").GetString()!,@"\A[A-Z]{3}\z")&&OptionalText(b,"carrierId")&&(Optional(b,"carrierId") is not string carrier||Uuid(carrier))&&(!b.TryGetProperty("evidenceReferenceIds",out var evidence)||evidence.ValueKind==JsonValueKind.Array&&evidence.EnumerateArray().All(Text));
 }
 public static bool TransitionValid(JsonElement b)
 {
  if(!Object(b,["targetStatus","occurredAt"],["targetStatus","occurredAt","resolutionCode","approvedAmount","note"]))return false;
  return Text(b.GetProperty("targetStatus"))&&Enum.GetNames<ClaimStatus>().Contains(b.GetProperty("targetStatus").GetString())&&Text(b.GetProperty("occurredAt"))&&Instant(b.GetProperty("occurredAt").GetString())&&OptionalText(b,"resolutionCode")&&OptionalText(b,"note")&&OptionalText(b,"approvedAmount")&&(Optional(b,"approvedAmount") is not string amount||ExactClaimAmount.TryParse(amount,out _));
 }
 public static Claim Plan(JsonElement b)=>new()
 {
  ShipmentId=Guid.Parse(b.GetProperty("shipmentId").GetString()!),CarrierId=Optional(b,"carrierId") is string carrier?Guid.Parse(carrier):null,
  ReasonCode=b.GetProperty("reasonCode").GetString()!,ClaimedAmount=b.GetProperty("claimedAmount").GetString()!,Currency=b.GetProperty("currency").GetString()!,
  EvidenceReferenceIds=b.TryGetProperty("evidenceReferenceIds",out var e)?e.EnumerateArray().Select(x=>x.GetString()!).ToList():[]
 };
 public static string Permission(ClaimStatus target)=>"supplychain.claims."+(target switch {ClaimStatus.Open=>"create",ClaimStatus.Investigating or ClaimStatus.Withdrawn=>"investigate",ClaimStatus.Settled=>"settle",_=>"decide"});
}
