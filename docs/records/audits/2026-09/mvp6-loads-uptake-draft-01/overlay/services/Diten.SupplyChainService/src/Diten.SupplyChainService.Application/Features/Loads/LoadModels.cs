using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Numerics;
using Diten.SupplyChainService.Domain.Features.Loads;
namespace Diten.SupplyChainService.Application.Features.Loads;
public sealed record LoadResponse(Guid LoadId,string LoadNumber,string Status,bool IdempotentReplay,string ContractVersion="v1");
public sealed record LoadSummary(Guid LoadId,string LoadNumber,Guid CarrierId,IReadOnlyList<Guid> ShipmentIds,string Status,Guid? LifecycleCorrelationId);
public sealed record LoadListResponse(IReadOnlyList<LoadSummary> Items,int Total,string ContractVersion="v1");
public static class LoadWire
{
 public static readonly string[] Modes = ["Road","Air","Sea","Rail","Parcel"];
 public static bool Uuid(string? s) => s is not null && Regex.IsMatch(s,@"\A[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\z");
 public static bool Instant(string? s) { try { NormalizeInstant(s!); return true; } catch { return false; } }
 public static string NormalizeInstant(string s)
 {
  var m=Regex.Match(s,@"\A([0-9]{4}-[0-9]{2}-[0-9]{2})[Tt]([0-9]{2}:[0-9]{2}:[0-9]{2})(?:\.([0-9]+))?([Zz]|[+-][0-9]{2}:[0-9]{2})\z");
  if(!m.Success)throw new FormatException();
  var fraction=m.Groups[3].Value.TrimEnd('0');
  var second=DateTimeOffset.ParseExact(m.Groups[1].Value+"T"+m.Groups[2].Value+m.Groups[4].Value.ToUpperInvariant(),new[]{"yyyy-MM-dd'T'HH:mm:ss'Z'","yyyy-MM-dd'T'HH:mm:sszzz"},CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal).ToUniversalTime();
  return second.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture)+(fraction.Length==0?"":"."+fraction)+"Z";
 }
 public static bool Text(JsonElement e) => e.ValueKind == JsonValueKind.String;
 public static bool Object(JsonElement e,string[] required,string[] allowed) => e.ValueKind == JsonValueKind.Object && required.All(x=>e.TryGetProperty(x,out _)) && e.EnumerateObject().All(x=>allowed.Contains(x.Name)) && e.EnumerateObject().Select(x=>x.Name).Distinct().Count()==e.EnumerateObject().Count();
 public static bool Integer(JsonElement e,out string number)
 {
  number="";if(e.ValueKind!=JsonValueKind.Number)return false;
  var m=Regex.Match(e.GetRawText(),@"\A(-?)([0-9]+)(?:\.([0-9]+))?(?:[eE]([+-]?[0-9]+))?\z");
  if(!m.Success)return false;
  var digits=(m.Groups[2].Value+m.Groups[3].Value).TrimStart('0');
  if(digits.Length==0){number="0";return true;}
  var exponent=(m.Groups[4].Success?BigInteger.Parse(m.Groups[4].Value,CultureInfo.InvariantCulture):BigInteger.Zero)-m.Groups[3].Length;
  while(digits.EndsWith('0')){digits=digits[..^1];exponent++;}
  if(exponent<0)return false;
  number=m.Groups[1].Value+digits+(exponent==0?"": "e"+exponent.ToString(CultureInfo.InvariantCulture));return true;
 }
 public static bool PositiveInteger(JsonElement e)=>Integer(e,out var n)&&n!="0"&&!n.StartsWith('-');
 public static bool CreateValid(JsonElement b)
 {
  string[] keys=["carrierId","shipmentIds","mode","plannedDepartAt","stops"];
  if(!Object(b,keys,keys)) return false;
  return Text(b.GetProperty("carrierId")) && Uuid(b.GetProperty("carrierId").GetString()) && Text(b.GetProperty("mode")) && Modes.Contains(b.GetProperty("mode").GetString()) && Text(b.GetProperty("plannedDepartAt")) && Instant(b.GetProperty("plannedDepartAt").GetString()) &&
  b.GetProperty("shipmentIds").ValueKind==JsonValueKind.Array && b.GetProperty("shipmentIds").GetArrayLength()>0 && b.GetProperty("shipmentIds").EnumerateArray().All(x=>Text(x)&&Uuid(x.GetString())) &&
  b.GetProperty("stops").ValueKind==JsonValueKind.Array && b.GetProperty("stops").GetArrayLength()>=2 && b.GetProperty("stops").EnumerateArray().All(x=>Object(x,["sequence","locationReferenceId","action"],["sequence","locationReferenceId","action"]) && PositiveInteger(x.GetProperty("sequence"))&&Text(x.GetProperty("locationReferenceId"))&&Text(x.GetProperty("action"))&&new[]{"Pickup","Delivery","Return"}.Contains(x.GetProperty("action").GetString()));
 }
 public static bool TransitionValid(JsonElement b) => Object(b,["targetStatus","occurredAt"],["targetStatus","occurredAt","note"]) && Text(b.GetProperty("targetStatus")) && Enum.GetNames<LoadStatus>().Contains(b.GetProperty("targetStatus").GetString()) && Text(b.GetProperty("occurredAt")) && Instant(b.GetProperty("occurredAt").GetString()) && (!b.TryGetProperty("note",out var n)||n.ValueKind is JsonValueKind.Null or JsonValueKind.String);
 public static LoadPlan Plan(JsonElement b) => new() { CarrierId=Guid.Parse(b.GetProperty("carrierId").GetString()!), ShipmentIds=b.GetProperty("shipmentIds").EnumerateArray().Select(x=>Guid.Parse(x.GetString()!)).ToList(), Mode=b.GetProperty("mode").GetString()!, PlannedDepartAt=NormalizeInstant(b.GetProperty("plannedDepartAt").GetString()!), Stops=b.GetProperty("stops").EnumerateArray().Select(x=>{ Integer(x.GetProperty("sequence"),out var n);return new LoadStop(n,x.GetProperty("locationReferenceId").GetString()!,x.GetProperty("action").GetString()!); }).ToList() };
}
