using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>MOD-0357 S2 — the meeting-type setting (pack §3/§14, gated by <c>types-manage</c> alone).</summary>
[ApiController]
[Route("api/v1/meetings/types")]
[Authorize]
public sealed class MeetingTypesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public MeetingTypesController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;

    [HttpGet]
    [HasPermission(MeetingPermissions.TypesManage)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMeetingTypeListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(MeetingPermissions.TypesManage)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMeetingTypeByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission(MeetingPermissions.TypesManage)]
    public async Task<IActionResult> Create([FromBody] CreateMeetingTypeRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateMeetingTypeCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(MeetingPermissions.TypesManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMeetingTypeRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateMeetingTypeCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(MeetingPermissions.TypesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteMeetingTypeCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }
}
