using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Providers;

/// <summary>
/// MOD-0024 — the SECOND Task Center provider (pack §12 K10). It plugs into the WC-1 <see cref="IWorkItemProvider"/>
/// seam alongside the MOD-0023 approval provider; WC-1's own code is untouched (only a DI line is added), which is
/// exactly what that seam exists for.
///
/// <para>READ-ONLY. The projection is derived from <see cref="ITaskLifecycleService"/> and
/// <see cref="ITaskAssignmentResolver"/> so the API and the Task Center can never disagree, and every emitted item
/// satisfies the executable contract (<c>fixture-contract.js</c> <c>validateWorkItem</c>).</para>
/// </summary>
public sealed class TaskWorkItemProvider : IWorkItemProvider
{
    // Must equal the module manifest's ModuleCode ("tasks") and the permission namespace (platform.tasks.*):
    // the Task Center resolves a work item's owning module by this code, so a provider-only alias like
    // "task-engine" leaves the item with no module the shell can name.
    private const string TaskProviderCode = WorkItemContract.ProviderCodeTasks;
    private const string NativeStatusKeyPrefix = "WorkAggregation_TaskStatus_";
    private const string ActionAcceptKey = "WorkAggregation_Action_Accept";
    private const string ActionClaimKey = "WorkAggregation_Action_Claim";
    private const string ActionStartKey = "WorkAggregation_Action_Start";
    private const string ActionCompleteKey = "WorkAggregation_Action_Complete";
    private const string ActionPlanKey = "WorkAggregation_Action_Plan";
    private const string ActionReleaseKey = "WorkAggregation_Action_Release";
    private const string ActionCancelKey = "WorkAggregation_Action_Cancel";
    private const string DisabledPermissionKey = "WorkAggregation_ActionDisabled_PermissionDenied";
    private const string DisabledApprovalKey = "WorkAggregation_ActionDisabled_ApprovalPending";
    private const string DisabledChecklistKey = "WorkAggregation_ActionDisabled_ChecklistIncomplete";

    private readonly ITaskItemRepository _tasks;
    private readonly IPositionAssignmentRepository _positionAssignments;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ITaskAssignmentResolver _assignmentResolver;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly IChecklistRunRepository _checklistRuns;

    public TaskWorkItemProvider(
        ITaskItemRepository tasks,
        IPositionAssignmentRepository positionAssignments,
        ITaskLifecycleService lifecycle,
        ITaskAssignmentResolver assignmentResolver,
        IUserDisplayNameResolver displayNames,
        IChecklistRunRepository checklistRuns)
    {
        _tasks = tasks;
        _positionAssignments = positionAssignments;
        _lifecycle = lifecycle;
        _assignmentResolver = assignmentResolver;
        _displayNames = displayNames;
        _checklistRuns = checklistRuns;
    }

    public string ProviderCode => TaskProviderCode;

    public string ProviderContractVersion => "1.0";

    /// <summary>
    /// The permissions BuildActions consults. Omitting one here makes its action unconditionally
    /// PERMISSION_DENIED, so this list and the actor.Has(...) calls must stay in step — TaskWorkItemProviderTests
    /// asserts exactly that.
    /// </summary>
    public IReadOnlyCollection<string> RequiredActionPermissions { get; } =
    [
        TaskPermissions.Update,     // accept + start
        TaskPermissions.Claim,      // claim a pooled task
        TaskPermissions.Complete,   // complete
        TaskPermissions.Cancel      // cancel
    ];

