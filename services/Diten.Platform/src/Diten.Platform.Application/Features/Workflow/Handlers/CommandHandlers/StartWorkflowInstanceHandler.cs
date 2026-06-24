using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

public sealed class StartWorkflowInstanceHandler
    : IRequestHandler<StartWorkflowInstanceCommand, Response<StartWorkflowInstanceResponse>>
{
    private const string InitialStage = "stage-1";
    private const string InitialStep = "step-1";
    private const string ResolverSource = "runtime_candidates";

    private readonly IWorkflowTemplateRepository _templateRepository;
    private readonly IWorkflowTemplateVersionRepository _versionRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository;
    private readonly IApprovalTaskRepository _taskRepository;
    private readonly IRuntimeAssignmentSnapshotRepository _assignmentSnapshotRepository;
    private readonly IWorkflowTransitionLogRepository _transitionLogRepository;
    private readonly IPositionAssignmentRepository? _positionAssignmentRepository;
    private readonly ISlaEscalationRuleRepository? _slaRules;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUserContext;

    public StartWorkflowInstanceHandler(
        IWorkflowTemplateRepository templateRepository,
        IWorkflowTemplateVersionRepository versionRepository,
        IWorkflowInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IRuntimeAssignmentSnapshotRepository assignmentSnapshotRepository,
        IWorkflowTransitionLogRepository transitionLogRepository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext,
        IPositionAssignmentRepository? positionAssignmentRepository = null,
        ISlaEscalationRuleRepository? slaRules = null)
    {
        _templateRepository = templateRepository;
        _versionRepository = versionRepository;
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _assignmentSnapshotRepository = assignmentSnapshotRepository;
        _transitionLogRepository = transitionLogRepository;
        _positionAssignmentRepository = positionAssignmentRepository;
        _slaRules = slaRules;
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
    }

    public async Task<Response<StartWorkflowInstanceResponse>> Handle(
        StartWorkflowInstanceCommand request,
        CancellationToken ct)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(request.Request.IdempotencyKey)
            ? null
            : request.Request.IdempotencyKey.Trim();
        if (idempotencyKey is not null)
        {
            var existing = await _instanceRepository.GetByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing is not null)
            {
                return await ExistingStartResponseAsync(existing, request.CorrelationId, ct);
            }
        }

        var template = await ResolveTemplateAsync(request.Request, ct);
        if (template is null)
        {
            return Response<StartWorkflowInstanceResponse>.Fail(
                "Workflow definition not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                request.CorrelationId);
        }

        if (template.ActivePublishedVersionId is null || template.ActivePublishedVersionId == Guid.Empty)
        {
            return Response<StartWorkflowInstanceResponse>.Fail(
                "Workflow definition has no active published version.",
                409,
                WorkflowReasonCodes.WorkflowTemplateNoActiveVersion,
                request.CorrelationId);
        }

        var activeVersion = await _versionRepository.GetByIdForTemplateAsync(
            template.Id,
            template.ActivePublishedVersionId.Value,
            ct);
        if (activeVersion is null)
        {
            return Response<StartWorkflowInstanceResponse>.Fail(
                "Active workflow definition version not found.",
                409,
                WorkflowReasonCodes.WorkflowTemplateVersionNotFound,
                request.CorrelationId);
        }

        if (activeVersion.Status != WorkflowTemplateVersionStatus.Published || !activeVersion.IsImmutable)
        {
            return Response<StartWorkflowInstanceResponse>.Fail(
                "Workflow definition active version is not published.",
                409,
                WorkflowReasonCodes.WorkflowTemplateNotPublished,
                request.CorrelationId);
        }

        var runtimeSteps = WorkflowDefinitionRuntimePlan.FromVersion(activeVersion);
        var firstStep = runtimeSteps.Count > 0
            ? runtimeSteps[0]
            : WorkflowDefinitionRuntimePlan.FallbackStep(
                WorkflowCandidateResolver.Normalize(request.Request.CandidatePrincipalIds),
                request.Request.CommentRequired,
                request.Request.EvidenceRequired);
        var candidateSource = firstStep.CandidatePrincipalIds.Count > 0
            ? firstStep.CandidatePrincipalIds
            : WorkflowCandidateResolver.Normalize(request.Request.CandidatePrincipalIds);
        var normalizedCandidates = await WorkflowCandidateResolver.ResolveAsync(
            candidateSource,
            _positionAssignmentRepository,
            ct);
        if (normalizedCandidates.Count == 0)
        {
            return Response<StartWorkflowInstanceResponse>.Fail(
                "At least one assignment candidate is required.",
                400,
                WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired,
                request.CorrelationId);
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _currentUserContext.ActorName;
        var objectType = request.Request.ObjectType.Trim();
        var objectId = request.Request.ObjectId.Trim();
        var objectRef = string.IsNullOrWhiteSpace(request.Request.ObjectRef)
            ? $"{objectType}|{objectId}"
            : request.Request.ObjectRef.Trim();
        var resolvedPrincipal = normalizedCandidates[0];
        var dueAt = request.Request.DueAt ??
            ResolveDueAtFromStep(firstStep, now) ??
            await ResolveDueAtAsync(template.Id, firstStep.StageCode, firstStep.StepCode, now, ct);

        var instance = new WorkflowInstance
        {
            TenantId = _tenantContext.TenantId,
            TemplateId = template.Id,
            WorkflowTemplateId = template.Id,
            TemplateVersionId = activeVersion.Id,
            ObjectType = objectType,
            ObjectId = objectId,
            ObjectRef = objectRef,
            CurrentStage = firstStep.StageCode,
            CurrentStep = firstStep.StepCode,
            Status = WorkflowInstanceStatus.Active,
            CorrelationId = request.CorrelationId,
            IdempotencyKey = idempotencyKey,
            StartedBy = actor,
            StartedAt = now,
            DueAt = dueAt,
            LastTransitionAt = now
        };

        var task = new ApprovalTask
        {
            TenantId = _tenantContext.TenantId,
            WorkflowInstanceId = instance.Id,
            StageCode = firstStep.StageCode,
            StepCode = firstStep.StepCode,
            Status = ApprovalTaskStatus.WaitingApproval,
            AssigneeRef = resolvedPrincipal,
            ReasonCode = request.Request.ReasonCode,
            IdempotencyKey = idempotencyKey,
            CommentRequired = firstStep.CommentRequired || request.Request.CommentRequired,
            EvidenceRequired = firstStep.EvidenceRequired || request.Request.EvidenceRequired,
            DueAt = dueAt
        };

        var snapshot = new RuntimeAssignmentSnapshot
        {
            TenantId = _tenantContext.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = task.Id,
            ResolverSource = ResolverSource,
            ResolvedPrincipalId = resolvedPrincipal,
            CandidatePrincipalIds = normalizedCandidates.ToList(),
            ResolvedAt = now.UtcDateTime,
            TieBreakExplanation = normalizedCandidates.Count == 1
                ? "single_candidate"
                : "lexicographic_first_principal"
        };
        task.AssignmentSnapshotId = snapshot.Id;

        var log = new WorkflowTransitionLog
        {
            TenantId = _tenantContext.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = task.Id,
            Action = WorkflowTransitionAction.Start,
            FromState = null,
            ToState = WorkflowInstanceStatus.Active.ToString(),
            FromStatus = null,
            ToStatus = WorkflowInstanceStatus.Active.ToString(),
            ActorId = _currentUserContext.UserId == Guid.Empty ? actor : _currentUserContext.UserId.ToString(),
            ActorRef = actor,
            ReasonCode = request.Request.ReasonCode,
            IdempotencyKey = idempotencyKey,
            CorrelationId = request.CorrelationId,
            SequenceNo = 1
        };

        var createdInstance = await _instanceRepository.CreateAsync(instance, ct);
        var createdTask = await _taskRepository.CreateAsync(task, ct);
        var createdSnapshot = await _assignmentSnapshotRepository.CreateAsync(snapshot, ct);
        await _transitionLogRepository.CreateAsync(log, ct);

        return Response<StartWorkflowInstanceResponse>.Success(
            ToStartResponse(createdInstance, createdTask, createdSnapshot.Id, request.CorrelationId),
            correlationId: request.CorrelationId);
    }

    private async Task<WorkflowTemplate?> ResolveTemplateAsync(StartWorkflowInstanceRequest request, CancellationToken ct)
    {
        if (request.TemplateId.HasValue && request.TemplateId.Value != Guid.Empty)
        {
            return await _templateRepository.GetByIdAsync(request.TemplateId.Value, ct);
        }

        return string.IsNullOrWhiteSpace(request.TemplateCode)
            ? null
            : await _templateRepository.GetByTemplateCodeAsync(request.TemplateCode.Trim(), ct);
    }

    private async Task<DateTimeOffset?> ResolveDueAtAsync(
        Guid templateId,
        string stageCode,
        string stepCode,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (_slaRules is null)
        {
            return null;
        }

        var rule = await _slaRules.FindForStepAsync(templateId, stageCode, stepCode, ct);
        return rule is null ? null : now.AddMinutes(rule.DueInMinutes);
    }

    private static DateTimeOffset? ResolveDueAtFromStep(WorkflowRuntimeStep step, DateTimeOffset now) =>
        step.DueInMinutes.HasValue ? now.AddMinutes(step.DueInMinutes.Value) : null;

    private async Task<Response<StartWorkflowInstanceResponse>> ExistingStartResponseAsync(
        WorkflowInstance existing,
        string correlationId,
        CancellationToken ct)
    {
        var task = await _taskRepository.GetFirstByInstanceIdAsync(existing.Id, ct);
        if (task is null || task.AssignmentSnapshotId is null)
        {
            return Response<StartWorkflowInstanceResponse>.Fail(
                "Existing workflow start is incomplete.",
                409,
                WorkflowReasonCodes.WorkflowInstanceStartConflict,
                correlationId);
        }

        return Response<StartWorkflowInstanceResponse>.Success(
            ToStartResponse(existing, task, task.AssignmentSnapshotId.Value, correlationId),
            correlationId: correlationId);
    }

    private static StartWorkflowInstanceResponse ToStartResponse(
        WorkflowInstance instance,
        ApprovalTask task,
        Guid assignmentSnapshotId,
        string correlationId) => new(
        instance.Id,
        instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId,
        instance.TemplateVersionId!.Value,
        task.Id,
        assignmentSnapshotId,
        instance.ObjectRef,
        instance.Status.ToString(),
        instance.CurrentStage,
        instance.CurrentStep,
        instance.StartedAt,
        instance.DueAt,
        correlationId);

}
