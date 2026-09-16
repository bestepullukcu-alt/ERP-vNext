using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.Succession;
using Diten.HumanCapitalService.Application.Features.Succession.Commands;
using Diten.HumanCapitalService.Application.Features.Succession.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/succession")]
[Authorize]
public sealed class SuccessionController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SuccessionController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(SuccessionGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSuccessionReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(SuccessionGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSuccessionReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(SuccessionGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] SuccessionReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateSuccessionReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(SuccessionGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateSuccessionReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(SuccessionGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteSuccessionReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(SuccessionGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSuccessionAuditMetadataQuery(id), ct));
}
