using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;

public sealed class GetTaskItemByIdHandler : IRequestHandler<GetTaskItemByIdQuery, Response<TaskItemDetailDto>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly ITaskWatcherRepository _watchers;
    private readonly ITaskDependencyRepository _dependencies;
    private readonly ITaskLifecycleService _lifecycle;
    private readonly ITaskApprovalService _approvals;

    public GetTaskItemByIdHandler(
        ITaskItemRepository tasks,
        ITaskWatcherRepository watchers,
        ITaskDependencyRepository dependencies,
        ITaskLifecycleService lifecycle,
        ITaskApprovalService approvals)
    {
        _tasks = tasks;
        _watchers = watchers;
        _dependencies = dependencies;
        _lifecycle = lifecycle;
        _approvals = approvals;
    }

    public async Task<Response<TaskItemDetailDto>> Handle(GetTaskItemByIdQuery request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(request.Id, ct);
        if (task is null)
        {
            // Cross-tenant reads land here too: the repository filter hides the row, so the caller learns
            // nothing about its existence (no metadata leak).
            return Response<TaskItemDetailDto>.Fail(
                "Task not found.", 404, TaskReasonCodes.NotFound, request.CorrelationId);
        }

        var watchers = await _watchers.ListByTaskIdAsync(task.Id, ct);
        var dependencies = await _dependencies.ListByTaskIdAsync(task.Id, ct);

        // Same shared rule as the list and the projection, for one task.
        var approvalStates = task.ApprovalRequired && task.WorkflowInstanceId is { } instanceId
            ? await _approvals.GetStatesAsync([instanceId], ct)
            : new Dictionary<Guid, TaskApprovalState>();
        var (approvalOutstanding, approvalRejected) = TaskApprovalView.Resolve(task, approvalStates);

        return Response<TaskItemDetailDto>.Success(
            TaskItemMapper.ToDetail(task, _lifecycle, approvalOutstanding, approvalRejected, watchers, dependencies),
            correlationId: request.CorrelationId);
    }
}
