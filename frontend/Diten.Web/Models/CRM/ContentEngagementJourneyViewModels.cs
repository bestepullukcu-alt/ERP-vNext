namespace Diten.Web.Models.CRM;

/// <summary>MOD-0162-FU05 ContentEngagementJourney create/edit view model (Compact). Optional numeric/date fields are
/// nullable so no spurious data-val-required is generated; required fields carry both the label marker and the HTML
/// required attribute. Stages are NOT bound here — they are managed inline through the stage sub-routes on the Edit
/// page (S2). There is no Campaign/Brand/Product/Segment field (§2.1/S6) and no runtime-state field of any kind.</summary>
public sealed class ContentEngagementJourneyEditViewModel
{
    public Guid? JourneyId { get; set; }

    public string JourneyCode { get; set; } = string.Empty;
    public string JourneyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Objective { get; set; } = string.Empty;

    public Guid? SubjectId { get; set; }
    public Guid? TopicId { get; set; }
    public Guid? AudienceProfileId { get; set; }
    public string? LanguageCode { get; set; }

    public string JourneyVersion { get; set; } = string.Empty;
    public string JourneyStatus { get; set; } = "draft";
    public string Source { get; set; } = "manual";
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public bool IsArchived { get; set; }
    public bool IsStageSetFrozen { get; set; }

    // Contract-driven option lists (never hardcoded).
    public string? ContractError { get; set; }
    public IReadOnlyList<string> JourneyStatuses { get; set; } = new List<string>();
    public IReadOnlyList<string> Sources { get; set; } = new List<string>();
    public IReadOnlyList<string> StageTypes { get; set; } = new List<string>();
    public IReadOnlyList<string> AdvancementRules { get; set; } = new List<string>();
    public IReadOnlyList<string> PathVersionPinPolicies { get; set; } = new List<string>();
    public int MaxStagesPerJourney { get; set; }
    public int MaxBranchConditionsPerStage { get; set; }
    public IReadOnlyList<string> LanguageOptions { get; set; } =
        new List<string> { "en", "tr", "fr", "es", "de", "ru", "zh", "ar" };

    public List<ContentEngagementJourneyOptionViewModel> SubjectOptions { get; set; } = new();
    public List<ContentEngagementJourneyOptionViewModel> TopicOptions { get; set; } = new();
    public List<ContentEngagementJourneyOptionViewModel> AudienceProfileOptions { get; set; } = new();
}

/// <summary>A reference-picker option. <c>Group</c> holds a parent id so the UI can cascade; <c>IsInactive</c> flags an
/// archived value that is kept visible so it survives the round-trip.</summary>
public sealed class ContentEngagementJourneyOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public bool IsInactive { get; set; }
}

/// <summary>Details page view model — the resolved journey (with embedded stages) + capability flags.</summary>
public sealed class ContentEngagementJourneyPageViewModel
{
    public ContentEngagementJourneyDetailViewModel Journey { get; set; } = new();
    public bool CanManage { get; set; }
    public bool CanPublish { get; set; }
}

/// <summary>Read model bound from the gateway journey detail response.</summary>
public sealed class ContentEngagementJourneyDetailViewModel
{
    public Guid JourneyId { get; set; }
    public string JourneyCode { get; set; } = string.Empty;
    public string JourneyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? TopicId { get; set; }
    public Guid? AudienceProfileId { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public string JourneyVersion { get; set; } = string.Empty;
    public string JourneyStatus { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string Source { get; set; } = string.Empty;
    public List<ContentEngagementJourneyStageViewModel> Stages { get; set; } = new();
    public int ActiveStageCount { get; set; }
    public int RequiredStageCount { get; set; }
    public int RepeatableStageCount { get; set; }
    public bool IsMixedLanguage { get; set; }
    public bool IsMixedSubject { get; set; }
    public bool HasUnresolvedStagePath { get; set; }
    public bool HasRepeatedPaths { get; set; }
    public bool IsStageSetFrozen { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public Guid? SupersedesJourneyId { get; set; }
    public int Version { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class ContentEngagementJourneyStageViewModel
{
    public Guid StageId { get; set; }
    public int StageOrder { get; set; }
    public string StageCode { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public string StageObjective { get; set; } = string.Empty;
    public string? StageType { get; set; }
    public Guid RecommendedKnowledgePathId { get; set; }
    public string PathCode { get; set; } = string.Empty;
    public string PathVersionPinPolicy { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool Repeatable { get; set; }
    public int? MinVisitNumber { get; set; }
    public int? MaxVisitNumber { get; set; }
    public string? AdvancementRule { get; set; }
    public Guid? FallbackStageId { get; set; }
    public string? Notes { get; set; }
    public List<ContentEngagementJourneyBranchConditionViewModel> BranchConditions { get; set; } = new();
    public string StageStatus { get; set; } = string.Empty;
    public Guid? ResolvedKnowledgePathId { get; set; }
    public string? ResolvedPathVersion { get; set; }
    public string? ResolvedPathName { get; set; }
    public int? ResolvedPathStepCount { get; set; }
    public string PathResolutionStatus { get; set; } = string.Empty;
    public bool IsCrossSubjectStage { get; set; }
    public bool IsCrossLanguageStage { get; set; }
    public int PathUsageCountInJourney { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class ContentEngagementJourneyBranchConditionViewModel
{
    public string ConditionCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? TargetStageId { get; set; }
}

// ----- gateway envelopes / contract -----

public sealed class ContentEngagementJourneyGatewayResponse<T>
{
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public int StatusCode { get; set; }
    public bool IsSuccessful { get; set; }
}

public sealed class ContentEngagementJourneyContractViewModel
{
    public bool IsReady { get; set; }
    public ContentEngagementJourneyContractFeatures Features { get; set; } = new();
    public ContentEngagementJourneyContractVocabularies Vocabularies { get; set; } = new();
    public ContentEngagementJourneyContractLimitsViewModel Limits { get; set; } = new();
}

public sealed class ContentEngagementJourneyContractFeatures
{
    public bool SupportsContentEngagementJourney { get; set; }
    public bool SupportsContentEngagementJourneyStage { get; set; }
}

public sealed class ContentEngagementJourneyContractVocabularies
{
    public List<string> JourneyStatuses { get; set; } = new();
    public List<string> Sources { get; set; } = new();
    public List<string> StageTypes { get; set; } = new();
    public List<string> AdvancementRules { get; set; } = new();
    public List<string> PathVersionPinPolicies { get; set; } = new();
}

public sealed class ContentEngagementJourneyContractLimitsViewModel
{
    public int MaxStagesPerJourney { get; set; }
    public int MaxBranchConditionsPerStage { get; set; }
}
