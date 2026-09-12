using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

/// <summary>
/// MOD-0288-FU02 — typed values: canonical encoding, one active value per unit+definition, tenant isolation,
/// compare-and-set, clearing, and the seven governance fields expressed WITHOUT seven new properties.
/// </summary>
public sealed class OrganizationFieldValueTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid LegalEntityId = Guid.Parse("10000000-0000-0000-0000-0000000000a1");

    // ── canonical typing, measured against the PRODUCTION rules class ────────────────────────────────────

    [Theory]
    [InlineData(OrganizationFieldDataType.Integer, " 007 ", "7")]
    [InlineData(OrganizationFieldDataType.Integer, "-3", "-3")]
    [InlineData(OrganizationFieldDataType.Decimal, "1.50", "1.50")]
    [InlineData(OrganizationFieldDataType.Boolean, "TRUE", "true")]
    [InlineData(OrganizationFieldDataType.Date, "2026-1-2", "2026-01-02")]
    [InlineData(OrganizationFieldDataType.Text, "  Head Office  ", "Head Office")]
    public void Values_are_stored_in_one_canonical_form(OrganizationFieldDataType type, string raw, string expected)
    {
        /*
         * ⚠ WHY CANONICAL AND NOT AS-TYPED. "1", "01" and " 1 " are the same integer, and an equality filter
         * matching only one of the three returns a result set that is wrong and looks correct. The same
         * reasoning fixes dates to yyyy-MM-dd and booleans to true/false.
         */
        var (canonical, message, _) =
            OrganizationFieldDefinitionRules.CanonicalizeValue(Definition("f", type), raw);

        Assert.Null(message);
        Assert.Equal(expected, canonical);
    }

    [Theory]
    [InlineData(OrganizationFieldDataType.Integer, "1.5")]
    [InlineData(OrganizationFieldDataType.Decimal, "abc")]
    [InlineData(OrganizationFieldDataType.Boolean, "yes")]
    [InlineData(OrganizationFieldDataType.Date, "31/12/2026")]
    [InlineData(OrganizationFieldDataType.Reference, "not-a-guid")]
    public void A_value_the_type_cannot_hold_is_refused(OrganizationFieldDataType type, string raw)
    {
        var definition = Definition("f", type);
        if (type == OrganizationFieldDataType.Reference)
        {
            definition.ValidationRules = new OrganizationFieldConstraints
            {
                ReferenceTarget = OrganizationFieldReferenceTarget.Position
            };
        }

        var (canonical, message, status) = OrganizationFieldDefinitionRules.CanonicalizeValue(definition, raw);

        Assert.Null(canonical);
        Assert.NotNull(message);
        Assert.Equal(400, status);
    }

    [Fact]
    public void Constraints_are_enforced_on_the_way_in()
    {
        var definition = Definition("f", OrganizationFieldDataType.Integer);
        definition.ValidationRules = new OrganizationFieldConstraints { MinValue = 1, MaxValue = 10 };

        Assert.NotNull(OrganizationFieldDefinitionRules.CanonicalizeValue(definition, "11").Message);
        Assert.Null(OrganizationFieldDefinitionRules.CanonicalizeValue(definition, "10").Message);
    }

    // ── one value per unit per definition ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_second_value_for_the_same_unit_and_definition_is_refused()
    {
        var f = await FixtureAsync();

        var first = await f.Handler.Handle(Set(f.Unit.Id, f.Definition.Id, "Notified body"), CancellationToken.None);
        Assert.True(first.IsSuccessful);
        Assert.Equal(201, first.StatusCode);

        /*
         * ⚠ THE FAKE MIRRORS THE UNIQUE PARTIAL INDEX, because that is where production enforces this — not
         * in the handler's read. A handler-only rule is one a second concurrent writer walks straight past.
         */
        var duplicate = new OrganizationFieldValue
        {
            TenantId = TenantId,
            OrganizationUnitId = f.Unit.Id,
            DefinitionId = f.Definition.Id,
            ValueType = OrganizationFieldDataType.Text,
            Value = "Something else"
        };
        Assert.False(await f.Values.TryInsertAsync(duplicate));
    }

    [Fact]
    public async Task The_database_itself_refuses_a_second_value_for_the_same_unit_and_definition()
    {
        /*
         * ⚠ AGAINST A REAL MONGO, BECAUSE THE RULE IS AN INDEX AND NOT A BRANCH. "One active value per unit per
         * definition" is enforced by the unique partial index
         * ux_organization_field_values_tenant_unit_definition_active — deliberately, so that a second writer
         * arriving between another handler's read and its write is refused by the database rather than waved
         * through into a duplicate row nobody notices. A fake cannot demonstrate that; it can only demonstrate
         * that a fake was written to agree.
         */
        await using var harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Organization);
        var repository = new OrganizationFieldValueRepository(harness.DbContext, harness.TenantContext);

        var unitId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();

        Assert.True(await repository.TryInsertAsync(Row(harness.TenantId, unitId, definitionId, "First")));

        // The duplicate is refused — reported as false, not thrown as a 500 and not silently stored.
        Assert.False(await repository.TryInsertAsync(Row(harness.TenantId, unitId, definitionId, "Second")));
        Assert.Equal("First", (await repository.GetAsync(unitId, definitionId))!.Value);

        // ⚠ AND THE FILTER IS PARTIAL, WHICH IS THE OTHER HALF. Once the value is cleared, the pair is free
        // again — a soft-deleted row must not permanently block the field from ever being filled in.
        var stored = await repository.GetAsync(unitId, definitionId);
        Assert.True(await repository.TrySoftDeleteAsync(stored!.Id, stored.Version));
        Assert.True(await repository.TryInsertAsync(Row(harness.TenantId, unitId, definitionId, "Third")));
        Assert.Equal("Third", (await repository.GetAsync(unitId, definitionId))!.Value);

        /*
         * ⚠ AND A ROW CANNOT BE WRITTEN INTO ANOTHER TENANT AT ALL — which is a stronger statement than "the
         * index is tenant-first", and it is what the code actually does. TenantRepository stamps the request
         * context's tenant over whatever the entity carried, so a client-supplied TenantId does not merely go
         * unused: it cannot reach the database. The insert below is therefore refused as a duplicate of THIS
         * tenant's row rather than accepted as a foreign one, and the single stored row still belongs here.
         */
        Assert.False(await repository.TryInsertAsync(Row(OtherTenantId, unitId, definitionId, "Foreign")));
        Assert.Equal(harness.TenantId, Assert.Single(await repository.GetByUnitAsync(unitId)).TenantId);
    }

    [Fact]
    public async Task Setting_the_value_again_updates_it_in_place()
    {
        var f = await FixtureAsync();
        await f.Handler.Handle(Set(f.Unit.Id, f.Definition.Id, "First"), CancellationToken.None);

        var stored = await f.Values.GetAsync(f.Unit.Id, f.Definition.Id);
        var versionBefore = stored!.Version;
        var second = await f.Handler.Handle(
            Set(f.Unit.Id, f.Definition.Id, "Second", versionBefore), CancellationToken.None);

        Assert.True(second.IsSuccessful);
        var after = await f.Values.GetAsync(f.Unit.Id, f.Definition.Id);
        Assert.Equal("Second", after!.Value);
        Assert.Equal(versionBefore + 1, after.Version);
    }

    [Fact]
    public async Task A_stale_expected_version_is_refused_with_409()
    {
        var f = await FixtureAsync();
        await f.Handler.Handle(Set(f.Unit.Id, f.Definition.Id, "First"), CancellationToken.None);

        var stored = await f.Values.GetAsync(f.Unit.Id, f.Definition.Id);
        var stale = await f.Handler.Handle(
            Set(f.Unit.Id, f.Definition.Id, "Second", stored!.Version - 1), CancellationToken.None);

        Assert.False(stale.IsSuccessful);
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal("First", (await f.Values.GetAsync(f.Unit.Id, f.Definition.Id))!.Value);
    }

    // ── clearing, requiredness, retirement ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_blank_value_clears_an_optional_field_and_is_refused_on_a_required_one()
    {
        var f = await FixtureAsync();
        await f.Handler.Handle(Set(f.Unit.Id, f.Definition.Id, "Something"), CancellationToken.None);

        var stored = await f.Values.GetAsync(f.Unit.Id, f.Definition.Id);
        var cleared = await f.Handler.Handle(
            Set(f.Unit.Id, f.Definition.Id, "   ", stored!.Version), CancellationToken.None);
        Assert.True(cleared.IsSuccessful);
        Assert.Null(await f.Values.GetAsync(f.Unit.Id, f.Definition.Id));

        f.Definition.IsRequired = true;
        var required = await f.Handler.Handle(Set(f.Unit.Id, f.Definition.Id, null), CancellationToken.None);
        Assert.False(required.IsSuccessful);
        Assert.Equal(400, required.StatusCode);
    }

    [Fact]
    public async Task A_retired_definition_accepts_no_new_value_but_keeps_the_old_one()
    {
        var f = await FixtureAsync();
        await f.Handler.Handle(Set(f.Unit.Id, f.Definition.Id, "Recorded"), CancellationToken.None);

        f.Definition.IsActive = false;

        var stored = await f.Values.GetAsync(f.Unit.Id, f.Definition.Id);
        var write = await f.Handler.Handle(
            Set(f.Unit.Id, f.Definition.Id, "Changed", stored!.Version), CancellationToken.None);

        Assert.False(write.IsSuccessful);
        Assert.Equal(409, write.StatusCode);
        Assert.Equal("Recorded", (await f.Values.GetAsync(f.Unit.Id, f.Definition.Id))!.Value);
    }

    // ── tenant isolation and non-disclosure ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_tenants_unit_is_answered_with_a_non_disclosing_404()
    {
        var f = await FixtureAsync();
        var foreign = new OrganizationUnit
        {
            TenantId = OtherTenantId, Code = "FOREIGN", Name = "Foreign", LegalEntityId = LegalEntityId
        };
        f.Units.Add(foreign);

        var response = await f.Handler.Handle(Set(foreign.Id, f.Definition.Id, "x"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        // ⚠ 404, not 403: a 403 would confirm the id exists somewhere, which is the disclosure the rule bans.
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Another_tenants_definition_is_answered_with_a_non_disclosing_404()
    {
        var f = await FixtureAsync();
        var foreign = new OrganizationFieldDefinition
        {
            TenantId = OtherTenantId,
            Code = "foreign.field",
            Name = "Foreign",
            DataType = OrganizationFieldDataType.Text
        };
        f.Definitions.Add(foreign);

        var response = await f.Handler.Handle(Set(f.Unit.Id, foreign.Id, "x"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    // ── the acceptance case the pack exists for ──────────────────────────────────────────────────────────

    [Fact]
    public async Task The_seven_register_governance_fields_need_no_new_properties()
    {
        /*
         * §16 criterion 7. The register's seven governance columns — Permanent OU ID, Regulatory role,
         * Accountable executive, Approval reference, Charter reference, IT-directory mapping status, Evidence
         * reference — are DEFINITIONS and VALUES, not seven columns on OrganizationUnit.
         */
        var f = await FixtureAsync();
        var seven = new (string Code, OrganizationFieldDataType Type, string Value)[]
        {
            ("permanent.ou.id", OrganizationFieldDataType.Text, "OU-0028"),
            ("regulatory.role", OrganizationFieldDataType.Text, "Notified body liaison"),
            ("accountable.executive", OrganizationFieldDataType.Text, "COO"),
            ("approval.reference", OrganizationFieldDataType.Text, "GMG-CGV-MTX-0002"),
            ("charter.reference", OrganizationFieldDataType.Text, "CH-2026-11"),
            ("it.directory.mapping.status", OrganizationFieldDataType.Boolean, "true"),
            ("evidence.reference", OrganizationFieldDataType.Text, "EV-0417")
        };

        foreach (var (code, type, value) in seven)
        {
            var definition = Definition(code, type);
            f.Definitions.Add(definition);
            var response = await f.Handler.Handle(Set(f.Unit.Id, definition.Id, value), CancellationToken.None);
            Assert.True(response.IsSuccessful, $"{code} was refused: {string.Join("; ", response.Errors)}");
        }

        Assert.Equal(7, (await f.Values.GetByUnitAsync(f.Unit.Id)).Count);

        // And nothing was added to the entity to hold them.
        var entityProperties = typeof(OrganizationUnit).GetProperties().Select(p => p.Name).ToHashSet();
        foreach (var (code, _, _) in seven)
        {
            var pascal = string.Concat(code.Split('.').Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
            Assert.DoesNotContain(pascal, entityProperties);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Fixture(
        InMemoryOrganizationUnitRepository Units,
        InMemoryOrganizationFieldDefinitionRepository Definitions,
        InMemoryOrganizationFieldValueRepository Values,
        SetOrganizationFieldValueCommandHandler Handler,
        OrganizationUnit Unit,
        OrganizationFieldDefinition Definition);

    private static Task<Fixture> FixtureAsync()
    {
        var units = new InMemoryOrganizationUnitRepository(TenantId);
        var definitions = new InMemoryOrganizationFieldDefinitionRepository(TenantId);
        var values = new InMemoryOrganizationFieldValueRepository(TenantId);
        var positions = new InMemoryPositionRepository(TenantId);

        var unit = new OrganizationUnit
        {
            TenantId = TenantId, Code = "QC-MYG", Name = "Quality Control", LegalEntityId = LegalEntityId
        };
        units.Add(unit);

        var definition = Definition("regulatory.role", OrganizationFieldDataType.Text);
        definitions.Add(definition);

        var context = new TenantContext();
        context.SetTenant(TenantId);

        return Task.FromResult(new Fixture(
            units, definitions, values,
            new SetOrganizationFieldValueCommandHandler(units, definitions, values, positions, context),
            unit, definition));
    }

    private static SetOrganizationFieldValueCommand Set(
        Guid unitId, Guid definitionId, string? value, int? expectedVersion = null) =>
        new(unitId, new SetOrganizationFieldValueRequest(definitionId, value, expectedVersion));

    private static OrganizationFieldValue Row(Guid tenantId, Guid unitId, Guid definitionId, string value) => new()
    {
        TenantId = tenantId,
        OrganizationUnitId = unitId,
        DefinitionId = definitionId,
        ValueType = OrganizationFieldDataType.Text,
        Value = value
    };

    private static OrganizationFieldDefinition Definition(string code, OrganizationFieldDataType type) => new()
    {
        TenantId = TenantId,
        Code = code,
        Name = code,
        DataType = type
    };
}
