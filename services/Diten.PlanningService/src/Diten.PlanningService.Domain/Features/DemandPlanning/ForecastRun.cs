namespace Diten.PlanningService.Domain.Features.DemandPlanning;

// Immutable evidence for one explicit SKU x Warehouse method selection.
public sealed class ForecastRun : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid PlanningCycleId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid SkuId { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
    public string BaseUomId { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Variant { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public DateOnly HistoryAsOfWeekStart { get; set; }
    public string HistoryContentSha256 { get; set; } = string.Empty;
    public bool UnitVerificationPending { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = [];
    public decimal? WapeRatio { get; set; }
    public decimal? Mae { get; set; }
    public decimal? Bias { get; set; }
    public decimal? Mase { get; set; }
    public List<ForecastHorizonSummary> ByWeeksAhead { get; set; } = [];
    public Guid SelectedBy { get; set; }
    public DateTimeOffset SelectedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
}

public sealed record ForecastHorizonSummary(int WeeksAhead, int Count,
    decimal Mae, decimal? WapeRatio, decimal Bias, decimal? Mase);

// Append-only selection audit. A later choice gets a new record and revision.
public sealed class ForecastSelectionAudit : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid PlanningCycleId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid ForecastRunId { get; set; }
    public Guid SkuId { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
    public string? PreviousMethod { get; set; }
    public string NewMethod { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
}