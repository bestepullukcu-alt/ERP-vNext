namespace Diten.PlanningService.Domain.Features.DemandPlanning;

// Accepted history is append-only. Corrections are separate source records, never edits here.
public sealed class AcceptedDemandHistoryRow : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid BatchId { get; set; }
    public int BatchRowNumber { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceRecordKey { get; set; } = string.Empty;
    public string SourceFileSha256 { get; set; } = string.Empty;
    public Guid SkuId { get; set; }
    public string SkuLevel { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public DateOnly OccurredOn { get; set; }
    public decimal OriginalQuantity { get; set; }
    public string OriginalUomId { get; set; } = string.Empty;
    public decimal BaseQuantity { get; set; }
    public string BaseUomId { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; }
    public string ConversionContractId { get; set; } = string.Empty;
    public string ConversionContractVersion { get; set; } = string.Empty;
    public decimal AcceptedQuantity { get; set; }
    public string AcceptedUomId { get; set; } = string.Empty;
    public bool UnitVerificationPending { get; set; }
    public string StockoutEvidenceState { get; set; } = "Unknown";
    public string ContentSha256 { get; set; } = string.Empty;
    public Guid AcceptedByActorId { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
    public string AcceptanceReason { get; set; } = string.Empty;
    public string RequestKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
}

public sealed class DemandHistoryAcceptanceAudit : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid AcceptedRowId { get; set; }
    public Guid BatchId { get; set; }
    public int BatchRowNumber { get; set; }
    public string SourceRecordKey { get; set; } = string.Empty;
    public string Action { get; set; } = "RowAccepted";
    public Guid ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ContentSha256 { get; set; } = string.Empty;
}

public sealed class AcceptedHistoryUnitSlot : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid SkuId { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
    public string UomId { get; set; } = string.Empty;
}
