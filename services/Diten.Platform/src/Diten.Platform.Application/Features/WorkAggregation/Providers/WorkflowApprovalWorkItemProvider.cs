using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.Platform.Application.Features.WorkAggregation.Providers;

// WC-1 (DCP-004) — the MOD-0023 approval provider (Binding A, the only provider bound in
// WC-1). It surfaces the current actor's ACTIONABLE approval tasks (parity with the GetMyWorkflowTasks
// foundation) and projects each into the canonical work item via the pure projection service.
//
// READ-ONLY: only repository reads are performed (GetAllForTenant / GetById); no write path is touched. All
// reads are tenant-scoped by the live TenantRepository<T>, so a cross-tenant task never enters the result.
public sealed class WorkflowApprovalWorkItemProvider : IWorkItemProvider
{
    // Non-terminal, actionable states (someone can still act). Terminal + Delegated are excluded from the
    // live inbox exactly as GetMyWorkflowTasks does; the projection service still maps every status (terminal
    // read-only, Delegated hidden) so a future history view needs no rewrite.
    private static readonly HashSet<ApprovalTaskStatus> ActionableStatuses =
    [
        ApprovalTaskStatus.WaitingApproval,
        ApprovalTaskStatus.WaitingEvidence,
        ApprovalTaskStatus.Escalated
    ];

    private readonly IApprovalTaskRepository _tasks;
    private readonly IRuntimeAssignmentSnapshotRepository _snapshots;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IWorkItemProjectionService _projection;
    private readonly IReadOnlyList<IApprovalSourceResolver> _sourceResolvers;
    private readonly ILogger<WorkflowApprovalWorkItemProvider> _logger;
    private readonly IWorkflowTemplateVersionRepository? _versions;
    private readonly IPositionRepository? _positions;

    public WorkflowApprovalWorkItemProvider(
        IApprovalTaskRepository tasks,
        IRuntimeAssignmentSnapshotRepository snapshots,
        IWorkflowInstanceRepository instances,
        IWorkItemProjectionService projection,
        // BL-437 — the source owners that can say what an approval is about. Optional so a caller that wires none
        // (the existing tests) gets exactly the old projection.
        IEnumerable<IApprovalSourceResolver>? sourceResolvers = null,
        ILogger<WorkflowApprovalWorkItemProvider>? logger = null,
        // REQ-WCN-01 — the pinned template version (step name, candidate references) and the tenant's positions
        // (their names). Optional and trailing: a caller that wires neither gets exactly the old projection.
        IWorkflowTemplateVersionRepository? versions = null,
        IPositionRepository? positions = null)
    {
        _tasks = tasks;
        _snapshots = snapshots;
        _instances = instances;
        _projection = projection;
        _sourceResolvers = sourceResolvers?.ToList() ?? [];
        _logger = logger ?? NullLogger<WorkflowApprovalWorkItemProvider>.Instance;
        _versions = versions;
        _positions = positions;
    }

    public string ProviderCode => WorkItemContract.ProviderCodeWorkflow;

    public string ProviderContractVersion => "1.0";

    /// <summary>
    /// The four approval-action permissions WorkItemProjectionService consults. These are exactly the keys the
    /// API layer used to hardcode, moved to their owner — MOD-0023's behaviour is unchanged.
    /// </summary>
    public IReadOnlyCollection<string> RequiredActionPermissions { get; } =
    [
        WorkflowPermissions.TasksApprove,
        WorkflowPermissions.TasksReject,
        WorkflowPermissions.TasksRequestInfo,
        WorkflowPermissions.TasksDelegate
    ];

