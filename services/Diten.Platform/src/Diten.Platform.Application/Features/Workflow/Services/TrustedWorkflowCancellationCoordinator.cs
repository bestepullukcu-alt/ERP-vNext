using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Workflow.Services;

public sealed class TrustedWorkflowCancellationCoordinator : ITrustedWorkflowCancellationCoordinator
{
    private const string FingerprintPrefix = "trusted-cancel:";
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _tasks;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly IPlatformTransactionExecutor _transactions;

    public TrustedWorkflowCancellationCoordinator(
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks,
        IWorkflowTransitionLogRepository logs,
        IPlatformTransactionExecutor transactions)
    {
        _instances = instances;
        _tasks = tasks;
        _logs = logs;
        _transactions = transactions;
    }

    public async Task<Response<TrustedWorkflowCancellationPreflight>> PreflightAsync(
        Guid serviceClientId,
        Guid workflowInstanceId,
        Guid approvalTaskId,
        string expectedObjectType,
        string expectedObjectId,
        Guid expectedMakerSubjectId,
        string correlationId,
        CancellationToken ct)
    {
        var instance = await _instances.GetByIdAsync(workflowInstanceId, ct);
        var task = await _tasks.GetByIdAsync(approvalTaskId, ct);
        if (!IsExactGraph(
                instance,
                task,
                serviceClientId,
                expectedMakerSubjectId,
                expectedObjectType,
                expectedObjectId)
            || instance!.Status != WorkflowInstanceStatus.Active
            || task!.Status is not (ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence)
            || instance.Version <= 0
            || task.Version <= 0)
        {
            return NotFound<TrustedWorkflowCancellationPreflight>(correlationId);
        }

        return Response<TrustedWorkflowCancellationPreflight>.Success(
            new TrustedWorkflowCancellationPreflight(
                instance.Id,
                task.Id,
                instance.ObjectType,
                instance.ObjectId,
                instance.ObjectRef,
                instance.Version,
                task.Version,
                instance.Status.ToString(),
                task.Status.ToString()),
            correlationId: correlationId);
    }

