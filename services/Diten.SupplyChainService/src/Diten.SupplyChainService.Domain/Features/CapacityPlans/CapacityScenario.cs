using Diten.SupplyChainService.Domain.Common;
namespace Diten.SupplyChainService.Domain.Features.CapacityPlans;
public sealed record CapacityConstraintReference(string ConstraintId, string Source, string SourceVersion);
public sealed record CapacityAdjustment(string ResourceRef, string Period, string AvailableCapacityDelta, string UomId);
public sealed class CapacityScenario : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid CapacityPlanId { get; set; }
    public string Name { get; set; } = "";
    public List<CapacityConstraintReference> ConstraintRefs { get; set; } = [];
    public List<CapacityAdjustment> Adjustments { get; set; } = [];
    public string Status { get; set; } = "Draft";
    public Guid CreatedBy { get; set; }
    public Guid CreatedEventId { get; set; }
}
