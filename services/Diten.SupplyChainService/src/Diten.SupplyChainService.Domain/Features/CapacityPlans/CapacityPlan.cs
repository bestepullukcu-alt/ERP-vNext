using Diten.SupplyChainService.Domain.Common;
namespace Diten.SupplyChainService.Domain.Features.CapacityPlans;
public sealed class CapacityPlan : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly HorizonStart { get; set; }
    public DateOnly HorizonEnd { get; set; }
    public string DemandPlanId { get; set; } = "";
    public string DemandPlanVersion { get; set; } = "";
    public DateTimeOffset SourceCapturedAt { get; set; }
    public string SourceChecksum { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public Guid CreatedBy { get; set; }
    public Guid CreatedEventId { get; set; }
}
