using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Commands;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/industry-knowledge-network")]
[Authorize]
public sealed class IndustryKnowledgeNetworkController : CustomBaseController
{
    private readonly IMediator _mediator;

    public IndustryKnowledgeNetworkController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(IndustryKnowledgeNetworkGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustryKnowledgeNetworkReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(IndustryKnowledgeNetworkGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustryKnowledgeNetworkReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(IndustryKnowledgeNetworkGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] IndustryKnowledgeNetworkReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateIndustryKnowledgeNetworkReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(IndustryKnowledgeNetworkGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateIndustryKnowledgeNetworkReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(IndustryKnowledgeNetworkGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteIndustryKnowledgeNetworkReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(IndustryKnowledgeNetworkGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustryKnowledgeNetworkAuditMetadataQuery(id), ct));
}
