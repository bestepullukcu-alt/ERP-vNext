using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;

namespace Diten.CrmService.Application.Features.Territory.Readiness;

public static class TerritoryReadinessStatus
{
    public const string Ready = "ready";
    public const string NotReady = "not_ready";
    public const string Unknown = "unknown";
}

public static class TerritoryReadinessReasonCodes
{
    public const string ReadinessOk = "readiness_ok";
    public const string CoverageNotCurrent = "coverage_not_current";
    public const string AccountInactive = "account_inactive";
    public const string AccountMissingLocation = "account_missing_location";
    public const string ContactNotLinkedToAccount = "contact_not_linked_to_account";
    public const string ContactInactive = "contact_inactive";
    public const string ContactAvailabilityUnknown = "contact_availability_unknown";
    public const string ContactNotAvailableOnDay = "contact_not_available_on_day";
    public const string OutsidePreferredWindow = "outside_preferred_window";
    public const string AppointmentRequired = "appointment_required";
    public const string FrequencyUnknown = "frequency_unknown";
    // MOD-0151 FU09B — a frequency policy WAS resolved by the MOD-0165 provider (informational; never blocks readiness).
    public const string FrequencyResolved = "frequency_resolved";
    // MOD-0151 FU09B — the provider resolved to a same-band tie (deterministic pick, but surfaced so it is visible).
    public const string FrequencyConflict = "frequency_conflict";
    public const string FrequencyNotDue = "frequency_not_due";
    public const string FrequencyOverdue = "frequency_overdue";
    public const string NoLastVisit = "no_last_visit";
    public const string ResourceNotCurrentOwner = "resource_not_current_owner";
    public const string BusinessScopeMismatch = "business_scope_mismatch";
}

/// <summary>
/// FU09A read projection only. It is never persisted or cached and deliberately contains no route, ordering,
/// distance, travel-time, score, visit-plan, GPS or patient fields.
/// </summary>
public sealed record TerritoryRouteCandidateReadModel(
    Guid AccountId,
    string AccountName,
    string AccountStatus,
    string AccountLocationReadiness,
    double? Latitude,
    double? Longitude,
    string? AddressSummary,
    Guid? TerritoryModelId,
    string? TerritoryModelName,
    Guid? TerritoryNodeId,
    string? TerritoryNodeCode,
    string? TerritoryNodeName,
    string? BusinessUnit,
    string? ResourceId,
    string? ResourceDisplayName,
    string? PositionCode,
    string? PositionTitle,
    Guid? ContactId,
    string? ContactName,
    Guid? AccountContactLinkId,
    string AvailabilityStatus,
    string? AvailableWindow,
    string? PreferredVisitWindow,
    string? AvoidWindow,
    bool AppointmentRequired,
    int? AverageVisitDurationMinutes,
    string FrequencyStatus,
    Guid? SelectedFrequencyPolicyId,
    // MOD-0151 FU09B — read-only frequency provider metadata (filled from the MOD-0165 FU03 resolve provider). These
    // describe HOW OFTEN the target should be visited; they never carry a due/overdue verdict, a last-visit date, a
    // route/order/plan field, or a consent decision (those remain out of scope).
    string? SelectedFrequencyPolicyCode,
    string? SelectedFrequencyPolicyName,
    string? FrequencyType,
    int? RequiredVisitCount,
    string? PeriodType,
    string? FrequencySelectionReason,
    IReadOnlyList<string> FrequencyReasonCodes,
    IReadOnlyList<FrequencyCandidatePolicy> FrequencyCandidatePolicies,
    DateOnly? LastVisitDate,
    string DueStatus,
    DateTimeOffset EffectiveAt,
    string ReadinessStatus,
    IReadOnlyList<string> ReasonCodes);

public sealed record TerritoryReadinessResultDto(
    int TotalCount,
    int ReadyCount,
    int NotReadyCount,
    int UnknownCount,
    int ReturnedCount,
    IReadOnlyList<TerritoryRouteCandidateReadModel> Items);
