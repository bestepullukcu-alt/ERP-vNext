using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// The four settings the create form collected from Phase 1 and no surface ever showed: watchers, the delegation
/// policy, the notification preferences and the reminder lead.
///
/// <para><b>Measured 2026-08-14.</b> All four were written at creation and none of them reached the Task Center;
/// <c>delegationAllowed</c> did not appear anywhere in the client at all. That is the same store-it-and-never-
/// show-it shape the plan date was in before f8d10259 — a value the system holds and its owner can never see.</para>
///
/// <para>They are projected and deliberately NOT placed on any screen in the same change. Where each belongs is a
/// design decision, and this round has already paid for the habit of inventing a card for a field that arrived
/// without one. These tests pin the WIRE, which is what a later screen will be built against.</para>
/// </summary>
public sealed class TaskProjectedSettingsTests
{
    [Fact]
    public async Task Watchers_reach_the_wire_with_their_names_and_their_role()
    {
        var task = Task();
        var watchers = new FakeTaskWatcherRepository();
        await watchers.CreateAsync(
            new TaskWatcher
            {
                TenantId = TaskTestData.Tenant,
                TaskItemId = task.Id,
                UserId = TaskTestData.Other,
                Role = TaskWatcherRole.Consultant
            },
            CancellationToken.None);

        var projected = await Project(task, watchers: watchers);

        var watcher = Assert.Single(projected.Watchers!);
        Assert.Equal(TaskTestData.Other.ToString(), watcher.Person.Id);
        Assert.Equal("Consultant", watcher.Role);
    }

    /// <summary>
    /// A watcher rides the SAME batched directory read as the assignee, so the screen can name a person rather
    /// than print an id. Resolving watcher names separately would be a second round-trip per page — and the
    /// version of this that forgets is the one that renders "somebody is watching" with no somebody.
    /// </summary>
    [Fact]
    public async Task A_watcher_is_named_by_the_same_batch_that_names_the_assignee()
    {
        var task = Task();
        var watchers = new FakeTaskWatcherRepository();
        await watchers.CreateAsync(
            new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, UserId = TaskTestData.Other },
            CancellationToken.None);

        var projected = await Project(
            task,
            watchers: watchers,
            names: new FakeUserDisplayNameResolver((TaskTestData.Other, "İzleyen Kişi")));

        Assert.Equal("İzleyen Kişi", Assert.Single(projected.Watchers!).Person.DisplayName);
    }

    [Fact]
    public async Task A_task_nobody_watches_carries_no_watcher_list_at_all()
    {
        var projected = await Project(Task());

        // Omitted rather than emitted empty — an empty array reaches the client as a present container and draws
        // an empty strip, which is the labelled-blank this project removes everywhere else.
        Assert.Null(projected.Watchers);
    }

    [Fact]
    public async Task The_delegation_policy_reaches_the_wire()
    {
        var allowed = await Project(Task(delegationAllowed: true));
        var refused = await Project(Task(delegationAllowed: false));

        Assert.True(allowed.DelegationAllowed);
        Assert.False(refused.DelegationAllowed);
    }

    /// <summary>
    /// NULL and EMPTY are different answers, and the projection keeps them apart. Null means nobody ever chose,
    /// so every dispatchable event is sent; empty means the owner chose none. Normalising either into the other
    /// would silence a task nobody configured, or claim a choice nobody made — the entity carries the same
    /// nullable for the same reason.
    /// </summary>
    [Fact]
    public async Task Never_chosen_and_chose_nothing_are_projected_differently()
    {
        var neverChosen = await Project(Task(notifyOn: null));
        var choseNothing = await Project(Task(notifyOn: []));

        Assert.Null(neverChosen.Notifications!.Events);
        Assert.Empty(choseNothing.Notifications!.Events!);
    }

    [Fact]
    public async Task The_chosen_events_and_the_master_switch_both_reach_the_wire()
    {
        var projected = await Project(Task(emailEnabled: false, notifyOn: ["task.assigned"]));

        Assert.False(projected.Notifications!.EmailEnabled);
        Assert.Equal(["task.assigned"], projected.Notifications.Events!);
    }

    [Fact]
    public async Task The_reminder_lead_reaches_the_wire_as_a_day_count()
    {
        Assert.Equal(3, (await Project(Task(reminderLeadDays: 3))).ReminderLeadDays);
        // No reminder asked for: omitted, not zero. Zero days is a real answer ("on the day"), so it cannot also
        // be how "nobody asked" is spelled.
        Assert.Null((await Project(Task(reminderLeadDays: null))).ReminderLeadDays);
    }

    private static TaskItem Task(
        bool delegationAllowed = false,
        bool emailEnabled = true,
        IReadOnlyList<string>? notifyOn = null,
        int? reminderLeadDays = null)
        => new()
        {
            TenantId = TaskTestData.Tenant,
            Title = "CT probe",
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = TaskTestData.Me,
            CreatedByUserId = TaskTestData.Me,
            OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = TaskLifecycle.InProgress,
            DelegationAllowed = delegationAllowed,
            EmailNotificationsEnabled = emailEnabled,
            NotifyOnEvents = notifyOn,
            ReminderLeadDays = reminderLeadDays,
            Version = 1
        };

    private static async Task<WorkItemProjectionDto> Project(
        TaskItem task,
        FakeTaskWatcherRepository? watchers = null,
        FakeUserDisplayNameResolver? names = null)
    {
        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            names ?? new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            new FakeTaskTransitionRepository(),
            new FakeTaskPersonalOverlayRepository(),
            watchers ?? new FakeTaskWatcherRepository(),
            TaskActors.PermitAll(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository());

        var actor = new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());
        return Assert.Single(await provider.GetWorkItemsAsync(actor, CancellationToken.None));
    }
}