    public async Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(
        WorkItemActor actor,
        CancellationToken ct = default)
    {
        var userId = actor.UserId.ToString();
        var all = await _tasks.GetAllForTenantAsync(ct);

        var instanceCache = new Dictionary<Guid, WorkflowInstance?>();
        // A snapshot read for the candidate check is not read again for the step context below.
        var snapshotCache = new Dictionary<Guid, RuntimeAssignmentSnapshot?>();
        var candidates = new List<ApprovalTask>();

        foreach (var task in all.Where(t => ActionableStatuses.Contains(t.Status)))
        {
            if (!await IsCandidateAsync(task, userId, snapshotCache, ct))
            {
                continue;
            }

            if (!instanceCache.ContainsKey(task.WorkflowInstanceId))
            {
                instanceCache[task.WorkflowInstanceId] = await _instances.GetByIdAsync(task.WorkflowInstanceId, ct);
            }

            candidates.Add(task);
        }

        var contexts = await ResolveSourceContextsAsync(
            instanceCache.Values.OfType<WorkflowInstance>().ToList(), actor, ct);

        var steps = await ResolveStepContextsAsync(candidates, instanceCache, snapshotCache, ct);

        var items = new List<WorkItemProjectionDto>();
        foreach (var task in candidates)
        {
            var instance = instanceCache[task.WorkflowInstanceId];
            var context = instance is not null && contexts.TryGetValue(instance.Id, out var found) ? found : null;
            var step = steps.TryGetValue(task.Id, out var foundStep) ? foundStep : null;

            var projected = _projection.Project(
                task, instance, actor, ProviderCode, ProviderContractVersion, context, step);
            if (projected is not null)
            {
                items.Add(projected);
            }
        }

        return items;
    }

    /*
     * BL-437 — ask each source owner once, with every instance of its type on this page. An owner that throws does
     * not take the approvals down with it: those rows fall back to the generic title — the board stays whole, and
     * the decision is still there to take.
     */
    private async Task<IReadOnlyDictionary<Guid, ApprovalSourceContext>> ResolveSourceContextsAsync(
        IReadOnlyList<WorkflowInstance> instances,
        WorkItemActor actor,
        CancellationToken ct)
    {
        var contexts = new Dictionary<Guid, ApprovalSourceContext>();
        if (instances.Count == 0 || _sourceResolvers.Count == 0)
        {
            return contexts;
        }

        // WP-CL-BE-3 — owners first; a fallback (the starter's display-context snapshot) only ever answers for an object
        // type NO owner claims, whatever the DI registration order is.
        var owners = _sourceResolvers.Where(r => r is not IFallbackApprovalSourceResolver).ToList();
        foreach (var resolver in owners.Concat(_sourceResolvers.OfType<IFallbackApprovalSourceResolver>()))
        {
            var isFallback = resolver is IFallbackApprovalSourceResolver;
            var owned = instances
                .Where(i => resolver.Handles(i.ObjectType) && !contexts.ContainsKey(i.Id))
                .Where(i => !isFallback || !owners.Any(o => o.Handles(i.ObjectType)))
                .ToList();
            if (owned.Count == 0)
            {
                continue;
            }

            IReadOnlyDictionary<Guid, ApprovalSourceContext> answered;
            try
            {
                answered = await resolver.ResolveAsync(owned, actor, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "Approval source resolver {Resolver} failed for {Count} instance(s); those approvals keep the "
                    + "generic title and carry no requester or link.", resolver.GetType().Name, owned.Count);
                continue;
            }

            foreach (var (instanceId, context) in answered)
            {
                contexts.TryAdd(instanceId, context);
            }
        }

        return contexts;
    }

    private const string PositionPrefix = "position:";

