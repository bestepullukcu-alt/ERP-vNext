using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0023 Batch 01 — tenant-scoped repository implementations over the live TenantRepository<T> base.
// CreateAsync / GetByIdAsync are inherited (the base sets TenantId from context on create and applies
// the tenant + IsDeleted execution filter on read). Collections use snake_case, area-prefixed names.

public sealed class WorkflowTemplateRepository : TenantRepository<WorkflowTemplate>, IWorkflowTemplateRepository
{
    public WorkflowTemplateRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.WorkflowTemplates)
    {
    }

    public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplate>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplate>.Filter.Eq(x => x.TemplateCode, templateCode));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default)
    {
        return await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default)
    {
        template.Version = expectedVersion + 1;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<WorkflowTemplate>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplate>.Filter.Eq(x => x.Id, template.Id),
            Builders<WorkflowTemplate>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, template, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}

public sealed class WorkflowTemplateVersionRepository
    : TenantRepository<WorkflowTemplateVersion>, IWorkflowTemplateVersionRepository
{
    public WorkflowTemplateVersionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.WorkflowTemplateVersions)
    {
    }

    public Task<WorkflowTemplateVersion?> GetByIdForTemplateAsync(
        Guid templateId,
        Guid id,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Id, id));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public Task<WorkflowTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId));
        return Collection.Find(filter).SortByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct)!;
    }

    public async Task<int> GetLatestVersionNumberAsync(Guid templateId, CancellationToken ct = default)
    {
        var latest = await GetLatestVersionAsync(templateId, ct);
        return latest?.VersionNumber ?? 0;
    }

    public Task<WorkflowTemplateVersion?> GetActivePublishedVersionAsync(Guid templateId, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Status, Domain.Enums.Workflow.WorkflowTemplateVersionStatus.Published),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.IsImmutable, true));
        return Collection.Find(filter).SortByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct)!;
    }

    public async Task<bool> ExistsVersionNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.VersionNumber, versionNumber));
        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowTemplateVersion>> ListByTemplateIdAsync(
        Guid templateId,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId));
        return await Collection.Find(filter).SortByDescending(x => x.VersionNumber).ToListAsync(ct);
    }

    public async Task<WorkflowTemplateVersionUpdateResult> UpdateAsync(
        WorkflowTemplateVersion version,
        int expectedVersion,
        CancellationToken ct = default)
    {
        var immutableFilter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Id, version.Id),
            Builders<WorkflowTemplateVersion>.Filter.Or(
                Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.IsImmutable, true),
                Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Status, Domain.Enums.Workflow.WorkflowTemplateVersionStatus.Published)));
        if (await Collection.Find(immutableFilter).AnyAsync(ct))
        {
            return WorkflowTemplateVersionUpdateResult.Immutable;
        }

        version.Version = expectedVersion + 1;
        version.UpdatedAt = DateTimeOffset.UtcNow;
        version.ConcurrencyToken = Guid.NewGuid().ToString("N");
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Id, version.Id),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, version, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1
            ? WorkflowTemplateVersionUpdateResult.Updated
            : WorkflowTemplateVersionUpdateResult.NotFoundOrConcurrencyConflict;
    }
}

public sealed class WorkflowInstanceRepository : TenantRepository<WorkflowInstance>, IWorkflowInstanceRepository
{
    private readonly IPlatformDbContext _dbContext;

