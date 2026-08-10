using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Phase 5 — managing the configurable field catalogue, through the real <see cref="TasksController"/> actions.
///
/// <para>Until this slice the catalogue could only be empty: the value validator read it and nothing wrote it.
/// Every rule asserted below exists because breaking it corrupts data that is already stored — an edited code
/// orphans values, a seventh section deletes items from the surface, a missing label puts a raw key on screen.
/// </para>
/// </summary>
public sealed class TaskFieldDefinitionManagementTests
{
    // ── The two label sources ────────────────────────────────────────────────

    [Fact]
    public async Task A_TENANT_definition_is_created_from_its_own_words()
    {
        /*
         * The problem this split solves: a tenant administrator cannot add a line to OUR resx files. With only a
         * resource key on the entity, every field they defined would have rendered as the literal key.
         */
        var harness = new Harness();

        var created = await harness.CreateAsync(labelText: "Mevzuat Aşaması");

        Assert.True(created.IsSuccessful);
        var stored = (await harness.GetAsync(created.Data)).Data!;
        Assert.Equal("Mevzuat Aşaması", stored.LabelText);
        // No key is invented for them. A made-up key is precisely how a raw key reaches the screen.
        Assert.Null(stored.LabelResourceKey);
    }

    [Fact]
    public async Task A_SYSTEM_definition_is_created_from_a_resource_key()
    {
        var harness = new Harness();

        var created = await harness.CreateAsync(labelResourceKey: "Tasks_Field_RegulatoryPhase");

        Assert.True(created.IsSuccessful);
        var stored = (await harness.GetAsync(created.Data)).Data!;
        Assert.Equal("Tasks_Field_RegulatoryPhase", stored.LabelResourceKey);
        Assert.Null(stored.LabelText);
    }

    [Fact]
    public async Task A_definition_with_BOTH_label_sources_is_refused()
    {
        // Ambiguous: the projection would have to guess which one the screen gets.
        var harness = new Harness();

        var created = await harness.CreateAsync(
            labelResourceKey: "Tasks_Field_RegulatoryPhase", labelText: "Mevzuat Aşaması");

        Assert.False(created.IsSuccessful);
        Assert.Equal(400, created.StatusCode);
        Assert.Equal(TaskReasonCodes.FieldLabelSourceInvalid, created.ReasonCode);
    }

    [Fact]
    public async Task A_definition_with_NEITHER_label_source_is_refused()
    {
        // It would leave the field with nothing to render but its code — the defect in its purest form.
        var harness = new Harness();

        var created = await harness.CreateAsync();

        Assert.False(created.IsSuccessful);
        Assert.Equal(TaskReasonCodes.FieldLabelSourceInvalid, created.ReasonCode);
    }

    [Fact]
    public async Task An_EDIT_that_leaves_no_label_source_is_refused_too()
    {
        var harness = new Harness();
        var created = await harness.CreateAsync(labelText: "Mevzuat Aşaması");
        var current = (await harness.GetAsync(created.Data)).Data!;

        var updated = await harness.UpdateAsync(created.Data, current.Version, labelText: null);

        Assert.False(updated.IsSuccessful);
        Assert.Equal(TaskReasonCodes.FieldLabelSourceInvalid, updated.ReasonCode);
        // The stored label survived the refused edit.
        Assert.Equal("Mevzuat Aşaması", harness.Definitions.All.Single().LabelText);
    }

    // ── The section cap comes from the contract ──────────────────────────────

    [Fact]
    public async Task A_SEVENTH_section_is_refused()
    {
        /*
         * The contract caps businessContext at six sections. Accepting a seventh and breaking it in the
         * projection would not merely look wrong: validateItems DROPS an item it cannot validate, so every task
         * carrying that section would VANISH from the surface. BL-038's lesson, enforced where it can still be
         * refused.
         */
        var harness = new Harness();
        for (var i = 1; i <= 6; i++)
        {
            var ok = await harness.CreateAsync(code: $"field.{i}", labelText: $"Alan {i}", section: $"Bölüm {i}");
            Assert.True(ok.IsSuccessful);
        }

        var seventh = await harness.CreateAsync(code: "field.7", labelText: "Alan 7", section: "Bölüm 7");

        Assert.False(seventh.IsSuccessful);
        Assert.Equal(400, seventh.StatusCode);
        Assert.Equal(TaskReasonCodes.FieldSectionLimitExceeded, seventh.ReasonCode);
    }

