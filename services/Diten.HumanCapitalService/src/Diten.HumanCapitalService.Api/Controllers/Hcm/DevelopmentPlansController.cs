using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Commands;
using Diten.HumanCapitalService.Application.Features.DevelopmentPlans.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/development-plans")]
[Authorize]
public sealed class DevelopmentPlansController : CustomBaseController
{
    private readonly IMediator _mediator;

    public DevelopmentPlansController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(DevelopmentPlanGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDevelopmentPlanReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(DevelopmentPlanGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDevelopmentPlanReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(DevelopmentPlanGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] DevelopmentPlanReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateDevelopmentPlanReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(DevelopmentPlanGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateDevelopmentPlanReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(DevelopmentPlanGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteDevelopmentPlanReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(DevelopmentPlanGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDevelopmentPlanAuditMetadataQuery(id), ct));
}
