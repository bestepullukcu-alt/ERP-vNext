using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Enums;

namespace Diten.InventoryService.Domain.WavePlanning.Rules;

/// <summary>One exceeded business limit of a wave (pack §8.5): the limit and what the wave holds.</summary>
public sealed record WaveLimitWarning(WaveLimitKind Kind, int Limit, int Actual);

/// <summary>The totals of a wave's ACTIVE memberships (Planned or Issued), computed when read — not stock.</summary>
public sealed record WaveTotals(int OrderCount, int LineCount, int PlannedTaskCount)
{
    public static readonly WaveTotals Empty = new(0, 0, 0);
}

/// <summary>
/// MOD-0180 pure rules (pack §4.1, §4.5, §8.2, §8.3, §8.5, §12): the bounds of the form and the setting, the DL-1 transitions, the business
/// limits (a warning) and the technical ceiling (a refusal). No I/O.
/// </summary>
public static class WaveRules
{
    public const int MaxShipToFilterLength = 100;

    public const int MinMaxOrders = 1;
    public const int MaxMaxOrders = 1000;

    public const int MinMaxLines = 1;
    public const int MaxMaxLines = 20000;

    public const int MaxNoteLength = 500;

    /// <summary>Pack §12: a cancel / setting / recall / exception-action reason is 10..500 characters after trim.</summary>
    public const int MinReasonLength = 10;
    public const int MaxReasonLength = 500;

    /// <summary>Pack §12: one add / remove request names 1..200 outbounds.</summary>
    public const int MaxOutboundsPerRequest = 200;

    /// <summary>Pack §8.5 (Q-8 = A): the technical ceiling of one wave — blocking, apart from the business limits.</summary>
    public const int TechnicalMaxOrders = 500;
    public const int TechnicalMaxPlannedTasks = 2000;

    /// <summary>Pack §8.2 rule 5: the most suggestions when the wave names no limit.</summary>
    public const int DefaultSuggestionLimit = 200;

    /// <summary>Pack §4.5 defaults (D-6, D-11).</summary>
    public const int DefaultRiskMinutesBeforeCutoff = 90;
    public const int DefaultRiskProgressPercent = 70;
    public const int DefaultPendingTaskMinutes = 30;

    /// <summary>Pack §12 ranges of the setting.</summary>
    public const int MaxRiskMinutes = 1440;
    public const int MaxRiskPercent = 100;
    public const int MaxPendingMinutes = 1440;

    /// <summary>Pack §8.4: the cutoff may lie at most this far in the past when it is entered.</summary>
    public static readonly TimeSpan CutoffPastTolerance = TimeSpan.FromHours(1);

    /// <summary>The MOD-0178 outbound status and pick-issuance state an order must have to enter a wave (pack §8.2 rule 2).</summary>
    public const string EligibleOutboundStatus = "Allocated";
    public const string AllocatingOutboundStatus = "Allocating";
    public const string DeferredPickIssuance = "Deferred";

    /// <summary>Draft edits, membership changes, suggestions and cancel are allowed only on a Draft (pack §8.3).</summary>
    public static bool IsDraft(Wave wave) => wave.Status == WaveStatus.Draft;

    /// <summary>The membership states that count as "in the wave" (pack §4.2 <c>Active</c>).</summary>
    public static bool IsActive(WaveOrderState state) => state is WaveOrderState.Planned or WaveOrderState.Issued;

    /// <summary>A trimmed reason of 10..500 characters, or null.</summary>
    public static string? Reason(string? raw)
    {
        var reason = raw?.Trim();
        return reason is null || reason.Length is < MinReasonLength or > MaxReasonLength ? null : reason;
    }

    public static bool RiskMinutesInRange(int value) => value is >= 1 and <= MaxRiskMinutes;

    public static bool RiskPercentInRange(int value) => value is >= 1 and <= MaxRiskPercent;

    public static bool PendingMinutesInRange(int value) => value is >= 1 and <= MaxPendingMinutes;

    /// <summary>Pack §8.5 (D-7): the business limits the totals exceed. An excess is WRITTEN — these are warnings.</summary>
    public static IReadOnlyList<WaveLimitWarning> LimitWarnings(int? maxOrders, int? maxLines, WaveTotals totals)
    {
        var warnings = new List<WaveLimitWarning>(2);
        if (maxOrders is { } orders && totals.OrderCount > orders) warnings.Add(new WaveLimitWarning(WaveLimitKind.MaxOrders, orders, totals.OrderCount));
        if (maxLines is { } lines && totals.LineCount > lines) warnings.Add(new WaveLimitWarning(WaveLimitKind.MaxLines, lines, totals.LineCount));
        return warnings;
    }

    /// <summary>Pack §8.5: more than 500 orders or 2.000 planned pick tasks in one wave is refused (422 <c>WAVE_TOO_LARGE</c>).</summary>
    public static bool ExceedsTechnicalLimit(WaveTotals totals)
        => totals.OrderCount > TechnicalMaxOrders || totals.PlannedTaskCount > TechnicalMaxPlannedTasks;

    /// <summary>
    /// Pack §8.2 rule 5: how many orders "Suggest" may still return — the room left under <c>MaxOrders</c> (never negative); 200 when the wave
    /// names no order limit. The line limit is applied row by row by the caller (<see cref="FitsLines"/>).
    /// </summary>
    public static int SuggestionOrderRoom(int? maxOrders, WaveTotals totals)
        => maxOrders is { } max ? Math.Max(0, max - totals.OrderCount) : DefaultSuggestionLimit;

    /// <summary>Does an order of <paramref name="lineCount"/> lines still fit under the wave's line limit?</summary>
    public static bool FitsLines(int? maxLines, int linesSoFar, int lineCount) => maxLines is not { } max || linesSoFar + lineCount <= max;
}
