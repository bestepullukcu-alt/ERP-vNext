namespace Diten.CrmService.Application.Features.CycleCapacity.Read;

/// <summary>
/// MOD-0155 FU06 — the READ-ONLY working-day seam. It asks CAND-CAP-0008 (the platform working calendar) one question,
/// <i>"how many working days lie between these two dates?"</i>, and it is the ONLY door this feature has onto that
/// capability.
/// <para><b>It never writes and it never invents.</b> A count is returned only when the calendar actually resolved
/// one; otherwise <see cref="WorkingDayCountResult.WorkingDays"/> is null and the resolution says why. A default month
/// length (<i>"about 22 working days"</i>) is <b>forbidden</b>: a plausible-looking guess is worse than no answer,
/// because nobody can tell it apart from a real one.</para>
/// <para><b>Why the returned count already excludes weekends and holidays.</b> The platform operation is
/// <c>working-days-between</c>, which walks the range day by day and counts only days its calendar calls working.
/// Consumers must therefore subtract only their OWN deductions (meetings, training, leave) — subtracting weekends or
/// public holidays again double-counts them.</para>
/// </summary>
public interface IWorkingDayCounter
{
    /// <summary>
    /// Working days in <paramref name="from"/>..<paramref name="to"/> INCLUSIVE, for a country and — optionally — one
    /// legal entity.
    /// </summary>
    /// <param name="countryCode">Upper-cased ISO alpha-2.</param>
    /// <param name="legalEntityId">
    /// Optional narrowing, passed through when the cycle period is legal-entity scoped. A business unit is never
    /// passed: it is a reference-data value code, not an organization-unit id (F-WC-ORG-UNIT).
    /// </param>
    Task<WorkingDayCountResult> CountAsync(
        string countryCode,
        Guid? legalEntityId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

/// <summary>
/// One working-day answer. <see cref="Resolution"/> is a
/// <see cref="Diten.CrmService.Domain.Entities.CycleCapacityResolutions"/> value, and
/// <see cref="WorkingDays"/> is non-null only when it is <c>resolved</c>.
/// <para><c>calendar_forbidden</c> is kept apart from <c>calendar_unresolved</c> on purpose: "the calendar does not
/// exist" and "you are not allowed to read the calendar" have completely different fixes, and collapsing them would
/// send an operator hunting for a missing calendar that is actually there (F-RBAC-WC).</para>
/// </summary>
public sealed record WorkingDayCountResult(
    string Resolution,
    int? WorkingDays,
    IReadOnlyList<string> ReasonCodes,
    string Reason);
