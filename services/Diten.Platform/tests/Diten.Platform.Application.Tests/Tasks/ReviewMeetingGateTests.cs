using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Tests.Meetings;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// `Task` here is System.Threading.Tasks.Task: this file's own namespace ends in `.Tasks`, which shadows it.
using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-MG-MOD0357-S9-REVIEW-GATE-01 · CT fix-up F1 (2026-09-15) — a Task whose TYPE requires a review meeting keeps
/// its DECISION (<c>submitReview</c>, <c>complete</c>) visible but DISABLED with <c>REVIEW_MEETING_REQUIRED</c> until
/// at least one non-cancelled linked meeting has PUBLISHED minutes, and the server refuses the same two decisions
/// with 409. <c>start</c>/resume is NEVER held back: scheduling and holding the meeting is part of the work.
///
/// <para>Both layers run against the PRODUCTION <see cref="ReviewMeetingGateReader"/> over the suite's in-memory
/// RecordLink/Meeting/MeetingMinutesVersion doubles, so every meeting shape of the golden matrix (none, scheduled,
/// cancelled, draft minutes, published, corrected-published) reaches the projection and the handlers through the
/// real reader rather than a pre-decided boolean. <c>ReviewMeetingGateReaderMongoTests</c> proves the same reader
/// against a real mongod.</para>
/// </summary>
public sealed class ReviewMeetingGateTests
{
    public enum MeetingShape { NoMeeting, Scheduled, Cancelled, DraftMinutes, Published, CorrectedPublished }

    public enum GateAction { Start, Resume, SubmitReview, Complete }

    private static bool Unlocks(MeetingShape shape) => shape is MeetingShape.Published or MeetingShape.CorrectedPublished;

    public static TheoryData<MeetingShape, GateAction> RequiredMatrix()
    {
        var data = new TheoryData<MeetingShape, GateAction>();
        foreach (var shape in Enum.GetValues<MeetingShape>())
        {
            foreach (var action in Enum.GetValues<GateAction>())
            {
                data.Add(shape, action);
            }
        }

        return data;
    }

    public static TheoryData<TaskReviewMeetingRequirement, GateAction> UngatedRequirements()
    {
        var data = new TheoryData<TaskReviewMeetingRequirement, GateAction>();
        foreach (var requirement in new[] { TaskReviewMeetingRequirement.Optional, TaskReviewMeetingRequirement.NotAllowed })
        {
            foreach (var action in Enum.GetValues<GateAction>())
            {
                data.Add(requirement, action);
            }
        }

        return data;
    }

    // ── the projection's own hint (TaskWorkItemProvider) ──────────────────────

