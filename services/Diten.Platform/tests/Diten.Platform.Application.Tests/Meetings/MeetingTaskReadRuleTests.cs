using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Meetings.Queries;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX3 (items 3 and 4) — the meeting module meets MOD-0024's tasks only through MOD-0024's own
/// read rule, the PRODUCTION one (<see cref="TaskReadAccessPolicy"/>) and the PRODUCTION write gate: a task the caller
/// cannot read is not linked (and answers like no task at all), is not offered by the link picker, and lends its title
/// to no row a meeting's reader sees. Only the data legs admit here (<see cref="RealTaskReadRule"/>): Me reads what Me
/// holds or created, and nothing else unless a test gives Me a team.
/// </summary>
public sealed class MeetingTaskReadRuleTests
{
    private static readonly Guid MeetingId = Guid.Parse("71111111-0000-4000-8000-000000000001");
    private static readonly Guid TypeId = Guid.Parse("73333333-0000-4000-8000-000000000003");
    private const string Corr = "corr-fix3";

    // ── item 3 — one "not found", whatever the reason ──────────────────────────────────────────────────────────

    /// <summary>MUTATION TARGET (ScheduleReviewMeetingForTaskHandler — its two own 404s carry the gate's body). No task,
    /// a task the caller cannot read (the gate's 404) and a task the caller reads but neither holds nor asked for (the
    /// handler's own 404): one status, one code, one sentence — over the REAL gate.</summary>
    [Fact]
    public async Task Scheduling_a_review_meeting_answers_one_not_found_for_no_task_an_unreadable_task_and_a_task_that_is_not_yours()
    {
        var unreadable = NewTask(holder: TaskTestData.Other, creator: TaskTestData.Other);
        var readableNotMine = NewTask(holder: TaskTestData.Rival, creator: TaskTestData.Other);   // Rival is on Me's team
        var tasks = new FakeTaskItemRepository(unreadable, readableNotMine);
        var me = new FakeCurrentUserContext(TaskTestData.Me);
        var gate = TaskWriteGateDoubles.Real(
            RealTaskReadRule.Over(tasks, TaskTestData.Me, team: new FakeTaskTeamResolver(TaskTestData.Rival)), me);
        var handler = new ScheduleReviewMeetingForTaskHandler(
            tasks, Links(out _), new MeetingIdempotencyKeyResolver(), me, new RefusingMediator(), writeGate: gate);

        async Task<(int, string?, string, bool)> AskAsync(Guid id)
        {
            var answer = await handler.Handle(new ScheduleReviewMeetingForTaskCommand(id,
                new ScheduleReviewMeetingForTaskRequest(TypeId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), null, $"k-{id}"),
                Corr), CancellationToken.None);
            return (answer.StatusCode, answer.ReasonCode, string.Join("|", answer.Errors), answer.IsSuccessful);
        }

