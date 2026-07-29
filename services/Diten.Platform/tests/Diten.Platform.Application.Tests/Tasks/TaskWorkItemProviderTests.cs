using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 — the Task Center projection. The executable contract (fixture-contract.js validateWorkItem) is the
// authority, so every emitted item is checked against its invariants here: a projection that fails them renders a
// broken row in the browser.
public sealed class TaskWorkItemProviderTests
{
    private static readonly Guid PositionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Projects_my_own_task_as_a_contract_valid_work_item()
    {
        var task = SelfTask();
        var items = await Provider(new FakeTaskItemRepository(task)).GetWorkItemsAsync(Actor(), CancellationToken.None);

        var item = Assert.Single(items);
        AssertContractConformant(item);
        Assert.Equal("task", item.WorkIntent);
        Assert.Equal("direct", item.AssignmentMode);
        Assert.Equal("owned", item.OwnershipState);
        Assert.Equal("admitted", item.AdmissionState);
        // MOD-0024 is the source, so it owns the lifecycle (unlike a workflow-gated business object).
        // The code is "tasks", matching the manifest ModuleCode and platform.tasks.* — a provider-only alias
        // left the Task Center unable to resolve the owning module.
        Assert.Equal(WorkItemContract.ProviderCodeTasks, item.LifecycleOwner);
        Assert.Equal(WorkItemContract.ProviderCodeTasks, item.Source.ProviderCode);
        Assert.Equal("/Tasks/" + task.Id, item.Source.DeepLink);
    }

    [Fact]
    public async Task Projects_an_unclaimed_pool_task_with_a_claim_action()
    {
        var task = PoolTask();
        var provider = Provider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(Holder(TaskTestData.Me)));

        var items = await provider.GetWorkItemsAsync(Actor(), CancellationToken.None);

