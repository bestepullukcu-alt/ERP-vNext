using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Concept.Node;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.Concept.ConceptPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>MOD-0162 FU03 — Concept node authoring. Canonical under <c>/api/crm/knowledge/concept-nodes</c>. No delete.</summary>
[Authorize]
public sealed class KnowledgeConceptNodesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeConceptNodesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/knowledge/concept-nodes")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? conceptTypeId,
        [FromQuery] string? status,
        [FromQuery] string? externalRefType,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListConceptNodesQuery(
                subjectId, conceptTypeId, status, externalRefType, effectiveAt, search, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/knowledge/concept-nodes/{conceptNodeId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid conceptNodeId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetConceptNodeQuery(conceptNodeId), cancellationToken));

    [HttpPost("api/crm/knowledge/concept-nodes")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateConceptNodeRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateConceptNodeCommand(
                request.SubjectId, request.ConceptTypeId, request.ConceptNodeCode, request.ConceptNodeName,
                request.EffectiveFrom, request.Description, request.Status, request.EffectiveTo,
                request.ExternalRefType, request.ExternalRefId, request.MetadataJson),
            cancellationToken));

    [HttpPut("api/crm/knowledge/concept-nodes/{conceptNodeId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid conceptNodeId, [FromBody] UpdateConceptNodeRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateConceptNodeCommand(
                conceptNodeId, request.ConceptNodeName, request.EffectiveFrom, request.Description, request.Status,
                request.EffectiveTo, request.ExternalRefType, request.ExternalRefId, request.MetadataJson),
            cancellationToken));

    [HttpPost("api/crm/knowledge/concept-nodes/{conceptNodeId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid conceptNodeId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveConceptNodeCommand(conceptNodeId), cancellationToken));
}
