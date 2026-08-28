using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Relationship;

/// <summary>MOD-0162 FU03 relationship write surface. <c>TenantId</c> server-resolved. A self-loop, a cross-subject
/// edge and a cycle among active edges are rejected; a duplicate active (From, To, RelationshipType) is a 409. A
/// non-conforming (fromType → toType) pair is NOT rejected — it is stored with <c>IsTemplateConforming = false</c>.</summary>
public sealed record CreateConceptRelationshipCommand(
    Guid SubjectId,
    Guid FromConceptNodeId,
    Guid ToConceptNodeId,
    string RelationshipType,
    string RelationshipCode,
    string RelationshipName,
    DateTimeOffset EffectiveFrom,
    string? Direction = null,
    int Priority = 0,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>From</c> / <c>To</c> / <c>RelationshipType</c> / <c>RelationshipCode</c>
/// are immutable (they are the edge identity). Activating an edge re-runs the cycle and duplicate guards.</summary>
public sealed record UpdateConceptRelationshipCommand(
    Guid ConceptRelationshipId,
    string RelationshipName,
    DateTimeOffset EffectiveFrom,
    string? Direction = null,
    int Priority = 0,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null) : IRequest<Response<bool>>;

public sealed record ArchiveConceptRelationshipCommand(Guid ConceptRelationshipId) : IRequest<Response<bool>>;
