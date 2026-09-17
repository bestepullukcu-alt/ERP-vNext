using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.OffboardingCases;
using Diten.HumanCapitalService.Application.Features.OffboardingCases.Commands;
using Diten.HumanCapitalService.Application.Features.OffboardingCases.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/offboarding-cases")]
[Authorize]
public sealed class OffboardingCasesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public OffboardingCasesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(OffboardingCaseGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOffboardingCaseListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(OffboardingCaseGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOffboardingCaseByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(OffboardingCaseGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] OffboardingCaseCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateOffboardingCaseCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(OffboardingCaseGuard.ManagePermission)]
    public async Task<IActionResult> Update(Guid id, [FromBody] OffboardingCaseUpdateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateOffboardingCaseCommand(id, request), ct));

    [HttpPost("{id:guid}/review")]
    [HasPermission(OffboardingCaseGuard.ReviewPermission)]
    public async Task<IActionResult> Review(Guid id, [FromBody] OffboardingCaseReviewRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ReviewOffboardingCaseCommand(id, request), ct));

    [HttpPost("{id:guid}/handoff")]
    [HasPermission(OffboardingCaseGuard.HandoffManagePermission)]
    public async Task<IActionResult> PlanHandoff(Guid id, [FromBody] OffboardingCaseHandoffRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new PlanOffboardingHandoffCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(OffboardingCaseGuard.ArchivePermission)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveOffboardingCaseCommand(id), ct));

    [HttpGet("health")]
    [HasPermission(OffboardingCaseGuard.ReadPermission)]
    public async Task<IActionResult> GetHealth(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOffboardingCaseHealthQuery(), ct));
}
