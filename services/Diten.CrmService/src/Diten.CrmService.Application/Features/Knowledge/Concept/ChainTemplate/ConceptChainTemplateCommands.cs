using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;

/// <summary>SCMM-10 (③, RM2) — one authored step of a branch: the concept type plus per-position cardinality. Structure
/// only (D8 — no engine). Moderator / for-whom now live at the template level (WP-A, D-a/D-e).</summary>
public sealed record ConceptChainStepInput(
    Guid ConceptTypeId,
    int MinSelection = 1,
    int? MaxSelection = null);

/// <summary>SCMM-10 (③, RM2) — one parallel branch: a stable code plus its ordered steps.</summary>
public sealed record ConceptChainBranchInput(
    string BranchCode,
    IReadOnlyList<ConceptChainStepInput> Steps,
    string? BranchName = null,
    int SortOrder = 0);

/// <summary>MOD-0162 FU03 chain-template write surface. <c>TenantId</c> server-resolved. <c>OrderedConceptTypes</c> is a
/// sequence of at least two same-subject type ids with no repeat (v1) — the backward-compatible spine. SCMM-10 (③) adds
/// an optional <c>Branches</c> structure (parallel branches, cardinality); when omitted the template stays exactly as
/// before. SCMM-10 (WP-A) adds template-level <c>ModeratorRoleType</c> (who presents the chain) and
/// <c>ForWhomAudienceProfileIds</c> (the chain's target audience). Two published versions of one <c>ChainCode</c> may not
/// overlap in effective window (409).</summary>
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
    IReadOnlyList<ConceptChainBranchInput>? Branches = null,
    string? ModeratorRoleType = null,
    IReadOnlyList<Guid>? ForWhomAudienceProfileIds = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>ChainCode</c> and <c>SubjectId</c> are immutable. A published version
/// freezes <c>OrderedConceptTypes</c>, <c>Branches</c> AND the template-level <c>ModeratorRoleType</c> /
/// <c>ForWhomAudienceProfileIds</c> (D-f) — changing any of them on a published template is rejected (make a new
/// version).</summary>
public sealed record UpdateConceptChainTemplateCommand(
    Guid ConceptChainTemplateId,
    string ChainName,
    IReadOnlyList<Guid> OrderedConceptTypes,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    string? ChainVersion = null,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<ConceptChainBranchInput>? Branches = null,
    string? ModeratorRoleType = null,
    IReadOnlyList<Guid>? ForWhomAudienceProfileIds = null) : IRequest<Response<bool>>;

public sealed record ArchiveConceptChainTemplateCommand(Guid ConceptChainTemplateId) : IRequest<Response<bool>>;
