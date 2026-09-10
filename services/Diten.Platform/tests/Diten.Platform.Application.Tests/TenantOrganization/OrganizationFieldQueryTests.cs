using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Entities.Organization;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

/// <summary>
/// MOD-0288-FU02 — what the value query will and will not answer.
///
/// <para>⚠ THE CENTRAL CLAIM UNDER TEST IS A REFUSAL, NOT A RESULT. A filter naming a non-queryable field is a
/// 400; it is never dropped so the rest of the query can proceed. Silent omission returns a wrong result set
/// that looks exactly like a correct one — which is the failure this whole feature is shaped to avoid.</para>
/// </summary>
public sealed class OrganizationFieldQueryTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid UnitA = Guid.Parse("20000000-0000-0000-0000-0000000000c1");
    private static readonly Guid UnitB = Guid.Parse("20000000-0000-0000-0000-0000000000c2");

    // ── supported operators ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Equality_matches_the_canonical_form_not_the_typed_one()
    {
        var (handler, definitions, values) = Fixture();
        var role = Queryable("regulatory.role", OrganizationFieldDataType.Text);
        definitions.Add(role);
        values.Add(Value(UnitA, role, "Notified body"));
        values.Add(Value(UnitB, role, "Competent authority"));

        var response = await handler.Handle(Query(Filter(role.Id, "eq", "  Notified body  ")), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(UnitA, Assert.Single(response.Data!.Items).OrganizationUnitId);
    }

    [Fact]
    public async Task In_matches_any_of_the_listed_values()
    {
        var (handler, definitions, values) = Fixture();
        var role = Queryable("regulatory.role", OrganizationFieldDataType.Text);
        definitions.Add(role);
        values.Add(Value(UnitA, role, "Notified body"));
        values.Add(Value(UnitB, role, "Competent authority"));

        var response = await handler.Handle(
            Query(Filter(role.Id, "in", "Notified body", "Competent authority")), CancellationToken.None);

        Assert.Equal(2, response.Data!.Items.Count);
    }

    [Fact]
    public async Task Prefix_contains_works_on_text_and_is_refused_on_a_number()
    {
        var (handler, definitions, values) = Fixture();
        var role = Queryable("regulatory.role", OrganizationFieldDataType.Text);
        var headcount = Queryable("headcount", OrganizationFieldDataType.Integer);
        definitions.Add(role);
        definitions.Add(headcount);
        values.Add(Value(UnitA, role, "Notified body"));

        var text = await handler.Handle(Query(Filter(role.Id, "contains", "notif")), CancellationToken.None);
        Assert.Single(text.Data!.Items);

        var number = await handler.Handle(Query(Filter(headcount.Id, "contains", "1")), CancellationToken.None);
        Assert.False(number.IsSuccessful);
        Assert.Equal(400, number.StatusCode);
    }

    // ── the refusals ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_filter_naming_a_non_queryable_definition_is_a_400_and_not_a_narrowed_result()
    {
        var (handler, definitions, values) = Fixture();
        var secret = Queryable("evidence.reference", OrganizationFieldDataType.Text);
        secret.IsQueryable = false;
        definitions.Add(secret);
        values.Add(Value(UnitA, secret, "EV-0417"));

        var response = await handler.Handle(Query(Filter(secret.Id, "eq", "EV-0417")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(OrganizationFieldDefinitionRules.NotQueryableMessage, Assert.Single(response.Errors));
    }

    [Fact]
    public async Task A_range_across_two_definitions_is_refused_because_the_values_are_not_comparable()
    {
        /*
         * ⚠ `Value` holds text, numbers and dates in ONE field, so a range across a mixed set compares BSON
         * type order rather than values — silently, and the answer looks like an answer.
         */
        var (handler, definitions, values) = Fixture();
        var opened = Queryable("opened.on", OrganizationFieldDataType.Date);
        var role = Queryable("regulatory.role", OrganizationFieldDataType.Text);
        definitions.Add(opened);
        definitions.Add(role);

        var mixed = await handler.Handle(
            Query(Filter(opened.Id, "gte", "2026-01-01"), Filter(role.Id, "eq", "Notified body")),
            CancellationToken.None);

        Assert.False(mixed.IsSuccessful);
        Assert.Equal(OrganizationFieldDefinitionRules.MixedRangeMessage, Assert.Single(mixed.Errors));

        // The same range, alone on one explicitly typed definition: allowed.
        var alone = await handler.Handle(Query(Filter(opened.Id, "gte", "2026-01-01")), CancellationToken.None);
        Assert.True(alone.IsSuccessful);
    }

    [Fact]
    public async Task A_numeric_range_is_refused_even_when_it_is_the_only_definition()
    {
        /*
         * ⚠ THE CAVEAT FOUND WHILE IMPLEMENTING THE PACK'S OWN. Even one explicitly typed definition is not
         * orderable when its canonical form is a string whose lexicographic order differs from its value
         * order — "10" sorts before "9". Refused rather than answered wrongly.
         */
        var (handler, definitions, _) = Fixture();
        var headcount = Queryable("headcount", OrganizationFieldDataType.Integer);
        definitions.Add(headcount);

        var response = await handler.Handle(Query(Filter(headcount.Id, "gt", "9")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(OrganizationFieldDefinitionRules.NumericOrderingMessage, Assert.Single(response.Errors));
    }

    [Fact]
    public async Task An_unknown_operator_is_refused()
    {
        var (handler, definitions, _) = Fixture();
        var role = Queryable("regulatory.role", OrganizationFieldDataType.Text);
        definitions.Add(role);

        var response = await handler.Handle(Query(Filter(role.Id, "regex", ".*")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    // ── paging ───────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    [InlineData(0, false)]
    public async Task Paging_is_bounded_at_two_hundred(int pageSize, bool accepted)
    {
        var (handler, _, _) = Fixture();
        var response = await handler.Handle(
            new GetOrganizationFieldValuesQuery(PageSize: pageSize), CancellationToken.None);

        Assert.Equal(accepted, response.IsSuccessful);
    }

    // ── classification on read ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_restricted_value_is_omitted_from_the_payload_without_the_grant()
    {
        /*
         * ⚠ OMITTED, NOT MASKED. A mask that travels is a value that travelled. The ROW is still returned:
         * dropping it would narrow the result silently, which this feature refuses everywhere else.
         */
        var permissions = new FakePermissions();
        var (handler, definitions, values) = Fixture(permissions);

        var evidence = Queryable("evidence.reference", OrganizationFieldDataType.Text);
        evidence.Classification = OrganizationFieldClassification.Restricted;
        definitions.Add(evidence);
        var stored = Value(UnitA, evidence, "EV-0417");
        stored.Classification = OrganizationFieldClassification.Restricted;
        values.Add(stored);

        var denied = await handler.Handle(
            new GetOrganizationFieldValuesQuery(OrganizationUnitId: UnitA), CancellationToken.None);
        var hidden = Assert.Single(denied.Data!.Items);
        Assert.True(hidden.Redacted);
        Assert.Null(hidden.Value);

        permissions.Granted.Add(
            OrganizationFieldMapper.ReadPermissionFor(OrganizationFieldClassification.Restricted)!);

        var allowed = await handler.Handle(
            new GetOrganizationFieldValuesQuery(OrganizationUnitId: UnitA), CancellationToken.None);
        var shown = Assert.Single(allowed.Data!.Items);
        Assert.False(shown.Redacted);
        Assert.Equal("EV-0417", shown.Value);
    }

    [Fact]
    public async Task An_unclassified_value_needs_no_extra_grant()
    {
        // Turning classification on must change nothing for the fields nobody classified.
        var permissions = new FakePermissions();
        var (handler, definitions, values) = Fixture(permissions);
        var role = Queryable("regulatory.role", OrganizationFieldDataType.Text);
        definitions.Add(role);
        values.Add(Value(UnitA, role, "Notified body"));

        var response = await handler.Handle(
            new GetOrganizationFieldValuesQuery(OrganizationUnitId: UnitA), CancellationToken.None);

        Assert.Equal("Notified body", Assert.Single(response.Data!.Items).Value);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static (GetOrganizationFieldValuesQueryHandler, InMemoryOrganizationFieldDefinitionRepository,
        InMemoryOrganizationFieldValueRepository) Fixture(IActorPermissionContext? permissions = null)
    {
        var definitions = new InMemoryOrganizationFieldDefinitionRepository(TenantId);
        var values = new InMemoryOrganizationFieldValueRepository(TenantId);
        return (new GetOrganizationFieldValuesQueryHandler(definitions, values, permissions ?? new AllowAll()),
            definitions, values);
    }

    private static GetOrganizationFieldValuesQuery Query(params OrganizationFieldValueFilterRequest[] filters) =>
        new(Filters: filters);

    private static OrganizationFieldValueFilterRequest Filter(Guid definitionId, string op, params string[] values) =>
        new(definitionId, op, values);

    private static OrganizationFieldDefinition Queryable(string code, OrganizationFieldDataType type) => new()
    {
        TenantId = TenantId,
        Code = code,
        Name = code,
        DataType = type,
        IsQueryable = true
    };

    private static OrganizationFieldValue Value(Guid unitId, OrganizationFieldDefinition definition, string value) => new()
    {
        TenantId = TenantId,
        OrganizationUnitId = unitId,
        DefinitionId = definition.Id,
        ValueType = definition.DataType,
        Value = value,
        Classification = definition.Classification
    };

    private sealed class AllowAll : IActorPermissionContext
    {
        public bool IsPlatformActor => true;
        public bool Has(string? permissionKey) => true;
    }

    private sealed class FakePermissions : IActorPermissionContext
    {
        public HashSet<string> Granted { get; } = new(StringComparer.Ordinal);
        public bool IsPlatformActor => false;
        public bool Has(string? permissionKey) =>
            string.IsNullOrWhiteSpace(permissionKey) || Granted.Contains(permissionKey);
    }
}
