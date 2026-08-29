using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Relationship;

/// <summary>Lists relationships for the tenant. Archived rows included by default. <c>Conformance</c> filters to
/// conforming (true) / non-conforming (false) edges when supplied.</summary>
public sealed record ListConceptRelationshipsQuery(
    Guid? SubjectId = null,
    Guid? FromNodeId = null,
    Guid? ToNodeId = null,
    string? RelationshipType = null,
    bool? Conformance = null,
    string? Status = null,
    bool IncludeArchived = true) : IRequest<Response<ConceptRelationshipListDto>>;

public sealed record GetConceptRelationshipQuery(Guid ConceptRelationshipId) : IRequest<Response<ConceptRelationshipDto>>;
