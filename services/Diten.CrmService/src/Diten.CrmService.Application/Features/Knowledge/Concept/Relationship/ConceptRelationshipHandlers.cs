using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using RelationshipEntity = Diten.CrmService.Domain.Entities.ConceptRelationship;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Relationship;

/// <summary>Shared relationship graph logic: read-time cycle detection over ACTIVE edges and template-conformance
/// derivation. Both are computed in memory (no denormalized traversal cache — that is an engine decision, F4/MOD-0058).
/// Cycle detection treats each active relationship as a single directed edge From → To (the <c>bidirectional</c> flag is
/// a traversal-semantics annotation, not a second chain step, so it never makes an edge self-cyclic).</summary>
internal static class ConceptRelationshipGraph
{
    /// <summary>Would the directed edge (from → to) close a cycle, given the other active edges? True if <c>to</c> can
    /// already reach <c>from</c> through active edges (then from → to closes the loop), or from == to.</summary>
    public static bool WouldCreateCycle(
        IReadOnlyList<RelationshipEntity> otherActiveEdges, Guid from, Guid to)
    {
        if (from == to)
        {
            return true;
        }

        var adjacency = new Dictionary<Guid, List<Guid>>();
        foreach (var edge in otherActiveEdges)
        {
            if (!adjacency.TryGetValue(edge.FromConceptNodeId, out var list))
            {
                list = new List<Guid>();
                adjacency[edge.FromConceptNodeId] = list;
            }

            list.Add(edge.ToConceptNodeId);
        }

        // Depth-first search from `to`; if we reach `from`, the proposed from → to would close a cycle.
        var stack = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        stack.Push(to);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == from)
            {
                return true;
            }

            if (!visited.Add(current))
            {
                continue;
            }