    public WorkflowInstanceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.WorkflowInstances)
    {
        _dbContext = dbContext;
    }

    // WP-CL-BE-3 — the terminal write joins the Platform transaction that also carries the completion outbox event.
    public async Task<bool> UpdateAsync(
        IPlatformTransactionSession session,
        WorkflowInstance instance,
        int expectedVersion,
        CancellationToken ct = default)
    {
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        instance.Version = expectedVersion + 1;
        instance.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.Id, instance.Id),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(handle, filter, instance, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    // WP-CL-BE-3 — batch status read over the {TenantId, ObjectType, ObjectId} index. Ordering is done by the caller in
    // memory (DateTimeOffset is a BSON array here — see GetLatestByObjectRefAsync).
    public async Task<IReadOnlyList<WorkflowInstance>> ListByObjectIdsAsync(
        string objectType,
        IReadOnlyCollection<string> objectIds,
        CancellationToken ct = default)
    {
        if (objectIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.ObjectType, objectType),
            Builders<WorkflowInstance>.Filter.In(x => x.ObjectId, objectIds));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public Task<WorkflowInstance?> GetByIdAsync(
        IPlatformTransactionSession session,
        Guid id,
        CancellationToken ct = default) =>
        Collection.Find(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            Builders<WorkflowInstance>.Filter.And(
                ExecutionFilter,
                Builders<WorkflowInstance>.Filter.Eq(x => x.Id, id)))
            .FirstOrDefaultAsync(ct)!;

    public async Task<bool> CancelTrustedAsync(
        IPlatformTransactionSession session,
        Guid instanceId,
        int expectedVersion,
        DateTimeOffset decisionAt,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.Id, instanceId),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Version, expectedVersion),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Status, WorkflowInstanceStatus.Active));
        var update = Builders<WorkflowInstance>.Update
            .Set(x => x.Status, WorkflowInstanceStatus.Cancelled)
            .Set(x => x.CompletedAt, decisionAt)
            .Set(x => x.LastTransitionAt, decisionAt)
            .Set(x => x.UpdatedAt, decisionAt)
            .Inc(x => x.Version, 1);
        var result = await Collection.UpdateOneAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            filter,
            update,
            cancellationToken: ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    // BL-484 — the same rule as GetByIdAsync (tenant + IsDeleted=false + id), for several ids in one read.
    public async Task<IReadOnlyList<WorkflowInstance>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.In(x => x.Id, ids));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    // D2-FIX4 item 3 — over ix_workflow_instances_tenant_template ({TenantId, WorkflowTemplateId}); the id only. Every
    // creator of an instance writes TemplateId and WorkflowTemplateId alike, and the second is the indexed one.
    public async Task<IReadOnlyList<Guid>> ListOpenIdsByTemplateAsync(Guid templateId, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.WorkflowTemplateId, templateId),
            Builders<WorkflowInstance>.Filter.In(
                x => x.Status,
                [WorkflowInstanceStatus.Pending, WorkflowInstanceStatus.Active, WorkflowInstanceStatus.Escalated]));
        return await Collection.Find(filter).Project(x => x.Id).ToListAsync(ct);
    }

    public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<(WorkflowInstance Instance, bool Created)> ReserveTrustedStartAsync(
        WorkflowInstance instance,
        CancellationToken ct = default)
    {
        try
        {
            return (await CreateAsync(instance, ct), true);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await GetByIdempotencyKeyAsync(instance.IdempotencyKey!, ct);
            if (existing is null)
            {
                throw;
            }

            return (existing, false);
        }
    }

    public async Task<bool> AdvanceStartCheckpointAsync(
        Guid instanceId,
        int expectedVersion,
        WorkflowStartCheckpoint expectedCheckpoint,
        WorkflowStartCheckpoint nextCheckpoint,
        CancellationToken ct = default)
    {
        if ((int)nextCheckpoint != (int)expectedCheckpoint + 1)
        {
            return false;
        }

        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.Id, instanceId),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Version, expectedVersion),
            Builders<WorkflowInstance>.Filter.Eq(x => x.StartCheckpoint, expectedCheckpoint),
            // BL-539 (CT decision, rule 4) — a reservation is completed only while it is still Pending: one the recovery
            // closed (Cancelled) is never brought back by a late replay.
            Builders<WorkflowInstance>.Filter.Eq(x => x.Status, WorkflowInstanceStatus.Pending));
        var update = Builders<WorkflowInstance>.Update
            .Set(x => x.StartCheckpoint, nextCheckpoint)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)
            .Inc(x => x.Version, 1);
        if (nextCheckpoint == WorkflowStartCheckpoint.Completed)
        {
            update = update
                .Set(x => x.Status, WorkflowInstanceStatus.Active)
                .Set(x => x.LastTransitionAt, DateTimeOffset.UtcNow);
        }
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    // "Latest" is picked in memory, NOT by a server-side sort — and it must stay that way until BL-030 lands.
    // No DateTimeOffsetSerializer is registered (see Diten.Platform.Infrastructure/DependencyInjection.cs), so the
    // driver persists every DateTimeOffset as a BSON array [ticks, offsetMinutes]. Sorting on TWO such fields makes
    // MongoDB fail the query outright with "cannot sort with keys that are parallel arrays" — that is how this
    // lookup, and with it the whole MOD-0023 transition gate, was dead against a real database. Registering the
    // serializer globally is the real cure but it changes the on-disk representation for every service and needs a
    // data migration; it is tracked as BL-030. Until then, do NOT put .SortByDescending(...).ThenByDescending(...)
    // back here. Same treatment as DocumentAccessPolicyRepository.ListAsync.
    // The filter already pins tenant + objectRef + objectType + objectId, so the candidate set is one business
    // object's instance history — small enough to order client-side.
    // Ordering intent is unchanged (StartedAt desc, then CreatedAt desc), with nulls made explicit: an instance
    // with no StartedAt sorts LAST, behind every started one, and ties break on CreatedAt descending. That is the
    // same placement MongoDB would have given null in a descending sort.
    public async Task<WorkflowInstance?> GetLatestByObjectRefAsync(
        string objectRef,
        string objectType,
        string objectId,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.ObjectRef, objectRef),
            Builders<WorkflowInstance>.Filter.Eq(x => x.ObjectType, objectType),
            Builders<WorkflowInstance>.Filter.Eq(x => x.ObjectId, objectId));

        var candidates = await Collection.Find(filter).ToListAsync(ct);
        return candidates
            .OrderByDescending(x => x.StartedAt.HasValue)
            .ThenByDescending(x => x.StartedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default)
    {
        return await Collection.Find(ExecutionFilter).SortByDescending(x => x.StartedAt).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
    {
        instance.Version = expectedVersion + 1;
        instance.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.Id, instance.Id),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, instance, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public Task<bool> UpdateEscalationOrTimeoutAsync(
        WorkflowInstance instance,
        int expectedVersion,
        CancellationToken ct = default) =>
        UpdateAsync(instance, expectedVersion, ct);

    // BL-539 — see IWorkflowInstanceRepository.CloseIncompleteTrustedStartAsync: targeted, fenced on version, checkpoint and Pending.
    public async Task<bool> CloseIncompleteTrustedStartAsync(
        Guid instanceId,
        int expectedVersion,
        WorkflowStartCheckpoint expectedCheckpoint,
        string reasonCode,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.Id, instanceId),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Version, expectedVersion),
            Builders<WorkflowInstance>.Filter.Eq(x => x.StartCheckpoint, expectedCheckpoint),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Status, WorkflowInstanceStatus.Pending),
            Builders<WorkflowInstance>.Filter.Ne(x => x.TrustedConsumerClientId, null));
        var update = Builders<WorkflowInstance>.Update
            .Set(x => x.Status, WorkflowInstanceStatus.Cancelled)
            .Set(x => x.StartClosedReasonCode, reasonCode)
            .Set(x => x.CompletedAt, now)
            .Set(x => x.LastTransitionAt, now)
            .Set(x => x.UpdatedAt, now)
            .Inc(x => x.Version, 1);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    // BL-549 — see IWorkflowInstanceRepository.ClaimStartCompletionAsync: targeted, versioned, only while Active.
    public async Task<bool> ClaimStartCompletionAsync(
        Guid instanceId,
        int expectedVersion,
        Guid approvalTaskId,
        Guid assignmentSnapshotId,
        Guid startTransitionLogId,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.Id, instanceId),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Version, expectedVersion),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Status, WorkflowInstanceStatus.Active));
        var update = Builders<WorkflowInstance>.Update
            .Set(x => x.InitialApprovalTaskId, approvalTaskId)
            .Set(x => x.InitialAssignmentSnapshotId, assignmentSnapshotId)
            .Set(x => x.StartTransitionLogId, startTransitionLogId)
            .Set(x => x.LastTransitionAt, now)
            .Set(x => x.UpdatedAt, now)
            .Inc(x => x.Version, 1);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}

