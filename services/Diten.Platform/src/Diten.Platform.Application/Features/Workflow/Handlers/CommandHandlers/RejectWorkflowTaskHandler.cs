using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class RejectWorkflowTaskHandler
    : IRequestHandler<RejectWorkflowTaskCommand, Response<WorkflowTaskTransitionResponse>>
{
    private readonly WorkflowTaskTransitionSupport _support;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IWorkflowTransitionLogRepository _logRepository;
    private readonly IWorkflowTemplateVersionRepository? _versionRepository;

    public RejectWorkflowTaskHandler(
        IApprovalTaskRepository taskRepository,
        IWorkflowInstanceRepository instanceRepository,
        IRuntimeAssignmentSnapshotRepository snapshotRepository,
        IWorkflowTransitionLogRepository logRepository,
        IWorkflowTemplateVersionRepository? versionRepository = null)
    {
        _support = new WorkflowTaskTransitionSupport(taskRepository, instanceRepository, snapshotRepository, logRepository);
        _taskRepository = taskRepository;
        _instanceRepository = instanceRepository;
        _logRepository = logRepository;
        _versionRepository = versionRepository;
    }

    public async Task<Response<WorkflowTaskTransitionResponse>> Handle(RejectWorkflowTaskCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Request.Comment) && await RequiresRejectCommentAsync(request, ct))
        {
            return Response<WorkflowTaskTransitionResponse>.Fail(
                "This approval requires a comment when rejecting.",
                400,
                WorkflowReasonCodes.WorkflowRejectCommentRequired,
                request.CorrelationId);
        }

        return await _support.TransitionAsync(
            request.TaskId,
            WorkflowTransitionAction.Reject,
            request.Request.ActorId,
            request.Request.ReasonCode,
            request.Request.IdempotencyKey,
            request.Request.Comment,
            request.Request.EvidenceRef,
            request.CorrelationId,
            ct);
    }

    /// <summary>
    /// MOD-0280-FU01 R5 — does the definition this task runs on demand a comment on reject? Only the definition's own
    /// option decides (<see cref="WorkflowDefinitionRuntimePlan.RejectCommentRequired"/>), default off, so a definition
    /// that never set it rejects exactly as before.
    ///
    /// <para>Anything that is not answerable here — an already-recorded reject being replayed, a task or instance that
    /// does not resolve — is left to the transition support, which owns those answers (idempotent replay, 404). This
    /// check only ever ADDS a refusal; it never decides a transition.</para>
    /// </summary>
    private async Task<bool> RequiresRejectCommentAsync(RejectWorkflowTaskCommand request, CancellationToken ct)
    {
        if (_versionRepository is null)
        {
            return false;
        }

        var replay = await _logRepository.GetByTaskActionIdempotencyKeyAsync(
            request.TaskId, WorkflowTransitionAction.Reject, request.Request.IdempotencyKey.Trim(), ct);
        if (replay is not null)
        {
            return false;
        }

        var task = await _taskRepository.GetByIdAsync(request.TaskId, ct);
        if (task is null)
        {
            return false;
        }

        var instance = await _instanceRepository.GetByIdAsync(task.WorkflowInstanceId, ct);
        if (instance?.TemplateVersionId is not { } versionId || versionId == Guid.Empty)
        {
            return false;
        }

        var templateId = instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId;
        var version = await _versionRepository.GetByIdForTemplateAsync(templateId, versionId, ct);
        return WorkflowDefinitionRuntimePlan.RejectCommentRequired(version);
    }
}
