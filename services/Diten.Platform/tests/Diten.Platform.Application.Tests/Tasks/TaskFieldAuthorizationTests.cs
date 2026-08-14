using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-024 Phase 2 — FIELD-LEVEL AUTHORIZATION on configurable task fields.
///
/// <para><b>What was there before.</b> The pipe was laid and no tap was fitted. <c>TaskFieldValue</c> has carried
/// <c>Classification</c>, <c>AccessState</c> and <c>Redacted</c> since Phase 1, and the mapper has honoured
/// <c>v.Redacted ? null : v.Value</c> the whole time — but <c>Redacted</c> was hardcoded false at every write and
/// set by nothing, ever. The hiding mechanism worked perfectly and no field was ever hidden.</para>
///
/// <para><b>Four layers, four separate proofs</b>, because passing one of them proves nothing about the others:
/// a value can be withheld from the response while its option list stays fully enumerable one route over, and a
/// read restriction is not a write restriction. Each has its own section below.</para>
///
/// <para><b>The rule keys on a PERMISSION, not a role.</b> That was measured, not assumed: role GUIDs never
/// reach this service (<c>JwtTenantAuthorizationContext.RoleIds</c> is hardcoded empty — Platform sees role
/// NAMES only), positions are not in the token at all, and <c>RolePermission</c> in MOD-0018 has no third
/// dimension to hang a field on. A permission key is the one currency MOD-0018 already mints, so the definition
/// names a requirement and MOD-0018 keeps deciding who meets it. No second authorization engine.</para>
/// </summary>
public sealed class TaskFieldAuthorizationTests
{
    private const string SalaryView = "acme.hr.salary.view";
    private const string SalaryEdit = "acme.hr.salary.edit";

    // ── LAYER 2: READ — the value never leaves the server ───────────────────────────────────────────────────

    [Fact]
    public async Task An_unauthorized_reader_gets_NO_VALUE_and_a_redacted_flag()
    {
        /*
         * MEASURED, not guessed: the executable contract has validated `REDACTED_VALUE_MUST_BE_OMITTED` since it
         * was written — a fixture carrying `redacted: true` beside a value is rejected — while the DTO had no
         * `redacted` field at all, so the rule was enforceable and unreachable at the same time. The shape the
         * contract asks for is therefore: flag TRUE, value ABSENT. Not a dropped row, and not a value that the
         * browser is trusted to hide.
         */
        var item = await ProjectAsync(TaskActors.None());

        var field = Assert.Single(item.BusinessContext!.Sections[0].Fields);
        Assert.Null(field.Value);
        Assert.True(field.Redacted);
        // The LABEL still travels: the catalogue is readable, so the field's existence is not the secret — only
        // its content is. Hiding the row would make "withheld" indistinguishable from "not on this task".
        Assert.NotNull(field.Label);
    }

    [Fact]
    public async Task An_authorized_reader_sees_everything_exactly_as_before()
    {
        // The regression half. A security rule that also breaks the permitted path has not been shipped safely.
        var item = await ProjectAsync(TaskActors.Holding(SalaryView));

        var field = Assert.Single(item.BusinessContext!.Sections[0].Fields);
        Assert.Equal("B3", field.Value);
        Assert.False(field.Redacted);
    }

    [Fact]
    public async Task An_UNRESTRICTED_field_is_visible_to_everyone_holding_nothing()
    {
        /*
         * The default that makes this deployable. Every definition written before this feature carries a null
         * ViewPermission, so turning it on must change nothing until somebody deliberately restricts something.
         * The opposite default would have blanked every configurable field in every tenant on deploy.
         */
        var item = await ProjectAsync(TaskActors.None(), viewPermission: null);

        Assert.Equal("B3", Assert.Single(item.BusinessContext!.Sections[0].Fields).Value);
    }

    [Fact]
    public async Task The_TASKS_DETAIL_response_redacts_too_and_not_only_the_projection()
    {
        /*
         * TWO read paths, and hiding a field in one of them is not half a fix — it is no fix. The Task Center
         * projection and the Tasks detail endpoint are different DTOs built by different files; both ask the
         * same rule.
         */
        var task = TaskWithSalary();
        var definitions = new Dictionary<string, TaskFieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["salary.band"] = Definition(SalaryView)
        };

