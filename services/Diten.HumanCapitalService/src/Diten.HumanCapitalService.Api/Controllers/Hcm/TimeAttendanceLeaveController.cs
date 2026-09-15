using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave;
using Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Commands;
using Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/time-attendance-leave")]
[Authorize]
public sealed class TimeAttendanceLeaveController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TimeAttendanceLeaveController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(TimeAttendanceLeaveGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceLeaveReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(TimeAttendanceLeaveGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceLeaveReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(TimeAttendanceLeaveGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] TimeAttendanceLeaveReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateTimeAttendanceLeaveReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(TimeAttendanceLeaveGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateTimeAttendanceLeaveReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(TimeAttendanceLeaveGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteTimeAttendanceLeaveReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(TimeAttendanceLeaveGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTimeAttendanceLeaveAuditMetadataQuery(id), ct));
}
