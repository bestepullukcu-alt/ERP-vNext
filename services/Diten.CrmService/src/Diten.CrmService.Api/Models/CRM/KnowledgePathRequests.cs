namespace Diten.CrmService.Api.Models.CRM;

// MOD-0162 FU04 request models. TenantId is NEVER part of any request body — it is server-resolved from the JWT claim.
// Route ids (pathId / stepId) come from the path, never the body. Steps are managed only through the step sub-routes;
// a `steps` array on the path update is rejected (V-P16) — the property exists only so the handler can detect and 400 it.

public sealed record CreateKnowledgePathRequest(
    string PathCode,
    string PathName,
    Guid SubjectId,
    string Objective,
    string PathVersion,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? LanguageCode = null,
    string? PathStatus = null,
    DateTimeOffset? EffectiveTo = null,
    string? Source = null,
    // WP-KP-1 — chain + country + language together; SubjectId then comes from the chain and PathCode may be empty.
    Guid? ChainTemplateId = null,
    string? CountryCode = null);

public sealed record UpdateKnowledgePathRequest(
    string PathName,
    Guid SubjectId,
    string Objective,
    string PathVersion,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? LanguageCode = null,
    string? PathStatus = null,
    DateTimeOffset? EffectiveTo = null,
    string? Source = null,
    int? ExpectedVersion = null,
    object? Steps = null,
    string? CountryCode = null);

public sealed record CreateKnowledgePathVersionRequest(string? NewPathVersion = null);

// WP-KP-1 — studio sub-routes.
public sealed record BindKnowledgePathChainRequest(
    Guid ChainTemplateId, string? CountryCode, string? LanguageCode, int? ExpectedVersion = null);

public sealed record KnowledgePathArrangementRequest(Guid ChainStepId, string? BranchCode, int Position = 0);

public sealed record AddKnowledgePathClaimRequest(
    Guid ClaimId, KnowledgePathArrangementRequest? Arrangement, int? ExpectedVersion = null);

public sealed record ArrangeKnowledgePathClaimRequest(int Position, int? ExpectedVersion = null);

// WP-KP-2 — review sub-routes.
public sealed record KnowledgePathDecisionRequest(string? Decision, string? Comment = null);

// WP-KP-3 — withdrawal of a released revision (reason required).
public sealed record KnowledgePathWithdrawRequest(string? Reason);

public sealed record KnowledgePathNoteRequest(
    string? Text, string? PageRef = null, string? BlockRef = null, string? StepRef = null, double? X = null, double? Y = null);

public sealed record KnowledgePathBranchConditionRequest(
    string ConditionCode,
    string? Description = null,
    Guid? TargetStepId = null);

public sealed record AddKnowledgePathStepRequest(
    int StepOrder,
    string StepCode,
    string StepTitle,
    string StepType,
    Guid ContentId,
    bool IsRequired,
    string? VersionPinPolicy = null,
    string? CompletionRule = null,
    Guid? PrerequisiteStepId = null,
    Guid? ConceptNodeId = null,
    int? EstimatedDurationMinutes = null,
    string? Notes = null,
    IReadOnlyList<KnowledgePathBranchConditionRequest>? BranchConditions = null,
    int? ExpectedVersion = null,
    KnowledgePathArrangementRequest? Arrangement = null);

public sealed record UpdateKnowledgePathStepRequest(
    int StepOrder,
    string StepCode,
    string StepTitle,
    string StepType,
    Guid ContentId,
    bool IsRequired,
    string? VersionPinPolicy = null,
    string? CompletionRule = null,
    Guid? PrerequisiteStepId = null,
    Guid? ConceptNodeId = null,
    int? EstimatedDurationMinutes = null,
    string? Notes = null,
    IReadOnlyList<KnowledgePathBranchConditionRequest>? BranchConditions = null,
    int? ExpectedVersion = null,
    KnowledgePathArrangementRequest? Arrangement = null);
