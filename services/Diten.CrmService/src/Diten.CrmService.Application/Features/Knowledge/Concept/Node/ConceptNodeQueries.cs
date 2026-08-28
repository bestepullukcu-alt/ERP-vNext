using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Node;

/// <summary>Lists concept nodes for the tenant. Archived rows included by default. <c>effectiveAt</c> filters to nodes
/// effective at the instant (in-memory; the effective window is a DateTimeOffset BSON array, never a server-side key).</summary>
public sealed record ListConceptNodesQuery(
    Guid? SubjectId = null,
    Guid? ConceptTypeId = null,
    string? Status = null,
    string? ExternalRefType = null,
    DateTimeOffset? EffectiveAt = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<ConceptNodeListDto>>;

public sealed record GetConceptNodeQuery(Guid ConceptNodeId) : IRequest<Response<ConceptNodeDto>>;
