using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
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

    /// <summary>BL-024 Phase 2 — the catalogue that says which fields are restricted, and who is asking.</summary>
    private readonly ITaskFieldDefinitionRepository _fieldDefinitions;
    private readonly IActorPermissionContext _actor;

    public GetTaskItemByIdHandler(
        ITaskItemRepository tasks,
        ITaskWatcherRepository watchers,
        ITaskDependencyRepository dependencies,
        ITaskLifecycleService lifecycle,
        ITaskApprovalService approvals,
        ITaskFieldDefinitionRepository fieldDefinitions,
        IActorPermissionContext actor)
    {
        _tasks = tasks;
        _watchers = watchers;
        _dependencies = dependencies;
        _lifecycle = lifecycle;
        _approvals = approvals;
        _fieldDefinitions = fieldDefinitions;
        _actor = actor;
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
        // Both decisions in ONE read: GetStatesAsync keys off the instance id and never asks what the instance
        // decides, so approval and review share it (Faz 3b).
        var gatedInstanceIds = new List<Guid>();
        if (task.ApprovalRequired && task.WorkflowInstanceId is { } instanceId)
        {
            gatedInstanceIds.Add(instanceId);
        }

        if (task.ReviewRequired && task.ReviewWorkflowInstanceId is { } reviewInstanceId)
        {
            gatedInstanceIds.Add(reviewInstanceId);
        }

        var approvalStates = gatedInstanceIds.Count > 0
            ? await _approvals.GetStatesAsync(gatedInstanceIds, ct)
            : new Dictionary<Guid, TaskApprovalState>();
        var (approvalOutstanding, approvalRejected) = TaskApprovalView.Resolve(task, approvalStates);
        var (reviewOutstanding, reviewRejected) = TaskReviewView.Resolve(task, approvalStates);

        /*
         * BL-024 Phase 2 — the catalogue, read only when this task actually carries values.
         *
         * ListAllAsync rather than ListActiveAsync: a RETIRED definition still governs the values written under
         * it. Reading only the active ones would make retiring a definition a way to unhide every value it ever
         * protected, which is a deactivation turning into a disclosure.
         */
        var definitions = task.FieldValues.Count == 0
            ? null
            : (await _fieldDefinitions.ListAllAsync(ct)).ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);

        return Response<TaskItemDetailDto>.Success(
            TaskItemMapper.ToDetail(
                task, _lifecycle, approvalOutstanding, approvalRejected, watchers, dependencies,
                _actor, definitions, reviewOutstanding, reviewRejected),
            correlationId: request.CorrelationId);
    }
}
