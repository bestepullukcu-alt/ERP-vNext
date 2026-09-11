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

/// <summary>
/// MOD-0357 S2 — thin Meetings controller, mirroring <c>TasksController</c>'s own shape exactly. Version-explicit
/// route under <c>api/v1/meetings</c>; gateway routing is a separate integration-agent task (pack §15 — no
/// route exists in <c>ocelot.json</c> today, so the proxy returns 503 until that lands).
/// </summary>
[ApiController]
[Route("api/v1/meetings")]
[Authorize]
public sealed class MeetingsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public MeetingsController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;

    [HttpPost]
    [HasPermission(MeetingPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateMeetingRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CreateMeetingCommand(request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet]
    [HasPermission(MeetingPermissions.Read)]
    public async Task<IActionResult> GetList([FromQuery] GetMeetingListFilter filter, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMeetingListQuery(filter, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(MeetingPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMeetingByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(MeetingPermissions.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMeetingRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateMeetingCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(MeetingPermissions.Update)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelMeetingRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CancelMeetingCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/reassign-organizer")]
    [HasPermission(MeetingPermissions.Update)]
    public async Task<IActionResult> ReassignOrganizer(
        Guid id, [FromBody] ReassignMeetingOrganizerRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReassignMeetingOrganizerCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/attendees")]
    [HasPermission(MeetingPermissions.Update)]
    public async Task<IActionResult> AddAttendees(
        Guid id, [FromBody] AddMeetingAttendeesRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AddMeetingAttendeesCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}/attendees/{userId:guid}")]
    [HasPermission(MeetingPermissions.Update)]
    public async Task<IActionResult> RemoveAttendee(Guid id, Guid userId, CancellationToken ct)
    {
        var response = await _mediator.Send(new RemoveMeetingAttendeeCommand(id, userId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/agenda")]
    [HasPermission(MeetingPermissions.Update)]
    public async Task<IActionResult> AddAgendaItem(Guid id, [FromBody] AddAgendaItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AddAgendaItemCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}/agenda/order")]
    [HasPermission(MeetingPermissions.Update)]
    public async Task<IActionResult> ReorderAgenda(Guid id, [FromBody] ReorderAgendaRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReorderAgendaCommand(id, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}/agenda/{itemId:guid}")]
    [HasPermission(MeetingPermissions.Update)]
    public async Task<IActionResult> UpdateAgendaItem(
        Guid id, Guid itemId, [FromBody] UpdateAgendaItemRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateAgendaItemCommand(id, itemId, request, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}/agenda/{itemId:guid}")]
    [HasPermission(MeetingPermissions.Update)]
    public async Task<IActionResult> DeleteAgendaItem(Guid id, Guid itemId, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteAgendaItemCommand(id, itemId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}/tasks")]
    [HasPermission(MeetingPermissions.Read)]
    public async Task<IActionResult> GetLinkedTasks(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetLinkedTasksQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // ── S3 lookups — "lookups" never matches {id:guid}, same disambiguation MeetingTypesController's own
    // "types" sub-route already relies on. ──────────────────────────────────────────────────────────────────

    /// <summary>The attendee picker (D2, scope-exempt) — Create needs it to invite anyone at all.</summary>
    [HttpGet("lookups/attendees")]
    [HasPermission(MeetingPermissions.Create)]
    public async Task<IActionResult> LookupAttendees(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMeetingAttendeeLookupQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    /// <summary>The type dropdown — gated on Read (not TypesManage): choosing a type is not managing the catalogue.</summary>
    [HttpGet("lookups/types")]
    [HasPermission(MeetingPermissions.Read)]
    public async Task<IActionResult> LookupTypes(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetMeetingTypeLookupQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }
}
