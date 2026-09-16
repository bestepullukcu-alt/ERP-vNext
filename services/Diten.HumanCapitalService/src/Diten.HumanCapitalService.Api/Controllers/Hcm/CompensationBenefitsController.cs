using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits.Commands;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/compensation-benefits")]
[Authorize]
public sealed class CompensationBenefitsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CompensationBenefitsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(CompensationBenefitsGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCompensationBenefitsReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(CompensationBenefitsGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCompensationBenefitsReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(CompensationBenefitsGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] CompensationBenefitsReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateCompensationBenefitsReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(CompensationBenefitsGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateCompensationBenefitsReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(CompensationBenefitsGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteCompensationBenefitsReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(CompensationBenefitsGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCompensationBenefitsAuditMetadataQuery(id), ct));
}
