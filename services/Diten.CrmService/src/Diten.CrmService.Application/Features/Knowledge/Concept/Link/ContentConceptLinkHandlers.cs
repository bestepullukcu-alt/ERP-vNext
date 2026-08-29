using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Link;

public sealed class CreateKnowledgeContentConceptLinkHandler
    : IRequestHandler<CreateKnowledgeContentConceptLinkCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgeContentConceptLinkRepository _links;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptRelationshipRepository _relationships;

    public CreateKnowledgeContentConceptLinkHandler(
        ITenantContext tenant,
        IActorContext actor,
        IKnowledgeContentConceptLinkRepository links,
        IKnowledgeContentRepository contents,
        IConceptNodeRepository nodes,
        IConceptRelationshipRepository relationships)
    {
        _tenant = tenant;
        _actor = actor;
        _links = links;
        _contents = contents;
        _nodes = nodes;
        _relationships = relationships;
    }

    public async Task<Response<Guid>> Handle(
        CreateKnowledgeContentConceptLinkCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (ConceptGraphValidation.ValidateLinkRole(request.LinkRole) is { } roleError)
        {
            return Response<Guid>.Fail(roleError, 400);
        }

        if (request.KnowledgeContentId == Guid.Empty || request.ConceptNodeId == Guid.Empty)
        {
            return Response<Guid>.Fail("KnowledgeContentId and ConceptNodeId are required.", 400);
        }

        // V18 — content must exist and be non-archived.
        var content = await _contents.GetByIdAsync(tenantId, request.KnowledgeContentId, cancellationToken);
        if (content is null)
        {
            return Response<Guid>.Fail("KnowledgeContentId does not reference an existing content.", 400);
        }

        if (content.IsArchived())
        {
            return Response<Guid>.Fail("A link cannot be created for archived content.", 400);
        }

        // V18 — node must exist and be non-archived.
        var node = await _nodes.GetByIdAsync(tenantId, request.ConceptNodeId, cancellationToken);
        if (node is null)
        {
            return Response<Guid>.Fail("ConceptNodeId does not reference an existing concept node.", 400);
        }

        if (node.IsArchived())
        {
            return Response<Guid>.Fail("A link cannot be anchored to an archived concept node.", 400);
        }

        // V21 — an optional relationship context must contain the anchored node (From or To).
        if (request.ConceptRelationshipId is { } relationshipId && relationshipId != Guid.Empty)
        {
            var relationship = await _relationships.GetByIdAsync(tenantId, relationshipId, cancellationToken);
            if (relationship is null)
            {
                return Response<Guid>.Fail("ConceptRelationshipId does not reference an existing relationship.", 400);
            }

            if (relationship.FromConceptNodeId != request.ConceptNodeId
                && relationship.ToConceptNodeId != request.ConceptNodeId)
            {
                return Response<Guid>.Fail(
                    "ConceptRelationshipId must contain the anchored ConceptNodeId (its From or To).", 400);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new KnowledgeContentConceptLink
        {
            TenantId = tenantId,
            KnowledgeContentId = request.KnowledgeContentId,
            ConceptNodeId = request.ConceptNodeId,
            ConceptRelationshipId = request.ConceptRelationshipId == Guid.Empty ? null : request.ConceptRelationshipId,
            LinkRole = ConceptLinkRoles.Normalize(request.LinkRole),
            SortOrder = request.SortOrder,
            Status = ConceptStatuses.Active,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _links.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class ArchiveKnowledgeContentConceptLinkHandler
    : IRequestHandler<ArchiveKnowledgeContentConceptLinkCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgeContentConceptLinkRepository _links;

    public ArchiveKnowledgeContentConceptLinkHandler(
        ITenantContext tenant, IActorContext actor, IKnowledgeContentConceptLinkRepository links)
    {
        _tenant = tenant;
        _actor = actor;
        _links = links;
    }

    public async Task<Response<bool>> Handle(
        ArchiveKnowledgeContentConceptLinkCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _links.GetByIdAsync(tenantId, request.LinkId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Content-concept link not found.", 404);
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

        await _links.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ListContentConceptLinksHandler
    : IRequestHandler<ListContentConceptLinksQuery, Response<KnowledgeContentConceptLinkListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgeContentConceptLinkRepository _links;

    public ListContentConceptLinksHandler(ITenantContext tenant, IKnowledgeContentConceptLinkRepository links)
    {
        _tenant = tenant;
        _links = links;
    }

    public async Task<Response<KnowledgeContentConceptLinkListDto>> Handle(
        ListContentConceptLinksQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<KnowledgeContentConceptLinkListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<KnowledgeContentConceptLink> rows;
        if (request.ContentId is { } contentId && contentId != Guid.Empty)
        {
            rows = await _links.ListByContentAsync(tenantId, contentId, cancellationToken);
        }
        else if (request.ConceptNodeId is { } nodeId && nodeId != Guid.Empty)
        {
            rows = await _links.ListByNodeAsync(tenantId, nodeId, cancellationToken);
        }
        else
        {
            rows = await _links.ListAsync(tenantId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.LinkRole))
        {
            var role = ConceptLinkRoles.Normalize(request.LinkRole);
            rows = rows.Where(x => string.Equals(x.LinkRole, role, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(ConceptGraphMapper.ToDto).ToList();
        return Response<KnowledgeContentConceptLinkListDto>.Success(
            new KnowledgeContentConceptLinkListDto(items, items.Count));
    }
}