    public async Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(
        WorkItemActor actor,
        CancellationToken ct = default)
    {
        var mine = await _tasks.ListByAssigneeAsync(actor.UserId, ct);

        // Pool work is offered to positions, so the actor's active positions decide what they may see/claim.
        var positionIds = await ResolveActivePositionIdsAsync(actor.UserId, ct);
        var pooled = await _tasks.ListUnclaimedByPositionsAsync(positionIds, ct);

        var tasks = mine
            .Concat(pooled)
            .DistinctBy(t => t.Id)
            .ToList();

        // ONE batched resolve for the whole page — never one call per task, and cached between requests.
        // Best effort: if AuthService is down this comes back empty and names are simply omitted.
        var userIds = tasks
            .SelectMany(t => new[] { t.AssigneeUserId, t.CreatedByUserId })
            .Where(id => id is not null && id != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var displayNames = await _displayNames.ResolveAsync(userIds, ct);

        // Phase 2 containers, both batched: one read for every task's checklist and one for every task's
        // children. Per-task reads here would be an N+1 over the whole page.
        var taskIds = tasks.Select(t => t.Id).ToList();
        var checklistByTask = (await _checklistRuns.ListByTaskIdsAsync(taskIds, ct))
            .GroupBy(run => run.TaskItemId)
            .ToDictionary(group => group.Key, group => group.First());

        // Only TOP-LEVEL tasks can have children (one level only), so nothing else needs asking about.
        var parentIds = tasks.Where(t => t.ParentTaskItemId is null).Select(t => t.Id).ToList();
        var childrenByParent = (await _tasks.ListByParentsAsync(parentIds, ct))
            .Where(child => child.ParentTaskItemId is not null)
            .GroupBy(child => child.ParentTaskItemId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TaskItem>)group.ToList());

        return tasks
            .Select(t => Project(
                t,
                actor,
                displayNames,
                checklistByTask.GetValueOrDefault(t.Id),
                childrenByParent.GetValueOrDefault(t.Id, [])))
            .ToList();
    }

    private WorkItemProjectionDto Project(
        TaskItem task,
        WorkItemActor actor,
        IReadOnlyDictionary<Guid, string> displayNames,
        ChecklistRun? checklist,
        IReadOnlyList<TaskItem> children)
    {
        var assignment = _assignmentResolver.Resolve(task);
        var normalized = _lifecycle.ToNormalizedStatus(task);
        var waiting = _lifecycle.ResolveWaitingContext(task);
        var terminal = _lifecycle.IsTerminal(task);

        // A terminal task exposes NO state-changing action (contract rule), so placement is empty too.
        // A blocking checklist item makes completion unavailable. Shown DISABLED with the reason rather than
        // hidden — and the server refuses the write too, so this is a hint, never the enforcement.
        var checklistBlocks = ChecklistBlocksCompletion(checklist);

        var (actions, primaryActionCode, overflowActionCodes) = terminal
            ? ([], null, (IReadOnlyList<string>)[])
            : BuildActions(task, actor, checklistBlocks);

        return new WorkItemProjectionDto(
            FixtureKind: WorkItemContract.FixtureKindWorkItem,
            Id: task.Id.ToString(),
            // A real operational task — unlike the approval provider, this one HAS a task lifecycle.
            WorkIntent: "task",
            AssignmentMode: assignment.AssignmentMode,
            OwnershipState: assignment.OwnershipState,
            AdmissionState: assignment.AdmissionState,
            NormalizedStatus: normalized,
            TaskLifecycle: task.Lifecycle.ToString(),
            ExecutionState: ResolveExecutionState(task),
            TimerState: WorkItemContract.NotApplicable,
            SystemState: WorkItemContract.SystemFresh,
            ActionDepth: WorkItemContract.DepthInline,
            // A DISPLAY label, not a resource one: unlike MOD-0023's ApprovalTask, a TaskItem carries a real
            // Title the user typed. Text a person wrote needs no translation, and routing it through a resource
            // key made the Task Center render the raw key "WorkAggregation_Title_Task" when no resx entry
            // existed. Locale is "und" because the language the title was typed in is not recorded.
            Title: WorkItemLabelDto.Display(task.Title),
            NativeStatus: new WorkItemNativeStatusDto(
                task.Lifecycle.ToString(),
                WorkItemLabelDto.Resource(NativeStatusKeyPrefix + task.Lifecycle)),
            Source: new WorkItemSourceDto(
                ProviderCode: TaskProviderCode,
                ProviderContractVersion: ProviderContractVersion,
                ObjectType: "task",
                ObjectId: task.Id.ToString(),
                // MOD-0024 owns its own detail surface, so it can supply a real deep link.
                DeepLink: $"/Tasks/{task.Id}"),
            // MOD-0024 IS the lifecycle owner here (unlike a workflow-gated business object).
            LifecycleOwner: TaskProviderCode,
            WorkItemCapabilities: ResolveCapabilities(task, checklist, children),
            Actions: actions,
            Concurrency: new WorkItemConcurrencyDto("version", task.Version.ToString()),
            WaitingContext: waiting is null
                ? null
                : new WorkItemWaitingContextDto(waiting.Type, waiting.WaitingOn, waiting.Since, waiting.ExpectedUntil),
            Escalation: null,
            DueAt: task.DueAt,
            PrimaryActionCode: primaryActionCode,
            OverflowActionCodes: overflowActionCodes,
            // An unclaimed pool task genuinely has no assignee — omit rather than invent one.
            Assignee: Person(task.AssigneeUserId, actor, displayNames),
            Requester: Person(task.CreatedByUserId, actor, displayNames),
            // Container ⇔ capability, both directions: emitted only when declared, and then even if empty.
            Checklist: checklist is null ? null : ToChecklist(checklist),
            Subtasks: task.ParentTaskItemId is null ? ToSubtasks(children) : null,
            ParentTaskItemId: task.ParentTaskItemId?.ToString());
    }

