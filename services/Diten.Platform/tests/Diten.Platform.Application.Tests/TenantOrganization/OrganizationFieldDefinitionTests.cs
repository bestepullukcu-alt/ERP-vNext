using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

/// <summary>
/// MOD-0288-FU02 — definition lifecycle: code normalization and immutability, the closed type set, the
/// per-tenant count limit the Tasks precedent never had, deactivation, and classification.
/// </summary>
public sealed class OrganizationFieldDefinitionTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-0000000000d1");

    // ── code ─────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("OU.Permanent Id", "ou.permanent.id")]
    [InlineData("  regulatory-role  ", "regulatory.role")]
    [InlineData("Accountable_Executive", "accountable.executive")]
    public void Codes_that_differ_only_in_shape_normalize_to_one_key(string raw, string expected)
        => Assert.Equal(expected, OrganizationFieldDefinitionRules.NormalizeCode(raw));

    [Fact]
    public async Task A_duplicate_code_is_refused_after_normalization()
    {
        var (definitions, handler) = Handler();
        var first = await handler.Handle(Create("Regulatory Role"), CancellationToken.None);
        Assert.True(first.IsSuccessful);

        var second = await handler.Handle(Create("regulatory-role"), CancellationToken.None);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Single(await definitions.GetAllAsync());
    }

    [Fact]
    public void The_update_request_carries_no_code_at_all()
    {
        /*
         * ⚠ IMMUTABILITY PROVED ON THE CONTRACT, NOT ON A HANDLER BRANCH. §12 says the code cannot change
         * after creation, and the strongest form of that is a request with nowhere to put a new one — a
         * validation branch can be bypassed by a second write path, an absent property cannot.
         */
        Assert.DoesNotContain(
            typeof(UpdateOrganizationFieldDefinitionRequest).GetProperties(),
            p => p.Name.Equals("Code", StringComparison.OrdinalIgnoreCase));
    }

    // ── the closed type set ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_type_set_is_exactly_the_eight_the_pack_names()
    {
        // A ninth member is a follow-up pack, not an edit: each type carries validation, query and index
        // consequences, and one added silently is a value nobody can validate or filter.
        Assert.Equal(
            new[] { "Text", "MultilineText", "Integer", "Decimal", "Boolean", "Date", "SingleSelect", "Reference" },
            Enum.GetNames<OrganizationFieldDataType>());
    }

    [Fact]
    public async Task An_unrecognised_type_is_refused_rather_than_defaulted()
    {
        var (_, handler) = Handler();
        var response = await handler.Handle(
            Create("Freeform", dataType: "Json"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task An_unrecognised_classification_is_refused_rather_than_silently_normal()
    {
        // ⚠ THE DANGEROUS FALLBACK. "Restrcted" quietly becoming Normal would publish a restricted value in
        // the clear, and nothing would announce it.
        var (_, handler) = Handler();
        var response = await handler.Handle(
            Create("Evidence Reference", classification: "Restrcted"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task A_single_select_needs_options_and_a_reference_needs_a_target()
    {
        var (_, handler) = Handler();

        var noOptions = await handler.Handle(
            Create("Regulatory Role", dataType: "SingleSelect"), CancellationToken.None);
        Assert.False(noOptions.IsSuccessful);

        var noTarget = await handler.Handle(
            Create("Accountable Executive", dataType: "Reference"), CancellationToken.None);
        Assert.False(noTarget.IsSuccessful);

        var withTarget = await handler.Handle(
            Create("Accountable Executive 2", dataType: "Reference",
                constraints: new OrganizationFieldConstraintsRequest(ReferenceTarget: "Position")),
            CancellationToken.None);
        Assert.True(withTarget.IsSuccessful);
    }

    // ── the count limit the precedent left open ──────────────────────────────────────────────────────────

    [Fact]
    public async Task The_fifty_first_active_definition_is_refused()
    {
        /*
         * ⚠ THE GAP THIS PACK CLOSES RATHER THAN INHERITS. TaskFieldDefinitionRules caps SECTIONS at six and
         * leaves the field COUNT unbounded; copying that omission would break both the screen that renders
         * the fields and the query that filters them.
         */
        var (definitions, handler) = Handler();
        for (var i = 0; i < OrganizationFieldDefinitionRules.MaxActiveDefinitions; i++)
        {
            definitions.Add(Definition($"f{i}"));
        }

        var response = await handler.Handle(Create("One Too Many"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(OrganizationFieldDefinitionRules.DefinitionLimitMessage, Assert.Single(response.Errors));
    }

    [Fact]
    public async Task Retired_definitions_do_not_occupy_a_slot()
    {
        var (definitions, handler) = Handler();
        for (var i = 0; i < OrganizationFieldDefinitionRules.MaxActiveDefinitions; i++)
        {
            var d = Definition($"f{i}");
            d.IsActive = i != 0;      // one of the fifty is retired
            definitions.Add(d);
        }

        var response = await handler.Handle(Create("Replacement"), CancellationToken.None);
        Assert.True(response.IsSuccessful);
    }

    // ── deactivation ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deactivation_retires_the_definition_and_keeps_it_readable()
    {
        var definitions = new InMemoryOrganizationFieldDefinitionRepository(TenantId);
        var definition = Definition("regulatory.role");
        definitions.Add(definition);

        var response = await new DeactivateOrganizationFieldDefinitionCommandHandler(definitions)
            .Handle(new DeactivateOrganizationFieldDefinitionCommand(definition.Id, definition.Version), CancellationToken.None);

        Assert.True(response.IsSuccessful);

        var stored = await definitions.GetByIdAsync(definition.Id);
        Assert.False(stored!.IsActive);
        Assert.Null(stored.DeletedAt);          // retired, NOT deleted
        Assert.False(stored.IsDeleted);

        // The default read hides it; asking for it explicitly still returns it.
        var visible = await new GetOrganizationFieldDefinitionsQueryHandler(definitions)
            .Handle(new GetOrganizationFieldDefinitionsQuery(), CancellationToken.None);
        Assert.Empty(visible.Data!);

        var all = await new GetOrganizationFieldDefinitionsQueryHandler(definitions)
            .Handle(new GetOrganizationFieldDefinitionsQuery(IncludeInactive: true), CancellationToken.None);
        Assert.Single(all.Data!);
    }

    [Fact]
    public async Task A_stale_expected_version_is_refused_with_409()
    {
        var definitions = new InMemoryOrganizationFieldDefinitionRepository(TenantId);
        var definition = Definition("regulatory.role");
        definition.Version = 4;
        definitions.Add(definition);

        var response = await new DeactivateOrganizationFieldDefinitionCommandHandler(definitions)
            .Handle(new DeactivateOrganizationFieldDefinitionCommand(definition.Id, 3), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task The_type_cannot_change_once_a_value_exists()
    {
        var definitions = new InMemoryOrganizationFieldDefinitionRepository(TenantId);
        var values = new InMemoryOrganizationFieldValueRepository(TenantId);
        var definition = Definition("permanent.ou.id");
        definitions.Add(definition);

        var handler = new UpdateOrganizationFieldDefinitionCommandHandler(definitions, values);
        var request = new UpdateOrganizationFieldDefinitionRequest(
            "Permanent OU ID", "Integer", false, false, 0, definition.Version);

        // With no values stored: the definition is still a draft, so the type may move.
        var beforeAnyValue = await handler.Handle(
            new UpdateOrganizationFieldDefinitionCommand(definition.Id, request), CancellationToken.None);
        Assert.True(beforeAnyValue.IsSuccessful);

        values.Add(new OrganizationFieldValue
        {
            TenantId = TenantId,
            OrganizationUnitId = Guid.NewGuid(),
            DefinitionId = definition.Id,
            ValueType = OrganizationFieldDataType.Integer,
            Value = "7"
        });

        var stored = await definitions.GetByIdAsync(definition.Id);
        var afterAValue = await handler.Handle(
            new UpdateOrganizationFieldDefinitionCommand(
                definition.Id,
                request with { DataType = "Text", ExpectedVersion = stored!.Version }),
            CancellationToken.None);

        Assert.False(afterAValue.IsSuccessful);
        Assert.Equal(409, afterAValue.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static (InMemoryOrganizationFieldDefinitionRepository, CreateOrganizationFieldDefinitionCommandHandler) Handler()
    {
        var definitions = new InMemoryOrganizationFieldDefinitionRepository(TenantId);
        var context = new TenantContext();
        context.SetTenant(TenantId);
        return (definitions, new CreateOrganizationFieldDefinitionCommandHandler(definitions, context));
    }

    private static CreateOrganizationFieldDefinitionCommand Create(
        string name,
        string dataType = "Text",
        string? classification = null,
        OrganizationFieldConstraintsRequest? constraints = null) =>
        new(new CreateOrganizationFieldDefinitionRequest(
            name, name, dataType, Classification: classification, ValidationRules: constraints));

    private static OrganizationFieldDefinition Definition(string code) => new()
    {
        TenantId = TenantId,
        Code = code,
        Name = code,
        DataType = OrganizationFieldDataType.Text
    };
}
