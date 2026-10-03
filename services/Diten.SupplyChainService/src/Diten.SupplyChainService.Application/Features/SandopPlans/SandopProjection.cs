using System.Text.Json;
using Diten.SupplyChainService.Domain.Features.SandopPlans;
namespace Diten.SupplyChainService.Application.Features.SandopPlans;
public static class SandopProjection
{ static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web);
 public static string Plan(SandopPlan p)=>JsonSerializer.Serialize(new{sandopPlanId=p.Id,name=p.Name,horizonStart=p.HorizonStart,horizonEnd=p.HorizonEnd,demandPlanId=p.DemandPlanId,demandPlanVersion=p.DemandPlanVersion,status=p.Status,currentSnapshotId=p.CurrentSnapshotId,createdAt=p.CreatedAt,contractVersion="v1"},Options);
 public static string Snapshot(SandopSnapshot s)=>JsonSerializer.Serialize(new{snapshotId=s.Id,sandopPlanId=s.PlanId,provenance=s.Provenance,supplyInputRefs=s.SupplyInputRefs,capturedAt=s.CapturedAt,contractVersion="v1"},Options);
 public static string SignOff(SandopSignOff s)=>JsonSerializer.Serialize(new{signOffId=s.Id,sandopPlanId=s.PlanId,snapshotId=s.SnapshotId,role=s.Role,decision=s.Decision,comment=s.Comment,decidedBy=s.DecidedBy,decidedAt=s.DecidedAt,contractVersion="v1"},Options);
 public static string Items(IEnumerable<string> items)=>JsonSerializer.Serialize(new { items=items.Select(x=>JsonDocument.Parse(x).RootElement).ToArray(), contractVersion="v1" });
}
