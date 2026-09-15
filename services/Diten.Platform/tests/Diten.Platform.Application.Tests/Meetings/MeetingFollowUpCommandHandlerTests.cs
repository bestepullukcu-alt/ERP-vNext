using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S7 (K6) — <see cref="ScheduleFollowUpMeetingHandler"/>. <see cref="CreateMeetingCommand"/> is answered
/// by a test-controlled fake mediator, isolated to THIS handler's own carry-forward/idempotency/cycle logic —
/// the SAME division of labour <c>MeetingTaskBridgeCommandHandlerTests</c> already draws for the S4 bridge's own
/// delegation to <c>CreateMeetingCommand</c>/<c>CreateTaskItemCommand</c>.
/// </summary>
public sealed class MeetingFollowUpCommandHandlerTests
{
    private static readonly Guid SourceMeetingId = Guid.Parse("55555555-0000-0000-0000-000000000001");
    private static readonly Guid TypeId = Guid.Parse("33333333-0000-0000-0000-000000000003");
    private const string CorrelationId = "corr-1";

    [Fact]
    public async Task Open_agenda_anchored_tasks_are_carried_forward_in_original_order_closed_ones_are_not()
    {
        var openTaskA = Guid.NewGuid();
        var closedTask = Guid.NewGuid();
        var openTaskB = Guid.NewGuid();
        var newMeetingId = Guid.NewGuid();
        var tasks = new FakeTaskItemRepository(
            MakeTask(openTaskA, "Açık A", TaskLifecycle.Open),
            MakeTask(closedTask, "Kapalı", TaskLifecycle.Done),
            MakeTask(openTaskB, "Açık B", TaskLifecycle.InProgress));

        var (handler, meetings, agendaItems, links, _) = Build(newMeetingId, tasks);
        SeedSourceMeeting(meetings);
        var linkA = await SeedAgendaLink(links, openTaskA);
        var linkClosed = await SeedAgendaLink(links, closedTask);
        var linkB = await SeedAgendaLink(links, openTaskB);
        await SeedAgendaItem(agendaItems, linkA.Id, sortOrder: 0);
        await SeedAgendaItem(agendaItems, linkClosed.Id, sortOrder: 1);
        await SeedAgendaItem(agendaItems, linkB.Id, sortOrder: 2);

        var result = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(
                SourceMeetingId,
                new ScheduleFollowUpMeetingRequest(
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "key-1"),
                CorrelationId),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.CarriedAgendaItemCount);

