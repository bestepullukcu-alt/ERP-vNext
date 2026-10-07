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

    /// <summary>D2-FIX2 item 6(b) — who withdrew a start task left waiting under a closed reservation.</summary>
    internal const string StartRecoveryActor = "system:workflow-start-recovery";

    /// <summary>D2-FIX2 item 3 / D2-FIX3 item 7 — the one answer to an empty approving seat, first call and replay alike.</summary>
    private const string EmptySeatMessage =
        "Nobody holds the approving seat right now; the start is kept and a retry with the same idempotency key completes it once someone does.";

    private readonly IWorkflowTemplateRepository _templates;
    private readonly IWorkflowTemplateVersionRepository _versions;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _tasks;
    private readonly IRuntimeAssignmentSnapshotRepository _snapshots;
    private readonly IWorkflowTransitionLogRepository _logs;
    private readonly ITenantContext _tenantContext;
    private readonly IPositionAssignmentRepository? _positionAssignments;
    private readonly ISlaEscalationRuleRepository? _slaRules;
    private readonly IPositionRepository? _positions;

    public WorkflowInstanceStartCoordinator(
        IWorkflowTemplateRepository templates,
        IWorkflowTemplateVersionRepository versions,
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository tasks,
        IRuntimeAssignmentSnapshotRepository snapshots,
        IWorkflowTransitionLogRepository logs,
        ITenantContext tenantContext,
        IPositionAssignmentRepository? positionAssignments = null,
        ISlaEscalationRuleRepository? slaRules = null,
        IPositionRepository? positions = null)
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
        _positions = positions;
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
        var existing = await _instances.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        var storedSnapshot = existing?.InitialAssignmentSnapshotId is { } storedSnapshotId
            ? await _snapshots.GetByIdAsync(storedSnapshotId, ct)
            : null;

        // D2 — A REPLAY TRUSTS WHAT WAS STORED. Once the start's assignment snapshot exists, its candidate list is the
        // fact; the seat is not resolved again. (Resolving again turned a seat change between the call and its retry
        // into a 409 for the same request — and MDM quarantines a 409.) Only a start that has no snapshot yet resolves.
        IReadOnlyList<string> candidates;
        // D2-FIX1 A3 — why a fresh resolution found nobody. A NEW start fails here at once (nothing was written); a key
        // that is already reserved first looks at what its first call persisted, below, and fails only if nothing was.
        string? noCandidateReason = null;
        if (storedSnapshot is not null)
        {
            if (storedSnapshot.CandidatePrincipalIds.Count == 0)
            {
                // D2-FIX1 — a stored proof with nobody in it cannot be replayed (and candidates[0] would throw).
                return Conflict("Trusted workflow assignment proof names no candidate.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            candidates = storedSnapshot.CandidatePrincipalIds;
        }
        else
        {
            var resolved = await WorkflowCandidateResolver.ResolveAsync(candidateSource, _positionAssignments, _positions, ct);
            // D2 — the delegated maker (the person the consumer starts the workflow for) is not one of its approvers.
            candidates = resolved.Count == 0
                ? []
                : WorkflowTaskTransitionSupport.WithoutStarter(resolved, delegatedMakerUserId, null);
            noCandidateReason = resolved.Count == 0
                ? WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired
                : candidates.Count == 0 ? WorkflowReasonCodes.WorkflowNoEligibleApprover : null;
            if (noCandidateReason is not null && existing is null)
            {
                /*
                 * D2-FIX3 item 7 — AN EMPTY SEAT IS TRANSIENT ON THE FIRST CALL TOO. The replay of a reserved key answers
                 * it 503 (below, D2-FIX2 item 3); the first call answered the same condition 400 — and MDM files a 400 as
                 * Invalid and QUARANTINES the operation, while a 503 is Retryable (its start-result lookup then finds no
                 * reservation and waits for the maker's replay). Same condition, same answer: 503 with the same contract
                 * code and the same sentence the replay gives. Nothing is reserved, so the retry is a fresh first call.
                 * Only "nobody but the starter" stays a 400: that one is a verdict, not a wait.
                 */
                return noCandidateReason == WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired
                    ? Unavailable(EmptySeatMessage, noCandidateReason, correlationId)
                    : NoCandidate(noCandidateReason, correlationId);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var dueAt = request.DueAt?.ToUniversalTime() ?? existing?.DueAt ??
            ResolveDueAtFromStep(firstStep, now) ??
            await ResolveDueAtAsync(template.Id, firstStep.StageCode, firstStep.StepCode, now, ct);
        var objectRef = string.IsNullOrWhiteSpace(request.ObjectRef)
            ? $"{objectType}|{objectId}"
            : request.ObjectRef.Trim();
        var reasonCode = string.IsNullOrWhiteSpace(request.ReasonCode) ? null : request.ReasonCode.Trim();
        var commentRequired = firstStep.CommentRequired || request.CommentRequired;
        var evidenceRequired = firstStep.EvidenceRequired || request.EvidenceRequired;
        // D2 — the fingerprint names what the caller DECLARED (the step's or the request's candidate list), not who
        // the seat resolves to today: the same request stays the same request when a seat changes hands.
        var declared = DeclaredCandidates(candidateSource);
        string Fingerprint(DateTimeOffset? due) => ComputeDeclaredFingerprint(
            _tenantContext.TenantId, serviceClientId, delegatedMakerUserId, template.Id, version.Id,
            objectType, objectId, objectRef, declared, reasonCode, commentRequired, evidenceRequired, due);
        var fingerprint = Fingerprint(dueAt);

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
            // D2 — the starter as a USER ID, so the gate's starter check (SoD) holds in any spelling of the id.
            StartedByUserId = delegatedMakerUserId,
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
            fingerprint = Fingerprint(dueAt);
        }

        if (!reserved.Created
            && !string.Equals(instance.StartRequestFingerprint, fingerprint, StringComparison.Ordinal)
            && await LegacyFingerprintAsync(candidateSource, storedSnapshot, serviceClientId, delegatedMakerUserId,
                template.Id, version.Id, objectType, objectId, objectRef, reasonCode, commentRequired, evidenceRequired,
                dueAt, ct) is { } legacy
            && string.Equals(instance.StartRequestFingerprint, legacy, StringComparison.Ordinal))
        {
            // A key reserved before D2 carries the first fingerprint (over the RESOLVED candidates). It is still in
            // flight somewhere (a consumer's outbox, a retry): its replay is answered as before, by that formula.
            fingerprint = legacy;
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

        // WP-WF-ENGINE-RECOVERY-01 — a reservation closed at ANY checkpoint short of Completed (the recovery closes incomplete
        // ones wherever they stopped) is answered the same way, never resumed.
        if (instance.Status == WorkflowInstanceStatus.Cancelled && instance.StartCheckpoint < WorkflowStartCheckpoint.Completed)
        {
            // D2-FIX1 A3 — a reservation closed because nobody could approve it (below): every replay of the key gets
            // the same answer, and it is never brought back to life by a later checkpoint.
            // D2-FIX2 item 3 — the answer is the reason the reservation was closed WITH, stored on it.
            // D2-FIX2 item 6(b) — and a task a racing first call wrote under it is withdrawn, never left waiting.
            await WithdrawStrayStartTaskAsync(instance, correlationId, ct);
            return ClosedAnswer(instance, correlationId);
        }

        if (!reserved.Created && storedSnapshot is null)
        {
            /*
             * D2-FIX1 A3 — A RESERVED KEY WITHOUT ITS SNAPSHOT: the first call crashed after the reservation (or the task)
             * and before the snapshot, or two first calls raced and this one lost. What the first call PERSISTED is the
             * fact, not who the seat resolves to now: a seat change in between used to rebuild a different task and
             * answer 409 RECOVERY_CONFLICT (MDM quarantines it), or 400 with the reservation left Pending forever.
             *
             * D2-FIX2 — a late snapshot that names NOBODY is no fact to follow: the task is read as if there were no
             * snapshot (skipping that read answered "no eligible approver" for a start that did write its task).
             */
            var persistedSnapshot = await _snapshots.GetByIdAsync(instance.InitialAssignmentSnapshotId.Value, ct);
            var lateSnapshotNamesSomeone = persistedSnapshot is { CandidatePrincipalIds.Count: > 0 };
            var persistedTask = lateSnapshotNamesSomeone
                ? null
                : await _tasks.GetByIdAsync(instance.InitialApprovalTaskId.Value, ct);
            if (lateSnapshotNamesSomeone)
            {
                candidates = persistedSnapshot!.CandidatePrincipalIds;
                noCandidateReason = null;
            }
            else if (persistedTask is not null && !string.IsNullOrWhiteSpace(persistedTask.AssigneeRef))
            {
                // The task names who it went to; the snapshot still to be written lists that person first.
                candidates = [persistedTask.AssigneeRef, .. candidates.Where(c => !SamePrincipal(c, persistedTask.AssigneeRef))];
                noCandidateReason = null;
            }
            else if (noCandidateReason == WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired)
            {
                /*
                 * D2-FIX2 item 3 — NOBODY RESOLVED AT ALL (an empty seat) IS NOT A VERDICT. Closing here made the key dead:
                 * once the seat was filled every replay got a DIFFERENT 400 (no-eligible) for the same request. The
                 * reservation stays open and the answer is transient (503); the replay after someone takes the seat
                 * completes the start. Only "nobody but the starter" (below) closes it.
                 */
                return Unavailable(EmptySeatMessage, noCandidateReason, correlationId);
            }
            else if (noCandidateReason is not null)
            {
                // Nothing was persisted past the reservation, and nobody may approve it now: the reservation is CLOSED
                // (cancelled), never left Pending — the same request is answered the same way on every replay.
                // D2-FIX2 item 3 — the reason is stored WITH the close, and every replay answers it (ClosedReason).
                if (instance.Status == WorkflowInstanceStatus.Pending && instance.StartCheckpoint == WorkflowStartCheckpoint.Reserved)
                {
                    var closedAt = DateTimeOffset.UtcNow;
                    instance.Status = WorkflowInstanceStatus.Cancelled;
                    instance.CompletedAt = closedAt;
                    instance.LastTransitionAt = closedAt;
                    instance.StartClosedReasonCode = noCandidateReason;
                    if (!await _instances.UpdateAsync(instance, instance.Version, ct))
                    {
                        return Conflict("Trusted workflow reservation changed meanwhile.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
                    }
                }

                return NoCandidate(noCandidateReason, correlationId);
            }
        }

        var task = ExpectedTask(
            instance,
            firstStep,
            candidates[0],
            reasonCode,
            idempotencyKey,
            dueAt,
            commentRequired,
            evidenceRequired);
        var snapshot = ExpectedSnapshot(instance, task.Id, candidates);
        var log = ExpectedStartLog(instance, task.Id, delegatedMakerUserId, reasonCode, idempotencyKey, correlationId);

        if (instance.StartCheckpoint < WorkflowStartCheckpoint.TaskPersisted)
        {
            var persisted = await _tasks.EnsureTrustedStartTaskAsync(task, ct);
            if (!MatchesTask(persisted, task))
            {
                return await ConflictOrClosedAsync(instance.Id, "Trusted workflow task recovery facts conflict.", correlationId, ct);
            }

            var advanced = await AdvanceAsync(instance, WorkflowStartCheckpoint.Reserved, WorkflowStartCheckpoint.TaskPersisted, ct);
            if (!advanced.Success)
            {
                /*
                 * D2-FIX2 item 6(b) — THE TASK STANDS ONLY IF ITS CHECKPOINT FENCE HOLDS. A twin call with the same key may
                 * have CLOSED the reservation (nobody but the starter could approve when it looked) while this call was
                 * writing its task: the fence then fails on the version the close moved, and the task just written sits
                 * WaitingApproval under a Cancelled instance — decidable by nobody, read by nothing. It is withdrawn here,
                 * and this call answers what every replay of the key answers. (The other way — making the close wait
                 * for this write — needs a lease or a second state on the reservation; the fence is already the
                 * arbiter, so the loser of it cleans up. A crash between the write and this line is healed by the next
                 * replay, above.)
                 */
                if (await ClosedMeanwhileAsync(advanced.Instance, correlationId, ct) is { } closedAnswer)
                {
                    return closedAnswer;
                }

                return await ConflictOrClosedAsync(instance.Id, "Trusted workflow task checkpoint could not be fenced.", correlationId, ct);
            }

            instance = advanced.Instance!;
        }
        else
        {
            var persisted = await _tasks.GetByIdAsync(task.Id, ct);
            if (persisted is null || !MatchesTask(persisted, task))
            {
                return await ConflictOrClosedAsync(instance.Id, "Trusted workflow task proof is missing or inconsistent.", correlationId, ct);
            }
        }

        if (instance.StartCheckpoint < WorkflowStartCheckpoint.AssignmentSnapshotPersisted)
        {
            var persisted = await _snapshots.EnsureTrustedStartSnapshotAsync(snapshot, ct);
            if (!MatchesSnapshot(persisted, snapshot))
            {
                return await ConflictOrClosedAsync(instance.Id, "Trusted workflow assignment proof conflicts.", correlationId, ct);
            }

            var advanced = await AdvanceAsync(instance, WorkflowStartCheckpoint.TaskPersisted, WorkflowStartCheckpoint.AssignmentSnapshotPersisted, ct);
            if (!advanced.Success)
            {
                // WP-WF-ENGINE-RECOVERY-01 — the recovery closed the reservation between this call's steps: it lost the race.
                return await ClosedMeanwhileAsync(advanced.Instance, correlationId, ct)
                    ?? Conflict("Trusted workflow assignment checkpoint could not be fenced.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            instance = advanced.Instance!;
        }
        else
        {
            var persisted = await _snapshots.GetByIdAsync(snapshot.Id, ct);
            if (persisted is null || !MatchesSnapshot(persisted, snapshot))
            {
                return await ConflictOrClosedAsync(instance.Id, "Trusted workflow assignment proof is missing or inconsistent.", correlationId, ct);
            }
        }

        if (instance.StartCheckpoint < WorkflowStartCheckpoint.StartLogPersisted)
        {
            var persisted = await _logs.EnsureTrustedStartLogAsync(log, ct);
            if (!MatchesStartLog(persisted, log))
            {
                return await ConflictOrClosedAsync(instance.Id, "Trusted workflow start-log proof conflicts.", correlationId, ct);
            }

            var advanced = await AdvanceAsync(instance, WorkflowStartCheckpoint.AssignmentSnapshotPersisted, WorkflowStartCheckpoint.StartLogPersisted, ct);
            if (!advanced.Success)
            {
                // WP-WF-ENGINE-RECOVERY-01 — the recovery closed the reservation between this call's steps: it lost the race.
                return await ClosedMeanwhileAsync(advanced.Instance, correlationId, ct)
                    ?? Conflict("Trusted workflow start-log checkpoint could not be fenced.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
            }

            instance = advanced.Instance!;
        }
        else
        {
            var persisted = await _logs.GetByIdAsync(log.Id, ct);
            if (persisted is null || !MatchesStartLog(persisted, log))
            {
                return await ConflictOrClosedAsync(instance.Id, "Trusted workflow start-log proof is missing or inconsistent.", correlationId, ct);
            }
        }

        if (instance.StartCheckpoint < WorkflowStartCheckpoint.Completed)
        {
            var advanced = await AdvanceAsync(instance, WorkflowStartCheckpoint.StartLogPersisted, WorkflowStartCheckpoint.Completed, ct);
            if (!advanced.Success)
            {
                // WP-WF-ENGINE-RECOVERY-01 — the recovery closed the reservation between this call's steps: it lost the race.
                return await ClosedMeanwhileAsync(advanced.Instance, correlationId, ct)
                    ?? Conflict("Trusted workflow completion checkpoint could not be fenced.", WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);
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
            return await ConflictOrClosedAsync(instance.Id, "Trusted workflow completion proof is inconsistent.", correlationId, ct);
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
        SamePrincipal(actual.AssigneeRef, expected.AssigneeRef) &&
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
        SamePrincipal(actual.ResolvedPrincipalId, expected.ResolvedPrincipalId) &&
        NamesTheSamePrincipals(actual.CandidatePrincipalIds, expected.CandidatePrincipalIds) &&
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

    // D1-FIX1 — a proof names a PERSON: a user id matches in any spelling; any other principal letter for letter.
    private static bool SamePrincipal(string? left, string? right) =>
        string.Equals(left, right, StringComparison.Ordinal) || WorkflowTaskTransitionSupport.PrincipalEquals(left, right);

    private static bool NamesTheSamePrincipals(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        static HashSet<string> Canonical(IReadOnlyList<string> principals) =>
            principals.Select(p => WorkflowTaskTransitionSupport.CanonicalPrincipal(p.Trim())).ToHashSet(StringComparer.Ordinal);

        return Canonical(left).SetEquals(Canonical(right));
    }

    /// <summary>
    /// D2 — the candidate list as DECLARED, in one spelling: <c>user:</c> and <c>position:</c> entries keep their prefix
    /// with the id in its one spelling, a bare user id in its one spelling, anything else as written; distinct, ordinal.
    /// </summary>
    internal static IReadOnlyList<string> DeclaredCandidates(IReadOnlyList<string> source)
    {
        static string Canonical(string entry)
        {
            foreach (var prefix in new[] { "user:", "position:" })
            {
                if (entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return prefix + WorkflowTaskTransitionSupport.CanonicalPrincipal(entry[prefix.Length..].Trim());
                }
            }

            return WorkflowTaskTransitionSupport.CanonicalPrincipal(entry);
        }

        return WorkflowCandidateResolver.Normalize(source)
            .Select(Canonical)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// D2 — FINGERPRINT VERSION 2: over the DECLARED candidates (<see cref="DeclaredCandidates"/>), tagged <c>v2</c> so it
    /// can never equal a version-1 value. Every other fact is the version-1 set, in the same order.
    /// </summary>
    internal static string ComputeDeclaredFingerprint(
        Guid tenantId,
        Guid clientId,
        Guid makerId,
        Guid templateId,
        Guid versionId,
        string objectType,
        string objectId,
        string objectRef,
        IReadOnlyList<string> declaredCandidates,
        string? reasonCode,
        bool commentRequired,
        bool evidenceRequired,
        DateTimeOffset? dueAt) =>
        ComputeFingerprint(tenantId, clientId, makerId, templateId, versionId, objectType, objectId, objectRef,
            declaredCandidates.Prepend(DeclaredFingerprintTag).ToList(), reasonCode, commentRequired, evidenceRequired,
            dueAt);

    private const string DeclaredFingerprintTag = "fingerprint:v2:declared";

    /// <summary>
    /// The version-1 fingerprint, EXACTLY as a start before D2 computed it: over the candidates resolved NOW (with
    /// D1-FIX1's stored-spelling substitution). Accepted only for a key that already carries a version-1 value; a new
    /// start never writes one. It is retired when no version-1 reservation can be replayed any more — see the D2 report
    /// (the consumers' retry windows) — by deleting this method and its one call.
    /// </summary>
    private async Task<string?> LegacyFingerprintAsync(
        IReadOnlyList<string> candidateSource,
        RuntimeAssignmentSnapshot? storedSnapshot,
        Guid serviceClientId,
        Guid delegatedMakerUserId,
        Guid templateId,
        Guid versionId,
        string objectType,
        string objectId,
        string objectRef,
        string? reasonCode,
        bool commentRequired,
        bool evidenceRequired,
        DateTimeOffset? dueAt,
        CancellationToken ct)
    {
        // D2-FIX1 A2 — a stored snapshot IS the list the version-1 fingerprint was computed over (D1-FIX1 stored the
        // resolved list it hashed). Resolving the seat again here turned a seat change into a 409 on the replay — and
        // MDM quarantines a 409. Only a start with no snapshot yet resolves.
        IReadOnlyList<string> resolved;
        if (storedSnapshot is not null)
        {
            resolved = storedSnapshot.CandidatePrincipalIds;
        }
        else
        {
            resolved = await WorkflowCandidateResolver.ResolveAsync(candidateSource, _positionAssignments, _positions, ct);
            if (resolved.Count == 0)
            {
                return null;
            }
        }

        return ComputeFingerprint(_tenantContext.TenantId, serviceClientId, delegatedMakerUserId, templateId, versionId,
            objectType, objectId, objectRef, resolved, reasonCode, commentRequired, evidenceRequired, dueAt);
    }

    /// <summary>The version-1 computation (D1-FIX1 byte-for-byte test). D2 — version 2 feeds it the declared list.</summary>
    internal static string ComputeFingerprint(
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

    /// <summary>D2-FIX2 item 3 — the reason a closed reservation was closed with; a reservation closed before the reason was
    /// stored was closed for "no eligible approver" (the only close D2-FIX1 answered with).</summary>
    private static string ClosedReason(WorkflowInstance closed) =>
        string.IsNullOrWhiteSpace(closed.StartClosedReasonCode)
            ? WorkflowReasonCodes.WorkflowNoEligibleApprover
            : closed.StartClosedReasonCode;

    /// <summary>
    /// D2-FIX2 item 6(b) — the start task of a CLOSED reservation, if one was written and is still waiting, is withdrawn
    /// (Cancelled, actioned by the start recovery, with the close's reason). Best effort: a task anything else has moved
    /// meanwhile is no longer waiting on this reservation's word, so a lost version check is left as it is.
    ///
    /// <para>D2-FIX3 item 8 — the write's RESULT is read, and only the write that landed records the act: a Cancel line in
    /// the instance's transition log (who: the start recovery; why: the close's reason; keyed per task, so a twin replay
    /// that also got here writes it once). Before, the result was dropped and the withdraw left no trace in the log every
    /// other MOD-0023 transition writes to. A write that lost its race writes nothing — whoever moved the task did.</para>
    /// </summary>
    /// <summary>
    /// WP-WF-ENGINE-RECOVERY-01 — a proof that no longer matches while the recovery closed the reservation meanwhile (it
    /// withdrew the start task, so the task's status no longer matches) is the closed key, not a recovery conflict: answered as
    /// every replay of a closed key is. Only a reservation still open gets the conflict.
    /// </summary>
    private async Task<Response<TrustedWorkflowStartResult>> ConflictOrClosedAsync(
        Guid instanceId, string message, string correlationId, CancellationToken ct) =>
        await ClosedMeanwhileAsync(await _instances.GetByIdAsync(instanceId, ct), correlationId, ct)
            ?? Conflict(message, WorkflowReasonCodes.WorkflowStartRecoveryConflict, correlationId);

    /// <summary>WP-WF-ENGINE-RECOVERY-01 — a reservation found closed after a failed fence: its stray task is withdrawn and
    /// the call answers what every replay of the key answers. Null when the reservation was not closed.</summary>
    private async Task<Response<TrustedWorkflowStartResult>?> ClosedMeanwhileAsync(
        WorkflowInstance? reread, string correlationId, CancellationToken ct)
    {
        if (reread is not { Status: WorkflowInstanceStatus.Cancelled } closedMeanwhile
            || closedMeanwhile.StartCheckpoint >= WorkflowStartCheckpoint.Completed)
        {
            return null;
        }

        await WithdrawStrayStartTaskAsync(closedMeanwhile, correlationId, ct);
        return ClosedAnswer(closedMeanwhile, correlationId);
    }

    /// <summary>WP-WF-ENGINE-RECOVERY-01 (CT decision, principle A) — a reservation the recovery closed as abandoned answers
    /// a replay exactly as start-result answers its key: as a key the engine never saw. One closed for "nobody may approve"
    /// keeps its D2 answer.</summary>
    private static Response<TrustedWorkflowStartResult> ClosedAnswer(WorkflowInstance closed, string correlationId) =>
        string.Equals(closed.StartClosedReasonCode, WorkflowReasonCodes.WorkflowStartAbandoned, StringComparison.Ordinal)
            ? Handlers.QueryHandlers.GetTrustedWorkflowStartResultHandler.NotFound(correlationId)
            : NoCandidate(ClosedReason(closed), correlationId);

    private Task WithdrawStrayStartTaskAsync(WorkflowInstance closed, string correlationId, CancellationToken ct) =>
        WithdrawStrayStartTaskAsync(_tasks, _logs, closed, correlationId, ct);

    /// <summary>WP-WF-ENGINE-RECOVERY-01 — shared with the abandoned-start recovery's close of an incomplete reservation.</summary>
    internal static async Task WithdrawStrayStartTaskAsync(
        IApprovalTaskRepository tasks,
        IWorkflowTransitionLogRepository logs,
        WorkflowInstance closed,
        string correlationId,
        CancellationToken ct)
    {
        if (closed.InitialApprovalTaskId is not { } taskId
            || await tasks.GetByIdAsync(taskId, ct) is not { } stray
            || stray.Status is not (ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var waitingAs = stray.Status;
        var reason = ClosedReason(closed);
        stray.Status = ApprovalTaskStatus.Cancelled;
        stray.CompletedAt = now;
        stray.ActionedBy = StartRecoveryActor;
        stray.ActionReasonCode = reason;
        if (!await tasks.UpdateAsync(stray, stray.Version, ct))
        {
            return;
        }

        var idempotencyKey = $"workflow-start-recovery-withdraw:{stray.Id:N}";
        if (await logs.FindByIdempotencyAsync(stray.Id, WorkflowTransitionAction.Cancel, idempotencyKey, ct) is not null)
        {
            return;
        }

        /*
         * D2-FIX4 item 7 — AN IDEMPOTENT APPEND. The line takes the instance's NEXT sequence number, and that number is
         * unique per instance: another transition of the same instance (an escalation, a decision) that read the same latest
         * number and wrote first made this append fail — AFTER the task above was already cancelled, so the withdraw threw and
         * left no line in the log every other transition writes to (the replay finds the task cancelled and writes nothing).
         * A failed append is therefore told apart by what it collided with:
         *   • a line with this task's key exists — a twin replay wrote it first: recorded once, which is the point;
         *   • otherwise the sequence number was taken: read the latest again and append at the next one (bounded);
         *   • still failing after that — a store failure, and not hidden.
         */
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await logs.AppendAsync(new WorkflowTransitionLog
                {
                    TenantId = closed.TenantId,
                    WorkflowInstanceId = closed.Id,
                    ApprovalTaskId = stray.Id,
                    Action = WorkflowTransitionAction.Cancel,
                    FromState = waitingAs.ToString(),
                    ToState = ApprovalTaskStatus.Cancelled.ToString(),
                    FromStatus = closed.Status.ToString(),
                    ToStatus = closed.Status.ToString(),
                    ActorId = StartRecoveryActor,
                    ActorRef = StartRecoveryActor,
                    ReasonCode = reason,
                    IdempotencyKey = idempotencyKey,
                    CorrelationId = correlationId,
                    SequenceNo = await logs.GetLatestSequenceNoAsync(closed.Id, ct) + 1
                }, ct);
                return;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                if (await logs.FindByIdempotencyAsync(stray.Id, WorkflowTransitionAction.Cancel, idempotencyKey, ct) is not null)
                {
                    return;
                }

                if (attempt >= MaxStrayLogAttempts)
                {
                    throw;
                }
            }
        }
    }

    /// <summary>D2-FIX4 item 7 — how many times the stray-task withdraw's log line is appended while its sequence number keeps
    /// being taken by another transition of the instance.</summary>
    internal const int MaxStrayLogAttempts = 5;

    private static Response<TrustedWorkflowStartResult> NoCandidate(string reason, string correlationId) =>
        reason == WorkflowReasonCodes.WorkflowNoEligibleApprover
            ? Fail("Every candidate approver is the person starting the workflow; nobody else may approve it.", reason, correlationId)
            : Fail("At least one assignment candidate is required.", reason, correlationId);

    private static Response<TrustedWorkflowStartResult> Fail(string message, string reason, string correlationId) =>
        Response<TrustedWorkflowStartResult>.Fail(message, 400, reason, correlationId);

    private static Response<TrustedWorkflowStartResult> Conflict(string message, string reason, string correlationId) =>
        Response<TrustedWorkflowStartResult>.Fail(message, 409, reason, correlationId);

    // D2-FIX2 item 3 — transient: nothing is decided, the same key is safe to retry.
    private static Response<TrustedWorkflowStartResult> Unavailable(string message, string reason, string correlationId) =>
        Response<TrustedWorkflowStartResult>.Fail(message, 503, reason, correlationId);
}
