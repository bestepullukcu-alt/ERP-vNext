using Diten.Platform.Domain.Entities.Workflow;

namespace Diten.Platform.Domain.Repositories;

// MOD-0023 Batch 01 — create/read repository seams for the workflow aggregates. All reads are tenant
// scoped through the live TenantRepository<T> execution filter; cross-tenant reads return null/empty
// with no metadata leak. Mutation seams beyond create (publish, transitions) land in later batches.

public interface IWorkflowTemplateRepository
{
    Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default);
    Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default);
}

public interface IWorkflowTemplateVersionRepository
{
    Task<WorkflowTemplateVersion> CreateAsync(WorkflowTemplateVersion version, CancellationToken ct = default);
    Task<WorkflowTemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowTemplateVersion?> GetByIdForTemplateAsync(Guid templateId, Guid id, CancellationToken ct = default);
    Task<WorkflowTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default);
    Task<int> GetLatestVersionNumberAsync(Guid templateId, CancellationToken ct = default);
    Task<WorkflowTemplateVersion?> GetActivePublishedVersionAsync(Guid templateId, CancellationToken ct = default);
    Task<bool> ExistsVersionNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTemplateVersion>> ListByTemplateIdAsync(Guid templateId, CancellationToken ct = default);
    Task<WorkflowTemplateVersionUpdateResult> UpdateAsync(
        WorkflowTemplateVersion version,
        int expectedVersion,
        CancellationToken ct = default);
}

public enum WorkflowTemplateVersionUpdateResult
{
    Updated = 0,
    NotFoundOrConcurrencyConflict = 1,
    Immutable = 2
}