        var carried = (await agendaItems.ListByMeetingIdAsync(newMeetingId, CancellationToken.None))
            .Where(a => a.CarriedFromMeetingId == SourceMeetingId)
            .OrderBy(a => a.SortOrder)
            .ToList();
        Assert.Equal(2, carried.Count);
        Assert.Equal("Açık A", carried[0].Text);
        Assert.Equal(0, carried[0].SortOrder);
        Assert.Equal("Açık B", carried[1].Text);
        Assert.Equal(1, carried[1].SortOrder);
    }

    [Fact]
    public async Task No_agenda_anchored_open_tasks_still_creates_the_meeting_with_zero_carried()
    {
        var newMeetingId = Guid.NewGuid();
        var (handler, meetings, agendaItems, _, _) = Build(newMeetingId, new FakeTaskItemRepository());
        SeedSourceMeeting(meetings);

        var result = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(
                SourceMeetingId,
                new ScheduleFollowUpMeetingRequest(
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "key-2"),
                CorrelationId),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(newMeetingId, result.Data!.MeetingId);
        Assert.Equal(0, result.Data.CarriedAgendaItemCount);
        Assert.Empty(await agendaItems.ListByMeetingIdAsync(newMeetingId, CancellationToken.None));
    }

    [Fact]
    public async Task A_task_only_linked_as_preparation_with_no_agenda_line_is_not_carried()
    {
        var prepTask = Guid.NewGuid();
        var newMeetingId = Guid.NewGuid();
        var tasks = new FakeTaskItemRepository(MakeTask(prepTask, "Hazırlık", TaskLifecycle.Open));
        var (handler, meetings, _, links, _) = Build(newMeetingId, tasks);
        SeedSourceMeeting(meetings);
        // Linked, but NEVER via an AgendaItem — "preparation", not an agenda line.
        var linkService = new RecordLinkService(links, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
        await linkService.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, SourceMeetingId),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, prepTask),
            RecordLinkTypes.Preparation,
            ct: CancellationToken.None);

        var result = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(
                SourceMeetingId,
                new ScheduleFollowUpMeetingRequest(
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "key-3"),
                CorrelationId),
            CancellationToken.None);

        Assert.Equal(0, result.Data!.CarriedAgendaItemCount);
    }

    [Fact]
    public async Task A_dangling_agenda_link_whose_task_no_longer_resolves_is_silently_dropped()
    {
        var newMeetingId = Guid.NewGuid();
        var (handler, meetings, agendaItems, links, _) = Build(newMeetingId, new FakeTaskItemRepository());
        SeedSourceMeeting(meetings);
        var danglingTaskId = Guid.NewGuid();
        var link = await SeedAgendaLink(links, danglingTaskId);
        await SeedAgendaItem(agendaItems, link.Id, sortOrder: 0);

        var result = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(
                SourceMeetingId,
                new ScheduleFollowUpMeetingRequest(
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "key-4"),
                CorrelationId),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Data!.CarriedAgendaItemCount);
    }

    [Fact]
    public async Task The_source_meeting_that_does_not_exist_is_404_FollowUpNotFound()
    {
        var (handler, _, _, _, _) = Build(Guid.NewGuid(), new FakeTaskItemRepository());

        var result = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(
                Guid.NewGuid(),
                new ScheduleFollowUpMeetingRequest(
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "key-5"),
                CorrelationId),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.FollowUpNotFound, result.ReasonCode);
    }

    [Fact]
    public async Task A_business_key_collision_resolving_back_to_the_source_meeting_itself_is_400_SelfFollowUp()
    {
        // CreateMeetingCommand's OWN idempotency can hand back an EXISTING meeting (organizer+type+start+end+
        // title all matching) — including, in the worst case, the source meeting itself. Refused defensively.
        var (handler, meetings, _, links, _) = Build(SourceMeetingId, new FakeTaskItemRepository());
        SeedSourceMeeting(meetings);

        var result = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(
                SourceMeetingId,
                new ScheduleFollowUpMeetingRequest(
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "key-6"),
                CorrelationId),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.SelfFollowUp, result.ReasonCode);
        Assert.Empty(links.Items); // nothing written — no follow-up link, no carried agenda
    }

    [Fact]
    public async Task A_business_key_collision_resolving_into_an_already_cyclic_ancestry_is_400_SelfFollowUp()
    {
        // A pre-existing corrupt 2-cycle (only reachable via seeded/fake data, never a live endpoint — see the
        // handler's own doc comment on why FollowUpOfMeetingId is otherwise write-once). Scheduling a follow-up
        // of A that collides onto B — which already points back at A — must not be allowed to "succeed" silently.
        var meetingA = SourceMeetingId;
        var meetingB = Guid.NewGuid();
        var (handler, meetings, _, links, _) = Build(meetingB, new FakeTaskItemRepository());
        SeedSourceMeeting(meetings, id: meetingA, followUpOf: meetingB);
        meetings.Seed(new Meeting
        {
            Id = meetingB, TenantId = TaskTestData.Tenant, Title = "B", MeetingTypeId = TypeId,
            StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1),
            OrganizerUserId = TaskTestData.Me, FollowUpOfMeetingId = meetingA,
            IdempotencyKey = "meeting-b", CreatedBy = "test"
        });

        var result = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(
                meetingA,
                new ScheduleFollowUpMeetingRequest(
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "key-7"),
                CorrelationId),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.SelfFollowUp, result.ReasonCode);
        Assert.Empty(links.Items);
    }

    [Fact]
    public async Task K11_the_SAME_idempotency_key_resubmitted_returns_the_SAME_meeting_and_carries_no_second_time()
    {
        var openTask = Guid.NewGuid();
        var firstMeetingId = Guid.NewGuid();
        var tasks = new FakeTaskItemRepository(MakeTask(openTask, "Açık", TaskLifecycle.Open));
        var (handler, meetings, agendaItems, links, mediator) = Build(firstMeetingId, tasks);
        SeedSourceMeeting(meetings);
        var link = await SeedAgendaLink(links, openTask);
        await SeedAgendaItem(agendaItems, link.Id, sortOrder: 0);

        var request = new ScheduleFollowUpMeetingRequest(
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "same-key");

        var first = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(SourceMeetingId, request, CorrelationId), CancellationToken.None);
        mediator.CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(Guid.NewGuid()), 201, CorrelationId);
        var second = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(SourceMeetingId, request, CorrelationId), CancellationToken.None);

        Assert.Equal(first.Data!.MeetingId, second.Data!.MeetingId);
        Assert.Equal(1, second.Data.CarriedAgendaItemCount);
        Assert.Equal(1, mediator.CreateMeetingCallCount); // the SECOND call never even reaches CreateMeetingCommand
        Assert.Single(links.Items.Where(l => l.LinkType == RecordLinkTypes.FollowUp));
        Assert.Single(links.Items.Where(l => l.LinkType == RecordLinkTypes.Agenda && l.SourceRecordId == firstMeetingId));
    }

    [Fact]
    public async Task No_title_defaults_to_source_title_devam_and_type_organizer_inherit_from_source()
    {
        var newMeetingId = Guid.NewGuid();
        var (handler, meetings, _, _, mediator) = Build(newMeetingId, new FakeTaskItemRepository());
        SeedSourceMeeting(meetings);

        await handler.Handle(
            new ScheduleFollowUpMeetingCommand(
                SourceMeetingId,
                new ScheduleFollowUpMeetingRequest(
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "key-8"),
                CorrelationId),
            CancellationToken.None);

        Assert.Equal("Kaynak Toplantı (devam)", mediator.LastCreateMeetingRequest!.Title);
        Assert.Equal(TypeId, mediator.LastCreateMeetingRequest.MeetingTypeId);
        Assert.Equal(TaskTestData.Me, mediator.LastCreateMeetingRequest.OrganizerUserId);
        Assert.Equal(SourceMeetingId, mediator.LastCreateMeetingRequest.FollowUpOfMeetingId);
    }

    // ── builders ─────────────────────────────────────────────────────────────────────────────────────────────

    private static void SeedSourceMeeting(FakeMeetingRepository meetings, Guid? id = null, Guid? followUpOf = null)
        => meetings.Seed(new Meeting
        {
            Id = id ?? SourceMeetingId, TenantId = TaskTestData.Tenant, Title = "Kaynak Toplantı", MeetingTypeId = TypeId,
            StartAt = DateTimeOffset.UtcNow.AddDays(-7), EndAt = DateTimeOffset.UtcNow.AddDays(-7).AddHours(1),
            OrganizerUserId = TaskTestData.Me, FollowUpOfMeetingId = followUpOf,
            IdempotencyKey = "source-key", CreatedBy = "test"
        });

    /// <summary>
    /// CT 2026-09-12 — the carry-forward a management review exists for: an action the meeting itself produced
    /// (bornFromMeeting) has NO agenda line of its own, and the first reading of K6 dropped it. It gets a line in
    /// the follow-up, after the anchored ones; a closed one still does not.
    /// </summary>
    [Fact]
    public async Task An_open_task_the_meeting_PRODUCED_is_carried_even_though_it_has_no_agenda_line()
    {
        var anchored = Guid.NewGuid();
        var decisionBorn = Guid.NewGuid();
        var closedDecisionBorn = Guid.NewGuid();
        var newMeetingId = Guid.NewGuid();
        var tasks = new FakeTaskItemRepository(
            MakeTask(anchored, "Gundemdeki acik is", TaskLifecycle.Open),
            MakeTask(decisionBorn, "Karardan dogan acik is", TaskLifecycle.InProgress),
            MakeTask(closedDecisionBorn, "Karardan dogan kapali is", TaskLifecycle.Done));
        var (handler, meetings, agendaItems, links, _) = Build(newMeetingId, tasks);
        SeedSourceMeeting(meetings);

        var anchoredLink = await SeedAgendaLink(links, anchored);
        await SeedAgendaItem(agendaItems, anchoredLink.Id, sortOrder: 0);
        await SeedProducedLink(links, decisionBorn);
        await SeedProducedLink(links, closedDecisionBorn);

        var result = await handler.Handle(
            new ScheduleFollowUpMeetingCommand(
                SourceMeetingId,
                new ScheduleFollowUpMeetingRequest(
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, null, null, null, null, null, "key-produced"),
                CorrelationId),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.CarriedAgendaItemCount);
        var carried = (await agendaItems.ListByMeetingIdAsync(newMeetingId))
            .OrderBy(a => a.SortOrder)
            .Select(a => a.Text)
            .ToList();
        Assert.Equal(["Gundemdeki acik is", "Karardan dogan acik is"], carried);
    }

    private static Task<RecordLink> SeedAgendaLink(FakeRecordLinkRepository links, Guid taskId)
    {
        var linkService = new RecordLinkService(links, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
        return linkService.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, SourceMeetingId),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, taskId),
            RecordLinkTypes.Agenda,
            ct: CancellationToken.None);
    }

    /// <summary>A task the meeting PRODUCED: linked, but with no agenda line of its own.</summary>
    private static Task<RecordLink> SeedProducedLink(FakeRecordLinkRepository links, Guid taskId)
    {
        var linkService = new RecordLinkService(links, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
        return linkService.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, SourceMeetingId),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, taskId),
            RecordLinkTypes.BornFromMeeting,
            ct: CancellationToken.None);
    }

    private static Task<AgendaItem> SeedAgendaItem(FakeAgendaItemRepository agendaItems, Guid recordLinkId, int sortOrder)
        => agendaItems.CreateAsync(new AgendaItem
        {
            TenantId = TaskTestData.Tenant, MeetingId = SourceMeetingId, Text = "eski metin",
            SortOrder = sortOrder, RecordLinkId = recordLinkId, CreatedBy = "test"
        }, CancellationToken.None);

    private static TaskItem MakeTask(Guid id, string title, TaskLifecycle lifecycle) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Title = title,
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = lifecycle
    };

    private static MeetingDto MakeMeetingDto(Guid id) => new(
        Id: id, Title: "t", MeetingTypeId: TypeId, MeetingTypeName: "Tür",
        StartAt: DateTimeOffset.UtcNow, EndAt: DateTimeOffset.UtcNow.AddHours(1), Location: null,
        OrganizerUserId: TaskTestData.Me, Description: null, FollowUpOfMeetingId: null,
        Lifecycle: Domain.Enums.Meetings.MeetingLifecycle.Scheduled, CancellationReason: null, Version: 1,
        Attendees: [], AgendaItems: []);

    private static (ScheduleFollowUpMeetingHandler Handler, FakeMeetingRepository Meetings, FakeAgendaItemRepository AgendaItems, FakeRecordLinkRepository Links, FollowUpMediator Mediator)
        Build(Guid newMeetingId, FakeTaskItemRepository tasks)
    {
        var meetings = new FakeMeetingRepository { Tenant = TaskTestData.Tenant };
        var agendaItems = new FakeAgendaItemRepository { Tenant = TaskTestData.Tenant };
        var linksRepo = new FakeRecordLinkRepository();
        var linkService = new RecordLinkService(linksRepo, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
        var mediator = new FollowUpMediator
        {
            Meetings = meetings,
            CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(newMeetingId), 201, CorrelationId)
        };
        var handler = new ScheduleFollowUpMeetingHandler(
            meetings, agendaItems, tasks, linkService, new FakeTenantContext(TaskTestData.Tenant),
            new MeetingIdempotencyKeyResolver(), new FakeCurrentUserContext(TaskTestData.Me), mediator);
        return (handler, meetings, agendaItems, linksRepo, mediator);
    }

    /// <summary>Answers ONLY <see cref="CreateMeetingCommand"/>, test-controlled — same isolation the S4 bridge's
    /// own <c>BridgeMediator</c> already establishes.</summary>
    private sealed class FollowUpMediator : IMediator
    {
        public required FakeMeetingRepository Meetings { get; init; }
        public Response<MeetingDto>? CreateMeetingResult { get; set; }
        public CreateMeetingRequest? LastCreateMeetingRequest { get; private set; }
        public int CreateMeetingCallCount { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is CreateMeetingCommand createMeeting)
            {
                CreateMeetingCallCount++;
                LastCreateMeetingRequest = createMeeting.Request;

                // LIVE-MEASURED BUG (fixed in ScheduleFollowUpMeetingHandler.CreatesCycleAsync): the real
                // CreateMeetingHandler PERSISTS the new row with FollowUpOfMeetingId already set — a fake that
                // only returns a DTO without also writing that row left this exact false-positive invisible to
                // every unit test, since CreatesCycleAsync's own `_meetings.GetByIdAsync` found nothing at all.
                // Mirroring the real write here is what makes these tests able to catch it.
                var dto = CreateMeetingResult?.Data;
                if (dto is not null && Meetings.GetByIdAsync(dto.Id, ct).GetAwaiter().GetResult() is null)
                {
                    Meetings.Seed(new Meeting
                    {
                        Id = dto.Id, TenantId = TaskTestData.Tenant, Title = createMeeting.Request.Title,
                        MeetingTypeId = createMeeting.Request.MeetingTypeId,
                        StartAt = createMeeting.Request.StartAt, EndAt = createMeeting.Request.EndAt,
                        OrganizerUserId = createMeeting.Request.OrganizerUserId ?? TaskTestData.Me,
                        FollowUpOfMeetingId = createMeeting.Request.FollowUpOfMeetingId,
                        IdempotencyKey = $"seeded-{dto.Id:N}", CreatedBy = "test"
                    });
                }

                return (Task<TResponse>)(object)Task.FromResult(CreateMeetingResult!);
            }

            throw new NotSupportedException($"FollowUpMediator does not support {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