public sealed class ApprovalTaskRepository : TenantRepository<ApprovalTask>, IApprovalTaskRepository
{
    private readonly IPlatformDbContext _dbContext;

    public ApprovalTaskRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.ApprovalTasks)
    {
        _dbContext = dbContext;
    }

    // D1 — the next step's task joins the transaction that closes the previous step.
    public async Task<ApprovalTask> CreateAsync(
        IPlatformTransactionSession session,
        ApprovalTask task,
        CancellationToken ct = default)
    {
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        typeof(ApprovalTask).GetProperty(nameof(ApprovalTask.TenantId))!.SetValue(task, TenantContext.TenantId);
        await Collection.InsertOneAsync(handle, task, cancellationToken: ct);
        return task;
    }

    // WP-CL-BE-3 — terminal task write inside the Platform transaction.
    public async Task<bool> UpdateAsync(
        IPlatformTransactionSession session,
        ApprovalTask task,
        int expectedVersion,
        CancellationToken ct = default)
    {
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        task.Version = expectedVersion + 1;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.Id, task.Id),
            Builders<ApprovalTask>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(handle, filter, task, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public Task<ApprovalTask?> GetByIdAsync(
        IPlatformTransactionSession session,
        Guid id,
        CancellationToken ct = default) =>
        Collection.Find(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            Builders<ApprovalTask>.Filter.And(
                ExecutionFilter,
                Builders<ApprovalTask>.Filter.Eq(x => x.Id, id)))
            .FirstOrDefaultAsync(ct)!;

    public async Task<bool> CancelTrustedAsync(
        IPlatformTransactionSession session,
        Guid taskId,
        int expectedVersion,
        Guid actorUserId,
        string reasonCode,
        DateTimeOffset decisionAt,
        CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.Id, taskId),
            Builders<ApprovalTask>.Filter.Eq(x => x.Version, expectedVersion),
            Builders<ApprovalTask>.Filter.In(
                x => x.Status,
                [ApprovalTaskStatus.WaitingApproval, ApprovalTaskStatus.WaitingEvidence]));
        var update = Builders<ApprovalTask>.Update
            .Set(x => x.Status, ApprovalTaskStatus.Cancelled)
            .Set(x => x.CompletedAt, decisionAt)
            .Set(x => x.ActionedBy, actorUserId.ToString("D"))
            .Set(x => x.ActionReasonCode, reasonCode)
            .Set(x => x.UpdatedAt, decisionAt)
            .Inc(x => x.Version, 1);
        var result = await Collection.UpdateOneAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            filter,
            update,
            cancellationToken: ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId));
        return Collection.Find(filter).SortBy(x => x.CreatedAt).FirstOrDefaultAsync(ct)!;
    }

    public async Task<ApprovalTask> EnsureTrustedStartTaskAsync(ApprovalTask task, CancellationToken ct = default)
    {
        try
        {
            return await CreateAsync(task, ct);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await GetByIdAsync(task.Id, ct) ??
                throw new InvalidOperationException("Duplicate workflow task could not be re-read in tenant scope.");
        }
    }

    public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId),
            ActiveStatuses);
        // Newest first; two open tasks created in the same instant are told apart by id (BL-484 L2) — the batched read
        // below uses the same order, so both name the same task.
        return Collection.Find(filter).SortByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<ApprovalTask>> ListActiveByInstanceIdsAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct = default)
    {
        if (workflowInstanceIds.Count == 0)
        {
            return [];
        }

        // The same filter and the same order as GetActiveByInstanceIdAsync (newest first, then by id), over all the
        // instances at once: the first task listed for an instance is the one the single read answers.
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.In(x => x.WorkflowInstanceId, workflowInstanceIds),
            ActiveStatuses);
        return await Collection.Find(filter).SortByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
    }

    /// <summary>An approval task still waiting on someone — what "active" means for a single and a batched read.</summary>
    private static FilterDefinition<ApprovalTask> ActiveStatuses
        => Builders<ApprovalTask>.Filter.In(
            x => x.Status,
            [ApprovalTaskStatus.WaitingApproval, ApprovalTaskStatus.WaitingEvidence]);

    public async Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId));
        return await Collection.Find(filter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdsAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct = default)
    {
        if (workflowInstanceIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.In(x => x.WorkflowInstanceId, workflowInstanceIds));
        return await Collection.Find(filter).SortByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default)
    {
        return await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApprovalTask>> ListOverdueTasksAsync(
        DateTimeOffset nowUtc,
        int maxItems,
        CancellationToken ct = default)
    {
        /*
         * What the sweep reads.
         *   • TimedOut is NOT read (BL-502 item 7): it is terminal; a sweep can only skip it, and it held a place.
         *   • D2-FIX2 item 4 — only tasks whose LOOK TIME has come: never looked at (null), or rested until now or earlier.
         *     Every task a sweep cannot act on is given its next look time (RunWorkflowEscalationsHandler), so a standing
         *     backlog it cannot act on — no SLA rule, nobody eligible, already escalated with no timeout — rests instead
         *     of filling the first page forever, and a parked task is looked at again (a seat may have changed hands).
         *     This replaces D2-FIX1's "parked tasks in a second read", which still starved them: the unparked skips were
         *     never parked.
         *   • Order: the look time first (never-looked — null — sorts first), then the oldest due date, then the id.
         *     DueAt is stored as [ticks, offsetMinutes] (BL-030), and an ASCENDING sort on an array uses its SMALLEST
         *     element — the offset — so the sort reads the ticks element itself (DueAt.0).
         *   • Served by ix_approval_tasks_tenant_status_next_look: scalar fields only (TenantId, Status, the look time,
         *     _id). The older ix_approval_tasks_tenant_status_due is multikey (DueAt is an array) and serves neither the
         *     look-time filter nor DueAt.0; BL-030 forbids putting a second array field beside it anyway.
         */
        var limit = Math.Max(1, maxItems);
        var due = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.In(
                x => x.Status,
                [
                    ApprovalTaskStatus.WaitingApproval,
                    ApprovalTaskStatus.WaitingEvidence,
                    ApprovalTaskStatus.Escalated
                ]),
            Builders<ApprovalTask>.Filter.Ne(x => x.DueAt, null),
            Builders<ApprovalTask>.Filter.Lte(x => x.DueAt, nowUtc),
            Builders<ApprovalTask>.Filter.Or(
                Builders<ApprovalTask>.Filter.Eq(x => x.EscalationNextLookAtUtc, null),
                Builders<ApprovalTask>.Filter.Lte(x => x.EscalationNextLookAtUtc, nowUtc.UtcDateTime)));
        var order = Builders<ApprovalTask>.Sort
            .Ascending(x => x.EscalationNextLookAtUtc)
            .Ascending(DueAtTicksPath)
            .Ascending(x => x.Id);

        return await Collection.Find(due).Sort(order).Limit(limit).ToListAsync(ct);
    }

    // D2-FIX2 item 4 — targeted: only the look time (and the parked reason), only on a task still waiting on somebody,
    // and no version — see IApprovalTaskRepository.ScheduleEscalationLookAsync.
    public async Task<bool> ScheduleEscalationLookAsync(
        Guid taskId,
        DateTime nextLookAtUtc,
        string? parkReasonCode,
        CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.Id, taskId),
            Builders<ApprovalTask>.Filter.In(
                x => x.Status,
                [
                    ApprovalTaskStatus.WaitingApproval,
                    ApprovalTaskStatus.WaitingEvidence,
                    ApprovalTaskStatus.Escalated
                ]));
        var update = Builders<ApprovalTask>.Update.Set(
            x => x.EscalationNextLookAtUtc, DateTime.SpecifyKind(nextLookAtUtc, DateTimeKind.Utc));
        if (parkReasonCode is not null)
        {
            update = update.Set(x => x.LastEscalationReasonCode, parkReasonCode);
        }

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }

    // D2-FIX4 item 3 — targeted, tenant-filtered: only the look time, only tasks still waiting on somebody that HAVE one,
    // in chunks of ids (served by ix_approval_tasks_tenant_instance_status), and no version.
    public async Task<int> ResetEscalationLooksAsync(IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct = default)
    {
        var reset = 0;
        foreach (var chunk in workflowInstanceIds.Distinct().Chunk(500))
        {
            var filter = Builders<ApprovalTask>.Filter.And(
                ExecutionFilter,
                Builders<ApprovalTask>.Filter.In(x => x.WorkflowInstanceId, chunk),
                Builders<ApprovalTask>.Filter.In(
                    x => x.Status,
                    [
                        ApprovalTaskStatus.WaitingApproval,
                        ApprovalTaskStatus.WaitingEvidence,
                        ApprovalTaskStatus.Escalated
                    ]),
                Builders<ApprovalTask>.Filter.Ne(x => x.EscalationNextLookAtUtc, null));
            var update = Builders<ApprovalTask>.Update.Set(x => x.EscalationNextLookAtUtc, (DateTime?)null);
            var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
            reset += (int)result.ModifiedCount;
        }

        return reset;
    }

    public async Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default)
    {
        task.Version = expectedVersion + 1;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.Id, task.Id),
            Builders<ApprovalTask>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, task, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public Task<bool> UpdateEscalationAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
        UpdateAsync(task, expectedVersion, ct);

    /// <summary>The ticks element of the stored [ticks, offsetMinutes] array (BL-030) — see ListOverdueTasksAsync.</summary>
    internal const string DueAtTicksPath = "DueAt.0";
}