public interface IWorkflowInstanceRepository
{
    Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<WorkflowInstance?> GetLatestByObjectRefAsync(
        string objectRef,
        string objectType,
        string objectId,
        CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByIdAsync(
        IPlatformTransactionSession session,
        Guid id,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Transactional workflow instance reads are not implemented by this repository.");
    Task<bool> CancelTrustedAsync(
        IPlatformTransactionSession session,
        Guid instanceId,
        int expectedVersion,
        DateTimeOffset decisionAt,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Trusted workflow cancellation is not implemented by this repository.");
    async Task<(WorkflowInstance Instance, bool Created)> ReserveTrustedStartAsync(
        WorkflowInstance instance,
        CancellationToken ct = default) =>
        (await CreateAsync(instance, ct), true);
    Task<bool> AdvanceStartCheckpointAsync(
        Guid instanceId,
        int expectedVersion,
        Domain.Enums.Workflow.WorkflowStartCheckpoint expectedCheckpoint,
        Domain.Enums.Workflow.WorkflowStartCheckpoint nextCheckpoint,
        CancellationToken ct = default) =>
        Task.FromResult(false);
    Task<bool> UpdateEscalationOrTimeoutAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default) =>
        UpdateAsync(instance, expectedVersion, ct);

    /// <summary>
    /// BL-539 (CT decision) — the recovery's close of an INCOMPLETE trusted reservation: only while it is still Pending, still at
    /// <paramref name="expectedCheckpoint"/> and still at <paramref name="expectedVersion"/> (a replay that advanced it meanwhile
    /// wins; the close then writes nothing). Sets Cancelled with the reason, as the coordinator's own close does.
    /// </summary>
    async Task<bool> CloseIncompleteTrustedStartAsync(
        Guid instanceId,
        int expectedVersion,
        Domain.Enums.Workflow.WorkflowStartCheckpoint expectedCheckpoint,
        string reasonCode,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var instance = await GetByIdAsync(instanceId, ct);
        if (instance is null || instance.Version != expectedVersion || instance.StartCheckpoint != expectedCheckpoint
            || instance.Status != Domain.Enums.Workflow.WorkflowInstanceStatus.Pending || instance.TrustedConsumerClientId is null)
        {
            return false;
        }

        instance.Status = Domain.Enums.Workflow.WorkflowInstanceStatus.Cancelled;
        instance.StartClosedReasonCode = reasonCode;
        instance.CompletedAt = now;
        instance.LastTransitionAt = now;
        return await UpdateAsync(instance, expectedVersion, ct);
    }

    /// <summary>
    /// BL-549 — a generic start whose task, snapshot or start log was never written is completed by its key's replay. Before
    /// writing anything the replay CLAIMS the instance: only while it is still Active and at <paramref name="expectedVersion"/>,
    /// it stores the ids the missing rows will carry (so every replay writes the SAME rows — one task, never two) and
    /// refreshes <c>LastTransitionAt</c>. The version bump fences the abandoned-start sweep: a sweep that read the instance
    /// before the claim fails its own versioned close, and one that reads it after sees a fresh age and leaves it alone.
    /// </summary>
    async Task<bool> ClaimStartCompletionAsync(
        Guid instanceId,
        int expectedVersion,
        Guid approvalTaskId,
        Guid assignmentSnapshotId,
        Guid startTransitionLogId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var instance = await GetByIdAsync(instanceId, ct);
        if (instance is null || instance.Version != expectedVersion
            || instance.Status != Domain.Enums.Workflow.WorkflowInstanceStatus.Active)
        {
            return false;
        }

        instance.InitialApprovalTaskId = approvalTaskId;
        instance.InitialAssignmentSnapshotId = assignmentSnapshotId;
        instance.StartTransitionLogId = startTransitionLogId;
        instance.LastTransitionAt = now;
        return await UpdateAsync(instance, expectedVersion, ct);
    }

    /// <summary>
    /// FIX2 item 1 — the same claim inside the Platform transaction that also inserts the start task: the claim and the task
    /// commit together or not at all, so the abandoned-start sweep never sees a claimed instance without its task. The Mongo
    /// repository overrides this with a session write; the default only serves in-memory doubles, which have no session.
    /// </summary>
    Task<bool> ClaimStartCompletionAsync(
        IPlatformTransactionSession session,
        Guid instanceId,
        int expectedVersion,
        Guid approvalTaskId,
        Guid assignmentSnapshotId,
        Guid startTransitionLogId,
        DateTimeOffset now,
        CancellationToken ct = default) =>
        ClaimStartCompletionAsync(instanceId, expectedVersion, approvalTaskId, assignmentSnapshotId, startTransitionLogId, now, ct);

    // WP-CL-BE-3 — the terminal transition writes inside the Platform transaction that also carries the completion
    // outbox event. The Mongo repository overrides this with a session write; the default only serves in-memory
    // test doubles, which have no session to join.
    Task<bool> UpdateAsync(IPlatformTransactionSession session, WorkflowInstance instance, int expectedVersion,
        CancellationToken ct = default) =>
        UpdateAsync(instance, expectedVersion, ct);

    // WP-CL-BE-3 — batch status read for cross-service reconciliation (tenant scoped). Default = in-memory filter
    // for test doubles; the Mongo repository uses the {TenantId, ObjectType, ObjectId} index.
    async Task<IReadOnlyList<WorkflowInstance>> ListByObjectIdsAsync(
        string objectType,
        IReadOnlyCollection<string> objectIds,
        CancellationToken ct = default) =>
        (await GetAllForTenantAsync(ct))
            .Where(x => string.Equals(x.ObjectType, objectType, StringComparison.Ordinal) && objectIds.Contains(x.ObjectId))
            .ToList();

    // BL-484 — GetByIdAsync for several instances in ONE read (tenant scoped, non-deleted): an id that is not a live
    // instance of this tenant is simply absent. Default = in-memory filter for test doubles.
    async Task<IReadOnlyList<WorkflowInstance>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default) =>
        (await GetAllForTenantAsync(ct)).Where(x => ids.Contains(x.Id)).ToList();

    /// <summary>
    /// D2-FIX4 item 3 — the ids of this tenant's instances of ONE template that are still open (Pending, Active or
    /// Escalated): who an SLA rule change can still affect. The store reads them over the
    /// <c>{TenantId, WorkflowTemplateId}</c> index and projects the id only. Default = in-memory filter for test doubles.
    /// </summary>
    async Task<IReadOnlyList<Guid>> ListOpenIdsByTemplateAsync(Guid templateId, CancellationToken ct = default) =>
        (await GetAllForTenantAsync(ct))
            .Where(x => (x.WorkflowTemplateId == templateId || x.TemplateId == templateId)
                && x.Status is Domain.Enums.Workflow.WorkflowInstanceStatus.Pending
                    or Domain.Enums.Workflow.WorkflowInstanceStatus.Active
                    or Domain.Enums.Workflow.WorkflowInstanceStatus.Escalated)
            .Select(x => x.Id)
            .ToList();
}

public interface IApprovalTaskRepository
{
    Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default);
    Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);
    Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);

    /// <summary>
    /// BL-484 — <see cref="GetActiveByInstanceIdAsync"/> for several instances in ONE read: every open task of these
    /// instances, NEWEST FIRST (ties by id, the same order the single read uses), so the first one listed for an instance
    /// is the one <see cref="GetActiveByInstanceIdAsync"/> answers. The default asks one instance at a time (what an
    /// in-memory double needs); the store overrides it.
    /// </summary>
    async Task<IReadOnlyList<ApprovalTask>> ListActiveByInstanceIdsAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct = default)
    {
        var active = new List<ApprovalTask>();
        foreach (var instanceId in workflowInstanceIds.Distinct())
        {
            if (await GetActiveByInstanceIdAsync(instanceId, ct) is { } task)
            {
                active.Add(task);
            }
        }

        return active;
    }

    Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);

    /// <summary>D1-FIX1 — <see cref="ListByInstanceIdAsync"/> for several instances in ONE read: every task of these
    /// instances, whatever its status, newest first (ties by id). The default asks one instance at a time (what an
    /// in-memory double needs); the store overrides it.</summary>
    async Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdsAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct = default)
    {
        var tasks = new List<ApprovalTask>();
        foreach (var instanceId in workflowInstanceIds.Distinct())
        {
            tasks.AddRange(await ListByInstanceIdAsync(instanceId, ct));
        }

        return tasks.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).ToList();
    }

    Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalTask>> ListOverdueTasksAsync(
        DateTimeOffset nowUtc,
        int maxItems,
        CancellationToken ct = default) =>
        GetAllForTenantAsync(ct);
    Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default);
    Task<ApprovalTask?> GetByIdAsync(
        IPlatformTransactionSession session,
        Guid id,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Transactional approval task reads are not implemented by this repository.");
    Task<bool> CancelTrustedAsync(
        IPlatformTransactionSession session,
        Guid taskId,
        int expectedVersion,
        Guid actorUserId,
        string reasonCode,
        DateTimeOffset decisionAt,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Trusted approval task cancellation is not implemented by this repository.");
    Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default) =>
        CreateAsync(task, ct);
    Task<bool> UpdateEscalationAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
        UpdateAsync(task, expectedVersion, ct);

    /// <summary>
    /// D2-FIX2 item 4 — the SLA sweep's next look at a task it could not act on (and, the first time nobody was eligible,
    /// the "parked" reason that keeps it from warning again). Touches only a task still waiting on somebody, changes
    /// nothing else, and takes NO version: a person deciding the task meanwhile must never be told "changed meanwhile"
    /// because a sweep rested it. False when no such task is there any more. The store overrides this with a targeted
    /// write; the default (in-memory doubles) reads and writes the task.
    /// </summary>
    async Task<bool> ScheduleEscalationLookAsync(
        Guid taskId,
        DateTime nextLookAtUtc,
        string? parkReasonCode,
        CancellationToken ct = default)
    {
        var task = await GetByIdAsync(taskId, ct);
        if (task is null || task.Status is not (Domain.Enums.Workflow.ApprovalTaskStatus.WaitingApproval
                or Domain.Enums.Workflow.ApprovalTaskStatus.WaitingEvidence
                or Domain.Enums.Workflow.ApprovalTaskStatus.Escalated))
        {
            return false;
        }

        task.EscalationNextLookAtUtc = nextLookAtUtc;
        if (parkReasonCode is not null)
        {
            task.LastEscalationReasonCode = parkReasonCode;
        }

        return await UpdateAsync(task, task.Version, ct);
    }

    /// <summary>
    /// D2-FIX4 item 3 — forgets the look time of every task still waiting on somebody under these instances, so the next sweep
    /// reads them again. A task rests until its look time (a day for what nothing will change), and a task that rested
    /// because it had NO SLA rule keeps resting after a rule is added — or tightened — for the template. Targeted: only the look
    /// time, only open tasks that have one, only this tenant's, no version (a person deciding the task meanwhile is never
    /// told "changed meanwhile" because a rule was edited). Returns how many were reset. The store overrides this with one
    /// targeted write per chunk of ids; the default (in-memory doubles) reads and writes each task.
    /// </summary>
    async Task<int> ResetEscalationLooksAsync(IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct = default)
    {
        if (workflowInstanceIds.Count == 0)
        {
            return 0;
        }

        var reset = 0;
        foreach (var task in (await GetAllForTenantAsync(ct)).Where(t =>
                     workflowInstanceIds.Contains(t.WorkflowInstanceId)
                     && t.EscalationNextLookAtUtc is not null
                     && (t.Status is Domain.Enums.Workflow.ApprovalTaskStatus.WaitingApproval
                         or Domain.Enums.Workflow.ApprovalTaskStatus.WaitingEvidence
                         or Domain.Enums.Workflow.ApprovalTaskStatus.Escalated)))
        {
            task.EscalationNextLookAtUtc = null;
            if (await UpdateAsync(task, task.Version, ct))
            {
                reset++;
            }
        }

        return reset;
    }

    // WP-CL-BE-3 — terminal transition write inside the Platform transaction (see IWorkflowInstanceRepository).
    Task<bool> UpdateAsync(IPlatformTransactionSession session, ApprovalTask task, int expectedVersion,
        CancellationToken ct = default) =>
        UpdateAsync(task, expectedVersion, ct);

    // D1 — the NEXT step's task, created inside the transaction that closes the step before it. Default = the plain
    // create (in-memory doubles have no session).
    Task<ApprovalTask> CreateAsync(IPlatformTransactionSession session, ApprovalTask task,
        CancellationToken ct = default) =>
        CreateAsync(task, ct);
}

