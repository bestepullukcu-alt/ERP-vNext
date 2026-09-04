namespace Diten.CrmService.Application.Features.PlannedVisit;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0155 FU01 — every DTO / read model of the PlannedVisit feature, in ONE file (the single documented exception to
// the one-public-type-per-file convention). TenantId appears in NO payload: it is server-resolved from the claim.
// PlannedDate is surfaced as an ISO "yyyy-MM-dd" STRING so a JSON client never has to guess a DateOnly's wire shape.
// The five provenance/snapshot blocks are DERIVED — they are read out, never authored back in.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>One row of the plan grid.</summary>
public sealed record PlannedVisitListItemDto(
    Guid PlannedVisitId,
    string VisitCode,
    string TargetType,
    Guid TargetId,
    Guid? AccountId,
    Guid? ContactId,
    Guid? AccountContactLinkId,
    string PlannedDate,
    string? PlannedStartTime,
    string? PlannedEndTime,
    int? PlannedDurationMinutes,
    string ResourceId,
    string ResourceType,
    string? ResourceDisplayName,
    string VisitPurpose,
    string VisitType,
    string? BusinessUnit,
    Guid? TerritoryNodeId,
    Guid? CampaignId,
    string PlanStatus,
    string Source,
    string? ConsentStatus,
    string? FrequencyStatus,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record PlannedVisitListDto(IReadOnlyList<PlannedVisitListItemDto> Items, int TotalCount);

/// <summary>Plan detail, including every read-only provenance/snapshot block.</summary>
public sealed record PlannedVisitDetailDto(
    Guid PlannedVisitId,
    string VisitCode,
    string TargetType,
    Guid TargetId,
    Guid? AccountId,
    Guid? ContactId,
    Guid? AccountContactLinkId,
    string PlannedDate,
    string? PlannedStartTime,
    string? PlannedEndTime,
    int? PlannedDurationMinutes,
    PlannedVisitResourceRefDto Resource,
    string? PositionCode,
    Guid? PositionId,
    string VisitPurpose,
    string VisitType,
    string? Objective,
    string? Notes,
    string? BusinessUnit,
    Guid? TerritoryNodeId,
    Guid? TerritoryModelId,
    Guid? CampaignId,
    Guid? ContentEngagementJourneyId,
    Guid? ContentEngagementJourneyStageId,
    string PlanStatus,
    string Source,
    string? CancellationReason,
    bool IsDraft,
    bool IsPlanned,
    bool IsConfirmed,
    bool IsCancelled,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    PlannedVisitScheduleSlotDto Slot,
    PlannedVisitFrequencyProvenanceDto? Frequency,
    PlannedVisitConsentProvenanceDto? Consent,
    PlannedVisitContentRefDto? Content,
    PlannedVisitSelectionProvenanceDto? Selection,
    PlannedVisitAvailabilitySnapshotDto? Availability,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record PlannedVisitResourceRefDto(string ResourceId, string ResourceType, string? DisplayName);

public sealed record PlannedVisitScheduleSlotDto(int? SequenceOrder, string? SlotStartTime, string? SlotEndTime, bool IsPacked);

public sealed record PlannedVisitFrequencyProvenanceDto(
    string FrequencyStatus,
    Guid? SelectedFrequencyPolicyId,
    string? SelectedPolicyCode,
    string? SelectedPolicyName,
    string? FrequencyType,
    int? RequiredVisitCount,
    string? PeriodType,
    string? SelectionReason,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset ResolvedAt);

public sealed record PlannedVisitConsentProvenanceDto(
    bool FilterApplied,
    string EligibilityStatus,
    string Decision,
    string Channel,
    string Purpose,
    Guid? MatchedConsentId,
    IReadOnlyList<Guid> MatchedPreferenceIds,
    IReadOnlyList<string> ReasonCodes,
    string SelectionReason,
    string EvaluatorVersion,
    DateTimeOffset EvaluatedAt);

public sealed record PlannedVisitContentRefDto(
    Guid? JourneyId,
    Guid? StageId,
    int? StageIndex,
    string? StageCode,
    string ContentSource,
    bool IsOverridden,
    Guid? StrategyTemplateId,
    string? JourneyDisplayName,
    string? StageDisplayName,
    DateTimeOffset ResolvedAt);

public sealed record PlannedVisitSelectionProvenanceDto(
    Guid? SegmentId,
    Guid? CampaignId,
    Guid? StrategyTemplateId,
    string SelectionMode,
    DateTimeOffset DecidedAt,
    string? DecidedBy);

public sealed record PlannedVisitAvailabilitySnapshotDto(
    string? Weekday,
    string? AvailableStartTime,
    string? AvailableEndTime,
    bool? AppointmentRequired,
    bool? WithinAvailableWindow,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset CapturedAt);