public sealed class RuntimeAssignmentSnapshotRepository
    : TenantRepository<RuntimeAssignmentSnapshot>, IRuntimeAssignmentSnapshotRepository
{
    private readonly IPlatformDbContext _dbContext;

    public RuntimeAssignmentSnapshotRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.WorkflowRuntimeAssignmentSnapshots)
    {
        _dbContext = dbContext;
    }

    // D1 — the next step's assignment snapshot joins the transaction that closes the previous step.
    public async Task<RuntimeAssignmentSnapshot> CreateAsync(
        IPlatformTransactionSession session,
        RuntimeAssignmentSnapshot snapshot,
        CancellationToken ct = default)
    {
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        typeof(RuntimeAssignmentSnapshot).GetProperty(nameof(RuntimeAssignmentSnapshot.TenantId))!
            .SetValue(snapshot, TenantContext.TenantId);
        await Collection.InsertOneAsync(handle, snapshot, cancellationToken: ct);
        return snapshot;
    }

    public async Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var filter = Builders<RuntimeAssignmentSnapshot>.Filter.And(
            ExecutionFilter,
            Builders<RuntimeAssignmentSnapshot>.Filter.In(x => x.Id, ids));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(
        Guid workflowInstanceId,
        CancellationToken ct = default)
    {
        var filter = Builders<RuntimeAssignmentSnapshot>.Filter.And(
            ExecutionFilter,
            Builders<RuntimeAssignmentSnapshot>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId));
        return await Collection.Find(filter).SortByDescending(x => x.ResolvedAt).ToListAsync(ct);
    }

    public async Task<RuntimeAssignmentSnapshot> EnsureTrustedStartSnapshotAsync(
        RuntimeAssignmentSnapshot snapshot,
        CancellationToken ct = default)
    {
        try
        {
            return await CreateAsync(snapshot, ct);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await GetByIdAsync(snapshot.Id, ct) ??
                throw new InvalidOperationException("Duplicate workflow assignment snapshot could not be re-read in tenant scope.");
        }
    }
}

