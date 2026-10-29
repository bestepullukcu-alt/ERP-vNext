using System.Text.Json;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance;
public static class SupplierPerformanceRequestFingerprint
{
    // MOD-0147 §0.5: the stored canonical representation itself ("v1:" + JSON, as CarrierRequestFingerprint), never a hash.
    // supplierId as sent (before canonicalisation); metrics in order with duplicates, every value as text; notes absent = null.
    public static string Create(CreateEvaluationInput r) => "v1:" + JsonSerializer.Serialize(new
    {
        supplierId = r.SupplierId,
        periodStart = r.PeriodStart,
        periodEnd = r.PeriodEnd,
        metrics = r.Metrics.Select(m => new { metricCode = m.MetricCode, measuredValue = m.MeasuredValue, weight = m.Weight }),
        notes = r.Notes
    });
}
