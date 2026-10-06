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
    bool IsArchived,
    // WP-KP-1 — the studio model (DESIGN-KP-STUDIO §2). A legacy path: ChainTemplate / CountryCode / DerivedContext null,
    // IsLegacyUnapproved true, empty Claims / ChainConformance.
    KnowledgePathChainTemplateDto? ChainTemplate = null,
    string? CountryCode = null,
    Diten.CrmService.Application.Features.Knowledge.Chain.ChainDerivedContextDto? DerivedContext = null,
    bool IsLegacyUnapproved = true,
    bool IdentityLocked = false,
    IReadOnlyList<KnowledgePathClaimDto>? Claims = null,
    IReadOnlyList<KnowledgePathChainConformanceDto>? ChainConformance = null);

/// <summary>WP-KP-1 — the pinned chain of a path (code / name read live from the template; null when unreadable).</summary>
public sealed record KnowledgePathChainTemplateDto(Guid Id, string? Code, string? Name, string Version);

/// <summary>WP-KP-1 — a slot address (branch + chain step = concept type id) and the position inside it.</summary>
public sealed record KnowledgePathArrangementDto(Guid ChainStepId, string BranchCode, int Position);

/// <summary>WP-KP-1 — a claim on a path, read against the path's country + language. The country version is the one
/// the coverage cell shows (approved › review-required › in-review › draft); <c>Text</c> / <c>Qualifier</c> are that
/// version's wording in the path language. <c>Reason</c> (not_approved / no_country_version / language_mismatch) is set
/// exactly when <c>Usable</c> is false; adding such a claim is allowed, the release decides (KP-3).</summary>
public sealed record KnowledgePathClaimDto(
    Guid ClaimId,
    string ClaimCode,
    string? Name,
    string? Text,
    string? Qualifier,
    Guid? CountryVersionId,
    string? CountryVersion,
    string? Status,
    bool Usable,
    string? Reason,
    KnowledgePathArrangementDto Arrangement);

/// <summary>WP-KP-1 — how many active steps sit on one chain slot against the slot's min / max.</summary>
public sealed record KnowledgePathChainConformanceDto(
    string BranchCode,
    Guid ChainStepId,
    string? Name,
    int Count,
    int Min,
    int? Max,
    string Status);

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
    bool IsArchived,
    // WP-KP-1 — the chain slot (null on a legacy path / an unplaced step).
    KnowledgePathArrangementDto? Arrangement = null);

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
    bool IsArchived,
    // WP-KP-1 — the studio list filters (KP-UI-1).
    string? CountryCode = null,
    Guid? ChainTemplateId = null,
    string? ChainTemplateCode = null,
    bool IsLegacyUnapproved = true);

public sealed record KnowledgePathListDto(IReadOnlyList<KnowledgePathListItemDto> Items, int Total);

public sealed record KnowledgePathStepListDto(IReadOnlyList<KnowledgePathStepDto> Items, int Total);
