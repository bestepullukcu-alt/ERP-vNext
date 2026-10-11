namespace Diten.CrmService.Application.Features.CycleCapacity.Read;

/// <summary>
/// WP-VP-FIX-1 (C3) — the READ-ONLY per-day sibling of <see cref="IWorkingDayCounter"/>: it asks CAND-CAP-0008 (the
/// platform working calendar) <i>"is this date a working day?"</i> through the SAME tenant-openable door
/// (<c>/api/platform/working-calendars/overrides/resolve</c>, op <c>is-working-day</c>,
/// <c>platform.working-calendar.override.read</c>) and the same transport. No new platform operation is involved.
/// <para><b>It never writes and it never invents.</b> <see cref="WorkingDayCheckResult.IsWorkingDay"/> is non-null only
/// when the calendar actually resolved the date; a forbidden / missing / unreachable calendar comes back as an
/// unresolved result, never as a guessed "Mon–Fri" answer — the caller decides what a fallback is and must say so.</para>
/// </summary>
public interface IWorkingDayChecker
{
    /// <param name="countryCode">Upper-cased ISO alpha-2.</param>
    /// <param name="legalEntityId">Optional narrowing (legal-entity scoped period); a business unit is never passed.</param>
    /// <param name="date">The date asked about.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<WorkingDayCheckResult> IsWorkingDayAsync(
        string countryCode,
        Guid? legalEntityId,
        DateOnly date,
        CancellationToken cancellationToken);
}

/// <summary>
/// One per-day answer. <see cref="Resolution"/> is a <see cref="Diten.CrmService.Domain.Entities.CycleCapacityResolutions"/>
/// value and <see cref="IsWorkingDay"/> is non-null only when it is <c>resolved</c>; <c>calendar_forbidden</c> stays apart
/// from <c>calendar_unresolved</c> (different fixes, F-RBAC-WC).
/// </summary>
public sealed record WorkingDayCheckResult(
    string Resolution,
    bool? IsWorkingDay,
    IReadOnlyList<string> ReasonCodes,
    string Reason,
    // WP-VP-3B (MK-9, additive) — the platform marked the date a HALF day (its holiday's isHalfDay, or the reason code
    // half_day_treated_as_working). Only meaningful on a resolved working day; false otherwise.
    bool IsHalfDay = false);
