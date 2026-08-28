using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Services;

/// <summary>
/// MOD-0029-FU14 — the two date rules of the external document register (GMG-QMS-SOP-0001 §10.2, §10.3):
/// the monitoring cadence and the 10-working-day impact assessment deadline.
///
/// Working days are Monday–Friday only. A holiday calendar is deliberately NOT implemented here — that is a
/// tenant-configurable concern for a later FU; this calculator is the seam it would replace.
/// </summary>
public static class ExternalDocumentScheduleCalculator
{
    /// <summary>SOP §10.3 — GMP/GDP/PV/RA impact must be assessed within 10 working days of the trigger.</summary>
    public const int ImpactAssessmentWorkingDays = 10;

    /// <summary>
    /// The next monitoring due date for a cadence. <see cref="ExternalMonitoringFrequency.OnTrigger"/> returns null:
    /// it is event-driven, so it can never be "overdue" on a schedule.
    /// </summary>
    public static DateTimeOffset? NextCheckDueDate(ExternalMonitoringFrequency frequency, DateTimeOffset from) =>
        frequency switch
        {
            ExternalMonitoringFrequency.Weekly => from.AddDays(7),
            ExternalMonitoringFrequency.Monthly => from.AddMonths(1),
            ExternalMonitoringFrequency.Quarterly => from.AddMonths(3),
            ExternalMonitoringFrequency.SemiAnnual => from.AddMonths(6),
            ExternalMonitoringFrequency.Annual => from.AddYears(1),
            ExternalMonitoringFrequency.OnTrigger => null,
            _ => from.AddYears(1)
        };

    /// <summary>
    /// Adds working days (Mon–Fri) to a date. Day 0 is the trigger date itself, so the result is the date
    /// <paramref name="workingDays"/> business days later, skipping weekends.
    /// </summary>
    public static DateTimeOffset AddWorkingDays(DateTimeOffset from, int workingDays)
    {
        var result = from;
        var remaining = workingDays;
        while (remaining > 0)
        {
            result = result.AddDays(1);
            if (result.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                remaining--;
            }
        }

        return result;
    }

    /// <summary>
    /// SOP §10.3 — the assessment due date. A GMP/GDP/PV/RA impact gets the 10-working-day clock; anything else
    /// gets a routine 30-calendar-day target so a non-regulated impact still carries a deadline.
    /// </summary>
    public static DateTimeOffset ImpactAssessmentDueDate(DateTimeOffset triggerDate, bool hasRegulatedImpact) =>
        hasRegulatedImpact
            ? AddWorkingDays(triggerDate, ImpactAssessmentWorkingDays)
            : triggerDate.AddDays(30);

    /// <summary>The impact domains that start the 10-working-day clock (SOP §10.3).</summary>
    public static bool HasRegulatedImpact(bool gmp, bool gdp, bool pv, bool ra) => gmp || gdp || pv || ra;
}