public sealed class WorkflowTransitionLogRepository : TenantRepository<WorkflowTransitionLog>, IWorkflowTransitionLogRepository
{
    private readonly IPlatformDbContext _dbContext;

    public WorkflowTransitionLogRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.WorkflowTransitionLogs)
    {
        _dbContext = dbContext;
    }

    // WP-CL-BE-3 — terminal transition log inside the Platform transaction. Same tenant rule as the base CreateAsync:
    // the tenant always comes from the context, never from the caller.
    public async Task<WorkflowTransitionLog> CreateAsync(
        IPlatformTransactionSession session,
        WorkflowTransitionLog log,
        CancellationToken ct = default)
    {
        var handle = PlatformMongoTransactionSession.Require(session, _dbContext);
        typeof(WorkflowTransitionLog).GetProperty(nameof(WorkflowTransitionLog.TenantId))!
            .SetValue(log, TenantContext.TenantId);
        await Collection.InsertOneAsync(handle, log, cancellationToken: ct);
        return log;
    }

    public async Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(
        Guid workflowInstanceId,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTransitionLog>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTransitionLog>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId));
        return await Collection.Find(filter).SortBy(x => x.SequenceNo).ToListAsync(ct);
    }

    public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
        Guid approvalTaskId,
        Domain.Enums.Workflow.WorkflowTransitionAction action,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTransitionLog>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTransitionLog>.Filter.Eq(x => x.ApprovalTaskId, approvalTaskId),
            Builders<WorkflowTransitionLog>.Filter.Eq(x => x.Action, action),
            Builders<WorkflowTransitionLog>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public Task<WorkflowTransitionLog?> FindByIdempotencyAsync(
        Guid approvalTaskId,
        Domain.Enums.Workflow.WorkflowTransitionAction action,
        string idempotencyKey,
        CancellationToken ct = default) =>
        GetByTaskActionIdempotencyKeyAsync(approvalTaskId, action, idempotencyKey, ct);

    public Task<WorkflowTransitionLog> AppendAsync(WorkflowTransitionLog log, CancellationToken ct = default) =>
        CreateAsync(log, ct);

    public async Task<WorkflowTransitionLog> AppendAsync(
        IPlatformTransactionSession session,
        WorkflowTransitionLog log,
        CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(
            PlatformMongoTransactionSession.Require(session, _dbContext),
            log,
            cancellationToken: ct);
        return log;
    }

    public async Task<WorkflowTransitionLog> EnsureTrustedStartLogAsync(
        WorkflowTransitionLog log,
        CancellationToken ct = default)
    {
        try
        {
            return await CreateAsync(log, ct);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return await GetByIdAsync(log.Id, ct) ??
                throw new InvalidOperationException("Duplicate workflow start log could not be re-read in tenant scope.");
        }
    }

    public async Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var latest = await Collection
            .Find(Builders<WorkflowTransitionLog>.Filter.And(
                ExecutionFilter,
                Builders<WorkflowTransitionLog>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId)))
            .SortByDescending(x => x.SequenceNo)
            .FirstOrDefaultAsync(ct);
        return latest?.SequenceNo ?? 0;
    }

    public async Task<long> GetLatestSequenceNoAsync(
        IPlatformTransactionSession session,
        Guid workflowInstanceId,
        CancellationToken ct = default)
    {
        var latest = await Collection
            .Find(
                PlatformMongoTransactionSession.Require(session, _dbContext),
                Builders<WorkflowTransitionLog>.Filter.And(
                    ExecutionFilter,
                    Builders<WorkflowTransitionLog>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId)))
            .SortByDescending(x => x.SequenceNo)
            .FirstOrDefaultAsync(ct);
        return latest?.SequenceNo ?? 0;
    }
}