    [Theory]
    [MemberData(nameof(RequiredMatrix))]
    public async Task Required_golden_matrix__start_always_enabled__decisions_wait_for_published_minutes(
        MeetingShape shape, GateAction gateAction)
    {
        var world = new MeetingWorld();
        var task = MakeTask(gateAction);
        world.Arrange(task.Id, shape);

        var action = await ProjectActionAsync(task, gateAction, TaskReviewMeetingRequirement.Required, world.Reader());

        var decision = gateAction is GateAction.SubmitReview or GateAction.Complete;
        if (!decision || Unlocks(shape))
        {
            Assert.True(action.Enabled, $"{gateAction} under {shape} must be enabled");
            Assert.Null(action.DisabledReasonCode);
        }
        else
        {
            Assert.False(action.Enabled, $"{gateAction} under {shape} must be disabled");
            Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, action.DisabledReasonCode);
        }
    }

    [Theory]
    [MemberData(nameof(UngatedRequirements))]
    public async Task Optional_and_NotAllowed_are_never_gated_even_with_no_meeting_at_all(
        TaskReviewMeetingRequirement requirement, GateAction gateAction)
    {
        var world = new MeetingWorld();
        var task = MakeTask(gateAction);

        var action = await ProjectActionAsync(task, gateAction, requirement, world.Reader());

        Assert.True(action.Enabled);
        Assert.Null(action.DisabledReasonCode);
    }

    [Fact]
    public async Task An_absent_reader_fails_CLOSED_for_the_decision_but_never_for_start()
    {
        // No IReviewMeetingGateReader wired at all — a Required type's decision must still be blocked, not waved
        // through because the seam is missing; and start must still be pressable.
        var complete = await ProjectActionAsync(
            MakeTask(GateAction.Complete), GateAction.Complete, TaskReviewMeetingRequirement.Required, reader: null);
        var start = await ProjectActionAsync(
            MakeTask(GateAction.Start), GateAction.Start, TaskReviewMeetingRequirement.Required, reader: null);

        Assert.False(complete.Enabled);
        Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, complete.DisabledReasonCode);
        Assert.True(start.Enabled);
    }

    [Fact]
    public async Task Approval_pending_wins_over_the_review_meeting_gate_on_complete()
    {
        // Precedence unchanged: approval first (the gate the user cannot clear), then the review meeting.
        var task = MakeTask(GateAction.Complete);
        task.ApprovalRequired = true;
        task.ApprovalManagerUserId = TaskTestData.Rival;
        task.WorkflowInstanceId = Guid.NewGuid();
        // No state seeded for this instance: TaskApprovalView.Resolve reads an unreadable instance as outstanding.

        var action = await ProjectActionAsync(
            task, GateAction.Complete, TaskReviewMeetingRequirement.Required, new MeetingWorld().Reader());

        Assert.False(action.Enabled);
        Assert.Equal(TaskReasonCodes.ApprovalPending, action.DisabledReasonCode);
    }

    [Fact]
    public async Task Start_keeps_its_own_approval_rule__disabled_for_APPROVAL_not_for_the_meeting()
    {
        var task = MakeTask(GateAction.Start);
        task.ApprovalRequired = true;
        task.ApprovalManagerUserId = TaskTestData.Rival;
        task.WorkflowInstanceId = Guid.NewGuid();

        var action = await ProjectActionAsync(
            task, GateAction.Start, TaskReviewMeetingRequirement.Required, new MeetingWorld().Reader());

        Assert.False(action.Enabled);
        Assert.Equal(TaskReasonCodes.ApprovalPending, action.DisabledReasonCode);
    }

    [Theory]
    [InlineData(MeetingShape.NoMeeting)]
    [InlineData(MeetingShape.Published)]
    public async Task A_released_review_s_complete_waits_for_the_minutes_too(MeetingShape shape)
    {
        // The server refuses → Done on EVERY path, so the PendingReview-released `complete` carries the same hint.
        var world = new MeetingWorld();
        var (task, approvals) = PendingReviewTask(new TaskApprovalState(IsPending: false, IsApproved: true, IsRejected: false));
        world.Arrange(task.Id, shape);

        var action = await ProjectActionAsync(
            task, GateAction.Complete, TaskReviewMeetingRequirement.Required, world.Reader(), approvals);

        Assert.Equal(Unlocks(shape), action.Enabled);
        if (!Unlocks(shape))
        {
            Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, action.DisabledReasonCode);
        }
    }

    [Theory]
    [InlineData(MeetingShape.NoMeeting)]
    [InlineData(MeetingShape.Published)]
    public async Task A_refused_review_s_resubmit_waits_for_the_minutes_too(MeetingShape shape)
    {
        var world = new MeetingWorld();
        var (task, approvals) = PendingReviewTask(new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: true));
        world.Arrange(task.Id, shape);

        var action = await ProjectActionAsync(
            task, GateAction.SubmitReview, TaskReviewMeetingRequirement.Required, world.Reader(), approvals);

        Assert.Equal(Unlocks(shape), action.Enabled);
        if (!Unlocks(shape))
        {
            Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, action.DisabledReasonCode);
        }
    }

    [Theory]
    [InlineData(MeetingShape.NoMeeting, false)]
    [InlineData(MeetingShape.DraftMinutes, false)]
    [InlineData(MeetingShape.Published, true)]
    public async Task The_wire_carries_the_TYPE_s_requirement_and_whether_minutes_published(
        MeetingShape shape, bool published)
    {
        var world = new MeetingWorld();
        var task = MakeTask(GateAction.Complete);
        world.Arrange(task.Id, shape);
        var provider = BuildProvider(task, TaskReviewMeetingRequirement.Required, world.Reader());

        var item = Assert.Single(await provider.GetWorkItemsAsync(TaskActor(task), CancellationToken.None));

        Assert.Equal("required", item.ReviewMeetingPolicy!.Requirement);
        Assert.Equal(published, item.ReviewMeetingPolicy.MinutesPublished);
    }

    // ── the decision handlers' own re-check ───────────────────────────────────

    [Theory]
    [InlineData(TaskLifecycle.Open)]
    [InlineData(TaskLifecycle.Waiting)]
    public async Task Start_and_resume_succeed_with_no_meeting_at_all_and_never_ask_the_reader(TaskLifecycle from)
    {
        var task = MakeTask(GateAction.Start);
        task.Lifecycle = from;
        var tasks = new FakeTaskItemRepository(task);
        var reader = new FakeReviewMeetingGateReader(false);

        var result = await TransitionTo(tasks, task, TaskLifecycle.InProgress, TaskReviewMeetingRequirement.Required, reader);

        Assert.True(result.IsSuccessful, result.ReasonCode);
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single().Lifecycle);
        Assert.Empty(reader.Calls);
    }

    [Theory]
    [InlineData(MeetingShape.NoMeeting)]
    [InlineData(MeetingShape.Scheduled)]
    [InlineData(MeetingShape.Cancelled)]
    [InlineData(MeetingShape.DraftMinutes)]
    public async Task Complete_is_refused_409_short_of_published_minutes(MeetingShape shape)
    {
        var world = new MeetingWorld();
        var task = MakeTask(GateAction.Complete);
        world.Arrange(task.Id, shape);
        var tasks = new FakeTaskItemRepository(task);

        var result = await TransitionTo(tasks, task, TaskLifecycle.Done, TaskReviewMeetingRequirement.Required, world.Reader());

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, result.ReasonCode);
        // Nothing was committed — the refusal is not cosmetic.
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single().Lifecycle);
    }

    [Theory]
    [InlineData(MeetingShape.Published)]
    [InlineData(MeetingShape.CorrectedPublished)]
    public async Task Complete_succeeds_once_minutes_are_published(MeetingShape shape)
    {
        var world = new MeetingWorld();
        var task = MakeTask(GateAction.Complete);
        world.Arrange(task.Id, shape);
        var tasks = new FakeTaskItemRepository(task);

        var result = await TransitionTo(tasks, task, TaskLifecycle.Done, TaskReviewMeetingRequirement.Required, world.Reader());

        Assert.True(result.IsSuccessful, result.ReasonCode);
        Assert.Equal(TaskLifecycle.Done, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Complete_is_refused_until_the_minutes_publish_then_goes_through()
    {
        var world = new MeetingWorld();
        var task = MakeTask(GateAction.Complete);
        var meetingId = world.Arrange(task.Id, MeetingShape.DraftMinutes);
        var tasks = new FakeTaskItemRepository(task);

        var refused = await TransitionTo(tasks, task, TaskLifecycle.Done, TaskReviewMeetingRequirement.Required, world.Reader());
        world.SeedMinutes(meetingId, versionNumber: 2, MinutesStatus.Published);
        var accepted = await TransitionTo(tasks, task, TaskLifecycle.Done, TaskReviewMeetingRequirement.Required, world.Reader());

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, refused.ReasonCode);
        Assert.True(accepted.IsSuccessful, accepted.ReasonCode);
        Assert.Equal(TaskLifecycle.Done, tasks.Items.Single().Lifecycle);
    }

    [Theory]
    [InlineData(MeetingShape.NoMeeting)]
    [InlineData(MeetingShape.Scheduled)]
    [InlineData(MeetingShape.Cancelled)]
    [InlineData(MeetingShape.DraftMinutes)]
    public async Task Submit_for_review_is_refused_409_short_of_published_minutes_and_opens_NO_review(MeetingShape shape)
    {
        var world = new MeetingWorld();
        var task = MakeTask(GateAction.SubmitReview);
        world.Arrange(task.Id, shape);
        var tasks = new FakeTaskItemRepository(task);
        var reviews = new FakeTaskReviewService();

        var result = await Submit(tasks, task, reviews, TaskReviewMeetingRequirement.Required, world.Reader());

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, result.ReasonCode);
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single().Lifecycle);
        // The approval boundary: a refused submit never reaches MOD-0023 — no review instance was opened.
        Assert.Empty(reviews.Started);
        Assert.Null(tasks.Items.Single().ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task Submit_for_review_is_refused_until_the_minutes_publish_then_goes_through()
    {
        var world = new MeetingWorld();
        var task = MakeTask(GateAction.SubmitReview);
        var meetingId = world.Arrange(task.Id, MeetingShape.Scheduled);
        var tasks = new FakeTaskItemRepository(task);
        var reviews = new FakeTaskReviewService();

        var refused = await Submit(tasks, task, reviews, TaskReviewMeetingRequirement.Required, world.Reader());
        world.SeedMinutes(meetingId, versionNumber: 1, MinutesStatus.Published);
        var accepted = await Submit(tasks, task, reviews, TaskReviewMeetingRequirement.Required, world.Reader());

        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, refused.ReasonCode);
        Assert.True(accepted.IsSuccessful, accepted.ReasonCode);
        Assert.Equal(TaskLifecycle.PendingReview, tasks.Items.Single().Lifecycle);
        Assert.Single(reviews.Started);
    }

    [Fact]
    public async Task A_resubmission_after_a_refused_review_is_gated_too()
    {
        var (task, states) = PendingReviewTask(new TaskApprovalState(IsPending: false, IsApproved: false, IsRejected: true));
        var tasks = new FakeTaskItemRepository(task);
        var reviews = new FakeTaskReviewService();

        var result = await Submit(
            tasks, task, reviews, TaskReviewMeetingRequirement.Required, new MeetingWorld().Reader(), states);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, result.ReasonCode);
        Assert.Empty(reviews.Started);
    }

    [Fact]
    public async Task Optional_never_reaches_the_gate_reader_on_either_decision()
    {
        var completeTask = MakeTask(GateAction.Complete);
        var submitTask = MakeTask(GateAction.SubmitReview);
        var reader = new FakeReviewMeetingGateReader(false);

        var completed = await TransitionTo(
            new FakeTaskItemRepository(completeTask), completeTask, TaskLifecycle.Done, TaskReviewMeetingRequirement.Optional, reader);
        var submitted = await Submit(
            new FakeTaskItemRepository(submitTask), submitTask, new FakeTaskReviewService(), TaskReviewMeetingRequirement.Optional, reader);

        Assert.True(completed.IsSuccessful, completed.ReasonCode);
        Assert.True(submitted.IsSuccessful, submitted.ReasonCode);
        Assert.Empty(reader.Calls);
    }

    [Fact]
    public async Task A_missing_reader_fails_CLOSED_on_both_decisions()
    {
        var completeTask = MakeTask(GateAction.Complete);
        var submitTask = MakeTask(GateAction.SubmitReview);

        var completed = await TransitionTo(
            new FakeTaskItemRepository(completeTask), completeTask, TaskLifecycle.Done, TaskReviewMeetingRequirement.Required, reviewMeetingGate: null);
        var submitted = await Submit(
            new FakeTaskItemRepository(submitTask), submitTask, new FakeTaskReviewService(), TaskReviewMeetingRequirement.Required, reviewMeetingGate: null);

        Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, completed.ReasonCode);
        Assert.Equal(TaskReasonCodes.ReviewMeetingRequired, submitted.ReasonCode);
    }

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>The meeting side of the matrix, behind the PRODUCTION reader.</summary>
    private sealed class MeetingWorld
    {
        private readonly FakeRecordLinkRepository _links = new();
        private readonly FakeMeetingRepository _meetings = new() { Tenant = TaskTestData.Tenant };
        private readonly FakeMeetingMinutesVersionRepository _minutes = new() { Tenant = TaskTestData.Tenant };

        public IReviewMeetingGateReader Reader() => new ReviewMeetingGateReader(
            new RecordLinkService(_links, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(TaskTestData.Me)),
            _meetings,
            _minutes);

        /// <summary>Links one review meeting of the given shape to the task; returns its id (Empty for none).</summary>
        public Guid Arrange(Guid taskId, MeetingShape shape)
        {
            if (shape == MeetingShape.NoMeeting)
            {
                return Guid.Empty;
            }

            var meeting = new Meeting
            {
                Id = Guid.NewGuid(),
                TenantId = TaskTestData.Tenant,
                Title = "Review meeting",
                MeetingTypeId = Guid.NewGuid(),
                StartAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
                EndAt = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
                OrganizerUserId = TaskTestData.Me,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                Lifecycle = shape == MeetingShape.Cancelled ? MeetingLifecycle.Cancelled : MeetingLifecycle.Scheduled
            };
            _meetings.Seed(meeting);
            _links.Seed(new RecordLink
            {
                TenantId = TaskTestData.Tenant,
                SourceModuleCode = RecordLinkModuleCodes.Meetings,
                SourceRecordId = meeting.Id,
                TargetModuleCode = RecordLinkModuleCodes.Tasks,
                TargetRecordId = taskId,
                LinkType = RecordLinkTypes.ReviewMeeting,
                CreatedByUserId = TaskTestData.Me,
                CreatedBy = "test"
            });

            switch (shape)
            {
                case MeetingShape.DraftMinutes:
                    SeedMinutes(meeting.Id, 1, MinutesStatus.Draft);
                    break;
                case MeetingShape.Cancelled:
                    // The strongest cancelled case: its minutes DID publish, and still must not unlock.
                    SeedMinutes(meeting.Id, 1, MinutesStatus.Published);
                    break;
                case MeetingShape.Published:
                    SeedMinutes(meeting.Id, 1, MinutesStatus.Published);
                    break;
                case MeetingShape.CorrectedPublished:
                    SeedMinutes(meeting.Id, 1, MinutesStatus.Published);
                    SeedMinutes(meeting.Id, 2, MinutesStatus.Published, correctionOf: 1);
                    break;
            }

            return meeting.Id;
        }

        public void SeedMinutes(Guid meetingId, int versionNumber, MinutesStatus status, int? correctionOf = null)
            => _minutes.Seed(new MeetingMinutesVersion
            {
                TenantId = TaskTestData.Tenant,
                MeetingId = meetingId,
                VersionNumber = versionNumber,
                Status = status,
                CorrectionOfVersionNumber = correctionOf,
                CorrectionReason = correctionOf is null ? null : "Correction"
            });
    }

    private static TaskItem MakeTask(GateAction forAction) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Golden matrix task",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = forAction switch
        {
            GateAction.Start => TaskLifecycle.Open,
            GateAction.Resume => TaskLifecycle.Waiting,
            _ => TaskLifecycle.InProgress
        },
        // `submitReview` is offered only for review-gated work that has not been submitted yet.
        ReviewRequired = forAction == GateAction.SubmitReview,
        TaskTypeId = Guid.NewGuid(),
        Version = 1
    };

    private static readonly Guid ReviewInstance = Guid.Parse("cafecafe-0000-0000-0000-00000000cafe");

    private static (TaskItem Task, FakeTaskApprovalService States) PendingReviewTask(TaskApprovalState reviewState)
    {
        var task = MakeTask(GateAction.Complete);
        task.Lifecycle = TaskLifecycle.PendingReview;
        task.ReviewRequired = true;
        task.ReviewWorkflowInstanceId = ReviewInstance;
        var states = new FakeTaskApprovalService();
        states.States[ReviewInstance] = reviewState;
        return (task, states);
    }

    private static string CodeOf(GateAction action) => action switch
    {
        GateAction.Start or GateAction.Resume => "start",
        GateAction.SubmitReview => "submitReview",
        _ => "complete"
    };

    private static async Task<WorkItemActionDto> ProjectActionAsync(
        TaskItem task, GateAction gateAction, TaskReviewMeetingRequirement requirement,
        IReviewMeetingGateReader? reader, FakeTaskApprovalService? approvals = null)
    {
        var provider = BuildProvider(task, requirement, reader, approvals);
        var item = Assert.Single(await provider.GetWorkItemsAsync(TaskActor(task), CancellationToken.None));
        return Assert.Single(item.Actions, a => a.Code == CodeOf(gateAction));
    }

    private static TaskWorkItemProvider BuildProvider(
        TaskItem task,
        TaskReviewMeetingRequirement requirement,
        IReviewMeetingGateReader? reviewMeetingGate,
        FakeTaskApprovalService? approvals = null)
        => new(
            new FakeTaskItemRepository(task), new FakePositionAssignmentRepository(),
            new TaskLifecycleService(), new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(), new FakeChecklistRunRepository(),
            approvals ?? new FakeTaskApprovalService(), new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(),
            new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(),
            TaskActors.PermitAll(), new FakePositionRepository(), new FakeOrganizationUnitRepository(),
            SlaForTests.Real(), new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository(TypeOf(task, requirement)),
            teamResolver: null, recordLinks: null, relatedRecordResolvers: null, attachments: null,
            logger: null, meetingRepository: null, reviewMeetingGate: reviewMeetingGate);

    private static TaskType TypeOf(TaskItem task, TaskReviewMeetingRequirement requirement) => new()
    {
        Id = task.TaskTypeId!.Value, TenantId = TaskTestData.Tenant, Code = "REV", Name = "Review",
        ReviewMeetingRequirement = requirement
    };

    private static WorkItemActor TaskActor(TaskItem task)
        => new(task.AssigneeUserId!.Value, IsPlatformActor: true, new HashSet<string>
        {
            TaskPermissions.Update, TaskPermissions.Claim, TaskPermissions.Complete,
            TaskPermissions.Cancel, TaskPermissions.Delete, TaskPermissions.Assign
        });

    private static Task<Response<NoContent>> TransitionTo(
        FakeTaskItemRepository tasks, TaskItem task, TaskLifecycle target,
        TaskReviewMeetingRequirement requirement, IReviewMeetingGateReader? reviewMeetingGate)
        => new TransitionTaskItemHandler(
                tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
                new FakeChecklistRunRepository(), new TaskChecklistService(), new FakeWorkflowTransitionGate(),
                new FakeTaskDependencyRepository(), new FakeTaskTypeRepository(TypeOf(task, requirement)), new FakeTaskNotificationService(),
                new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                new FakeTaskAttachmentRepository(), NullLogger<TransitionTaskItemHandler>.Instance, reviewMeetingGate)
            .Handle(
                new TransitionTaskItemCommand(task.Id, target, new TaskTransitionRequest(task.Version, null, null), "corr"),
                CancellationToken.None);

    private static Task<Response<NoContent>> Submit(
        FakeTaskItemRepository tasks, TaskItem task, FakeTaskReviewService reviews,
        TaskReviewMeetingRequirement requirement, IReviewMeetingGateReader? reviewMeetingGate,
        FakeTaskApprovalService? states = null)
        => new SubmitTaskForReviewHandler(
                tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
                reviews, states ?? new FakeTaskApprovalService(), NullLogger<SubmitTaskForReviewHandler>.Instance,
                new FakeTaskTypeRepository(TypeOf(task, requirement)), reviewMeetingGate)
            .Handle(
                new SubmitTaskForReviewCommand(task.Id, new TaskTransitionRequest(task.Version, null, null), "corr"),
                CancellationToken.None);
}

/// <summary>Test double — always answers a fixed unlock state, and remembers who asked (so tests can assert an
/// Optional/NotAllowed type — or a `start` — never even queries it).</summary>
internal sealed class FakeReviewMeetingGateReader(bool unlocked) : IReviewMeetingGateReader
{
    public List<Guid> Calls { get; } = [];

    public Task<bool> HasUnlockedReviewMeetingAsync(Guid taskId, CancellationToken ct)
    {
        Calls.Add(taskId);
        return Task.FromResult(unlocked);
    }

    public Task<IReadOnlyDictionary<Guid, bool>> ResolveUnlockedReviewMeetingsAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct)
    {
        foreach (var id in taskIds) { Calls.Add(id); }
        IReadOnlyDictionary<Guid, bool> result = taskIds.ToDictionary(id => id, _ => unlocked);
        return Task.FromResult(result);
    }
}
