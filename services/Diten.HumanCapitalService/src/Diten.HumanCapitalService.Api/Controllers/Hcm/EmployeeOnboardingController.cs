using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Commands;
using Diten.HumanCapitalService.Application.Features.EmployeeOnboarding.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/employee-onboarding")]
[Authorize]
public sealed class EmployeeOnboardingController : CustomBaseController
{
    private readonly IMediator _mediator;

    public EmployeeOnboardingController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(EmployeeOnboardingGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEmployeeOnboardingReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(EmployeeOnboardingGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEmployeeOnboardingReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(EmployeeOnboardingGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] EmployeeOnboardingReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateEmployeeOnboardingReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(EmployeeOnboardingGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateEmployeeOnboardingReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(EmployeeOnboardingGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteEmployeeOnboardingReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(EmployeeOnboardingGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEmployeeOnboardingAuditMetadataQuery(id), ct));
}
