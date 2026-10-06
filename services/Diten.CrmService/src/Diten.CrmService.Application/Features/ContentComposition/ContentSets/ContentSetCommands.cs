using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

// SCMM-14 (CAND-CAP-0011, DESIGN-SCMM-14) content-set write surface — mutable DRAFT authoring only. TenantId is
// server-resolved and never in the payload. Every reference is pinned at selection time (D14-d). No delete — closing a
// set is ArchiveContentSetCommand. No freeze / approve / release here (SCMM-15/16/17).

/// <summary>Create an empty draft from a composition template (+ optional reusable scope). The template's ChainVersion
/// and the scope's ScopeVersion are pinned at creation.</summary>
public sealed record CreateContentSetDraftCommand(
    string SetCode,
    string SetName,
    Guid ConceptChainTemplateId,
    string? Description = null,
    Guid? ContentScopeId = null) : IRequest<Response<Guid>>;

/// <summary>Clone an existing set into a NEW draft (new id, new code, refs + selections remapped, Status=draft). No
/// inherited approval / validation: the clone carries no eligibility snapshot and starts fresh (docx no-inherited-approval).</summary>
public sealed record CloneContentSetToDraftCommand(
    Guid SourceContentSetId,
    string NewSetCode,
    string? NewSetName = null) : IRequest<Response<Guid>>;

/// <summary>Edit the draft's mutable metadata (name / description / status draft|inactive). Refs and selections change
/// through the add / remove / arrange commands. Archived set cannot be updated; update never sets status=archived.</summary>
public sealed record UpdateContentSetCommand(
    Guid ContentSetId,
    string SetName,
    string? Description = null,
    string? Status = null) : IRequest<Response<bool>>;

public sealed record ArchiveContentSetCommand(Guid ContentSetId) : IRequest<Response<bool>>;

/// <summary>Add a content component to a template slot. ContentVersion + LanguageCode are pinned from the component at
/// add time. Returns the new SelectionId. Invalid slot / branch / cardinality → 400/409.</summary>
public sealed record AddContentSetComponentCommand(
    Guid ContentSetId,
    Guid KnowledgeContentId,
    Guid TemplateStepId,
    int Position,
    string? BranchId = null,
    string? Role = null) : IRequest<Response<Guid>>;

public sealed record RemoveContentSetComponentCommand(Guid ContentSetId, Guid SelectionId) : IRequest<Response<bool>>;

/// <summary>Re-arrange an existing component selection into a (possibly different) template slot.</summary>
public sealed record ArrangeContentSetComponentCommand(
    Guid ContentSetId,
    Guid SelectionId,
    Guid TemplateStepId,
    int Position,
    string? BranchId = null) : IRequest<Response<bool>>;

/// <summary>Add a claim to a template slot. ClaimVersion is pinned at add time. Returns the new SelectionId.</summary>
public sealed record AddContentSetClaimCommand(
    Guid ContentSetId,
    Guid ClaimId,
    Guid TemplateStepId,
    int Position,
    string? BranchId = null) : IRequest<Response<Guid>>;

public sealed record RemoveContentSetClaimCommand(Guid ContentSetId, Guid SelectionId) : IRequest<Response<bool>>;

public sealed record ArrangeContentSetClaimCommand(
    Guid ContentSetId,
    Guid SelectionId,
    Guid TemplateStepId,
    int Position,
    string? BranchId = null) : IRequest<Response<bool>>;

/// <summary>Apply eligibility (D14-c): build the context from the pinned scope + selected components, evaluate each
/// selected claim's referenced eligibility policy through the in-process port, and write the per-item snapshot. Non-blocking
/// (never fails the draft on a Blocked/Unresolved outcome); an infrastructure failure of the port PROPAGATES (fail-closed).</summary>
public sealed record ApplyContentSetEligibilityCommand(Guid ContentSetId) : IRequest<Response<bool>>;