            if (adjacency.TryGetValue(current, out var neighbours))
            {
                foreach (var next in neighbours)
                {
                    stack.Push(next);
                }
            }
        }

        return false;
    }

    /// <summary>Is (fromType → toType) an adjacent ordered pair in any non-archived chain template of the subject?</summary>
    public static bool IsConforming(
        IReadOnlyList<ConceptChainTemplate> subjectTemplates, Guid fromTypeId, Guid toTypeId)
    {
        foreach (var template in subjectTemplates)
        {
            if (template.IsArchived())
            {
                continue;
            }

            var ordered = template.OrderedConceptTypes;
            for (var i = 0; i + 1 < ordered.Count; i++)
            {
                if (ordered[i] == fromTypeId && ordered[i + 1] == toTypeId)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public sealed class CreateConceptRelationshipHandler
    : IRequestHandler<CreateConceptRelationshipCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptRelationshipRepository _relationships;
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptChainTemplateRepository _templates;

    public CreateConceptRelationshipHandler(
        ITenantContext tenant,
        IActorContext actor,
        IConceptRelationshipRepository relationships,
        IConceptNodeRepository nodes,
        IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _actor = actor;
        _relationships = relationships;
        _nodes = nodes;
        _templates = templates;
    }

    public async Task<Response<Guid>> Handle(CreateConceptRelationshipCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = ConceptGraphValidation.ValidateRelationshipType(request.RelationshipType)
            ?? ConceptGraphValidation.ValidateDirection(request.Direction)
            ?? KnowledgeValidation.ValidateCode(request.RelationshipCode, "RelationshipCode")
            ?? KnowledgeValidation.ValidateName(request.RelationshipName, "RelationshipName")
            ?? ConceptGraphValidation.ValidateConceptStatus(request.Status)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        // V07 — self-loop.
        if (request.FromConceptNodeId == request.ToConceptNodeId)
        {
            return Response<Guid>.Fail("A relationship cannot connect a node to itself (From == To).", 400);
        }

        var from = await _nodes.GetByIdAsync(tenantId, request.FromConceptNodeId, cancellationToken);
        var to = await _nodes.GetByIdAsync(tenantId, request.ToConceptNodeId, cancellationToken);
        if (from is null || to is null)
        {
            return Response<Guid>.Fail("FromConceptNodeId / ToConceptNodeId must reference existing nodes.", 400);
        }

        // V09 — archived node cannot take a new relationship.
        if (from.IsArchived() || to.IsArchived())
        {
            return Response<Guid>.Fail("A relationship cannot be created on an archived node.", 400);
        }

        // V08 — cross-subject edge. Both endpoints and the request subject must agree.
        if (from.SubjectId != request.SubjectId || to.SubjectId != request.SubjectId)
        {
            return Response<Guid>.Fail("A relationship must connect two nodes of the same subject.", 400);
        }

        var relationshipType = ConceptRelationshipTypes.Normalize(request.RelationshipType);
        var subjectEdges = await _relationships.ListBySubjectAsync(tenantId, request.SubjectId, cancellationToken);

        // V11 — duplicate active (From, To, RelationshipType).
        var duplicate = subjectEdges.FirstOrDefault(e =>
            e.IsActive()
            && e.FromConceptNodeId == request.FromConceptNodeId
            && e.ToConceptNodeId == request.ToConceptNodeId
            && string.Equals(e.RelationshipType, relationshipType, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            return Response<Guid>.Fail(
                $"An active relationship already exists for this (From, To, {relationshipType}) triple " +
                $"(relationshipId={duplicate.Id}).", 409);
        }

        // V10 — cycle among active edges (only when this edge would be active).
        var willBeActive = string.Equals(
            ConceptStatuses.Normalize(request.Status), ConceptStatuses.Active, StringComparison.OrdinalIgnoreCase);
        if (willBeActive)
        {
            var activeEdges = subjectEdges.Where(e => e.IsActive()).ToList();
            if (ConceptRelationshipGraph.WouldCreateCycle(
                    activeEdges, request.FromConceptNodeId, request.ToConceptNodeId))
            {
                return Response<Guid>.Fail(
                    "This relationship would create a cycle among active relationships.", 400);
            }
        }

        // V16 — template conformance (derived, never rejects).
        var subjectTemplates = await _templates.ListBySubjectAsync(tenantId, request.SubjectId, cancellationToken);
        var isConforming = ConceptRelationshipGraph.IsConforming(
            subjectTemplates, from.ConceptTypeId, to.ConceptTypeId);

        var now = DateTimeOffset.UtcNow;
        var entity = new RelationshipEntity
        {
            TenantId = tenantId,
            SubjectId = request.SubjectId,
            FromConceptNodeId = request.FromConceptNodeId,
            ToConceptNodeId = request.ToConceptNodeId,
            RelationshipType = relationshipType,
            RelationshipCode = request.RelationshipCode.Trim(),
            RelationshipName = request.RelationshipName.Trim(),
            Direction = ConceptDirections.Normalize(request.Direction),
            Priority = request.Priority,
            IsTemplateConforming = isConforming,
            Status = ConceptStatuses.Normalize(request.Status),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _relationships.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateConceptRelationshipHandler
    : IRequestHandler<UpdateConceptRelationshipCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptRelationshipRepository _relationships;
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptChainTemplateRepository _templates;

    public UpdateConceptRelationshipHandler(
        ITenantContext tenant,
        IActorContext actor,
        IConceptRelationshipRepository relationships,
        IConceptNodeRepository nodes,
        IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _actor = actor;
        _relationships = relationships;
        _nodes = nodes;
        _templates = templates;
    }

    public async Task<Response<bool>> Handle(UpdateConceptRelationshipCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _relationships.GetByIdAsync(tenantId, request.ConceptRelationshipId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept relationship not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Fail("An archived concept relationship cannot be updated.", 409);
        }

        if (string.Equals(request.Status?.Trim(), ConceptStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive a concept relationship.", 400);
        }

        var error = ConceptGraphValidation.ValidateDirection(request.Direction)
            ?? KnowledgeValidation.ValidateName(request.RelationshipName, "RelationshipName")
            ?? ConceptGraphValidation.ValidateConceptStatus(request.Status)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo);
        if (error is not null)
        {
            return Response<bool>.Fail(error, 400);
        }

        var newStatus = ConceptStatuses.Normalize(request.Status ?? entity.Status);
        var willBeActive = string.Equals(newStatus, ConceptStatuses.Active, StringComparison.OrdinalIgnoreCase);
        var subjectEdges = await _relationships.ListBySubjectAsync(tenantId, entity.SubjectId, cancellationToken);

        // Activating (or staying active) re-runs the duplicate + cycle guards, excluding this edge.
        if (willBeActive)
        {
            var duplicate = subjectEdges.FirstOrDefault(e =>
                e.Id != entity.Id
                && e.IsActive()
                && e.FromConceptNodeId == entity.FromConceptNodeId
                && e.ToConceptNodeId == entity.ToConceptNodeId
                && string.Equals(e.RelationshipType, entity.RelationshipType, StringComparison.OrdinalIgnoreCase));
            if (duplicate is not null)
            {
                return Response<bool>.Fail(
                    $"An active relationship already exists for this (From, To, {entity.RelationshipType}) triple " +
                    $"(relationshipId={duplicate.Id}).", 409);
            }

            var activeEdges = subjectEdges.Where(e => e.Id != entity.Id && e.IsActive()).ToList();
            if (ConceptRelationshipGraph.WouldCreateCycle(
                    activeEdges, entity.FromConceptNodeId, entity.ToConceptNodeId))
            {
                return Response<bool>.Fail(
                    "Activating this relationship would create a cycle among active relationships.", 400);
            }
        }

        // Re-derive conformance (templates may have changed since create).
        var from = await _nodes.GetByIdAsync(tenantId, entity.FromConceptNodeId, cancellationToken);
        var to = await _nodes.GetByIdAsync(tenantId, entity.ToConceptNodeId, cancellationToken);
        var subjectTemplates = await _templates.ListBySubjectAsync(tenantId, entity.SubjectId, cancellationToken);
        var isConforming = from is not null && to is not null
            && ConceptRelationshipGraph.IsConforming(subjectTemplates, from.ConceptTypeId, to.ConceptTypeId);

        var now = DateTimeOffset.UtcNow;
        entity.RelationshipName = request.RelationshipName.Trim();
        entity.Direction = ConceptDirections.Normalize(request.Direction);
        entity.Priority = request.Priority;
        entity.Status = newStatus;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.IsTemplateConforming = isConforming;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _relationships.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveConceptRelationshipHandler
    : IRequestHandler<ArchiveConceptRelationshipCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptRelationshipRepository _relationships;

    public ArchiveConceptRelationshipHandler(
        ITenantContext tenant, IActorContext actor, IConceptRelationshipRepository relationships)
    {
        _tenant = tenant;
        _actor = actor;
        _relationships = relationships;
    }

    public async Task<Response<bool>> Handle(
        ArchiveConceptRelationshipCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _relationships.GetByIdAsync(tenantId, request.ConceptRelationshipId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept relationship not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = ConceptStatuses.Archived;
        entity.ArchivedAt = now;
        entity.ArchivedBy = _actor.ActorName;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _relationships.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ListConceptRelationshipsHandler
    : IRequestHandler<ListConceptRelationshipsQuery, Response<ConceptRelationshipListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptRelationshipRepository _relationships;

    public ListConceptRelationshipsHandler(ITenantContext tenant, IConceptRelationshipRepository relationships)
    {
        _tenant = tenant;
        _relationships = relationships;
    }

    public async Task<Response<ConceptRelationshipListDto>> Handle(
        ListConceptRelationshipsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptRelationshipListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<RelationshipEntity> rows = request.SubjectId is { } subjectId && subjectId != Guid.Empty
            ? await _relationships.ListBySubjectAsync(tenantId, subjectId, cancellationToken)
            : await _relationships.ListAsync(tenantId, cancellationToken);

        if (request.FromNodeId is { } fromId && fromId != Guid.Empty)
        {
            rows = rows.Where(x => x.FromConceptNodeId == fromId);
        }

        if (request.ToNodeId is { } toId && toId != Guid.Empty)
        {
            rows = rows.Where(x => x.ToConceptNodeId == toId);
        }

        if (!string.IsNullOrWhiteSpace(request.RelationshipType))
        {
            var type = ConceptRelationshipTypes.Normalize(request.RelationshipType);
            rows = rows.Where(x => string.Equals(x.RelationshipType, type, StringComparison.OrdinalIgnoreCase));
        }

        if (request.Conformance is { } conformance)
        {
            rows = rows.Where(x => x.IsTemplateConforming == conformance);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ConceptStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.Status == status);
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(ConceptGraphMapper.ToDto).ToList();
        return Response<ConceptRelationshipListDto>.Success(new ConceptRelationshipListDto(items, items.Count));
    }
}

public sealed class GetConceptRelationshipHandler
    : IRequestHandler<GetConceptRelationshipQuery, Response<ConceptRelationshipDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptRelationshipRepository _relationships;

    public GetConceptRelationshipHandler(ITenantContext tenant, IConceptRelationshipRepository relationships)
    {
        _tenant = tenant;
        _relationships = relationships;
    }

    public async Task<Response<ConceptRelationshipDto>> Handle(
        GetConceptRelationshipQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptRelationshipDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _relationships.GetByIdAsync(tenantId, request.ConceptRelationshipId, cancellationToken);
        return entity is null
            ? Response<ConceptRelationshipDto>.Fail("Concept relationship not found.", 404)
            : Response<ConceptRelationshipDto>.Success(ConceptGraphMapper.ToDto(entity));
    }
}
