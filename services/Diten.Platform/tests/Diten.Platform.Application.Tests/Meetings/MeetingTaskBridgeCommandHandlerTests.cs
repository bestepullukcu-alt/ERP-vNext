using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S4 — the three bridge handlers. <see cref="CreateTaskItemCommand"/>/<see cref="CreateMeetingCommand"/>
/// are answered by a test-controlled fake mediator (isolated to THIS module's own logic — K2 and the pack's own
/// "no second create path" is proven by inspecting exactly what request this module builds and sends, not by
/// re-running MOD-0024's own assignment-guard suite, which already proves the guard itself elsewhere).
/// </summary>
public sealed class MeetingTaskBridgeCommandHandlerTests
{
    private static readonly Guid MeetingId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid TaskId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid TypeId = Guid.Parse("33333333-0000-0000-0000-000000000003");
    private static readonly Guid AgendaItemId = Guid.Parse("44444444-0000-0000-0000-000000000004");
    private const string CorrelationId = "corr-1";

    // ── K1/K2/K11 — CreateTaskFromMeeting ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Creates_a_task_via_the_ordinary_command_and_writes_ONE_link()
    {
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var (handler, links, agendaItems, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(1)); // future meeting → "preparation"

        var result = await handler.Handle(
            new CreateTaskFromMeetingCommand(
                MeetingId,
                new CreateTaskFromMeetingRequest("Bir görev", null, TaskTestData.Me, null, null, null, "key-1"),
                CorrelationId),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskId, result.Data!.TaskId);
        var link = Assert.Single(links.Items);
        Assert.Equal(RecordLinkModuleCodes.Meetings, link.SourceModuleCode);
        Assert.Equal(MeetingId, link.SourceRecordId);
        Assert.Equal(RecordLinkModuleCodes.Tasks, link.TargetModuleCode);
        Assert.Equal(TaskId, link.TargetRecordId);
        Assert.Equal(RecordLinkTypes.Preparation, link.LinkType);
        Assert.NotNull(mediator.LastCreateTaskRequest);
        Assert.Equal("Bir görev", mediator.LastCreateTaskRequest!.Title);
        Assert.Equal(TaskTestData.Me, mediator.LastCreateTaskRequest.AssigneeUserId);
    }

    [Fact]
    public async Task A_meeting_already_started_links_as_bornFromMeeting_not_preparation()
    {
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var (handler, links, _, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(-1)); // already started

        await handler.Handle(
            new CreateTaskFromMeetingCommand(
                MeetingId,
                new CreateTaskFromMeetingRequest("Bir görev", null, null, null, null, null, "key-2"),
                CorrelationId),
            CancellationToken.None);

        Assert.Equal(RecordLinkTypes.BornFromMeeting, Assert.Single(links.Items).LinkType);
    }

    [Fact]
    public async Task No_assignee_creates_a_SelfAssigned_task_not_a_second_shape()
    {
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var (handler, _, _, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(1));

        await handler.Handle(
            new CreateTaskFromMeetingCommand(
                MeetingId,
                new CreateTaskFromMeetingRequest("Bir görev", null, null, null, null, null, "key-3"),
                CorrelationId),
            CancellationToken.None);

        Assert.Equal(TaskAssignmentTarget.SelfAssigned, mediator.LastCreateTaskRequest!.AssignmentTarget);
        Assert.Null(mediator.LastCreateTaskRequest.AssigneeUserId);
    }

    [Fact]
    public async Task No_TaskTypeId_falls_back_to_the_meetings_own_type_default()
    {
        var defaultTaskTypeId = Guid.NewGuid();
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var meetingType = new MeetingType
        {
            Id = TypeId, TenantId = TaskTestData.Tenant, Name = "Tür",
            DefaultActionTaskTypeId = defaultTaskTypeId, CreatedBy = "test"
        };
        var (handler, _, _, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(1), meetingType);

        await handler.Handle(
            new CreateTaskFromMeetingCommand(
                MeetingId,
                new CreateTaskFromMeetingRequest("Bir görev", null, null, null, null, TaskTypeId: null, "key-4"),
                CorrelationId),
            CancellationToken.None);

        Assert.Equal(defaultTaskTypeId, mediator.LastCreateTaskRequest!.TaskTypeId);
    }

