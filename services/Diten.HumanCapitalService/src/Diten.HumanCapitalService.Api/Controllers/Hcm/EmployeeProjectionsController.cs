using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections.Commands;
using Diten.HumanCapitalService.Application.Features.EmployeeProjections.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/employee-projections")]
[Authorize]
public sealed class EmployeeProjectionsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public EmployeeProjectionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission("hcm.employee-projections.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEmployeeProjectionListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission("hcm.employee-projections.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEmployeeProjectionByIdQuery(id), ct));

    [HttpPost]
    [HasPermission("hcm.employee-projections.manage")]
    public async Task<IActionResult> Create([FromBody] EmployeeProjectionCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateEmployeeProjectionCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission("hcm.employee-projections.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EmployeeProjectionUpdateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateEmployeeProjectionCommand(id, request), ct));

    [HttpPost("{id:guid}/source-link")]
    [HasPermission("hcm.employee-projections.source-link.manage")]
    public async Task<IActionResult> UpdateSourceLink(Guid id, [FromBody] EmployeeProjectionUpdateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateEmployeeProjectionCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission("hcm.employee-projections.archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveEmployeeProjectionCommand(id), ct));

    [HttpGet("health")]
    [HasPermission("hcm.employee-projections.read")]
    public async Task<IActionResult> GetHealth(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEmployeeProjectionHealthQuery(), ct));
}
