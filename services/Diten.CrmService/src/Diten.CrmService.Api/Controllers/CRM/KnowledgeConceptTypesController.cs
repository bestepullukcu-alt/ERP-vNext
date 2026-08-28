using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Concept.Type;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.Concept.ConceptPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU03 — Concept type authoring. Canonical under <c>/api/crm/knowledge/concept-types</c>. Reads use
/// <c>crm.knowledge.concept.read</c> and writes <c>crm.knowledge.concept.manage</c> canonically, both on the documented
/// DEV-ONLY territory fallback until MOD-0162-FU03-RBAC lands. <b>No delete endpoint</b>: closing a type is Archive.
/// </summary>
[Authorize]
public sealed class KnowledgeConceptTypesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeConceptTypesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/knowledge/concept-types")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? subjectId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListConceptTypesQuery(subjectId, status, search, includeArchived), cancellationToken));

    [HttpGet("api/crm/knowledge/concept-types/{conceptTypeId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid conceptTypeId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetConceptTypeQuery(conceptTypeId), cancellationToken));

    [HttpPost("api/crm/knowledge/concept-types")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateConceptTypeRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateConceptTypeCommand(
                request.SubjectId, request.ConceptTypeCode, request.ConceptTypeName, request.Description,
                request.SortOrder, request.Status),
            cancellationToken));

    [HttpPut("api/crm/knowledge/concept-types/{conceptTypeId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid conceptTypeId, [FromBody] UpdateConceptTypeRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateConceptTypeCommand(
                conceptTypeId, request.ConceptTypeName, request.Description, request.SortOrder, request.Status),
            cancellationToken));

    [HttpPost("api/crm/knowledge/concept-types/{conceptTypeId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid conceptTypeId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveConceptTypeCommand(conceptTypeId), cancellationToken));
}
