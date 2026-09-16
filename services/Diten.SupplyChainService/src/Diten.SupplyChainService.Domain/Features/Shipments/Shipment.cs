using Diten.SupplyChainService.Domain.Common;
namespace Diten.SupplyChainService.Domain.Features.Shipments;
public sealed class Shipment : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceDocumentId { get; set; } = string.Empty;
    public string? SourceSystem { get; set; }
    public string? ExternalRef { get; set; }
    public string WarehouseReferenceId { get; set; } = string.Empty;
    public string ShipToReference { get; set; } = string.Empty;
    public Guid? CarrierId { get; set; }
    public Guid? LoadPlanId { get; set; }
    public ShipmentLine[] Lines { get; set; } = [];
    public DateTimeOffset PlannedShipAt { get; set; }
    public DateTimeOffset? PlannedDeliverAt { get; set; }
    public DateTimeOffset? ActualDeliverAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public ShipmentStatus Status { get; set; }
    public Guid CorrelationId { get; set; }
    public ProofOfDelivery? Pod { get; set; }
}