public interface IRuntimeAssignmentSnapshotRepository
{
    Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default);
    Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);

    /// <summary>D1-FIX1 — <see cref="GetByIdAsync"/> for several snapshots in ONE read; an id that is not this tenant's
    /// snapshot is simply absent. The default asks one at a time (in-memory doubles); the store overrides it.</summary>
    async Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var snapshots = new List<RuntimeAssignmentSnapshot>();
        foreach (var id in ids.Distinct())
        {
            if (await GetByIdAsync(id, ct) is { } snapshot)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    Task<RuntimeAssignmentSnapshot> EnsureTrustedStartSnapshotAsync(
        RuntimeAssignmentSnapshot snapshot,
        CancellationToken ct = default) =>
        CreateAsync(snapshot, ct);

    // D1 — the next step's assignment snapshot, inside the same transaction as its task (see IApprovalTaskRepository).
    Task<RuntimeAssignmentSnapshot> CreateAsync(IPlatformTransactionSession session, RuntimeAssignmentSnapshot snapshot,
        CancellationToken ct = default) =>
        CreateAsync(snapshot, ct);
}

public interface IWorkflowTransitionLogRepository
{
    Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default);
    Task<WorkflowTransitionLog> AppendAsync(WorkflowTransitionLog log, CancellationToken ct = default) =>
        CreateAsync(log, ct);

    // WP-CL-BE-3 — terminal transition log inside the Platform transaction (see IWorkflowInstanceRepository).
    Task<WorkflowTransitionLog> CreateAsync(IPlatformTransactionSession session, WorkflowTransitionLog log,
        CancellationToken ct = default) =>
        CreateAsync(log, ct);
    Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default);

    // MOD-0022 E4 — single use of an approval signature: does any transition of this tenant already carry it? Fails closed
    // in an implementation that does not answer (no silent "false").
    Task<bool> IsSignatureBoundAsync(string signatureId, CancellationToken ct = default) =>
        throw new NotSupportedException("This transition log store cannot answer the e-signature single-use check.");

    Task<WorkflowTransitionLog?> FindByIdempotencyAsync(
        Guid approvalTaskId,
        Domain.Enums.Workflow.WorkflowTransitionAction action,
        string idempotencyKey,
        CancellationToken ct = default) =>
        GetByTaskActionIdempotencyKeyAsync(approvalTaskId, action, idempotencyKey, ct);
    Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
        Guid approvalTaskId,
        Domain.Enums.Workflow.WorkflowTransitionAction action,
        string idempotencyKey,
        CancellationToken ct = default);
    Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);
    Task<long> GetLatestSequenceNoAsync(
        IPlatformTransactionSession session,
        Guid workflowInstanceId,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Transactional transition sequence reads are not implemented by this repository.");
    Task<WorkflowTransitionLog> AppendAsync(
        IPlatformTransactionSession session,
        WorkflowTransitionLog log,
        CancellationToken ct = default) =>
        throw new NotSupportedException("Transactional transition append is not implemented by this repository.");
    Task<WorkflowTransitionLog> EnsureTrustedStartLogAsync(
        WorkflowTransitionLog log,
        CancellationToken ct = default) =>
        CreateAsync(log, ct);
}

public interface ISlaEscalationRuleRepository
{
    Task<SlaEscalationRule> CreateAsync(SlaEscalationRule rule, CancellationToken ct = default);
    Task<SlaEscalationRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SlaEscalationRule>> ListActiveByTemplateIdAsync(Guid templateId, CancellationToken ct = default);
    Task<IReadOnlyList<SlaEscalationRule>> ListActiveAsync(CancellationToken ct = default);
    Task<SlaEscalationRule?> FindForStepAsync(
        Guid templateId,
        string stageCode,
        string stepCode,
        CancellationToken ct = default);
    Task DeactivateRulesForTemplateAsync(Guid templateId, CancellationToken ct = default);
}