    /*
     * REQ-WCN-01 (W-1, W-3) — what the PINNED template version says about each task's step: its name, and — when the
     * step names no person directly — the names of its candidate positions.
     *
     * Read-only and batched: each distinct version is read once, and position names come from ONE tenant-scoped read
     * for the whole page (never one per item). A failing read costs the badge and the names, never the approval: the
     * rows keep today's shape and the decision is still there to take.
     */
    private async Task<IReadOnlyDictionary<Guid, ApprovalStepContext>> ResolveStepContextsAsync(
        IReadOnlyList<ApprovalTask> tasks,
        IReadOnlyDictionary<Guid, WorkflowInstance?> instances,
        Dictionary<Guid, RuntimeAssignmentSnapshot?> snapshots,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, ApprovalStepContext>();
        if (_versions is null || tasks.Count == 0)
        {
            return result;
        }

        try
        {
            var plans = new Dictionary<Guid, IReadOnlyList<WorkflowRuntimeStep>>();
            var stepByTask = new Dictionary<Guid, WorkflowRuntimeStep>();
            foreach (var task in tasks)
            {
                if (instances.GetValueOrDefault(task.WorkflowInstanceId)?.TemplateVersionId is not { } versionId)
                {
                    continue;
                }

                if (!plans.TryGetValue(versionId, out var plan))
                {
                    plan = WorkflowDefinitionRuntimePlan.FromVersion(await _versions.GetByIdAsync(versionId, ct));
                    plans[versionId] = plan;
                }

                var index = WorkflowDefinitionRuntimePlan.IndexOf(plan, task.StageCode, task.StepCode);
                if (index >= 0)
                {
                    stepByTask[task.Id] = plan[index];
                }
            }

            /*
             * Whose candidate positions are worth naming: the step names NO person directly, AND the task still sits
             * with the step's own candidates. That second half is read from the task's CURRENT assignment snapshot,
             * not from its status: a delegation hands the task to one named person and puts it back to
             * WaitingApproval (an escalated task included), so the status alone would go on naming the step's
             * positions for a decision that now waits on somebody else.
             */
            var positionIdsByTask = new Dictionary<Guid, IReadOnlyList<Guid>>();
            foreach (var task in tasks)
            {
                if (!stepByTask.TryGetValue(task.Id, out var ownStep))
                {
                    continue;
                }

                var ids = CandidatePositionIds(ownStep);
                if (ids.Count > 0 && await StillWithTheStepsOwnCandidatesAsync(task, snapshots, ct))
                {
                    positionIdsByTask[task.Id] = ids;
                }
            }

            // The names are an extra on top of the step name: a position read that fails costs the names only.
            IReadOnlyDictionary<Guid, string> positionNames;
            try
            {
                positionNames = await ReadPositionNamesAsync(
                    positionIdsByTask.Values.SelectMany(ids => ids).ToHashSet(),
                    tasks.Select(task => task.TenantId).ToHashSet(),
                    ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex,
                    "Candidate position names could not be read; {Count} approval(s) keep their step name and carry "
                    + "no candidate positions.", positionIdsByTask.Count);
                positionNames = new Dictionary<Guid, string>();
            }

            foreach (var task in tasks)
            {
                if (!stepByTask.TryGetValue(task.Id, out var step))
                {
                    continue;
                }

                // The plan falls back to the step CODE when the definition gives no name; a code is not a name.
                var stepName = string.Equals(step.StepName, step.StepCode, StringComparison.Ordinal)
                    ? null
                    : step.StepName;
                var names = positionIdsByTask.TryGetValue(task.Id, out var ids)
                    ? ids.Where(positionNames.ContainsKey).Select(id => positionNames[id]).ToList()
                    : [];

                if (stepName is not null || names.Count > 0)
                {
                    result[task.Id] = new ApprovalStepContext(stepName, names);
                }
            }

            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex,
                "Approval step context could not be read for {Count} task(s); those approvals carry no step name and "
                + "no candidate positions.", tasks.Count);
            return new Dictionary<Guid, ApprovalStepContext>();
        }
    }

    /// <summary>
    /// The assignment snapshots MOD-0023 writes when a step OPENS — the task is with the step's own candidates.
    /// </summary>
    internal static readonly IReadOnlySet<string> StepOwnResolverSources =
        new HashSet<string>(StringComparer.Ordinal) { "runtime_candidates", "runtime_next_step_candidates" };

    /// <summary>
    /// The snapshots MOD-0023 writes when a task LEAVES its step's candidates: handed to one person (delegation) or
    /// to the escalation principals. Every resolver source MOD-0023 writes is in one of the two sets — a new one is
    /// classified here before it ships (ApprovalStepContextTests scans the production handlers for them).
    /// </summary>
    internal static readonly IReadOnlySet<string> MovedOnResolverSources =
        new HashSet<string>(StringComparer.Ordinal) { "delegate_request", "escalation_rules" };

    /// <summary>
    /// Fail-closed: positions are named only for a snapshot KNOWN to be the step's own. A task with no snapshot at
    /// all has nothing saying it moved; a snapshot that cannot be read, or whose source is not classified, names
    /// nobody.
    /// </summary>
    private async Task<bool> StillWithTheStepsOwnCandidatesAsync(
        ApprovalTask task,
        Dictionary<Guid, RuntimeAssignmentSnapshot?> snapshots,
        CancellationToken ct)
    {
        if (task.Status == ApprovalTaskStatus.Escalated)
        {
            return false;
        }

        if (task.AssignmentSnapshotId is not { } snapshotId)
        {
            return true;
        }

        if (!snapshots.TryGetValue(snapshotId, out var snapshot))
        {
            snapshot = await _snapshots.GetByIdAsync(snapshotId, ct);
            snapshots[snapshotId] = snapshot;
        }

        return snapshot is not null && StepOwnResolverSources.Contains(snapshot.ResolverSource);
    }

    // The step's position candidates, in definition order — empty when the step names any person directly.
    private static IReadOnlyList<Guid> CandidatePositionIds(WorkflowRuntimeStep step)
    {
        var ids = new List<Guid>();
        foreach (var candidate in step.CandidatePrincipalIds)
        {
            if (candidate.StartsWith(PositionPrefix, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(candidate[PositionPrefix.Length..].Trim(), out var positionId))
            {
                if (!ids.Contains(positionId))
                {
                    ids.Add(positionId);
                }

                continue;
            }

            // `user:{id}` or a bare principal id — a person named directly.
            return [];
        }

        return ids;
    }

    /*
     * ONE read for the page. The repository is tenant-scoped; the tenant comparison below is the second lock — a
     * position of another tenant is never named, whatever the repository returns. Only a live seat is named (the same
     * rule MOD-0023 uses to hand out approvals): a closed or archived position is waiting on nobody.
     */
    private async Task<IReadOnlyDictionary<Guid, string>> ReadPositionNamesAsync(
        IReadOnlyCollection<Guid> positionIds,
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken ct)
    {
        if (_positions is null || positionIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return (await _positions.GetByIdsAsync(positionIds, ct))
            .Where(position => positionIds.Contains(position.Id))
            .Where(position => tenantIds.Contains(position.TenantId))
            .Where(WorkflowCandidateResolver.IsLivePosition)
            .Where(position => !string.IsNullOrWhiteSpace(position.Name))
            .GroupBy(position => position.Id)
            .ToDictionary(group => group.Key, group => group.First().Name.Trim());
    }

    // Candidate resolution mirrors GetMyWorkflowTasks: the directly resolved assignee always sees the task;
    // otherwise the caller must be the resolved principal or a candidate in the assignment snapshot.
    private async Task<bool> IsCandidateAsync(
        ApprovalTask task, string userId, Dictionary<Guid, RuntimeAssignmentSnapshot?> snapshots, CancellationToken ct)
    {
        if (Matches(task.AssigneeRef, userId))
        {
            return true;
        }

        if (task.AssignmentSnapshotId is not { } snapshotId)
        {
            return false;
        }

        if (!snapshots.TryGetValue(snapshotId, out var snapshot))
        {
            snapshot = await _snapshots.GetByIdAsync(snapshotId, ct);
            snapshots[snapshotId] = snapshot;
        }

        if (snapshot is null)
        {
            return false;
        }

        return Matches(snapshot.ResolvedPrincipalId, userId)
               || snapshot.CandidatePrincipalIds.Any(id => Matches(id, userId));
    }

    private static bool Matches(string? principalId, string userId)
        => !string.IsNullOrWhiteSpace(principalId)
           && string.Equals(principalId.Trim(), userId, StringComparison.OrdinalIgnoreCase);
}
