using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges.Commands;
using Diten.HumanCapitalService.Application.Features.EmploymentChanges.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/employment-changes")]
[Authorize]
public sealed class EmploymentChangesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public EmploymentChangesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(EmploymentChangeGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEmploymentChangeReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(EmploymentChangeGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEmploymentChangeReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(EmploymentChangeGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] EmploymentChangeReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateEmploymentChangeReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(EmploymentChangeGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateEmploymentChangeReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(EmploymentChangeGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteEmploymentChangeReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(EmploymentChangeGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEmploymentChangeAuditMetadataQuery(id), ct));
}
