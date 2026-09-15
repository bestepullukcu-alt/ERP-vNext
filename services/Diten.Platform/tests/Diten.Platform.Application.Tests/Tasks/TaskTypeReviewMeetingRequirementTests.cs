using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// `Task` here is System.Threading.Tasks.Task: this file's own namespace ends in `.Tasks`, which shadows it.
using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-PSS-MOD0024-REVIEW-MEETING-POLICY-01 — the TYPE carries whether a review meeting is NotAllowed, Optional or
/// Required before the reviewer's final decision.
///
/// <para>Optional is the default because the task projection emitted a constant <c>optional</c> before this
/// field existed: every type that does not say, stored or new, must keep behaving exactly as it did.</para>
/// </summary>
public sealed class TaskTypeReviewMeetingRequirementTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static CreateTaskTypeHandler CreateHandler(FakeTaskTypeRepository types)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        return new CreateTaskTypeHandler(
            types, tenant, new Mock<ICurrentUserContext>().Object, new FakeControlledDocumentEffectivenessPort());
    }

    private static CreateTaskTypeRequest Create(TaskReviewMeetingRequirement? requirement) => new(
        "REV", "Review", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null,
        ReviewMeetingRequirement: requirement);

    private static UpdateTaskTypeRequest Update(TaskReviewMeetingRequirement? requirement, int expectedVersion = 1) => new(
        "REV", "Review", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null, expectedVersion,
        ReviewMeetingRequirement: requirement);

    private static TaskType Stored(TaskReviewMeetingRequirement requirement) => new()
    {
        TenantId = TenantId, Code = "REV", Name = "Review", ReviewMeetingRequirement = requirement
    };

    [Fact]
    public void The_three_values_are_pinned__the_driver_stores_the_number()
    {
        Assert.Equal(0, (int)TaskReviewMeetingRequirement.NotAllowed);
        Assert.Equal(1, (int)TaskReviewMeetingRequirement.Optional);
        Assert.Equal(2, (int)TaskReviewMeetingRequirement.Required);
        Assert.Equal(3, Enum.GetValues<TaskReviewMeetingRequirement>().Length);
    }

    [Fact]
    public void A_new_type_defaults_to_Optional__the_value_the_projection_emitted_before()
    {
        Assert.Equal(TaskReviewMeetingRequirement.Optional, new TaskType { TenantId = TenantId, Code = "X", Name = "X" }.ReviewMeetingRequirement);
    }

    [Fact]
    public async Task Create_without_a_value_stores_Optional()
    {
        var types = new FakeTaskTypeRepository();

        var result = await CreateHandler(types).Handle(new CreateTaskTypeCommand(Create(null), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskReviewMeetingRequirement.Optional, Assert.Single(types.All).ReviewMeetingRequirement);
    }

    [Theory]
    [InlineData(TaskReviewMeetingRequirement.NotAllowed)]
    [InlineData(TaskReviewMeetingRequirement.Required)]
    public async Task Create_stores_the_requirement_it_was_given(TaskReviewMeetingRequirement requirement)
    {
        var types = new FakeTaskTypeRepository();

        var result = await CreateHandler(types).Handle(new CreateTaskTypeCommand(Create(requirement), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(requirement, Assert.Single(types.All).ReviewMeetingRequirement);
    }

    [Fact]
    public async Task Update_without_a_value_is_NOT_ASKING_and_keeps_Required()
    {
        /*
         * ⚠ The deletion-class guard: a client written before this field existed posts no value. Treating that as
         * Optional would quietly drop a Required gate on every save.
         */
        var type = Stored(TaskReviewMeetingRequirement.Required);
        var types = new FakeTaskTypeRepository(type);

        var result = await new UpdateTaskTypeHandler(types, new FakeControlledDocumentEffectivenessPort())
            .Handle(new UpdateTaskTypeCommand(type.Id, Update(null), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskReviewMeetingRequirement.Required, type.ReviewMeetingRequirement);
    }

    [Fact]
    public async Task Update_with_a_value_replaces_it()
    {
        var type = Stored(TaskReviewMeetingRequirement.Optional);
        var types = new FakeTaskTypeRepository(type);

        var result = await new UpdateTaskTypeHandler(types, new FakeControlledDocumentEffectivenessPort()).Handle(
            new UpdateTaskTypeCommand(type.Id, Update(TaskReviewMeetingRequirement.NotAllowed), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskReviewMeetingRequirement.NotAllowed, type.ReviewMeetingRequirement);
    }

    [Fact]
    public async Task An_undefined_value_is_refused_on_create_and_update__not_stored()
    {
        // The string converter still accepts a bare integer, so `7` can reach the handler.
        var undefined = (TaskReviewMeetingRequirement)7;

        var created = await CreateHandler(new FakeTaskTypeRepository())
            .Handle(new CreateTaskTypeCommand(Create(undefined), "c"), CancellationToken.None);
        Assert.False(created.IsSuccessful);
        Assert.Equal(400, created.StatusCode);
        Assert.Equal(TaskReasonCodes.TaskTypeReviewMeetingRequirementInvalid, created.ReasonCode);

        var type = Stored(TaskReviewMeetingRequirement.Required);
        var updated = await new UpdateTaskTypeHandler(new FakeTaskTypeRepository(type), new FakeControlledDocumentEffectivenessPort())
            .Handle(new UpdateTaskTypeCommand(type.Id, Update(undefined), "c"), CancellationToken.None);
        Assert.False(updated.IsSuccessful);
        Assert.Equal(TaskReasonCodes.TaskTypeReviewMeetingRequirementInvalid, updated.ReasonCode);
        Assert.Equal(TaskReviewMeetingRequirement.Required, type.ReviewMeetingRequirement);
    }

    [Fact]
    public void The_read_model_carries_the_stored_requirement()
    {
        var dto = TaskTypeMapping.ToDto(Stored(TaskReviewMeetingRequirement.Required));

        Assert.Equal(TaskReviewMeetingRequirement.Required, dto.ReviewMeetingRequirement);
    }

    [Fact]
    public void It_is_a_TYPE_setting__the_task_carries_no_copy_of_it()
    {
        // A copy on the task would freeze the value per task and make "change the type" mean two things.
        Assert.DoesNotContain(
            typeof(TaskItem).GetProperties(),
            property => property.Name.Contains("ReviewMeeting", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// The same field against a REAL mongod: backward compatibility without a migration, and the proof that a type
/// change touches no task document.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TaskTypeReviewMeetingRequirementMongoTests
{
    private const string Field = nameof(TaskType.ReviewMeetingRequirement);

    [Fact]
    public async Task A_stored_type_WITHOUT_the_field_reads_as_Optional()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (repository, database, tenantId) = Arrange(mongo);

        var type = new TaskType
        {
            TenantId = tenantId, Code = "LEGACY", Name = "Written before the field",
            ReviewMeetingRequirement = TaskReviewMeetingRequirement.NotAllowed
        };
        await repository.CreateAsync(type);

        // Make it a genuine pre-field document. The element exists after a write, so removing it is not vacuous.
        var types = database.GetCollection<BsonDocument>(PlatformCollections.TaskTypes);
        var byCode = Builders<BsonDocument>.Filter.Eq(nameof(TaskType.Code), "LEGACY");
        Assert.True((await types.Find(byCode).SingleAsync()).Contains(Field));
        await types.UpdateOneAsync(byCode, Builders<BsonDocument>.Update.Unset(Field));
        Assert.False((await types.Find(byCode).SingleAsync()).Contains(Field));

        var read = await repository.GetByIdAsync(type.Id);

        Assert.NotNull(read);
        Assert.Equal(TaskReviewMeetingRequirement.Optional, read!.ReviewMeetingRequirement);
        // And the list — the read that takes a tenant's whole screen down when one document cannot deserialise.
        Assert.Equal(TaskReviewMeetingRequirement.Optional, Assert.Single(await repository.ListAllAsync()).ReviewMeetingRequirement);
    }

    [Fact]
    public async Task Required_survives_the_round_trip()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (repository, _, tenantId) = Arrange(mongo);

        var type = new TaskType
        {
            TenantId = tenantId, Code = "GATED", Name = "Gated", ReviewMeetingRequirement = TaskReviewMeetingRequirement.Required
        };
        await repository.CreateAsync(type);

        Assert.Equal(TaskReviewMeetingRequirement.Required, (await repository.GetByIdAsync(type.Id))!.ReviewMeetingRequirement);
    }

    [Fact]
    public async Task Changing_the_type_s_requirement_leaves_every_task_document_byte_identical()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (repository, database, tenantId) = Arrange(mongo);

        var type = new TaskType { TenantId = tenantId, Code = "REV", Name = "Review" };
        await repository.CreateAsync(type);

        var tasks = database.GetCollection<BsonDocument>(PlatformCollections.TaskItems);
        await tasks.InsertManyAsync(
        [
            new BsonDocument { { "_id", Guid.NewGuid().ToString() }, { "TenantId", tenantId.ToString() }, { "TaskTypeId", type.Id.ToString() }, { "Title", "open under the type" } },
            new BsonDocument { { "_id", Guid.NewGuid().ToString() }, { "TenantId", tenantId.ToString() }, { "TaskTypeId", type.Id.ToString() }, { "Title", "another" } }
        ]);
        var before = await SnapshotAsync(tasks);

        var result = await new UpdateTaskTypeHandler(repository, new FakeControlledDocumentEffectivenessPort()).Handle(
            new UpdateTaskTypeCommand(
                type.Id,
                new UpdateTaskTypeRequest("REV", "Review", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null, type.Version,
                    ReviewMeetingRequirement: TaskReviewMeetingRequirement.Required),
                "c"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskReviewMeetingRequirement.Required, (await repository.GetByIdAsync(type.Id))!.ReviewMeetingRequirement);
        Assert.Equal(before, await SnapshotAsync(tasks));
    }

    private static (TaskTypeRepository Repository, IMongoDatabase Database, Guid TenantId) Arrange(DisposableMongoReplicaSet mongo)
    {
        var tenantId = Guid.NewGuid();
        var database = mongo.CreateDatabase();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return (new TaskTypeRepository(new PlatformDbContext(mongo.Client, database), tenantContext), database, tenantId);
    }

    private static async Task<string> SnapshotAsync(IMongoCollection<BsonDocument> collection)
    {
        var documents = await collection.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
            .ToListAsync();
        return Convert.ToHexString(documents.SelectMany(document => document.ToBson()).ToArray());
    }
}
