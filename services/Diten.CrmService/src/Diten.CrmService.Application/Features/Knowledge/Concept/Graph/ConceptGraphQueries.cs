using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Graph;

/// <summary>Reads the adjacency view for a subject — node list + edge list + template list. NOT an engine: no multi-hop
/// traversal, no best-path, no scoring, no recommendation. Empty when there is no data.</summary>
public sealed record GetConceptGraphQuery(
    Guid SubjectId,
    DateTimeOffset? EffectiveAt = null,
    bool IncludeArchived = false) : IRequest<Response<ConceptGraphDto>>;

/// <summary>1-hop neighbourhood of a node: the node, its directly incident edges and the neighbour nodes. Fixed depth.</summary>
public sealed record GetConceptGraphByNodeQuery(
    Guid NodeId,
    bool IncludeArchived = false) : IRequest<Response<ConceptGraphDto>>;

/// <summary>2 edge-layers from a content: the nodes it links to, and those nodes' 1-hop neighbourhood. Fixed depth —
/// no third layer, no transitive closure.</summary>
public sealed record GetConceptGraphByContentQuery(
    Guid ContentId,
    bool IncludeArchived = false) : IRequest<Response<ConceptGraphDto>>;