    /// <summary>
    /// A person reference for the projection. The id is always real; the display NAME is left null because Platform
    /// has no user-directory seam to resolve an AuthService user (the task's AssigneeUserId/CreatedByUserId are
    /// AuthService identities — MOD-0288's PersonReference has no UserId, so it cannot supply the name either).
    /// <c>IsCurrentUser</c> is the one thing the server can state for certain, letting the client render "Me"
    /// without any localized text crossing the wire.
    /// </summary>
    private static WorkItemPersonDto? Person(
        Guid? userId,
        WorkItemActor actor,
        IReadOnlyDictionary<Guid, string> displayNames)
    {
        if (userId is null || userId == Guid.Empty)
        {
            return null;
        }

        // An unresolved name stays NULL (and is omitted on the wire) rather than falling back to the id: a GUID
        // is not a person's name. The client shows "Me" for the caller and a name-unavailable label otherwise.
        var resolved = displayNames.TryGetValue(userId.Value, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : null;

        return new WorkItemPersonDto(
            userId.Value.ToString(),
            DisplayName: resolved,
            IsCurrentUser: userId == actor.UserId);
    }

    /// <summary>
    /// Declared capabilities gate which detail blocks render. Phase 1 declares only what actually exists:
    /// planning/execution plus businessContext when configurable values are present. Checklist/subtasks arrive
    /// with Phase 2 — declaring them now would render empty blocks.
    /// </summary>
    private static IReadOnlyList<string> ResolveCapabilities(
        TaskItem task,
        ChecklistRun? checklist,
        IReadOnlyList<TaskItem> children)
    {
        var capabilities = new List<string> { "planning", "execution" };
        if (task.FieldValues.Count > 0)
        {
            capabilities.Add("businessContext");
        }

        // Declared only when the container will be emitted — the contract rejects either half alone.
        if (checklist is not null)
        {
            capabilities.Add("checklist");
        }

        // A subtask cannot have subtasks, so it never declares the capability; a parent always may, even with
        // no children yet, because the shell offers "add a subtask" there.
        if (task.ParentTaskItemId is null)
        {
            capabilities.Add("subtasks");
        }

        return capabilities;
    }

    /// <summary>
    /// Checklist items keep the label FORM they were authored with: a template item stays a resource key so it
    /// localizes, an ad-hoc item the user typed becomes display text. Emitting a resource label for typed text is
    /// what puts a raw key on screen.
    /// </summary>
    private static WorkItemChecklistDto ToChecklist(ChecklistRun run)
        => new(run.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => new WorkItemChecklistItemDto(
                Id: item.Code,
                Label: item.LabelResourceKey is { Length: > 0 } key
                    ? WorkItemLabelDto.Resource(key)
                    : WorkItemLabelDto.Display(item.LabelText ?? string.Empty),
                Completed: item.Completed,
                Required: item.Requirement != ChecklistItemRequirement.Optional,
                Blocking: item.Requirement == ChecklistItemRequirement.Blocking,
                EvidenceRequired: item.EvidenceRequired))
            .ToList(),
            Version: run.Version);