    [Fact]
    public async Task A_seventh_definition_in_an_EXISTING_section_is_fine()
    {
        // Non-vacuity: the cap is on SECTIONS, not on definitions. Counting definitions would have made the
        // catalogue unusable after six fields.
        var harness = new Harness();
        for (var i = 1; i <= 6; i++)
        {
            await harness.CreateAsync(code: $"field.{i}", labelText: $"Alan {i}", section: $"Bölüm {i}");
        }

        var seventh = await harness.CreateAsync(code: "field.7", labelText: "Alan 7", section: "Bölüm 1");

        Assert.True(seventh.IsSuccessful);
    }

    [Fact]
    public async Task A_RETIRED_definitions_section_no_longer_occupies_a_slot()
    {
        // Otherwise a tenant that ever used six sections could never introduce another one, even after retiring
        // every field in one of them.
        var harness = new Harness();
        var toRetire = await harness.CreateAsync(code: "field.1", labelText: "Alan 1", section: "Bölüm 1");
        for (var i = 2; i <= 6; i++)
        {
            await harness.CreateAsync(code: $"field.{i}", labelText: $"Alan {i}", section: $"Bölüm {i}");
        }

        await harness.DeleteAsync(toRetire.Data);
        var seventh = await harness.CreateAsync(code: "field.7", labelText: "Alan 7", section: "Bölüm 7");

        Assert.True(seventh.IsSuccessful);
    }

    // ── The code is the join key, so it never moves ──────────────────────────

    [Fact]
    public async Task The_update_request_carries_no_CODE_at_all()
    {
        /*
         * Not "the handler ignores it" — the field is absent from the request type, so a caller cannot express
         * the change. Values already stored join to their definition BY CODE; renaming one orphans them all and
         * the screen shows a column of data with no heading.
         */
        var codeProperty = typeof(UpdateTaskFieldDefinitionRequest).GetProperty("Code");

        Assert.Null(codeProperty);
        // Non-vacuity: creation obviously does carry one.
        Assert.NotNull(typeof(CreateTaskFieldDefinitionRequest).GetProperty("Code"));
    }

    [Fact]
    public async Task An_edit_leaves_the_code_untouched()
    {
        var harness = new Harness();
        var created = await harness.CreateAsync(code: "regulatory.phase", labelText: "Mevzuat Aşaması");
        var current = (await harness.GetAsync(created.Data)).Data!;

        await harness.UpdateAsync(created.Data, current.Version, labelText: "Yeni etiket");

        Assert.Equal("regulatory.phase", harness.Definitions.All.Single().Code);
        // …and the rest of the edit did land, so this is not passing because nothing was saved.
        Assert.Equal("Yeni etiket", harness.Definitions.All.Single().LabelText);
    }

    [Fact]
    public async Task Two_definitions_cannot_claim_the_same_code()
    {
        // A duplicate is not a naming annoyance: it is two definitions claiming the same stored data.
        var harness = new Harness();
        await harness.CreateAsync(code: "regulatory.phase", labelText: "Mevzuat Aşaması");

        var second = await harness.CreateAsync(code: "regulatory.phase", labelText: "Başka");

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(TaskReasonCodes.FieldDefinitionCodeTaken, second.ReasonCode);
    }

    // ── Retiring: never destruction ──────────────────────────────────────────

