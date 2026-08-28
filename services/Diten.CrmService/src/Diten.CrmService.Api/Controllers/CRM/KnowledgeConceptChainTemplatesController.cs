using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.Concept.ConceptPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>MOD-0162 FU03 — Concept chain template authoring. Canonical under
/// <c>/api/crm/knowledge/concept-chain-templates</c>. No delete.</summary>
[Authorize]
public sealed class KnowledgeConceptChainTemplatesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeConceptChainTemplatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/knowledge/concept-chain-templates")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? subjectId,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListConceptChainTemplatesQuery(subjectId, status, effectiveAt, search, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/knowledge/concept-chain-templates/{templateId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid templateId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetConceptChainTemplateQuery(templateId), cancellationToken));

    [HttpPost("api/crm/knowledge/concept-chain-templates")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateConceptChainTemplateRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateConceptChainTemplateCommand(
                request.SubjectId, request.ChainCode, request.ChainName, request.OrderedConceptTypes,
                request.EffectiveFrom, request.Description, request.Status, request.ChainVersion, request.EffectiveTo),
            cancellationToken));

    [HttpPut("api/crm/knowledge/concept-chain-templates/{templateId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid templateId, [FromBody] UpdateConceptChainTemplateRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateConceptChainTemplateCommand(
                templateId, request.ChainName, request.OrderedConceptTypes, request.EffectiveFrom, request.Description,
                request.Status, request.ChainVersion, request.EffectiveTo),
            cancellationToken));

    [HttpPost("api/crm/knowledge/concept-chain-templates/{templateId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(Guid templateId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveConceptChainTemplateCommand(templateId), cancellationToken));
}
