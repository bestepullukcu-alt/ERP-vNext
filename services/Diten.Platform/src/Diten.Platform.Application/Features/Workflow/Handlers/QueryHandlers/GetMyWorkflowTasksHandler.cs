using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetMyWorkflowTasksHandler
    : IRequestHandler<GetMyWorkflowTasksQuery, Response<IReadOnlyList<WorkflowTaskDto>>>
{
    // Non-terminal, actionable task states (someone can still act). Excludes Approved/Rejected/Cancelled/
    // TimedOut/Delegated.
    private static readonly HashSet<ApprovalTaskStatus> ActionableStatuses =
    [
        ApprovalTaskStatus.WaitingApproval,
        ApprovalTaskStatus.WaitingEvidence,
        ApprovalTaskStatus.Escalated
    ];

    private readonly IApprovalTaskRepository _tasks;
    private readonly IRuntimeAssignmentSnapshotRepository _snapshots;
    private readonly ICurrentUserContext _currentUser;

    public GetMyWorkflowTasksHandler(
        IApprovalTaskRepository tasks,
        IRuntimeAssignmentSnapshotRepository snapshots,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _snapshots = snapshots;
        _currentUser = currentUser;
    }

    public async Task<Response<IReadOnlyList<WorkflowTaskDto>>> Handle(GetMyWorkflowTasksQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId.ToString();

        var all = await _tasks.GetAllForTenantAsync(ct);
        var mine = new List<ApprovalTask>();

        foreach (var task in all.Where(t => ActionableStatuses.Contains(t.Status)))
        {
            if (await IsCandidateAsync(task, userId, ct))
            {
                mine.Add(task);
            }
        }

        IReadOnlyList<WorkflowTaskDto> result = mine
            .OrderBy(t => t.DueAt ?? DateTimeOffset.MaxValue)
            .Select(WorkflowDefinitionMapper.ToTask)
            .ToList();

        return Response<IReadOnlyList<WorkflowTaskDto>>.Success(result, correlationId: request.CorrelationId);
    }

    private async Task<bool> IsCandidateAsync(ApprovalTask task, string userId, CancellationToken ct)
    {
        // The directly resolved assignee always sees the task.
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