        var none = await AskAsync(Guid.NewGuid());
        Assert.Equal((404, (string?)TaskReasonCodes.NotFound, "Task not found.", false), none);
        Assert.Equal(none, await AskAsync(unreadable.Id));
        Assert.Equal(none, await AskAsync(readableNotMine.Id));
    }

    // ── item 4 — linking, listing, picking ─────────────────────────────────────────────────────────────────────

    /// <summary>MUTATION TARGET (LinkExistingTaskHandler — the read rule). A task the caller cannot read answers like a
    /// task that does not exist — the same status, code and sentence — and nothing is linked; a task they can read is
    /// linked.</summary>
    [Fact]
    public async Task Linking_a_task_the_caller_cannot_read_answers_like_linking_no_task_and_links_nothing()
    {
        var mine = NewTask(holder: TaskTestData.Me, creator: TaskTestData.Me);
        var hidden = NewTask(holder: TaskTestData.Other, creator: TaskTestData.Other);
        var tasks = new FakeTaskItemRepository(mine, hidden);
        var meetings = new FakeMeetingRepository { Tenant = TaskTestData.Tenant };
        meetings.Seed(NewMeeting());
        var linkService = Links(out var links);
        var handler = new LinkExistingTaskHandler(
            meetings, new FakeAgendaItemRepository { Tenant = TaskTestData.Tenant }, tasks, linkService,
            RealTaskReadRule.Over(tasks, TaskTestData.Me), new FakeCurrentUserContext(TaskTestData.Me));

        async Task<(int, string?, string)> LinkAsync(Guid id)
        {
            var answer = await handler.Handle(
                new LinkExistingTaskCommand(MeetingId, new LinkExistingTaskRequest(id, null), Corr), CancellationToken.None);
            return (answer.StatusCode, answer.ReasonCode, string.Join("|", answer.Errors));
        }

        var none = await LinkAsync(Guid.NewGuid());
        Assert.Equal((404, (string?)TaskReasonCodes.NotFound, "Task not found."), none);
        Assert.Equal(none, await LinkAsync(hidden.Id));
        Assert.Empty(links.Items);

        Assert.Equal(200, (await LinkAsync(mine.Id)).Item1);
        Assert.Equal(mine.Id, Assert.Single(links.Items).TargetRecordId);
    }

    /// <summary>MUTATION TARGET (TaskRelatedRecordResolver — the read rule). A meeting's linked-task list keeps every row,
    /// and shows the title only of a task the reader may read; the other row carries the code in its place.</summary>
    [Fact]
    public async Task A_meetings_linked_task_list_shows_the_title_only_of_a_task_the_reader_can_read()
    {
        var mine = NewTask(holder: TaskTestData.Me, creator: TaskTestData.Me, title: "Benim işim");
        var hidden = NewTask(holder: TaskTestData.Other, creator: TaskTestData.Other, title: "Gizli iş");
        var tasks = new FakeTaskItemRepository(mine, hidden);
        var meetings = new FakeMeetingRepository { Tenant = TaskTestData.Tenant };
        meetings.Seed(NewMeeting());
        var linkService = Links(out _);
        foreach (var task in new[] { mine, hidden })
        {
            await linkService.AddLinkAsync(
                new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, MeetingId),
                new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, task.Id),
                RecordLinkTypes.Agenda, ct: CancellationToken.None);
        }

        var me = new FakeCurrentUserContext(TaskTestData.Me);
        var resolver = new TaskRelatedRecordResolver(tasks, RealTaskReadRule.Over(tasks, TaskTestData.Me), me);
        var handler = new GetLinkedTasksHandler(
            meetings, new FakeMeetingAttendeeRepository { Tenant = TaskTestData.Tenant }, linkService,
            new RelatedRecordResolverRegistry([resolver]), me, TaskActors.None());

        var listed = await handler.Handle(new GetLinkedTasksQuery(MeetingId, Corr), CancellationToken.None);

        Assert.True(listed.IsSuccessful);
        var titles = listed.Data!.ToDictionary(row => Guid.Parse(row.TaskId), row => row.Title);
        Assert.Equal("Benim işim", titles[mine.Id]);
        Assert.Equal(TaskLinks.InaccessibleTitleCode, titles[hidden.Id]);
        Assert.DoesNotContain(listed.Data!, row => row.Title == "Gizli iş");
    }

    /// <summary>MUTATION TARGET (GetTaskLinkCandidatesHandler — the read rule, asked before the limit). The picker offers
    /// only tasks the caller can read; a page of unreadable matches does not crowd them out.</summary>
    [Fact]
    public async Task The_link_picker_offers_only_tasks_the_caller_can_read()
    {
        var hidden = Enumerable.Range(0, 25)
            .Select(i => NewTask(holder: TaskTestData.Other, creator: TaskTestData.Other, title: $"A gizli {i:00}"))
            .ToArray();
        var mine = NewTask(holder: TaskTestData.Me, creator: TaskTestData.Me, title: "Z benim");
        var tasks = new FakeTaskItemRepository([.. hidden, mine]);
        var handler = new GetTaskLinkCandidatesHandler(
            tasks, RealTaskReadRule.Over(tasks, TaskTestData.Me), new FakeCurrentUserContext(TaskTestData.Me));

        var offered = await handler.Handle(new GetTaskLinkCandidatesQuery(null, 20, Corr), CancellationToken.None);

        Assert.Equal(mine.Id, Assert.Single(offered.Data!).Id);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────

    private static TaskItem NewTask(Guid holder, Guid creator, string title = "Bir görev") => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = title,
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = holder,
        CreatedByUserId = creator,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        CreatedBy = "fix3"
    };

    private static Meeting NewMeeting() => new()
    {
        Id = MeetingId, TenantId = TaskTestData.Tenant, Title = "Toplantı", MeetingTypeId = TypeId,
        StartAt = DateTimeOffset.UtcNow, EndAt = DateTimeOffset.UtcNow.AddHours(1), OrganizerUserId = TaskTestData.Me,
        IdempotencyKey = "meeting-fix3", CreatedBy = "test"
    };

    private static RecordLinkService Links(out FakeRecordLinkRepository links)
    {
        links = new FakeRecordLinkRepository();
        return new RecordLinkService(links, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me));
    }

    /// <summary>Every path these tests take refuses before a meeting would be created.</summary>
    private sealed class RefusingMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => throw new InvalidOperationException($"No request was expected: {request.GetType().Name}");

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}