    [Fact]
    public async Task An_explicit_TaskTypeId_overrides_the_meetings_own_default()
    {
        var explicitTypeId = Guid.NewGuid();
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var meetingType = new MeetingType
        {
            Id = TypeId, TenantId = TaskTestData.Tenant, Name = "Tür",
            DefaultActionTaskTypeId = Guid.NewGuid(), CreatedBy = "test"
        };
        var (handler, _, _, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(1), meetingType);

        await handler.Handle(
            new CreateTaskFromMeetingCommand(
                MeetingId,
                new CreateTaskFromMeetingRequest("Bir görev", null, null, null, null, explicitTypeId, "key-5"),
                CorrelationId),
            CancellationToken.None);

        Assert.Equal(explicitTypeId, mediator.LastCreateTaskRequest!.TaskTypeId);
    }

    [Fact]
    public async Task An_AgendaItemId_makes_the_agenda_row_carry_the_new_links_id()
    {
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var (handler, links, agendaItems, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(1));
        await agendaItems.CreateAsync(new AgendaItem
        {
            Id = AgendaItemId, TenantId = TaskTestData.Tenant, MeetingId = MeetingId,
            Text = "Satır", SortOrder = 1, CreatedBy = "test"
        });

        var result = await handler.Handle(
            new CreateTaskFromMeetingCommand(
                MeetingId,
                new CreateTaskFromMeetingRequest("Bir görev", null, null, null, AgendaItemId, null, "key-6"),
                CorrelationId),
            CancellationToken.None);

        var link = Assert.Single(links.Items);
        var stored = await agendaItems.GetByIdAsync(AgendaItemId, CancellationToken.None);
        Assert.Equal(link.Id, stored!.RecordLinkId);
        Assert.Equal(AgendaItemId, result.Data!.AgendaItemId);
    }