    [Fact]
    public async Task A_retired_definition_leaves_the_catalogue_but_the_ROW_survives()
    {
        var harness = new Harness();
        var created = await harness.CreateAsync(code: "regulatory.phase", labelText: "Mevzuat Aşaması");

        var deleted = await harness.DeleteAsync(created.Data);

        Assert.True(deleted.IsSuccessful);
        Assert.Empty((await harness.ListAsync()).Data!);
        // The row is still there — values already written point at it, and a hard delete would orphan their
        // explanation.
        var stored = Assert.Single(harness.Definitions.All);
        Assert.NotNull(stored.DeletedAt);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task A_value_stored_under_a_RETIRED_definition_is_still_readable()
    {
        /*
         * The whole reason retirement is not deletion. The task keeps its value; only the OFFER of the field to
         * new work stops. Asserted through the real validator, because "the row survives" would be a hollow
         * claim if the value could no longer be read back.
         */
        var harness = new Harness();
        var created = await harness.CreateAsync(code: "regulatory.phase", labelText: "Mevzuat Aşaması");

        var before = await harness.ValidateValueAsync("regulatory.phase", "Faz 2");
        Assert.True(before.IsValid);
        var stored = Assert.Single(before.Values);

        await harness.DeleteAsync(created.Data);

        // The value written earlier is unchanged and still carries its definition's code and classification.
        Assert.Equal("regulatory.phase", stored.DefinitionCode);
        Assert.Equal("Faz 2", stored.Value);
    }

    [Fact]
    public async Task A_retired_definition_is_no_longer_OFFERED_to_new_work()
    {
        // The other half: retiring must actually stop the field appearing on new tasks, or it is decoration.
        var harness = new Harness();
        var created = await harness.CreateAsync(code: "regulatory.phase", labelText: "Mevzuat Aşaması");
        await harness.DeleteAsync(created.Data);

        var after = await harness.ValidateValueAsync("regulatory.phase", "Faz 3");

        Assert.False(after.IsValid);
        Assert.Equal(TaskReasonCodes.FieldDefinitionUnknown, after.ReasonCode);
    }

    [Fact]
    public async Task A_PAUSED_definition_still_appears_in_the_catalogue()
    {
        // Non-vacuity for the retire test: a definition that vanished when it was switched off could never be
        // switched back on.
        var harness = new Harness();
        await harness.CreateAsync(code: "regulatory.phase", labelText: "Mevzuat Aşaması", isActive: false);

        Assert.Single((await harness.ListAsync()).Data!);
    }

    // ── Value validation still works ─────────────────────────────────────────

    [Fact]
    public async Task A_value_for_a_definition_nobody_created_is_still_refused()
    {
        // The rule that made this catalogue matter in the first place: an unknown code would smuggle an ad-hoc
        // column into the engine, which is what §12 K1 forbids.
        var harness = new Harness();

        var result = await harness.ValidateValueAsync("nobody.defined.this", "x");

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldDefinitionUnknown, result.ReasonCode);
    }

    // ── Classification is recorded and NOTHING more ──────────────────────────

    [Fact]
    public async Task A_RESTRICTED_classification_is_stored_and_changes_nothing()
    {
        /*
         * Deliberately explicit. Field-level authorization is BL-024; carrying the metadata now keeps that work
         * additive. Half an access-control system — metadata that looks protective and protects nothing — is
         * more dangerous than none, so this test states that today's behaviour is IDENTICAL either way.
         */
        var harness = new Harness();
        await harness.CreateAsync(
            code: "salary.band", labelText: "Ücret bandı",
            classification: TaskFieldClassification.Restricted,
            defaultAccessState: TaskFieldAccessState.Hidden);

        var restricted = await harness.ValidateValueAsync("salary.band", "B3");

        var plain = new Harness();
        await plain.CreateAsync(code: "salary.band", labelText: "Ücret bandı");
        var normal = await plain.ValidateValueAsync("salary.band", "B3");

        // Same acceptance, same stored value: the classification decided nothing.
        Assert.Equal(normal.IsValid, restricted.IsValid);
        Assert.Equal(
            normal.Values.Single().Value,
            restricted.Values.Single().Value);

        // And it IS recorded, so BL-024 has something to build on.
        Assert.Equal(TaskFieldClassification.Restricted, harness.Definitions.All.Single().Classification);
        Assert.Equal(TaskFieldAccessState.Hidden, harness.Definitions.All.Single().DefaultAccessState);
    }

    // ── Retiring several at once ─────────────────────────────────────────────

