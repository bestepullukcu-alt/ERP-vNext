namespace Diten.CrmService.Application.Features.Knowledge.Path;

/// <summary>MOD-0162 FU04 read model for a KnowledgePath. TenantId is never echoed (server-resolved). Steps are embedded
/// and returned with resolved content (never silently); the derived counters/flags are computed, never persisted.</summary>
public sealed record KnowledgePathDto(
    Guid PathId,
    string PathCode,
    string PathName,
    string? Description,
    Guid SubjectId,
    Guid? TopicId,
    Guid? AudienceProfileId,
    string Objective,
    string? LanguageCode,
    string PathVersion,
    string PathStatus,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    IReadOnlyList<KnowledgePathStepDto> Steps,
    int ActiveStepCount,
    int RequiredStepCount,
    bool IsMixedLanguage,
    bool IsMixedSubject,
    bool HasUnresolvedStepContent,
    bool IsStepSetFrozen,
    DateTimeOffset? StepSetFrozenAt,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    Guid? SupersedesPathId,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

/// <summary>MOD-0162 FU04 read model for an embedded step. Content is resolved per <c>VersionPinPolicy</c> and the
/// resolution status is always visible (pinned / resolved-latest / unresolved) — no silent version drift or silent
/// drop. Concept node fields are label reads only (the node is never mutated).</summary>
public sealed record KnowledgePathStepDto(
    Guid StepId,
    int StepOrder,
    string StepCode,
    string StepTitle,
    string StepType,
    Guid ContentId,
    string ContentCode,
    string VersionPinPolicy,
    bool IsRequired,
    string CompletionRule,
    Guid? PrerequisiteStepId,
    Guid? ConceptNodeId,
    int? EstimatedDurationMinutes,
    string? Notes,
    IReadOnlyList<KnowledgePathBranchConditionDto> BranchConditions,
    string StepStatus,
    Guid? ResolvedContentId,
    string? ResolvedContentVersion,
    string? ResolvedContentTitle,
    string ContentResolutionStatus,
    bool IsCrossSubjectStep,
    bool IsCrossLanguageStep,
    string? ConceptNodeCode,
    string? ConceptNodeName,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    bool IsArchived);

public sealed record KnowledgePathBranchConditionDto(
    string ConditionCode,
    string? Description,
    Guid? TargetStepId);

/// <summary>List projection — no embedded steps (large-document guard); only the active/required counters.</summary>
public sealed record KnowledgePathListItemDto(
    Guid PathId,
    string PathCode,
    string PathName,
    Guid SubjectId,
    Guid? TopicId,
    Guid? AudienceProfileId,
    string? LanguageCode,
    string PathVersion,
    string PathStatus,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    int ActiveStepCount,
    int RequiredStepCount,
    bool HasUnresolvedStepContent,
    bool IsStepSetFrozen,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    bool IsArchived);

public sealed record KnowledgePathListDto(IReadOnlyList<KnowledgePathListItemDto> Items, int Total);

public sealed record KnowledgePathStepListDto(IReadOnlyList<KnowledgePathStepDto> Items, int Total);
