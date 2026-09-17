using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Commands;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/industry-succession-pool")]
[Authorize]
public sealed class IndustrySuccessionPoolController : CustomBaseController
{
    private readonly IMediator _mediator;

    public IndustrySuccessionPoolController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(IndustrySuccessionPoolGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustrySuccessionPoolReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(IndustrySuccessionPoolGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustrySuccessionPoolReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(IndustrySuccessionPoolGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] IndustrySuccessionPoolReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateIndustrySuccessionPoolReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(IndustrySuccessionPoolGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateIndustrySuccessionPoolReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(IndustrySuccessionPoolGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteIndustrySuccessionPoolReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(IndustrySuccessionPoolGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetIndustrySuccessionPoolAuditMetadataQuery(id), ct));
}