        var hidden = TaskItemMapper.ToDetail(
            task, new TaskLifecycleService(), false, false, [], [], TaskActors.None(), definitions);
        var shown = TaskItemMapper.ToDetail(
            task, new TaskLifecycleService(), false, false, [], [], TaskActors.Holding(SalaryView), definitions);

        Assert.Null(Assert.Single(hidden.FieldValues!).Value);
        Assert.True(Assert.Single(hidden.FieldValues!).Redacted);
        Assert.Equal("B3", Assert.Single(shown.FieldValues!).Value);
    }

    [Fact]
    public async Task A_value_whose_definition_VANISHED_stays_hidden_when_it_was_classified()
    {
        /*
         * Fail-closed exactly where it matters. A definition can be retired and purged; the value survives it and
         * carries the classification copied onto it at write time. If the rule cannot be read, a value once
         * marked sensitive must not become readable BY LOSING ITS RULE — that would make deletion a disclosure
         * channel. An ordinary (Normal) value keeps showing, because hiding those would blank real data for a
         * reason nobody chose.
         */
        var task = TaskWithSalary();
        task.FieldValues[0].Classification = TaskFieldClassification.Confidential;

        var item = await ProjectAsync(TaskActors.None(), catalogue: EmptyCatalogue());
        Assert.Null(Assert.Single(item.BusinessContext!.Sections[0].Fields).Value);

        // …and the ordinary case is untouched.
        var normal = await ProjectAsync(
            TaskActors.None(), catalogue: EmptyCatalogue(), classification: TaskFieldClassification.Normal);
        Assert.Equal("B3", Assert.Single(normal.BusinessContext!.Sections[0].Fields).Value);
    }

    [Fact]
    public async Task A_PLATFORM_actor_passes_every_field_rule()
    {
        // Mirrors HasPermissionAttribute's bypass. Deriving "platform" differently here would let a field be
        // hidden from an actor the endpoint beside it lets straight through.
        var item = await ProjectAsync(TaskActors.PermitAll());

        Assert.Equal("B3", Assert.Single(item.BusinessContext!.Sections[0].Fields).Value);
    }

    // ── LAYER 3: THE OPTION LIST — a hidden field's picker is hidden too ─────────────────────────────────────

    [Fact]
    public async Task An_unauthorized_reader_is_REFUSED_the_field_option_list()
    {
        /*
         * BL-024's own note raised this and it is the reason the endpoint is in scope. `options` and `records`
         * sit on plain `platform.tasks.read`, so redacting the VALUE while leaving the SELECTOR open would let
         * anyone enumerate the very list the field was hidden to protect. Redaction in name only.
         */
        var response = await OptionsAsync(TaskActors.None());

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(TaskReasonCodes.FieldAccessDenied, response.ReasonCode);
    }

    [Fact]
    public async Task The_option_refusal_is_403_and_not_a_pretend_404()
    {
        /*
         * The definition's EXISTENCE is not the secret — `GET field-definitions` lists it — so claiming it is
         * missing would be a lie the caller can disprove in one request. Error codes people learn to distrust
         * are worse than blunt ones.
         */
        var denied = await OptionsAsync(TaskActors.None());
        var missing = await OptionsAsync(TaskActors.Holding(SalaryView), code: "no.such.field");

        Assert.Equal(403, denied.StatusCode);
        Assert.Equal(404, missing.StatusCode);
        Assert.Equal(TaskReasonCodes.FieldDefinitionUnknown, missing.ReasonCode);
    }

    [Fact]
    public async Task An_authorized_reader_still_gets_the_options()
    {
        // Regression: the picker must keep working for the people it is for, or the field becomes unfillable.
        var response = await OptionsAsync(TaskActors.Holding(SalaryView));

        Assert.Equal(200, response.StatusCode);
        Assert.NotEmpty(response.Data!);
    }

    // ── LAYER 4: WRITE — and it is NOT the read rule ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_value_hand_placed_in_the_payload_for_an_unwritable_field_is_REFUSED()
    {
        // Refused, not ignored. Ignoring it would answer 204 to somebody who just tried to set a value they have
        // no authority over, and they would have every reason to believe it took.
        var result = await ValidateAsync(
            TaskActors.Holding(SalaryView),           // may READ it…
            [new TaskFieldValueDto("salary.band", TaskFieldValueType.Text, "B9")]);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldAccessDenied, result.ReasonCode);
    }

    [Fact]
    public async Task READ_ACCESS_IS_NOT_WRITE_ACCESS()
    {
        /*
         * The distinction the two keys exist for, asserted directly: the same actor, the same field, one answer
         * for reading and a different one for writing. An approver who may read a salary band is not thereby
         * allowed to change it.
         */
        var reader = TaskActors.Holding(SalaryView);
        var writer = TaskActors.Holding(SalaryView, SalaryEdit);
        var payload = new[] { new TaskFieldValueDto("salary.band", TaskFieldValueType.Text, "B9") };

        Assert.False((await ValidateAsync(reader, payload)).IsValid);
        Assert.True((await ValidateAsync(writer, payload)).IsValid);
    }

    [Fact]
    public async Task Write_access_alone_is_not_enough_without_read_access()
    {
        /*
         * The floor. Writing a value you may not SEE is a covert write into a field you cannot verify — and the
         * client never received the value, so it could not have round-tripped it honestly either.
         */
        var result = await ValidateAsync(
            TaskActors.Holding(SalaryEdit),           // edit but not view
            [new TaskFieldValueDto("salary.band", TaskFieldValueType.Text, "B9")]);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldAccessDenied, result.ReasonCode);
    }

    [Fact]
    public async Task An_ordinary_edit_by_an_unauthorized_user_does_NOT_DELETE_the_hidden_value()
    {
        /*
         * ⚠ THE HALF WITH NO ATTACKER IN IT, and the one that would have done the most damage.
         *
         * Redaction and full-replace are each fine and lethal together: `UpdateTaskItemRequest` replaces
         * `task.FieldValues` wholesale, and a caller who may not see the field never received its value — so an
         * ordinary "change the title" round-trip posts the field back MISSING and deletes it. No error, no
         * attacker, no trace. The stored values are handed to the validator so an unwritable field is carried
         * through untouched.
         */
        var stored = TaskWithSalary().FieldValues;

        // The client sends back everything it was given — which did not include the salary band.
        var result = await ValidateAsync(TaskActors.None(), values: [], existing: stored);

        Assert.True(result.IsValid);
        var survivor = Assert.Single(result.Values);
        Assert.Equal("salary.band", survivor.DefinitionCode);
        Assert.Equal("B3", survivor.Value);
    }

    [Fact]
    public async Task A_preserved_REQUIRED_field_does_not_make_the_task_uneditable()
    {
        /*
         * The corollary. If a preserved value did not count as supplied, a required restricted field would refuse
         * every edit by exactly the people the restriction was aimed at — the feature would present as "this task
         * cannot be saved" and nobody would connect it to a permission.
         */
        var stored = TaskWithSalary().FieldValues;

        var result = await ValidateAsync(
            TaskActors.None(), values: [], existing: stored, required: true);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_authorized_writer_still_writes_normally()
    {
        // Regression, again: the rule must not cost the permitted path anything.
        var result = await ValidateAsync(
            TaskActors.Holding(SalaryView, SalaryEdit),
            [new TaskFieldValueDto("salary.band", TaskFieldValueType.Text, "B9")]);

        Assert.True(result.IsValid);
        Assert.Equal("B9", Assert.Single(result.Values).Value);
    }

    [Fact]
    public async Task THE_UPDATE_HANDLER_preserves_it_too_and_not_only_the_service()
    {
        /*
         * ⚠ THE TEST THAT WAS MISSING, WRITTEN AFTER THE LIVE RUN CAUGHT WHAT IT WOULD HAVE CAUGHT.
         *
         * The preservation test above drives the SERVICE and passes `existing` itself. The service was correct
         * and the handler never supplied that argument, so on the live system an ordinary title edit by a user
         * who could not see a restricted field deleted it — 204, no error. Green tests, broken system.
         *
         * This drives the HANDLER, which is the only thing that proves the wiring. A unit test of a collaborator
         * cannot vouch for its caller.
         */
        var task = TaskWithSalary();
        var tasks = new FakeTaskItemRepository(task);
        var definition = Definition(SalaryView);
        definition.EditPermission = SalaryEdit;

        var handler = new UpdateTaskItemHandler(
            tasks,
            new FakeOrganizationUnitRepository(),
            new TaskFieldDefinitionService(
                new FakeTaskFieldDefinitionRepository(definition), TaskRecordSourceDoubles.None, TaskActors.None()),
            new FakeCurrentUserContext(TaskTestData.Me),
            new FakeTaskApprovalService(),
            new FakeTaskReviewService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UpdateTaskItemHandler>.Instance);

        // Exactly what a browser sends back: everything it was given, which did NOT include the hidden value.
        var response = await handler.Handle(
            new Diten.Platform.Application.Features.Tasks.Commands.UpdateTaskItemCommand(
                task.Id,
                new UpdateTaskItemRequest(
                    Title: "Yeni başlık",
                    Description: null,
                    Priority: TaskPriority.Medium,
                    OrganizationUnitId: null,
                    DueAt: null,
                    StartAt: null,
                    PlannedDate: null,
                    EstimateHours: null,
                    Tags: null,
                    ReviewRequired: false,
                    EmailNotificationsEnabled: false,
                    DelegationAllowed: false,
                    FieldValues: [],
                    ExpectedVersion: task.Version),
                "corr"),
            CancellationToken.None);

        Assert.Equal(204, response.StatusCode);

        var stored = tasks.Items.Single();
        Assert.Equal("Yeni başlık", stored.Title);
        var survivor = Assert.Single(stored.FieldValues);
        Assert.Equal("B3", survivor.Value);
    }

    // ── LAYER 1: THE DEFINITION — and no cache between it and the answer ────────────────────────────────────

    [Fact]
    public async Task Changing_the_DEFINITION_changes_the_answer_on_the_very_next_read()
    {
        /*
         * No cache sits between the catalogue and the decision: the definitions are read per request and the rule
         * is a pure function of (definition, actor). The same actor, the same task, the same repository instance —
         * only the definition is edited between the two reads.
         *
         * ⚠ SCOPE OF THIS CLAIM, stated so it is not read as more than it is: this covers a change to the
         * DEFINITION. A change to who HOLDS the permission lives in the caller's access token, which is minted at
         * login with no revocation channel, so a grant change waits for the token to turn over (measured: 120
         * minutes). That is a platform-wide property this feature consumes and cannot fix — recorded on BL-024.
         */
        var definitions = new FakeTaskFieldDefinitionRepository(Definition(viewPermission: null));
        var task = TaskWithSalary();
        var actor = TaskActors.None();

        var before = await ProjectWithAsync(task, definitions, actor);
        Assert.Equal("B3", Assert.Single(before.BusinessContext!.Sections[0].Fields).Value);

        // An administrator restricts the field. Nothing else changes.
        definitions.All[0].ViewPermission = SalaryView;

        var after = await ProjectWithAsync(task, definitions, actor);
        Assert.Null(Assert.Single(after.BusinessContext!.Sections[0].Fields).Value);
        Assert.True(Assert.Single(after.BusinessContext.Sections[0].Fields).Redacted);
    }

    [Fact]
    public void The_rule_lives_in_exactly_ONE_place()
    {
        /*
         * The structural claim. Four call sites need this decision, and four copies of a security rule is four
         * chances to fix three of them — the shape of BL-042 and BL-051, both "one fact, several writers, one
         * forgotten". Every site delegates to TaskFieldAccessRules; nothing re-implements the comparison.
         *
         * Derived from the source rather than a list of file names, so a fifth reader added later is caught.
         */
        var root = SourceRoot();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith("TaskFieldAccessRules.cs", StringComparison.Ordinal)) { continue; }

            /*
             * EVALUATING the key is what is forbidden — asking whether the caller holds it. STORING one is not:
             * the definition handlers legitimately assign `ViewPermission = Trimmed(request.ViewPermission)`,
             * and an earlier, blunter version of this test flagged them, which would have taught the next reader
             * that the guard cries wolf.
             *
             * So the pattern is the evaluation itself: a permission check and one of the two keys on one line.
             */
            foreach (var line in File.ReadAllLines(file))
            {
                var evaluatesAKey = line.Contains(".Has(")
                                    && (line.Contains("ViewPermission") || line.Contains("EditPermission"));
                if (evaluatesAKey)
                {
                    offenders.Add($"{Path.GetFileName(file)}: {line.Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These files evaluate a field permission themselves instead of asking TaskFieldAccessRules: "
            + string.Join(", ", offenders));
    }

    // ── DRIVERS ─────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<WorkItemProjectionDto> ProjectAsync(
        FakeActorPermissions actor,
        string? viewPermission = SalaryView,
        FakeTaskFieldDefinitionRepository? catalogue = null,
        TaskFieldClassification classification = TaskFieldClassification.Confidential)
    {
        var task = TaskWithSalary();
        task.FieldValues[0].Classification = classification;
        return await ProjectWithAsync(
            task, catalogue ?? new FakeTaskFieldDefinitionRepository(Definition(viewPermission)), actor);
    }

    private static async Task<WorkItemProjectionDto> ProjectWithAsync(
        TaskItem task,
        FakeTaskFieldDefinitionRepository definitions,
        FakeActorPermissions actor)
    {
        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            new FakeTaskTransitionRepository(), new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(),
            actor,
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            definitions);

        var workActor = new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());
        return Assert.Single(await provider.GetWorkItemsAsync(workActor, CancellationToken.None));
    }

    private static async Task<Response<IReadOnlyList<TaskFieldOptionDto>>> OptionsAsync(
        FakeActorPermissions actor,
        string code = "salary.band")
    {
        var definition = Definition(SalaryView);
        definition.OptionsSourceKind = TaskFieldOptionsSourceKind.PlatformLookup;
        definition.OptionsSourceKey = "currency";

        var lookups = new Mock<IPlatformLookupProvider>();
        lookups
            .Setup(l => l.GetLookupOptionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LookupOptionDto> { new("TRY", "TRY", "TRY") });

        var handler = new GetTaskFieldDefinitionOptionsHandler(
            new FakeTaskFieldDefinitionRepository(definition),
            lookups.Object,
            new Mock<IBusinessReferenceDataConsumerQueryService>().Object,
            TaskRecordSourceDoubles.None,
            new FakeTenantContext(TaskTestData.Tenant),
            new ConfigurationBuilder().Build(),
            actor);

        return await handler.Handle(
            new GetTaskFieldDefinitionOptionsQuery(code, "corr"), CancellationToken.None);
    }

    private static Task<TaskFieldValidationResult> ValidateAsync(
        FakeActorPermissions actor,
        IReadOnlyList<TaskFieldValueDto>? values,
        IReadOnlyList<TaskFieldValue>? existing = null,
        bool required = false)
    {
        var definition = Definition(SalaryView);
        definition.EditPermission = SalaryEdit;
        definition.IsRequired = required;

        return new TaskFieldDefinitionService(
                new FakeTaskFieldDefinitionRepository(definition), TaskRecordSourceDoubles.None, actor)
            .ValidateAndMaterializeAsync(values, CancellationToken.None, required, existing);
    }

    // ── FIXTURES ────────────────────────────────────────────────────────────────────────────────────────────

    private static TaskFieldDefinition Definition(string? viewPermission) => new()
    {
        TenantId = TaskTestData.Tenant,
        Code = "salary.band",
        LabelText = "Ücret bandı",
        ValueType = TaskFieldValueType.Text,
        Section = "İK",
        Classification = TaskFieldClassification.Confidential,
        ViewPermission = viewPermission
    };

    /// <summary>A catalogue that has forgotten the definition — retired and purged.</summary>
    private static FakeTaskFieldDefinitionRepository EmptyCatalogue() => new();

    private static TaskItem TaskWithSalary() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Ücret revizyonu",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
        Lifecycle = TaskLifecycle.InProgress,
        Version = 1,
        FieldValues =
        [
            new TaskFieldValue
            {
                DefinitionCode = "salary.band",
                ValueType = TaskFieldValueType.Text,
                Value = "B3",
                Classification = TaskFieldClassification.Confidential
            }
        ]
    };

    /// <summary>The Application project's source tree, found from the test assembly rather than a fixed path.</summary>
    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "src", "Diten.Platform.Application", "Features", "Tasks");
    }
}
