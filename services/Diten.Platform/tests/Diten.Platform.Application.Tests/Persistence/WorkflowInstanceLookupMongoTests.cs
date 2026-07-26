using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

// Regression cover for the MOD-0023 transition gate lookup, executed against a REAL MongoDB.
//
// Why this file exists: EvaluateWorkflowTransitionGateHandler died in production with
// "cannot sort with keys that are parallel arrays" while 1595 tests were green, because every existing
// test substitutes IWorkflowInstanceRepository with a fake — the Mongo query was never issued. Even the
// in-process chain test that used the real gate handler and the real MOD-0023 handler missed it, because
// the repository at the end of that chain was still a fake. Only a real driver + real server reproduces it.
public sealed class WorkflowInstanceLookupMongoTests : IAsyncLifetime
{
    private const string ObjectType = "task";
    private const string ObjectId = "835dc3ef-56be-437f-9a5e-7df1b1931324";
    private const string ObjectRef = $"tasks|{ObjectType}|{ObjectId}";

    private MongoIntegrationHarness _harness = null!;
    private WorkflowInstanceRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync();
        _repository = new WorkflowInstanceRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    // The core regression. Before the fix this does not merely return the wrong row — the whole query is
    // rejected by the server, the handler throws, and the gate answers WorkflowGateEvaluationFailed, which
    // masked a permanently broken approval gate as a transient evaluation error.
    [Fact]
    public async Task Latest_by_object_ref_survives_a_real_mongo_query()
    {
        var older = await SeedAsync(startedAt: Now.AddHours(-3), createdAt: Now.AddHours(-3));
        var newest = await SeedAsync(startedAt: Now.AddHours(-1), createdAt: Now.AddHours(-1));

        var latest = await _repository.GetLatestByObjectRefAsync(ObjectRef, ObjectType, ObjectId);

        Assert.NotNull(latest);
        Assert.Equal(newest.Id, latest!.Id);
        Assert.NotEqual(older.Id, latest.Id);
    }

    // Pins the null placement the in-memory ordering makes explicit: a never-started instance loses to any
    // started one, however recently it was created. This is the semantic the old server-side descending sort
    // gave for free; losing it silently would change which instance the gate evaluates.
    [Fact]
    public async Task Instance_without_a_start_time_never_wins_over_a_started_one()
    {
        var started = await SeedAsync(startedAt: Now.AddDays(-10), createdAt: Now.AddDays(-10));
        await SeedAsync(startedAt: null, createdAt: Now);

        var latest = await _repository.GetLatestByObjectRefAsync(ObjectRef, ObjectType, ObjectId);

        Assert.Equal(started.Id, latest!.Id);
    }

    // With no started instance at all, the fallback is CreatedAt descending.
    // Note: this is the one case the broken server-side sort also survived — a null StartedAt serializes as
    // BSON null rather than an array, so there is only one array key to sort on. That data dependency is why
    // the gate looked healthy until real instances started carrying a StartedAt.
    [Fact]
    public async Task Never_started_instances_fall_back_to_created_at_descending()
    {
        await SeedAsync(startedAt: null, createdAt: Now.AddHours(-5));
        var newest = await SeedAsync(startedAt: null, createdAt: Now.AddHours(-1));

        var latest = await _repository.GetLatestByObjectRefAsync(ObjectRef, ObjectType, ObjectId);

        Assert.Equal(newest.Id, latest!.Id);
    }

    // Equal StartedAt must break on CreatedAt descending, matching the original ThenByDescending intent.
    [Fact]
    public async Task Equal_start_times_break_the_tie_on_created_at()
    {
        var sharedStart = Now.AddHours(-2);
        await SeedAsync(startedAt: sharedStart, createdAt: Now.AddHours(-4));
        var newest = await SeedAsync(startedAt: sharedStart, createdAt: Now.AddHours(-2));

        var latest = await _repository.GetLatestByObjectRefAsync(ObjectRef, ObjectType, ObjectId);

        Assert.Equal(newest.Id, latest!.Id);
    }

    // Moving the ordering client-side must not widen the query: another object's instances stay invisible.
    [Fact]
    public async Task Other_object_refs_are_not_considered()
    {
        var mine = await SeedAsync(startedAt: Now.AddHours(-6), createdAt: Now.AddHours(-6));
        await SeedAsync(startedAt: Now, createdAt: Now, objectId: "some-other-task", objectRef: "tasks|task|some-other-task");

        var latest = await _repository.GetLatestByObjectRefAsync(ObjectRef, ObjectType, ObjectId);

        Assert.Equal(mine.Id, latest!.Id);
    }

    [Fact]
    public async Task Soft_deleted_instances_are_not_returned()
    {
        var live = await SeedAsync(startedAt: Now.AddHours(-6), createdAt: Now.AddHours(-6));
        await SeedAsync(startedAt: Now, createdAt: Now, isDeleted: true);

        var latest = await _repository.GetLatestByObjectRefAsync(ObjectRef, ObjectType, ObjectId);

        Assert.Equal(live.Id, latest!.Id);
    }

    // Mechanism proof and BL-030 tripwire. This asserts the server STILL rejects a two-key DateTimeOffset
    // sort, which is the only reason GetLatestByObjectRefAsync orders in memory. If BL-030 registers a
    // DateTimeOffsetSerializer, this test starts failing — that failure is the signal to revisit the
    // in-memory ordering, not something to delete.
    [Fact]
    public async Task Server_side_sort_on_two_date_time_offset_keys_is_still_rejected_by_mongo()
    {
        await SeedAsync(startedAt: Now.AddHours(-1), createdAt: Now.AddHours(-1));

        var collection = _harness.Database.GetCollection<WorkflowInstance>("workflow_instances");
        var filter = Builders<WorkflowInstance>.Filter.And(
            Builders<WorkflowInstance>.Filter.Eq(x => x.TenantId, _harness.TenantId),
            Builders<WorkflowInstance>.Filter.Eq(x => x.IsDeleted, false),
            Builders<WorkflowInstance>.Filter.Eq(x => x.ObjectRef, ObjectRef));

        var exception = await Assert.ThrowsAsync<MongoCommandException>(async () =>
            await collection
                .Find(filter)
                .SortByDescending(x => x.StartedAt)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync());

        Assert.Contains("parallel arrays", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private async Task<WorkflowInstance> SeedAsync(
        DateTimeOffset? startedAt,
        DateTimeOffset createdAt,
        string objectId = ObjectId,
        string objectRef = ObjectRef,
        bool isDeleted = false)
    {
        var instance = new WorkflowInstance
        {
            TenantId = _harness.TenantId,
            CreatedAt = createdAt,
            TemplateId = Guid.NewGuid(),
            WorkflowTemplateId = Guid.NewGuid(),
            ObjectType = ObjectType,
            ObjectId = objectId,
            ObjectRef = objectRef,
            Status = WorkflowInstanceStatus.Active,
            StartedAt = startedAt,
            IsDeleted = isDeleted
        };

        await _harness.Database
            .GetCollection<WorkflowInstance>("workflow_instances")
            .InsertOneAsync(instance);

        return instance;
    }
}
