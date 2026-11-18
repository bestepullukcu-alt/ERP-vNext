namespace Diten.InventoryService.Application.Features.CycleCounting;

/// <summary>
/// SB3/F-2 (final look 03, fixed in DL-1): the localization key names of the codes a screen or the Evidence Pack shows. A status, decision or
/// decision source is never stored as text — the label is the translation of its key (pack §4.2 <c>Decision</c>, §4.5). The count status and
/// the line status have SEPARATE prefixes, and each prefix names the Web resource family that owns its values; the DL-4 / DL-5 parity guard
/// (<c>CycleCountEvidenceTextParityTests</c>) measures the service's <c>CycleCountEvidenceText_{lang}.resx</c> against exactly that family.
/// </summary>
public static class CycleCountTextKeys
{
    /// <summary><c>CycleCount.Status.{CycleCountStatus}</c> — the count's state.</summary>
    public const string StatusPrefix = "CycleCount.Status.";

    /// <summary><c>CycleCount.LineStatus.{CycleCountLineStatus}</c> — a line's state (not the count's).</summary>
    public const string LineStatusPrefix = "CycleCount.LineStatus.";

    /// <summary><c>CycleCount.Decision.{Kind}</c> — Approved / Rejected / Recount (DL-3).</summary>
    public const string DecisionPrefix = "CycleCount.Decision.";

    /// <summary><c>CycleCount.Decision.Source.{Source}</c> — User / Tolerance (DL-3).</summary>
    public const string DecisionSourcePrefix = "CycleCount.Decision.Source.";

    /// <summary>
    /// prefix → the Web resource family (path under <c>frontend/Diten.Web/Resources/</c>, without <c>.{lang}.resx</c>) whose values the service
    /// file must equal in 7 languages. The count status is shown on the Cycle Count Dashboard; line status, decision and its source on the
    /// Variance Trace (pack §8.13, §11).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> WebFamilies = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [StatusPrefix] = "Views/Warehouse/CycleCounts/CycleCountsIndex",
        [LineStatusPrefix] = "Views/Warehouse/CountVariances/CountVariancesIndex",
        [DecisionPrefix] = "Views/Warehouse/CountVariances/CountVariancesIndex",
        [DecisionSourcePrefix] = "Views/Warehouse/CountVariances/CountVariancesIndex"
    };

    public static string Status(Domain.CycleCounting.Enums.CycleCountStatus status) => StatusPrefix + status;

    public static string LineStatus(Domain.CycleCounting.Enums.CycleCountLineStatus status) => LineStatusPrefix + status;
}
