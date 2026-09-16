using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement.Commands;
using Diten.HumanCapitalService.Application.Features.HrCaseManagement.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/hr-case-management")]
[Authorize]
public sealed class HrCaseManagementController : CustomBaseController
{
    private readonly IMediator _mediator;

    public HrCaseManagementController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(HrCaseManagementGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrCaseManagementReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(HrCaseManagementGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrCaseManagementReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(HrCaseManagementGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] HrCaseManagementReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateHrCaseManagementReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(HrCaseManagementGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateHrCaseManagementReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(HrCaseManagementGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteHrCaseManagementReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(HrCaseManagementGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrCaseManagementAuditMetadataQuery(id), ct));
}
