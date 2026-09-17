using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.SelfService;
using Diten.HumanCapitalService.Application.Features.SelfService.Commands;
using Diten.HumanCapitalService.Application.Features.SelfService.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/self-service")]
[Authorize]
public sealed class SelfServiceController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SelfServiceController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(SelfServiceGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSelfServiceReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(SelfServiceGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSelfServiceReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(SelfServiceGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] SelfServiceReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateSelfServiceReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(SelfServiceGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateSelfServiceReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(SelfServiceGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteSelfServiceReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(SelfServiceGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSelfServiceAuditMetadataQuery(id), ct));
}
