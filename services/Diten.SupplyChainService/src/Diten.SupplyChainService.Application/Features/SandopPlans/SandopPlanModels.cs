using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Application.Features.SandopPlans;
public sealed record SandopCommandContext(SandopScope Scope,string Key,Guid Correlation);
public sealed record SandopQueryContext(SandopScope Scope,Guid Correlation);
public static class SandopWire
{
 public static SandopResult? Validate(SandopAction action,JsonElement body)
 {
  if(body.ValueKind!=JsonValueKind.Object) return SandopResult.Error(400,"INVALID_REQUEST");
  string[] required=action switch { SandopAction.Create=>["name","horizonStart","horizonEnd","demandPlanId","demandPlanVersion"],SandopAction.Capture=>["demandPlanId","demandPlanVersion","sourceCapturedAt","sourceChecksum"],SandopAction.SignOff=>["snapshotId","role","decision"],_=>[] };
  string[] allowed=action switch { SandopAction.SignOff=>["snapshotId","role","decision","comment"],SandopAction.Capture=>["demandPlanId","demandPlanVersion","sourceCapturedAt","sourceChecksum","supplyInputRefs"],_=>required };
  if(body.EnumerateObject().Any(p=>!allowed.Contains(p.Name,StringComparer.Ordinal))||required.Any(p=>!body.TryGetProperty(p,out var v)||v.ValueKind!=JsonValueKind.String)) return SandopResult.Error(400,"INVALID_REQUEST");
  if(required.Any(p=>string.IsNullOrEmpty(body.GetProperty(p).GetString()))) return SandopResult.Error(400,"INVALID_REQUEST");
  if(action==SandopAction.Create)
  { if(!DateOnly.TryParseExact(body.GetProperty("horizonStart").GetString(),"yyyy-MM-dd",out var start)||!DateOnly.TryParseExact(body.GetProperty("horizonEnd").GetString(),"yyyy-MM-dd",out var end)||end<start) return SandopResult.Error(400,"INVALID_REQUEST"); }
  if(action==SandopAction.Capture)
  { if(!System.Text.RegularExpressions.Regex.IsMatch(body.GetProperty("sourceCapturedAt").GetString()!,@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$")||!DateTimeOffset.TryParse(body.GetProperty("sourceCapturedAt").GetString(),out _)) return SandopResult.Error(400,"INVALID_REQUEST");
   if(body.TryGetProperty("supplyInputRefs",out var refs)&&(refs.ValueKind!=JsonValueKind.Array||refs.EnumerateArray().Any(x=>x.ValueKind!=JsonValueKind.Object||!new[]{"source","resourceId","resourceVersion"}.All(n=>x.TryGetProperty(n,out var v)&&v.ValueKind==JsonValueKind.String&&!string.IsNullOrEmpty(v.GetString()))||x.EnumerateObject().Any(p=>!new[]{"source","resourceId","resourceVersion"}.Contains(p.Name)))))return SandopResult.Error(400,"INVALID_REQUEST"); }
  if(action==SandopAction.SignOff)
  { if(!Guid.TryParseExact(body.GetProperty("snapshotId").GetString(),"D",out _))return SandopResult.Error(400,"INVALID_REQUEST");
   if(!new[]{"DemandPlanning","SupplyPlanning","Finance","Operations","Executive"}.Contains(body.GetProperty("role").GetString())||!new[]{"Approved","Rejected"}.Contains(body.GetProperty("decision").GetString()))return SandopResult.Error(400,"INVALID_REQUEST");
   if(body.TryGetProperty("comment",out var comment)&&(comment.ValueKind!=JsonValueKind.String||comment.GetString()!.Length>2000))return SandopResult.Error(400,"INVALID_REQUEST"); }
  return null;
 }
}