    [Fact]
    public async Task An_unknown_AgendaItemId_is_404_and_creates_no_task_and_no_link()
    {
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var (handler, links, _, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(1));

        var result = await handler.Handle(
            new CreateTaskFromMeetingCommand(
                MeetingId,
                new CreateTaskFromMeetingRequest("Bir görev", null, null, null, Guid.NewGuid(), null, "key-7"),
                CorrelationId),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(MeetingReasonCodes.AgendaItemNotFound, result.ReasonCode);
        Assert.Empty(links.Items);
        Assert.False(mediator.CreateTaskWasSent);
    }

    [Fact]
    public async Task An_unknown_meeting_is_404_and_never_calls_the_task_engine()
    {
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var (handler, _, _, _) = Build(mediator, meetingStartAt: null); // meeting never seeded

        var result = await handler.Handle(
            new CreateTaskFromMeetingCommand(
                MeetingId,
                new CreateTaskFromMeetingRequest("Bir görev", null, null, null, null, null, "key-8"),
                CorrelationId),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.False(mediator.CreateTaskWasSent);
    }

    [Fact]
    public async Task A_refused_task_create_propagates_the_SAME_reason_code_untouched()
    {
        // K2 — "aynı reason code'lar, no new ones invented for this path".
        var mediator = new BridgeMediator
        {
            CreateTaskResult = Response<Guid>.Fail(
                "The assignee is outside the eligible scope.", 400, TaskReasonCodes.AssigneeNotAssignable, CorrelationId)
        };
        var (handler, links, _, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(1));

        var result = await handler.Handle(
            new CreateTaskFromMeetingCommand(
                MeetingId,
                new CreateTaskFromMeetingRequest("Bir görev", null, TaskTestData.Rival, null, null, null, "key-9"),
                CorrelationId),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.AssigneeNotAssignable, result.ReasonCode);
        Assert.Empty(links.Items); // no link written when the task itself never was
    }

    [Fact]
    public async Task K11_a_resubmitted_request_with_the_SAME_idempotency_key_returns_the_SAME_task_and_writes_no_second_link()
    {
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var (handler, links, _, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(1));
        var request = new CreateTaskFromMeetingRequest("Bir görev", null, TaskTestData.Me, null, null, null, "same-key");

        var first = await handler.Handle(new CreateTaskFromMeetingCommand(MeetingId, request, CorrelationId), CancellationToken.None);
        mediator.CreateTaskResult = Response<Guid>.Success(Guid.NewGuid(), 201, CorrelationId); // would be a DIFFERENT task if called again
        var second = await handler.Handle(new CreateTaskFromMeetingCommand(MeetingId, request, CorrelationId), CancellationToken.None);

        Assert.Equal(first.Data!.TaskId, second.Data!.TaskId);
        Assert.Single(links.Items); // ONE link, not two
        Assert.Equal(1, mediator.CreateTaskCallCount); // the second request never re-asked MOD-0024 at all
    }

    [Fact]
    public async Task A_DIFFERENT_idempotency_key_from_the_same_actor_and_meeting_creates_a_SECOND_task_and_link()
    {
        // Two genuinely different requests must not collapse into one just because meeting+actor repeat.
        var mediator = new BridgeMediator { CreateTaskResult = Response<Guid>.Success(TaskId, 201, CorrelationId) };
        var (handler, links, _, _) = Build(mediator, DateTimeOffset.UtcNow.AddDays(1));

        await handler.Handle(
            new CreateTaskFromMeetingCommand(MeetingId,
                new CreateTaskFromMeetingRequest("A", null, null, null, null, null, "key-a"), CorrelationId),
            CancellationToken.None);
        mediator.CreateTaskResult = Response<Guid>.Success(Guid.NewGuid(), 201, CorrelationId);
        await handler.Handle(
            new CreateTaskFromMeetingCommand(MeetingId,
                new CreateTaskFromMeetingRequest("B", null, null, null, null, null, "key-b"), CorrelationId),
            CancellationToken.None);

        Assert.Equal(2, links.Items.Count);
        Assert.Equal(2, mediator.CreateTaskCallCount);
    }

    // ── K1/AC2 — LinkExistingTask ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Links_an_existing_task_as_agenda()
    {
        var (handler, links, _, _) = BuildLinkHandler(MakeTask(TaskId));

        var result = await handler.Handle(
            new LinkExistingTaskCommand(MeetingId, new LinkExistingTaskRequest(TaskId, null), CorrelationId),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var link = Assert.Single(links.Items);
        Assert.Equal(RecordLinkTypes.Agenda, link.LinkType);
    }

    [Fact]
    public async Task Linking_the_SAME_task_twice_is_409_not_a_silent_success()
    {
        var (handler, links, _, _) = BuildLinkHandler(MakeTask(TaskId));
        await handler.Handle(
            new LinkExistingTaskCommand(MeetingId, new LinkExistingTaskRequest(TaskId, null), CorrelationId), CancellationToken.None);

        var second = await handler.Handle(
            new LinkExistingTaskCommand(MeetingId, new LinkExistingTaskRequest(TaskId, null), CorrelationId), CancellationToken.None);

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(MeetingReasonCodes.TaskAlreadyLinked, second.ReasonCode);
        Assert.Single(links.Items); // still one row
    }

    [Fact]
    public async Task Linking_a_task_from_ANOTHER_tenant_is_404_never_a_leak()
    {
        var otherTenantTask = MakeTask(TaskId, tenantId: TaskTestData.OtherTenant);
        var (handler, links, _, _) = BuildLinkHandler(otherTenantTask);

        var result = await handler.Handle(
            new LinkExistingTaskCommand(MeetingId, new LinkExistingTaskRequest(TaskId, null), CorrelationId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Empty(links.Items);
    }

    // ── K3/K9/K11 — ScheduleReviewMeetingForTask ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Schedules_a_review_meeting_via_the_ordinary_create_path_and_writes_ONE_reviewMeeting_link()
    {
        var newMeetingId = Guid.NewGuid();
        var mediator = new BridgeMediator
        {
            CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(newMeetingId), 201, CorrelationId)
        };
        var (handler, links) = BuildScheduleHandler(mediator, MakeTask(TaskId, holder: TaskTestData.Me));

        var result = await handler.Handle(
            new ScheduleReviewMeetingForTaskCommand(
                TaskId,
                new ScheduleReviewMeetingForTaskRequest(TypeId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, "key-1"),
                CorrelationId),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(newMeetingId, result.Data!.MeetingId);
        var link = Assert.Single(links.Items);
        Assert.Equal(RecordLinkTypes.ReviewMeeting, link.LinkType);
        Assert.Equal(RecordLinkModuleCodes.Meetings, link.SourceModuleCode);
        Assert.Equal(newMeetingId, link.SourceRecordId);
        Assert.Equal(TaskId, link.TargetRecordId);
        Assert.Contains("Bir görev", mediator.LastCreateMeetingRequest!.Title); // "Review: <task title>" default
    }

    [Fact]
    public async Task An_explicit_title_is_used_verbatim()
    {
        var mediator = new BridgeMediator
        {
            CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(Guid.NewGuid()), 201, CorrelationId)
        };
        var (handler, _) = BuildScheduleHandler(mediator, MakeTask(TaskId, holder: TaskTestData.Me));

        await handler.Handle(
            new ScheduleReviewMeetingForTaskCommand(
                TaskId,
                new ScheduleReviewMeetingForTaskRequest(TypeId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "Özel Başlık", "key-2"),
                CorrelationId),
            CancellationToken.None);

        Assert.Equal("Özel Başlık", mediator.LastCreateMeetingRequest!.Title);
    }

    [Fact]
    public async Task Neither_holder_nor_requester_gets_404_not_403_no_existence_leak()
    {
        var mediator = new BridgeMediator();
        var (handler, links) = BuildScheduleHandler(mediator, MakeTask(TaskId, holder: TaskTestData.Rival, requester: TaskTestData.Other));

        var result = await handler.Handle(
            new ScheduleReviewMeetingForTaskCommand(
                TaskId,
                new ScheduleReviewMeetingForTaskRequest(TypeId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, "key-3"),
                CorrelationId),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.False(mediator.CreateMeetingWasSent);
        Assert.Empty(links.Items);
    }

    [Fact]
    public async Task A_second_schedule_attempt_for_the_SAME_task_is_409_not_a_second_meeting()
    {
        var mediator = new BridgeMediator
        {
            CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(Guid.NewGuid()), 201, CorrelationId)
        };
        var (handler, links) = BuildScheduleHandler(mediator, MakeTask(TaskId, holder: TaskTestData.Me));
        await handler.Handle(
            new ScheduleReviewMeetingForTaskCommand(
                TaskId, new ScheduleReviewMeetingForTaskRequest(TypeId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, "key-4"),
                CorrelationId),
            CancellationToken.None);

        var second = await handler.Handle(
            new ScheduleReviewMeetingForTaskCommand(
                TaskId, new ScheduleReviewMeetingForTaskRequest(TypeId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, "key-5"),
                CorrelationId),
            CancellationToken.None);

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(MeetingReasonCodes.ReviewAlreadyScheduled, second.ReasonCode);
        Assert.Single(links.Items);
    }

    [Fact]
    public async Task K11_the_SAME_idempotency_key_resubmitted_returns_the_SAME_meeting_and_writes_no_second_link()
    {
        var firstMeetingId = Guid.NewGuid();
        var mediator = new BridgeMediator
        {
            CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(firstMeetingId), 201, CorrelationId)
        };
        var (handler, links) = BuildScheduleHandler(mediator, MakeTask(TaskId, holder: TaskTestData.Me));
        var request = new ScheduleReviewMeetingForTaskRequest(TypeId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, "same-key");

        var first = await handler.Handle(new ScheduleReviewMeetingForTaskCommand(TaskId, request, CorrelationId), CancellationToken.None);
        mediator.CreateMeetingResult = Response<MeetingDto>.Success(MakeMeetingDto(Guid.NewGuid()), 201, CorrelationId);
        var second = await handler.Handle(new ScheduleReviewMeetingForTaskCommand(TaskId, request, CorrelationId), CancellationToken.None);

        Assert.Equal(first.Data!.MeetingId, second.Data!.MeetingId);
        Assert.Single(links.Items);
        Assert.Equal(1, mediator.CreateMeetingCallCount);
    }

    // ── builders ─────────────────────────────────────────────────────────────────────────────────────────────

    private static (CreateTaskFromMeetingHandler Handler, FakeRecordLinkRepository Links, FakeAgendaItemRepository AgendaItems, FakeMeetingTypeRepository Types)
        Build(BridgeMediator mediator, DateTimeOffset? meetingStartAt, MeetingType? meetingType = null)
    {
        var meetings = new FakeMeetingRepository { Tenant = TaskTestData.Tenant };
        if (meetingStartAt is { } startAt)
        {
            meetings.Seed(new Meeting
            {
                Id = MeetingId, TenantId = TaskTestData.Tenant, Title = "Toplantı", MeetingTypeId = TypeId,
                StartAt = startAt, EndAt = startAt.AddHours(1), OrganizerUserId = TaskTestData.Me,
                IdempotencyKey = "meeting-key", CreatedBy = "test"
            });
        }

        var types = new FakeMeetingTypeRepository { Tenant = TaskTestData.Tenant };
        if (meetingType is not null)
        {
            types.Seed(meetingType);
        }

        var agendaItems = new FakeAgendaItemRepository { Tenant = TaskTestData.Tenant };
        var linksRepo = new FakeRecordLinkRepository();
        var linkService = new RecordLinkService(linksRepo, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
        var idempotency = new MeetingIdempotencyKeyResolver();
        var handler = new CreateTaskFromMeetingHandler(
            meetings, types, agendaItems, linkService, idempotency, new FakeCurrentUserContext(TaskTestData.Me), mediator);
        return (handler, linksRepo, agendaItems, types);
    }

    private static (LinkExistingTaskHandler Handler, FakeRecordLinkRepository Links, FakeAgendaItemRepository AgendaItems, FakeMeetingRepository Meetings)
        BuildLinkHandler(params TaskItem[] tasks)
    {
        var meetings = new FakeMeetingRepository { Tenant = TaskTestData.Tenant };
        meetings.Seed(new Meeting
        {
            Id = MeetingId, TenantId = TaskTestData.Tenant, Title = "Toplantı", MeetingTypeId = TypeId,
            StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1), OrganizerUserId = TaskTestData.Me,
            IdempotencyKey = "meeting-key", CreatedBy = "test"
        });
        var agendaItems = new FakeAgendaItemRepository { Tenant = TaskTestData.Tenant };
        var taskRepo = new FakeTaskItemRepository(tasks);
        var linksRepo = new FakeRecordLinkRepository();
        var linkService = new RecordLinkService(linksRepo, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
        var handler = new LinkExistingTaskHandler(meetings, agendaItems, taskRepo, linkService);
        return (handler, linksRepo, agendaItems, meetings);
    }

    private static (ScheduleReviewMeetingForTaskHandler Handler, FakeRecordLinkRepository Links)
        BuildScheduleHandler(BridgeMediator mediator, params TaskItem[] tasks)
    {
        var taskRepo = new FakeTaskItemRepository(tasks);
        var linksRepo = new FakeRecordLinkRepository();
        var linkService = new RecordLinkService(linksRepo, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
        var idempotency = new MeetingIdempotencyKeyResolver();
        var handler = new ScheduleReviewMeetingForTaskHandler(
            taskRepo, linkService, idempotency, new FakeCurrentUserContext(TaskTestData.Me), mediator);
        return (handler, linksRepo);
    }

    private static TaskItem MakeTask(Guid id, Guid? holder = null, Guid? requester = null, Guid? tenantId = null) => new()
    {
        Id = id,
        TenantId = tenantId ?? TaskTestData.Tenant,
        Title = "Bir görev",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = holder ?? TaskTestData.Me,
        CreatedByUserId = requester ?? holder ?? TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open
    };

    private static MeetingDto MakeMeetingDto(Guid id) => new(
        Id: id, Title: "t", MeetingTypeId: TypeId, MeetingTypeName: "Tür",
        StartAt: DateTimeOffset.UtcNow, EndAt: DateTimeOffset.UtcNow.AddHours(1), Location: null,
        OrganizerUserId: TaskTestData.Me, Description: null, FollowUpOfMeetingId: null,
        Lifecycle: Domain.Enums.Meetings.MeetingLifecycle.Scheduled, CancellationReason: null, Version: 1,
        Attendees: [], AgendaItems: []);

    /// <summary>Answers ONLY <see cref="CreateTaskItemCommand"/> and <see cref="CreateMeetingCommand"/>, with
    /// test-controlled results, so these handler tests stay isolated to MOD-0357's own logic.</summary>
    private sealed class BridgeMediator : IMediator
    {
        public Response<Guid>? CreateTaskResult { get; set; }
        public Response<MeetingDto>? CreateMeetingResult { get; set; }
        public CreateTaskItemRequest? LastCreateTaskRequest { get; private set; }
        public CreateMeetingRequest? LastCreateMeetingRequest { get; private set; }
        public bool CreateTaskWasSent { get; private set; }
        public bool CreateMeetingWasSent { get; private set; }
        public int CreateTaskCallCount { get; private set; }
        public int CreateMeetingCallCount { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is CreateTaskItemCommand createTask)
            {
                CreateTaskWasSent = true;
                CreateTaskCallCount++;
                LastCreateTaskRequest = createTask.Request;
                return (Task<TResponse>)(object)Task.FromResult(CreateTaskResult!);
            }

            if (request is CreateMeetingCommand createMeeting)
            {
                CreateMeetingWasSent = true;
                CreateMeetingCallCount++;
                LastCreateMeetingRequest = createMeeting.Request;
                return (Task<TResponse>)(object)Task.FromResult(CreateMeetingResult!);
            }

            throw new NotSupportedException($"BridgeMediator does not support {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => throw new NotSupportedException();
    }
}
