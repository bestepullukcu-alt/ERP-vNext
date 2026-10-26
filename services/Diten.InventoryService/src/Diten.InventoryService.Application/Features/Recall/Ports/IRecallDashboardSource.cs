namespace Diten.InventoryService.Application.Features.Recall.Ports;

/// <summary>
/// WP-0177-R6 — the in-process ledger read of the Hazırlık Paneli (pack §8.9): every outbound movement (the trace's outbound types,
/// <c>TraceRules.Outbound</c>) and every REVERSAL posted in the window, of ANY SKU, in the caller's company only (Tenant + LegalEntity —
/// another company's movements are never part of a company's KPI). Read only.
/// </summary>
public interface IRecallDashboardSource
{
    Task<IReadOnlyList<TraceTransaction>> OutboundInWindowAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
