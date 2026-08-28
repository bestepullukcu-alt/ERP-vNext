using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// "NOBODY ACCEPTED THIS AND ITS CHILDREN ARE ALREADY WORKING" — the server half.
///
/// <para><b>A signal, not a gate.</b> The browser now says this in a sentence. Nothing here may start enforcing
/// it: no rule ties a child's START to its parent's acceptance, deliberately — one unpressed "Accept" would
/// otherwise stop everyone below it. These tests hold that line from the server side, where a new blocker would
/// actually have teeth.</para>
///
/// <para><b>Where the count comes from.</b> The projection already carries each child's state in the contract
/// vocabulary; this pins that it is the SHARED vocabulary (<see cref="TaskBlockingRules.StateOf"/>) rather than
/// a second copy — the provider had written the same switch out again, and a drift there would make the
/// browser's sentence count the wrong children.</para>
/// </summary>
public sealed class TaskRunningChildrenSignalTests
{
    [Fact]
    public async Task A_running_child_is_projected_as_in_progress_through_the_SHARED_vocabulary()
    {
        var parent = Parent(TaskLifecycle.Open);
        var child = Child(parent, TaskLifecycle.InProgress);

        var item = await ProjectAsync(parent, child);

        var projected = Assert.Single(item.Subtasks!.Items);
        Assert.Equal(TaskBlockingRules.StateOf(child), projected.Status);
        Assert.Equal("in-progress", projected.Status);
    }

    [Theory]
    [InlineData(TaskLifecycle.InProgress, "in-progress")]
    [InlineData(TaskLifecycle.PendingReview, "in-progress")]
    [InlineData(TaskLifecycle.Waiting, "in-progress")]
    [InlineData(TaskLifecycle.Open, "not-started")]
    [InlineData(TaskLifecycle.Planned, "not-started")]
    [InlineData(TaskLifecycle.Done, "done")]
    [InlineData(TaskLifecycle.Cancelled, "cancelled")]
    public async Task Every_lifecycle_projects_the_state_the_shared_rule_says_it_does(
        TaskLifecycle lifecycle, string expected)
    {
        // The whole vocabulary, so a future edit to either side has to face this table.
        var parent = Parent(TaskLifecycle.Open);
        var item = await ProjectAsync(parent, Child(parent, lifecycle));

        Assert.Equal(expected, Assert.Single(item.Subtasks!.Items).Status);
    }

    [Fact]
    public void The_lifecycle_to_state_switch_is_written_in_exactly_ONE_place()
    {
        /*
         * The behavioural test above cannot fail on a COPY of the rule — the copy was character-for-character
         * identical, which is exactly why it survived. This one measures what the behavioural test cannot: that
         * the mapping exists once. A second spelling drifts silently, and the browser's new "already running"
         * sentence counts children by that very value.
         */
        var root = RepoPaths.ApplicationSource("Features", "Tasks");

        var spelling = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("=> \"in-progress\""))
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();

        Assert.True(spelling.Count == 1,
            $"the lifecycle→state mapping is written in {spelling.Count} places: " + string.Join(", ", spelling));
        Assert.Equal("TaskBlockingRules.cs", spelling[0]);
    }

    [Fact]
    public void A_running_child_does_NOT_block_anything_new()
    {
        /*
         * The direction, asserted where it would hurt. OpenSubtasksBlockingCompletion is the ONLY subtask rule
         * there is, and it is about COMPLETING the parent — not about accepting it and not about starting the
         * child. A running child appears in it exactly as an unstarted one does; nothing new was added.
         */
        var parent = Parent(TaskLifecycle.Open);
        var running = Child(parent, TaskLifecycle.InProgress);
        var notStarted = Child(parent, TaskLifecycle.Open);
        var cancelled = Child(parent, TaskLifecycle.Cancelled);

        var blocking = TaskBlockingRules.OpenSubtasksBlockingCompletion([running, notStarted, cancelled]);

        Assert.Equal(2, blocking.Count);
        Assert.DoesNotContain(cancelled, blocking);
    }

    [Fact]
    public async Task An_unaccepted_parent_with_running_children_still_offers_the_SAME_actions()
    {
        /*
         * If the signal ever grew teeth, this is where it would show: the action set for a pending-acceptance
         * parent must not depend on what its children are doing.
         */
        var lonely = Parent(TaskLifecycle.Open);
        var busy = Parent(TaskLifecycle.Open);

        var withNone = await ProjectAsync(lonely);
        var withRunning = await ProjectAsync(busy, Child(busy, TaskLifecycle.InProgress), Child(busy, TaskLifecycle.InProgress));

        Assert.Equal(
            withNone.Actions.Where(a => a.Enabled).Select(a => a.Code).OrderBy(c => c),
            withRunning.Actions.Where(a => a.Enabled).Select(a => a.Code).OrderBy(c => c));
        Assert.Null(withRunning.BlockedState);
    }

    private static async Task<WorkItemProjectionDto> ProjectAsync(TaskItem parent, params TaskItem[] children)
    {
        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository([parent, .. children]),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(), TaskActors.PermitAll(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository());

        var actor = new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());
        var items = await provider.GetWorkItemsAsync(actor, CancellationToken.None);
        return Assert.Single(items, i => i.Id == parent.Id.ToString());
    }

    private static TaskItem Parent(TaskLifecycle lifecycle) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Parent",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = lifecycle,
        Version = 1
    };

    private static TaskItem Child(TaskItem parent, TaskLifecycle lifecycle) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = $"Child {lifecycle}",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        ParentTaskItemId = parent.Id,
        Lifecycle = lifecycle,
        Version = 1
    };
}
