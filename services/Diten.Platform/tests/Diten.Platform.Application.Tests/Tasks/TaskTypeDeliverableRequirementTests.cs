using Diten.Platform.Application.Common;
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
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// `Task` here is System.Threading.Tasks.Task: this file's own namespace ends in `.Tasks`, which shadows it.
using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-PSS-MOD0024-ATTACHMENTS-UX-01 — <see cref="TaskType.RequiresDeliverableOnCompletion"/>: a type may demand
/// at least one live Deliverable-kind attachment (ATT-1) before work of that type can be marked Done. Enforced on
/// the SAME complete transition the checklist evidence gate already guards, and modelled on
/// <c>TaskTypeReviewMeetingRequirementTests</c>: unit tests for create/update/projection, plus a real-Mongo class
/// proving a type written before this field existed reads false with no migration.
/// </summary>
public sealed class TaskTypeDeliverableRequirementTests
{
    private static readonly Guid TenantId = TaskTestData.Tenant;

    private static CreateTaskTypeHandler CreateHandler(FakeTaskTypeRepository types)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        return new CreateTaskTypeHandler(
            types, tenant, new Mock<ICurrentUserContext>().Object, new FakeControlledDocumentEffectivenessPort());
    }

    private static CreateTaskTypeRequest CreateRequest(bool requiresDeliverable) => new(
        "DLV", "Deliverable", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null,
        RequiresDeliverableOnCompletion: requiresDeliverable);

    private static UpdateTaskTypeRequest UpdateRequest(bool requiresDeliverable, int expectedVersion = 1) => new(
        "DLV", "Deliverable", null, TaskRecordClass.NOT_A_RECORD, null, null, false, null, null, expectedVersion,
        RequiresDeliverableOnCompletion: requiresDeliverable);

    private static TaskType Stored(bool requiresDeliverable) => new()
    {
        TenantId = TenantId, Code = "DLV", Name = "Deliverable", RequiresDeliverableOnCompletion = requiresDeliverable
    };

    // ── The entity default ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_new_type_defaults_to_false__every_type_written_before_this_field_existed_behaves_as_one()
    {
        Assert.False(new TaskType { TenantId = TenantId, Code = "X", Name = "X" }.RequiresDeliverableOnCompletion);
    }

    // ── Create / update ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_without_asking_stores_false()
    {
        var types = new FakeTaskTypeRepository();

        var result = await CreateHandler(types).Handle(
            new CreateTaskTypeCommand(CreateRequest(requiresDeliverable: false), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.False(Assert.Single(types.All).RequiresDeliverableOnCompletion);
    }

    [Fact]
    public async Task Create_can_turn_the_flag_on()
    {
        var types = new FakeTaskTypeRepository();

        var result = await CreateHandler(types).Handle(
            new CreateTaskTypeCommand(CreateRequest(requiresDeliverable: true), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(Assert.Single(types.All).RequiresDeliverableOnCompletion);
    }

    [Fact]
    public async Task Update_is_a_plain_full_replace__unlike_ClosureOutcomes_there_is_no_pre_existing_screen_to_protect()
    {
        var type = Stored(requiresDeliverable: true);
        var types = new FakeTaskTypeRepository(type);

        var result = await new UpdateTaskTypeHandler(types).Handle(
            new UpdateTaskTypeCommand(type.Id, UpdateRequest(requiresDeliverable: false), "c"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.False(type.RequiresDeliverableOnCompletion);
    }

    [Fact]
    public void The_read_model_carries_the_stored_flag()
    {
        Assert.True(TaskTypeMapping.ToDto(Stored(requiresDeliverable: true)).RequiresDeliverableOnCompletion);
        Assert.False(TaskTypeMapping.ToDto(Stored(requiresDeliverable: false)).RequiresDeliverableOnCompletion);
    }

    // ── The complete-transition gate (TransitionTaskItemHandler) ────────────────────────────────────────────

    private static TaskItem OpenTask(Guid? taskTypeId = null) => new()
    {
        TenantId = TenantId,
        OrganizationUnitId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
        Title = "Ship the report",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        Lifecycle = TaskLifecycle.InProgress,
        TaskTypeId = taskTypeId
    };

    private static TaskAttachment Attachment(Guid taskId, TaskAttachmentKind kind, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        TaskId = taskId,
        Kind = kind,
        ContentId = Guid.NewGuid(),
        FileName = "report.xlsx",
        MediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        UploadedByUserId = TaskTestData.Me,
        UploadedAt = DateTimeOffset.UtcNow,
        IsDeleted = deleted
    };

    private static TransitionTaskItemHandler Handler(
        FakeTaskItemRepository tasks, FakeTaskTypeRepository types, FakeTaskAttachmentRepository attachments)
        => new(
            tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
            new FakeChecklistRunRepository(), new TaskChecklistService(), new FakeWorkflowTransitionGate(),
            new FakeTaskDependencyRepository(), types, new FakeTaskNotificationService(),
            new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
            attachments, NullLogger<TransitionTaskItemHandler>.Instance);

    private static Task<Response<NoContent>> Complete(
        TaskItem task, FakeTaskTypeRepository types, FakeTaskAttachmentRepository attachments)
        => Handler(new FakeTaskItemRepository(task), types, attachments).Handle(
            new TransitionTaskItemCommand(
                task.Id, TaskLifecycle.Done, new TaskTransitionRequest(task.Version, null, null), "c"),
            CancellationToken.None);

    [Fact]
    public async Task Flag_off_completes_exactly_as_before__no_attachment_asked_for()
    {
        var type = Stored(requiresDeliverable: false);
        var task = OpenTask(type.Id);

        var result = await Complete(task, new FakeTaskTypeRepository(type), new FakeTaskAttachmentRepository());

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.Done, task.Lifecycle);
    }

    [Fact]
    public async Task Flag_on_and_no_deliverable_refuses_with_the_pack_s_own_code()
    {
        var type = Stored(requiresDeliverable: true);
        var task = OpenTask(type.Id);

        var result = await Complete(task, new FakeTaskTypeRepository(type), new FakeTaskAttachmentRepository());

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.DeliverableRequired, result.ReasonCode);
        Assert.Equal(TaskLifecycle.InProgress, task.Lifecycle);
    }

    [Fact]
    public async Task Flag_on_and_a_live_deliverable_completes()
    {
        var type = Stored(requiresDeliverable: true);
        var task = OpenTask(type.Id);
        var attachments = new FakeTaskAttachmentRepository();
        attachments.Items.Add(Attachment(task.Id, TaskAttachmentKind.Deliverable));

        var result = await Complete(task, new FakeTaskTypeRepository(type), attachments);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.Done, task.Lifecycle);
    }

    [Fact]
    public async Task A_soft_deleted_deliverable_does_not_count()
    {
        var type = Stored(requiresDeliverable: true);
        var task = OpenTask(type.Id);
        var attachments = new FakeTaskAttachmentRepository();
        attachments.Items.Add(Attachment(task.Id, TaskAttachmentKind.Deliverable, deleted: true));

        var result = await Complete(task, new FakeTaskTypeRepository(type), attachments);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.DeliverableRequired, result.ReasonCode);
    }

    [Fact]
    public async Task An_Evidence_or_plain_Attachment_file_does_not_satisfy_the_Deliverable_gate()
    {
        var type = Stored(requiresDeliverable: true);
        var task = OpenTask(type.Id);
        var attachments = new FakeTaskAttachmentRepository();
        attachments.Items.Add(Attachment(task.Id, TaskAttachmentKind.Evidence));
        attachments.Items.Add(Attachment(task.Id, TaskAttachmentKind.Attachment));

        var result = await Complete(task, new FakeTaskTypeRepository(type), attachments);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.DeliverableRequired, result.ReasonCode);
    }

    [Fact]
    public async Task An_unclassified_task_or_a_type_that_no_longer_resolves_never_gates()
    {
        // No TaskTypeId at all — the overwhelming majority of tasks open today.
        var untyped = OpenTask(taskTypeId: null);
        var untypedResult = await Complete(untyped, new FakeTaskTypeRepository(), new FakeTaskAttachmentRepository());
        Assert.True(untypedResult.IsSuccessful);

        // A TaskTypeId pointing at nothing the repository can resolve (hard-deleted out from under it elsewhere).
        var dangling = OpenTask(taskTypeId: Guid.NewGuid());
        var danglingResult = await Complete(dangling, new FakeTaskTypeRepository(), new FakeTaskAttachmentRepository());
        Assert.True(danglingResult.IsSuccessful);
    }
}

