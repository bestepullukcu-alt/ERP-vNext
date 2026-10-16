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
    private readonly IPositionRepository? _positionRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPlatformTransactionExecutor? _transactions;

    public StartWorkflowInstanceHandler(
        IWorkflowTemplateRepository templateRepository,
        IWorkflowTemplateVersionRepository versionRepository,
        IWorkflowInstanceRepository instanceRepository,
        IApprovalTaskRepository taskRepository,
        IRuntimeAssignmentSnapshotRepository assignmentSnapshotRepository,
        IWorkflowTransitionLogRepository transitionLogRepository,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext,
        // Main sync FIX1 (CT decision A): the tests-only seam is a REQUIRED enumerable — empty in production (nothing
        // registers it), one instance where a test host asks for sequential writes by name. It was an optional (= null)
        // Diten.* parameter, which main's composition-root guard (Q366) rightly refuses: an optional dependency that is
        // never registered resolves to null silently.
        IEnumerable<SequentialWorkflowStartWritesForTests> sequentialWritesForTests,
        IPositionAssignmentRepository? positionAssignmentRepository = null,
        ISlaEscalationRuleRepository? slaRules = null,
        IPositionRepository? positionRepository = null,
        IPlatformTransactionExecutor? transactions = null)
    {
        // FIX3 item 2 — the claim and the start rows commit in one transaction (FIX2 item 1). No SILENT fallback: without the
        // executor this handler is built only where a test asked for sequential writes by name; anywhere else it refuses.
        _transactions = transactions;
        if (transactions is null && !sequentialWritesForTests.Any())
        {
            throw new ArgumentNullException(
                nameof(transactions), "MOD-0023's start needs IPlatformTransactionExecutor: its claim and rows commit together.");
        }
        _templateRepository = templateRepository;
        _versionRepository = versionRepository;
        _instanceRepository = instanceRepository;
        _taskRepository = taskRepository;
        _assignmentSnapshotRepository = assignmentSnapshotRepository;
        _transitionLogRepository = transitionLogRepository;
        _positionAssignmentRepository = positionAssignmentRepository;
        _slaRules = slaRules;
        _positionRepository = positionRepository;
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
                return await ExistingStartResponseAsync(existing, request, ct);
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
            _positionRepository,
            ct);
        if (normalizedCandidates.Count == 0)
        {
            /*
             * D2-FIX4 item 7 — AN EMPTY SEAT IS TRANSIENT HERE TOO. The trusted start answers "somebody was declared but the
             * seat resolves to nobody right now" 503 (D2-FIX3 item 7); this start still answered 400 for the same condition,
             * and a caller files a 400 as an invalid request and gives up, while a 503 is the one it knows to retry.
             * Nothing is written either way, so the retry is a fresh first call. Only a request that DECLARED nobody at all
             * (no candidates of its own and none in the template's step) stays a 400: that one is a malformed request, not a
             * wait. The contract code is the same in both.
             */
            return candidateSource.Count == 0
                ? Response<StartWorkflowInstanceResponse>.Fail(
                    "At least one assignment candidate is required.",
                    400,
                    WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired,
                    request.CorrelationId)
                : Response<StartWorkflowInstanceResponse>.Fail(
                    "Nobody holds the approving seat right now; nothing was started. Try again once someone does.",
                    503,
                    WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired,
                    request.CorrelationId);
        }

        var actor = _currentUserContext.ActorName;
        Guid? starterUserId = _currentUserContext.UserId == Guid.Empty ? null : _currentUserContext.UserId;
        // D2 — the person starting the workflow is not one of its approvers. Nobody left: refused before anything is
        // written (no instance, no log).
        normalizedCandidates = WorkflowTaskTransitionSupport.WithoutStarter(normalizedCandidates, starterUserId, actor);
        if (normalizedCandidates.Count == 0)
        {
            return Response<StartWorkflowInstanceResponse>.Fail(
                "Every candidate approver is the person starting the workflow; nobody else may approve it.",
                400,
                WorkflowReasonCodes.WorkflowNoEligibleApprover,
                request.CorrelationId);
        }

        var now = DateTimeOffset.UtcNow;
        var objectType = request.Request.ObjectType.Trim();
        var objectId = request.Request.ObjectId.Trim();
        var objectRef = string.IsNullOrWhiteSpace(request.Request.ObjectRef)
            ? $"{objectType}|{objectId}"
            : request.Request.ObjectRef.Trim();
        var resolvedPrincipal = normalizedCandidates[0];
        // D2-FIX2 item 6(a) — stored in UTC, as the trusted start already does. A due date with an offset (a task's own
        // due date passes straight through) was stored as [local ticks, offset] (BL-030): the sweep's DueAt.0 order and its
        // DueAt <= now filter both read the ticks, so such a task sorted and fell due hours off. Rows stored before this
        // keep their offset (no migration); they are named in the D2-FIX2 report.
        var dueAt = request.Request.DueAt?.ToUniversalTime() ??
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
            StartedByUserId = starterUserId,
            StartedAt = now,
            DueAt = dueAt,
            LastTransitionAt = now,
            // WP-CL-BE-3 — snapshots: the template key for the completion event, the starter's display context for
            // WorkCenterNext (validated by StartWorkflowInstanceValidator; written once, never changed).
            TemplateCode = template.TemplateCode,
            DisplayContext = request.Request.DisplayContext?.ToSnapshot(),
            StartRequestFingerprint = idempotencyKey is null ? null : Fingerprint(template.Id, objectType, objectId, objectRef, request.Request)
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
            SignatureRequired = firstStep.SignatureRequired,
            SignatureMeaning = firstStep.SignatureMeaning,
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

        /*
         * BL-549 — FOUR WRITES, NOT ONE: the instance, its task, the assignment snapshot and the start log are separate
         * documents. A crash after the instance left it Active with no task: nobody could decide it, and every replay of the
         * key answered 409 for good. Now the instance carries the ids of the three rows that follow it (and, keyed, the
         * fingerprint of the facts it was started with), so the replay writes exactly THOSE rows — see
         * ExistingStartResponseAsync — and every write below tolerates the row a replay already wrote (same id).
         */
        instance.InitialApprovalTaskId = task.Id;
        instance.InitialAssignmentSnapshotId = snapshot.Id;
        instance.StartTransitionLogId = log.Id;

        WorkflowInstance createdInstance;
        if (idempotencyKey is null)
        {
            createdInstance = await _instanceRepository.CreateAsync(instance, ct);
        }
        else
        {
            // Two first calls with one key: the unique key index lets one in; the other answers as that start's replay.
            var reserved = await _instanceRepository.ReserveTrustedStartAsync(instance, ct);
            if (!reserved.Created)
            {
                return await ExistingStartResponseAsync(reserved.Instance, request, ct);
            }

            createdInstance = reserved.Instance;
        }

        /*
         * FIX1 item 2 / FIX2 item 1 — THE TASK GOES IN ONLY UNDER A CLAIM, AND IN THE SAME TRANSACTION. A first call that
         * stopped between its instance and its task (a paused pod, ≥ the abandon age) could have its instance closed by the
         * recovery and then write a task under a Cancelled instance. The claim (versioned, only while Active) and the task
         * insert commit together: a recovery that reads before the commit sees the old version and no task, and its fenced
         * close fails (or conflicts with the open transaction); one that reads after sees the task and leaves it alone. A
         * transaction that never commits (the pod stays paused past Mongo's transaction lifetime) leaves neither.
         */
        bool claimed;
        try
        {
            // FIX3 K8 — the snapshot and the start log go in with the claim and the task: the start is whole or not there.
            claimed = await ClaimWithStartRowsAsync(createdInstance.Id, createdInstance.Version, task.Id, snapshot.Id, log.Id,
                task, snapshot, log, ct);
        }
        catch (PlatformTransactionUnavailableException)
        {
            return await AfterUnavailableAsync(createdInstance.Id, request.CorrelationId, ct);
        }

        if (!claimed)
        {
            /*
             * FIX2 item 2 — A LOST CLAIM IS NOT A CLOSE. A replay of the same key (a double submit) may have claimed the
             * instance first and is completing it with the same fixed ids: this call then answers as that replay does. Only
             * an instance that really left Active (the recovery closed it) answers 409 — never "as a replay" (FIX3 V3).
             */
            var reread = await _instanceRepository.GetByIdAsync(createdInstance.Id, ct);
            if (reread is not null && reread.Status == WorkflowInstanceStatus.Active)
            {
                return await ExistingStartResponseAsync(reread, request, ct);
            }

            return StartConflict("The workflow start was closed before its task was written.", request.CorrelationId);
        }

        return Response<StartWorkflowInstanceResponse>.Success(
            ToStartResponse(createdInstance, task, snapshot.Id, request.CorrelationId),
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
        StartWorkflowInstanceCommand request,
        CancellationToken ct)
    {
        var correlationId = request.CorrelationId;
        var task = await _taskRepository.GetFirstByInstanceIdAsync(existing.Id, ct);
        var snapshot = task?.AssignmentSnapshotId is { } snapshotId
            ? await _assignmentSnapshotRepository.GetByIdAsync(snapshotId, ct)
            : null;
        var startLog = task is null ? null : await StartLogAsync(existing.Id, ct);
        // FIX1 K2 — "whole" is the task and its snapshot. A missing start log is completed only on a LIVE instance; an older
        // instance that is no longer Active and never had one answers exactly as before (200).
        if (task is not null && snapshot is not null && (startLog is not null || existing.Status != WorkflowInstanceStatus.Active))
        {
            // The start is whole: the replay answers it, whatever has become of the instance since (unchanged).
            return Response<StartWorkflowInstanceResponse>.Success(
                ToStartResponse(existing, task, snapshot.Id, correlationId),
                correlationId: correlationId);
        }

        // BL-549 — an INCOMPLETE generic start. A trusted start completes through its own coordinator (checkpoints), and a
        // closed instance (the abandoned-start sweep closed it) is never brought back: both are answered as before.
        if (existing.TrustedConsumerClientId is not null || existing.Status != WorkflowInstanceStatus.Active)
        {
            return StartConflict("Existing workflow start is incomplete.", correlationId);
        }

        return await CompleteStartAsync(existing, task, snapshot, request, ct);
    }

    /// <summary>
    /// BL-549 — THE REPLAY COMPLETES THE START. The same key with the same facts writes what the first call did not (the task,
    /// the snapshot, the start log) and answers what the first call would have answered; different facts get 409, never a
    /// start of something else under the key.
    /// <para>One task, however many replays run at once: the ids are fixed ON the instance (a start since BL-549 wrote them
    /// with it; an older one gets them from the first replay's claim, and a replay that loses that claim reads the winner's),
    /// and every row is inserted under its fixed id — the id is the unique index, so a second insert re-reads the first.</para>
    /// </summary>
    private async Task<Response<StartWorkflowInstanceResponse>> CompleteStartAsync(
        WorkflowInstance existing,
        ApprovalTask? task,
        RuntimeAssignmentSnapshot? snapshot,
        StartWorkflowInstanceCommand request,
        CancellationToken ct)
    {
        var correlationId = request.CorrelationId;
        var objectType = request.Request.ObjectType.Trim();
        var objectId = request.Request.ObjectId.Trim();
        var objectRef = string.IsNullOrWhiteSpace(request.Request.ObjectRef)
            ? $"{objectType}|{objectId}"
            : request.Request.ObjectRef.Trim();
        var template = await ResolveTemplateAsync(request.Request, ct);
        var templateId = existing.TemplateId == Guid.Empty ? existing.WorkflowTemplateId : existing.TemplateId;
        if (template is null || template.Id != templateId
            || !string.Equals(existing.ObjectType, objectType, StringComparison.Ordinal)
            || !string.Equals(existing.ObjectId, objectId, StringComparison.Ordinal)
            || !string.Equals(existing.ObjectRef, objectRef, StringComparison.Ordinal)
            // A start written since BL-549 also carries the fingerprint of every fact it was asked with; an older one is
            // held to its identity (definition and object), the only facts it stored.
            // FIX1 K1 — the start is the starter's: somebody else replaying the same key and facts does not complete it under
            // the original starter's name. Held on the stored starter, so a start written before BL-549 is held too.
            || !SameStarter(existing)
            || (existing.StartRequestFingerprint is { } stored
                && !string.Equals(stored, Fingerprint(templateId, objectType, objectId, objectRef, request.Request), StringComparison.Ordinal)))
        {
            return StartConflict("The idempotency key is already bound to different workflow start facts.", correlationId);
        }

        var version = existing.TemplateVersionId is { } versionId
            ? await _versionRepository.GetByIdForTemplateAsync(templateId, versionId, ct)
            : null;
        if (version is null)
        {
            return StartConflict("Existing workflow start is incomplete.", correlationId);
        }

        var runtimeSteps = WorkflowDefinitionRuntimePlan.FromVersion(version);
        var firstStep = runtimeSteps.Count > 0
            ? runtimeSteps[0]
            : WorkflowDefinitionRuntimePlan.FallbackStep(
                WorkflowCandidateResolver.Normalize(request.Request.CandidatePrincipalIds),
                request.Request.CommentRequired,
                request.Request.EvidenceRequired);

        List<string> candidates;
        if (task is not null && !string.IsNullOrWhiteSpace(task.AssigneeRef))
        {
            // The task names who it went to — the persisted fact, not who the seat resolves to now (the trusted start's rule).
            var resolved = await ResolveCandidatesAsync(firstStep, request.Request, ct);
            candidates = [task.AssigneeRef, .. resolved.Where(c => !string.Equals(c, task.AssigneeRef, StringComparison.OrdinalIgnoreCase))];
        }
        else
        {
            var candidateSource = firstStep.CandidatePrincipalIds.Count > 0
                ? firstStep.CandidatePrincipalIds
                : WorkflowCandidateResolver.Normalize(request.Request.CandidatePrincipalIds);
            candidates = await ResolveCandidatesAsync(firstStep, request.Request, ct);
            candidates = WorkflowTaskTransitionSupport.WithoutStarter(candidates, existing.StartedByUserId, existing.StartedBy).ToList();
            if (candidates.Count == 0)
            {
                // Nothing is written: the same answer a first call gets, and the replay after someone takes the seat completes.
                return candidateSource.Count == 0
                    ? Response<StartWorkflowInstanceResponse>.Fail(
                        "At least one assignment candidate is required.", 400,
                        WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, correlationId)
                    : Response<StartWorkflowInstanceResponse>.Fail(
                        "Nobody holds the approving seat right now; nothing was started. Try again once someone does.", 503,
                        WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, correlationId);
            }
        }

        var instance = existing;
        var resolvedPrincipal = candidates[0];
        ApprovalTask completedTask;
        Guid completedSnapshotId;
        for (var attempt = 0; ; attempt++)
        {
            var taskId = task?.Id ?? instance.InitialApprovalTaskId ?? Guid.NewGuid();
            var snapshotId = task?.AssignmentSnapshotId ?? instance.InitialAssignmentSnapshotId ?? Guid.NewGuid();
            var logId = instance.StartTransitionLogId ?? Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            // What the first call did not write — and ONLY that — goes in with the claim, in one transaction (FIX3 K8).
            var missingTask = task is not null ? null : new ApprovalTask
            {
                Id = taskId,
                TenantId = instance.TenantId,
                WorkflowInstanceId = instance.Id,
                StageCode = firstStep.StageCode,
                StepCode = firstStep.StepCode,
                Status = ApprovalTaskStatus.WaitingApproval,
                AssigneeRef = resolvedPrincipal,
                AssignmentSnapshotId = snapshotId,
                ReasonCode = request.Request.ReasonCode,
                IdempotencyKey = instance.IdempotencyKey,
                CommentRequired = firstStep.CommentRequired || request.Request.CommentRequired,
                EvidenceRequired = firstStep.EvidenceRequired || request.Request.EvidenceRequired,
                DueAt = instance.DueAt
            };
            var existingSnapshot = snapshot ?? await _assignmentSnapshotRepository.GetByIdAsync(snapshotId, ct);
            var missingSnapshot = existingSnapshot is not null ? null : new RuntimeAssignmentSnapshot
            {
                Id = snapshotId,
                TenantId = instance.TenantId,
                WorkflowInstanceId = instance.Id,
                ApprovalTaskId = taskId,
                ResolverSource = ResolverSource,
                ResolvedPrincipalId = task?.AssigneeRef ?? resolvedPrincipal,
                CandidatePrincipalIds = candidates.ToList(),
                ResolvedAt = now.UtcDateTime,
                TieBreakExplanation = candidates.Count == 1 ? "single_candidate" : "lexicographic_first_principal"
            };
            var missingLog = await StartLogAsync(instance.Id, ct) is not null ? null : new WorkflowTransitionLog
            {
                Id = logId,
                TenantId = instance.TenantId,
                WorkflowInstanceId = instance.Id,
                ApprovalTaskId = taskId,
                Action = WorkflowTransitionAction.Start,
                FromState = null,
                ToState = WorkflowInstanceStatus.Active.ToString(),
                FromStatus = null,
                ToStatus = WorkflowInstanceStatus.Active.ToString(),
                // The start's own actor, not the replay's: the log records who started it.
                ActorId = instance.StartedByUserId is { } starter && starter != Guid.Empty ? starter.ToString() : instance.StartedBy,
                ActorRef = instance.StartedBy,
                ReasonCode = request.Request.ReasonCode,
                IdempotencyKey = instance.IdempotencyKey,
                CorrelationId = instance.CorrelationId ?? correlationId,
                SequenceNo = 1,
                CreatedAt = instance.StartedAt ?? now
            };
            bool claimed;
            try
            {
                claimed = await ClaimWithStartRowsAsync(instance.Id, instance.Version, taskId, snapshotId, logId,
                    missingTask, missingSnapshot, missingLog, ct);
            }
            catch (PlatformTransactionUnavailableException)
            {
                return await AfterUnavailableAsync(instance.Id, correlationId, ct);
            }

            if (claimed)
            {
                completedTask = task ?? missingTask!;
                completedSnapshotId = existingSnapshot?.Id ?? snapshotId;
                break;
            }

            // Another replay claimed it first (its ids are now on the instance), or it was closed meanwhile.
            var reread = await _instanceRepository.GetByIdAsync(instance.Id, ct);
            if (reread is null || reread.Status != WorkflowInstanceStatus.Active)
            {
                return StartConflict("Existing workflow start is incomplete.", correlationId);
            }

            // FIX3 item 1 — the replay that won may already have committed the whole start: answered as it is. Out of tries
            // with the start still not whole, the answer is the retryable 503, not a 409 that reads as "different facts".
            if (await WholeStartAsync(reread, correlationId, ct) is { } whole)
            {
                return whole;
            }

            if (attempt >= 2)
            {
                return StartUnavailable(correlationId);
            }

            instance = reread;
            task ??= await _taskRepository.GetFirstByInstanceIdAsync(instance.Id, ct);
            snapshot ??= task?.AssignmentSnapshotId is { } readSnapshotId
                ? await _assignmentSnapshotRepository.GetByIdAsync(readSnapshotId, ct)
                : null;
        }

        instance = await _instanceRepository.GetByIdAsync(instance.Id, ct) ?? instance;
        return Response<StartWorkflowInstanceResponse>.Success(
            ToStartResponse(instance, completedTask, completedSnapshotId, correlationId),
            correlationId: correlationId);
    }

    private bool SameStarter(WorkflowInstance existing)
    {
        if (existing.StartedByUserId is { } starter && starter != Guid.Empty)
        {
            return _currentUserContext.UserId == starter;
        }

        return string.Equals(existing.StartedBy, _currentUserContext.ActorName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<string>> ResolveCandidatesAsync(
        WorkflowRuntimeStep firstStep,
        StartWorkflowInstanceRequest request,
        CancellationToken ct)
    {
        var candidateSource = firstStep.CandidatePrincipalIds.Count > 0
            ? firstStep.CandidatePrincipalIds
            : WorkflowCandidateResolver.Normalize(request.CandidatePrincipalIds);
        return (await WorkflowCandidateResolver.ResolveAsync(
            candidateSource, _positionAssignmentRepository, _positionRepository, ct)).ToList();
    }

    private async Task<WorkflowTransitionLog?> StartLogAsync(Guid instanceId, CancellationToken ct) =>
        (await _transitionLogRepository.ListByInstanceIdAsync(instanceId, ct))
            .FirstOrDefault(log => log.SequenceNo == 1 && log.Action == WorkflowTransitionAction.Start);

    /// <summary>
    /// FIX2 item 1 · FIX3 K8 — the claim and every start row still missing (task, assignment snapshot, start log) in ONE
    /// Platform transaction: all commit or none does. False = the claim was lost (another replay claimed first, or the
    /// instance left Active).
    /// </summary>
    private async Task<bool> ClaimWithStartRowsAsync(
        Guid instanceId,
        int version,
        Guid taskId,
        Guid snapshotId,
        Guid logId,
        ApprovalTask? taskToWrite,
        RuntimeAssignmentSnapshot? snapshotToWrite,
        WorkflowTransitionLog? logToWrite,
        CancellationToken ct)
    {
        if (_transactions is null)
        {
            // Tests only (SequentialWorkflowStartWritesForTests): the claim first, then each missing row on its own.
            if (!await _instanceRepository.ClaimStartCompletionAsync(instanceId, version, taskId, snapshotId, logId, DateTimeOffset.UtcNow, ct))
            {
                return false;
            }

            if (taskToWrite is not null)
            {
                await _taskRepository.EnsureTrustedStartTaskAsync(taskToWrite, ct);
            }

            if (snapshotToWrite is not null)
            {
                await _assignmentSnapshotRepository.EnsureTrustedStartSnapshotAsync(snapshotToWrite, ct);
            }

            if (logToWrite is not null)
            {
                await _transitionLogRepository.EnsureTrustedStartLogAsync(logToWrite, ct);
            }

            return true;
        }

        try
        {
            return await _transactions.ExecuteAsync(async (session, tct) =>
        {
            if (!await _instanceRepository.ClaimStartCompletionAsync(
                    session, instanceId, version, taskId, snapshotId, logId, DateTimeOffset.UtcNow, tct))
            {
                return false;
            }

            if (taskToWrite is not null)
            {
                await _taskRepository.CreateAsync(session, taskToWrite, tct);
            }

            if (snapshotToWrite is not null)
            {
                await _assignmentSnapshotRepository.CreateAsync(session, snapshotToWrite, tct);
            }

            if (logToWrite is not null)
            {
                await _transitionLogRepository.CreateAsync(session, logToWrite, tct);
            }

            return true;
        }, ct);
        }
        catch (MongoDB.Driver.MongoException exception) when (
            exception.HasErrorLabel("TransientTransactionError") || exception.HasErrorLabel("UnknownTransactionCommitResult"))
        {
            // FIX3 K7 — a transient failure AT COMMIT leaves the executor unmapped (it maps the body's and the unknown
            // commit result's); for the start it is the same "not written now, replay the key" (503), never a 500.
            throw new PlatformTransactionUnavailableException("The start's transaction ended in a transient error.", exception);
        }
    }

    /// <summary>
    /// FIX3 item 1 — "UNAVAILABLE" IS OFTEN A LOST RACE. Two submits of one key at once write the same instance in two
    /// transactions; the loser gets a write conflict, which the executor retries without waiting — all three tries can meet
    /// the same open winner. Before answering 503 the instance and its rows are read again: a live, WHOLE start (the winner
    /// committed) is answered exactly as its replay. Only a live start that is not whole yet answers 503 (replay the key); one
    /// that left Active answers 409. The terminal writer's AfterUnavailableAsync is the same idea.
    /// </summary>
    private async Task<Response<StartWorkflowInstanceResponse>> AfterUnavailableAsync(
        Guid instanceId, string correlationId, CancellationToken ct)
    {
        var reread = await _instanceRepository.GetByIdAsync(instanceId, ct);
        if (reread is null || reread.Status != WorkflowInstanceStatus.Active)
        {
            return StartConflict("The workflow start was closed before its task was written.", correlationId);
        }

        return await WholeStartAsync(reread, correlationId, ct) ?? StartUnavailable(correlationId);
    }

    /// <summary>The answer for a live start that is WHOLE (task, its snapshot, the start log); null when it is not yet.</summary>
    private async Task<Response<StartWorkflowInstanceResponse>?> WholeStartAsync(
        WorkflowInstance instance, string correlationId, CancellationToken ct)
    {
        var task = await _taskRepository.GetFirstByInstanceIdAsync(instance.Id, ct);
        if (task?.AssignmentSnapshotId is { } snapshotId
            && await _assignmentSnapshotRepository.GetByIdAsync(snapshotId, ct) is not null
            && await StartLogAsync(instance.Id, ct) is not null)
        {
            return Response<StartWorkflowInstanceResponse>.Success(
                ToStartResponse(instance, task, snapshotId, correlationId),
                correlationId: correlationId);
        }

        return null;
    }

    private static Response<StartWorkflowInstanceResponse> StartUnavailable(string correlationId) =>
        Response<StartWorkflowInstanceResponse>.Fail(
            "The workflow start could not be written right now; replay the same key to complete it.",
            503,
            WorkflowReasonCodes.WorkflowTransitionConflict,
            correlationId);

    private static Response<StartWorkflowInstanceResponse> StartConflict(string message, string correlationId) =>
        Response<StartWorkflowInstanceResponse>.Fail(message, 409, WorkflowReasonCodes.WorkflowInstanceStartConflict, correlationId);

    /// <summary>
    /// BL-549 — the facts a keyed generic start was asked with, so its replay can tell "the same start" from "something else
    /// under the same key". The candidates are the ones the CALLER named (sorted), not who they resolved to: a seat changing
    /// hands between the crash and the replay is not a different request.
    /// </summary>
    private static string Fingerprint(
        Guid templateId,
        string objectType,
        string objectId,
        string objectRef,
        StartWorkflowInstanceRequest request)
    {
        var candidates = WorkflowCandidateResolver.Normalize(request.CandidatePrincipalIds)
            .OrderBy(c => c, StringComparer.Ordinal);
        var facts = string.Join('\n',
            "generic-start-v1",
            templateId.ToString("D"),
            objectType,
            objectId,
            objectRef,
            string.Join(',', candidates),
            request.ReasonCode?.Trim() ?? string.Empty,
            request.CommentRequired ? "1" : "0",
            request.EvidenceRequired ? "1" : "0",
            request.DueAt?.ToUniversalTime().UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(facts)))
            .ToLowerInvariant();
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
