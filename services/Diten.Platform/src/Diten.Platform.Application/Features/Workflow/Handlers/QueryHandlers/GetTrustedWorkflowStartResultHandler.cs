using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;

public sealed class GetTrustedWorkflowStartResultHandler
    : IRequestHandler<GetTrustedWorkflowStartResultQuery, Response<TrustedWorkflowStartResult>>
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _tasks;
    private readonly IRuntimeAssignmentSnapshotRepository _snapshots;
    private readonly IWorkflowTransitionLogRepository _logs;

    public GetTrustedWorkflowStartResultHandler(
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks,
        IRuntimeAssignmentSnapshotRepository snapshots,
        IWorkflowTransitionLogRepository logs)
    {
        _instances = instances;
        _tasks = tasks;
        _snapshots = snapshots;
        _logs = logs;
    }

    public async Task<Response<TrustedWorkflowStartResult>> Handle(
        GetTrustedWorkflowStartResultQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var instance = await _instances.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (instance is null
            || instance.TrustedConsumerClientId != request.ServiceClientId
            || instance.DelegatedMakerUserId != request.ExpectedMakerSubjectId
            || !string.Equals(instance.ObjectType, request.ExpectedObjectType, StringComparison.Ordinal)
            || !string.Equals(instance.ObjectId, request.ExpectedObjectId, StringComparison.Ordinal))
        {
            return NotFound(request.CorrelationId);
        }

        if (instance.StartCheckpoint != WorkflowStartCheckpoint.Completed)
        {
            return Response<TrustedWorkflowStartResult>.Fail(
                "Trusted workflow start is not completed.",
                409,
                WorkflowReasonCodes.WorkflowStartNotCompleted,
                request.CorrelationId);
        }

        if (instance.TemplateId == Guid.Empty
            || instance.TemplateVersionId is not { } templateVersionId || templateVersionId == Guid.Empty
            || instance.InitialApprovalTaskId is not { } taskId || taskId == Guid.Empty
            || instance.InitialAssignmentSnapshotId is not { } snapshotId || snapshotId == Guid.Empty
            || instance.StartTransitionLogId is not { } logId || logId == Guid.Empty
            || !IsLowerSha256(instance.StartRequestFingerprint)
            || !string.Equals(
                instance.StartedBy,
                request.ExpectedMakerSubjectId.ToString("D"),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(instance.ObjectRef))
        {
            return Inconsistent(request.CorrelationId);
        }

        var task = await _tasks.GetByIdAsync(taskId, cancellationToken);
        var snapshot = await _snapshots.GetByIdAsync(snapshotId, cancellationToken);
        var log = await _logs.GetByIdAsync(logId, cancellationToken);
        if (task is null || snapshot is null || log is null
            || task.WorkflowInstanceId != instance.Id
            || task.AssignmentSnapshotId != snapshot.Id
            || snapshot.WorkflowInstanceId != instance.Id
            || snapshot.ApprovalTaskId != task.Id
            || log.WorkflowInstanceId != instance.Id
            || log.ApprovalTaskId != task.Id
            || log.Action != WorkflowTransitionAction.Start
            || log.SequenceNo != 1
            || !string.Equals(log.ActorId, request.ExpectedMakerSubjectId.ToString("D"), StringComparison.Ordinal)
            || !string.Equals(log.ActorRef, log.ActorId, StringComparison.Ordinal)
            || !string.Equals(log.ToState, WorkflowInstanceStatus.Active.ToString(), StringComparison.Ordinal)
            || !string.Equals(log.ToStatus, WorkflowInstanceStatus.Active.ToString(), StringComparison.Ordinal)
            || !string.Equals(log.IdempotencyKey, request.IdempotencyKey, StringComparison.Ordinal))
        {
            return Inconsistent(request.CorrelationId);
        }

        return Response<TrustedWorkflowStartResult>.Success(new(
            instance.Id,
            instance.TemplateId,
            templateVersionId,
            task.Id,
            snapshot.Id,
            log.Id,
            instance.ObjectRef,
            instance.Status.ToString(),
            instance.CurrentStage,
            instance.CurrentStep,
            instance.StartedAt,
            instance.DueAt,
            true,
            request.CorrelationId), 200, request.CorrelationId);
    }

    private static Response<TrustedWorkflowStartResult> NotFound(string correlationId) =>
        Response<TrustedWorkflowStartResult>.Fail(
            "Workflow start result not found.", 404, WorkflowReasonCodes.NotFoundNonLeakage, correlationId);

    private static Response<TrustedWorkflowStartResult> Inconsistent(string correlationId) =>
        Response<TrustedWorkflowStartResult>.Fail(
            "Trusted workflow start proof is inconsistent.",
            409,
            WorkflowReasonCodes.WorkflowStartRecoveryConflict,
            correlationId);

    private static bool IsLowerSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
