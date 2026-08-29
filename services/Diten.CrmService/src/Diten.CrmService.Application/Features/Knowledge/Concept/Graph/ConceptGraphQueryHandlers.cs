using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Graph;

/// <summary>Shared assembly + filtering for the read-only graph projections. Ordering is deterministic (nodes by name,
/// edges by Priority → RelationshipCode as the repositories already return them).</summary>
internal static class ConceptGraphAssembler
{
    public static IEnumerable<ConceptNode> ApplyNodeFilters(
        IEnumerable<ConceptNode> nodes, DateTimeOffset? effectiveAt, bool includeArchived)
    {
        if (!includeArchived)
        {
            nodes = nodes.Where(n => !n.IsArchived());
        }

        if (effectiveAt is { } at)
        {
            nodes = nodes.Where(n => n.IsEffectiveAt(at));
        }

        return nodes;
    }

    public static IEnumerable<ConceptRelationship> ApplyEdgeFilters(
        IEnumerable<ConceptRelationship> edges, DateTimeOffset? effectiveAt, bool includeArchived)
    {
        if (!includeArchived)
        {
            edges = edges.Where(e => !e.IsArchived());
        }

        if (effectiveAt is { } at)
        {
            edges = edges.Where(e => e.EffectiveFrom <= at && (e.EffectiveTo is null || at <= e.EffectiveTo));
        }

        return edges;
    }
}

public sealed class GetConceptGraphHandler : IRequestHandler<GetConceptGraphQuery, Response<ConceptGraphDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptRelationshipRepository _relationships;
    private readonly IConceptChainTemplateRepository _templates;

    public GetConceptGraphHandler(
        ITenantContext tenant,
        IConceptNodeRepository nodes,
        IConceptRelationshipRepository relationships,
        IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _nodes = nodes;
        _relationships = relationships;
        _templates = templates;
    }

    public async Task<Response<ConceptGraphDto>> Handle(GetConceptGraphQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptGraphDto>.Fail("Tenant context is required.", 400);
        }

        if (request.SubjectId == Guid.Empty)
        {
            return Response<ConceptGraphDto>.Fail("subjectId is required.", 400);
        }

        var nodes = ConceptGraphAssembler.ApplyNodeFilters(
                await _nodes.ListBySubjectAsync(tenantId, request.SubjectId, cancellationToken),
                request.EffectiveAt, request.IncludeArchived)
            .Select(ConceptGraphMapper.ToDto).ToList();

        var edges = ConceptGraphAssembler.ApplyEdgeFilters(
                await _relationships.ListBySubjectAsync(tenantId, request.SubjectId, cancellationToken),
                request.EffectiveAt, request.IncludeArchived)
            .Select(ConceptGraphMapper.ToDto).ToList();

        var templates = (await _templates.ListBySubjectAsync(tenantId, request.SubjectId, cancellationToken))
            .Where(t => request.IncludeArchived || !t.IsArchived())
            .Select(ConceptGraphMapper.ToDto).ToList();

        return Response<ConceptGraphDto>.Success(new ConceptGraphDto(request.SubjectId, nodes, edges, templates));
    }
}

public sealed class GetConceptGraphByNodeHandler
    : IRequestHandler<GetConceptGraphByNodeQuery, Response<ConceptGraphDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptRelationshipRepository _relationships;
    private readonly IConceptChainTemplateRepository _templates;

    public GetConceptGraphByNodeHandler(
        ITenantContext tenant,
        IConceptNodeRepository nodes,
        IConceptRelationshipRepository relationships,
        IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _nodes = nodes;
        _relationships = relationships;
        _templates = templates;
    }

    public async Task<Response<ConceptGraphDto>> Handle(
        GetConceptGraphByNodeQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptGraphDto>.Fail("Tenant context is required.", 400);
        }

        var node = await _nodes.GetByIdAsync(tenantId, request.NodeId, cancellationToken);
        if (node is null)
        {
            return Response<ConceptGraphDto>.Fail("Concept node not found.", 404);
        }

        var subjectNodes = ConceptGraphAssembler.ApplyNodeFilters(
                await _nodes.ListBySubjectAsync(tenantId, node.SubjectId, cancellationToken),
                null, request.IncludeArchived)
            .ToList();

        // Exactly 1 hop: edges directly incident to the node.
        var incidentEdges = ConceptGraphAssembler.ApplyEdgeFilters(
                await _relationships.ListBySubjectAsync(tenantId, node.SubjectId, cancellationToken),
                null, request.IncludeArchived)
            .Where(e => e.FromConceptNodeId == node.Id || e.ToConceptNodeId == node.Id)
            .ToList();

        var neighbourIds = incidentEdges
            .SelectMany(e => new[] { e.FromConceptNodeId, e.ToConceptNodeId })
            .Append(node.Id)
            .ToHashSet();

        var nodes = subjectNodes.Where(n => neighbourIds.Contains(n.Id))
            .Select(ConceptGraphMapper.ToDto).ToList();
        var edges = incidentEdges.Select(ConceptGraphMapper.ToDto).ToList();
        var templates = (await _templates.ListBySubjectAsync(tenantId, node.SubjectId, cancellationToken))
            .Where(t => request.IncludeArchived || !t.IsArchived())
            .Select(ConceptGraphMapper.ToDto).ToList();

        return Response<ConceptGraphDto>.Success(new ConceptGraphDto(node.SubjectId, nodes, edges, templates));
    }
}

