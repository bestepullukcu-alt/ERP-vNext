using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Concept.Relationship;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.Concept.ConceptPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>MOD-0162 FU03 — Concept relationship authoring. Canonical under
/// <c>/api/crm/knowledge/concept-relationships</c>. No delete.</summary>
[Authorize]
public sealed class KnowledgeConceptRelationshipsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeConceptRelationshipsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/knowledge/concept-relationships")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? fromNodeId,
        [FromQuery] Guid? toNodeId,
        [FromQuery] string? relationshipType,
        [FromQuery] bool? conformance,
        [FromQuery] string? status,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListConceptRelationshipsQuery(
                subjectId, fromNodeId, toNodeId, relationshipType, conformance, status, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/knowledge/concept-relationships/{relationshipId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid relationshipId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetConceptRelationshipQuery(relationshipId), cancellationToken));

    [HttpPost("api/crm/knowledge/concept-relationships")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateConceptRelationshipRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateConceptRelationshipCommand(
                request.SubjectId, request.FromConceptNodeId, request.ToConceptNodeId, request.RelationshipType,
                request.RelationshipCode, request.RelationshipName, request.EffectiveFrom, request.Direction,
                request.Priority, request.Status, request.EffectiveTo),
            cancellationToken));

    [HttpPut("api/crm/knowledge/concept-relationships/{relationshipId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid relationshipId, [FromBody] UpdateConceptRelationshipRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateConceptRelationshipCommand(
                relationshipId, request.RelationshipName, request.EffectiveFrom, request.Direction, request.Priority,
                request.Status, request.EffectiveTo),
            cancellationToken));

    [HttpPost("api/crm/knowledge/concept-relationships/{relationshipId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid relationshipId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveConceptRelationshipCommand(relationshipId), cancellationToken));
}
