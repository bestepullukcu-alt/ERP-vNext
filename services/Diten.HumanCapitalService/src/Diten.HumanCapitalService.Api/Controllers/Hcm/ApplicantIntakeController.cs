using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake.Commands;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/applicant-intake")]
[Authorize]
public sealed class ApplicantIntakeController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ApplicantIntakeController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(ApplicantIntakeGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetApplicantIntakeReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(ApplicantIntakeGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetApplicantIntakeReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(ApplicantIntakeGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] ApplicantIntakeCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateApplicantIntakeReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(ApplicantIntakeGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateApplicantIntakeReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(ApplicantIntakeGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteApplicantIntakeReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(ApplicantIntakeGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetApplicantIntakeAuditMetadataQuery(id), ct));
}
