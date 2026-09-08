using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;

/// <summary>SCMM-10 (③, RM2) — one authored step of a branch: the concept type plus per-position cardinality and the
/// moderator / for-whom config references. All references are opaque config strings (D8 — no engine).</summary>
public sealed record ConceptChainStepInput(
    Guid ConceptTypeId,
    int MinSelection = 1,
    int? MaxSelection = null,
    IReadOnlyList<string>? AllowedRoleRefs = null,
    IReadOnlyList<string>? AudienceDimensionRefs = null);

/// <summary>SCMM-10 (③, RM2) — one parallel branch: a stable code plus its ordered steps.</summary>
public sealed record ConceptChainBranchInput(
    string BranchCode,
    IReadOnlyList<ConceptChainStepInput> Steps,
    string? BranchName = null,
    int SortOrder = 0);

/// <summary>MOD-0162 FU03 chain-template write surface. <c>TenantId</c> server-resolved. <c>OrderedConceptTypes</c> is a
/// sequence of at least two same-subject type ids with no repeat (v1) — the backward-compatible spine. SCMM-10 (③) adds
/// an optional <c>Branches</c> structure (parallel branches, cardinality, moderator, for-whom); when omitted the template
/// stays exactly as before. Two published versions of one <c>ChainCode</c> may not overlap in effective window (409).</summary>
public sealed record CreateConceptChainTemplateCommand(
    Guid SubjectId,
    string ChainCode,
    string ChainName,
    IReadOnlyList<Guid> OrderedConceptTypes,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    string? ChainVersion = null,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<ConceptChainBranchInput>? Branches = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>ChainCode</c> and <c>SubjectId</c> are immutable. A published version
/// freezes BOTH <c>OrderedConceptTypes</c> AND <c>Branches</c> — changing either on a published template is rejected
/// (make a new version).</summary>
public sealed record UpdateConceptChainTemplateCommand(
    Guid ConceptChainTemplateId,
    string ChainName,
    IReadOnlyList<Guid> OrderedConceptTypes,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    string? ChainVersion = null,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<ConceptChainBranchInput>? Branches = null) : IRequest<Response<bool>>;

public sealed record ArchiveConceptChainTemplateCommand(Guid ConceptChainTemplateId) : IRequest<Response<bool>>;