    /// <summary>
    /// Subtasks in the contract's own vocabulary. MOD-0024 is their source, so the mode is `full`: they are
    /// created and completed here rather than deep-linked elsewhere.
    /// </summary>
    private static WorkItemSubtasksDto ToSubtasks(IReadOnlyList<TaskItem> children)
        => new("full", children
            .Select(child => new WorkItemSubtaskDto(
                Id: child.Id.ToString(),
                Title: child.Title,
                Status: child.Lifecycle switch
                {
                    TaskLifecycle.Done => "done",
                    TaskLifecycle.InProgress or TaskLifecycle.PendingReview or TaskLifecycle.Waiting => "in-progress",
                    _ => "not-started"
                }))
            .ToList());

    private static string ResolveExecutionState(TaskItem task) => task.Lifecycle switch
    {
        TaskLifecycle.InProgress => "active",
        TaskLifecycle.Waiting or TaskLifecycle.PendingReview => "paused",
        _ => "notStarted"
    };

    /// <summary>
    /// The single authoritative actions[] for this task. Eligibility is resolved HERE (server-side): the browser
    /// renders what it is given and invents nothing.
    /// </summary>
    /// <remarks>
    /// Covers every Phase-1 command the API exposes (accept · claim · release · plan · start · complete · cancel)
    /// so the row's overflow menu is not empty. Each entry is gated on BOTH the lifecycle/ownership rule the server
    /// enforces (TaskLifecycleService.CanTransition) and the permission its endpoint requires, so a projected
    /// action cannot be one the server would refuse.
    ///
    /// <para>Phase 2+ commands (pause/resume/logTime) and Phase 3 (requestInfo/signoff/return) are deliberately
    /// absent: projecting an action with no endpoint behind it is how the mock era misled users.</para>
    /// </remarks>
    /// <summary>
    /// Mirrors <c>ITaskChecklistService.BlocksCompletion</c>. Only <c>Blocking</c> items gate completion; a
    /// <c>Required</c> item is an expectation.
    /// </summary>
    private static bool ChecklistBlocksCompletion(ChecklistRun? checklist)
        => checklist is not null
           && checklist.Items.Any(i => i.Requirement == ChecklistItemRequirement.Blocking && !i.Completed);

