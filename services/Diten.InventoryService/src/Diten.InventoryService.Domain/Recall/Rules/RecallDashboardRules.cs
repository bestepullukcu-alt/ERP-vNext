using Diten.InventoryService.Domain.Recall.Enums;

namespace Diten.InventoryService.Domain.Recall.Rules;

/// <summary>
/// WP-0177-R6 — the Hazırlık Paneli formulas (pack §8.9), pure. The panel never sums quantities across SKUs or units
/// (ASSUMPTION-0174-12).
/// </summary>
public static class RecallDashboardRules
{
    /// <summary>"Lot kapsamı" / "İzleme boşluğu" window (pack §8.9: 90 days).</summary>
    public const int WindowDays = 90;

    /// <summary>The drill chart: the last 12 completed runs (pack §8.9).</summary>
    public const int ChartRuns = 12;

    /// <summary>How many lotless movements the panel lists (the count is always the full count).</summary>
    public const int GapMovementsShown = 20;

    /// <summary>How many open cases the panel's table shows.</summary>
    public const int OpenCaseRowsShown = 20;

    /// <summary>"Açık gerçek vaka": every status but Closed / Cancelled.</summary>
    public static bool IsOpen(RecallCaseStatus status) => status is not (RecallCaseStatus.Closed or RecallCaseStatus.Cancelled);

    /// <summary>"Son tatbikat süresi" badge: Hedefte when the duration is within the target (minutes ≤ hours × 60), else Hedef aşıldı.</summary>
    public static bool OnTarget(int durationMinutes, int targetHours) => durationMinutes <= targetHours * 60;

    /// <summary>
    /// "Lot kapsamı" percent: with-lot ÷ all, one decimal; null when there is no outbound movement. A coverage that is not complete never
    /// shows 100 (99.96 % would round to 100 and hide a gap) — it caps at 99.9.
    /// </summary>
    public static decimal? CoveragePercent(int withLot, int total)
    {
        if (total <= 0) return null;
        var percent = Math.Round(withLot * 100m / total, 1, MidpointRounding.AwayFromZero);
        return withLot < total && percent >= 100m ? 99.9m : percent;
    }
}
