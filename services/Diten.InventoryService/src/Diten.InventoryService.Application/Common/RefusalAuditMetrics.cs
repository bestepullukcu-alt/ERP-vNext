using System.Diagnostics.Metrics;

namespace Diten.InventoryService.Application.Common;

/// <summary>
/// MOD-0173-FU03 §13 "Ret denetim satırı yazılamaz": a refusal row that could not be written is never silent — the writer logs a warning and
/// adds to this counter (AUD-001 §4.3 "diğer iş kayıtları": best effort, observable). The refused request's answer is unchanged.
/// </summary>
public static class RefusalAuditMetrics
{
    private static readonly Meter Meter = new("Diten.InventoryService.Audit");

    public static readonly Counter<long> Failed = Meter.CreateCounter<long>("inventory_refusal_audit_failed_total");
}
