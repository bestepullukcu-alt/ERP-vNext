using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Node;

/// <summary>MOD-0162 FU03 concept-node write surface. <c>TenantId</c> server-resolved. No delete — closing is archive.
/// ExternalRef is a paired provenance field (global-product / document / audience-profile / reference-data-value /
/// other); the master stays the SoR and nothing is copied.</summary>
public sealed record CreateConceptNodeCommand(
    Guid SubjectId,
    Guid ConceptTypeId,
    string ConceptNodeCode,
    string ConceptNodeName,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null,
    string? ExternalRefType = null,
    string? ExternalRefId = null,
    string? MetadataJson = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>ConceptNodeCode</c>, <c>SubjectId</c> and <c>ConceptTypeId</c> are
/// immutable. An archived node cannot be updated.</summary>
public sealed record UpdateConceptNodeCommand(
    Guid ConceptNodeId,
    string ConceptNodeName,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null,
    string? ExternalRefType = null,
    string? ExternalRefId = null,
    string? MetadataJson = null) : IRequest<Response<bool>>;

public sealed record ArchiveConceptNodeCommand(Guid ConceptNodeId) : IRequest<Response<bool>>;