    public async Task<Response<TrustedWorkflowCancellationEvidence>> CancelAsync(
        Guid serviceClientId,
        Guid delegatedRequesterUserId,
        TrustedWorkflowCancellationRequest request,
        string correlationId,
        CancellationToken ct)
    {
        var instance = await _instances.GetByIdAsync(request.WorkflowInstanceId, ct);
        var task = await _tasks.GetByIdAsync(request.ApprovalTaskId, ct);
        if (!IsExactGraph(
                instance,
                task,
                serviceClientId,
                request.ExpectedMakerSubjectId,
                request.ExpectedObjectType,
                request.ExpectedObjectId))
        {
            return NotFound<TrustedWorkflowCancellationEvidence>(correlationId);
        }

        if (delegatedRequesterUserId != instance!.DelegatedMakerUserId)
        {
            return Response<TrustedWorkflowCancellationEvidence>.Fail(
                "Only the canonical delegated requester can cancel this workflow.",
                403,
                "WORKFLOW_TRUSTED_CANCEL_REQUESTER_FORBIDDEN",
                correlationId);
        }

        var fingerprint = Fingerprint(serviceClientId, delegatedRequesterUserId, request);
        var replay = await _logs.FindByIdempotencyAsync(
            request.ApprovalTaskId,
            WorkflowTransitionAction.Cancel,
            request.IdempotencyKey,
            ct);
        if (replay is not null)
        {
            return await ResolveReplayAsync(instance, task!, replay, fingerprint, correlationId, ct);
        }

        if (instance.Status != WorkflowInstanceStatus.Active
            || task!.Status is not (ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence)
            || instance.Version != request.ExpectedWorkflowInstanceVersion
            || task.Version != request.ExpectedApprovalTaskVersion)
        {
            return Conflict<TrustedWorkflowCancellationEvidence>(correlationId);
        }

        try
        {
            await _transactions.ExecuteAsync(async (session, token) =>
            {
                var currentInstance = await _instances.GetByIdAsync(session, request.WorkflowInstanceId, token);
                var currentTask = await _tasks.GetByIdAsync(session, request.ApprovalTaskId, token);
                if (!IsExactGraph(
                        currentInstance,
                        currentTask,
                        serviceClientId,
                        request.ExpectedMakerSubjectId,
                        request.ExpectedObjectType,
                        request.ExpectedObjectId)
                    || currentInstance!.Status != WorkflowInstanceStatus.Active
                    || currentTask!.Status is not (ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence)
                    || currentInstance.Version != request.ExpectedWorkflowInstanceVersion
                    || currentTask.Version != request.ExpectedApprovalTaskVersion)
                {
                    throw new TrustedCancellationConflictException();
                }

                var decisionAt = DateTimeOffset.UtcNow;
                var sequence = await _logs.GetLatestSequenceNoAsync(session, currentInstance.Id, token) + 1;
                if (!await _tasks.CancelTrustedAsync(
                        session,
                        currentTask.Id,
                        request.ExpectedApprovalTaskVersion,
                        delegatedRequesterUserId,
                        request.ReasonCode,
                        decisionAt,
                        token)
                    || !await _instances.CancelTrustedAsync(
                        session,
                        currentInstance.Id,
                        request.ExpectedWorkflowInstanceVersion,
                        decisionAt,
                        token))
                {
                    throw new TrustedCancellationConflictException();
                }

                await _logs.AppendAsync(session, new WorkflowTransitionLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = currentInstance.TenantId,
                    WorkflowInstanceId = currentInstance.Id,
                    ApprovalTaskId = currentTask.Id,
                    Action = WorkflowTransitionAction.Cancel,
                    FromState = currentInstance.Status.ToString(),
                    ToState = WorkflowInstanceStatus.Cancelled.ToString(),
                    FromStatus = currentTask.Status.ToString(),
                    ToStatus = ApprovalTaskStatus.Cancelled.ToString(),
                    ActorId = delegatedRequesterUserId.ToString("D"),
                    ActorRef = delegatedRequesterUserId.ToString("D"),
                    ReasonCode = request.ReasonCode,
                    IdempotencyKey = request.IdempotencyKey,
                    Comment = request.Comment,
                    EvidenceRef = fingerprint,
                    CorrelationId = correlationId,
                    SequenceNo = sequence,
                    CreatedAt = decisionAt
                }, token);

                return true;
            }, ct);
        }
        catch (TrustedCancellationConflictException)
        {
            return await ResolveCommittedReplayAfterRaceAsync(request, fingerprint, correlationId, ct)
                ?? Conflict<TrustedWorkflowCancellationEvidence>(correlationId);
        }
        catch (PlatformTransactionUnavailableException)
        {
            var committedReplay = await ResolveCommittedReplayAfterRaceAsync(
                request,
                fingerprint,
                correlationId,
                ct);
            if (committedReplay is not null)
            {
                return committedReplay;
            }

            return Response<TrustedWorkflowCancellationEvidence>.Fail(
                "Trusted workflow cancellation is temporarily unavailable.",
                503,
                "WORKFLOW_TRUSTED_CANCEL_UNAVAILABLE",
                correlationId);
        }

