using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Services;
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
    /// <summary>
    /// Label for resuming a task that was parked in <see cref="TaskLifecycle.Waiting"/>. It is a LABEL only — the
    /// action code stays <c>start</c>, because the client turns the code straight into the endpoint segment
    /// (<c>POST /api/v1/tasks/{id}/{code}</c>) and no <c>resume</c> endpoint exists or is needed.
    /// </summary>
    private const string ActionResumeKey = "WorkAggregation_Action_Resume";
    /// <summary>Label for parking a task in Waiting. Code and endpoint are both <c>inquire</c>.</summary>
    private const string ActionInquireKey = "WorkAggregation_Action_Inquire";

    /// <summary>Faz 3b — hand finished work to a reviewer. The code doubles as the URL segment.</summary>
    private const string ActionSubmitReviewKey = "WorkAggregation_Action_SubmitReview";

    /// <summary>Heading for values whose definition declares no section, or cannot be read at all.</summary>
    private const string SectionUnfiledKey = "WorkAggregation_BusinessContext_Unfiled";

    /// <summary>Label for a value whose definition has vanished — never its raw code.</summary>
    private const string FieldUnknownKey = "WorkAggregation_BusinessContext_UnknownField";
    /// <summary>Give assigned work back to whoever asked for it. Code and endpoint are both <c>return</c>.</summary>
    private const string ActionReturnKey = "WorkAggregation_Action_Return";
    /// <summary>Hand work to a different person. Code and endpoint are both <c>reassign</c>.</summary>
    private const string ActionReassignKey = "WorkAggregation_Action_Reassign";
    private const string DisabledPermissionKey = "WorkAggregation_ActionDisabled_PermissionDenied";
    private const string DisabledApprovalKey = "WorkAggregation_ActionDisabled_ApprovalPending";
    private const string DisabledChecklistKey = "WorkAggregation_ActionDisabled_ChecklistIncomplete";

    /// <summary>
    /// The task itself forbids delegation (<c>DelegationAllowed = false</c>). The SAME key shape as its five
    /// siblings above — a reason a reader can act on, not a new vocabulary.
    /// </summary>
    private const string DisabledDelegationKey = "WorkAggregation_ActionDisabled_DelegationNotAllowed";
    /// <summary>An unfinished predecessor. The BLOCKER carries which task and which edge; this is the button's own reason.</summary>
    private const string DisabledDependencyKey = "WorkAggregation_ActionDisabled_DependencyBlocked";
    /// <summary>An open subtask. Same shape as above: the blocker names which child, this is the button's reason.</summary>
    private const string DisabledSubtaskKey = "WorkAggregation_ActionDisabled_SubtaskBlocked";
    // `complete` needs its OWN wording: "waiting for approval, cannot be started" is wrong on a task already
    // in progress, and the server refuses Done for the same reason it refuses InProgress.
    private const string DisabledApprovalCompleteKey = "WorkAggregation_ActionDisabled_ApprovalPendingComplete";

    /// <summary>Faz 3b — the work is with a reviewer, so completion is not the holder's to press yet.</summary>
    private const string DisabledReviewCompleteKey = "WorkAggregation_ActionDisabled_ReviewPending";
    // An approval that never STARTED is a different fact from one that is running, and the user can act on it
    // (retry the save) instead of waiting for an approver who was never asked.
    private const string DisabledApprovalStartFailedKey = "WorkAggregation_ApprovalError_StartFailed";

    private const string GateNotRequired = "notRequired";
    private const string GateRequired = "required";
    private const string GatePending = "pending";
    private const string GateApproved = "approved";
    private const string GateRejected = "rejected";

    private readonly ITaskItemRepository _tasks;
    private readonly ITaskSeatDirectory _seats;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ITaskAssignmentResolver _assignmentResolver;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly IChecklistRunRepository _checklistRuns;
    private readonly ITaskApprovalService _approvals;
    private readonly ITaskDependencyRepository _dependencies;
    private readonly ITaskCommentRepository _comments;

    /// <summary>
    /// WC-1 — the lifecycle event log. REQUIRED, not optional like the team resolver: an absent team resolver can
    /// only narrow a scope, while an absent history would publish a feed that looks complete and is not.
    /// </summary>
    private readonly ITaskTransitionRepository _transitions;

    /// <summary>
    /// WC-1 — the READER'S OWN overlay (notes + snooze). Read for <c>actor.UserId</c> and nobody else: this is the
    /// one container here whose contents differ per viewer, so the user id is part of the query rather than a
    /// filter applied afterwards. There is no code path in this class that can assemble another person's overlay.
    /// </summary>
    private readonly ITaskPersonalOverlayRepository _personalOverlays;

    /// <summary>Who is watching each task. Visibility only — a watcher never earns an action (pack §12 K3).</summary>
    private readonly ITaskWatcherRepository _watchers;

    /*
     * BL-024 Phase 2 — the caller's permissions, for FIELD-level questions.
     *
     * ⚠ NOT `WorkItemActor.GrantedPermissions`, and the difference is a trap worth naming. That set is built by
     * the controller from `RequiredActionPermissions()` — a FIXED, compile-time list of the action keys the
     * providers declare. A field's permission key is DATA a tenant administrator typed onto a definition, so it
     * is never in that set, and `actor.Has(fieldKey)` would answer FALSE for somebody who genuinely holds it.
     * That failure is silent and it points the wrong way: fields would vanish for the very people entitled to
     * them, and no error would say so.
     */
    /// <summary>
    /// The task-type catalogue (DCP-005 slice 1), read once per page and never per item.
    /// </summary>
    private readonly ITaskTypeRepository _taskTypes;

    private readonly IActorPermissionContext _permissions;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;

    public TaskWorkItemProvider(
        ITaskItemRepository tasks,
        ITaskSeatDirectory seats,
        ITaskLifecycleService lifecycle,
        ITaskAssignmentResolver assignmentResolver,
        IUserDisplayNameResolver displayNames,
        IChecklistRunRepository checklistRuns,
        ITaskApprovalService approvals,
        ITaskDependencyRepository dependencies,
        ITaskCommentRepository comments,
        ITaskTransitionRepository transitions,
        ITaskPersonalOverlayRepository personalOverlays,
        ITaskWatcherRepository watchers,
        IActorPermissionContext permissions,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        IWorkItemSlaCalculator sla,
        ITaskFieldDefinitionRepository fieldDefinitions,
        ITaskTypeRepository taskTypes,
        /*
         * BL-023 — resolves "my team" for the Ekibim scope. OPTIONAL on purpose: a caller that never asks for
         * that scope (every caller before this change, and every test that pins Self behaviour) is unaffected,
         * and an absent resolver can only ever narrow the answer to Self — it can never widen one.
         */
        ITaskTeamResolver? teamResolver = null)
    {
        _teamResolver = teamResolver;
        _sla = sla;
        _fieldDefinitions = fieldDefinitions;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _dependencies = dependencies;
        _comments = comments;
        _transitions = transitions;
        _personalOverlays = personalOverlays;
        _watchers = watchers;
        _taskTypes = taskTypes;
        _permissions = permissions;
        _tasks = tasks;
        _seats = seats;
        _lifecycle = lifecycle;
        _assignmentResolver = assignmentResolver;
        _displayNames = displayNames;
        _checklistRuns = checklistRuns;
        _approvals = approvals;
    }

    /// <summary>
    /// WC-2 — the SLA decision, made here rather than in the browser. Injected rather than called statically so
    /// the working-time seam behind it can be swapped, which is the entire point of the slice.
    /// </summary>
    private readonly IWorkItemSlaCalculator _sla;

    /// <summary>BL-023 — the descent that answers "whose work is my team's". Null ⇒ only the Self scope is served.</summary>
    private readonly ITaskTeamResolver? _teamResolver;

    /// <summary>
    /// The configurable-field catalogue (Phase 5). Read ONCE per page — a stored value carries only its code, so
    /// its section, order, label and type all come from here, and a per-item lookup would be an N+1 across every
    /// row that has any configurable value at all.
    /// </summary>
    private readonly ITaskFieldDefinitionRepository _fieldDefinitions;

    public string ProviderCode => TaskProviderCode;

    public string ProviderContractVersion => "1.0";

    /// <summary>
    /// The permissions BuildActions consults. Omitting one here makes its action unconditionally
    /// PERMISSION_DENIED, so this list and the actor.Has(...) calls must stay in step — TaskWorkItemProviderTests
    /// asserts exactly that.
    /// </summary>
    public IReadOnlyCollection<string> RequiredActionPermissions { get; } =
    [
        TaskPermissions.Update,     // accept + start + resume + inquire + return
        TaskPermissions.Claim,      // claim a pooled task
        TaskPermissions.Complete,   // complete
        TaskPermissions.Cancel,     // cancel
        TaskPermissions.Delete,     // administrative authority to cancel someone else's task
        TaskPermissions.Assign      // reassign — moving work onto another person IS assigning it
    ];

    public async Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(
        WorkItemActor actor,
        CancellationToken ct = default)
    {
        /*
         * BL-023 — WHOSE work. Two scopes, and Team is deliberately NOT a superset of Self: merging them would
         * double every row and answer neither question ("what must I do" vs "what is my team carrying").
         *
         * Team never includes POOL work: an unclaimed pooled task belongs to nobody yet, so it is not any
         * subordinate's load. Showing it under a manager's team view would count work that may never land there.
         */
        List<TaskItem> tasks;
        /*
         * BL-016 — the ids that reached this page ONLY because the actor OPENED the work, and holds no other
         * relationship to it. Empty for a Team read and for every task the actor holds or may claim.
         *
         * It has to be a set computed HERE rather than a per-task test later, because the question is not "did
         * this person create it" (that is on the task) but "is creating it the ONLY reason this row is on the
         * board" — and only the code that ran the three reads knows which one produced the row.
         */
        var initiatorOnly = new HashSet<Guid>();
        if (actor.Scope == WorkItemScope.Team)
        {
            var team = _teamResolver is null
                ? TaskTeamScope.None
                : await _teamResolver.ResolveTeamAsync(ct);

            var theirs = new List<TaskItem>();
            foreach (var member in team.UserIds)
            {
                theirs.AddRange(await _tasks.ListByAssigneeAsync(member, ct));
            }

            tasks = theirs.DistinctBy(t => t.Id).ToList();
        }
        else
        {
            var mine = await _tasks.ListByAssigneeAsync(actor.UserId, ct);

            // Pool work is offered to positions, so the actor's active positions decide what they may see/claim.
            var positionIds = await ResolveActivePositionIdsAsync(actor.UserId, ct);
            var pooled = await _tasks.ListUnclaimedByPositionsAsync(positionIds, ct);

            /*
             * BL-016 — THE THIRD OWNERSHIP QUESTION: what did I start that somebody else is carrying.
             *
             * Neither read above can answer it. `mine` asks what the actor HOLDS and `pooled` asks what their
             * positions are OFFERING; a task the actor opened and handed to a colleague appears in no query at
             * all, which is why "where is the task I gave Ahmet" had, literally, no answer on this surface.
             *
             * ⚠ PRECEDENCE, MEASURED AND DELIBERATE. A task can satisfy two of these at once, and it must still
             * belong to exactly ONE ownership tab — that is the surface's axis law. The order is:
             *
             *     hold it        → İşlerim / Gelen Kutusu   (mine)
             *     may claim it   → Havuz                    (pooled)
             *     opened it      → Başlattıklarım           (this read, and only what the first two did not take)
             *
             * Holding outranks having opened it: a task you assigned to yourself is YOUR WORK, not something you
             * are watching somebody else do — put it in the Outbox and the reader would have to look in two
             * places for work that is on their own desk. Claimable outranks it for the same reason in the other
             * direction: `claim` is an action the reader can actually press, and the tab where the action lives
             * is the tab the row belongs in.
             *
             * `Except` on the id set is what enforces that, so the DistinctBy below never has to choose.
             */
            var alreadyOnTheBoard = mine.Concat(pooled).Select(t => t.Id).ToHashSet();
            var initiated = (await _tasks.ListByCreatorAsync(actor.UserId, ct))
                .Where(t => !alreadyOnTheBoard.Contains(t.Id))
                .ToList();
            initiatorOnly = initiated.Select(t => t.Id).ToHashSet();

            tasks = mine
                .Concat(pooled)
                .Concat(initiated)
                .DistinctBy(t => t.Id)
                .ToList();
        }

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

        /*
         * WC-1 — every task's history for the whole page in ONE read, like every other container here.
         *
         * Read BEFORE the display names on purpose: an event's actor is resolved live rather than snapshotted
         * (see TaskTransition), so those ids have to join the SAME batch. Resolving them afterwards would be a
         * second directory round-trip per page, and resolving them per row an N+1 across every history on screen.
         */
        var transitionsByTask = (await _transitions.ListByTaskIdsAsync(taskIds, ct))
            .GroupBy(transition => transition.TaskItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TaskTransition>)group.ToList());

        /*
         * Watchers for the whole page, in one read — and read HERE, before the display-name batch, for the same
         * reason the transitions are: a watcher the screen cannot name is a row that says somebody is watching
         * without saying who. Resolving those ids afterwards would be a second directory round-trip per page.
         */
        var watchersByTask = (await _watchers.ListByTaskIdsAsync(taskIds, ct))
            .GroupBy(watcher => watcher.TaskItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TaskWatcher>)group.ToList());

        // ONE batched resolve for the whole page — never one call per task, and cached between requests. It runs
        // after the children are known so subtask holders ride the SAME batch; resolving them per row would be an
        // N+1 across the page. Best effort: if AuthService is down this comes back empty and names are omitted.
        var userIds = tasks
            // The approval manager joins the batch so the gates card can name a person instead of an id.
            .SelectMany(t => new[] { t.AssigneeUserId, t.CreatedByUserId, t.ApprovalManagerUserId })
            .Concat(childrenByParent.SelectMany(pair => pair.Value).Select(child => child.AssigneeUserId))
            // Whoever performed each recorded act. A history that says "the task was released" without saying by
            // whom answers half the question it was asked.
            .Concat(transitionsByTask.SelectMany(pair => pair.Value).Select(transition => transition.ActorUserId))
            // Whoever is watching. Same batch, same reason — a named watcher or none at all.
            .Concat(watchersByTask.SelectMany(pair => pair.Value).Select(watcher => (Guid?)watcher.UserId))
            // Whoever a parked task is waiting on. In the SAME batch for the same reason: a wait that says
            // "waiting on somebody" without saying who answers half the question it was asked.
            .Concat(tasks.Select(t => t.WaitingOnUserId))
            .Where(id => id is not null && id != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var displayNames = await _displayNames.ResolveAsync(userIds, ct);

        // Approval state comes from MOD-0023, in ONE read for the whole page. It must be READ, never inferred:
        // ApprovalRequired records that approval was asked for, not whether it has been given, so deriving from
        // the flag left an APPROVED task still showing Waiting with `start` disabled.
        //
        // Review joins the SAME read rather than getting one of its own: GetStatesAsync keys off the INSTANCE id
        // and never asks what the instance decides, so it serves both decisions unchanged. Two calls would be two
        // round-trips for one page, and a per-item read would be an N+1 across every gated row on the surface.
        var instanceIds = tasks
            .Where(t => t.ApprovalRequired && t.WorkflowInstanceId is not null)
            .Select(t => t.WorkflowInstanceId!.Value)
            .Concat(tasks
                .Where(t => t.ReviewRequired && t.ReviewWorkflowInstanceId is not null)
                .Select(t => t.ReviewWorkflowInstanceId!.Value))
            .Distinct()
            .ToList();
        var approvalStates = await _approvals.GetStatesAsync(instanceIds, ct);

        /*
         * Dependency edges for the whole page in ONE read, plus a second read for the tasks at the FAR end of
         * those edges. The far end is fetched because a blocker has to name a real task and report its real
         * state: "something is blocking this" with nothing behind it is the invented-data failure in banner form.
         * An edge whose far end cannot be read (another tenant's task, a deleted one) is dropped rather than
         * rendered as an unnamed blocker.
         */
        // One read for the whole page's conversation, like every other container here.
        var commentsByTask = (await _comments.ListByTaskIdsAsync(taskIds, ct))
            .GroupBy(comment => comment.TaskItemId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TaskComment>)group.ToList());

        /*
         * Pool queue names, resolved in TWO reads for the whole page rather than one per task — the same batching
         * rule the display names and the checklist runs follow. A per-item lookup here would be an N+1 across
         * every pooled row on the surface.
         */
        var poolLabels = await ResolvePoolLabelsAsync(tasks, ct);

        /*
         * DCP-005 slice 1 — the task types for the whole page in ONE read, batched exactly like the pool labels
         * above and for the same reason: a per-item lookup would be an N+1 across every typed row.
         *
         * ⚠ `ListAllAsync`, not `ListActiveAsync`: a task keeps showing the type it was opened under even after
         * that type is retired. Reading only the active ones would blank the type on historical work — which is
         * the whole thing retiring-instead-of-deleting exists to prevent.
         */
        var taskTypes = (await _taskTypes.ListAllAsync(ct)).ToDictionary(type => type.Id);

        /*
         * WC-1 — THIS reader's overlays for the whole page, in one read. The actor's user id is the second half of
         * the query, not a filter applied to the result: a batch read that fetched every overlay and then kept the
         * matching ones would put other people's private notes in this process's memory, one refactor away from
         * the wire.
         */
        var personalByTask = (await _personalOverlays.ListForUserAsync(taskIds, actor.UserId, ct))
            .GroupBy(overlay => overlay.TaskItemId)
            .ToDictionary(group => group.Key, group => group.First());


        /*
         * The field catalogue, ONE read for the page — including retired definitions.
         *
         * Retired ones are needed precisely because they are retired: a value written before a definition was
         * withdrawn still has to render with the label it was written under. Dropping it would delete data from
         * the screen that the API still returns, and printing its raw code would put `regulatory.phase` where a
         * heading belongs — the two exits this codebase has already ruled out.
         */
        var fieldDefinitions = tasks.Any(t => t.FieldValues.Count > 0)
            ? (await _fieldDefinitions.ListAllAsync(ct)).ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, TaskFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        var edges = await _dependencies.ListByTaskIdsAsync(taskIds, ct);
        var edgeTaskIds = edges
            .SelectMany(edge => new[] { edge.TaskItemId, edge.DependsOnTaskItemId })
            .Distinct()
            .Where(id => !taskIds.Contains(id))
            .ToList();
        var edgeTasks = tasks
            .Concat(await _tasks.ListByIdsAsync(edgeTaskIds, ct))
            .DistinctBy(t => t.Id)
            .ToDictionary(t => t.Id);
        var edgesByTask = edges
            .SelectMany(edge => new[] { (Key: edge.TaskItemId, Edge: edge), (Key: edge.DependsOnTaskItemId, Edge: edge) })
            .GroupBy(pair => pair.Key)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TaskDependency>)group.Select(p => p.Edge).Distinct().ToList());

        return tasks
            .Select(t =>
            {
                var (outstanding, rejected) = ApprovalView(t, approvalStates);
                var (reviewOutstanding, reviewRejected) = TaskReviewView.Resolve(t, approvalStates);
                return Project(
                    t,
                    actor,
                    displayNames,
                    checklistByTask.GetValueOrDefault(t.Id),
                    childrenByParent.GetValueOrDefault(t.Id, []),
                    outstanding,
                    rejected,
                    reviewOutstanding,
                    reviewRejected,
                    edgesByTask.GetValueOrDefault(t.Id, []),
                    edgeTasks,
                    commentsByTask.GetValueOrDefault(t.Id, []),
                    transitionsByTask.GetValueOrDefault(t.Id, []),
                    poolLabels,
                    taskTypes,
                    fieldDefinitions,
                    personalByTask.GetValueOrDefault(t.Id),
                    watchersByTask.GetValueOrDefault(t.Id, []),
                    initiatorOnly.Contains(t.Id));
            })
            .ToList();
    }

    /// <summary>
    /// Both flags come from ONE shared rule (TaskApprovalView) so the Task Center, the task list and the detail
    /// view can never disagree about whether an approval is still outstanding.
    /// </summary>
    private static (bool Outstanding, bool Rejected) ApprovalView(
        TaskItem task,
        IReadOnlyDictionary<Guid, TaskApprovalState> states)
        => TaskApprovalView.Resolve(task, states);

    private WorkItemProjectionDto Project(
        TaskItem task,
        WorkItemActor actor,
        IReadOnlyDictionary<Guid, string> displayNames,
        ChecklistRun? checklist,
        IReadOnlyList<TaskItem> children,
        bool approvalOutstanding,
        bool approvalRejected,
        bool reviewOutstanding,
        bool reviewRejected,
        IReadOnlyList<TaskDependency>? edges = null,
        IReadOnlyDictionary<Guid, TaskItem>? edgeTasks = null,
        IReadOnlyList<TaskComment>? comments = null,
        IReadOnlyList<TaskTransition>? transitions = null,
        IReadOnlyDictionary<Guid, string>? poolLabels = null,
        IReadOnlyDictionary<Guid, TaskType>? taskTypes = null,
        IReadOnlyDictionary<string, TaskFieldDefinition>? fieldDefinitions = null,
        TaskPersonalOverlay? personal = null,
        IReadOnlyList<TaskWatcher>? watchers = null,
        // BL-016 — the actor OPENED this and holds no other relationship to it. Decided by GetWorkItemsAsync,
        // the only code that knows which read produced the row; see the precedence note there.
        bool initiatorOnly = false)
    {
        var assignment = _assignmentResolver.Resolve(task);
        var normalized = _lifecycle.ToNormalizedStatus(
            task, approvalOutstanding, approvalRejected, reviewOutstanding, reviewRejected);
        var waiting = _lifecycle.ResolveWaitingContext(
            task, approvalOutstanding, approvalRejected, reviewOutstanding, reviewRejected);
        // A rejected approval is terminal too: refused work must not keep offering start/complete.
        var terminal = _lifecycle.IsTerminal(task) || approvalRejected;

        // A terminal task exposes NO state-changing action (contract rule), so placement is empty too.
        // A blocking checklist item makes completion unavailable. Shown DISABLED with the reason rather than
        // hidden — and the server refuses the write too, so this is a hint, never the enforcement.
        var checklistBlocks = ChecklistBlocksCompletion(checklist);

        // Dependencies are resolved BEFORE the actions are built, because an unfinished predecessor changes which
        // actions may be offered — and the contract requires every blocked action to be present and disabled,
        // never hidden. A hidden button teaches the reader nothing about why the work will not move.
        var dependencies = ToDependencies(task, edges ?? [], edgeTasks);
        /*
         * The task's TYPE, resolved ONCE. Three things below need it — the outcome dictionary the picker offers,
         * the label on the closure record, and the label on each closing transition in the feed — and resolving
         * it three times is how the three come to disagree about a type that was retired mid-page.
         */
        var resolvedType = task.TaskTypeId is { } resolvedTypeId
            && taskTypes?.TryGetValue(resolvedTypeId, out var found) == true
                ? found
                : null;

        var activity = ToActivity(
            comments ?? [], transitions ?? [], displayNames, actor.UserId, fieldDefinitions, _permissions,
            resolvedType);

        /*
         * THE FOUR CONDITIONAL CONTAINERS, DECIDED ONCE.
         *
         * The contract's CAPABILITY_REQUIRED_FOR_DATA / CAPABILITY_CONTAINER_REQUIRED pair rejects a HALF: a
         * container without its capability, or a capability without its container. validateItems does not repair
         * a half — it DROPS the whole item, so the task vanishes from the surface with its title, its actions and
         * everything else.
         *
         * `dependencies` shipped as exactly that half. The field has been emitted since BL-028 and the capability
         * was never added, so from the day dependencies existed, every task that had one was invisible in the Task
         * Center. Two were being dropped in production when this was measured.
         *
         * The cure is structural rather than careful: each container is built here, ONCE, and the capability list
         * below is derived from these very objects. A capability is declared exactly when its container is
         * non-null, because it is the same reference — there is no second condition left to drift. Previously
         * checklist, subtasks and businessContext each had their condition written twice; they agreed today, and
         * the day they stopped agreeing an item would disappear.
         */
        var dependencyList = dependencies.Count == 0 ? null : dependencies;
        /*
         * The checklist container is emitted for EVERY task, run or no run — the same rule subtasks follow
         * below, and now for the same reason.
         *
         * It used to be `checklist is null ? null : …`, which was correct while the only way to get a checklist
         * was to ask for a template at creation: a task with no run could never grow one, so declaring an empty
         * container would have been an offer the product could not keep. The shell now has an add row, and the
         * old condition became a trap — a task with no run declared no capability, the card was never drawn, and
         * the only way to add the first item was on a task that already had one. Discoverable only if you
         * already had it.
         *
         * Declared-and-empty is a state the contract models (CAPABILITY_CONTAINER_REQUIRED); a half is not.
         */
        var checklistBlock = checklist is null ? EmptyChecklist : ToChecklist(checklist, actor);
        // A subtask cannot have subtasks. A parent always gets the container, even empty, because the shell
        // offers "add a subtask" there — declared-and-empty is a state the contract models; a half is not.
        var subtasks = task.ParentTaskItemId is null ? ToSubtasks(children, actor, displayNames) : null;
        var businessContext = ToBusinessContext(task, fieldDefinitions, _permissions);
        var blockers = ResolveBlockers(task, edges ?? [], edgeTasks, children);

        var (built, primaryActionCode, overflowActionCodes) = terminal
            ? ([], null, (IReadOnlyList<string>)[])
            : BuildActions(
                task, actor, checklistBlocks, approvalOutstanding, reviewOutstanding, reviewRejected,
                initiatorOnly);

        /*
         * Apply the blocks LAST, as a rewrite over whatever was offered. Done here rather than inside BuildActions
         * because it is one rule — "an unsatisfied predecessor disables the act it gates" — and threading it
         * through five branches would let a new branch forget it.
         *
         * A blocker whose action is not on offer is DROPPED: a FinishToFinish edge does not stop a task that has
         * not started, because `complete` is not being offered anyway. Keeping it would also break the contract,
         * which requires every affected code to name an action the reader can actually see disabled.
         */
        var offered = built.Select(action => action.Code).ToHashSet();
        var effectiveBlockers = blockers.Where(b => offered.Contains(b.AffectedActionCode!)).ToList();
        /*
         * The button's reason follows the FIRST blocker on that action, in the order they were resolved
         * (dependencies, then subtasks). Same order the handler checks in, so the sentence beside the greyed
         * button is the one the server would answer with — two different orders would have the screen blame an
         * open subtask while the 409 blamed a predecessor.
         */
        var reasonByAction = effectiveBlockers
            .GroupBy(b => b.AffectedActionCode!)
            .ToDictionary(group => group.Key, group => group.First().Code);
        var actions = reasonByAction.Count == 0
            ? built
            : built
                .Select(action => reasonByAction.TryGetValue(action.Code, out var reasonCode) && action.Enabled
                    ? AsDisabled(
                        action,
                        reasonCode,
                        reasonCode == WorkAggregationReasonCodes.SubtaskBlocked
                            ? DisabledSubtaskKey
                            : DisabledDependencyKey)
                    : action)
                .ToList();

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
            WorkItemCapabilities: ResolveCapabilities(
                dependencyList, checklistBlock, subtasks, businessContext,
                task.EstimateHours, task.SpentHours),
            Actions: actions,
            Concurrency: new WorkItemConcurrencyDto("version", task.Version.ToString()),
            WaitingContext: waiting is null
                ? null
                /*
                 * The reason is the user's own sentence, so it crosses as a DISPLAY label.
                 *
                 * ⚠ `waitingOn` USED TO BE HARD-NULL, and the comment here said why: "stays null until something
                 * can resolve a real identity to put there." That something now exists — the holder names the
                 * person when they park the task, and `Person(...)` resolves the name from the SAME batched
                 * directory read the assignee and the requester use.
                 *
                 * `Person` is reused rather than reimplemented, which is what keeps the module's rule intact: a
                 * name that cannot be resolved comes back as a person with a null displayName, NEVER as a GUID.
                 * An id is not a person, and printing one is the failure this projection has refused twice
                 * before (the fabricated pool label, the raw resource key).
                 */
                : new WorkItemWaitingContextDto(
                    waiting.Type,
                    WaitingOn: Person(waiting.WaitingOnUserId, actor, displayNames),
                    Reason: waiting.Reason is null ? null : WorkItemLabelDto.Display(waiting.Reason),
                    waiting.Since,
                    waiting.ExpectedUntil),
            Escalation: null,
            DueAt: task.DueAt,
            PrimaryActionCode: primaryActionCode,
            OverflowActionCodes: overflowActionCodes,
            // An unclaimed pool task genuinely has no assignee — omit rather than invent one.
            Assignee: Person(task.AssigneeUserId, actor, displayNames),
            Requester: Person(task.CreatedByUserId, actor, displayNames),
            // All four conditional containers are the SAME objects the capability list was derived from —
            // see the block that builds them. No condition is restated here, so none can drift.
            Checklist: checklistBlock,
            Subtasks: subtasks,
            ParentTaskItemId: task.ParentTaskItemId?.ToString(),
            Gates: BuildGates(
                task, actor, displayNames, approvalOutstanding, approvalRejected, reviewOutstanding, reviewRejected),
            // The engine's own spelling, straight through — the contract's PRIORITIES are that enum (BL-032).
            Priority: task.Priority.ToString(),
            Dependencies: dependencyList,
            // Absent when nothing blocks. A terminal task offers no actions at all, so a blocker pointing at one
            // would break the contract's "every affected code is a disabled action" rule.
            BlockedState: effectiveBlockers.Count == 0
                ? null
                : new WorkItemBlockedStateDto(
                    Blocked: true,
                    AffectedActionCodes: reasonByAction.Keys.ToList(),
                    Blockers: effectiveBlockers),
            // Always emitted, because the capability is always declared: MOD-0024 owns the conversation, so the
            // feed exists even before anyone has said anything. Declared-and-empty is the valid state the
            // contract models; a HALF (one without the other) is what it rejects.
            Activity: activity,
            // Straight through — never DueAt. A plan write that stored the date but never showed it back would
            // be real on the server and invisible on the screen.
            PlannedDate: task.PlannedDate,
            /*
             * ⚠ ABSENT WHEN THE TYPE CANNOT BE RESOLVED, not fabricated. A task carrying an id whose type was
             * hard-deleted out from under it (which this module refuses to do, but another tenant tool might)
             * projects no type rather than an id with an empty name — a half-identity on screen is worse than
             * none, and this repository has paid for that shape before.
             */
            TaskType: resolvedType is { } taskType
                ? new WorkItemTaskTypeDto(
                    taskType.Id.ToString(),
                    taskType.Code,
                    taskType.Name,
                    /*
                     * The two halves of the outcome dictionary, already split by disposition so the dialog does
                     * not re-derive the filter. Null rather than an empty list when this type asks nothing —
                     * that absence IS the backward-compatible path, and the client reads it as "close the way
                     * you always did".
                     */
                    ToClosureOutcomes(taskType, TaskClosureDisposition.Completed),
                    ToClosureOutcomes(taskType, TaskClosureDisposition.Cancelled))
                : null,
            Pool: ToPool(task, poolLabels),
            BusinessContext: businessContext,
            /*
             * Measured against the wall clock at PROJECTION time, and stated as a state rather than a countdown.
             * The reader's tab may outlive this answer; the absolute DueAt travels with it so the words on screen
             * can be re-derived, and no frozen day count is sent (see the DTO's own note, and the `ago` ban).
             *
             * BL-046 — EXCEPT once the work is closed. A finished task was still measured against today, so the
             * History list read "Completed · 11 days late" and would read "12 days late" tomorrow. Finished work
             * does not keep getting later. For a terminal task the clock stops at the moment it closed, which
             * makes the badge a fact about that task rather than a fact about today.
             *
             * The badge is NOT dropped: closing late is exactly what reporting wants to see. Freezing it keeps
             * the information and removes the lie.
             */
            SlaState: _sla.Resolve(task.DueAt, SlaReferenceInstant(task, terminal)),
            /*
             * The other half of BL-046, and it does not work alone. Freezing the STATE here while the client
             * still counted the days against today produced "-2 days LEFT" on a live screen — the count needs
             * the instant to measure from, so the instant travels.
             *
             * Only the REAL timestamp, never SlaReferenceInstant's now-fallback: a fabricated closing time would
             * freeze a lie instead of a fact. When it is genuinely absent the client says "closed late" without
             * a number rather than quoting one.
             */
            ClosedAt: terminal ? task.CompletedAt ?? task.CancelledAt : null,
            /*
             * WHAT WAS DECIDED, beside WHEN it ended. Resolved against the type's CURRENT dictionary, so an
             * outcome that has since been retired yields the bare code rather than a blank — see WorkItemClosureDto.
             */
            Closure: terminal ? ToClosure(task, resolvedType) : null,
            /*
             * WHERE THIS CAME FROM — and it travels for a CLOSED task too, deliberately.
             *
             * The chip is a triage signal and the shell hides it on finished work; there is nothing to triage on
             * a task nobody has to pick up. But the DETAIL page of a closed task answers a different question —
             * what happened to this work — and "it came back twice before it was finished" is a real part of
             * that answer. Withholding the fact here would decide the second question with the first one's
             * reasoning, and the projection has no business doing that: it states what is true, the surface
             * decides what is worth showing. (Same split `closedAt` gets: emitted as fact, drawn selectively.)
             */
            Returned: ToReturned(transitions),
            /*
             * WHAT THE WORK IS. The form has collected these four since Phase 1 and none of them reached the
             * Task Center, so the detail page could say a task was fifteen days overdue without saying what it
             * asked for or when it was due. Each is omitted when absent rather than emitted empty — the screen
             * prints a row only for a fact that exists.
             */
            Summary: string.IsNullOrWhiteSpace(task.Description)
                ? null
                : WorkItemLabelDto.Display(task.Description),
            StartAt: task.StartAt,
            EstimateHours: task.EstimateHours,
            /*
             * ⚠ ONLY WHEN THERE IS SOMETHING TO SAY (2026-08-24, Tur B). The effort card compares spent against
             * estimate; a task with neither would otherwise get a card reading "0 / 0", which is the confident
             * zero this projection avoids everywhere else — it reads as "nobody has worked on this" rather than
             * "this is not being tracked".
             */
            SpentHours: task.EstimateHours is null && task.SpentHours == 0 ? null : task.SpentHours,
            Tags: task.Tags is { Count: > 0 } ? task.Tags.ToList() : null,
            /*
             * WC-1 — the reader's own layer, or nothing at all.
             *
             * `ToPersonal` returns null when this reader has neither snoozed the task nor written a note, which is
             * most rows: an empty container on every item would put a personal layer on the wire for work nobody
             * has laid one over, and the screen would have to tell "no notes" apart from "no overlay" for no gain.
             */
            Personal: ToPersonal(personal),
            /*
             * ── The four settings the create form collected and no surface ever showed ──────────────────────
             *
             * Measured 2026-08-14: watchers, delegation policy, notification preferences and the reminder lead
             * were all written at creation and none of them reached the Task Center. `delegationAllowed` did not
             * appear anywhere in the client at all. They are projected here and NOT placed on any screen in the
             * same change — where each belongs is a design decision, and this round has already paid for the
             * habit of inventing a card for a field that arrived without one.
             */
            Watchers: watchers is { Count: > 0 }
                ? watchers
                    .Select(w => new WorkItemWatcherDto(
                        // Person(...) can return null only for a null id; a watcher always has one.
                        Person(w.UserId, actor, displayNames)!,
                        w.Role.ToString()))
                    .ToList()
                : null,
            // Straight through as a real bool: this task DOES express a delegation policy, so it says which one.
            // The DTO's nullable is for providers that have no such concept — not for this one.
            DelegationAllowed: task.DelegationAllowed,
            Notifications: new WorkItemNotificationsDto(
                task.EmailNotificationsEnabled,
                // NULL and EMPTY are different answers here — "nobody chose, so everything is sent" versus "the
                // owner chose nothing". Normalising either into the other would silence a task or invent a choice.
                task.NotifyOnEvents is null ? null : task.NotifyOnEvents.ToList()),
            ReminderLeadDays: task.ReminderLeadDays,
            // BL-016 — stated ONLY when the shell cannot work it out for itself; see the DTO for why the holder
            // and pool cases are deliberately silent.
            ViewerRelation: initiatorOnly ? WorkItemContract.ViewerRelationInitiator : null);
    }

    /// <summary>
    /// The reader's own overlay, or null when they have laid none over this task.
    ///
    /// <para>A PAST snooze is projected as no snooze at all: an expired park is over, and sending the stale date
    /// would have every client re-derive "is this still snoozed?" — the kind of decision the server is here to
    /// make. The notes still travel, so an overlay whose only content was an expired snooze collapses to null and
    /// the task carries no personal layer, which is exactly true.</para>
    /// </summary>
    private static WorkItemPersonalDto? ToPersonal(TaskPersonalOverlay? overlay)
    {
        if (overlay is null)
        {
            return null;
        }

        var snoozedUntil = overlay.SnoozedUntil > DateTimeOffset.UtcNow ? overlay.SnoozedUntil : null;
        var notes = overlay.Notes
            // Oldest first — the order they were written, which is the order they read in.
            .OrderBy(note => note.CreatedAt)
            .ThenBy(note => note.Id)
            .Select(note => new WorkItemPersonalNoteDto(note.Id.ToString(), note.Text, note.CreatedAt))
            .ToList();

        /*
         * ⚠ `Pinned` JOINS THE "IS THERE ANYTHING TO SAY" TEST. Without it a task whose ONLY personal state is
         * a pin would project `personal: null`, and the mark would vanish on the next read — which is the very
         * defect this column was added to fix.
         */
        return snoozedUntil is null && notes.Count == 0 && !overlay.Pinned
            ? null
            : new WorkItemPersonalDto(snoozedUntil, overlay.Pinned, notes);
    }

    /// <summary>
    /// When to measure the SLA from. For live work that is now; for closed work it is the moment it closed
    /// (BL-046), so a finished task cannot go on getting later every day it sits in History.
    ///
    /// <para>Cancelled falls back to CompletedAt and vice versa, and both fall back to now: a terminal task with
    /// no closing timestamp is old data, and measuring it from today is no worse than the state it was already
    /// in — whereas throwing would take the whole item off the surface.</para>
    /// </summary>
    private static DateTimeOffset SlaReferenceInstant(TaskItem task, bool terminal)
        => terminal
            ? task.CompletedAt ?? task.CancelledAt ?? DateTimeOffset.UtcNow
            : DateTimeOffset.UtcNow;

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
    /// The declared capability list, derived from the containers that will actually be emitted.
    ///
    /// <para><b>Every parameter is the emitted object itself, not a condition that reproduces it.</b> That is the
    /// whole design: the contract drops an item that declares a capability without its container or carries a
    /// container without its capability, and the only durable way to keep the two in step is to stop writing the
    /// condition twice. <c>dependencies</c> is why — its container shipped in BL-028, its capability was never
    /// added, and every task with a dependency has been invisible on the surface ever since.</para>
    /// </summary>
    private static IReadOnlyList<string> ResolveCapabilities(
        IReadOnlyList<WorkItemDependencyDto>? dependencies,
        WorkItemChecklistDto? checklist,
        WorkItemSubtasksDto? subtasks,
        WorkItemBusinessContextDto? businessContext,
        decimal? estimateHours,
        decimal spentHours)
    {
        // Unconditional: MOD-0024 owns planning and execution for every task it projects.
        var capabilities = new List<string> { "planning", "execution" };

        if (businessContext is not null)
        {
            capabilities.Add("businessContext");
        }

        if (checklist is not null)
        {
            capabilities.Add("checklist");
        }

        if (subtasks is not null)
        {
            capabilities.Add("subtasks");
        }

        if (dependencies is not null)
        {
            capabilities.Add("dependencies");
        }

        /*
         * ⚠ `taskContext` — THE EFFORT CARD'S GATE (2026-08-24, Tur B).
         *
         * Declared only when the task actually carries effort figures, for the same reason `businessContext`
         * and `checklist` are conditional: a capability is a promise that the card has something to show. A
         * task nobody estimated and nobody logged against gets no card rather than a card of zeroes.
         */
        if (estimateHours is not null || spentHours != 0)
        {
            capabilities.Add("taskContext");
        }

        /*
         * Activity is declared UNCONDITIONALLY, and its container is emitted unconditionally to match. MOD-0024 is
         * the source of its own comments, so the feed exists for every task whether or not anyone has written in
         * it yet. Declaring it only when comments exist would hide the composer on exactly the tasks nobody has
         * commented on, which is where it is needed most.
         *
         * What the feed does NOT contain is a lifecycle event log: none exists, and deriving one from the
         * timestamps a task happens to carry would omit accept/plan/claim/release/inquire silently.
         */
        capabilities.Add("activity");

        return capabilities;
    }

    /// <summary>
    /// Checklist items keep the label FORM they were authored with: a template item stays a resource key so it
    /// localizes, an ad-hoc item the user typed becomes display text. Emitting a resource label for typed text is
    /// what puts a raw key on screen.
    /// </summary>
    /// <summary>
    /// A task that has no run yet — an empty list at version 0.
    ///
    /// <para><b>Version 0 is load-bearing, not a placeholder.</b> The add endpoint reads it as "no run exists,
    /// create one", which is exactly the branch <c>AddChecklistItemHandler</c> already takes when it finds none.
    /// Sending version 1 would claim a document that is not there, and the first add would then look like a lost
    /// expected-version race to a user who is the only person on the page.</para>
    /// </summary>
    private static readonly WorkItemChecklistDto EmptyChecklist = new([], Version: 0);

    private static WorkItemChecklistDto ToChecklist(ChecklistRun run, WorkItemActor actor)
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
                EvidenceRequired: item.EvidenceRequired,
                /*
                 * The SAME test the write handlers apply, evaluated once here so the screen can be honest about
                 * what it offers. A null author — a row older than the field, or one instantiated from a template
                 * — is somebody else's, and reads as not editable.
                 *
                 * This is a courtesy, not the guard. The endpoints refuse independently and would refuse just as
                 * firmly if this line said true for everything; drawing a control that the server will reject is
                 * simply a worse way to tell someone the answer.
                 */
                Editable: item.AddedByUserId is not null && item.AddedByUserId == actor.UserId))
            .ToList(),
            Version: run.Version);

    /// <summary>
    /// Subtasks in the contract's own vocabulary. MOD-0024 is their source, so the mode is `full`: they are
    /// created and completed here rather than deep-linked elsewhere.
    /// </summary>

    /// <summary>
    /// What must happen before this work may proceed, and where that stands — REPORTED, never decided here.
    ///
    /// <para>Approval status is read from what MOD-0023 says about the instance, not from
    /// <c>ApprovalRequired</c>: that flag records only that approval was ASKED FOR. Deriving status from it is
    /// exactly the mistake that once left an approved task still showing as waiting.</para>
    ///
    /// <para>Review status is read the SAME way, from the review instance (Faz 3b). It used to be derived from
    /// <c>task.Lifecycle == PendingReview</c>, which is the same mistake in the same shape: the lifecycle records
    /// that the work was HANDED OVER, never what came back, so a released review and a refused one were both
    /// reported as "pending" and neither could ever be told from the other.</para>
    ///
    /// <para>Review's decider is a CANDIDATE hint, exactly like approval's manager: it is who the requester
    /// suggested, and MOD-0023/MOD-0018 decide who may actually act. It is never who DID review — that answer
    /// belongs to the instance.</para>
    /// </summary>
    private static WorkItemGatesDto BuildGates(
        TaskItem task,
        WorkItemActor actor,
        IReadOnlyDictionary<Guid, string> displayNames,
        bool approvalOutstanding,
        bool approvalRejected,
        bool reviewOutstanding,
        bool reviewRejected)
    {
        var approvalStatus = !task.ApprovalRequired ? GateNotRequired
            : approvalRejected ? GateRejected
            : approvalOutstanding ? GatePending
            // Required, not outstanding and not refused: MOD-0023 released it.
            : GateApproved;

        var reviewStatus = !task.ReviewRequired ? GateNotRequired
            : reviewRejected ? GateRejected
            // Sitting with a reviewer right now — an instance exists and MOD-0023 has not answered it.
            : reviewOutstanding && task.ReviewWorkflowInstanceId is not null ? GatePending
            // Declared, but the work has not been submitted yet: no instance, so nobody is holding it.
            : task.ReviewWorkflowInstanceId is null ? GateRequired
            // Required, not outstanding and not refused: MOD-0023 released it.
            : GateApproved;

        return new WorkItemGatesDto(
            Approval: new WorkItemGateDto(
                task.ApprovalRequired,
                approvalStatus,
                // A CANDIDATE approver hint (MOD-0018/MOD-0023 resolve real authority) — a typed identity, so the
                // client can render a person instead of a raw id, and null when there is none.
                Person(task.ApprovalManagerUserId, actor, displayNames)),
            Review: new WorkItemGateDto(
                task.ReviewRequired,
                reviewStatus,
                Person(task.ReviewerCandidateUserId, actor, displayNames)));
    }

    /// <summary>
    /// The configurable values, grouped into the sections their definitions declare.
    ///
    /// <para><b>The label source split is carried straight through.</b> A SYSTEM definition names a resource key
    /// and becomes a <c>resource</c> label; a TENANT definition carries the administrator's own words and becomes
    /// a <c>display</c> one. Collapsing them is how a raw key reaches the screen, which this codebase has done
    /// once already — the contract models both forms precisely so it never has to happen again.</para>
    ///
    /// <para><b>Nothing is invented for a value whose definition cannot be read.</b> It keeps its value and is
    /// grouped under an "unfiled" section with no title, rather than being dropped (the data exists and the API
    /// returns it) or labelled with its own code (a raw code where a heading belongs).</para>
    /// </summary>
    private static WorkItemBusinessContextDto? ToBusinessContext(
        TaskItem task,
        IReadOnlyDictionary<string, TaskFieldDefinition>? definitions,
        // BL-024 Phase 2 — who is asking. Threaded rather than resolved here: the provider already receives the
        // actor for action enablement, and a second source of "who" would be a second answer.
        IActorPermissionContext actor)
    {
        if (task.FieldValues.Count == 0)
        {
            return null;
        }

        var catalogue = definitions ?? new Dictionary<string, TaskFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        var grouped = task.FieldValues
            .Select(value => (Value: value, Definition: catalogue.GetValueOrDefault(value.DefinitionCode)))
            // A definition with no section, and a value with no definition at all, both land in one unnamed
            // group rather than inventing a heading nobody wrote.
            .GroupBy(pair => pair.Definition?.Section?.Trim() is { Length: > 0 } section ? section : null)
            .OrderBy(group => group.Key is null)             // the unfiled group goes last
            .ThenBy(group => group.Min(pair => pair.Definition?.SortOrder ?? int.MaxValue))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            /*
             * The contract caps sections at six and the catalogue enforces that at the WRITE — but older data
             * predates the rule, so the projection defends itself too. Taking the first six is the one exit that
             * keeps the item on the surface: exceeding the cap makes validateItems drop the WHOLE task, values
             * and title and actions with it.
             */
            .Take(TaskFieldDefinitionRules.MaxSections)
            .Select(group => new WorkItemBusinessSectionDto(
                group.Key is { } title
                    // A section name is something a tenant administrator typed. It is content, not a key we own.
                    ? WorkItemLabelDto.Display(title)
                    : WorkItemLabelDto.Resource(SectionUnfiledKey),
                group
                    .OrderBy(pair => pair.Definition?.SortOrder ?? int.MaxValue)
                    .ThenBy(pair => pair.Value.DefinitionCode, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => ToBusinessField(pair.Value, pair.Definition, actor))
                    .ToList())
            )
            .ToList();

        return new WorkItemBusinessContextDto(grouped);
    }

    private static WorkItemBusinessFieldDto ToBusinessField(
        TaskFieldValue value,
        TaskFieldDefinition? definition,
        IActorPermissionContext actor)
    {
        /*
         * BL-024 Phase 2 — the SECOND read path, and the reason the decision is a shared rule rather than a line
         * of code in the mapper.
         *
         * The Tasks detail response and this projection are two different DTOs built by two different files, and
         * a field hidden in one and shown in the other is not half-fixed — it is not fixed. Both ask
         * TaskFieldAccessRules the same question.
         *
         * The contract enforces the outcome too: REDACTED_VALUE_MUST_BE_OMITTED fails any fixture that ships
         * `redacted: true` alongside a value, so a regression here is caught by the browser's own validator and
         * not only by a server test.
         */
        var visible = TaskFieldAccessRules.CanView(value, definition, actor);

        return new WorkItemBusinessFieldDto(
            ResolveFieldLabel(definition),
            // The CONTRACT's spelling, not the enum's. The two vocabularies were declared to match value-for-value
            // on purpose; shipping PascalCase here is the shape that has cost this module twice.
            value.ValueType.ToString().ToLowerInvariant(),
            // OMITTED, never sent-and-hidden: the contract rejects a redacted field that still carries its value.
            visible ? value.Value : null,
            definition?.Importance == TaskFieldImportance.Primary ? "primary" : "secondary",
            Redacted: !visible);
    }

    /// <summary>
    /// The label, from whichever source the definition has — and NEITHER is a fallback for the other.
    ///
    /// <para>A value whose definition cannot be read (retired and since purged, or written before the catalogue
    /// existed) gets a stated "unknown field" label rather than its own code. The code is an identifier, not a
    /// name, and printing one where a heading belongs is the defect this split exists to prevent.</para>
    /// </summary>
    private static WorkItemLabelDto ResolveFieldLabel(TaskFieldDefinition? definition)
    {
        if (definition?.LabelText is { Length: > 0 } text)
        {
            return WorkItemLabelDto.Display(text);
        }

        return definition?.LabelResourceKey is { Length: > 0 } key
            ? WorkItemLabelDto.Resource(key)
            : WorkItemLabelDto.Resource(FieldUnknownKey);
    }

    private static WorkItemSubtasksDto ToSubtasks(
        IReadOnlyList<TaskItem> children,
        WorkItemActor actor,
        IReadOnlyDictionary<Guid, string> displayNames)
        => new("full", children
            .Select(child => new WorkItemSubtaskDto(
                Id: child.Id.ToString(),
                Title: child.Title,
                /*
                 * The SHARED vocabulary, not a second copy of it. TaskBlockingRules.StateOf is where
                 * lifecycle → contract-state is decided (cancelled is its own value and never folded into
                 * not-started, because called-off work is not waiting to begin). This switch was written out
                 * again here, character-for-character; the browser now reads `in-progress` to say "already
                 * running", so a drift between the two spellings would make that sentence count the wrong
                 * children.
                 */
                Status: TaskBlockingRules.StateOf(child),
                // A subtask carries its OWN holder and date; without them the row can only repeat its title,
                // and "who is doing this and by when" is the reason to look at the list at all.
                Assignee: Person(child.AssigneeUserId, actor, displayNames),
                DueAt: child.DueAt,
                // Same rule as the parent's own cancel action, applied to the SUBTASK's requester. Terminal work
                // cannot be called off again — it has already stopped.
                CanCancel: child.Lifecycle is not (TaskLifecycle.Done or TaskLifecycle.Cancelled)
                    && ((child.CreatedByUserId is not null && child.CreatedByUserId == actor.UserId)
                        || actor.Has(TaskPermissions.Delete))))
            .ToList());

    /// <summary>
    /// Every edge touching this task, in BOTH directions: what it waits on (<c>pred</c>) and what waits on it
    /// (<c>succ</c>). Both, because "who am I holding up" is as much a part of the picture as "who is holding me
    /// up", and only the shell knows which of the two the reader is looking for.
    ///
    /// <para>An edge whose far end cannot be read is DROPPED. A row that can only say "a task" is worse than no
    /// row: it asserts a dependency exists without letting anyone check it.</para>
    /// </summary>
    private static IReadOnlyList<WorkItemDependencyDto> ToDependencies(
        TaskItem task,
        IReadOnlyList<TaskDependency> edges,
        IReadOnlyDictionary<Guid, TaskItem>? edgeTasks)
    {
        if (edges.Count == 0 || edgeTasks is null)
        {
            return [];
        }

        var result = new List<WorkItemDependencyDto>();
        foreach (var edge in edges)
        {
            var isPredecessorEdge = edge.TaskItemId == task.Id;
            var otherId = isPredecessorEdge ? edge.DependsOnTaskItemId : edge.TaskItemId;
            if (!edgeTasks.TryGetValue(otherId, out var other))
            {
                continue;
            }

            result.Add(new WorkItemDependencyDto(
                Id: edge.Id.ToString(),
                // The other task's title is text a person typed, so it crosses as a DISPLAY label.
                Title: WorkItemLabelDto.Display(other.Title),
                Type: edge.DependencyType.ToString(),
                State: TaskBlockingRules.StateOf(other),
                Direction: isPredecessorEdge ? "pred" : "succ",
                // Only an edge this task WAITS on can block it, and only while its predecessor is unsatisfied.
                Blocking: isPredecessorEdge && !TaskBlockingRules.IsSatisfied(edge.DependencyType, other)));
        }

        return result;
    }

    /// <summary>
    /// The blockers this task actually has right now: unsatisfied PREDECESSOR edges only. Each one names the task
    /// in the way, the edge type, and which action it stops, so the client can build a typed sentence without any
    /// localized text crossing the wire.
    /// </summary>
    private static IReadOnlyList<WorkItemBlockerDto> ResolveBlockers(
        TaskItem task,
        IReadOnlyList<TaskDependency> edges,
        IReadOnlyDictionary<Guid, TaskItem>? edgeTasks,
        IReadOnlyList<TaskItem> children)
    {
        /*
         * ORDER MATTERS: dependencies first, then open subtasks. The handler checks the two gates in the same
         * order, and the button's reason is taken from the first blocker on that action — so the reason on screen
         * and the reason in the 409 are the same fact.
         *
         * One blocker PER open subtask, never one summarising them. A blocker names the thing in the way; a
         * bundled "3 subtasks are open" would lose which three, and the client already derives the count from
         * blockers.length.
         */
        return [.. DependencyBlockers(task, edges, edgeTasks), .. SubtaskBlockers(children)];
    }

    /// <summary>
    /// BL-035 — every subtask that is neither done nor cancelled. <c>DependencyType</c> stays null: this is not an
    /// edge, and the DTO left those three fields optional for exactly this case.
    /// </summary>
    private static IReadOnlyList<WorkItemBlockerDto> SubtaskBlockers(IReadOnlyList<TaskItem> children)
        => TaskBlockingRules.OpenSubtasksBlockingCompletion(children)
            .Select(child => new WorkItemBlockerDto(
                Code: WorkAggregationReasonCodes.SubtaskBlocked,
                // The child's own title, so the parent's banner names it — text a person typed, hence display.
                Label: WorkItemLabelDto.Display(child.Title),
                TaskItemId: child.Id.ToString(),
                DependencyType: null,
                AffectedActionCode: TaskBlockingRules.CompleteActionCode))
            .ToList();

    private static IReadOnlyList<WorkItemBlockerDto> DependencyBlockers(
        TaskItem task,
        IReadOnlyList<TaskDependency> edges,
        IReadOnlyDictionary<Guid, TaskItem>? edgeTasks)
    {
        if (edges.Count == 0 || edgeTasks is null)
        {
            return [];
        }

        return edges
            .Where(edge => edge.TaskItemId == task.Id)
            .Select(edge => (Edge: edge, Other: edgeTasks.GetValueOrDefault(edge.DependsOnTaskItemId)))
            .Where(pair => pair.Other is not null
                           && !TaskBlockingRules.IsSatisfied(pair.Edge.DependencyType, pair.Other))
            .Select(pair => new WorkItemBlockerDto(
                Code: WorkAggregationReasonCodes.DependencyBlocked,
                Label: WorkItemLabelDto.Display(pair.Other!.Title),
                TaskItemId: pair.Other.Id.ToString(),
                DependencyType: pair.Edge.DependencyType.ToString(),
                AffectedActionCode: TaskBlockingRules.AffectedActionCode(pair.Edge.DependencyType)))
            .ToList();
    }

    /// <summary>
    /// ONE feed, two kinds, newest first — what happened and what people said about it, read together.
    ///
    /// <para><b>Merged here rather than by the client</b>, because "newest first" has to be decided once. Both
    /// repositories already order their own half by <c>CreatedAt</c> with the same <c>Id</c> tie-break, so this
    /// re-sorts the union by the identical key and the two halves interleave exactly where their timestamps say
    /// they should. A client stitching two pre-sorted lists would have to re-derive that rule, and the day the two
    /// rules disagreed the feed would silently reorder itself.</para>
    ///
    /// <para>An event carries NO text: its sentence is built in the reader's language from the codes in
    /// <see cref="WorkItemActivityEventDto"/>. A comment carries no event. Neither half fakes the other's shape.</para>
    /// </summary>
    /// <summary>
    /// The recorded field changes, FILTERED FOR THIS READER.
    ///
    /// <para>⚠ THE BACK DOOR THIS CLOSES. BL-024 hides a configurable field's VALUE from a caller without its
    /// view permission — and a history that reported "changed X from 45.000 to 52.000" would hand the same value
    /// back through a different door. The rule is asked HERE, on the server, through the same
    /// <c>TaskFieldAccessRules</c> the value goes through, so the two answers cannot drift.</para>
    ///
    /// <para>A field the reader may not see keeps its ROW and loses everything else — including its NAME, which
    /// leaks on its own ("Salary band" tells you the task carries salary data). What remains is that somebody
    /// edited something at that moment, which the entry's actor and timestamp already say. Dropping the row
    /// instead would make a person with fewer permissions see a DIFFERENT history rather than a shorter one, and
    /// the count on screen would disagree between two readers of the same task.</para>
    /// </summary>
    private static IReadOnlyList<WorkItemFieldChangeDto>? ToFieldChanges(
        IReadOnlyList<TaskFieldChange>? changes,
        IReadOnlyDictionary<string, TaskFieldDefinition>? definitions,
        IActorPermissionContext actor)
    {
        if (changes is null || changes.Count == 0)
        {
            return null;
        }

        var catalogue = definitions ?? new Dictionary<string, TaskFieldDefinition>(StringComparer.OrdinalIgnoreCase);

        return changes.Select(change =>
        {
            /*
             * Only a CONFIGURABLE field can be restricted — the built-in ones (a due date, a priority) are part
             * of every task and carry no per-field permission. Treating them as unrestricted here is not a
             * shortcut: there is no rule to consult, and inventing one would hide facts nobody classified.
             */
            if (change.Field != TaskFieldChangeCodes.CustomField)
            {
                return new WorkItemFieldChangeDto(
                    change.Field, Label: null, change.From, change.To, change.ValuesOmitted);
            }

            var definition = change.DefinitionCode is null
                ? null
                : catalogue.GetValueOrDefault(change.DefinitionCode);

            /*
             * The value the rule is asked about is a RECONSTRUCTED one, carrying the definition code and the
             * classification the definition declares. The stored history row is not a TaskFieldValue and never
             * was; what matters is that the same rule sees the same two inputs it sees on the read path.
             *
             * When the definition is gone (retired and purged), CanView falls back to the value's own
             * classification — and a history row has none, so it is treated as the classification the definition
             * WOULD have carried is unknown. Fail closed: unknown means redacted.
             */
            if (definition is null)
            {
                return new WorkItemFieldChangeDto(
                    Field: null, Label: null, From: null, To: null,
                    ValuesOmitted: change.ValuesOmitted, Redacted: true);
            }

            var probe = new TaskFieldValue
            {
                DefinitionCode = definition.Code,
                ValueType = definition.ValueType,
                Classification = definition.Classification
            };

            return TaskFieldAccessRules.CanView(probe, definition, actor)
                ? new WorkItemFieldChangeDto(
                    change.Field, ResolveFieldLabel(definition), change.From, change.To, change.ValuesOmitted)
                : new WorkItemFieldChangeDto(
                    Field: null, Label: null, From: null, To: null,
                    ValuesOmitted: change.ValuesOmitted, Redacted: true);
        }).ToList();
    }

    /// <summary>
    /// The RETURN signal, derived from the history this page has ALREADY read.
    ///
    /// <para>⚠ NO REPOSITORY CALL. <c>transitions</c> is the list <c>GetWorkItemsAsync</c> batched for the whole
    /// page in one read (see the comment on that read), and it is already a parameter here because the activity
    /// feed needs it. Asking a repository for the same rows again would put an N+1 back across every row on
    /// screen — the exact cost that batch exists to avoid — for a fact already in memory.</para>
    ///
    /// <para>The LAST return, not the first: a task returned twice is telling the story of the most recent one,
    /// and the count beside it says the earlier ones happened.</para>
    /// </summary>
    private static WorkItemReturnedDto? ToReturned(IReadOnlyList<TaskTransition>? transitions)
    {
        if (transitions is not { Count: > 0 })
        {
            return null;
        }

        var returns = transitions
            .Where(transition => transition.Kind == TaskTransitionKind.Returned)
            .ToList();

        if (returns.Count == 0)
        {
            // The overwhelming majority. Null rather than a zero-count object: "never returned" is an absence,
            // and an object saying `count: 0` is a signal that has to be inspected before it can be ignored.
            return null;
        }

        var latest = returns.MaxBy(transition => transition.CreatedAt)!;

        return new WorkItemReturnedDto(
            latest.CreatedAt,
            /*
             * DISPLAY, never a resource key: this is the returner's own sentence. Omitted when blank rather than
             * sent as an empty label — the handler requires a reason, so a blank one means a row written before
             * that rule or by something else, and an empty quotation reads as though nobody said anything.
             */
            string.IsNullOrWhiteSpace(latest.Reason) ? null : WorkItemLabelDto.Display(latest.Reason),
            returns.Count);
    }

    /// <summary>
    /// The outcomes a type offers for ONE closure, as the picker's rows — or NULL when it offers none.
    ///
    /// <para>Null rather than an empty list, and the difference is load-bearing: the client reads absence as "this
    /// type asks nothing, close it the way you always did", which is the state every task type written before the
    /// dictionary existed is in. An empty array would promise a picker and then draw no rows.</para>
    /// </summary>
    private static IReadOnlyList<WorkItemClosureOutcomeDto>? ToClosureOutcomes(
        TaskType type, TaskClosureDisposition disposition)
    {
        var offered = TaskTypeRules.OutcomesFor(type, disposition);
        return offered.Count == 0
            ? null
            : offered
                .Select(outcome => new WorkItemClosureOutcomeDto(
                    outcome.Code, OutcomeLabel(outcome), outcome.RequiresReason))
                .ToList();
    }

    /// <summary>
    /// One outcome's label, in the contract's own discriminated shape — a SYSTEM outcome as a resource key the
    /// reader's language resolves, a TENANT outcome as the words its administrator typed.
    ///
    /// <para>No third branch and no fallback to the code: <c>TaskTypeRules.NormalizeClosureOutcomes</c> refuses an
    /// outcome carrying neither label, so an entry reaching here without one cannot have been stored by this
    /// engine. If one ever does, the display half prints the code, which is the honest answer.</para>
    /// </summary>
    private static WorkItemLabelDto OutcomeLabel(TaskClosureOutcome outcome) =>
        string.IsNullOrWhiteSpace(outcome.LabelResourceKey)
            ? WorkItemLabelDto.Display(
                string.IsNullOrWhiteSpace(outcome.LabelText) ? outcome.Code : outcome.LabelText)
            : WorkItemLabelDto.Resource(outcome.LabelResourceKey);

    /// <summary>
    /// The closure record: the stored code, plus its words when the type still offers that outcome.
    ///
    /// <para>A code with no matching outcome keeps the code and loses only the label — see
    /// <see cref="WorkItemClosureDto"/> for why that beats blanking the record of a retired outcome.</para>
    /// </summary>
    private static WorkItemClosureDto? ToClosure(TaskItem task, TaskType? type) =>
        string.IsNullOrWhiteSpace(task.ClosureReasonCode)
            ? null
            : new WorkItemClosureDto(task.ClosureReasonCode, ResolveOutcomeLabel(type, task.ClosureReasonCode));

    /// <summary>The label for a stored code, or null when the type does not (or no longer) offers it.</summary>
    private static WorkItemLabelDto? ResolveOutcomeLabel(TaskType? type, string? reasonCode)
    {
        var code = (reasonCode ?? string.Empty).Trim();
        if (code.Length == 0 || type?.ClosureOutcomes is not { Count: > 0 } outcomes)
        {
            return null;
        }

        var match = outcomes.FirstOrDefault(outcome =>
            string.Equals(outcome.Code, code, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : OutcomeLabel(match);
    }

    private static IReadOnlyList<WorkItemActivityEntryDto> ToActivity(
        IReadOnlyList<TaskComment> comments,
        IReadOnlyList<TaskTransition> transitions,
        IReadOnlyDictionary<Guid, string> displayNames,
        Guid actorUserId,
        IReadOnlyDictionary<string, TaskFieldDefinition>? fieldDefinitions,
        IActorPermissionContext permissions,
        TaskType? taskType)
        => comments
            .Select(comment => new WorkItemActivityEntryDto(
                Id: comment.Id.ToString(),
                Kind: "comment",
                // A WITHDRAWN comment carries no text, and the entity carries none either — the words were
                // cleared at rest, not merely withheld here. What survives is the row, so the feed keeps a
                // marker where somebody spoke and took it back.
                Text: comment.Text,
                // Null rather than a GUID when the name was never resolved: the client has a label for "name
                // unavailable" and an id is not a person.
                Actor: string.IsNullOrWhiteSpace(comment.AuthorDisplayName) ? null : comment.AuthorDisplayName,
                At: comment.CreatedAt,
                Event: null,
                EditedAt: comment.EditedAt,
                WithdrawnAt: comment.WithdrawnAt,
                // The AUTHORITY, decided here and only here. The client has the author's NAME and nothing else,
                // so two people sharing a name would otherwise be handed each other's controls — and the handler
                // would then refuse a button the screen had offered.
                Editable: comment.AuthorUserId == actorUserId && comment.WithdrawnAt is null))
            .Concat(transitions.Select(transition => new WorkItemActivityEntryDto(
                Id: transition.Id.ToString(),
                Kind: "event",
                Text: null,
                // Resolved LIVE from the page's batched directory read, unlike a comment's snapshotted author —
                // see TaskTransition for why the two differ. Absent from the batch means the person could not be
                // resolved, and the row then names nobody rather than printing an id.
                Actor: transition.ActorUserId is { } actorId
                    ? displayNames.GetValueOrDefault(actorId)
                    : null,
                At: transition.CreatedAt,
                Event: new WorkItemActivityEventDto(
                    Code: TaskTransitionCodes.For(transition.Kind),
                    From: transition.FromLifecycle.ToString(),
                    To: transition.ToLifecycle.ToString(),
                    Reason: string.IsNullOrWhiteSpace(transition.Reason) ? null : transition.Reason,
                    FieldChanges: ToFieldChanges(transition.FieldChanges, fieldDefinitions, permissions),
                    /*
                     * Recorded since WC-1 and never projected: the feed carried the actor's words and dropped the
                     * classification beside them. Both travel now — the code is what a report groups by, the
                     * label is what the row reads as.
                     */
                    ReasonCode: string.IsNullOrWhiteSpace(transition.ReasonCode) ? null : transition.ReasonCode,
                    Outcome: ResolveOutcomeLabel(taskType, transition.ReasonCode)))))
            .OrderByDescending(entry => entry.At)
            .ThenByDescending(entry => entry.Id)
            .ToList();

    /// <summary>
    /// Queue names for every pool position on the page, in TWO reads total (positions, then organization units)
    /// rather than one per task.
    ///
    /// <para>The label is "{position} — {unit}", the same composition the assignable-position lookup uses, and for
    /// the same reason: "QA Specialist" alone cannot be told apart across facilities, and pooling is exactly where
    /// that ambiguity routes work to the wrong place.</para>
    ///
    /// <para>Unlike that lookup, this does NOT skip archived or draft positions. That lookup answers "where may
    /// this be pooled", where an unusable position must not be offered; this answers "where is this already
    /// pooled", and work sitting in a queue that has since been archived still needs its queue named. A position
    /// whose unit cannot be resolved simply gets no entry here, and the caller emits an unnamed queue.</para>
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> ResolvePoolLabelsAsync(
        IReadOnlyList<TaskItem> tasks,
        CancellationToken ct)
    {
        var poolPositionIds = tasks
            .Where(task => task.PoolPositionId is not null)
            .Select(task => task.PoolPositionId!.Value)
            .Distinct()
            .ToHashSet();

        if (poolPositionIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var positions = (await _positions.GetAllAsync(ct)).Where(p => poolPositionIds.Contains(p.Id)).ToList();
        if (positions.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var unitById = (await _organizationUnits.GetAllAsync(ct)).ToDictionary(unit => unit.Id);

        var labels = new Dictionary<Guid, string>();
        foreach (var position in positions)
        {
            if (!unitById.TryGetValue(position.OrganizationUnitId, out var unit))
            {
                // Left out deliberately: an unresolvable unit means the queue stays UNNAMED, never half-named.
                continue;
            }

            labels[position.Id] = $"{position.Name} — {unit.Name}";
        }

        return labels;
    }

    /// <summary>
    /// The queue a pooled task waits in. Emitted for pool work only — a directly-assigned task has no queue, and
    /// saying it belongs to one would be inventing a fact.
    ///
    /// <para>An unresolvable position still yields a pool WITH its id and WITHOUT a label. The two tempting
    /// alternatives are both defects this codebase has already paid for: putting the GUID in the label renders a
    /// raw id where a team name belongs, and omitting the field entirely makes the contract reject the item so
    /// the task disappears from the Pool tab (BL-038's lesson — validateItems drops what it cannot validate).</para>
    /// </summary>
    private static WorkItemPoolDto? ToPool(TaskItem task, IReadOnlyDictionary<Guid, string>? poolLabels)
    {
        if (task.AssignmentTarget != TaskAssignmentTarget.PositionPool || task.PoolPositionId is null)
        {
            return null;
        }

        var positionId = task.PoolPositionId.Value;
        var label = poolLabels is not null && poolLabels.TryGetValue(positionId, out var name)
            ? WorkItemLabelDto.Display(name)
            : null;

        return new WorkItemPoolDto(positionId.ToString(), label);
    }

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
        BuildActions(
            TaskItem task,
            WorkItemActor actor,
            bool checklistBlocks,
            bool approvalOutstanding,
            bool reviewOutstanding,
            bool reviewRejected,
            bool initiatorOnly = false)
    {
        var actions = new List<WorkItemActionDto>();
        string? primary = null;

        var isPool = task.AssignmentTarget == TaskAssignmentTarget.PositionPool;
        var unclaimed = isPool && task.AssigneeUserId is null;
        // WHO THE ACTOR IS TO THIS TASK. Read up here rather than beside their first use because the outbox
        // branch below turns on exactly these, and two places deciding "am I the requester" is one place too many.
        var isRequester = task.CreatedByUserId is not null && task.CreatedByUserId == actor.UserId;
        var isHolder = task.AssigneeUserId == actor.UserId;
        var hasSeparateRequester = task.CreatedByUserId is not null && task.CreatedByUserId != task.AssigneeUserId;
        var openOrPlanned = task.Lifecycle is TaskLifecycle.Open or TaskLifecycle.Planned;
        // An approval-gated task is visible but not startable — MOD-0023 must release it first (pack §12 K2).
        // Read from MOD-0023, not from the flag: once approved this is false and `start` becomes enabled.
        var approvalPending = approvalOutstanding && openOrPlanned;

        /*
         * ── BL-016 · THE OUTBOX IS AN OBSERVATIONAL SURFACE ──────────────────────────────────────────────
         *
         * The actor OPENED this work and does not hold it — somebody else does. What may be offered here is
         * decided by ONE question: is this act the requester's, or the holder's?
         *
         * OFFERED (the requester's own levers, both already enforced on the server):
         *   cancel   — TransitionTaskItemHandler answers a non-requester's /cancel with 403 CANCEL_NOT_REQUESTER,
         *              so this is the one act whose authority the engine already ties to whoever asked for the work.
         *   reassign — "this is with the wrong person" is the requester's correction, and the gate below already
         *              reads `isHolder || isRequester`. It leads the row: cancel is destructive and a destructive
         *              act must never be the primary button.
         *
         * WITHHELD, and this is the point of the branch — accept · start · resume · complete · submitReview ·
         * plan · inquire · claim · release · return. Every one of them is a HOLDER's act, and BuildActions gates
         * them on lifecycle and permission ONLY. Measured before this branch existed: an Open task the actor
         * created and assigned to a colleague came back offering `accept` as its primary button. The server
         * refuses the write, so nothing would have broken — but a button that exists to be pressed and answers
         * 403 is worse than no button, and "the server will catch it" is not a reason to draw it.
         *
         * ⚠ WITHHELD, NOT DISABLED, and the difference is deliberate. This card's usual rule is to grey an action
         * and state the reason, because a blocked act is still the reader's act. These are not: they are somebody
         * else's work. A greyed `Tamamla` on every outbox row would say "you could finish this if only…", and the
         * honest sentence — "this is not yours" — is the ROW, not a tooltip on ten dead buttons.
         *
         * ⚠ RECALL (geri çağırma) IS NOT HERE. Taking work back from its holder is a real verb with a real
         * transition and no endpoint behind it today; the spec puts it at v1.5. Projecting it would be the
         * mock-era failure this provider's own remarks refuse — an action with nothing behind it.
         */
        if (initiatorOnly)
        {
            var outbox = new List<WorkItemActionDto>();
            string? outboxPrimary = null;

            // A pooled task has no holder to correct, so reassign is not offered on one — the same rule the
            // holder's path applies, read from the same condition.
            if (!isPool)
            {
                outbox.Add(ReassignAction(task, actor));
                outboxPrimary = "reassign";
            }

            outbox.Add(CancelAction(actor));
            return (outbox, outboxPrimary, outbox.Select(a => a.Code).Where(c => c != outboxPrimary).ToList());
        }

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
            // No instance id means the handoff never reached MOD-0023 (it was down, or the link failed to store):
            // telling the user "waiting for approval" would point at an approver who was never asked.
            var approvalNeverStarted = task.WorkflowInstanceId is null;
            actions.Add(Disabled("start", ActionStartKey, TaskReasonCodes.ApprovalPending,
                approvalNeverStarted ? DisabledApprovalStartFailedKey : DisabledApprovalKey));
            primary = "start";
        }
        else if (task.Lifecycle is TaskLifecycle.Waiting)
        {
            /*
             * Waiting is a real state the holder can be in — blocked on someone else — and nothing was projected
             * for it, which is why the lifecycle was legal in the transition matrix yet dead in the product: a
             * task that reached Waiting had no way back out.
             *
             * The action is the EXISTING start endpoint wearing a resume label. Waiting → InProgress is already
             * allowed by TaskLifecycleService, start already targets InProgress, and the code doubles as the URL
             * segment on the client — so emitting "resume" here would POST to an endpoint that does not exist.
             *
             * The approval gate still applies, and that is NOT a coincidence to rely on: TransitionTaskItemHandler
             * keys its gate on the TARGET lifecycle (InProgress), never on the source, so resuming is gated by the
             * same condition that gates a first start. Disabling it here keeps the reason visible instead of
             * letting the user press a button that will 409.
             */
            actions.Add(approvalOutstanding
                ? Disabled("start", ActionResumeKey, TaskReasonCodes.ApprovalPending, DisabledApprovalKey)
                : Build("start", ActionResumeKey, actor.Has(TaskPermissions.Update)));
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
                /*
                 * Review-gated work does not offer `complete` from here at all — the next step is `submitReview`,
                 * and offering both would put two "I am finished" buttons side by side where only one can work.
                 * `complete` reappears once MOD-0023 has released the review, below.
                 *
                 * Approval is still checked FIRST: it is the gate the user cannot clear themselves.
                 */
                if (task.ReviewRequired && task.ReviewWorkflowInstanceId is null)
                {
                    actions.Add(approvalOutstanding
                        ? Disabled("submitReview", ActionSubmitReviewKey,
                            TaskReasonCodes.ApprovalPending, DisabledApprovalCompleteKey)
                        : checklistBlocks
                            ? Disabled("submitReview", ActionSubmitReviewKey,
                                TaskReasonCodes.ChecklistIncomplete, DisabledChecklistKey)
                            : Build("submitReview", ActionSubmitReviewKey, actor.Has(TaskPermissions.Update),
                                requiresConfirmation: true));
                    primary = "submitReview";
                }
                else
                {
                    // Approval is checked BEFORE the checklist: it is the gate the user cannot clear themselves, so
                    // pointing them at unticked items they can complete without unblocking anything would be a lie.
                    // The server refuses Done in both cases (409), so this is a hint about a real refusal, never the
                    // enforcement.
                    actions.Add(approvalOutstanding
                        ? Disabled("complete", ActionCompleteKey,
                            TaskReasonCodes.ApprovalPending, DisabledApprovalCompleteKey)
                        : checklistBlocks
                            ? Disabled("complete", ActionCompleteKey,
                                TaskReasonCodes.ChecklistIncomplete, DisabledChecklistKey)
                            : Build("complete", ActionCompleteKey, actor.Has(TaskPermissions.Complete),
                                requiresConfirmation: true));
                    primary = "complete";
                }
            }

            /*
             * Sitting with a reviewer, or back from one (Faz 3b).
             *
             * VISIBLE BUT DISABLED while the review is open, never hidden: the whole point of the state is that
             * someone else is holding the work, and a vanished button teaches the reader nothing about who. The
             * server refuses the same write with REVIEW_PENDING, so this is the hint and that is the rule.
             *
             * A REFUSED review offers `submitReview` again rather than a dead end. Unlike a refused approval —
             * which kills the request — a refused review hands the WORK back to the person holding it, and work
             * that came back with nothing to press would be a trap.
             */
            if (task.Lifecycle is TaskLifecycle.PendingReview)
            {
                if (reviewRejected)
                {
                    actions.Add(Build("submitReview", ActionSubmitReviewKey, actor.Has(TaskPermissions.Update),
                        requiresConfirmation: true));
                    primary = "submitReview";
                }
                else if (reviewOutstanding)
                {
                    actions.Add(Disabled("complete", ActionCompleteKey,
                        TaskReasonCodes.ReviewPending, DisabledReviewCompleteKey));
                    primary = "complete";
                }
                else
                {
                    // Released: the reviewer is done, so completion is the holder's again.
                    actions.Add(checklistBlocks
                        ? Disabled("complete", ActionCompleteKey,
                            TaskReasonCodes.ChecklistIncomplete, DisabledChecklistKey)
                        : Build("complete", ActionCompleteKey, actor.Has(TaskPermissions.Complete),
                            requiresConfirmation: true));
                    primary = "complete";
                }
            }
        }

        // Planning a personal date is available while the work has not started (Open ⇄ Planned on the server).
        if (openOrPlanned && !unclaimed)
        {
            actions.Add(Build("plan", ActionPlanKey, actor.Has(TaskPermissions.Update)));
        }

        /*
         * Saying "I am blocked" — the ENTRY to Waiting, offered on work the actor actually holds and has not
         * finished. Without it a blocked task either keeps looking active or gets cancelled, and neither is true.
         *
         * The code is `inquire` and the endpoint is POST {id}/inquire: the client turns an action code straight
         * into the URL segment, so the two names are one name. `requestInfo` is MOD-0023's verb for an approver
         * asking a submitter for more information and is deliberately untouched.
         */
        if (!unclaimed && task.Lifecycle is TaskLifecycle.Open or TaskLifecycle.Planned or TaskLifecycle.InProgress)
        {
            actions.Add(Build("inquire", ActionInquireKey, actor.Has(TaskPermissions.Update), requiresReason: true));
        }

        /*
         * Handing work back, and handing it on. Until these existed the only way out of unwanted work was
         * `cancel`, which means the opposite — the request is destroyed rather than declined.
         *
         * `return` goes to the REQUESTER, so it is offered only when there is a separate requester to return it
         * to: returning your own self-assigned task to yourself is a no-op dressed as an action. Only the holder
         * may do it, which is why it is not offered on unclaimed pool work either.
         *
         * `reassign` is the holder's (delegating) or the requester's (correcting). It is never offered on pooled
         * work: a pool is claimed and released, and naming a holder it does not have would contradict that.
         */
        if (!isPool && isHolder && hasSeparateRequester)
        {
            actions.Add(Build("return", ActionReturnKey, actor.Has(TaskPermissions.Update), requiresReason: true));
        }

        if (!isPool && (isHolder || isRequester))
        {
            // Drawn-and-greyed rather than hidden when the task forbids delegation — see ReassignAction.
            actions.Add(ReassignAction(task, actor));
        }

        // Only a pooled task that someone has taken can be handed back to the pool.
        if (isPool && !unclaimed)
        {
            actions.Add(Build("release", ActionReleaseKey, actor.Has(TaskPermissions.Claim),
                requiresConfirmation: true));
        }

        /*
         * Cancelling is the REQUESTER's right, not the assignee's.
         *
         * This used to be projected unconditionally, so being handed a task was enough to call the whole thing
         * off — the recipient could cancel the requester's work. That is the wrong half of the SAP/ServiceNow
         * split: an assignee who does not want the work RETURNS it; only the person who asked for it (or someone
         * with administrative authority over the record) cancels it.
         *
         * "Administrative authority" is bound to the DELETE permission because it is the only declared key that
         * already means power over any task record, and cancelling is strictly weaker than deleting. A dedicated
         * platform.tasks.cancel-any would say it more precisely, but adding a permission is a manifest + role-sync
         * change, not a projection one — recorded rather than smuggled in here.
         *
         * The refusal is ENFORCED, not merely projected: TransitionTaskItemHandler answers a /cancel POST from a
         * non-requester with 403 CANCEL_NOT_REQUESTER (TaskWaitingAndCancelAuthorityTests). This note used to say
         * enforcement was a follow-up and stayed behind after it shipped — a hidden control is presentation, the
         * refusal is the rule, and both are in place here.
         */
        if (isRequester || actor.Has(TaskPermissions.Delete))
        {
            actions.Add(CancelAction(actor));
        }

        // Everything that is not the primary belongs in the overflow menu, in the order built above.
        var overflow = actions
            .Select(action => action.Code)
            .Where(code => code != primary)
            .ToList();

        return (actions, primary, overflow);
    }

    /// <summary>
    /// Handing work on — the holder's (delegating) or the requester's (correcting).
    ///
    /// <para>⚠ DISABLED WITH A REASON, never withheld. This card's rule is that an action whose reason cannot be
    /// stated is not drawn at all — and here the reason is plain ("this task may not be delegated"), so the button
    /// is drawn, greyed, and explains itself. Hiding it would leave the holder wondering why a task they hold
    /// cannot be handed on.</para>
    ///
    /// <para>The task's own policy is checked BEFORE the permission: "nobody may delegate this" outranks "you may
    /// not delegate", and reporting the permission first would send a reader after an authority that would never
    /// help.</para>
    ///
    /// <para>One factory rather than one construction per caller: the holder's path and BL-016's outbox path both
    /// offer this act, and two copies would be free to drift on the policy-before-permission order.</para>
    /// </summary>
    private static WorkItemActionDto ReassignAction(TaskItem task, WorkItemActor actor)
        => !task.DelegationAllowed
            ? Disabled("reassign", ActionReassignKey,
                TaskReasonCodes.DelegationNotAllowed, DisabledDelegationKey)
            : Build("reassign", ActionReassignKey, actor.Has(TaskPermissions.Assign), requiresReason: true);

    /// <summary>
    /// Calling the work off — the REQUESTER's right, not the assignee's (see the caller for the authority rule and
    /// for why administrative cancellation is bound to the delete permission). Shared with BL-016's outbox path for
    /// the same reason <see cref="ReassignAction"/> is.
    /// </summary>
    private static WorkItemActionDto CancelAction(WorkItemActor actor)
        => Build("cancel", ActionCancelKey, actor.Has(TaskPermissions.Cancel),
            requiresConfirmation: true, riskLevel: "destructive");

    private static WorkItemActionDto Build(
        string code,
        string labelKey,
        bool permitted,
        bool requiresConfirmation = false,
        string riskLevel = "normal",
        bool requiresReason = false)
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
                RequiresReason: requiresReason,
                RequiresEvidence: false,
                SupportsBulk: false,
                RiskLevel: riskLevel)
            : Disabled(code, labelKey, WorkAggregationReasonCodes.PermissionDenied, DisabledPermissionKey);

    /// <summary>
    /// Turn an offered action into a disabled one, KEEPING its own label. The resume button says "Resume", not
    /// "Start", and rebuilding the label from a code would rename the button at the moment it is blocked.
    /// </summary>
    private static WorkItemActionDto AsDisabled(WorkItemActionDto action, string reasonCode, string reasonKey)
        => action with
        {
            Enabled = false,
            DisabledReasonCode = reasonCode,
            DisabledReason = WorkItemLabelDto.Resource(reasonKey)
        };

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
        return await _seats.PositionIdsForUserAsync(userId, ct);
    }
}
