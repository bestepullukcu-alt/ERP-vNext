using Diten.CrmService.Application.Features.Knowledge.Concept.Contract;
using Diten.CrmService.Application.Features.Knowledge.Concept.Graph;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.Concept.ConceptPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU03 — Concept graph contract + read-only adjacency views. Canonical under
/// <c>/api/crm/knowledge/concept-graph</c>. These are ADJACENCY reads, NOT an engine: no multi-hop traversal, no
/// best-path, no scoring, no recommendation. <c>by-node</c> is exactly 1 hop; <c>by-content</c> is exactly 2 edge layers.
/// </summary>
[Authorize]
public sealed class KnowledgeConceptGraphController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeConceptGraphController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/knowledge/concept-graph/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetConceptGraphContractQuery(), cancellationToken));

    [HttpGet("api/crm/knowledge/concept-graph")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Graph(
        [FromQuery] Guid subjectId,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetConceptGraphQuery(subjectId, effectiveAt, includeArchived), cancellationToken));

    [HttpGet("api/crm/knowledge/concept-graph/by-node/{nodeId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ByNode(
        Guid nodeId, [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetConceptGraphByNodeQuery(nodeId, includeArchived), cancellationToken));

    [HttpGet("api/crm/knowledge/concept-graph/by-content/{contentId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ByContent(
        Guid contentId, [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetConceptGraphByContentQuery(contentId, includeArchived), cancellationToken));
}