        instance = await _instances.GetByIdAsync(request.WorkflowInstanceId, ct);
        task = await _tasks.GetByIdAsync(request.ApprovalTaskId, ct);
        var committedLog = await _logs.FindByIdempotencyAsync(
            request.ApprovalTaskId,
            WorkflowTransitionAction.Cancel,
            request.IdempotencyKey,
            ct);
        return committedLog is null
            ? Response<TrustedWorkflowCancellationEvidence>.Fail(
                "Cancellation evidence could not be verified.",
                409,
                "WORKFLOW_TRUSTED_CANCEL_INCOHERENT",
                correlationId)
            : BuildEvidence(instance, task, committedLog, fingerprint, false, correlationId);
    }

    private async Task<Response<TrustedWorkflowCancellationEvidence>?> ResolveCommittedReplayAfterRaceAsync(
        TrustedWorkflowCancellationRequest request,
        string fingerprint,
        string correlationId,
        CancellationToken ct)
    {
        var committedLog = await _logs.FindByIdempotencyAsync(
            request.ApprovalTaskId,
            WorkflowTransitionAction.Cancel,
            request.IdempotencyKey,
            ct);
        if (committedLog is null)
        {
            return null;
        }

        var committedInstance = await _instances.GetByIdAsync(request.WorkflowInstanceId, ct);
        var committedTask = await _tasks.GetByIdAsync(request.ApprovalTaskId, ct);
        return BuildEvidence(
            committedInstance,
            committedTask,
            committedLog,
            fingerprint,
            true,
            correlationId);
    }

    private async Task<Response<TrustedWorkflowCancellationEvidence>> ResolveReplayAsync(
        WorkflowInstance instance,
        ApprovalTask task,
        WorkflowTransitionLog log,
        string fingerprint,
        string correlationId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.CompletedTask;
        return BuildEvidence(instance, task, log, fingerprint, true, correlationId);
    }

    private static Response<TrustedWorkflowCancellationEvidence> BuildEvidence(
        WorkflowInstance? instance,
        ApprovalTask? task,
        WorkflowTransitionLog log,
        string fingerprint,
        bool isReplay,
        string correlationId)
    {
        if (instance is null
            || task is null
            || instance.TemplateVersionId is null
            || task.WorkflowInstanceId != instance.Id
            || instance.Status != WorkflowInstanceStatus.Cancelled
            || task.Status != ApprovalTaskStatus.Cancelled
            || log.WorkflowInstanceId != instance.Id
            || log.ApprovalTaskId != task.Id
            || log.Action != WorkflowTransitionAction.Cancel
            || !string.Equals(log.EvidenceRef, fingerprint, StringComparison.Ordinal)
            || !Guid.TryParse(log.ActorId, out var actorUserId)
            || string.IsNullOrWhiteSpace(log.ReasonCode)
            || log.SequenceNo <= 0)
        {
            return Conflict<TrustedWorkflowCancellationEvidence>(correlationId);
        }

        return Response<TrustedWorkflowCancellationEvidence>.Success(new TrustedWorkflowCancellationEvidence(
            instance.Id,
            task.Id,
            instance.TemplateId,
            instance.TemplateVersionId.Value,
            instance.ObjectType,
            instance.ObjectId,
            instance.ObjectRef,
            WorkflowTransitionAction.Cancel.ToString(),
            actorUserId,
            log.ReasonCode,
            log.Comment,
            log.CreatedAt,
            log.SequenceNo,
            log.Id,
            task.Status.ToString(),
            instance.Status.ToString(),
            instance.Version,
            task.Version,
            isReplay,
            log.CorrelationId), correlationId: correlationId);
    }

    private static bool IsExactGraph(
        WorkflowInstance? instance,
        ApprovalTask? task,
        Guid serviceClientId,
        Guid makerSubjectId,
        string objectType,
        string objectId) =>
        instance is not null
        && task is not null
        && instance.TrustedConsumerClientId == serviceClientId
        && instance.DelegatedMakerUserId == makerSubjectId
        && string.Equals(instance.ObjectType, objectType, StringComparison.Ordinal)
        && string.Equals(instance.ObjectId, objectId, StringComparison.Ordinal)
        && task.WorkflowInstanceId == instance.Id;

    private static string Fingerprint(
        Guid serviceClientId,
        Guid delegatedRequesterUserId,
        TrustedWorkflowCancellationRequest request)
    {
        var canonical = string.Join('\n',
            serviceClientId.ToString("D"),
            delegatedRequesterUserId.ToString("D"),
            request.WorkflowInstanceId.ToString("D"),
            request.ApprovalTaskId.ToString("D"),
            request.ExpectedObjectType,
            request.ExpectedObjectId,
            request.ExpectedMakerSubjectId.ToString("D"),
            request.ExpectedWorkflowInstanceVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.ExpectedApprovalTaskVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.ReasonCode,
            request.Comment ?? string.Empty,
            request.IdempotencyKey);
        return FingerprintPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static Response<T> NotFound<T>(string correlationId) =>
        Response<T>.Fail("Trusted workflow cancellation target was not found.", 404,
            "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND", correlationId);

    private static Response<T> Conflict<T>(string correlationId) =>
        Response<T>.Fail("Trusted workflow cancellation conflicts with current state.", 409,
            "WORKFLOW_TRUSTED_CANCEL_CONFLICT", correlationId);

    private sealed class TrustedCancellationConflictException : Exception;
}
