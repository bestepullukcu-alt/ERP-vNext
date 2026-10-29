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
    public Guid? LifecycleCorrelationId { get; set; }
    public ProofOfDelivery? Pod { get; set; }
    // MOD-0183 §23.4 (DL-1): the current dispatch attempt (kept once Completed — MOD-0186 reads its issues) and the
    // append-only list of closed (Compensated) attempts, so every movement of the shipment can be traced.
    public DispatchAttempt? DispatchAttempt { get; set; }
    public DispatchAttempt[] DispatchHistory { get; set; } = [];
    public DispatchLateWrite[] DispatchLateWrites { get; set; } = [];

    /// <summary>The attempt number the next opened attempt gets: 1, or one more than the last closed one.</summary>
    public int NextDispatchAttemptNumber() => DispatchHistory.Length == 0 ? 1 : DispatchHistory.Max(a => a.N) + 1;
    /// <summary>3.4.0 <c>stockIssued</c>: true when Completed; null when the shipment never had an attempt; false otherwise.</summary>
    public bool? StockIssued() => DispatchAttempt?.State == DispatchState.Completed ? true
        : DispatchAttempt is null && DispatchHistory.Length == 0 ? null : false;
    /// <summary>3.4.0 <c>dispatchState</c>: the open attempt's state, None when there is none.</summary>
    public string DispatchStateWire() => DispatchAttempt?.State switch
    {
        DispatchState.InProgress => "InProgress",
        DispatchState.CompensationFailed => "CompensationFailed",
        _ => "None"
    };
}
