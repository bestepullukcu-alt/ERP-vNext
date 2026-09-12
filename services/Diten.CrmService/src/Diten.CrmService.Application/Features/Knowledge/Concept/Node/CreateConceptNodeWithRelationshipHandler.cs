using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Concept.Relationship;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using NodeEntity = Diten.CrmService.Domain.Entities.ConceptNode;
using RelationshipEntity = Diten.CrmService.Domain.Entities.ConceptRelationship;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Node;

/// <summary>
/// SCMM-09 (②) combined-write handler. Reuses the SAME field validations as <c>CreateConceptNodeHandler</c> (V04/V05/V06)
/// and <c>CreateConceptRelationshipHandler</c> (type/direction/duplicate-active V11/cycle V10/conformance V16), then
/// persists the node and the edge atomically through <see cref="IConceptNodeWithRelationshipUnitOfWork"/>. No engine is
/// opened (D8) — conformance is derived, never enforced.
/// </summary>
public sealed class CreateConceptNodeWithRelationshipHandler
    : IRequestHandler<CreateConceptNodeWithRelationshipCommand, Response<ConceptNodeWithRelationshipResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptTypeRepository _types;
    private readonly IConceptRelationshipRepository _relationships;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IConceptNodeWithRelationshipUnitOfWork _unitOfWork;
    private readonly IKnowledgeConceptAuditPublisher? _audit;

    public CreateConceptNodeWithRelationshipHandler(
        ITenantContext tenant,
        IActorContext actor,
        IConceptNodeRepository nodes,
        IConceptTypeRepository types,
        IConceptRelationshipRepository relationships,
        IConceptChainTemplateRepository templates,
        IConceptNodeWithRelationshipUnitOfWork unitOfWork,
        IKnowledgeConceptAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _nodes = nodes;
        _types = types;
        _relationships = relationships;
        _templates = templates;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<Response<ConceptNodeWithRelationshipResult>> Handle(
        CreateConceptNodeWithRelationshipCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail("Tenant context is required.", 400);
        }

        // New-node field validation (mirror of CreateConceptNodeHandler).
        var nodeError = KnowledgeValidation.ValidateCode(request.ConceptNodeCode, "ConceptNodeCode")
            ?? KnowledgeValidation.ValidateName(request.ConceptNodeName, "ConceptNodeName")
            ?? ConceptGraphValidation.ValidateConceptStatus(request.NodeStatus)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.NodeEffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.NodeEffectiveFrom, request.NodeEffectiveTo)
            ?? ConceptGraphValidation.ValidateExternalRef(request.ExternalRefType, request.ExternalRefId);
        if (nodeError is not null)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(nodeError, 400);
        }

        if (request.ConceptTypeId == Guid.Empty)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail("ConceptTypeId is required and cannot be empty.", 400);
        }

        // Edge field validation (mirror of CreateConceptRelationshipHandler).
        var edgeError = ConceptGraphValidation.ValidateRelationshipType(request.RelationshipType)
            ?? ConceptGraphValidation.ValidateDirection(request.Direction)
            ?? KnowledgeValidation.ValidateCode(request.RelationshipCode, "RelationshipCode")
            ?? KnowledgeValidation.ValidateName(request.RelationshipName, "RelationshipName")
            ?? ConceptGraphValidation.ValidateConceptStatus(request.RelationshipStatus)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.RelationshipEffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.RelationshipEffectiveFrom, request.RelationshipEffectiveTo);
        if (edgeError is not null)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(edgeError, 400);
        }

        if (request.CounterpartConceptNodeId == Guid.Empty)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail("CounterpartConceptNodeId is required.", 400);
        }

        // V04/V05 — the type must exist, be non-archived and belong to the request subject.
        var type = await _types.GetByIdAsync(tenantId, request.ConceptTypeId, cancellationToken);
        if (type is null)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(
                "ConceptTypeId does not reference an existing concept type.", 400);
        }

        if (type.IsArchived())
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(
                "A concept node cannot be created under an archived concept type.", 400);
        }

        if (type.SubjectId != request.SubjectId)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(
                "The node's SubjectId must equal the concept type's subject.", 400);
        }

        // V06 — node code unique within (subject, type) among non-archived rows.
        var code = request.ConceptNodeCode.Trim();
        if (await _nodes.GetActiveByCodeAsync(tenantId, request.SubjectId, request.ConceptTypeId, code, cancellationToken)
            is { } duplicateNode)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(
                $"A non-archived concept node already uses ConceptNodeCode '{code}' (conceptNodeId={duplicateNode.Id}).", 409);
        }

        // Counterpart node must exist, be non-archived and belong to the same subject (V08/V09).
        var counterpart = await _nodes.GetByIdAsync(tenantId, request.CounterpartConceptNodeId, cancellationToken);
        if (counterpart is null)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(
                "CounterpartConceptNodeId must reference an existing node.", 400);
        }

        if (counterpart.IsArchived())
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(
                "A relationship cannot be created on an archived node.", 400);
        }

        if (counterpart.SubjectId != request.SubjectId)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(
                "A relationship must connect two nodes of the same subject.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var node = new NodeEntity
        {
            TenantId = tenantId,
            SubjectId = request.SubjectId,
            ConceptTypeId = request.ConceptTypeId,
            ConceptNodeCode = code,
            ConceptNodeName = request.ConceptNodeName.Trim(),
            Description = KnowledgeValidation.Trim(request.NodeDescription),
            Status = ConceptStatuses.Normalize(request.NodeStatus),
            EffectiveFrom = request.NodeEffectiveFrom,
            EffectiveTo = request.NodeEffectiveTo,
            ExternalRefType = CreateConceptNodeHandler.NormalizeExternalRefType(request.ExternalRefType),
            ExternalRefId = KnowledgeValidation.Trim(request.ExternalRefId),
            MetadataJson = KnowledgeValidation.Trim(request.MetadataJson),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        // Edge direction: the new node is the source (default) or the target.
        var fromId = request.NewNodeIsSource ? node.Id : request.CounterpartConceptNodeId;
        var toId = request.NewNodeIsSource ? request.CounterpartConceptNodeId : node.Id;
        var fromTypeId = request.NewNodeIsSource ? request.ConceptTypeId : counterpart.ConceptTypeId;
        var toTypeId = request.NewNodeIsSource ? counterpart.ConceptTypeId : request.ConceptTypeId;

        var relationshipType = ConceptRelationshipTypes.Normalize(request.RelationshipType);
        var subjectEdges = await _relationships.ListBySubjectAsync(tenantId, request.SubjectId, cancellationToken);

        // V11 — duplicate active (From, To, RelationshipType). The new node id is fresh, but the same guard is applied.
        var duplicateEdge = subjectEdges.FirstOrDefault(e =>
            e.IsActive()
            && e.FromConceptNodeId == fromId
            && e.ToConceptNodeId == toId
            && string.Equals(e.RelationshipType, relationshipType, StringComparison.OrdinalIgnoreCase));
        if (duplicateEdge is not null)
        {
            return Response<ConceptNodeWithRelationshipResult>.Fail(
                $"An active relationship already exists for this (From, To, {relationshipType}) triple " +
                $"(relationshipId={duplicateEdge.Id}).", 409);
        }

        // V10 — cycle among active edges (only when this edge would be active).
        var willBeActive = string.Equals(
            ConceptStatuses.Normalize(request.RelationshipStatus), ConceptStatuses.Active, StringComparison.OrdinalIgnoreCase);
        if (willBeActive)
        {
            var activeEdges = subjectEdges.Where(e => e.IsActive()).ToList();
            if (ConceptRelationshipGraph.WouldCreateCycle(activeEdges, fromId, toId))
            {
                return Response<ConceptNodeWithRelationshipResult>.Fail(
                    "This relationship would create a cycle among active relationships.", 400);
            }
        }

        // V16 — template conformance (derived, never rejects).
        var subjectTemplates = await _templates.ListBySubjectAsync(tenantId, request.SubjectId, cancellationToken);
        var isConforming = ConceptRelationshipGraph.IsConforming(subjectTemplates, fromTypeId, toTypeId);

        var edge = new RelationshipEntity
        {
            TenantId = tenantId,
            SubjectId = request.SubjectId,
            FromConceptNodeId = fromId,
            ToConceptNodeId = toId,
            RelationshipType = relationshipType,
            RelationshipCode = request.RelationshipCode.Trim(),
            RelationshipName = request.RelationshipName.Trim(),
            Direction = ConceptDirections.Normalize(request.Direction),
            Priority = request.Priority,
            IsTemplateConforming = isConforming,
            Status = ConceptStatuses.Normalize(request.RelationshipStatus),
            EffectiveFrom = request.RelationshipEffectiveFrom,
            EffectiveTo = request.RelationshipEffectiveTo,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _unitOfWork.CommitAsync(node, edge, cancellationToken);

        if (_audit is not null)
        {
            await _audit.PublishAsync(KnowledgeConceptAuditEvents.NodeWithRelationshipCreated, tenantId,
                KnowledgeConceptAuditEntities.ConceptNode, node.Id, node.Version,
                $"{node.ConceptNodeCode};edge={edge.Id}", cancellationToken);
        }

        return Response<ConceptNodeWithRelationshipResult>.Success(
            new ConceptNodeWithRelationshipResult(node.Id, edge.Id), 201);
    }
}