    private static (IReadOnlyList<WorkItemActionDto> Actions, string? Primary, IReadOnlyList<string> Overflow)
        BuildActions(TaskItem task, WorkItemActor actor, bool checklistBlocks)
    {
        var actions = new List<WorkItemActionDto>();
        string? primary = null;

        var isPool = task.AssignmentTarget == TaskAssignmentTarget.PositionPool;
        var unclaimed = isPool && task.AssigneeUserId is null;
        var openOrPlanned = task.Lifecycle is TaskLifecycle.Open or TaskLifecycle.Planned;
        // An approval-gated task is visible but not startable — MOD-0023 must release it first (pack §12 K2).
        var approvalPending = task.ApprovalRequired && openOrPlanned;

        if (unclaimed)
        {
            // Nobody holds it yet, so claiming is the only way to move it forward.
            actions.Add(Build("claim", ActionClaimKey, actor.Has(TaskPermissions.Claim)));
            primary = "claim";
        }
        else if (task.AssignmentTarget == TaskAssignmentTarget.Person && openOrPlanned)
        {
            // The acceptance gate: an assignee decides whether to take the work on.
            actions.Add(Build("accept", ActionAcceptKey, actor.Has(TaskPermissions.Update)));
            primary = "accept";
        }
        else if (approvalPending)
        {
            // Shown DISABLED rather than hidden, so the reason is visible instead of the button vanishing.
            actions.Add(Disabled("start", ActionStartKey, TaskReasonCodes.InvalidState, DisabledApprovalKey));
            primary = "start";
        }
        else
        {
            if (openOrPlanned)
            {
                actions.Add(Build("start", ActionStartKey, actor.Has(TaskPermissions.Update)));
                primary = "start";
            }

            if (task.Lifecycle is TaskLifecycle.InProgress)
            {
                actions.Add(checklistBlocks
                    ? Disabled("complete", ActionCompleteKey,
                        TaskReasonCodes.ChecklistIncomplete, DisabledChecklistKey)
                    : Build("complete", ActionCompleteKey, actor.Has(TaskPermissions.Complete),
                        requiresConfirmation: true));
                primary = "complete";
            }
        }

        // Planning a personal date is available while the work has not started (Open ⇄ Planned on the server).
        if (openOrPlanned && !unclaimed)
        {
            actions.Add(Build("plan", ActionPlanKey, actor.Has(TaskPermissions.Update)));
        }

        // Only a pooled task that someone has taken can be handed back to the pool.
        if (isPool && !unclaimed)
        {
            actions.Add(Build("release", ActionReleaseKey, actor.Has(TaskPermissions.Claim),
                requiresConfirmation: true));
        }

        // Cancellation is allowed from every non-terminal state (CanTransition), and this method is only reached
        // for non-terminal tasks — see the call site.
        actions.Add(Build("cancel", ActionCancelKey, actor.Has(TaskPermissions.Cancel),
            requiresConfirmation: true, riskLevel: "destructive"));

        // Everything that is not the primary belongs in the overflow menu, in the order built above.
        var overflow = actions
            .Select(action => action.Code)
            .Where(code => code != primary)
            .ToList();

        return (actions, primary, overflow);
    }

    private static WorkItemActionDto Build(
        string code,
        string labelKey,
        bool permitted,
        bool requiresConfirmation = false,
        string riskLevel = "normal")
        => permitted
            ? new WorkItemActionDto(
                Code: code,
                Label: WorkItemLabelDto.Resource(labelKey),
                SemanticType: code,
                Enabled: true,
                Source: WorkItemContract.ActionSourceProvider,
                DisabledReasonCode: null,
                DisabledReason: null,
                RequiresConfirmation: requiresConfirmation,
                RequiresReason: false,
                RequiresEvidence: false,
                SupportsBulk: false,
                RiskLevel: riskLevel)
            : Disabled(code, labelKey, WorkAggregationReasonCodes.PermissionDenied, DisabledPermissionKey);

    private static WorkItemActionDto Disabled(string code, string labelKey, string reasonCode, string reasonKey)
        => new(
            Code: code,
            Label: WorkItemLabelDto.Resource(labelKey),
            SemanticType: code,
            Enabled: false,
            Source: WorkItemContract.ActionSourceProvider,
            DisabledReasonCode: reasonCode,
            DisabledReason: WorkItemLabelDto.Resource(reasonKey),
            RequiresConfirmation: false,
            RequiresReason: false,
            RequiresEvidence: false,
            SupportsBulk: false,
            RiskLevel: "normal");

    private async Task<IReadOnlyList<Guid>> ResolveActivePositionIdsAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var assignments = await _positionAssignments.GetAllAsync(ct);
        return assignments
            .Where(a => a.UserId == userId
                        && !a.IsCancelled
                        && a.EffectiveFrom <= now
                        && (a.EffectiveTo is null || a.EffectiveTo > now))
            .Select(a => a.PositionId)
            .Distinct()
            .ToList();
    }
}
