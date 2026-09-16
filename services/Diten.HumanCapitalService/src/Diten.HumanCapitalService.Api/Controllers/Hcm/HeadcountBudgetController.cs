using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget.Commands;
using Diten.HumanCapitalService.Application.Features.HeadcountBudget.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/headcount-budget")]
[Authorize]
public sealed class HeadcountBudgetController : CustomBaseController
{
    private readonly IMediator _mediator;

    public HeadcountBudgetController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(HeadcountBudgetGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHeadcountBudgetReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(HeadcountBudgetGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHeadcountBudgetReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(HeadcountBudgetGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] HeadcountBudgetReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateHeadcountBudgetReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(HeadcountBudgetGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateHeadcountBudgetReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(HeadcountBudgetGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteHeadcountBudgetReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(HeadcountBudgetGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHeadcountBudgetAuditMetadataQuery(id), ct));
}
