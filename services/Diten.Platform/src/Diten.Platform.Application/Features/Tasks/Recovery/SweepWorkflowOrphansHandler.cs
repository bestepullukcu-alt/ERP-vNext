using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Recovery;

/// <summary>BL-540 (1) — see <see cref="SweepWorkflowOrphansCommand"/>.</summary>
public sealed class SweepWorkflowOrphansHandler
    : IRequestHandler<SweepWorkflowOrphansCommand, Response<WorkflowOrphanSweepResult>>
{
    public const int DefaultMaxItems = 200;
    public const int MaxMaxItems = 1000;

    private readonly IWorkflowStartRecoveryStore _store;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _approvalTasks;
    private readonly ITaskItemRepository _taskItems;
    private readonly IMediator _mediator;

    public SweepWorkflowOrphansHandler(
        IWorkflowStartRecoveryStore store,
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository approvalTasks,
        ITaskItemRepository taskItems,
        IMediator mediator)
    {
        _store = store;
        _instances = instances;
        _approvalTasks = approvalTasks;
        _taskItems = taskItems;
        _mediator = mediator;
    }

    public async Task<Response<WorkflowOrphanSweepResult>> Handle(SweepWorkflowOrphansCommand request, CancellationToken ct)
    {
        var max = request.MaxItems <= 0 ? DefaultMaxItems : Math.Min(request.MaxItems, MaxMaxItems);
        var open = await _store.ListOpenGenericInstancesAsync(
            [TaskApprovalService.ApprovalObjectType, TaskReviewService.ReviewObjectType], max, ct);

        var taskIds = open.Select(i => Guid.TryParse(i.ObjectId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToList();
        var liveTasks = (await _taskItems.ListByIdsAsync(taskIds, ct)).ToDictionary(t => t.Id);

        var now = DateTimeOffset.UtcNow;
        var items = new List<WorkflowOrphanItem>();
        var closed = 0;
        foreach (var instance in open)
        {
            var reason = await OrphanReasonAsync(instance, liveTasks, ct);
            if (reason is null)
            {
                continue;
            }

            var openTasks = (await _approvalTasks.ListByInstanceIdAsync(instance.Id, ct))
                .Where(t => t.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence
                    or ApprovalTaskStatus.Escalated)
                .Select(t => t.Id)
                .ToList();
            string? outcome = null;
            if (request.Apply)
            {
                outcome = openTasks.Count == 0 ? "NO_OPEN_TASK" : "CLOSED";
                foreach (var taskId in openTasks)
                {
                    var cancelled = await _mediator.Send(new CancelWorkflowTaskCommand(
                        taskId,
                        new CancelWorkflowTaskRequest(
                            SweepWorkflowOrphansCommand.SystemActor,
                            WorkflowReasonCodes.WorkflowOrphanClosed,
                            $"workflow-orphan-recovery:{taskId:N}",
                            reason),
                        request.CorrelationId,
                        // An orphan is often escalated (the SLA sweep kept escalating it); nobody owns it to withdraw it.
                        AllowEscalated: true), ct);
                    if (!cancelled.IsSuccessful)
                    {
                        outcome = cancelled.ReasonCode ?? "FAILED";
                        break;
                    }
                }

                if (outcome == "CLOSED")
                {
                    closed++;
                }
            }

            items.Add(new WorkflowOrphanItem(
                instance.Id,
                instance.ObjectType,
                instance.ObjectId,
                instance.Status.ToString(),
                (int)Math.Max(0, (now - (instance.StartedAt ?? instance.CreatedAt)).TotalMinutes),
                reason,
                openTasks,
                outcome));
        }

        return Response<WorkflowOrphanSweepResult>.Success(
            new WorkflowOrphanSweepResult(!request.Apply, items.Count, closed, items),
            correlationId: request.CorrelationId);
    }

    private async Task<string?> OrphanReasonAsync(
        WorkflowInstance instance,
        IReadOnlyDictionary<Guid, Domain.Entities.Tasks.TaskItem> liveTasks,
        CancellationToken ct)
    {
        if (!Guid.TryParse(instance.ObjectId, out var taskId) || !liveTasks.TryGetValue(taskId, out var task))
        {
            return SweepWorkflowOrphansCommand.TaskDeleted;
        }

        if (instance.ObjectType == TaskReviewService.ReviewObjectType && task.ReviewWorkflowInstanceId != instance.Id)
        {
            // Not linked — but the LATEST round of a task is not an orphan: its link may simply not be stored yet, and the
            // review service adopts it (TaskReviewService.StartReviewAsync). Only an older round is.
            var latest = await _instances.GetLatestByObjectRefAsync(
                instance.ObjectRef, instance.ObjectType, instance.ObjectId, ct);
            return latest is not null && latest.Id != instance.Id ? SweepWorkflowOrphansCommand.ReviewRoundReplaced : null;
        }

        return null;
    }
}
