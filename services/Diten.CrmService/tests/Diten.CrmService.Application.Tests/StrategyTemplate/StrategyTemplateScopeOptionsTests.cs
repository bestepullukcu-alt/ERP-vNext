using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// WP-ST-SCOPE — the cascading scope selector's read: three feeds (country / legal-entity / territory-BU) with three
/// separate readiness flags, and no hardcoded fallback. A deliberate mirror of the campaign selector; these tests wire
/// the three read seams directly.
/// </summary>
public sealed class StrategyTemplateScopeOptionsTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

    private static GetStrategyTemplateScopeOptionsHandler Handler(
        FakeCatalogReader references, FakeLegalEntityCatalog legalEntities, FakeTerritory territory)
        => new(StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
            references, legalEntities, territory);

    [Fact]
    public async Task Three_feeds_are_returned_with_their_readiness_flags()
    {
        var references = new FakeCatalogReader()
            .Publish(StrategyTemplateScopeReferenceSets.CountrySet, "TR", "DE")
            .Publish(StrategyTemplateScopeReferenceSets.BusinessUnitSet, "rx");
        var legalEntities = new FakeLegalEntityCatalog(new LegalEntityLookupOption(Guid.NewGuid(), "LE-1", "Acme TR"));
        var territory = new FakeTerritory("rx", "otc");

        var response = await Handler(references, legalEntities, territory).Handle(
            new GetStrategyTemplateScopeOptionsQuery("TR", Start, End), default);

        Assert.True(response.IsSuccessful);
        var dto = response.Data!;
        Assert.Equal(StrategyTemplateScopeTypes.ByPrecedence, dto.ScopeTypes);
        Assert.True(dto.CountrySetPublished);
        Assert.Equal(2, dto.Countries.Count);
        Assert.True(dto.LegalEntityLookupAvailable);
        Assert.Single(dto.LegalEntities);
        // A window plus matching plans -> the business-unit list is the Territory-derived narrowing.
        Assert.True(dto.BusinessUnitFromTerritory);
        Assert.Equal(2, dto.BusinessUnits.Count);
        Assert.True(dto.BusinessUnitSetPublished);
    }

    [Fact]
    public async Task Without_a_window_the_business_units_fall_back_to_the_published_vocabulary()
    {
        var references = new FakeCatalogReader()
            .Publish(StrategyTemplateScopeReferenceSets.BusinessUnitSet, "rx", "otc", "vaccines");
        var territory = new FakeTerritory("should-not-be-used");

        var response = await Handler(references, new FakeLegalEntityCatalog(), territory).Handle(
            new GetStrategyTemplateScopeOptionsQuery(null, null, null), default);

        Assert.True(response.IsSuccessful);
        Assert.False(response.Data!.BusinessUnitFromTerritory);
        Assert.Equal(3, response.Data.BusinessUnits.Count);
        Assert.Equal(0, territory.Calls);
    }

    [Fact]
    public async Task An_unpublished_country_set_is_reported_as_not_published_and_empty()
    {
        var response = await Handler(new FakeCatalogReader(), new FakeLegalEntityCatalog(), new FakeTerritory())
            .Handle(new GetStrategyTemplateScopeOptionsQuery(null, null, null), default);

        Assert.True(response.IsSuccessful);
        Assert.False(response.Data!.CountrySetPublished);
        Assert.Empty(response.Data.Countries);
    }

    [Fact]
    public async Task An_unreachable_legal_entity_lookup_is_reported_as_unavailable_and_empty()
    {
        var response = await Handler(new FakeCatalogReader(), FakeLegalEntityCatalog.Unavailable(), new FakeTerritory())
            .Handle(new GetStrategyTemplateScopeOptionsQuery(null, null, null), default);

        Assert.True(response.IsSuccessful);
        Assert.False(response.Data!.LegalEntityLookupAvailable);
        Assert.Empty(response.Data.LegalEntities);
    }

    // ---- local read-seam doubles ------------------------------------------------------------------------------

    private sealed class FakeCatalogReader : IReferenceDataCatalogReader
    {
        private readonly Dictionary<string, ReferenceSetSnapshot> _sets = new(StringComparer.OrdinalIgnoreCase);

        public FakeCatalogReader Publish(string setCode, params string[] values)
        {
            _sets[setCode] = new ReferenceSetSnapshot(
                setCode, true,
                values.Select(v => new ReferenceValueSnapshot(v, v, null, true, false, null)).ToList());
            return this;
        }

        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken ct)
            => Task.FromResult(_sets.TryGetValue(setCode, out var s) ? s : ReferenceSetSnapshot.NotPublished(setCode));
    }

    private sealed class FakeLegalEntityCatalog : ICyclePeriodLegalEntityCatalog
    {
        private readonly LegalEntityLookupResult _result;

        public FakeLegalEntityCatalog(params LegalEntityLookupOption[] options)
            => _result = new LegalEntityLookupResult(true, options);

        private FakeLegalEntityCatalog(LegalEntityLookupResult result) => _result = result;

        public static FakeLegalEntityCatalog Unavailable() => new(LegalEntityLookupResult.Unavailable);

        public Task<LegalEntityLookupResult> GetReferenceableAsync(CancellationToken ct)
            => Task.FromResult(_result);
    }

    private sealed class FakeTerritory : ITerritoryBusinessUnitCatalog
    {
        private readonly IReadOnlyList<TerritoryBusinessUnitCandidate> _candidates;
        public int Calls { get; private set; }

        public FakeTerritory(params string[] codes)
            => _candidates = codes.Select(c => new TerritoryBusinessUnitCandidate(c, new[] { "TM-" + c })).ToList();

        public Task<IReadOnlyList<TerritoryBusinessUnitCandidate>> GetCandidatesAsync(
            string? country, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_candidates);
        }
    }
}
