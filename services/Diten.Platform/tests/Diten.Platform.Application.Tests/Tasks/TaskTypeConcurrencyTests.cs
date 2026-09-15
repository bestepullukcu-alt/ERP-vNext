using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// `Task` here is System.Threading.Tasks.Task: this file's own namespace ends in `.Tasks`, which shadows it.
using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-PSS-MOD0024-TASK-TYPE-CONCURRENCY-01 (BL-375) — two managers editing the same task type at once used to
/// have the second silently overwrite the first's change with no warning at all: <c>ITaskTypeRepository.UpdateAsync</c>
/// took no expected version and always replaced. Modelled on <c>TaskFieldDefinitionHandlers</c>'s own concurrency
/// guard (<c>TaskModels.cs</c>'s <see cref="UpdateTaskFieldDefinitionRequest"/>) — the sibling this WP copies.
///
/// <para>Activate/deactivate shares the SAME repository write path as the full edit, so it gets the SAME
/// protection here too, not left as the one door still unguarded.</para>
/// </summary>
public sealed class TaskTypeConcurrencyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static CreateTaskTypeHandler CreateHandler(FakeTaskTypeRepository types)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        return new CreateTaskTypeHandler(
            types, tenant, new Mock<ICurrentUserContext>().Object, new FakeControlledDocumentEffectivenessPort());
    }

    private static UpdateTaskTypeRequest UpdateRequest(int expectedVersion) => new(
        "CNC", "Concurrency", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null, expectedVersion);

    private static TaskType Stored(int version = 1) => new()
    {
        TenantId = TenantId, Code = "CNC", Name = "Concurrency", Version = version
    };

    // ── Unit tests — the full edit ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_with_the_current_version_succeeds_and_bumps_it_by_one()
    {
        var type = Stored(version: 3);
        var types = new FakeTaskTypeRepository(type);

        var result = await new UpdateTaskTypeHandler(types).Handle(
            new UpdateTaskTypeCommand(type.Id, UpdateRequest(3), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(4, Assert.Single(types.All).Version);
    }

    [Fact]
    public async Task Update_with_a_stale_version_is_refused_and_the_stored_row_is_untouched()
    {
        var type = Stored(version: 3);
        var types = new FakeTaskTypeRepository(type);

        var result = await new UpdateTaskTypeHandler(types).Handle(
            new UpdateTaskTypeCommand(type.Id, UpdateRequest(2), "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, result.ReasonCode);
        // The version-bump only happens inside a successful UpdateAsync (see the fake and the real repository
        // both) — unchanged here proves the write never landed. The BYTE-IDENTICAL document guarantee this
        // implies for a real caller is proven against a real mongod below (TaskTypeConcurrencyMongoTests), where
        // the in-memory entity and the persisted document are genuinely separate objects — a fake sharing one
        // reference between "read" and "stored" cannot prove that half on its own.
        Assert.Equal(3, Assert.Single(types.All).Version);
    }

    // ── Unit tests — activate/deactivate (the SAME write path) ─────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_with_the_current_version_succeeds_and_bumps_it()
    {
        var type = Stored(version: 1);
        var types = new FakeTaskTypeRepository(type);

        var result = await new SetTaskTypeActiveHandler(types, new FakeControlledDocumentEffectivenessPort()).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(false, 1), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var stored = Assert.Single(types.All);
        Assert.False(stored.IsActive);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task Deactivate_with_a_stale_version_is_refused()
    {
        var type = Stored(version: 5);
        var types = new FakeTaskTypeRepository(type);
        var before = type.IsActive;

        var result = await new SetTaskTypeActiveHandler(types, new FakeControlledDocumentEffectivenessPort()).Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(!before, 4), "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, result.ReasonCode);
        // The version-bump only happens inside a successful UpdateAsync — unchanged here proves the write never
        // landed. (Not asserting IsActive here: the HANDLER assigns it to the entity in memory before calling
        // UpdateAsync, same as every other field on a full-replace edit — the fake shares that one object between
        // "read" and "stored", so it cannot demonstrate the in-memory assignment being discarded. A real caller's
        // persisted document never sees it either way; see TaskTypeConcurrencyMongoTests for that proof.)
        Assert.Equal(5, Assert.Single(types.All).Version);
    }

    [Fact]
    public void The_read_model_carries_the_stored_version()
    {
        var dto = TaskTypeQueryHandlersTestSupport.ToDto(Stored(version: 7));
        Assert.Equal(7, dto.Version);
    }
}

/// <summary>Thin indirection so this file does not need a `using` for the internal mapping class's namespace
/// quirks — calls straight through to <c>TaskTypeMapping.ToDto</c>.</summary>
internal static class TaskTypeQueryHandlersTestSupport
{
    public static TaskTypeDto ToDto(TaskType type) =>
        Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers.TaskTypeMapping.ToDto(type);
}

/// <summary>
/// The same guard against a REAL mongod: the expected-version write actually fences at the database, and a
/// rejected write leaves the stored document byte-identical.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TaskTypeConcurrencyMongoTests
{
    [Fact]
    public async Task A_stale_write_is_rejected_and_the_stored_document_is_byte_identical()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (repository, database, tenantId) = Arrange(mongo);

        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Concurrency" };
        await repository.CreateAsync(type);
        var before = await SnapshotAsync(database, type.Id);

        var handler = new UpdateTaskTypeHandler(repository);
        var stale = new UpdateTaskTypeRequest(
            "CNC", "Renamed while stale", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null,
            ExpectedVersion: 0 /* the row is really at Version 1 */);

        var result = await handler.Handle(new UpdateTaskTypeCommand(type.Id, stale, "c"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, result.ReasonCode);
        Assert.Equal(before, await SnapshotAsync(database, type.Id));
    }

    [Fact]
    public async Task The_correct_version_succeeds_and_the_stored_version_advances_by_exactly_one()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (repository, _, tenantId) = Arrange(mongo);

        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Concurrency" };
        await repository.CreateAsync(type);
        var startingVersion = (await repository.GetByIdAsync(type.Id))!.Version;

        var handler = new UpdateTaskTypeHandler(repository);
        var request = new UpdateTaskTypeRequest(
            "CNC", "Renamed", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null,
            ExpectedVersion: startingVersion);

        var result = await handler.Handle(new UpdateTaskTypeCommand(type.Id, request, "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var after = await repository.GetByIdAsync(type.Id);
        Assert.Equal("Renamed", after!.Name);
        Assert.Equal(startingVersion + 1, after.Version);
    }

    [Fact]
    public async Task Two_concurrent_edits_the_second_one_applied_is_refused_not_silently_overwritten()
    {
        // THE ACTUAL BUG BL-375 NAMES: both managers read the SAME version, both submit — the second must be
        // refused rather than quietly winning over the first.
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (repository, _, tenantId) = Arrange(mongo);

        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Concurrency" };
        await repository.CreateAsync(type);
        var readVersion = (await repository.GetByIdAsync(type.Id))!.Version;

        var handler = new UpdateTaskTypeHandler(repository);
        var managerA = new UpdateTaskTypeRequest(
            "CNC", "Manager A's name", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null,
            ExpectedVersion: readVersion);
        var managerB = new UpdateTaskTypeRequest(
            "CNC", "Manager B's name", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null,
            ExpectedVersion: readVersion);

        var first = await handler.Handle(new UpdateTaskTypeCommand(type.Id, managerA, "a"), CancellationToken.None);
        var second = await handler.Handle(new UpdateTaskTypeCommand(type.Id, managerB, "b"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, second.ReasonCode);
        // Manager A's write survives; Manager B's is the one refused.
        Assert.Equal("Manager A's name", (await repository.GetByIdAsync(type.Id))!.Name);
    }

    [Fact]
    public async Task Deactivate_with_a_stale_version_is_rejected_and_the_stored_document_is_byte_identical()
    {
        // The SAME write path as the full edit (BL-375's own point): the toggle gets the same real-database proof.
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (repository, database, tenantId) = Arrange(mongo);

        var type = new TaskType { TenantId = tenantId, Code = "CNC", Name = "Concurrency", IsActive = true };
        await repository.CreateAsync(type);
        var before = await SnapshotAsync(database, type.Id);

        var handler = new SetTaskTypeActiveHandler(repository, new FakeControlledDocumentEffectivenessPort());
        var result = await handler.Handle(
            new SetTaskTypeActiveCommand(type.Id, new SetTaskTypeActiveRequest(false, 0 /* stale */), "c"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, result.ReasonCode);
        Assert.Equal(before, await SnapshotAsync(database, type.Id));
        Assert.True((await repository.GetByIdAsync(type.Id))!.IsActive);
    }

    private static (TaskTypeRepository Repository, IMongoDatabase Database, Guid TenantId) Arrange(DisposableMongoReplicaSet mongo)
    {
        var tenantId = Guid.NewGuid();
        var database = mongo.CreateDatabase();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return (new TaskTypeRepository(new PlatformDbContext(mongo.Client, database), tenantContext), database, tenantId);
    }

    private static async Task<string> SnapshotAsync(IMongoDatabase database, Guid typeId)
    {
        var collection = database.GetCollection<BsonDocument>(
            Diten.Platform.Infrastructure.Persistence.Schema.PlatformCollections.TaskTypes);
        var document = await collection.Find(
            Builders<BsonDocument>.Filter.Eq("_id", typeId.ToString())).FirstOrDefaultAsync();
        return document?.ToJson() ?? string.Empty;
    }
}
