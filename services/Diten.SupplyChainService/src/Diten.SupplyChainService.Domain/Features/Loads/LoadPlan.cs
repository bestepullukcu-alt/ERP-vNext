using Diten.SupplyChainService.Domain.Common;
namespace Diten.SupplyChainService.Domain.Features.Loads;
public sealed class LoadPlan : EntityBase
{
 public Guid LegalEntityId { get; set; }
 public Guid CreatedBy { get; set; }
 public Guid? UpdatedBy { get; set; }
 public string LoadNumber { get; set; } = "";
 public Guid CarrierId { get; set; }
 public List<Guid> ShipmentIds { get; set; } = [];
 public string Mode { get; set; } = "";
 public string PlannedDepartAt { get; set; } = "";
 public List<LoadStop> Stops { get; set; } = [];
 public LoadStatus Status { get; set; }
 public Guid CorrelationRoot { get; set; }
 public Guid LastEventId { get; set; }
}
