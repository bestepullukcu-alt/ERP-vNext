using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Workflow.Services;

public sealed class WorkflowInstanceStartCoordinator : IWorkflowInstanceStartCoordinator
{
    private const string ResolverSource = "trusted_runtime_candidates";

    private readonly IWorkflowTemplateRepository _templates;
    private readonly IWorkflowTemplateVersionRepository _versions;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _tasks;
    private readonly IRuntimeAssignmentSnapshotRepository _snapshots;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly ITenantContext _tenantContext;
    private readonly IPositionAssignmentRepository? _positionAssignments;
    private readonly ISlaEscalationRuleRepository? _slaRules;

    public WorkflowInstanceStartCoordinator(
        IWorkflowTemplateRepository templates,
        IWorkflowTemplateVersionRepository versions,
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks,
        IRuntimeAssignmentSnapshotRepository snapshots,
        IWorkflowTransitionLogRepository logs,
        ITenantContext tenantContext,
        IPositionAssignmentRepository? positionAssignments = null,
        ISlaEscalationRuleRepository? slaRules = null)
    {
        _templates = templates;
        _versions = versions;
        _instances = instances;
        _tasks = tasks;
        _snapshots = snapshots;
        _logs = logs;
        _tenantContext = tenantContext;
        _positionAssignments = positionAssignments;
        _slaRules = slaRules;
    }

    public async Task<Response<TrustedWorkflowStartResult>> StartAsync(
        TrustedWorkflowStartRequest request,
        Guid serviceClientId,
        Guid delegatedMakerUserId,
        string correlationId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenantContext.TenantId == Guid.Empty ||
            serviceClientId == Guid.Empty ||
            delegatedMakerUserId == Guid.Empty)
        {
            return Fail("Trusted workflow identity is incomplete.", WorkflowReasonCodes.ValidationFailed, correlationId);
        }

