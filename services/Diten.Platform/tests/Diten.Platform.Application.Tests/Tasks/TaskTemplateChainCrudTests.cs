using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-054 — the template chain, through the real <see cref="TasksController"/> actions.
///
/// <para><b>What was broken, and it was one missing link.</b> A recurrence rule generated a task with a title and
/// nothing else — no priority, no due date, no checklist — because the rule's TEMPLATE picker had no source: the
/// entity, the lookup endpoint and the picker all existed, and there was nowhere to CREATE a template. These are
/// the two screens' write paths, and they are tested through the controller because that is the only surface a
/// template can be defined from.</para>
///
/// <para><b>Checklist templates come first in this file, as they do in the slice.</b> The task-template form
/// carries a checklist picker; building it before its source would repeat the same defect one level in.</para>
/// </summary>
public sealed class TaskTemplateChainCrudTests
{
    // ── Checklist templates ──────────────────────────────────────────────────

    [Fact]
    public async Task A_checklist_template_can_be_created_read_back_and_listed()
    {
        var harness = new Harness();

        var created = await harness.CreateChecklistAsync(code: "qa-release");

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);

        var fetched = await harness.GetChecklistAsync(created.Data);
        Assert.True(fetched.IsSuccessful);
        // Codes are stored in ONE canonical spelling, so `qa-release` and `QA-Release` cannot become two
        // templates nobody can tell apart.
        Assert.Equal("QA-RELEASE", fetched.Data!.Code);
        Assert.Equal(2, fetched.Data.ItemCount);

