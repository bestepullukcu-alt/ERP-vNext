using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 — configurable fields are what keep the engine generic (Phase, Work Type, Market… are definitions, not
// columns). These tests hold that line: an undefined field is rejected rather than becoming an ad-hoc column, and
// the executable contract's bounds are enforced server-side.
public sealed class TaskFieldDefinitionServiceTests
{
    [Fact]
    public async Task No_values_is_valid()
    {
        var result = await Service().ValidateAndMaterializeAsync(null);
        Assert.True(result.IsValid);
        Assert.Empty(result.Values);
    }

    [Fact]
    public async Task An_undefined_field_code_is_rejected()
    {
        // Accepting this would smuggle a column into a generic engine — the exact failure K1 prevents.
        var result = await Service(Definition("regulatory.phase", TaskFieldValueType.Text))
            .ValidateAndMaterializeAsync([new TaskFieldValueDto("finance.fiscalperiod", TaskFieldValueType.Text, "Q3")]);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldDefinitionUnknown, result.ReasonCode);
    }

    [Fact]
    public async Task A_value_type_mismatch_is_rejected()
    {
        var result = await Service(Definition("regulatory.phase", TaskFieldValueType.Text))
            .ValidateAndMaterializeAsync([new TaskFieldValueDto("regulatory.phase", TaskFieldValueType.Number, "3")]);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldValueInvalid, result.ReasonCode);
    }

    [Fact]
    public async Task A_required_field_cannot_be_blank()
    {
        var definition = Definition("regulatory.phase", TaskFieldValueType.Text);
        definition.IsRequired = true;

        var result = await Service(definition)
            .ValidateAndMaterializeAsync([new TaskFieldValueDto("regulatory.phase", TaskFieldValueType.Text, "  ")]);

        Assert.False(result.IsValid);
    }

    /*
     * THE ASYMMETRY, closed. Requiredness used to be checked only for a field that had been SUPPLIED, so the way
     * around it was to leave the field out: the form blocked an empty box, and anything that skipped the form —
     * curl, a stale page, another client — stored a task with the required field simply absent. "Required" was a
     * client-side opinion. Both sides hold it now.
     */
    [Fact]
    public async Task A_required_field_that_is_OMITTED_is_rejected_too()
    {
        var definition = Definition("regulatory.phase", TaskFieldValueType.Text);
        definition.IsRequired = true;

        var result = await Service(definition).ValidateAndMaterializeAsync(null);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldValueInvalid, result.ReasonCode);
    }

    [Fact]
    public async Task A_partial_payload_still_has_to_carry_every_required_field()
    {
        var optional = Definition("regulatory.phase", TaskFieldValueType.Text);
        var required = Definition("regulatory.market", TaskFieldValueType.Text);
        required.IsRequired = true;

        var result = await Service(optional, required)
            .ValidateAndMaterializeAsync([new TaskFieldValueDto("regulatory.phase", TaskFieldValueType.Text, "II")]);

        Assert.False(result.IsValid);
        Assert.Contains("regulatory.market", result.Message);
    }

    [Fact]
    public async Task A_machine_made_task_is_not_refused_for_a_field_nobody_could_have_filled()
    {
        /*
         * The recurrence sweep and creation-from-template pass enforceRequired: false, and say so at their call
         * sites. A sweep has nobody to ask: refusing would not collect the value, it would stop the recurrence
         * while the period is consumed anyway — the same failure this module already shipped once for assignment.
         */
        var definition = Definition("regulatory.phase", TaskFieldValueType.Text);
        definition.IsRequired = true;

        var result = await Service(definition)
            .ValidateAndMaterializeAsync(null, CancellationToken.None, enforceRequired: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Duplicate_values_for_one_definition_are_rejected()
    {
        var result = await Service(Definition("regulatory.phase", TaskFieldValueType.Text))
            .ValidateAndMaterializeAsync([
                new TaskFieldValueDto("regulatory.phase", TaskFieldValueType.Text, "A"),
                new TaskFieldValueDto("regulatory.phase", TaskFieldValueType.Text, "B")
            ]);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Text_longer_than_the_contract_limit_is_rejected()
    {
        var result = await Service(Definition("notes.long", TaskFieldValueType.Text))
            .ValidateAndMaterializeAsync([
                new TaskFieldValueDto("notes.long", TaskFieldValueType.Text,
                    new string('x', TaskFieldLimits.MaxTextLengthPerField + 1))
            ]);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldLimitExceeded, result.ReasonCode);
    }

    [Theory]
    [InlineData(TaskFieldValueType.Number, "12.5", true)]
    [InlineData(TaskFieldValueType.Number, "not-a-number", false)]
    [InlineData(TaskFieldValueType.Boolean, "true", true)]
    [InlineData(TaskFieldValueType.Boolean, "yes", false)]
    [InlineData(TaskFieldValueType.Date, "2026-07-30", true)]
    [InlineData(TaskFieldValueType.Date, "30/07/2026-bad", false)]
    [InlineData(TaskFieldValueType.Link, "/Tasks/1", true)]
    [InlineData(TaskFieldValueType.Link, "https://example.com", true)]
    [InlineData(TaskFieldValueType.Link, "javascript:alert(1)", false)]
    [InlineData(TaskFieldValueType.Link, "//evil.example.com", false)]
    public async Task Value_shape_is_checked_per_allowlisted_type(
        TaskFieldValueType type, string value, bool expectedValid)
    {
        var result = await Service(Definition("f", type))
            .ValidateAndMaterializeAsync([new TaskFieldValueDto("f", type, value)]);

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task More_than_eight_fields_in_one_section_is_rejected()
    {
        var definitions = Enumerable.Range(0, TaskFieldLimits.MaxFieldsPerSection + 1)
            .Select(i => Definition($"sec.f{i}", TaskFieldValueType.Text, section: "General"))
            .ToArray();

        var values = definitions
            .Select(d => new TaskFieldValueDto(d.Code, TaskFieldValueType.Text, "v"))
            .ToList();

        var result = await Service(definitions).ValidateAndMaterializeAsync(values);

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldLimitExceeded, result.ReasonCode);
    }

    [Fact]
    public async Task More_than_eight_primary_fields_is_rejected()
    {
        var definitions = Enumerable.Range(0, TaskFieldLimits.MaxPrimaryFields + 1)
            .Select(i =>
            {
                // Spread across sections so the per-section cap is not what trips first.
                var d = Definition($"p.f{i}", TaskFieldValueType.Text, section: $"S{i % 3}");
                d.Importance = TaskFieldImportance.Primary;
                return d;
            })
            .ToArray();

        var result = await Service(definitions).ValidateAndMaterializeAsync(
            definitions.Select(d => new TaskFieldValueDto(d.Code, TaskFieldValueType.Text, "v")).ToList());

        Assert.False(result.IsValid);
        Assert.Equal(TaskReasonCodes.FieldLimitExceeded, result.ReasonCode);
    }

    [Fact]
    public async Task Classification_metadata_is_copied_from_the_definition_so_BL_024_is_additive()
    {
        var definition = Definition("cost.amount", TaskFieldValueType.Currency);
        definition.Classification = TaskFieldClassification.Confidential;
        definition.DefaultAccessState = TaskFieldAccessState.Masked;

        var result = await Service(definition)
            .ValidateAndMaterializeAsync([new TaskFieldValueDto("cost.amount", TaskFieldValueType.Currency, "1000")]);

        Assert.True(result.IsValid);
        var value = Assert.Single(result.Values);
        Assert.Equal(TaskFieldClassification.Confidential, value.Classification);
        Assert.Equal(TaskFieldAccessState.Masked, value.AccessState);
        // Phase 1 stores the metadata but performs NO redaction decision yet.
        Assert.False(value.Redacted);
    }

    private static TaskFieldDefinitionService Service(params TaskFieldDefinition[] definitions)
        => new(new FakeTaskFieldDefinitionRepository(definitions), TaskRecordSourceDoubles.None, TaskActors.PermitAll());

    /// <summary>The same service, given the module record sources a record-backed definition points at.</summary>
    private static TaskFieldDefinitionService ServiceWithSources(
        Diten.Platform.Application.Features.Tasks.Services.ITaskRecordSource[] sources,
        params TaskFieldDefinition[] definitions)
        => new(new FakeTaskFieldDefinitionRepository(definitions), TaskRecordSourceDoubles.With(sources), TaskActors.PermitAll());

    private static TaskFieldDefinition Definition(
        string code, TaskFieldValueType type, string section = "General") => new()
    {
        TenantId = TaskTestData.Tenant,
        Code = code,
        LabelResourceKey = $"Field_{code}",
        ValueType = type,
        Section = section,
        IsActive = true
    };
}
