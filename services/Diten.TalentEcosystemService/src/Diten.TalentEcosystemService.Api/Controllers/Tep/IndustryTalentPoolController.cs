using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.IndustryTalentPool;
using Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Commands;
using Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/industry-talent-pool")]
[Authorize]
public sealed class IndustryTalentPoolController : CustomBaseController
{
    private readonly IMediator _mediator;

    public IndustryTalentPoolController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(IndustryTalentPoolGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustryTalentPoolReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(IndustryTalentPoolGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustryTalentPoolReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(IndustryTalentPoolGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] IndustryTalentPoolReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateIndustryTalentPoolReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(IndustryTalentPoolGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateIndustryTalentPoolReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(IndustryTalentPoolGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteIndustryTalentPoolReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(IndustryTalentPoolGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustryTalentPoolAuditMetadataQuery(id), ct));
}