        Assert.Single((await harness.ListChecklistsAsync()).Data!);
    }

    [Fact]
    public async Task A_checklist_template_with_NO_items_is_refused()
    {
        /*
         * An empty checklist instantiates an empty list onto every task bound to it — and on screen, an empty
         * checklist is indistinguishable from one that failed to load. The author believes they configured a
         * gate; the holder sees nothing and completes the task.
         */
        var harness = new Harness();

        var created = await harness.CreateChecklistAsync(items: []);

        Assert.False(created.IsSuccessful);
        Assert.Equal(400, created.StatusCode);
        Assert.Equal(TaskReasonCodes.ChecklistTemplateEmpty, created.ReasonCode);
    }

    [Fact]
    public async Task Two_checklist_items_sharing_a_CODE_are_refused()
    {
        // The code is the join key a ticked run item is matched back by. Two items sharing one make every later
        // tick, edit and removal ambiguous — and the ambiguity surfaces on a live task months later, not here.
        var harness = new Harness();

        var created = await harness.CreateChecklistAsync(items:
        [
            Item("step-1", "İlk adım"),
            Item("STEP-1", "Aynı kodu taşıyan ikinci adım")
        ]);

        Assert.False(created.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ChecklistItemCodeDuplicate, created.ReasonCode);
    }

    [Fact]
    public async Task A_second_checklist_template_with_the_same_CODE_is_refused()
    {
        var harness = new Harness();
        await harness.CreateChecklistAsync(code: "qa-release");

        // Different spelling, same code — the normalisation is what makes this a duplicate rather than a
        // second template with a confusingly similar name.
        var second = await harness.CreateChecklistAsync(code: "QA-Release");

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(TaskReasonCodes.ChecklistTemplateCodeTaken, second.ReasonCode);
    }

    [Fact]
    public async Task A_checklist_template_can_be_edited_and_its_items_are_renumbered()
    {
        var harness = new Harness();
        var created = await harness.CreateChecklistAsync(code: "qa-release");
        var current = (await harness.GetChecklistAsync(created.Data)).Data!;

        var updated = await harness.UpdateChecklistAsync(
            created.Data, current.Version, "QA-RELEASE",
            items:
            [
                // Sent with deliberately gapped and out-of-order numbers, as a client that removed a row would.
                Item("step-2", "İkinci adım", sortOrder: 40),
                Item("step-1", "İlk adım", sortOrder: 10),
                Item("step-3", "Üçüncü adım", sortOrder: 40)
            ]);

        Assert.True(updated.IsSuccessful);

        var after = (await harness.GetChecklistAsync(created.Data)).Data!;
        // Renumbered from the order they ARRIVED in: gaps and ties would let two steps sort by whichever comes
        // out of the driver first, so the same checklist reads differently on two screens.
        Assert.Equal(["step-2", "step-1", "step-3"], after.Items.Select(item => item.Code));
        Assert.Equal([0, 1, 2], after.Items.Select(item => item.SortOrder));
    }

    [Fact]
    public async Task An_edit_that_CHANGES_the_code_is_refused_rather_than_ignored()
    {
        // The form sends it read-only, so a different code is a client bug or a bypassed form. Quietly keeping
        // the stored value would report success for a change the caller asked for and did not get.
        var harness = new Harness();
        var created = await harness.CreateChecklistAsync(code: "qa-release");
        var current = (await harness.GetChecklistAsync(created.Data)).Data!;

        var updated = await harness.UpdateChecklistAsync(created.Data, current.Version, "SOMETHING-ELSE");

        Assert.False(updated.IsSuccessful);
        Assert.Equal(400, updated.StatusCode);
        Assert.Equal(TaskReasonCodes.TemplateCodeImmutable, updated.ReasonCode);
        Assert.Equal("QA-RELEASE", harness.Checklists.Store.Single().Code);
    }

    [Fact]
    public async Task A_checklist_edit_on_a_STALE_version_is_refused()
    {
        var harness = new Harness();
        var created = await harness.CreateChecklistAsync(code: "qa-release");

        var updated = await harness.UpdateChecklistAsync(created.Data, expectedVersion: 99, "QA-RELEASE");

        Assert.False(updated.IsSuccessful);
        Assert.Equal(409, updated.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, updated.ReasonCode);
    }

    [Fact]
    public async Task A_retired_checklist_template_leaves_the_list_and_stops_being_active()
    {
        // SOFT, and IsActive goes false with it: the row survives because task templates and live runs point at
        // it, and two independent readers ask two different questions about whether it still counts.
        var harness = new Harness();
        var created = await harness.CreateChecklistAsync(code: "qa-release");

        var deleted = await harness.DeleteChecklistAsync(created.Data);

        Assert.True(deleted.IsSuccessful);
        Assert.Empty((await harness.ListChecklistsAsync()).Data!);
        Assert.Equal(404, (await harness.GetChecklistAsync(created.Data)).StatusCode);

        var stored = harness.Checklists.Store.Single();
        Assert.NotNull(stored.DeletedAt);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task A_PAUSED_checklist_template_still_appears_in_the_list()
    {
        // Non-vacuity for the retire test, and a real requirement: a template that vanished when it was switched
        // off could never be switched back on.
        var harness = new Harness();
        var created = await harness.CreateChecklistAsync(code: "qa-release", isActive: false);

        Assert.Single((await harness.ListChecklistsAsync()).Data!);
        Assert.False((await harness.GetChecklistAsync(created.Data)).Data!.IsActive);
    }

    // ── The link: the picker the task-template form is filled from ───────────

    [Fact]
    public async Task The_checklist_LOOKUP_offers_what_was_just_created_and_drops_what_was_retired()
    {
        /*
         * ⚠ THE POINT OF THE WHOLE SLICE, one level in.
         *
         * The recurrence rule's template picker sat live and empty for a long time because nothing could create
         * its source. A task-template form shipped before this endpoint would be the identical control — and the
         * person filling it could not tell a missing endpoint from an empty tenant.
         */
        var harness = new Harness();
        Assert.Empty((await harness.ChecklistLookupAsync()).Data!);

        var kept = await harness.CreateChecklistAsync(code: "qa-release");
        var retired = await harness.CreateChecklistAsync(code: "obsolete");
        await harness.DeleteChecklistAsync(retired.Data);

        var offered = (await harness.ChecklistLookupAsync()).Data!;

        Assert.Single(offered);
        Assert.Equal(kept.Data, offered[0].Id);
        // The item count travels, so the picker can say how long a checklist is before it is chosen.
        Assert.Equal(2, offered[0].ItemCount);
    }

    // ── Task templates ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_task_template_can_be_created_read_back_and_listed()
    {
        var harness = new Harness();
        var checklist = await harness.CreateChecklistAsync(code: "qa-release");

        var created = await harness.CreateTemplateAsync(checklistTemplateId: checklist.Data);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);

        var fetched = (await harness.GetTemplateAsync(created.Data)).Data!;
        // Enums as STRINGS on the wire — the live Platform convention, and one an enum-as-number defect already
        // cost this module once.
        Assert.Equal("High", fetched.DefaultPriority);
        Assert.Equal("SelfAssigned", fetched.DefaultAssignmentTarget);
        Assert.Equal(checklist.Data, fetched.ChecklistTemplateId);
        Assert.Equal(3, fetched.DefaultDueInDays);

        Assert.Single((await harness.ListTemplatesAsync()).Data!);
    }

    [Fact]
    public async Task A_task_template_that_defaults_to_a_NAMED_PERSON_is_refused()
    {
        /*
         * A template carries a pool position and NO assignee field, so "assign to a person" names nobody — and
         * the generated task falls into the failure the recurrence rule already paid for once: work created for
         * Guid.Empty, in nobody's list, with its period consumed so it can never be regenerated.
         */
        var harness = new Harness();

        var created = await harness.CreateTemplateAsync(target: TaskAssignmentTarget.Person);

        Assert.False(created.IsSuccessful);
        Assert.Equal(400, created.StatusCode);
        Assert.Equal(TaskReasonCodes.TemplateAssignmentInvalid, created.ReasonCode);
    }

    [Fact]
    public async Task A_POOLED_task_template_needs_a_position()
    {
        var harness = new Harness();

        var created = await harness.CreateTemplateAsync(target: TaskAssignmentTarget.PositionPool);

        Assert.False(created.IsSuccessful);
        Assert.Equal(TaskReasonCodes.TemplateAssignmentInvalid, created.ReasonCode);
    }

    [Fact]
    public async Task A_task_template_naming_a_checklist_that_does_NOT_EXIST_is_refused()
    {
        /*
         * Refused at the WRITE, not at generation. A template bound to a checklist that cannot resolve produces
         * tasks with no gates at all, silently — it looks configured and does the opposite of what it says, and
         * nobody finds out until the gate has already been passed.
         */
        var harness = new Harness();

        var created = await harness.CreateTemplateAsync(checklistTemplateId: Guid.NewGuid());

        Assert.False(created.IsSuccessful);
        Assert.Equal(400, created.StatusCode);
        Assert.Equal(TaskReasonCodes.TemplateChecklistUnresolved, created.ReasonCode);
    }

    [Fact]
    public async Task A_task_template_naming_a_RETIRED_checklist_is_refused_too()
    {
        // Non-vacuity for the test above with a stronger claim: existence is not the check, offerability is.
        var harness = new Harness();
        var checklist = await harness.CreateChecklistAsync(code: "obsolete");
        await harness.DeleteChecklistAsync(checklist.Data);

        var created = await harness.CreateTemplateAsync(checklistTemplateId: checklist.Data);

        Assert.False(created.IsSuccessful);
        Assert.Equal(TaskReasonCodes.TemplateChecklistUnresolved, created.ReasonCode);
    }

    [Fact]
    public async Task A_task_template_with_NO_checklist_is_perfectly_legal()
    {
        // Non-vacuity for both refusals above: if the check simply refused everything, they would pass while
        // a plain reminder — a title and a due date, which is real work — became impossible to define.
        var harness = new Harness();

        var created = await harness.CreateTemplateAsync(checklistTemplateId: null);

        Assert.True(created.IsSuccessful);
        Assert.Null((await harness.GetTemplateAsync(created.Data)).Data!.ChecklistTemplateId);
    }

    [Fact]
    public async Task A_template_with_NO_legal_entity_means_EVERY_company()
    {
        /*
         * ⚠ ONE company or none — never a list. A multi-select rots: the day a new company is opened, every
         * template that should also cover it has to be found and edited one at a time, and nobody does that, so
         * the list silently comes to mean "the companies we had when somebody last looked".
         *
         * The contract enforces it by SHAPE: `LegalEntityId` is a single nullable Guid, so a set of companies
         * cannot be expressed at all. Null is the deliberate "all companies" answer, and it must survive the
         * round trip rather than being normalised into something.
         */
        var harness = new Harness();

        var everywhere = await harness.CreateTemplateAsync(legalEntityId: null);
        var oneCompany = await harness.CreateTemplateAsync(code: "TR-ONLY", legalEntityId: Harness.CompanyTr);

        Assert.Null((await harness.GetTemplateAsync(everywhere.Data)).Data!.LegalEntityId);
        Assert.Equal(Harness.CompanyTr, (await harness.GetTemplateAsync(oneCompany.Data)).Data!.LegalEntityId);
    }

    [Fact]
    public async Task An_EMPTY_legal_entity_guid_is_stored_as_all_companies_rather_than_company_zero()
    {
        // What a form's blank option actually posts. Storing Guid.Empty would make the template belong to a
        // company that does not exist, and it would then match nothing at all rather than everything.
        var harness = new Harness();

        var created = await harness.CreateTemplateAsync(legalEntityId: Guid.Empty);

        Assert.Null((await harness.GetTemplateAsync(created.Data)).Data!.LegalEntityId);
    }

    [Fact]
    public async Task A_second_task_template_with_the_same_CODE_is_refused()
    {
        var harness = new Harness();
        await harness.CreateTemplateAsync(code: "monthly-close");

        var second = await harness.CreateTemplateAsync(code: "MONTHLY-Close");

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(TaskReasonCodes.TaskTemplateCodeTaken, second.ReasonCode);
    }

    [Fact]
    public async Task A_task_template_can_be_edited()
    {
        var harness = new Harness();
        var created = await harness.CreateTemplateAsync(code: "monthly-close");
        var current = (await harness.GetTemplateAsync(created.Data)).Data!;

        var updated = await harness.UpdateTemplateAsync(
            created.Data, current.Version, "MONTHLY-CLOSE", dueInDays: 10, isActive: false);

        Assert.True(updated.IsSuccessful);
        var after = (await harness.GetTemplateAsync(created.Data)).Data!;
        Assert.Equal(10, after.DefaultDueInDays);
        Assert.False(after.IsActive);
    }

    [Fact]
    public async Task An_edit_does_NOT_blank_the_template_default_field_values()
    {
        /*
         * The FULL-REPLACE trap, which this module has been bitten by twice. `UpdateTaskTemplateRequest` does not
         * carry DefaultFieldValues — offering them in the form is BL-058 — so an update that wrote the property
         * anyway would quietly delete whatever a template already holds.
         */
        var harness = new Harness();
        var created = await harness.CreateTemplateAsync(code: "monthly-close");
        var stored = harness.Templates.All.Single();
        stored.DefaultFieldValues =
        [
            new Diten.Platform.Domain.Entities.Tasks.TaskFieldValue
            {
                DefinitionCode = "regulatory.phase",
                ValueType = TaskFieldValueType.Text,
                Value = "II"
            }
        ];

        await harness.UpdateTemplateAsync(created.Data, stored.Version, "MONTHLY-CLOSE", dueInDays: 5);

        Assert.Single(harness.Templates.All.Single().DefaultFieldValues);
    }

    [Fact]
    public async Task A_task_template_edit_that_CHANGES_the_code_is_refused()
    {
        var harness = new Harness();
        var created = await harness.CreateTemplateAsync(code: "monthly-close");
        var current = (await harness.GetTemplateAsync(created.Data)).Data!;

        var updated = await harness.UpdateTemplateAsync(created.Data, current.Version, "SOMETHING-ELSE");

        Assert.False(updated.IsSuccessful);
        Assert.Equal(TaskReasonCodes.TemplateCodeImmutable, updated.ReasonCode);
    }

    [Fact]
    public async Task A_retired_task_template_leaves_the_list_and_stops_being_offered()
    {
        var harness = new Harness();
        var created = await harness.CreateTemplateAsync(code: "monthly-close");

        Assert.True((await harness.DeleteTemplateAsync(created.Data)).IsSuccessful);

        Assert.Empty((await harness.ListTemplatesAsync()).Data!);
        Assert.Equal(404, (await harness.GetTemplateAsync(created.Data)).StatusCode);
        // The rule form's own picker must lose it too: a live schedule bound to a retired shape would go on
        // producing work from something nobody maintains.
        Assert.Empty((await harness.TemplateLookupAsync()).Data!);

        var stored = harness.Templates.All.Single();
        Assert.NotNull(stored.DeletedAt);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task Retiring_a_task_template_that_is_already_gone_is_a_404()
    {
        var harness = new Harness();

        Assert.Equal(404, (await harness.DeleteTemplateAsync(Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task The_rule_form_TEMPLATE_PICKER_fills_from_what_this_screen_creates()
    {
        /*
         * ⚠ THE DEFECT, MEASURED END TO END.
         *
         * `/api/v1/tasks/lookups/task-templates` and the picker that reads it both shipped long ago. The list
         * was empty because there was nowhere to create a template — nothing needed connecting, only building.
         * This asserts the connection actually exists rather than assuming it.
         */
        var harness = new Harness();
        Assert.Empty((await harness.TemplateLookupAsync()).Data!);

        var created = await harness.CreateTemplateAsync(code: "monthly-close");

        var offered = (await harness.TemplateLookupAsync()).Data!;
        Assert.Single(offered);
        Assert.Equal(created.Data, offered[0].Id);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static ChecklistTemplateItemDto Item(string code, string label, int sortOrder = 0)
        => new(code, LabelResourceKey: null, LabelText: label, ChecklistItemRequirement.Blocking, sortOrder, false);

    private static readonly ChecklistTemplateItemDto[] TwoItems =
        [Item("step-1", "İlk adım"), Item("step-2", "İkinci adım")];

    private sealed class Harness
    {
        internal static readonly Guid CompanyTr = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private readonly TasksController _controller;

        public Harness()
        {
            var tenant = new FakeTenantContext(TaskTestData.Tenant);
            var user = new FakeCurrentUserContext(TaskTestData.Me);
            Checklists = new FakeChecklistTemplateRepository();
            Templates = new FakeTaskTemplateRepository();

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(
                new TemplateChainMediator(Checklists, Templates, tenant, user), correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public FakeChecklistTemplateRepository Checklists { get; }

        public FakeTaskTemplateRepository Templates { get; }

        public async Task<Response<Guid>> CreateChecklistAsync(
            string code = "qa-release",
            bool isActive = true,
            IReadOnlyList<ChecklistTemplateItemDto>? items = null)
            => Unwrap<Guid>(await _controller.CreateChecklistTemplate(
                new CreateChecklistTemplateRequest(
                    code, "Serbest bırakma kontrolü", "Parti serbest bırakma öncesi adımlar",
                    items ?? TwoItems, isActive),
                CancellationToken.None));

        public async Task<Response<NoContent>> UpdateChecklistAsync(
            Guid id,
            int expectedVersion,
            string code,
            IReadOnlyList<ChecklistTemplateItemDto>? items = null,
            bool isActive = true)
            => Unwrap<NoContent>(await _controller.UpdateChecklistTemplate(
                id,
                new UpdateChecklistTemplateRequest(
                    code, "Serbest bırakma kontrolü", null, items ?? TwoItems, isActive, expectedVersion),
                CancellationToken.None));

        public async Task<Response<NoContent>> DeleteChecklistAsync(Guid id)
            => Unwrap<NoContent>(await _controller.DeleteChecklistTemplate(id, CancellationToken.None));

        public async Task<Response<ChecklistTemplateDto>> GetChecklistAsync(Guid id)
            => Unwrap<ChecklistTemplateDto>(await _controller.GetChecklistTemplate(id, CancellationToken.None));

        public async Task<Response<IReadOnlyList<ChecklistTemplateDto>>> ListChecklistsAsync()
            => Unwrap<IReadOnlyList<ChecklistTemplateDto>>(
                await _controller.GetChecklistTemplates(CancellationToken.None));

        public async Task<Response<IReadOnlyList<ChecklistTemplateLookupDto>>> ChecklistLookupAsync()
            => Unwrap<IReadOnlyList<ChecklistTemplateLookupDto>>(
                await _controller.GetChecklistTemplateLookup(CancellationToken.None));

        public async Task<Response<Guid>> CreateTemplateAsync(
            string code = "monthly-close",
            TaskAssignmentTarget target = TaskAssignmentTarget.SelfAssigned,
            Guid? poolPositionId = null,
            Guid? checklistTemplateId = null,
            Guid? legalEntityId = null,
            bool isActive = true)
            => Unwrap<Guid>(await _controller.CreateTemplate(
                new CreateTaskTemplateRequest(
                    code, "Ay sonu kapanış", "Ay sonu kapanış — {month}", "Mutabakat ve kapanış adımları",
                    TaskPriority.High, target, poolPositionId, DefaultDueInDays: 3,
                    checklistTemplateId, legalEntityId, isActive),
                CancellationToken.None));

        public async Task<Response<NoContent>> UpdateTemplateAsync(
            Guid id,
            int expectedVersion,
            string code,
            int? dueInDays = 3,
            bool isActive = true)
            => Unwrap<NoContent>(await _controller.UpdateTemplate(
                id,
                new UpdateTaskTemplateRequest(
                    code, "Ay sonu kapanış", null, null,
                    TaskPriority.High, TaskAssignmentTarget.SelfAssigned, null, dueInDays,
                    ChecklistTemplateId: null, LegalEntityId: null, isActive, expectedVersion),
                CancellationToken.None));

        public async Task<Response<NoContent>> DeleteTemplateAsync(Guid id)
            => Unwrap<NoContent>(await _controller.DeleteTemplate(id, CancellationToken.None));

        public async Task<Response<TaskTemplateDto>> GetTemplateAsync(Guid id)
            => Unwrap<TaskTemplateDto>(await _controller.GetTemplate(id, CancellationToken.None));

        public async Task<Response<IReadOnlyList<TaskTemplateDto>>> ListTemplatesAsync()
            => Unwrap<IReadOnlyList<TaskTemplateDto>>(await _controller.GetTemplates(CancellationToken.None));

        public async Task<Response<IReadOnlyList<TaskTemplateLookupDto>>> TemplateLookupAsync()
            => Unwrap<IReadOnlyList<TaskTemplateLookupDto>>(
                await _controller.GetTaskTemplates(CancellationToken.None));

        /// <summary>The controller copies the status code onto the result verbatim, so this asserts on the wire.</summary>
        private static Response<T> Unwrap<T>(IActionResult result)
            => result is NoContentResult
                ? Response<T>.Success(204, "corr")
                : (Response<T>)((ObjectResult)result).Value!;
    }

    private sealed class TemplateChainMediator(
        FakeChecklistTemplateRepository checklists,
        FakeTaskTemplateRepository templates,
        FakeTenantContext tenant,
        FakeCurrentUserContext user) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request switch
            {
                CreateChecklistTemplateCommand command => (Task<TResponse>)(object)
                    new CreateChecklistTemplateHandler(checklists, tenant, user).Handle(command, ct),
                UpdateChecklistTemplateCommand command => (Task<TResponse>)(object)
                    new UpdateChecklistTemplateHandler(checklists, user).Handle(command, ct),
                DeleteChecklistTemplateCommand command => (Task<TResponse>)(object)
                    new DeleteChecklistTemplateHandler(checklists, user).Handle(command, ct),
                GetChecklistTemplateListQuery query => (Task<TResponse>)(object)
                    new GetChecklistTemplateListHandler(checklists).Handle(query, ct),
                GetChecklistTemplateByIdQuery query => (Task<TResponse>)(object)
                    new GetChecklistTemplateByIdHandler(checklists).Handle(query, ct),
                GetChecklistTemplateLookupQuery query => (Task<TResponse>)(object)
                    new GetChecklistTemplateLookupHandler(checklists).Handle(query, ct),
                CreateTaskTemplateCommand command => (Task<TResponse>)(object)
                    new CreateTaskTemplateHandler(templates, checklists, tenant, user).Handle(command, ct),
                UpdateTaskTemplateCommand command => (Task<TResponse>)(object)
                    new UpdateTaskTemplateHandler(templates, checklists, user).Handle(command, ct),
                DeleteTaskTemplateCommand command => (Task<TResponse>)(object)
                    new DeleteTaskTemplateHandler(templates, user).Handle(command, ct),
                GetTaskTemplateListQuery query => (Task<TResponse>)(object)
                    new GetTaskTemplateListHandler(templates).Handle(query, ct),
                GetTaskTemplateByIdQuery query => (Task<TResponse>)(object)
                    new GetTaskTemplateByIdHandler(templates).Handle(query, ct),
                GetTaskTemplateLookupQuery query => (Task<TResponse>)(object)
                    new GetTaskTemplateLookupHandler(templates).Handle(query, ct),
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