        var item = Assert.Single(items);
        AssertContractConformant(item);
        Assert.Equal("groupQueue", item.AssignmentMode);
        Assert.Equal("unowned", item.OwnershipState);
        Assert.Equal("pendingClaim", item.AdmissionState);
        Assert.Equal("claim", item.PrimaryActionCode);
        var action = Assert.Single(item.Actions, a => a.Code == "claim");
        Assert.True(action.Enabled);
        // Cancel is offered too (allowed from every non-terminal state) — but claim stays the primary.
        Assert.Contains(item.Actions, a => a.Code == "cancel");
    }

    [Fact]
    public async Task An_assigned_but_unaccepted_task_offers_only_accept()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.Lifecycle = TaskLifecycle.Open;

        var items = await Provider(new FakeTaskItemRepository(task)).GetWorkItemsAsync(Actor(), CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("pendingAcceptance", item.AdmissionState);
        // Acceptance is the primary decision; planning/cancelling remain available behind it.
        Assert.Equal("accept", item.PrimaryActionCode);
        Assert.Contains(item.Actions, a => a.Code == "accept");
        Assert.DoesNotContain(item.Actions, a => a.Code == "start");
    }

    [Fact]
    public async Task An_approval_gated_task_shows_start_DISABLED_rather_than_hiding_it()
    {
        var task = SelfTask();
        task.ApprovalRequired = true;
        task.ApprovalManagerUserId = TaskTestData.Rival;

        var items = await Provider(new FakeTaskItemRepository(task)).GetWorkItemsAsync(Actor(), CancellationToken.None);

        var item = Assert.Single(items);
        // Waiting on the approver — and the pair must be present per the contract.
        Assert.Equal("Waiting", item.NormalizedStatus);
        Assert.NotNull(item.WaitingContext);

        // START is shown but blocked, with the reason visible — MOD-0023 must release the approval first.
        var action = Assert.Single(item.Actions, a => a.Code == "start");
        Assert.False(action.Enabled);
        Assert.NotNull(action.DisabledReasonCode);
        Assert.NotNull(action.DisabledReason);
        Assert.Equal("start", item.PrimaryActionCode);
        // Planning and cancelling are unaffected by a pending approval (the server allows both from Open).
        Assert.Contains(item.Actions, a => a.Code == "plan" && a.Enabled);
        Assert.Contains(item.Actions, a => a.Code == "cancel" && a.Enabled);
        AssertContractConformant(item);
    }

    [Fact]
    public async Task Without_permission_the_action_is_disabled_with_a_reason_not_omitted()
    {
        var task = SelfTask();
        var items = await Provider(new FakeTaskItemRepository(task))
            .GetWorkItemsAsync(ActorWithoutPermissions(), CancellationToken.None);

        // EVERY projected action is disabled for an actor holding nothing, and each says why rather than
        // silently disappearing.
        var actions = Assert.Single(items).Actions;
        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
        {
            Assert.False(action.Enabled);
            Assert.Equal(WorkAggregationReasonCodes.PermissionDenied, action.DisabledReasonCode);
        });
    }

    [Theory]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task A_terminal_task_exposes_no_enabled_action(TaskLifecycle lifecycle)
    {
        var task = SelfTask();
        task.Lifecycle = lifecycle;
        task.CompletedAt = lifecycle == TaskLifecycle.Done ? DateTimeOffset.UtcNow : null;
        task.CancelledAt = lifecycle == TaskLifecycle.Cancelled ? DateTimeOffset.UtcNow : null;

        var items = await Provider(new FakeTaskItemRepository(task)).GetWorkItemsAsync(Actor(), CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Empty(item.Actions);
        AssertContractConformant(item);
    }

    [Fact]
    public async Task Capabilities_are_only_declared_when_the_data_exists()
    {
        var bare = SelfTask();
        var withFields = SelfTask();
        withFields.FieldValues.Add(new TaskFieldValue
        {
            DefinitionCode = "regulatory.phase",
            ValueType = TaskFieldValueType.Text,
            Value = "Phase 1"
        });

        var bareItem = Assert.Single(
            await Provider(new FakeTaskItemRepository(bare)).GetWorkItemsAsync(Actor(), CancellationToken.None));
        var fieldItem = Assert.Single(
            await Provider(new FakeTaskItemRepository(withFields)).GetWorkItemsAsync(Actor(), CancellationToken.None));

        // Declaring a capability with no data renders an empty block, so it must not be declared.
        Assert.DoesNotContain("businessContext", bareItem.WorkItemCapabilities);
        Assert.Contains("businessContext", fieldItem.WorkItemCapabilities);

        // Phase 2: `checklist` follows the DATA (no run → not declared), but `subtasks` follows the task's
        // POSITION in the hierarchy — a top-level task can always be given children, so the container is offered
        // even while empty. Both directions are asserted in TaskChecklistSubtaskTests.
        Assert.DoesNotContain("checklist", fieldItem.WorkItemCapabilities);
        Assert.Null(fieldItem.Checklist);
        Assert.Contains("subtasks", fieldItem.WorkItemCapabilities);
        Assert.NotNull(fieldItem.Subtasks);
    }

    [Fact]
    public async Task Another_tenants_task_is_never_projected()
    {
        // The repository double mirrors the tenant execution filter: a row belonging to another tenant is simply
        // not returned, so the projection is empty rather than leaking a foreign task. TenantId is init-only, so
        // the foreign row is constructed foreign — which is also how the real entity behaves.
        var foreign = new TaskItem
        {
            TenantId = TaskTestData.OtherTenant,
            Title = "Another tenant's task",
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = TaskTestData.Me,
            OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = TaskLifecycle.Open,
            Version = 1
        };

        var items = await Provider(new FakeTaskItemRepository(foreign))
            .GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.Empty(items);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    // ── Cancelling belongs to the requester, not to whoever was handed the work ──────────────────────────────

    /*
     * The defect: `cancel` was projected unconditionally, so being ASSIGNED a task was enough to call the whole
     * thing off. That is the wrong half of the return/cancel split — an assignee who does not want the work
     * returns it; the person who asked for it cancels it.
     */
    [Fact]
    public async Task An_assignee_who_did_not_create_the_task_is_not_offered_cancel()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.CreatedByUserId = TaskTestData.Rival;   // someone else asked for this work

        var items = await Provider(new FakeTaskItemRepository(task))
            .GetWorkItemsAsync(AssigneeActor(), CancellationToken.None);

        var item = Assert.Single(items);
        AssertContractConformant(item);
        Assert.DoesNotContain(item.Actions, a => a.Code == "cancel");
        // The work is still actionable — this removes one action, it does not strand the assignee.
        Assert.Contains(item.Actions, a => a.Code == "accept");
    }

    [Fact]
    public async Task The_creator_is_offered_cancel()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.CreatedByUserId = TaskTestData.Me;      // I asked for it, so I may call it off

        var items = await Provider(new FakeTaskItemRepository(task))
            .GetWorkItemsAsync(AssigneeActor(), CancellationToken.None);

        var item = Assert.Single(items);
        AssertContractConformant(item);
        var cancel = Assert.Single(item.Actions, a => a.Code == "cancel");
        Assert.True(cancel.Enabled);
    }

    [Fact]
    public async Task Administrative_authority_may_cancel_someone_elses_task()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.Person;
        task.CreatedByUserId = TaskTestData.Rival;

        var admin = new WorkItemActor(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>(
            new[] { TaskPermissions.Update, TaskPermissions.Cancel, TaskPermissions.Delete },
            StringComparer.OrdinalIgnoreCase));

        var items = await Provider(new FakeTaskItemRepository(task))
            .GetWorkItemsAsync(admin, CancellationToken.None);

        Assert.Contains(Assert.Single(items).Actions, a => a.Code == "cancel");
    }

    // ── Waiting is no longer a dead end ─────────────────────────────────────────────────────────────────────

    /*
     * Waiting was legal in the transition matrix but nothing projected an action for it, so a task that got there
     * could never come back. The action is the EXISTING start endpoint under a resume label — the code doubles as
     * the URL segment on the client, so emitting "resume" would post to an endpoint that does not exist.
     */
    [Fact]
    public async Task A_waiting_task_offers_resume_using_the_start_endpoint()
    {
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.Waiting;

        var items = await Provider(new FakeTaskItemRepository(task))
            .GetWorkItemsAsync(Actor(), CancellationToken.None);

        var item = Assert.Single(items);
        AssertContractConformant(item);
        Assert.Equal("Waiting", item.NormalizedStatus);
        Assert.Equal("start", item.PrimaryActionCode);

        var resume = Assert.Single(item.Actions, a => a.Code == "start");
        Assert.True(resume.Enabled);
        // Same endpoint, different word: the label is the only thing that changes.
        Assert.Equal("WorkAggregation_Action_Resume", resume.Label.Key);
    }

    [Fact]
    public async Task Resume_is_disabled_with_a_visible_reason_while_approval_is_outstanding()
    {
        var instanceId = Guid.Parse("abcdabcd-abcd-abcd-abcd-abcdabcdabcd");
        var task = SelfTask();
        task.Lifecycle = TaskLifecycle.Waiting;
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = instanceId;

        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService().Pending(instanceId), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository());

        var items = await provider.GetWorkItemsAsync(Actor(), CancellationToken.None);

        var item = Assert.Single(items);
        AssertContractConformant(item);
        var resume = Assert.Single(item.Actions, a => a.Code == "start");
        Assert.False(resume.Enabled);
        // Disabled WITH the reason, never hidden — the server refuses it too (TaskApprovalHttpContractTests).
        Assert.Equal(TaskReasonCodes.ApprovalPending, resume.DisabledReasonCode);
    }

    private static WorkItemActor AssigneeActor() => new(
        TaskTestData.Me,
        IsPlatformActor: false,
        new HashSet<string>(
            new[] { TaskPermissions.Update, TaskPermissions.Complete, TaskPermissions.Cancel },
            StringComparer.OrdinalIgnoreCase));

    private static TaskWorkItemProvider Provider(
        FakeTaskItemRepository tasks,
        FakePositionAssignmentRepository? positionAssignments = null)
        => new(tasks,
            positionAssignments ?? new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(), new FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository());

    private static WorkItemActor Actor() => new(
        TaskTestData.Me,
        IsPlatformActor: true,
        new HashSet<string>());

    private static WorkItemActor ActorWithoutPermissions() => new(
        TaskTestData.Me,
        IsPlatformActor: false,
        new HashSet<string>());

    private static TaskItem SelfTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Write the report",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static TaskItem PoolTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Pooled inspection",
        AssignmentTarget = TaskAssignmentTarget.PositionPool,
        PoolPositionId = PositionId,
        AssigneeUserId = null,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static PositionAssignment Holder(Guid userId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
    };

    /// <summary>The fixture-contract.js invariant set, replicated for the backend projection.</summary>
    private static void AssertContractConformant(WorkItemProjectionDto dto)
    {
        Assert.Equal(WorkItemContract.FixtureKindWorkItem, dto.FixtureKind);
        Assert.False(string.IsNullOrWhiteSpace(dto.Id));

        string[] intents = ["task", "approval", "review", "issue", "exception"];
        string[] modes = ["direct", "approval", "groupQueue", "offered"];
        string[] ownership = ["unowned", "assigned", "owned", "notApplicable"];
        string[] admission = ["pendingAcceptance", "pendingClaim", "pendingOffer", "admitted", "notApplicable"];
        string[] statuses = ["Pending", "InProgress", "Waiting", "Done", "Cancelled"];
        string[] lifecycles =
            ["Open", "Planned", "InProgress", "Waiting", "PendingReview", "Done", "Cancelled", "notApplicable"];
        string[] executions = ["notStarted", "active", "paused", "notApplicable"];
        string[] timers = ["inactive", "running", "paused", "notApplicable"];
        string[] systems =
            ["fresh", "stale", "sourceUnavailable", "authorityEnded", "processing", "reconciliationRequired"];
        string[] depths = ["inline", "deeplink"];
        string[] capabilities =
        [
            "planning", "execution", "timeTracking", "checklist", "subtasks", "dependencies",
            "attachments", "evidence", "activity", "processStages", "businessContext", "relatedRecords"
        ];

        Assert.Contains(dto.WorkIntent, intents);
        Assert.Contains(dto.AssignmentMode, modes);
        Assert.Contains(dto.OwnershipState, ownership);
        Assert.Contains(dto.AdmissionState, admission);
        Assert.Contains(dto.NormalizedStatus, statuses);
        Assert.Contains(dto.TaskLifecycle, lifecycles);
        Assert.Contains(dto.ExecutionState, executions);
        Assert.Contains(dto.TimerState, timers);
        Assert.Contains(dto.SystemState, systems);
        Assert.Contains(dto.ActionDepth, depths);
        Assert.All(dto.WorkItemCapabilities, c => Assert.Contains(c, capabilities));

        // A task intent MUST carry a real lifecycle (notApplicable is for non-task intents).
        Assert.NotEqual("notApplicable", dto.TaskLifecycle);

        // Waiting ⇔ waitingContext, enforced bidirectionally by the contract.
        Assert.Equal(dto.NormalizedStatus == "Waiting", dto.WaitingContext is not null);

        // source is required and complete.
        Assert.False(string.IsNullOrWhiteSpace(dto.Source.ProviderCode));
        Assert.False(string.IsNullOrWhiteSpace(dto.Source.ProviderContractVersion));
        Assert.False(string.IsNullOrWhiteSpace(dto.Source.ObjectType));
        Assert.False(string.IsNullOrWhiteSpace(dto.Source.ObjectId));

        // The TITLE is a display label: a TaskItem owns a real user-entered Title, and text a person typed needs
        // no translation. (This assertion previously demanded the resource form, which is what made the Task
        // Center render the raw key "WorkAggregation_Title_Task" — see TaskWorkItemProviderWireContractTests.)
        Assert.Equal(WorkItemContract.LabelDisplay, dto.Title.Kind);
        Assert.False(string.IsNullOrWhiteSpace(dto.Title.Text));
        Assert.Null(dto.Title.Key);

        // System-owned strings stay resource labels so they localize in all seven languages.
        Assert.Equal(WorkItemContract.LabelResource, dto.NativeStatus.Label.Kind);
        Assert.False(string.IsNullOrWhiteSpace(dto.NativeStatus.Label.Key));
        Assert.Null(dto.NativeStatus.Label.Text);
        Assert.False(string.IsNullOrWhiteSpace(dto.NativeStatus.Code));

        // actions: unique codes; a disabled action must explain itself; every action names its source.
        Assert.Equal(dto.Actions.Select(a => a.Code).Distinct().Count(), dto.Actions.Count);
        foreach (var action in dto.Actions)
        {
            Assert.False(string.IsNullOrWhiteSpace(action.Source));
            Assert.Equal(WorkItemContract.LabelResource, action.Label.Kind);
            if (!action.Enabled)
            {
                Assert.False(string.IsNullOrWhiteSpace(action.DisabledReasonCode));
                Assert.NotNull(action.DisabledReason);
            }
        }

        // A terminal item exposes no enabled state-changing action.
        if (dto.NormalizedStatus is "Done" or "Cancelled")
        {
            Assert.DoesNotContain(dto.Actions, a => a.Enabled);
        }

        // Exactly one projection-level concurrency token.
        Assert.NotNull(dto.Concurrency);
        Assert.False(string.IsNullOrWhiteSpace(dto.Concurrency.Token));
    }
}
