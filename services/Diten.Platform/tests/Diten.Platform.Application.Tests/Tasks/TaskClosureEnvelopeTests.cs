using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
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
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-PSS-MOD0024-CLOSURE-ENVELOPE-2A-01 — Faz 2a: the closing narrative (<c>TaskItem.ClosureNote</c>) and the
/// CLOSURE-stage configurable fields (<c>TaskFieldDefinition.Stage</c>).
///
/// <para><b>The one rule everything else serves.</b> A Closure-stage definition must be invisible to every
/// ORDINARY create/update and enforced only at <c>complete</c>/<c>cancel</c> — the create form never draws it, so
/// a type with one Required closure field that ALSO gated ordinary creation would make every new task of that
/// type unsavable for a field nobody on the create screen could ever see.</para>
/// </summary>
public sealed class TaskClosureEnvelopeTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;

    private static TaskFieldDefinition ClosureField(string code, bool required, TaskFieldValueType type = TaskFieldValueType.Text) => new()
    {
        TenantId = Tenant, Code = code, LabelText = code, ValueType = type, Section = "Closure",
        Stage = TaskFieldStage.Closure, IsRequired = required
    };

    private static TaskFieldDefinition EntryField(string code, bool required) => new()
    {
        TenantId = Tenant, Code = code, LabelText = code, ValueType = TaskFieldValueType.Text, Section = "Entry",
        Stage = TaskFieldStage.Entry, IsRequired = required
    };

    private static TaskItem OpenTask() => new()
    {
        TenantId = Tenant,
        OrganizationUnitId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
        Title = "Close me",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        Lifecycle = TaskLifecycle.InProgress
    };

    // ── §4 — the entity default ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Stage_defaults_to_Entry__every_definition_written_before_this_field_existed_behaves_as_one()
    {
        // No `Stage` set — this is the object an OLD definition deserialises into, not one this test configured.
        var writtenBeforeTheFieldExisted = new TaskFieldDefinition
        {
            TenantId = Tenant, Code = "x", LabelText = "x", ValueType = TaskFieldValueType.Text, Section = "s"
        };

        Assert.Equal(TaskFieldStage.Entry, writtenBeforeTheFieldExisted.Stage);
    }

    // ── §4 — a Closure field must not gate an ordinary create/update ────────────────────────────────────────

    [Fact]
    public async Task A_REQUIRED_closure_field_does_not_block_an_ordinary_create()
    {
        /*
         * ⚠ THE SABOTAGE THIS TEST CATCHES: removing the Entry-only filter in
         * `TaskFieldDefinitionService.ValidateAndMaterializeAsync` turns this red — every create of this type
         * would then refuse with TASK_FIELD_VALUE_INVALID for a field the create form never drew.
         */
        var definitions = new FakeTaskFieldDefinitionRepository(ClosureField("closure.note", required: true));
        var service = new TaskFieldDefinitionService(definitions, TaskRecordSourceDoubles.None, TaskActors.PermitAll());

        var result = await service.ValidateAndMaterializeAsync(values: null, enforceRequired: true);

        Assert.True(result.IsValid);
        Assert.Empty(result.Values);
    }

    [Fact]
    public async Task A_REQUIRED_entry_field_still_blocks_create__the_existing_rule_is_untouched()
    {
        var definitions = new FakeTaskFieldDefinitionRepository(EntryField("entry.phase", required: true));
        var service = new TaskFieldDefinitionService(definitions, TaskRecordSourceDoubles.None, TaskActors.PermitAll());

        var result = await service.ValidateAndMaterializeAsync(values: null, enforceRequired: true);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldValueInvalid, result.ReasonCode);
    }

    // ── §4 — closure-field validation at complete/cancel ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_REQUIRED_closure_field_with_no_value_and_nothing_stored_is_refused()
    {
        var definitions = new FakeTaskFieldDefinitionRepository(ClosureField("closure.note", required: true));
        var service = new TaskFieldDefinitionService(definitions, TaskRecordSourceDoubles.None, TaskActors.PermitAll());

        var result = await service.ValidateClosureFieldsAsync(values: null, existingValues: []);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.ClosureFieldRequired, result.ReasonCode);
    }

    [Fact]
    public async Task A_REQUIRED_closure_field_already_stored_from_an_earlier_attempt_is_NOT_re_demanded()
    {
        // Idempotent retry: a type that also demands an outcome must not force the closure field back into a
        // retry that is only correcting the outcome.
        var definitions = new FakeTaskFieldDefinitionRepository(ClosureField("closure.note", required: true));
        var service = new TaskFieldDefinitionService(definitions, TaskRecordSourceDoubles.None, TaskActors.PermitAll());
        var existing = new List<TaskFieldValue>
        {
            new() { DefinitionCode = "closure.note", ValueType = TaskFieldValueType.Text, Value = "already given" }
        };

        var result = await service.ValidateClosureFieldsAsync(values: null, existingValues: existing);

        Assert.True(result.IsValid);
        Assert.Empty(result.Values);
    }

    [Fact]
    public async Task A_supplied_value_satisfies_the_requirement_and_materializes()
    {
        var definitions = new FakeTaskFieldDefinitionRepository(ClosureField("closure.note", required: true));
        var service = new TaskFieldDefinitionService(definitions, TaskRecordSourceDoubles.None, TaskActors.PermitAll());
        var supplied = new[] { new TaskFieldValueDto("closure.note", TaskFieldValueType.Text, "why it closed") };

        var result = await service.ValidateClosureFieldsAsync(supplied, existingValues: []);

        Assert.True(result.IsValid);
        var value = Assert.Single(result.Values);
        Assert.Equal("closure.note", value.DefinitionCode);
        Assert.Equal("why it closed", value.Value);
    }

    [Fact]
    public async Task An_ENTRY_field_s_code_is_not_a_valid_closure_field__the_two_vocabularies_do_not_mix()
    {
        var definitions = new FakeTaskFieldDefinitionRepository(EntryField("entry.phase", required: false));
        var service = new TaskFieldDefinitionService(definitions, TaskRecordSourceDoubles.None, TaskActors.PermitAll());
        var supplied = new[] { new TaskFieldValueDto("entry.phase", TaskFieldValueType.Text, "x") };

        var result = await service.ValidateClosureFieldsAsync(supplied, existingValues: []);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldDefinitionUnknown, result.ReasonCode);
    }

    [Fact]
    public async Task A_value_type_mismatch_is_refused()
    {
        var definitions = new FakeTaskFieldDefinitionRepository(ClosureField("closure.count", required: false, TaskFieldValueType.Number));
        var service = new TaskFieldDefinitionService(definitions, TaskRecordSourceDoubles.None, TaskActors.PermitAll());
        var supplied = new[] { new TaskFieldValueDto("closure.count", TaskFieldValueType.Text, "not a number") };

        var result = await service.ValidateClosureFieldsAsync(supplied, existingValues: []);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldValueInvalid, result.ReasonCode);
    }

    // ── The transition handler, end to end ───────────────────────────────────────────────────────────────────

    private static TransitionTaskItemHandler Handler(
        FakeTaskItemRepository tasks, ITaskFieldDefinitionService fieldDefinitions)
        => new(
            tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
            new FakeChecklistRunRepository(), new TaskChecklistService(), new FakeWorkflowTransitionGate(),
            new FakeTaskDependencyRepository(), new FakeTaskTypeRepository(), new FakeTaskNotificationService(),
            fieldDefinitions, new FakeTaskAttachmentRepository(), NullLogger<TransitionTaskItemHandler>.Instance);

    [Fact]
    public async Task Completing_without_a_required_closure_field_is_refused_with_the_pack_s_own_code()
    {
        var task = OpenTask();
        var tasks = new FakeTaskItemRepository(task);
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(ClosureField("closure.note", required: true)),
            TaskRecordSourceDoubles.None, TaskActors.PermitAll());

        var result = await Handler(tasks, service).Handle(
            new TransitionTaskItemCommand(task.Id, TaskLifecycle.Done, new TaskTransitionRequest(task.Version, null, null), "corr"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ClosureFieldRequired, result.ReasonCode);
        Assert.Null(task.CompletedAt); // refused BEFORE any state moved
    }

    [Fact]
    public async Task Completing_WITH_the_closure_field_stores_it_and_preserves_entry_stage_values()
    {
        var task = OpenTask();
        task.FieldValues.Add(new TaskFieldValue { DefinitionCode = "entry.phase", ValueType = TaskFieldValueType.Text, Value = "Faz II" });
        var tasks = new FakeTaskItemRepository(task);
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(ClosureField("closure.note", required: true), EntryField("entry.phase", false)),
            TaskRecordSourceDoubles.None, TaskActors.PermitAll());

        var result = await Handler(tasks, service).Handle(
            new TransitionTaskItemCommand(
                task.Id, TaskLifecycle.Done,
                new TaskTransitionRequest(
                    task.Version, null, null,
                    ClosureFieldValues: [new TaskFieldValueDto("closure.note", TaskFieldValueType.Text, "root cause found")]),
                "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(task.CompletedAt);
        Assert.Equal("root cause found", task.FieldValues.Single(v => v.DefinitionCode == "closure.note").Value);
        // The entry-stage value from BEFORE closure survives — this is a MERGE, never a replace.
        Assert.Equal("Faz II", task.FieldValues.Single(v => v.DefinitionCode == "entry.phase").Value);
    }

    [Fact]
    public async Task A_closure_value_already_on_the_task_is_REPLACED__never_duplicated__by_the_one_just_supplied()
    {
        // Simulates a retry: the code already carries a value — from a prior attempt the outcome check refused,
        // say — and this close supplies a different one for the SAME code.
        var task = OpenTask();
        task.FieldValues.Add(new TaskFieldValue { DefinitionCode = "closure.note", ValueType = TaskFieldValueType.Text, Value = "first draft" });
        var tasks = new FakeTaskItemRepository(task);
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(ClosureField("closure.note", required: false)),
            TaskRecordSourceDoubles.None, TaskActors.PermitAll());

        var result = await Handler(tasks, service).Handle(
            new TransitionTaskItemCommand(
                task.Id, TaskLifecycle.Done,
                new TaskTransitionRequest(
                    task.Version, null, null,
                    ClosureFieldValues: [new TaskFieldValueDto("closure.note", TaskFieldValueType.Text, "final")]),
                "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("final", Assert.Single(task.FieldValues.Where(v => v.DefinitionCode == "closure.note")).Value);
    }

    // ── ClosureNote — the narrative, and its copy on the transition log ─────────────────────────────────────

    [Fact]
    public async Task A_closing_note_lands_on_the_task_AND_on_the_transition_log()
    {
        /*
         * ⚠ MEASURED BEFORE THIS SLICE: `TransitionTaskItemHandler` declared `reason: null` unconditionally for
         * complete/cancel, so a Note the client sent reached neither the task nor the log — only the
         * RequiresReason presence check ever read it. Every other act with a reason in the actor's own words
         * (wait, return, reassign) already copies it to the log; this closes the one gap.
         */
        var task = OpenTask();
        var tasks = new FakeTaskItemRepository(task);
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll());

        var result = await Handler(tasks, service).Handle(
            new TransitionTaskItemCommand(
                task.Id, TaskLifecycle.Done, new TaskTransitionRequest(task.Version, null, "  it's done, signed off  "), "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("it's done, signed off", task.ClosureNote);
        var entry = Assert.Single(tasks.Transitions.Events);
        Assert.Equal(TaskTransitionKind.Completed, entry.Kind);
        Assert.Equal("it's done, signed off", entry.Reason);
    }

    [Fact]
    public async Task A_non_closing_transition_never_writes_a_closure_note_even_when_it_carries_a_Note()
    {
        var task = OpenTask();
        var tasks = new FakeTaskItemRepository(task);
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll());

        await Handler(tasks, service).Handle(
            new TransitionTaskItemCommand(
                task.Id, TaskLifecycle.Waiting, new TaskTransitionRequest(task.Version, null, "parked for now"), "corr"),
            CancellationToken.None);

        Assert.Null(task.ClosureNote);
    }

    [Fact]
    public async Task The_closing_note_is_refused_past_four_thousand_characters()
    {
        var task = OpenTask();
        var tasks = new FakeTaskItemRepository(task);
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll());
        var tooLong = new string('x', TaskFieldLimits.MaxDescriptionLength + 1);

        var result = await Handler(tasks, service).Handle(
            new TransitionTaskItemCommand(task.Id, TaskLifecycle.Done, new TaskTransitionRequest(task.Version, null, tooLong), "corr"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ClosureNoteTooLong, result.ReasonCode);
        Assert.Null(task.ClosureNote);
    }

    // ── The projection: closure.note / closure.fields / closure.deliverables ───────────────────────────────────

    [Fact]
    public void The_read_model_carries_the_stored_stage()
    {
        var definition = ClosureField("closure.note", required: true);

        var dto = TaskFieldDefinitionMapper.ToDto(definition);

        Assert.Equal("Closure", dto.Stage);
    }

    [Fact]
    public async Task A_legacy_field_definition_without_Stage_reads_as_Entry_against_a_REAL_mongod()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Tenant);
        var repository = new TaskFieldDefinitionRepository(new PlatformDbContext(mongo.Client, database), tenantContext);

        var definition = EntryField("legacy.code", false);
        await repository.CreateAsync(definition);

        var collection = database.GetCollection<BsonDocument>(PlatformCollections.TaskFieldDefinitions);
        var byCode = Builders<BsonDocument>.Filter.Eq(nameof(TaskFieldDefinition.Code), "legacy.code");
        Assert.True((await collection.Find(byCode).SingleAsync()).Contains(nameof(TaskFieldDefinition.Stage)));
        await collection.UpdateOneAsync(byCode, Builders<BsonDocument>.Update.Unset(nameof(TaskFieldDefinition.Stage)));

        var read = await repository.GetByIdAsync(definition.Id);

        Assert.NotNull(read);
        Assert.Equal(TaskFieldStage.Entry, read!.Stage);
    }

    [Fact]
    public async Task A_legacy_task_without_ClosureNote_reads_as_null_against_a_REAL_mongod()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Tenant);
        var context = new PlatformDbContext(mongo.Client, database);
        var repository = new TaskItemRepository(context, tenantContext, new TaskTransitionRepository(context, tenantContext));

        var task = OpenTask();
        task.ClosureNote = "will be removed";
        await repository.CreateAsync(task);

        var collection = database.GetCollection<BsonDocument>(PlatformCollections.TaskItems);
        var byId = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(task.Id, GuidRepresentation.Standard));
        Assert.True((await collection.Find(byId).SingleAsync()).Contains(nameof(TaskItem.ClosureNote)));
        await collection.UpdateOneAsync(byId, Builders<BsonDocument>.Update.Unset(nameof(TaskItem.ClosureNote)));

        var read = await repository.GetByIdAsync(task.Id);

        Assert.NotNull(read);
        Assert.Null(read!.ClosureNote);
    }
}
