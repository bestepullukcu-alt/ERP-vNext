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

/// <summary>MOD-0357 S11 — the recurring meeting-series setting, gated by <c>series-manage</c> alone. Mirrors
/// <c>MeetingTypesController</c>'s own shape exactly (S8).</summary>
[ApiController]
[Route("api/v1/meetings/series")]
[Authorize]
public sealed class MeetingSeriesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public MeetingSeriesController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;

    [HttpGet]
    [HasPermission(MeetingPermissions.SeriesManage)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMeetingSeriesListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(MeetingPermissions.SeriesManage)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMeetingSeriesByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission(MeetingPermissions.SeriesManage)]
    public async Task<IActionResult> Create([FromBody] CreateMeetingSeriesRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateMeetingSeriesCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(MeetingPermissions.SeriesManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMeetingSeriesRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateMeetingSeriesCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(MeetingPermissions.SeriesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteMeetingSeriesCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }
}
