using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.SupplyChain.Shipments;

public sealed class CreateShipmentViewModel
{
    [Required, MinLength(1)] public string? SourceModule { get; set; }
    [Required, MinLength(1)] public string? SourceType { get; set; }
    [Required, MinLength(1)] public string? SourceDocumentId { get; set; }
    [Required, MinLength(1)] public string? WarehouseReferenceId { get; set; }
    [Required, MinLength(1)] public string? ShipToReference { get; set; }
    [Required] public DateTimeOffset PlannedShipAt { get; set; }
    [Required, MinLength(1)] public List<CreateShipmentLineViewModel>? Lines { get; set; }
    public DateTimeOffset? PlannedDeliverAt { get; set; }
}

public sealed class CreateShipmentLineViewModel
{
    [Required, MinLength(1)] public string? LineNumber { get; set; }
    [Required] public Guid ItemId { get; set; }
    [Required] public Guid SkuId { get; set; }
    [Required, RegularExpression(@"^-?[0-9]+(\.[0-9]+)?$")] public string? Quantity { get; set; }
    [Required, MinLength(1)] public string? UomId { get; set; }
    public string? InventoryReferenceId { get; set; }
}

public sealed class TransitionShipmentViewModel
{
    [Required, RegularExpression("^(Planned|Dispatched|InTransit|Delivered|Exception|Closed|Cancelled)$")]
    public string? TargetStatus { get; set; }

    [Required] public DateTimeOffset OccurredAt { get; set; }
    public string? ReasonCode { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
}

public sealed class CaptureShipmentPodViewModel
{
    [Required, MinLength(1)] public string? RecipientName { get; set; }
    [Required] public DateTimeOffset ReceivedAt { get; set; }
    [Required, MinLength(1)] public List<string>? EvidenceReferenceIds { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
}
