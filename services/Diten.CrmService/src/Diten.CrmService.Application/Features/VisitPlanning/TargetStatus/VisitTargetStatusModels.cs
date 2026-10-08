namespace Diten.CrmService.Application.Features.VisitPlanning.TargetStatus;

// WP-VP-3D — the read models of the target-status reads (B-6, B-9, D5). Kept OUT of VisitPlanningModels.cs on purpose:
// that file is WP-VP-3A's (engine / session), and these reads must not collide with it.

/// <summary>
/// One doctor's state in the rep's period (B-6). Counts are over the REP's own plans only (ownership).
/// <list type="bullet">
/// <item><c>Done</c> — plans dated in the period, not cancelled / archived, whose report outcome is <c>completed</c>.</item>
/// <item><c>Planned</c> — plans dated today..period end, not cancelled / archived, with no report at all.</item>
/// <item><c>Remaining</c> — max(0, required − done − planned); null when the frequency is not resolved.</item>
/// <item><c>LastVisitDate</c> — the latest <c>completed</c> report's <c>ExecutedAt</c> (any date); null ⇒ never.</item>
/// <item><c>DueThisWeek</c> — see <see cref="ContactPeriodStatusReader.IsDueThisWeek"/>.</item>
/// <item><c>SegmentBadges</c> — the doctor's ACTIVE segment names (information only, K-4), at most
/// <see cref="ContactPeriodStatusReader.MaxSegmentBadges"/>.</item>
/// <item><c>ConsentStatus</c> — the MOD-0164 visit-channel eligibility (<c>allowed / blocked / unknown / not_applicable</c>).</item>
/// <item><c>Inactive</c> — the contact master's status is not <c>active</c> (the WP-VP-2 target-name rule).</item>
/// </list>
/// </summary>
public sealed record ContactPeriodStatusDto(
    Guid ContactId,
    int? RequiredVisitCount,
    string FrequencyStatus,
    string? PeriodType,
    int Done,
    int Planned,
    int? Remaining,
    DateTimeOffset? LastVisitDate,
    bool NeverVisited,
    bool DueThisWeek,
    IReadOnlyList<string> SegmentBadges,
    string? ConsentStatus,
    bool Inactive)
{
    /// <summary>WP-VP-4L (1) — <c>weekly</c> when the frequency is unknown: the period counts one visit per working week
    /// (<see cref="FrequencyDefaults"/>); null otherwise.</summary>
    public string? FrequencyDefault { get; init; }

    /// <summary>WP-VP-4L (2) — the doctor has an extra visit (the rep's addition) in the asked week (null = no week asked).</summary>
    public bool? ExtraThisWeek { get; init; }

    /// <summary>WP-VP-4L (2) — visits beyond the period's requirement (done + planned − required, never below 0): an extra
    /// visit counts, <see cref="Remaining"/> never goes under 0.</summary>
    public int OverFrequency { get; init; }
}

/// <summary>The period the statuses were counted in (null fields ⇒ no period could be resolved: counts are 0, remaining
/// and dueThisWeek are not computed, lastVisitDate still is).</summary>
public sealed record TargetStatusPeriodDto(Guid? CyclePeriodId, string? CycleCode, string? StartDate, string? EndDate, int? WeekCount);

/// <summary><c>GET api/crm/visit-plan/my-accounts/{accountId}/doctors</c> — an institution's active doctors with their
/// period status. <c>OutOfTerritory</c> = the account is not in the rep's current coverage (shown, never hidden — K-5).</summary>
public sealed record AccountDoctorsDto(
    Guid AccountId,
    string? AccountName,
    bool OutOfTerritory,
    TargetStatusPeriodDto Period,
    IReadOnlyList<AccountDoctorItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AccountDoctorItemDto(
    Guid ContactId,
    Guid AccountContactLinkId,
    string DisplayName,
    string? Specialty,
    string? ProfessionalTitle,
    bool IsPrimary,
    ContactPeriodStatusDto Status);

/// <summary><c>GET api/crm/visit-plan/sessions/{id}/targets</c> — everything the plan selected, named and placed, in ONE
/// response (D5): institutions, pharmacies and doctors (doctors with their period status).</summary>
public sealed record SessionTargetsDto(
    Guid PlanningSessionId,
    string ResourceId,
    TargetStatusPeriodDto Period,
    IReadOnlyList<SessionTargetAccountDto> Accounts,
    IReadOnlyList<SessionTargetAccountDto> Pharmacies,
    IReadOnlyList<SessionTargetDoctorDto> Doctors);

/// <summary>A selected institution / pharmacy. <c>Found = false</c> ⇒ the master is gone (soft-deleted / other tenant):
/// the id is kept, nothing is invented.</summary>
public sealed record SessionTargetAccountDto(
    Guid AccountId,
    bool Found,
    string? AccountName,
    string? AccountCode,
    string? AccountType,
    string? CityRef,
    string? DistrictRef,
    string? AddressLine,
    double? Latitude,
    double? Longitude,
    bool Inactive);

public sealed record SessionTargetDoctorDto(
    Guid ContactId,
    Guid? AccountId,
    Guid? AccountContactLinkId,
    bool Found,
    string? DisplayName,
    string? Specialty,
    ContactPeriodStatusDto Status,
    // WP-VP-4A (E4-3C-B1, additive) — the rep's stored product pick for this doctor; empty when none.
    IReadOnlyList<PlanningSessionProductDto>? Products = null);

public static class TargetStatusQuickFilters
{
    public const string All = "all";
    public const string Due = "due";
    public const string Never = "never";

    public const string InvalidQuickCode = "invalid_quick";

    public static bool TryNormalize(string? raw, out string value)
    {
        value = string.IsNullOrWhiteSpace(raw) ? All : raw.Trim().ToLowerInvariant();
        return value is All or Due or Never;
    }
}