        var idempotencyKey = request.IdempotencyKey?.Trim();
        var objectType = request.ObjectType?.Trim();
        var objectId = request.ObjectId?.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            string.IsNullOrWhiteSpace(objectType) ||
            string.IsNullOrWhiteSpace(objectId))
        {
            return Fail("Trusted workflow start facts are incomplete.", WorkflowReasonCodes.ValidationFailed, correlationId);
        }

        var template = await ResolveTemplateAsync(request, ct);
        if (template is null)
        {
            return Response<TrustedWorkflowStartResult>.Fail(
                "Workflow definition not found.",
                404,
                WorkflowReasonCodes.NotFoundNonLeakage,
                correlationId);
        }

        if (template.ActivePublishedVersionId is null || template.ActivePublishedVersionId == Guid.Empty)
        {
            return Conflict("Workflow definition has no active published version.", WorkflowReasonCodes.WorkflowTemplateNoActiveVersion, correlationId);
        }

        var version = await _versions.GetByIdForTemplateAsync(template.Id, template.ActivePublishedVersionId.Value, ct);
        if (version is null)
        {
            return Conflict("Active workflow definition version not found.", WorkflowReasonCodes.WorkflowTemplateVersionNotFound, correlationId);
        }

        if (version.Status != WorkflowTemplateVersionStatus.Published || !version.IsImmutable)
        {
            return Conflict("Workflow definition active version is not published.", WorkflowReasonCodes.WorkflowTemplateNotPublished, correlationId);
        }

        var runtimeSteps = WorkflowDefinitionRuntimePlan.FromVersion(version);
        var firstStep = runtimeSteps.Count > 0
            ? runtimeSteps[0]
            : WorkflowDefinitionRuntimePlan.FallbackStep(
                WorkflowCandidateResolver.Normalize(request.CandidatePrincipalIds),
                request.CommentRequired,
                request.EvidenceRequired);
        var candidateSource = firstStep.CandidatePrincipalIds.Count > 0
            ? firstStep.CandidatePrincipalIds
            : WorkflowCandidateResolver.Normalize(request.CandidatePrincipalIds);
        var candidates = await WorkflowCandidateResolver.ResolveAsync(candidateSource, _positionAssignments, ct);
        if (candidates.Count == 0)
        {
            return Fail("At least one assignment candidate is required.", WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, correlationId);
        }

        var existing = await _instances.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        var now = DateTimeOffset.UtcNow;
        var dueAt = request.DueAt?.ToUniversalTime() ?? existing?.DueAt ??
            ResolveDueAtFromStep(firstStep, now) ??
            await ResolveDueAtAsync(template.Id, firstStep.StageCode, firstStep.StepCode, now, ct);
        var objectRef = string.IsNullOrWhiteSpace(request.ObjectRef)
            ? $"{objectType}|{objectId}"
            : request.ObjectRef.Trim();
        var reasonCode = string.IsNullOrWhiteSpace(request.ReasonCode) ? null : request.ReasonCode.Trim();
        var fingerprint = ComputeFingerprint(
            _tenantContext.TenantId,
            serviceClientId,
            delegatedMakerUserId,
            template.Id,
            version.Id,
            objectType,
            objectId,
            objectRef,
            candidates,
            reasonCode,
            firstStep.CommentRequired || request.CommentRequired,
            firstStep.EvidenceRequired || request.EvidenceRequired,
            dueAt);

        var reservation = new WorkflowInstance
        {
            TenantId = _tenantContext.TenantId,
            TemplateId = template.Id,
            WorkflowTemplateId = template.Id,
            TemplateVersionId = version.Id,
            ObjectType = objectType,
            ObjectId = objectId,
            ObjectRef = objectRef,
            CurrentStage = firstStep.StageCode,
            CurrentStep = firstStep.StepCode,
            Status = WorkflowInstanceStatus.Pending,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            TrustedConsumerClientId = serviceClientId,
            DelegatedMakerUserId = delegatedMakerUserId,
            StartRequestFingerprint = fingerprint,
            StartCheckpoint = WorkflowStartCheckpoint.Reserved,
            InitialApprovalTaskId = Guid.NewGuid(),
            InitialAssignmentSnapshotId = Guid.NewGuid(),
            StartTransitionLogId = Guid.NewGuid(),
            StartedBy = delegatedMakerUserId.ToString(),
            StartedAt = now,
            DueAt = dueAt,
            LastTransitionAt = now
        };

        var reserved = existing is null
            ? await _instances.ReserveTrustedStartAsync(reservation, ct)
            : (Instance: existing, Created: false);
        var instance = reserved.Instance;
        if (!reserved.Created && request.DueAt is null && instance.DueAt != dueAt)
        {
            // Concurrent callers can resolve a relative SLA a few ticks apart before one wins the unique
            // reservation. The persisted winner's due date is the canonical resolved fact for exact replay.
            dueAt = instance.DueAt;
            fingerprint = ComputeFingerprint(
                _tenantContext.TenantId,
                serviceClientId,
                delegatedMakerUserId,
                template.Id,
                version.Id,
                objectType,
                objectId,
                objectRef,
                candidates,
                reasonCode,
                firstStep.CommentRequired || request.CommentRequired,
                firstStep.EvidenceRequired || request.EvidenceRequired,
                dueAt);
        }

        if (!MatchesReservation(
                instance,
                serviceClientId,
                delegatedMakerUserId,
                template.Id,
                version.Id,
                fingerprint,
                objectType,
                objectId,
                objectRef))
        {
            return Conflict(
                "The idempotency key is already bound to different workflow start facts.",
                WorkflowReasonCodes.WorkflowStartIdempotencyConflict,
                correlationId);
        }

        if (instance.InitialApprovalTaskId is null ||
            instance.InitialAssignmentSnapshotId is null ||
            instance.StartTransitionLogId is null)
        {
            return Conflict("Trusted workflow reservation is incomplete.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
        }

        var task = ExpectedTask(
            instance,
            firstStep,
            candidates[0],
            reasonCode,
            idempotencyKey,
            dueAt,
            firstStep.CommentRequired || request.CommentRequired,
            firstStep.EvidenceRequired || request.EvidenceRequired);
        var snapshot = ExpectedSnapshot(instance, task.Id, candidates);
        var log = ExpectedStartLog(instance, task.Id, delegatedMakerUserId, reasonCode, idempotencyKey, correlationId);

        if (instance.StartCheckpoint < WorkflowStartCheckpoint.TaskPersisted)
        {
            var persisted = await _tasks.EnsureTrustedStartTaskAsync(task, ct);
            if (!MatchesTask(persisted, task))
            {
                return Conflict("Trusted workflow task recovery facts conflict.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            var advanced = await AdvanceAsync(instance, WorkflowStartCheckpoint.Reserved, WorkflowStartCheckpoint.TaskPersisted, ct);
            if (!advanced.Success)
            {
                return Conflict("Trusted workflow task checkpoint could not be fenced.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            instance = advanced.Instance!;
        }
        else
        {
            var persisted = await _tasks.GetByIdAsync(task.Id, ct);
            if (persisted is null || !MatchesTask(persisted, task))
            {
                return Conflict("Trusted workflow task proof is missing or inconsistent.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }
        }

        if (instance.StartCheckpoint < WorkflowStartCheckpoint.AssignmentSnapshotPersisted)
        {
            var persisted = await _snapshots.EnsureTrustedStartSnapshotAsync(snapshot, ct);
            if (!MatchesSnapshot(persisted, snapshot))
            {
                return Conflict("Trusted workflow assignment proof conflicts.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            var advanced = await AdvanceAsync(instance, WorkflowStartCheckpoint.TaskPersisted, WorkflowStartCheckpoint.AssignmentSnapshotPersisted, ct);
            if (!advanced.Success)
            {
                return Conflict("Trusted workflow assignment checkpoint could not be fenced.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            instance = advanced.Instance!;
        }
        else
        {
            var persisted = await _snapshots.GetByIdAsync(snapshot.Id, ct);
            if (persisted is null || !MatchesSnapshot(persisted, snapshot))
            {
                return Conflict("Trusted workflow assignment proof is missing or inconsistent.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }
        }

        if (instance.StartCheckpoint < WorkflowStartCheckpoint.StartLogPersisted)
        {
            var persisted = await _logs.EnsureTrustedStartLogAsync(log, ct);
            if (!MatchesStartLog(persisted, log))
            {
                return Conflict("Trusted workflow start-log proof conflicts.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            var advanced = await AdvanceAsync(instance, WorkflowStartCheckpoint.AssignmentSnapshotPersisted, WorkflowStartCheckpoint.StartLogPersisted, ct);
            if (!advanced.Success)
            {
                return Conflict("Trusted workflow start-log checkpoint could not be fenced.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            instance = advanced.Instance!;
        }
        else
        {
            var persisted = await _logs.GetByIdAsync(log.Id, ct);
            if (persisted is null || !MatchesStartLog(persisted, log))
            {
                return Conflict("Trusted workflow start-log proof is missing or inconsistent.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }
        }

        if (instance.StartCheckpoint < WorkflowStartCheckpoint.Completed)
        {
            var advanced = await AdvanceAsync(instance, WorkflowStartCheckpoint.StartLogPersisted, WorkflowStartCheckpoint.Completed, ct);
            if (!advanced.Success)
            {
                return Conflict("Trusted workflow completion checkpoint could not be fenced.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            instance = advanced.Instance!;
        }

        var completedTask = await _tasks.GetByIdAsync(task.Id, ct);
        var completedSnapshot = await _snapshots.GetByIdAsync(snapshot.Id, ct);
        var completedLog = await _logs.GetByIdAsync(log.Id, ct);
        if (instance.StartCheckpoint != WorkflowStartCheckpoint.Completed ||
            instance.Status != WorkflowInstanceStatus.Active ||
            completedTask is null || !MatchesTask(completedTask, task) ||
            completedSnapshot is null || !MatchesSnapshot(completedSnapshot, snapshot) ||
            completedLog is null || !MatchesStartLog(completedLog, log))
        {
            return Conflict("Trusted workflow completion proof is inconsistent.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
        }

        var result = new TrustedWorkflowStartResult(
            instance.Id,
            instance.TemplateId,
            instance.TemplateVersionId!.Value,
            task.Id,
            snapshot.Id,
            log.Id,
            instance.ObjectRef,
            instance.Status.ToString(),
            instance.CurrentStage,
            instance.CurrentStep,
            instance.StartedAt,
            instance.DueAt,
            !reserved.Created,
            correlationId);
        return Response<TrustedWorkflowStartResult>.Success(result, reserved.Created ? 201 : 200, correlationId);
    }

    private async Task<WorkflowTemplate?> ResolveTemplateAsync(TrustedWorkflowStartRequest request, CancellationToken ct)
    {
        if (request.TemplateId is { } templateId && templateId != Guid.Empty)
        {
            return await _templates.GetByIdAsync(templateId, ct);
        }

        return string.IsNullOrWhiteSpace(request.TemplateCode)
            ? null
            : await _templates.GetByTemplateCodeAsync(request.TemplateCode.Trim(), ct);
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

    private async Task<(bool Success, WorkflowInstance? Instance)> AdvanceAsync(
        WorkflowInstance instance,
        WorkflowStartCheckpoint expected,
        WorkflowStartCheckpoint next,
        CancellationToken ct)
    {
        if (instance.StartCheckpoint >= next)
        {
            return (true, instance);
        }

        var updated = await _instances.AdvanceStartCheckpointAsync(instance.Id, instance.Version, expected, next, ct);
        var reread = await _instances.GetByIdAsync(instance.Id, ct);
        return (reread is not null && reread.StartCheckpoint >= next && (updated || reread.Version > instance.Version), reread);
    }

    private static ApprovalTask ExpectedTask(
        WorkflowInstance instance,
        WorkflowRuntimeStep step,
        string resolvedPrincipal,
        string? reasonCode,
        string idempotencyKey,
        DateTimeOffset? dueAt,
        bool commentRequired,
        bool evidenceRequired) =>
        new()
        {
            Id = instance.InitialApprovalTaskId!.Value,
            TenantId = instance.TenantId,
            WorkflowInstanceId = instance.Id,
            StageCode = step.StageCode,
            StepCode = step.StepCode,
            Status = ApprovalTaskStatus.WaitingApproval,
            AssignmentSnapshotId = instance.InitialAssignmentSnapshotId,
            AssigneeRef = resolvedPrincipal,
            ReasonCode = reasonCode,
            IdempotencyKey = idempotencyKey,
            CommentRequired = commentRequired,
            EvidenceRequired = evidenceRequired,
            DueAt = dueAt
        };

    private static RuntimeAssignmentSnapshot ExpectedSnapshot(
        WorkflowInstance instance,
        Guid taskId,
        IReadOnlyList<string> candidates) =>
        new()
        {
            Id = instance.InitialAssignmentSnapshotId!.Value,
            TenantId = instance.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = taskId,
            ResolverSource = ResolverSource,
            ResolvedPrincipalId = candidates[0],
            CandidatePrincipalIds = candidates.ToList(),
            ResolvedAt = ToMongoDateTimePrecision(instance.StartedAt!.Value.UtcDateTime),
            TieBreakExplanation = candidates.Count == 1 ? "single_candidate" : "lexicographic_first_principal"
        };

    private static WorkflowTransitionLog ExpectedStartLog(
        WorkflowInstance instance,
        Guid taskId,
        Guid maker,
        string? reasonCode,
        string idempotencyKey,
        string correlationId) =>
        new()
        {
            Id = instance.StartTransitionLogId!.Value,
            TenantId = instance.TenantId,
            WorkflowInstanceId = instance.Id,
            ApprovalTaskId = taskId,
            Action = WorkflowTransitionAction.Start,
            ToState = WorkflowInstanceStatus.Active.ToString(),
            ToStatus = WorkflowInstanceStatus.Active.ToString(),
            ActorId = maker.ToString(),
            ActorRef = maker.ToString(),
            ReasonCode = reasonCode,
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId,
            SequenceNo = 1
        };

    private static bool MatchesReservation(
        WorkflowInstance instance,
        Guid clientId,
        Guid makerId,
        Guid templateId,
        Guid versionId,
        string fingerprint,
        string objectType,
        string objectId,
        string objectRef) =>
        instance.TrustedConsumerClientId == clientId &&
        instance.DelegatedMakerUserId == makerId &&
        instance.TemplateId == templateId &&
        instance.TemplateVersionId == versionId &&
        string.Equals(instance.StartRequestFingerprint, fingerprint, StringComparison.Ordinal) &&
        string.Equals(instance.ObjectType, objectType, StringComparison.Ordinal) &&
        string.Equals(instance.ObjectId, objectId, StringComparison.Ordinal) &&
        string.Equals(instance.ObjectRef, objectRef, StringComparison.Ordinal);

    private static bool MatchesTask(ApprovalTask actual, ApprovalTask expected) =>
        actual.Id == expected.Id &&
        actual.TenantId == expected.TenantId &&
        actual.WorkflowInstanceId == expected.WorkflowInstanceId &&
        actual.AssignmentSnapshotId == expected.AssignmentSnapshotId &&
        actual.Status == expected.Status &&
        string.Equals(actual.StageCode, expected.StageCode, StringComparison.Ordinal) &&
        string.Equals(actual.StepCode, expected.StepCode, StringComparison.Ordinal) &&
        string.Equals(actual.AssigneeRef, expected.AssigneeRef, StringComparison.Ordinal) &&
        string.Equals(actual.ReasonCode, expected.ReasonCode, StringComparison.Ordinal) &&
        string.Equals(actual.IdempotencyKey, expected.IdempotencyKey, StringComparison.Ordinal) &&
        actual.CommentRequired == expected.CommentRequired &&
        actual.EvidenceRequired == expected.EvidenceRequired &&
        actual.DueAt == expected.DueAt;

    private static bool MatchesSnapshot(RuntimeAssignmentSnapshot actual, RuntimeAssignmentSnapshot expected) =>
        actual.Id == expected.Id &&
        actual.TenantId == expected.TenantId &&
        actual.WorkflowInstanceId == expected.WorkflowInstanceId &&
        actual.ApprovalTaskId == expected.ApprovalTaskId &&
        string.Equals(actual.ResolverSource, expected.ResolverSource, StringComparison.Ordinal) &&
        string.Equals(actual.ResolvedPrincipalId, expected.ResolvedPrincipalId, StringComparison.Ordinal) &&
        actual.CandidatePrincipalIds.SequenceEqual(expected.CandidatePrincipalIds, StringComparer.Ordinal) &&
        actual.ResolvedAt == expected.ResolvedAt &&
        string.Equals(actual.TieBreakExplanation, expected.TieBreakExplanation, StringComparison.Ordinal);

    private static bool MatchesStartLog(WorkflowTransitionLog actual, WorkflowTransitionLog expected) =>
        actual.Id == expected.Id &&
        actual.TenantId == expected.TenantId &&
        actual.WorkflowInstanceId == expected.WorkflowInstanceId &&
        actual.ApprovalTaskId == expected.ApprovalTaskId &&
        actual.Action == WorkflowTransitionAction.Start &&
        actual.SequenceNo == 1 &&
        string.Equals(actual.ActorId, expected.ActorId, StringComparison.Ordinal) &&
        string.Equals(actual.ReasonCode, expected.ReasonCode, StringComparison.Ordinal) &&
        string.Equals(actual.IdempotencyKey, expected.IdempotencyKey, StringComparison.Ordinal) &&
        string.Equals(actual.ToStatus, expected.ToStatus, StringComparison.Ordinal);

    private static string ComputeFingerprint(
        Guid tenantId,
        Guid clientId,
        Guid makerId,
        Guid templateId,
        Guid versionId,
        string objectType,
        string objectId,
        string objectRef,
        IReadOnlyList<string> candidates,
        string? reasonCode,
        bool commentRequired,
        bool evidenceRequired,
        DateTimeOffset? dueAt)
    {
        var canonical = string.Join('\n',
            tenantId.ToString("D"),
            clientId.ToString("D"),
            makerId.ToString("D"),
            templateId.ToString("D"),
            versionId.ToString("D"),
            Encode(objectType),
            Encode(objectId),
            Encode(objectRef),
            string.Join(',', candidates.Select(Encode)),
            Encode(reasonCode ?? string.Empty),
            commentRequired ? "1" : "0",
            evidenceRequired ? "1" : "0",
            dueAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static DateTime ToMongoDateTimePrecision(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return new DateTime(
            utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond),
            DateTimeKind.Utc);
    }

    private static Response<TrustedWorkflowStartResult> Fail(string message, string reason, string correlationId) =>
        Response<TrustedWorkflowStartResult>.Fail(message, 400, reason, correlationId);

    private static Response<TrustedWorkflowStartResult> Conflict(string message, string reason, string correlationId) =>
        Response<TrustedWorkflowStartResult>.Fail(message, 409, reason, correlationId);
}
