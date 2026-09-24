using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Features.Workflow;
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

    public WorkflowApprovalWorkItemProvider(
        IApprovalTaskRepository tasks,
        IRuntimeAssignmentSnapshotRepository snapshots,
        IWorkflowInstanceRepository instances,
        IWorkItemProjectionService projection,
        // BL-437 — the source owners that can say what an approval is about. Optional so a caller that wires none
        // (the existing tests) gets exactly the old projection.
        IEnumerable<IApprovalSourceResolver>? sourceResolvers = null,
        ILogger<WorkflowApprovalWorkItemProvider>? logger = null)
    {
        _tasks = tasks;
        _snapshots = snapshots;
        _instances = instances;
        _projection = projection;
        _sourceResolvers = sourceResolvers?.ToList() ?? [];
        _logger = logger ?? NullLogger<WorkflowApprovalWorkItemProvider>.Instance;
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
        var candidates = new List<ApprovalTask>();

        foreach (var task in all.Where(t => ActionableStatuses.Contains(t.Status)))
        {
            if (!await IsCandidateAsync(task, userId, ct))
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

        var items = new List<WorkItemProjectionDto>();
        foreach (var task in candidates)
        {
            var instance = instanceCache[task.WorkflowInstanceId];
            var context = instance is not null && contexts.TryGetValue(instance.Id, out var found) ? found : null;

            var projected = _projection.Project(task, instance, actor, ProviderCode, ProviderContractVersion, context);
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

        foreach (var resolver in _sourceResolvers)
        {
            var owned = instances.Where(i => resolver.Handles(i.ObjectType) && !contexts.ContainsKey(i.Id)).ToList();
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

    // Candidate resolution mirrors GetMyWorkflowTasks: the directly resolved assignee always sees the task;
    // otherwise the caller must be the resolved principal or a candidate in the assignment snapshot.
    private async Task<bool> IsCandidateAsync(ApprovalTask task, string userId, CancellationToken ct)
    {
        if (Matches(task.AssigneeRef, userId))
        {
            return true;
        }

        if (task.AssignmentSnapshotId is not { } snapshotId)
        {
            return false;
        }

        var snapshot = await _snapshots.GetByIdAsync(snapshotId, ct);
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
