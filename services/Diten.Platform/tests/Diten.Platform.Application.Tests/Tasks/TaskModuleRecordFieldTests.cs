using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/*
 * ══ Configurable fields whose values come from ANOTHER MODULE'S RECORDS ═══════════════════════════════════════
 *
 * The pattern has three older names — SAP's check table with its F4 search help, Oracle's table-validated value
 * set behind a DFF, ServiceNow's reference field — and one shared sentence: the administrator defines the FIELD,
 * another module owns the VALUES.
 *
 * What this suite is actually protecting is the CONTRACT, not the two modules that implement it first. The
 * expensive failure in WC-1 was not a wrong query; it was two providers answering in two shapes, so the second
 * consumer rewrote the first one's work. Hence the recurring move below: everything is asserted over TWO sources
 * with different keys, and a claim that only holds for one of them is a claim about a special case.
 */
public sealed class TaskModuleRecordFieldTests
{
    private const string DepartmentSource = "test-department";
    private const string SupplierSource = "test-supplier";

    private static readonly string UnitA = Guid.Parse("3f1b2a2c-0000-4000-8000-000000000001").ToString();
    private static readonly string UnitB = Guid.Parse("3f1b2a2c-0000-4000-8000-000000000002").ToString();
    private static readonly string SupplierA = Guid.Parse("7c9d4e5f-0000-4000-8000-000000000001").ToString();

    // Two sources, deliberately unlike each other: one carries a secondary line, the other does not, and their
    // ids are not drawn from the same range. Anything that passes for both is not shaped around either.
    private static FakeTaskRecordSource Departments() => new(
        DepartmentSource,
        new TaskRecordDto(UnitA, "QA-01", "Kalite Güvence", null),
        new TaskRecordDto(UnitB, "REG-02", "Ruhsatlandırma", null));

    private static FakeTaskRecordSource Suppliers() => new(
        SupplierSource,
        new TaskRecordDto(SupplierA, "SUP-77", "Acme Kimya", "İstanbul"));

    // ── The registry ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_registry_finds_every_source_the_container_gave_it()
    {
        var registry = TaskRecordSourceDoubles.With(Departments(), Suppliers());

        Assert.Equal(DepartmentSource, registry.Find(DepartmentSource)?.SourceKey);
        Assert.Equal(SupplierSource, registry.Find(SupplierSource)?.SourceKey);
        Assert.Equal(2, registry.All.Count);
    }

    [Fact]
    public void An_unknown_source_is_an_answer_not_an_exception()
    {
        // Callers ACT on null — they refuse the definition, or they report the field as unresolvable. An
        // exception would turn a configuration mistake into a 500.
        Assert.Null(TaskRecordSourceDoubles.With(Departments()).Find("product"));
        Assert.Null(TaskRecordSourceDoubles.With(Departments()).Find(null));
    }

    [Fact]
    public void Two_modules_claiming_one_key_is_refused_at_startup()
    {
        /*
         * Whichever source won would be decided by registration order, and the symptom months later is "the
         * picker shows the wrong list". Failing at construction makes it a deployment error instead.
         */
        var error = Assert.Throws<InvalidOperationException>(
            () => TaskRecordSourceDoubles.With(Departments(), new FakeTaskRecordSource(DepartmentSource)));

        Assert.Contains(DepartmentSource, error.Message, StringComparison.Ordinal);
    }

