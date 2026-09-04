namespace Diten.Web.Models.CRM;

/// <summary>MOD-0162-FU04 KnowledgePath create/edit view model (Compact). Optional numeric/date fields are nullable so no
/// spurious data-val-required is generated; required fields carry both the label marker and the HTML required attribute.
/// Steps are NOT bound here — they are managed inline through the step sub-routes on the Edit page (D2).</summary>
public sealed class KnowledgePathEditViewModel
{
    public Guid? PathId { get; set; }

    public string PathCode { get; set; } = string.Empty;
    public string PathName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Objective { get; set; } = string.Empty;

    public Guid? SubjectId { get; set; }
    public Guid? TopicId { get; set; }
    public Guid? AudienceProfileId { get; set; }
    public string? LanguageCode { get; set; }

    public string PathVersion { get; set; } = string.Empty;
    public string PathStatus { get; set; } = "draft";
    public string Source { get; set; } = "manual";
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public bool IsArchived { get; set; }
    public bool IsStepSetFrozen { get; set; }

    // Contract-driven option lists (never hardcoded).
    public string? ContractError { get; set; }
    public IReadOnlyList<string> PathStatuses { get; set; } = new List<string>();
    public IReadOnlyList<string> Sources { get; set; } = new List<string>();
    public IReadOnlyList<string> StepTypes { get; set; } = new List<string>();
    public IReadOnlyList<string> CompletionRules { get; set; } = new List<string>();
    public IReadOnlyList<string> VersionPinPolicies { get; set; } = new List<string>();
    public IReadOnlyList<string> LanguageOptions { get; set; } = new List<string> { "en", "tr", "fr", "es", "de", "ru", "zh", "ar" };

    public List<KnowledgePathOptionViewModel> SubjectOptions { get; set; } = new();
    public List<KnowledgePathOptionViewModel> TopicOptions { get; set; } = new();
    public List<KnowledgePathOptionViewModel> AudienceProfileOptions { get; set; } = new();
}

/// <summary>A reference-picker option. <c>Group</c> holds a parent id so the UI can cascade; <c>IsInactive</c> flags an
/// archived value that is kept visible so it survives the round-trip.</summary>
public sealed class KnowledgePathOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public bool IsInactive { get; set; }
}

/// <summary>Details page view model — the resolved path (with embedded steps) + capability flags.</summary>
public sealed class KnowledgePathPageViewModel
{
    public KnowledgePathDetailViewModel Path { get; set; } = new();
    public bool CanManage { get; set; }
    public bool CanPublish { get; set; }
}

/// <summary>Read model bound from the gateway path detail response.</summary>
public sealed class KnowledgePathDetailViewModel
{
    public Guid PathId { get; set; }
    public string PathCode { get; set; } = string.Empty;
    public string PathName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid SubjectId { get; set; }
    public Guid? TopicId { get; set; }
    public Guid? AudienceProfileId { get; set; }
    // Resolved display labels for the classification ids (fail-soft; null when unresolved → view falls back to the id).
    public string? SubjectName { get; set; }
    public string? TopicName { get; set; }
    public string? AudienceProfileName { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public string PathVersion { get; set; } = string.Empty;
    public string PathStatus { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string Source { get; set; } = string.Empty;
    public List<KnowledgePathStepViewModel> Steps { get; set; } = new();
    public int ActiveStepCount { get; set; }
    public int RequiredStepCount { get; set; }
    public bool IsMixedLanguage { get; set; }
    public bool IsMixedSubject { get; set; }
    public bool HasUnresolvedStepContent { get; set; }
    public bool IsStepSetFrozen { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public Guid? SupersedesPathId { get; set; }
    public int Version { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class KnowledgePathStepViewModel
{
    public Guid StepId { get; set; }
    public int StepOrder { get; set; }
    public string StepCode { get; set; } = string.Empty;
    public string StepTitle { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public Guid ContentId { get; set; }
    public string ContentCode { get; set; } = string.Empty;
    public string VersionPinPolicy { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string CompletionRule { get; set; } = string.Empty;
    public Guid? PrerequisiteStepId { get; set; }
    public Guid? ConceptNodeId { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public string? Notes { get; set; }
    public List<KnowledgePathBranchConditionViewModel> BranchConditions { get; set; } = new();
    public string StepStatus { get; set; } = string.Empty;
    public Guid? ResolvedContentId { get; set; }
    public string? ResolvedContentVersion { get; set; }
    public string? ResolvedContentTitle { get; set; }
    public string ContentResolutionStatus { get; set; } = string.Empty;
    public bool IsCrossSubjectStep { get; set; }
    public bool IsCrossLanguageStep { get; set; }
    public string? ConceptNodeCode { get; set; }
    public string? ConceptNodeName { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class KnowledgePathBranchConditionViewModel
{
    public string ConditionCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? TargetStepId { get; set; }
}

// ----- gateway envelopes / contract -----

public sealed class KnowledgePathGatewayResponse<T>
{
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public int StatusCode { get; set; }
    public bool IsSuccessful { get; set; }
}

public sealed class KnowledgePathContractViewModel
{
    public bool IsReady { get; set; }
    public KnowledgePathContractFeatures Features { get; set; } = new();
    public KnowledgePathContractVocabularies Vocabularies { get; set; } = new();
}

public sealed class KnowledgePathContractFeatures
{
    public bool SupportsKnowledgePath { get; set; }
    public bool SupportsKnowledgePathStep { get; set; }
}

public sealed class KnowledgePathContractVocabularies
{
    public List<string> PathStatuses { get; set; } = new();
    public List<string> Sources { get; set; } = new();
    public List<string> StepTypes { get; set; } = new();
    public List<string> CompletionRules { get; set; } = new();
    public List<string> VersionPinPolicies { get; set; } = new();
}