public sealed class GetConceptGraphByContentHandler
    : IRequestHandler<GetConceptGraphByContentQuery, Response<ConceptGraphDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgeContentConceptLinkRepository _links;
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptRelationshipRepository _relationships;
    private readonly IConceptChainTemplateRepository _templates;

    public GetConceptGraphByContentHandler(
        ITenantContext tenant,
        IKnowledgeContentConceptLinkRepository links,
        IConceptNodeRepository nodes,
        IConceptRelationshipRepository relationships,
        IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _links = links;
        _nodes = nodes;
        _relationships = relationships;
        _templates = templates;
    }

    public async Task<Response<ConceptGraphDto>> Handle(
        GetConceptGraphByContentQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptGraphDto>.Fail("Tenant context is required.", 400);
        }

        // Layer 0 — nodes the content links to.
        var links = (await _links.ListByContentAsync(tenantId, request.ContentId, cancellationToken))
            .Where(l => request.IncludeArchived || !l.IsArchived())
            .ToList();
        var layer0NodeIds = links.Select(l => l.ConceptNodeId).ToHashSet();

        if (layer0NodeIds.Count == 0)
        {
            return Response<ConceptGraphDto>.Success(
                new ConceptGraphDto(Guid.Empty, Array.Empty<ConceptNodeDto>(), Array.Empty<ConceptRelationshipDto>(),
                    Array.Empty<ConceptChainTemplateDto>()));
        }

        // Resolve the linked nodes and the subject(s) they belong to.
        var allNodes = ConceptGraphAssembler.ApplyNodeFilters(
                await _nodes.ListAsync(tenantId, cancellationToken), null, request.IncludeArchived)
            .ToList();
        var layer0Nodes = allNodes.Where(n => layer0NodeIds.Contains(n.Id)).ToList();
        var subjectIds = layer0Nodes.Select(n => n.SubjectId).ToHashSet();

        // Layer 1 — 1-hop edges from the layer-0 nodes (fixed depth, no third layer).
        var edges = new List<ConceptRelationship>();
        foreach (var subjectId in subjectIds)
        {
            edges.AddRange(ConceptGraphAssembler.ApplyEdgeFilters(
                await _relationships.ListBySubjectAsync(tenantId, subjectId, cancellationToken),
                null, request.IncludeArchived));
        }

        var incidentEdges = edges
            .Where(e => layer0NodeIds.Contains(e.FromConceptNodeId) || layer0NodeIds.Contains(e.ToConceptNodeId))
            .ToList();

        var involvedNodeIds = incidentEdges
            .SelectMany(e => new[] { e.FromConceptNodeId, e.ToConceptNodeId })
            .Concat(layer0NodeIds)
            .ToHashSet();

        var nodes = allNodes.Where(n => involvedNodeIds.Contains(n.Id))
            .OrderBy(n => n.ConceptNodeName)
            .Select(ConceptGraphMapper.ToDto).ToList();

        var templates = new List<ConceptChainTemplateDto>();
        foreach (var subjectId in subjectIds)
        {
            templates.AddRange(
                (await _templates.ListBySubjectAsync(tenantId, subjectId, cancellationToken))
                .Where(t => request.IncludeArchived || !t.IsArchived())
                .Select(ConceptGraphMapper.ToDto));
        }

        var primarySubject = layer0Nodes.Select(n => n.SubjectId).FirstOrDefault();
        return Response<ConceptGraphDto>.Success(new ConceptGraphDto(
            primarySubject,
            nodes,
            incidentEdges.Select(ConceptGraphMapper.ToDto).ToList(),
            templates));
    }
}
