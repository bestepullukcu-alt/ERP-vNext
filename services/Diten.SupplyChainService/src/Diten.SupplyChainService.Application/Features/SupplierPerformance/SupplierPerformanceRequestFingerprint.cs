using System.Globalization;
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

    // §0.5: submittedAt as the instant it denotes; comment absent = null, "" distinct; If-Match is not part of it.
    public static string Submit(SubmitEvaluationInput r) => "v1:" + JsonSerializer.Serialize(new
    {
        submittedAt = r.SubmittedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        comment = r.Comment
    });
}