/// <summary>
/// The same field against a REAL mongod: backward compatibility without a migration, exactly like
/// <c>TaskTypeReviewMeetingRequirementMongoTests</c> proves for its own sibling field.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class TaskTypeDeliverableRequirementMongoTests
{
    private const string Field = nameof(TaskType.RequiresDeliverableOnCompletion);

    [Fact]
    public async Task A_stored_type_WITHOUT_the_field_reads_as_false()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (repository, database, tenantId) = Arrange(mongo);

        var type = new TaskType
        {
            TenantId = tenantId, Code = "LEGACY", Name = "Written before the field",
            RequiresDeliverableOnCompletion = true
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
        Assert.False(read!.RequiresDeliverableOnCompletion);
        // And the list — the read that takes a tenant's whole screen down when one document cannot deserialise.
        Assert.False(Assert.Single(await repository.ListAllAsync()).RequiresDeliverableOnCompletion);
    }

    [Fact]
    public async Task True_survives_the_round_trip()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var (repository, _, tenantId) = Arrange(mongo);

        var type = new TaskType
        {
            TenantId = tenantId, Code = "GATED", Name = "Gated", RequiresDeliverableOnCompletion = true
        };
        await repository.CreateAsync(type);

        Assert.True((await repository.GetByIdAsync(type.Id))!.RequiresDeliverableOnCompletion);
    }

    private static (TaskTypeRepository Repository, IMongoDatabase Database, Guid TenantId) Arrange(DisposableMongoReplicaSet mongo)
    {
        var tenantId = Guid.NewGuid();
        var database = mongo.CreateDatabase();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return (new TaskTypeRepository(new PlatformDbContext(mongo.Client, database), tenantContext), database, tenantId);
    }
}
