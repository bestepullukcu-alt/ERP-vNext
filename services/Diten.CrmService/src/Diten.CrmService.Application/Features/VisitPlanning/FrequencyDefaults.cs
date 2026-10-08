using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-4L (1) — THE rule for a target whose frequency is unknown (no policy, or one that cannot be resolved): ONE visit
/// per working week (user decision 2026-10-08, "sıklığı tanımsız olanlar haftada 1"). The period then needs
/// <c>UnitsIn("week", period)</c> visits — its working weeks; a week that is all holiday does not count.
/// <para>The ONE place: the planning engine (<see cref="FrequencyExtendPlanner"/>) and the 3D status reader
/// (<c>ContactPeriodStatusReader.RequiredInPeriod</c>) both call <see cref="UnknownRequiredInPeriod"/> — the two once
/// disagreed (3A / 3D, CT fix ccd93de04) and must not again. <c>frequencyStatus</c> stays <c>unknown</c> (where the number
/// came from); <see cref="Weekly"/> is the extra <c>frequencyDefault</c> field the screens read ("haftada 1 (varsayılan)").</para>
/// </summary>
public static class FrequencyDefaults
{
    /// <summary>Visits per working week for an unknown frequency.</summary>
    public const int UnknownPerWeek = 1;

    /// <summary>The <c>frequencyDefault</c> value of an unknown frequency (slot, doctor content, 3D status).</summary>
    public const string Weekly = "weekly";

    /// <summary>The unscheduled reason of a weekly default visit its week could not hold: it is skipped, not shifted (every
    /// later week has its own visit — no chain of shifted visits).</summary>
    public const string WeekFullSkipped = "week_full_skipped";

    /// <summary>The visits an unknown-frequency target needs in the period: one per working week.</summary>
    public static int UnknownRequiredInPeriod(PlanningPeriodFrame period)
        => UnknownPerWeek * FrequencyExtendPlanner.UnitsIn(FrequencyPeriodType.Week, period);
}
