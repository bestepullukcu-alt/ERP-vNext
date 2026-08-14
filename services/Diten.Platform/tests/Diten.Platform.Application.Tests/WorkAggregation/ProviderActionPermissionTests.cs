using System.Reflection;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

/// <summary>
/// The permission keys that gate projected actions[] must be DECLARED by the provider that checks them.
///
/// <para>They used to be hardcoded in WorkItemsController, which collected only MOD-0023's four workflow keys.
/// When MOD-0024 arrived, every platform.tasks.* check in TaskWorkItemProvider consulted a set that never
/// contained them, so actor.Has(...) always returned false and every task action came back
/// enabled:false / PERMISSION_DENIED — while the endpoint itself happily accepted the call (proven live: the same
/// action returned 409, not 403).</para>
///
/// <para>Why no test caught it: every existing provider test used IsPlatformActor:true, which bypasses permission
/// evaluation entirely, or an actor with NO permissions. Nothing ever asserted the positive case — a tenant actor
/// who HOLDS the permission getting an enabled action. That is the first test below.</para>
/// </summary>
public sealed class ProviderActionPermissionTests
{
    // ── The missing positive case ─────────────────────────────────────────────

    [Fact]
    public async Task An_actor_holding_the_declared_permissions_gets_ENABLED_actions()
    {
        var provider = TaskProvider(SelfTask());

        var items = await provider.GetWorkItemsAsync(
            GrantedActor(provider.RequiredActionPermissions), CancellationToken.None);

        var actions = Assert.Single(items).Actions;
        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
        {
            Assert.True(action.Enabled, $"'{action.Code}' should be enabled for an actor holding every declared permission.");
            Assert.Null(action.DisabledReasonCode);
        });
    }

    [Fact]
    public async Task An_actor_holding_nothing_gets_DISABLED_actions_that_say_why()
    {
        var provider = TaskProvider(SelfTask());

        var items = await provider.GetWorkItemsAsync(EmptyActor(), CancellationToken.None);

        var actions = Assert.Single(items).Actions;
        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
        {
            Assert.False(action.Enabled);
            Assert.Equal(WorkAggregationReasonCodes.PermissionDenied, action.DisabledReasonCode);
        });
    }

    /// <summary>
    /// The declaration↔usage guard. If the provider consults a key it does not declare, granting exactly the
    /// declared set leaves that one action disabled — so this fails without naming individual keys, and keeps
    /// working as Phase 2–5 add actions.
    /// </summary>
    [Theory]
    [InlineData(TaskAssignmentTarget.SelfAssigned, TaskLifecycle.Open)]
    [InlineData(TaskAssignmentTarget.SelfAssigned, TaskLifecycle.InProgress)]
    [InlineData(TaskAssignmentTarget.Person, TaskLifecycle.Open)]
    public async Task Declaring_the_permissions_is_enough_to_enable_every_action(
        TaskAssignmentTarget target, TaskLifecycle lifecycle)
    {
        var task = SelfTask();
        task.AssignmentTarget = target;
        task.Lifecycle = lifecycle;

        var provider = TaskProvider(task);
        var items = await provider.GetWorkItemsAsync(
            GrantedActor(provider.RequiredActionPermissions), CancellationToken.None);

        var actions = Assert.Single(items).Actions;
        var stillDenied = actions
            .Where(a => a.DisabledReasonCode == WorkAggregationReasonCodes.PermissionDenied)
            .Select(a => a.Code)
            .ToList();

        Assert.Empty(stillDenied);
    }

    [Fact]
    public async Task A_pooled_task_claim_is_enabled_by_the_declared_claim_permission()
    {
        var provider = TaskProvider(PoolTask(), new FakePositionAssignmentRepository(Holder()));

        var items = await provider.GetWorkItemsAsync(
            GrantedActor([TaskPermissions.Claim]), CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("claim", item.PrimaryActionCode);
        Assert.True(Assert.Single(item.Actions, a => a.Code == "claim").Enabled);
    }

    // ── The architectural guarantee ───────────────────────────────────────────

    [Fact]
    public void Every_provider_in_the_assembly_declares_its_action_permissions()
    {
        var providerTypes = typeof(IWorkItemProvider).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IWorkItemProvider).IsAssignableFrom(t))
            .ToList();

        // Guard against a vacuous pass if the seam is ever renamed or moved.
        Assert.True(providerTypes.Count >= 2, "expected at least the workflow and task providers");

        foreach (var type in providerTypes)
        {
            var property = type.GetProperty(nameof(IWorkItemProvider.RequiredActionPermissions));
            Assert.NotNull(property);
        }
    }

    [Fact]
    public void The_task_provider_declares_exactly_the_task_permissions_it_consults()
    {
        var declared = TaskProvider(SelfTask()).RequiredActionPermissions;

        Assert.Contains(TaskPermissions.Update, declared);
        Assert.Contains(TaskPermissions.Claim, declared);
        Assert.Contains(TaskPermissions.Complete, declared);
        // Read/create/delete gate ENDPOINTS, not projected actions — declaring them here would ask the API layer
        // to evaluate claims it does not need.
        Assert.DoesNotContain(TaskPermissions.Read, declared);
        Assert.DoesNotContain(TaskPermissions.Create, declared);
    }

    /// <summary>
    /// MOD-0023 behaviour preservation: the four keys the controller used to hardcode are now declared by the
    /// workflow provider, byte for byte, and nothing was added or dropped.
    /// </summary>
    [Fact]
    public void The_workflow_provider_declares_the_same_four_keys_the_controller_used_to_hardcode()
    {
        var declared = WorkflowProviderDeclaration();

        Assert.Equal(
            new[]
            {
                WorkflowPermissions.TasksApprove,
                WorkflowPermissions.TasksReject,
                WorkflowPermissions.TasksRequestInfo,
                WorkflowPermissions.TasksDelegate
            }.OrderBy(k => k, StringComparer.Ordinal),
            declared.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void The_union_across_providers_covers_both_modules_without_duplicates()
    {
        // This is what WorkItemsController now evaluates.
        var union = WorkflowProviderDeclaration()
            .Concat(TaskProvider(SelfTask()).RequiredActionPermissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Contains(WorkflowPermissions.TasksApprove, union);
        Assert.Contains(TaskPermissions.Update, union);
        Assert.Equal(union.Count, union.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The declaration is a constant list, so the repository graph is irrelevant here — the constructor only
    /// assigns fields and nothing in this test queries approvals.
    /// </summary>
    private static IReadOnlyCollection<string> WorkflowProviderDeclaration()
        => new WorkflowApprovalWorkItemProvider(null!, null!, null!, null!).RequiredActionPermissions;

    private static TaskWorkItemProvider TaskProvider(
        TaskItem task,
        FakePositionAssignmentRepository? positionAssignments = null)
        => new(
            new FakeTaskItemRepository(task),
            positionAssignments ?? new FakePositionAssignmentRepository(),
            new Application.Features.Tasks.Services.TaskLifecycleService(),
            new Application.Features.Tasks.Services.TaskAssignmentResolver(),
            new Tasks.FakeUserDisplayNameResolver(),
            new Tasks.FakeChecklistRunRepository(), new Tasks.FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), new Tasks.FakeTaskPersonalOverlayRepository(), new Tasks.FakeTaskWatcherRepository(), TaskActors.PermitAll(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), Tasks.SlaForTests.Real(), new FakeTaskFieldDefinitionRepository());

    private static WorkItemActor GrantedActor(IEnumerable<string> permissions)
        => new(TaskTestData.Me, IsPlatformActor: false,
            new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase));

    private static WorkItemActor EmptyActor()
        => new(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>());

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

    private static TaskItem PoolTask()
    {
        var task = SelfTask();
        task.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        task.AssigneeUserId = null;
        task.PoolPositionId = PositionId;
        return task;
    }

    private static readonly Guid PositionId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static Domain.Entities.Organization.PositionAssignment Holder() => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = TaskTestData.Me,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        EffectiveTo = null
    };
}