    // ── Resolution: one path, three kinds ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(DepartmentSource)]
    [InlineData(SupplierSource)]
    public async Task Both_sources_resolve_through_the_same_options_query(string sourceKey)
    {
        /*
         * THE test of whether this is a contract. Not "records can be fetched" — that is one query — but "two
         * unrelated sources come back through the SAME query, in the SAME shape, with the caller naming only a
         * FIELD". If either needed its own route, the third source would rewrite both.
         */
        var (handler, _) = Options(RecordDefinition("delivery.ref", sourceKey));

        var response = await handler.Handle(
            new GetTaskFieldDefinitionOptionsQuery("delivery.ref", "corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotEmpty(response.Data!);
    }

    [Fact]
    public async Task The_stored_value_is_the_identity_and_the_reader_gets_the_key_and_the_name()
    {
        /*
         * BL-049, stated as a rule rather than as a screen. The option VALUE is the record's identity, so
         * renaming the record does not rewrite the task; the LABEL is the name and the secondary line carries
         * the business key, so nothing the reader sees is a GUID.
         */
        var (handler, _) = Options(RecordDefinition("delivery.department", DepartmentSource));

        var response = await handler.Handle(
            new GetTaskFieldDefinitionOptionsQuery("delivery.department", "corr"), CancellationToken.None);

        var option = Assert.Single(response.Data!, o => o.Value == UnitA);
        Assert.Equal("Kalite Güvence", option.Label);
        Assert.Equal("QA-01", option.Secondary);
        Assert.DoesNotContain(UnitA, option.Label, StringComparison.Ordinal);
        Assert.DoesNotContain(UnitA, option.Secondary!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_secondary_line_disambiguates_two_records_that_share_a_name()
    {
        // "QA Specialist" in two facilities is one word twice. The source's own secondary line is carried
        // through beside the business key rather than dropped.
        var (handler, _) = Options(RecordDefinition("delivery.supplier", SupplierSource));

        var response = await handler.Handle(
            new GetTaskFieldDefinitionOptionsQuery("delivery.supplier", "corr"), CancellationToken.None);

        var option = Assert.Single(response.Data!);
        Assert.Equal("SUP-77 · İstanbul", option.Secondary);
    }

    [Fact]
    public async Task The_term_reaches_the_source_rather_than_being_filtered_after_the_fact()
    {
        /*
         * The whole reason records are searched instead of listed: five thousand rows must never cross the wire
         * to be narrowed here. Asserting only on the RESULT would pass just as well if the source returned
         * everything and this handler filtered it — so the assertion is that the source was ASKED.
         */
        var departments = Departments();
        var (handler, _) = Options(RecordDefinition("delivery.department", DepartmentSource), departments);

        var response = await handler.Handle(
            new GetTaskFieldDefinitionOptionsQuery("delivery.department", "corr", Term: "Ruhsat"),
            CancellationToken.None);

        Assert.Equal(["Ruhsat"], departments.SearchTerms);
        Assert.Equal("Ruhsatlandırma", Assert.Single(response.Data!).Label);
    }

    [Fact]
    public async Task A_source_no_module_registered_is_reported_not_answered_with_an_empty_list()
    {
        /*
         * The distinction the form depends on. An empty list means "nothing matched"; an unresolvable source
         * means "do not show this field at all". Collapsing them is how a field becomes an empty picker nobody
         * can fill — BL-050's shape.
         */
        var (handler, _) = Options(RecordDefinition("delivery.product", "product"));

        var response = await handler.Handle(
            new GetTaskFieldDefinitionOptionsQuery("delivery.product", "corr"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(TaskReasonCodes.FieldOptionsUnresolved, response.ReasonCode);
    }

    // ── Hydration: the round trip that loses data if it is missing ───────────────────────────────────────────

    [Fact]
    public async Task Stored_identities_resolve_back_even_when_the_first_page_never_held_them()
    {
        /*
         * The edit form's data-loss trap. The picker opens with the first page; a task saved months ago points
         * at a record that page does not contain, and a control that cannot render its own value posts back a
         * different one. Yesterday's round caught this exact shape on date fields.
         */
        var (handler, _) = Options(RecordDefinition("delivery.department", DepartmentSource));

        var response = await handler.Handle(
            new GetTaskFieldDefinitionOptionsQuery("delivery.department", "corr", Ids: [UnitB]),
            CancellationToken.None);

        var option = Assert.Single(response.Data!);
        Assert.Equal(UnitB, option.Value);
        Assert.Equal("Ruhsatlandırma", option.Label);
    }

    [Fact]
    public async Task A_hydration_ignores_the_search_term_because_it_is_not_a_search()
    {
        // The stored value has to come back whatever is in the search box; narrowing it by the term would drop
        // the value the form is trying to display.
        var (handler, _) = Options(RecordDefinition("delivery.department", DepartmentSource));

        var response = await handler.Handle(
            new GetTaskFieldDefinitionOptionsQuery(
                "delivery.department", "corr", Term: "nothing-matches-this", Ids: [UnitA]),
            CancellationToken.None);

        Assert.Equal(UnitA, Assert.Single(response.Data!).Value);
    }

    // ── Values: the check table's actual purpose ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_value_naming_a_real_record_is_accepted()
    {
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(RecordDefinition("delivery.department", DepartmentSource)),
            TaskRecordSourceDoubles.With(Departments()));

        var result = await service.ValidateAndMaterializeAsync(
            [new TaskFieldValueDto("delivery.department", TaskFieldValueType.Reference, UnitA)]);

        Assert.True(result.IsValid);
        Assert.Equal(UnitA, Assert.Single(result.Values).Value);
    }

    [Fact]
    public async Task A_well_formed_identity_that_names_no_record_is_refused()
    {
        /*
         * This is what a check table IS. Before it, a Reference value only had to LOOK like an identity — any
         * GUID passed — so a client could store a pointer into nothing and the field would render as a picker
         * with no selection, forever.
         */
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(RecordDefinition("delivery.department", DepartmentSource)),
            TaskRecordSourceDoubles.With(Departments()));

        var result = await service.ValidateAndMaterializeAsync(
            [new TaskFieldValueDto(
                "delivery.department", TaskFieldValueType.Reference, Guid.NewGuid().ToString())]);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldValueInvalid, result.ReasonCode);
    }

    [Fact]
    public async Task Values_from_two_different_sources_are_each_checked_against_their_own_source()
    {
        /*
         * A single-source check would pass a supplier id into the department source and, if either happened to
         * hold it, accept the wrong thing. Two fields, two sources, one call: the department id is valid and the
         * supplier id is not, and the refusal has to name the supplier field.
         */
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(
                RecordDefinition("delivery.department", DepartmentSource),
                RecordDefinition("delivery.supplier", SupplierSource)),
            TaskRecordSourceDoubles.With(Departments(), Suppliers()));

        var result = await service.ValidateAndMaterializeAsync([
            new TaskFieldValueDto("delivery.department", TaskFieldValueType.Reference, UnitA),
            // A real id — but the DEPARTMENT source's, offered to the supplier field.
            new TaskFieldValueDto("delivery.supplier", TaskFieldValueType.Reference, UnitB)
        ]);

        Assert.False(result.IsValid);
        Assert.Contains("delivery.supplier", result.Message!, StringComparison.Ordinal);
    }

    // ── The definition itself ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_record_source_on_a_value_type_that_cannot_hold_an_identity_is_refused()
    {
        var invalid = TaskFieldDefinitionRules.ValidateOptionSource(
            TaskFieldOptionsSourceKind.ModuleRecord, DepartmentSource, TaskFieldValueType.Number,
            _ => true);

        Assert.Equal(TaskReasonCodes.FieldOptionSourceInvalid, invalid?.ReasonCode);
    }

    [Fact]
    public void An_unregistered_record_source_is_refused_where_the_administrator_can_still_read_why()
    {
        // The form drops an unresolvable field — correctly. But an administrator who saves a definition and then
        // never sees it again has nothing to diagnose, so the write says no first.
        var invalid = TaskFieldDefinitionRules.ValidateOptionSource(
            TaskFieldOptionsSourceKind.ModuleRecord, "product", TaskFieldValueType.Reference,
            _ => false);

        Assert.Equal(TaskReasonCodes.FieldOptionSourceInvalid, invalid?.ReasonCode);
    }

    [Fact]
    public void The_two_older_source_kinds_are_left_alone_by_the_new_rule()
    {
        // Non-vacuity in the other direction: a rule that refused everything would satisfy both tests above.
        Assert.Null(TaskFieldDefinitionRules.ValidateOptionSource(
            TaskFieldOptionsSourceKind.BusinessReferenceData, "country", TaskFieldValueType.Status, _ => false));
        Assert.Null(TaskFieldDefinitionRules.ValidateOptionSource(
            TaskFieldOptionsSourceKind.PlatformLookup, "currencies", TaskFieldValueType.Status, _ => false));
        Assert.Null(TaskFieldDefinitionRules.ValidateOptionSource(
            TaskFieldOptionsSourceKind.None, null, TaskFieldValueType.Text, _ => false));
    }

    [Fact]
    public async Task The_create_handler_refuses_a_definition_pointing_at_no_module()
    {
        var definitions = new FakeTaskFieldDefinitionRepository();
        var handler = new CreateTaskFieldDefinitionHandler(
            definitions,
            new FakeTenantContext(TaskTestData.Tenant),
            new FakeCurrentUserContext(TaskTestData.Me),
            TaskRecordSourceDoubles.With(Departments()));

        var response = await handler.Handle(
            new CreateTaskFieldDefinitionCommand(
                new CreateTaskFieldDefinitionRequest(
                    Code: "delivery.product",
                    LabelResourceKey: null,
                    LabelText: "Ürün",
                    ValueType: TaskFieldValueType.Reference,
                    Section: "Delivery",
                    Importance: TaskFieldImportance.Secondary,
                    IsRequired: false,
                    SortOrder: 10,
                    OptionsSourceKind: TaskFieldOptionsSourceKind.ModuleRecord,
                    OptionsSourceKey: "product",
                    AppliesToModuleCode: null,
                    Classification: TaskFieldClassification.Normal,
                    DefaultAccessState: TaskFieldAccessState.Visible),
                "corr"),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(TaskReasonCodes.FieldOptionSourceInvalid, response.ReasonCode);
        Assert.Empty(await definitions.ListAllAsync(CancellationToken.None));
    }

    // ── The administrator's picker (İŞ 1) ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_registered_source_is_offered_to_the_administrator_without_this_file_naming_it()
    {
        /*
         * The list comes from the REGISTRY, so a module that registers a source appears on the screen with no
         * code edited anywhere. That is the difference between "adding a record" and "writing code", and it is
         * the requirement this round exists to satisfy.
         */
        var handler = new GetTaskFieldOptionSourcesHandler(
            NoReferenceSets(),
            TaskRecordSourceDoubles.With(Departments(), Suppliers()),
            new FakeTenantContext(TaskTestData.Tenant),
            new ConfigurationBuilder().Build());

        var response = await handler.Handle(
            new GetTaskFieldOptionSourcesQuery(TaskFieldOptionsSourceKind.ModuleRecord, "corr"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(
            [DepartmentSource, SupplierSource],
            response.Data!.Select(dto => dto.Key).OrderBy(key => key, StringComparer.Ordinal).ToList());
        // Each one says which module owns it, so the administrator picks by meaning rather than by key spelling.
        Assert.All(response.Data!, dto => Assert.False(string.IsNullOrWhiteSpace(dto.ModuleCode)));
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A tenant with no reference sets at all: this suite is about record sources, not about BRD.</summary>
    private static IBusinessReferenceDataStewardshipRepository NoReferenceSets()
    {
        var repository = new Mock<IBusinessReferenceDataStewardshipRepository>();
        repository
            .Setup(r => r.QuerySetsAsync(It.IsAny<BusinessReferenceDataSetListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<BusinessReferenceDataSet>)[], 0L));
        return repository.Object;
    }

    private static TaskFieldDefinition RecordDefinition(string code, string sourceKey) => new()
    {
        TenantId = TaskTestData.Tenant,
        Code = code,
        LabelText = code,
        ValueType = TaskFieldValueType.Reference,
        Section = "Delivery",
        OptionsSourceKind = TaskFieldOptionsSourceKind.ModuleRecord,
        OptionsSourceKey = sourceKey,
        IsActive = true
    };

    /// <summary>
    /// The options handler with BOTH fake sources registered and the two older source kinds wired to doubles
    /// that THROW. A record field must never reach the lookup provider or the reference-data service — if it
    /// does, the "one path" claim is decoration.
    /// </summary>
    private static (GetTaskFieldDefinitionOptionsHandler Handler, FakeTaskFieldDefinitionRepository Definitions)
        Options(TaskFieldDefinition definition, FakeTaskRecordSource? departments = null)
    {
        var definitions = new FakeTaskFieldDefinitionRepository(definition);
        var handler = new GetTaskFieldDefinitionOptionsHandler(
            definitions,
            // STRICT mocks: any call to them fails the test. A record field must never reach the platform
            // lookup provider or the reference-data service — if it can, the "one path" claim is decoration.
            new Mock<IPlatformLookupProvider>(MockBehavior.Strict).Object,
            new Mock<IBusinessReferenceDataConsumerQueryService>(MockBehavior.Strict).Object,
            TaskRecordSourceDoubles.With(departments ?? Departments(), Suppliers()),
            new FakeTenantContext(TaskTestData.Tenant),
            new ConfigurationBuilder().Build());

        return (handler, definitions);
    }
}
