namespace Diten.CrmService.Application.Features.PlannedVisit.Contract;

/// <summary>Machine-readable refusal codes, so a UI and a smoke script can branch without parsing prose.</summary>
public static class PlannedVisitErrorCodes
{
    public const string CodeRequired = "planned_visit_code_required";
    public const string CodeInvalid = "planned_visit_code_invalid";
    public const string CodeTaken = "planned_visit_code_taken";

    public const string UnsupportedVocabularyValue = "unsupported_vocabulary_value";

    public const string TargetRequired = "planned_visit_target_required";
    public const string TargetNotFound = "target_not_found";
    public const string TargetTypeMismatch = "target_type_mismatch";

    public const string ResourceRequired = "planned_visit_resource_required";

    public const string DateRequired = "planned_visit_date_required";
    public const string DateInPast = "planned_visit_date_in_past";
    public const string TimeWindowInvalid = "planned_visit_time_window_invalid";
    public const string DurationInvalid = "planned_visit_duration_invalid";

    public const string JourneyNotPublished = "journey_not_published";
    public const string StageNotInJourney = "stage_not_in_journey";

    public const string TerritoryNodeNotFound = "planned_visit_territory_node_not_found";
    public const string CampaignNotFound = "planned_visit_campaign_not_found";

    public const string InvalidTransition = "planned_visit_invalid_transition";
    public const string Archived = "planned_visit_archived";
    public const string CancellationReasonRequired = "planned_visit_cancellation_reason_required";
    public const string ConcurrencyConflict = "planned_visit_concurrency_conflict";

    // Legacy planning guards (§21/L5-L6)
    public const string Overlap = "planned_visit_overlap";
    public const string DuplicateSameDayType = "planned_visit_duplicate_same_day_type";

    // Consent guard (§12.3)
    public const string BlockedByConsent = "plan_blocked_by_consent";
    public const string ConsentUnknown = "plan_consent_unknown";
    public const string ConsentFilterNotApplied = "consent_filter_not_applied";
    public const string ConsentEvaluationError = "consent_evaluation_error";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CodeRequired, CodeInvalid, CodeTaken, UnsupportedVocabularyValue,
        TargetRequired, TargetNotFound, TargetTypeMismatch, ResourceRequired,
        DateRequired, DateInPast, TimeWindowInvalid, DurationInvalid,
        JourneyNotPublished, StageNotInJourney, TerritoryNodeNotFound, CampaignNotFound,
        InvalidTransition, Archived, CancellationReasonRequired, ConcurrencyConflict,
        Overlap, DuplicateSameDayType,
        BlockedByConsent, ConsentUnknown, ConsentFilterNotApplied, ConsentEvaluationError
    };
}
