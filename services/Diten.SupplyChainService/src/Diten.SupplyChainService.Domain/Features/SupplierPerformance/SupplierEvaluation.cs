using Diten.SupplyChainService.Domain.Common;
namespace Diten.SupplyChainService.Domain.Features.SupplierPerformance;
public sealed class SupplierEvaluation : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    /// <summary>The canonical supplier code Procurement answered (pack §0.0 D2): a master uuid in the request is resolved to it.</summary>
    public string SupplierId { get; set; } = "";
    public string PeriodStart { get; set; } = "";
    public string PeriodEnd { get; set; } = "";
    public List<EvaluationMetricResult> Metrics { get; set; } = [];
    public string OverallScore { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public EvaluationStatus Status { get; set; }
    /// <summary>True for DRAFT and PUBLISHED; the partial filter of the one-active-evaluation-per-period index (pack §0.2).</summary>
    public bool IsActivePeriod { get; set; }
    public string PolicyRevision { get; set; } = "";
    public string? Notes { get; set; }
    public Guid CorrelationId { get; set; }
    // Set by submit (DL-2); a scorecard is the published view of its evaluation.
    public Guid? ScorecardId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
