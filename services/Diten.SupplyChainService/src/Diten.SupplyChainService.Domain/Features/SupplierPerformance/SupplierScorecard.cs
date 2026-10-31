namespace Diten.SupplyChainService.Domain.Features.SupplierPerformance;
/// <summary>The immutable publication of a PUBLISHED evaluation (pack §3). Submit produces it; nothing recomputes it.</summary>
public sealed record SupplierScorecard(Guid ScorecardId, Guid EvaluationId, string SupplierId, string PeriodStart, string PeriodEnd,
    IReadOnlyList<EvaluationMetricResult> Metrics, string OverallScore, string RiskLevel, DateTimeOffset PublishedAt, Guid CorrelationId)
{
    public static SupplierScorecard? From(SupplierEvaluation e) =>
        e is { Status: EvaluationStatus.PUBLISHED, ScorecardId: { } id, PublishedAt: { } at }
            ? new(id, e.Id, e.SupplierId, e.PeriodStart, e.PeriodEnd, e.Metrics, e.OverallScore, e.RiskLevel, at, e.PublishCorrelationId ?? e.CorrelationId)
            : null;
}
