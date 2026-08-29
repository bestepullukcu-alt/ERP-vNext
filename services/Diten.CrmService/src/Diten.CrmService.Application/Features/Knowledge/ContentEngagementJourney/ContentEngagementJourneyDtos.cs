namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;

/// <summary>MOD-0162 FU05 read model for a ContentEngagementJourney. TenantId is never echoed (server-resolved). Stages
/// are embedded and returned with a resolved path (never silently); the derived counters/flags are computed, never
/// persisted. There is no current-stage, progress or completion member — this is a template, not a run.</summary>
public sealed record ContentEngagementJourneyDto(
    Guid JourneyId,
    string JourneyCode,
    string JourneyName,
    string? Description,
    Guid SubjectId,
    Guid? TopicId,
    Guid? AudienceProfileId,
    string Objective,
    string? LanguageCode,
    string JourneyVersion,
    string JourneyStatus,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    IReadOnlyList<ContentEngagementJourneyStageDto> Stages,
    int ActiveStageCount,
    int RequiredStageCount,
    int RepeatableStageCount,
    bool IsMixedLanguage,
    bool IsMixedSubject,
    bool HasUnresolvedStagePath,
    bool HasRepeatedPaths,
    bool IsStageSetFrozen,
    DateTimeOffset? StageSetFrozenAt,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    Guid? SupersedesJourneyId,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

/// <summary>MOD-0162 FU05 read model for an embedded stage. The KnowledgePath is resolved per
/// <c>PathVersionPinPolicy</c> and the resolution status is always visible (pinned / resolved-latest / unresolved) — no
/// silent version drift or silent drop. The path's steps are NEVER copied: only a step COUNT is surfaced.
/// <c>AdvancementRule</c>, <c>FallbackStageId</c> and <c>BranchConditions</c> are declared metadata, echoed as data and
/// never evaluated.</summary>
public sealed record ContentEngagementJourneyStageDto(
    Guid StageId,
    int StageOrder,
    string StageCode,
    string StageName,
    string StageObjective,
    string? StageType,
    Guid RecommendedKnowledgePathId,
    string PathCode,
    string PathVersionPinPolicy,
    bool IsRequired,
    bool Repeatable,
    int? MinVisitNumber,
    int? MaxVisitNumber,
    string? AdvancementRule,
    Guid? FallbackStageId,
    string? Notes,
    IReadOnlyList<ContentEngagementJourneyBranchConditionDto> BranchConditions,
    string StageStatus,
    Guid? ResolvedKnowledgePathId,
    string? ResolvedPathVersion,
    string? ResolvedPathName,
    int? ResolvedPathStepCount,
    string PathResolutionStatus,
    bool IsCrossSubjectStage,
    bool IsCrossLanguageStage,
    int PathUsageCountInJourney,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    bool IsArchived);

public sealed record ContentEngagementJourneyBranchConditionDto(
    string ConditionCode,
    string? Description,
    Guid? TargetStageId);

/// <summary>List projection — no embedded stages (large-document guard); only the counters and flags.</summary>
public sealed record ContentEngagementJourneyListItemDto(
    Guid JourneyId,
    string JourneyCode,
    string JourneyName,
    Guid SubjectId,
    Guid? TopicId,
    Guid? AudienceProfileId,
    string? LanguageCode,
    string JourneyVersion,
    string JourneyStatus,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    int ActiveStageCount,
    int RequiredStageCount,
    int RepeatableStageCount,
    bool HasUnresolvedStagePath,
    bool HasRepeatedPaths,
    bool IsStageSetFrozen,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    bool IsArchived);

public sealed record ContentEngagementJourneyListDto(
    IReadOnlyList<ContentEngagementJourneyListItemDto> Items, int Total);

public sealed record ContentEngagementJourneyStageListDto(
    IReadOnlyList<ContentEngagementJourneyStageDto> Items, int Total);
