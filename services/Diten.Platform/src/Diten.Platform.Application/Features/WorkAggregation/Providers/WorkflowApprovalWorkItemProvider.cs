using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;

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

    public WorkflowApprovalWorkItemProvider(
        IApprovalTaskRepository tasks,
        IRuntimeAssignmentSnapshotRepository snapshots,
        IWorkflowInstanceRepository instances,
        IWorkItemProjectionService projection)
    {
        _tasks = tasks;
        _snapshots = snapshots;
        _instances = instances;
        _projection = projection;
    }

    public string ProviderCode => WorkItemContract.ProviderCodeWorkflow;

    public string ProviderContractVersion => "1.0";

    public async Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(
        WorkItemActor actor,
        CancellationToken ct = default)
    {
        var userId = actor.UserId.ToString();
        var all = await _tasks.GetAllForTenantAsync(ct);

        var instanceCache = new Dictionary<Guid, WorkflowInstance?>();
        var items = new List<WorkItemProjectionDto>();

        foreach (var task in all.Where(t => ActionableStatuses.Contains(t.Status)))
        {
            if (!await IsCandidateAsync(task, userId, ct))
            {
                continue;
            }

            if (!instanceCache.TryGetValue(task.WorkflowInstanceId, out var instance))
            {
                instance = await _instances.GetByIdAsync(task.WorkflowInstanceId, ct);
                instanceCache[task.WorkflowInstanceId] = instance;
            }

            var projected = _projection.Project(task, instance, actor, ProviderCode, ProviderContractVersion);
            if (projected is not null)
            {
                items.Add(projected);
            }
        }

        return items;
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
