using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;

public sealed class AddAgendaItemHandler : IRequestHandler<AddAgendaItemCommand, Response<AgendaItemDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IAgendaItemRepository _agendaItems;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public AddAgendaItemHandler(
        IMeetingRepository meetings,
        IAgendaItemRepository agendaItems,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _meetings = meetings;
        _agendaItems = agendaItems;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<AgendaItemDto>> Handle(AddAgendaItemCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<AgendaItemDto>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        var editable = UpdateMeetingHandler.CheckEditable(meeting, command.CorrelationId);
        if (editable is not null)
        {
            return Response<AgendaItemDto>.Fail(editable.Errors, editable.StatusCode, editable.ReasonCode, command.CorrelationId);
        }

        var existing = await _agendaItems.ListByMeetingIdAsync(command.MeetingId, ct);
        var nextSortOrder = existing.Count == 0 ? 0 : existing.Max(x => x.SortOrder) + 1;

        var item = await _agendaItems.CreateAsync(new AgendaItem
        {
            TenantId = _tenantContext.TenantId,
            MeetingId = command.MeetingId,
            Text = command.Request.Text.Trim(),
            SortOrder = nextSortOrder,
            CreatedBy = _currentUser.ActorName
        }, ct);

        return Response<AgendaItemDto>.Success(
            new AgendaItemDto(item.Id, item.Text, item.SortOrder, item.Version, item.RecordLinkId), 201, command.CorrelationId);
    }
}

public sealed class UpdateAgendaItemHandler : IRequestHandler<UpdateAgendaItemCommand, Response<NoContent>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IAgendaItemRepository _agendaItems;

    public UpdateAgendaItemHandler(IMeetingRepository meetings, IAgendaItemRepository agendaItems)
    {
        _meetings = meetings;
        _agendaItems = agendaItems;
    }

    public async Task<Response<NoContent>> Handle(UpdateAgendaItemCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<NoContent>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        var editable = UpdateMeetingHandler.CheckEditable(meeting, command.CorrelationId);
        if (editable is not null)
        {
            return editable;
        }

        var item = await _agendaItems.GetByIdAsync(command.AgendaItemId, ct);
        if (item is null || item.MeetingId != command.MeetingId)
        {
            return Response<NoContent>.Fail("The agenda item does not exist.", 404, MeetingReasonCodes.AgendaItemNotFound, command.CorrelationId);
        }

        item.Text = command.Request.Text.Trim();

        if (!await _agendaItems.UpdateAsync(item, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The agenda item changed meanwhile; reload and retry.", 409, MeetingReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}

public sealed class DeleteAgendaItemHandler : IRequestHandler<DeleteAgendaItemCommand, Response<NoContent>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IAgendaItemRepository _agendaItems;

    public DeleteAgendaItemHandler(IMeetingRepository meetings, IAgendaItemRepository agendaItems)
    {
        _meetings = meetings;
        _agendaItems = agendaItems;
    }

    public async Task<Response<NoContent>> Handle(DeleteAgendaItemCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<NoContent>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        var editable = UpdateMeetingHandler.CheckEditable(meeting, command.CorrelationId);
        if (editable is not null)
        {
            return editable;
        }

        var item = await _agendaItems.GetByIdAsync(command.AgendaItemId, ct);
        if (item is null || item.MeetingId != command.MeetingId)
        {
            return Response<NoContent>.Fail("The agenda item does not exist.", 404, MeetingReasonCodes.AgendaItemNotFound, command.CorrelationId);
        }

        await _agendaItems.DeleteAsync(item.Id, ct);
        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}

public sealed class ReorderAgendaHandler : IRequestHandler<ReorderAgendaCommand, Response<NoContent>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IAgendaItemRepository _agendaItems;

    public ReorderAgendaHandler(IMeetingRepository meetings, IAgendaItemRepository agendaItems)
    {
        _meetings = meetings;
        _agendaItems = agendaItems;
    }

    public async Task<Response<NoContent>> Handle(ReorderAgendaCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<NoContent>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        var editable = UpdateMeetingHandler.CheckEditable(meeting, command.CorrelationId);
        if (editable is not null)
        {
            return editable;
        }

        var existing = await _agendaItems.ListByMeetingIdAsync(command.MeetingId, ct);
        var existingIds = existing.Select(x => x.Id).ToHashSet();
        var requestedIds = command.Request.OrderedAgendaItemIds;

        // AC7 — the reorder request must name EXACTLY the live set: no missing id, no extra id, no duplicate.
        if (requestedIds.Count != existingIds.Count
            || requestedIds.Distinct().Count() != requestedIds.Count
            || !requestedIds.All(existingIds.Contains))
        {
            return Response<NoContent>.Fail(
                "The reorder request must list every current agenda item exactly once.",
                400, MeetingReasonCodes.AgendaReorderMismatch, command.CorrelationId);
        }

        var byId = existing.ToDictionary(x => x.Id);
        for (var i = 0; i < requestedIds.Count; i++)
        {
            var item = byId[requestedIds[i]];
            if (item.SortOrder == i)
            {
                continue;
            }

            item.SortOrder = i;
            await _agendaItems.UpdateAsync(item, item.Version, ct);
        }

        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}
