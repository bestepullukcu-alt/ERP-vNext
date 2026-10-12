using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Commands;

// MOD-0162 FU04 KnowledgePath write surface. TenantId is server-resolved (never in a payload). No delete/PATCH — closing
// is archive. Steps are managed only through the step sub-commands (never through a `steps` array on Update — V-P16).
// D2: the step commands mutate the SAME path document and share the path's optimistic Version token.

public sealed record CreateKnowledgePathCommand(
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
    // WP-SB-2: set only by the Content Studio release (server-side provenance; never from an API payload).
    Diten.CrmService.Domain.Entities.KnowledgeStudioOrigin? StudioOrigin = null,
    // WP-KP-1: a chain-bound path is created with chain + country + language together (any one given ⇒ all three
    // required). SubjectId is then taken from the chain and PathCode may be omitted (server issues KP-{yyyy}-{hex}).
    Guid? ChainTemplateId = null,
    string? CountryCode = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the path's mutable fields. <c>PathCode</c> is immutable. A <c>steps</c> array is rejected
/// (V-P16). A published version is frozen: only EffectiveTo and PathStatus (inactive/archived) may change (V-P13);
/// switching to <c>published</c> via Update is rejected — publish is a separate endpoint (V-P12, D4).</summary>
public sealed record UpdateKnowledgePathCommand(
    Guid PathId,
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
    bool StepsProvided = false,
    int? ExpectedVersion = null,
    // WP-KP-1: on a chain-bound path country / language are the identity (a different value ⇒ 409 path_identity_locked;
    // null keeps it). A legacy path takes a country only through bind-chain.
    string? CountryCode = null) : IRequest<Response<bool>>;

/// <summary>D4 — publish is its own endpoint and its own permission (SoD: author ≠ publisher). Freezes the step set.</summary>
public sealed record PublishKnowledgePathCommand(Guid PathId, int? ExpectedVersion = null) : IRequest<Response<bool>>;

/// <summary>D5 — clones a published version into a new draft: PathVersion bumped, steps copied with NEW StepIds,
/// SupersedesPathId set, no auto-publish, source version unchanged.</summary>
public sealed record CreateKnowledgePathVersionCommand(
    Guid PathId, string? NewPathVersion = null,
    Diten.CrmService.Domain.Entities.KnowledgeStudioOrigin? StudioOrigin = null) : IRequest<Response<Guid>>;

public sealed record ArchiveKnowledgePathCommand(Guid PathId, int? ExpectedVersion = null) : IRequest<Response<bool>>;

/// <summary>WP-KP-1 — binds a legacy (chain-less) DRAFT path to a chain + country + language, once (the back end of the
/// legacy-path wizard). The path subject must be the chain's subject (409 chain_subject_mismatch).</summary>
public sealed record BindKnowledgePathChainCommand(
    Guid PathId, Guid ChainTemplateId, string? CountryCode, string? LanguageCode, int? ExpectedVersion = null)
    : IRequest<Response<bool>>;

// ---------------- WP-KP-1 claims on a chain-bound path (mutate the path document, share the path Version token) -------

public sealed record AddKnowledgePathClaimCommand(
    Guid PathId, Guid ClaimId, KnowledgePathArrangementInput? Arrangement, int? ExpectedVersion = null)
    : IRequest<Response<bool>>;

/// <summary>Changes only the Position inside the claim's slot (the slot itself never moves — D-KP-7).</summary>
public sealed record ArrangeKnowledgePathClaimCommand(
    Guid PathId, Guid ClaimId, int Position, int? ExpectedVersion = null) : IRequest<Response<bool>>;

public sealed record RemoveKnowledgePathClaimCommand(
    Guid PathId, Guid ClaimId, int? ExpectedVersion = null) : IRequest<Response<bool>>;

// ---------------- embedded step sub-commands (mutate the path document, share the path Version token) ----------------

public sealed record AddKnowledgePathStepCommand(
    Guid PathId,
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
    IReadOnlyList<KnowledgePathBranchConditionInput>? BranchConditions = null,
    int? ExpectedVersion = null,
    // WP-KP-1: required on a chain-bound path (the client StepOrder is then ignored — the server computes it).
    KnowledgePathArrangementInput? Arrangement = null) : IRequest<Response<Guid>>;

public sealed record UpdateKnowledgePathStepCommand(
    Guid PathId,
    Guid StepId,
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
    IReadOnlyList<KnowledgePathBranchConditionInput>? BranchConditions = null,
    int? ExpectedVersion = null,
    // WP-KP-1: required on a chain-bound path; only Position may change once placed.
    KnowledgePathArrangementInput? Arrangement = null) : IRequest<Response<bool>>;

public sealed record ArchiveKnowledgePathStepCommand(
    Guid PathId, Guid StepId, int? ExpectedVersion = null) : IRequest<Response<bool>>;
