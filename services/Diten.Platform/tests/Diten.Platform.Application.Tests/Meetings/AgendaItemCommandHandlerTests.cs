using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

public sealed class AgendaItemCommandHandlerTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;

    private static (FakeMeetingRepository Meetings, FakeAgendaItemRepository Agenda, FakeTenantContext TenantContext, FakeCurrentUserContext CurrentUser, Meeting Meeting) Fixture()
    {
        var meetings = new FakeMeetingRepository { Tenant = Tenant };
        var agenda = new FakeAgendaItemRepository { Tenant = Tenant };
        var meeting = new Meeting
        {
            TenantId = Tenant, Title = "T", MeetingTypeId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
            OrganizerUserId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid().ToString()
        };
        meetings.Seed(meeting);
        return (meetings, agenda, new FakeTenantContext(Tenant), new FakeCurrentUserContext(Guid.NewGuid()), meeting);
    }

    [Fact]
    public async Task Add_assigns_SortOrder_after_the_current_last_item()
    {
        var (meetings, agenda, tenantContext, currentUser, meeting) = Fixture();
        var handler = new AddAgendaItemHandler(meetings, agenda, tenantContext, currentUser);

        var first = await handler.Handle(new AddAgendaItemCommand(meeting.Id, new AddAgendaItemRequest("İlk madde"), "corr"), CancellationToken.None);
        var second = await handler.Handle(new AddAgendaItemCommand(meeting.Id, new AddAgendaItemRequest("İkinci madde"), "corr"), CancellationToken.None);

        Assert.Equal(0, first.Data!.SortOrder);
        Assert.Equal(1, second.Data!.SortOrder);
    }

    // ── AC7 — reorder requires the EXACT live set ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reorder_with_a_missing_id_is_400_MEETING_AGENDA_REORDER_MISMATCH()
    {
        var (meetings, agenda, tenantContext, currentUser, meeting) = Fixture();
        var addHandler = new AddAgendaItemHandler(meetings, agenda, tenantContext, currentUser);
        var a = await addHandler.Handle(new AddAgendaItemCommand(meeting.Id, new AddAgendaItemRequest("A"), "corr"), CancellationToken.None);
        await addHandler.Handle(new AddAgendaItemCommand(meeting.Id, new AddAgendaItemRequest("B"), "corr"), CancellationToken.None);

        var reorderHandler = new ReorderAgendaHandler(meetings, agenda);
        var response = await reorderHandler.Handle(
            new ReorderAgendaCommand(meeting.Id, new ReorderAgendaRequest([a.Data!.Id]), "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.AgendaReorderMismatch, response.ReasonCode);
    }

    [Fact]
    public async Task Reorder_with_the_exact_live_set_reversed_flips_the_SortOrder()
    {
        var (meetings, agenda, tenantContext, currentUser, meeting) = Fixture();
        var addHandler = new AddAgendaItemHandler(meetings, agenda, tenantContext, currentUser);
        var a = await addHandler.Handle(new AddAgendaItemCommand(meeting.Id, new AddAgendaItemRequest("A"), "corr"), CancellationToken.None);
        var b = await addHandler.Handle(new AddAgendaItemCommand(meeting.Id, new AddAgendaItemRequest("B"), "corr"), CancellationToken.None);

        var reorderHandler = new ReorderAgendaHandler(meetings, agenda);
        var response = await reorderHandler.Handle(
            new ReorderAgendaCommand(meeting.Id, new ReorderAgendaRequest([b.Data!.Id, a.Data!.Id]), "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var items = await agenda.ListByMeetingIdAsync(meeting.Id);
        Assert.Equal(b.Data.Id, items[0].Id);
        Assert.Equal(a.Data.Id, items[1].Id);
    }

    [Fact]
    public async Task UpdateAgendaItem_with_a_stale_ExpectedVersion_is_409_MEETING_CONCURRENCY_CONFLICT()
    {
        var (meetings, agenda, tenantContext, currentUser, meeting) = Fixture();
        var addHandler = new AddAgendaItemHandler(meetings, agenda, tenantContext, currentUser);
        var item = await addHandler.Handle(new AddAgendaItemCommand(meeting.Id, new AddAgendaItemRequest("A"), "corr"), CancellationToken.None);

        var updateHandler = new UpdateAgendaItemHandler(meetings, agenda);
        var response = await updateHandler.Handle(
            new UpdateAgendaItemCommand(meeting.Id, item.Data!.Id, new UpdateAgendaItemRequest("B", item.Data.Version + 5), "corr"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(MeetingReasonCodes.ConcurrencyConflict, response.ReasonCode);
    }

    [Fact]
    public async Task DeleteAgendaItem_removes_it_from_the_live_list()
    {
        var (meetings, agenda, tenantContext, currentUser, meeting) = Fixture();
        var addHandler = new AddAgendaItemHandler(meetings, agenda, tenantContext, currentUser);
        var item = await addHandler.Handle(new AddAgendaItemCommand(meeting.Id, new AddAgendaItemRequest("A"), "corr"), CancellationToken.None);

        var deleteHandler = new DeleteAgendaItemHandler(meetings, agenda);
        var response = await deleteHandler.Handle(new DeleteAgendaItemCommand(meeting.Id, item.Data!.Id, "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(await agenda.ListByMeetingIdAsync(meeting.Id));
    }
}