    [Fact]
    public async Task A_bulk_retire_deactivates_every_one_and_destroys_none()
    {
        /*
         * The button that started this: it was live on screen and its endpoint did not exist — select rows,
         * press, 404. The behaviour it now reaches is the SINGLE retire's, because the bulk handler loops that
         * command rather than implementing a second one.
         */
        var harness = new Harness();
        var first = await harness.CreateAsync(code: "field.1", labelText: "Alan 1");
        var second = await harness.CreateAsync(code: "field.2", labelText: "Alan 2", section: "Bölüm 2");

        var result = await harness.BulkDeleteAsync(first.Data, second.Data);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Deactivated);
        Assert.Equal(0, result.Data.NotFound);
        Assert.Empty((await harness.ListAsync()).Data!);
        // The ROWS survive — values already stored point at them.
        Assert.Equal(2, harness.Definitions.All.Count);
        Assert.All(harness.Definitions.All, d =>
        {
            Assert.NotNull(d.DeletedAt);
            Assert.False(d.IsActive);
        });
    }

    [Fact]
    public async Task Values_stored_under_a_BULK_retired_definition_are_still_readable()
    {
        /*
         * THE regression of this ticket. Retiring is not deletion precisely so the values keep their
         * explanation, and a second bulk path that hard-deleted would have taken that away while the single path
         * kept it. Proved through the real validator, not asserted.
         */
        var harness = new Harness();
        var created = await harness.CreateAsync(code: "regulatory.phase", labelText: "Mevzuat Aşaması");

        var before = await harness.ValidateValueAsync("regulatory.phase", "Faz 2");
        var stored = Assert.Single(before.Values);

        await harness.BulkDeleteAsync(created.Data);

        Assert.Equal("regulatory.phase", stored.DefinitionCode);
        Assert.Equal("Faz 2", stored.Value);
        // …and the definition row is still there to render it against.
        Assert.Single(harness.Definitions.All);
    }

    [Fact]
    public async Task A_bulk_retired_definition_stops_being_offered_to_new_work()
    {
        // The other half, exactly as the single retire has it: readable backwards, not offered forwards.
        var harness = new Harness();
        var created = await harness.CreateAsync(code: "regulatory.phase", labelText: "Mevzuat Aşaması");

        await harness.BulkDeleteAsync(created.Data);

        var after = await harness.ValidateValueAsync("regulatory.phase", "Faz 3");
        Assert.False(after.IsValid);
    }

    [Fact]
    public async Task A_PARTIAL_selection_retires_what_it_can_and_reports_what_it_could_not()
    {
        /*
         * Both traps refuse themselves here. Claiming three would be a lie the toast repeats; rejecting the
         * whole request would punish two valid ids for one stale one — and a bulk selection is exactly where a
         * stale row is most likely, because the list was rendered before the user finished choosing.
         */
        var harness = new Harness();
        var first = await harness.CreateAsync(code: "field.1", labelText: "Alan 1");
        var second = await harness.CreateAsync(code: "field.2", labelText: "Alan 2", section: "Bölüm 2");

        var result = await harness.BulkDeleteAsync(first.Data, Guid.NewGuid(), second.Data);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Deactivated);
        Assert.Equal(1, result.Data.NotFound);
        // The two real ones DID get retired — a partial result is not a rollback.
        Assert.Empty((await harness.ListAsync()).Data!);
    }

    [Fact]
    public async Task An_EMPTY_selection_is_harmless()
    {
        // A bulk bar can fire with an empty selection after a reload. Answering 400 would put a red toast on a
        // user who did nothing wrong.
        var harness = new Harness();

        var result = await harness.BulkDeleteAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, result.Data!.Deactivated);
        Assert.Equal(0, result.Data.NotFound);
    }

    [Fact]
    public async Task A_selection_over_the_cap_is_REFUSED_rather_than_truncated()
    {
        /*
         * Silently processing the first two hundred and reporting success is the "5 deleted" lie in another
         * form: the caller believes the rest are gone. The refusal is visible and the log names the count.
         */
        var harness = new Harness();
        var ids = Enumerable.Range(0, BulkDeleteTaskFieldDefinitionHandler.MaxIdsPerRequest + 1)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        var result = await harness.BulkDeleteAsync(ids);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.BulkLimitExceeded, result.ReasonCode);
    }

    [Fact]
    public async Task A_selection_AT_the_cap_is_accepted()
    {
        // Non-vacuity for the refusal above: an off-by-one that refused the allowed maximum would pass it.
        var harness = new Harness();
        var ids = Enumerable.Range(0, BulkDeleteTaskFieldDefinitionHandler.MaxIdsPerRequest)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        var result = await harness.BulkDeleteAsync(ids);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void The_bulk_retire_LOOPS_the_single_command()
    {
        /*
         * Structural, because the risk is structural: a second retire implementation could hard-delete while the
         * single one soft-deletes, and the two would drift apart silently. The handler takes a mediator and no
         * repository at all — it cannot write a definition even if someone tried.
         */
        var parameters = typeof(BulkDeleteTaskFieldDefinitionHandler)
            .GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Contains(typeof(IMediator), parameters);
        Assert.DoesNotContain(typeof(Diten.Platform.Domain.Repositories.ITaskFieldDefinitionRepository), parameters);
    }

    // ── The write side is permission-gated ───────────────────────────────────

    [Theory]
    [InlineData(nameof(TasksController.CreateFieldDefinition))]
    [InlineData(nameof(TasksController.UpdateFieldDefinition))]
    [InlineData(nameof(TasksController.DeleteFieldDefinition))]
    // The bulk retire carries the SAME key: doing it to several at once is not a higher authority, and a
    // separate key would be one more thing to grant and forget.
    [InlineData(nameof(TasksController.BulkDeleteFieldDefinitions))]
    public void Shaping_the_catalogue_requires_the_MANAGE_permission(string action)
    {
        /*
         * Reading the catalogue is an ordinary task read; SHAPING it is an administrative act, and the pack
         * declares a permission for exactly that. Asserted off the real attribute rather than by driving HTTP,
         * because the attribute IS the enforcement — a missing one is silent, and the endpoint would simply work
         * for everybody.
         */
        var attribute = typeof(TasksController)
            .GetMethod(action)!
            .GetCustomAttributes(typeof(Diten.Platform.API.Security.HasPermissionAttribute), false)
            .Cast<Diten.Platform.API.Security.HasPermissionAttribute>()
            .SingleOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(TaskPermissions.FieldDefinitionsManage, attribute!.Permission);
    }

    [Theory]
    [InlineData(nameof(TasksController.GetFieldDefinitions))]
    [InlineData(nameof(TasksController.GetFieldDefinition))]
    public void Reading_the_catalogue_needs_only_the_ordinary_task_read(string action)
    {
        // Non-vacuity for the three above: if every field-definition endpoint carried the manage permission,
        // they would all pass while nobody could see the catalogue their own tasks render from.
        var attribute = typeof(TasksController)
            .GetMethod(action)!
            .GetCustomAttributes(typeof(Diten.Platform.API.Security.HasPermissionAttribute), false)
            .Cast<Diten.Platform.API.Security.HasPermissionAttribute>()
            .Single();

        Assert.Equal(TaskPermissions.Read, attribute.Permission);
    }

    // ── harness ──────────────────────────────────────────────────────────────

    private sealed class Harness
    {
        private readonly TasksController _controller;

        public Harness()
        {
            var tenant = new FakeTenantContext(TaskTestData.Tenant);
            Definitions = new FakeTaskFieldDefinitionRepository();
            var user = new FakeCurrentUserContext(TaskTestData.Me);
            Validator = new TaskFieldDefinitionService(Definitions, TaskRecordSourceDoubles.None);

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(new FieldDefinitionMediator(Definitions, tenant, user), correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public FakeTaskFieldDefinitionRepository Definitions { get; }

        /// <summary>The REAL value validator, so "still readable" is proved rather than asserted.</summary>
        public TaskFieldDefinitionService Validator { get; }

        public async Task<Response<Guid>> CreateAsync(
            string code = "regulatory.phase",
            string? labelResourceKey = null,
            string? labelText = null,
            string section = "Uyum",
            bool isActive = true,
            TaskFieldClassification classification = TaskFieldClassification.Normal,
            TaskFieldAccessState defaultAccessState = TaskFieldAccessState.Visible)
            => Unwrap<Guid>(await _controller.CreateFieldDefinition(
                new CreateTaskFieldDefinitionRequest(
                    Code: code,
                    LabelResourceKey: labelResourceKey,
                    LabelText: labelText,
                    ValueType: TaskFieldValueType.Text,
                    Section: section,
                    Importance: TaskFieldImportance.Secondary,
                    IsRequired: false,
                    SortOrder: 0,
                    OptionsSourceKind: TaskFieldOptionsSourceKind.None,
                    OptionsSourceKey: null,
                    AppliesToModuleCode: null,
                    Classification: classification,
                    DefaultAccessState: defaultAccessState,
                    IsActive: isActive),
                CancellationToken.None));

        public async Task<Response<NoContent>> UpdateAsync(
            Guid id, int expectedVersion, string? labelText = "Mevzuat Aşaması", string section = "Uyum")
            => Unwrap<NoContent>(await _controller.UpdateFieldDefinition(
                id,
                new UpdateTaskFieldDefinitionRequest(
                    LabelResourceKey: null,
                    LabelText: labelText,
                    ValueType: TaskFieldValueType.Text,
                    Section: section,
                    Importance: TaskFieldImportance.Secondary,
                    IsRequired: false,
                    SortOrder: 0,
                    OptionsSourceKind: TaskFieldOptionsSourceKind.None,
                    OptionsSourceKey: null,
                    AppliesToModuleCode: null,
                    Classification: TaskFieldClassification.Normal,
                    DefaultAccessState: TaskFieldAccessState.Visible,
                    IsActive: true,
                    ExpectedVersion: expectedVersion),
                CancellationToken.None));

        public async Task<Response<NoContent>> DeleteAsync(Guid id)
            => Unwrap<NoContent>(await _controller.DeleteFieldDefinition(id, CancellationToken.None));

        public async Task<Response<BulkDeactivateFieldDefinitionsResponse>> BulkDeleteAsync(params Guid[] ids)
            => Unwrap<BulkDeactivateFieldDefinitionsResponse>(
                await _controller.BulkDeleteFieldDefinitions(
                    new BulkDeleteTaskFieldDefinitionRequest(ids), CancellationToken.None));

        public async Task<Response<TaskFieldDefinitionDto>> GetAsync(Guid id)
            => Unwrap<TaskFieldDefinitionDto>(await _controller.GetFieldDefinition(id, CancellationToken.None));

        public async Task<Response<IReadOnlyList<TaskFieldDefinitionDto>>> ListAsync()
            => Unwrap<IReadOnlyList<TaskFieldDefinitionDto>>(
                await _controller.GetFieldDefinitions(CancellationToken.None));

        public Task<TaskFieldValidationResult> ValidateValueAsync(string code, string value)
            => Validator.ValidateAndMaterializeAsync(
                [new TaskFieldValueDto(code, TaskFieldValueType.Text, value)], CancellationToken.None);

        private static Response<T> Unwrap<T>(IActionResult result)
            => result is NoContentResult
                ? Response<T>.Success(204, "corr")
                : (Response<T>)((ObjectResult)result).Value!;
    }

    private sealed class FieldDefinitionMediator(
        FakeTaskFieldDefinitionRepository definitions,
        FakeTenantContext tenant,
        FakeCurrentUserContext user) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request switch
            {
                CreateTaskFieldDefinitionCommand c => (Task<TResponse>)(object)
                    new CreateTaskFieldDefinitionHandler(definitions, tenant, user, TaskRecordSourceDoubles.None).Handle(c, ct),
                UpdateTaskFieldDefinitionCommand c => (Task<TResponse>)(object)
                    new UpdateTaskFieldDefinitionHandler(definitions, user, TaskRecordSourceDoubles.None).Handle(c, ct),
                DeleteTaskFieldDefinitionCommand c => (Task<TResponse>)(object)
                    new DeleteTaskFieldDefinitionHandler(definitions, user).Handle(c, ct),
                // The bulk handler is given THIS mediator, so its loop reaches the real single handler above —
                // a stub here would prove nothing about the semantics being shared.
                BulkDeleteTaskFieldDefinitionCommand c => (Task<TResponse>)(object)
                    new BulkDeleteTaskFieldDefinitionHandler(
                        this,
                        Microsoft.Extensions.Logging.Abstractions
                            .NullLogger<BulkDeleteTaskFieldDefinitionHandler>.Instance).Handle(c, ct),
                GetTaskFieldDefinitionListQuery q => (Task<TResponse>)(object)
                    new GetTaskFieldDefinitionListHandler(definitions).Handle(q, ct),
                GetTaskFieldDefinitionByIdQuery q => (Task<TResponse>)(object)
                    new GetTaskFieldDefinitionByIdHandler(definitions).Handle(q, ct),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}