public sealed class SlaEscalationRuleRepository : TenantRepository<SlaEscalationRule>, ISlaEscalationRuleRepository
{
    public SlaEscalationRuleRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.WorkflowSlaRules)
    {
    }

    public async Task<IReadOnlyList<SlaEscalationRule>> ListActiveByTemplateIdAsync(
        Guid templateId,
        CancellationToken ct = default)
    {
        var filter = Builders<SlaEscalationRule>.Filter.And(
            ExecutionFilter,
            Builders<SlaEscalationRule>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.IsActive, true));
        return await Collection.Find(filter).SortBy(x => x.StageCode).ThenBy(x => x.StepCode).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SlaEscalationRule>> ListActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<SlaEscalationRule>.Filter.And(
            ExecutionFilter,
            Builders<SlaEscalationRule>.Filter.Eq(x => x.IsActive, true));
        return await Collection.Find(filter).SortBy(x => x.TemplateId).ThenBy(x => x.StageCode).ToListAsync(ct);
    }

    public Task<SlaEscalationRule?> FindForStepAsync(
        Guid templateId,
        string stageCode,
        string stepCode,
        CancellationToken ct = default)
    {
        var filter = Builders<SlaEscalationRule>.Filter.And(
            ExecutionFilter,
            Builders<SlaEscalationRule>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.StageCode, stageCode),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.StepCode, stepCode),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.IsActive, true));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task DeactivateRulesForTemplateAsync(Guid templateId, CancellationToken ct = default)
    {
        var filter = Builders<SlaEscalationRule>.Filter.And(
            ExecutionFilter,
            Builders<SlaEscalationRule>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.IsActive, true));

        var update = Builders<SlaEscalationRule>.Update
            .Set(x => x.IsActive, false)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }
}
