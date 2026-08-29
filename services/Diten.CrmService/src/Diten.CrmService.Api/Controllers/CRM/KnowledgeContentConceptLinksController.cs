using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Concept.Link;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.Concept.ConceptPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>MOD-0162 FU03 — content ↔ concept link authoring. Canonical under
/// <c>/api/crm/knowledge/content-concept-links</c>. No delete / update — a link is created or archived.</summary>
[Authorize]
public sealed class KnowledgeContentConceptLinksController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeContentConceptLinksController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/knowledge/content-concept-links")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? contentId,
        [FromQuery] Guid? conceptNodeId,
        [FromQuery] string? linkRole,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListContentConceptLinksQuery(contentId, conceptNodeId, linkRole, includeArchived), cancellationToken));

    [HttpPost("api/crm/knowledge/content-concept-links")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContentConceptLinkRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateKnowledgeContentConceptLinkCommand(
                request.KnowledgeContentId, request.ConceptNodeId, request.ConceptRelationshipId, request.LinkRole,
                request.SortOrder),
            cancellationToken));

    [HttpPost("api/crm/knowledge/content-concept-links/{linkId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid linkId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveKnowledgeContentConceptLinkCommand(linkId), cancellationToken));
}
