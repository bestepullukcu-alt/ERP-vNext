namespace Diten.SupplyChainService.Domain.Features.SupplierPerformance;
/// <summary>One metric as stored and answered: the code, the measured value as sent, and the policy weight (pack §0.0).</summary>
public sealed record EvaluationMetricResult(string MetricCode, string Score, string Weight);
