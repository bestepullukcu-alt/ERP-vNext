using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// BL-549 + BL-539 + BL-540 (1) — see <see cref="IWorkflowStartRecoveryStore"/>. The "no task" test is a server-side
/// <c>$lookup</c> into <c>approval_tasks</c> on (TenantId, WorkflowInstanceId) — the leading keys of
/// <c>ix_approval_tasks_tenant_instance_status</c> — limited to one row per instance, so a cycle reads only the instances it
/// reports. Times are compared as the driver stores them (DateTimeOffset as [ticks, offset], BL-030): the bound is UTC and
/// every instance time the engine writes is UTC, the same rule the SLA sweep's due-date filter relies on.
/// </summary>
public sealed class WorkflowStartRecoveryStore : TenantRepository<WorkflowInstance>, IWorkflowStartRecoveryStore
{
    private static readonly WorkflowInstanceStatus[] OpenStatuses =
        [WorkflowInstanceStatus.Pending, WorkflowInstanceStatus.Active, WorkflowInstanceStatus.Escalated];

    public WorkflowStartRecoveryStore(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.WorkflowInstances)
    {
    }

    public async Task<(IReadOnlyList<WorkflowInstance> Page, long Total)> FindAbandonedGenericStartsAsync(
        DateTimeOffset movedBeforeUtc,
        int maxItems,
        CancellationToken ct = default)
    {
        // FIX1 K3 — one pass: the $lookup runs once, and $facet splits its result into the page and the count.
        var pipeline = AbandonedPipeline(movedBeforeUtc);
        pipeline.Add(new BsonDocument("$facet", new BsonDocument
        {
            {
                "page", new BsonArray
                {
                    new BsonDocument("$sort", new BsonDocument("_id", 1)),
                    new BsonDocument("$limit", Math.Max(1, maxItems)),
                    new BsonDocument("$project", new BsonDocument("_task", 0))
                }
            },
            { "total", new BsonArray { new BsonDocument("$count", "n") } }
        }));
        var result = await Collection.Aggregate<BsonDocument>(pipeline, cancellationToken: ct).FirstOrDefaultAsync(ct);
        if (result is null)
        {
            return ([], 0);
        }

        var page = result["page"].AsBsonArray
            .Select(document => BsonSerializer.Deserialize<WorkflowInstance>(document.AsBsonDocument))
            .ToList();
        var total = result["total"].AsBsonArray.Count == 0 ? 0 : result["total"][0]["n"].ToInt64();
        return (page, total);
    }

    public async Task<long> CountIncompleteTrustedStartsAsync(DateTimeOffset movedBeforeUtc, CancellationToken ct = default) =>
        await Collection.CountDocumentsAsync(IncompleteTrusted(movedBeforeUtc), cancellationToken: ct);

    public async Task<IReadOnlyList<WorkflowInstance>> ListIncompleteTrustedStartsAsync(
        DateTimeOffset movedBeforeUtc,
        int maxItems,
        CancellationToken ct = default) =>
        await Collection.Find(IncompleteTrusted(movedBeforeUtc)).SortBy(x => x.Id).Limit(Math.Max(1, maxItems)).ToListAsync(ct);

    // A reservation's every checkpoint advance stamps UpdatedAt; one never advanced has only its CreatedAt.
    private FilterDefinition<WorkflowInstance> IncompleteTrusted(DateTimeOffset movedBeforeUtc)
    {
        var f = Builders<WorkflowInstance>.Filter;
        return f.And(
            ExecutionFilter,
            f.Ne(x => x.TrustedConsumerClientId, null),
            f.Ne(x => x.StartCheckpoint, WorkflowStartCheckpoint.Completed),
            f.Eq(x => x.Status, WorkflowInstanceStatus.Pending),
            f.Or(
                f.Lte(x => x.UpdatedAt, movedBeforeUtc),
                f.And(f.Eq(x => x.UpdatedAt, null), f.Lte(x => x.CreatedAt, movedBeforeUtc))));
    }

    public async Task<IReadOnlyList<WorkflowInstance>> ListOpenGenericInstancesAsync(
        IReadOnlyCollection<string> objectTypes,
        int maxItems,
        Guid? afterId = null,
        CancellationToken ct = default)
    {
        // FIX1 item 5 — a page AFTER a cursor (by id), so the caller can walk every open instance, not only the first page.
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.TrustedConsumerClientId, null),
            Builders<WorkflowInstance>.Filter.In(x => x.Status, OpenStatuses),
            Builders<WorkflowInstance>.Filter.In(x => x.ObjectType, objectTypes),
            afterId is { } after ? Builders<WorkflowInstance>.Filter.Gt(x => x.Id, after) : Builders<WorkflowInstance>.Filter.Empty);
        return await Collection.Find(filter).SortBy(x => x.Id).Limit(Math.Max(1, maxItems)).ToListAsync(ct);
    }

    private List<BsonDocument> AbandonedPipeline(DateTimeOffset movedBeforeUtc)
    {
        var moved = Builders<WorkflowInstance>.Filter;
        var match = moved.And(
            ExecutionFilter,
            moved.Eq(x => x.TrustedConsumerClientId, null),
            moved.Eq(x => x.Status, WorkflowInstanceStatus.Active),
            moved.Or(
                moved.Lte(x => x.LastTransitionAt, movedBeforeUtc),
                moved.And(moved.Eq(x => x.LastTransitionAt, null), moved.Lte(x => x.StartedAt, movedBeforeUtc))));
        var rendered = match.Render(BsonSerializer.LookupSerializer<WorkflowInstance>(), BsonSerializer.SerializerRegistry);
        return
        [
            new BsonDocument("$match", rendered),
            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", PlatformCollections.ApprovalTasks },
                { "let", new BsonDocument { { "tenant", "$TenantId" }, { "instance", "$_id" } } },
                {
                    // ANY task counts, deleted ones included: an instance that ever had a task is never "abandoned".
                    "pipeline", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$TenantId", "$$tenant" }),
                            new BsonDocument("$eq", new BsonArray { "$WorkflowInstanceId", "$$instance" })
                        }))),
                        new BsonDocument("$limit", 1),
                        new BsonDocument("$project", new BsonDocument("_id", 1))
                    }
                },
                { "as", "_task" }
            }),
            new BsonDocument("$match", new BsonDocument("_task", new BsonDocument("$size", 0)))
        ];
    }
}
