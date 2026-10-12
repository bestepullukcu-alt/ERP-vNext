using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CyclePeriod;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Tests.CyclePeriod;

/// <summary>
/// WP-CAP-MODEL (K-2) — the cycle-code suggestion: one format per scope level, NN = the first free sequence in the
/// scope and year (closed periods hold theirs), and nothing written.
/// </summary>
public sealed class CyclePeriodCodeSuggestionTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegalEntityX = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid LegalEntityUnknown = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");

    private sealed class FakeRepo : ICyclePeriodRepository
    {
        public List<PeriodEntity> Items { get; } = new();
        public int Writes { get; private set; }

        private IReadOnlyList<PeriodEntity> Scope(Guid tenantId)
            => Items.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList();

        public Task<PeriodEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Scope(tenantId).FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<PeriodEntity>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(Scope(tenantId));

        public Task<IReadOnlyList<PeriodEntity>> ListByCodeAsync(Guid tenantId, string cycleCode, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(Scope(tenantId).Where(x => x.CycleCode == cycleCode).ToList());

        public Task<IReadOnlyList<PeriodEntity>> ListByYearAsync(Guid tenantId, int year, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(Scope(tenantId).Where(x => x.Year == year).ToList());

        public Task<IReadOnlyList<PeriodEntity>> ListActiveAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(Scope(tenantId).Where(x => x.IsActive()).ToList());

        public Task InsertAsync(PeriodEntity entity, CancellationToken ct)
        {
            Writes++;
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceAsync(PeriodEntity entity, int expectedVersion, CancellationToken ct)
        {
            Writes++;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeLegalEntities : ICyclePeriodLegalEntityCatalog
    {
        public LegalEntityLookupResult Result { get; set; } = new(true, new[]
        {
            new LegalEntityLookupOption(LegalEntityX, "l1", "Grand Medical İlaç A.Ş.")
        });

        public Task<LegalEntityLookupResult> GetReferenceableAsync(CancellationToken ct) => Task.FromResult(Result);
    }

    private sealed class FakeCatalog : IReferenceDataCatalogReader
    {
        public ReferenceSetSnapshot BusinessUnits { get; set; } = new(
            CyclePeriodReferenceSets.BusinessUnitSet,
            true,
            new[] { new ReferenceValueSnapshot("b1", "İstanbul Anadolu", null, true, false, null) });

        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken ct)
            => Task.FromResult(setCode == CyclePeriodReferenceSets.BusinessUnitSet
                ? BusinessUnits
                : ReferenceSetSnapshot.NotPublished(setCode));
    }

    private sealed record Harness(FakeRepo Repo, FakeLegalEntities LegalEntities, FakeCatalog Catalog,
        GetCyclePeriodCodeSuggestionHandler Handler);

    private static Harness Build(Guid tenantId)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(tenantId);
        var repo = new FakeRepo();
        var legal = new FakeLegalEntities();
        var catalog = new FakeCatalog();
        return new Harness(repo, legal, catalog, new GetCyclePeriodCodeSuggestionHandler(tenant, repo, legal, catalog));
    }

    private static PeriodEntity Row(
        Guid tenantId, int year, int sequence, string scopeType, string? country = null, Guid? legalEntityId = null,
        string? businessUnitId = null, string status = CyclePeriodStatuses.Draft)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CycleCode = $"x-{Guid.NewGuid():N}",
            CycleName = "row",
            Year = year,
            SequenceInYear = sequence,
            ScopeType = scopeType,
            CountryScope = country,
            LegalEntityId = legalEntityId,
            BusinessUnitId = businessUnitId,
            CycleStatus = status
        };

    [Fact]
    public async Task CS01_Country_Scope_Uses_Iso2_And_The_First_Free_Sequence_Counting_Closed_Rows()
    {
        var h = Build(TenantA);
        h.Repo.Items.Add(Row(TenantA, 2026, 1, CyclePeriodScopeTypes.Country, "TR", status: CyclePeriodStatuses.Closed));
        h.Repo.Items.Add(Row(TenantA, 2026, 2, CyclePeriodScopeTypes.Country, "TR", status: CyclePeriodStatuses.Closed));
        h.Repo.Items.Add(Row(TenantA, 2026, 3, CyclePeriodScopeTypes.Country, "TR", status: CyclePeriodStatuses.Active));
        h.Repo.Items.Add(Row(TenantA, 2026, 5, CyclePeriodScopeTypes.Country, "TR"));
        // Other scopes, other years and other tenants never take a sequence from this one.
        h.Repo.Items.Add(Row(TenantA, 2026, 4, CyclePeriodScopeTypes.Country, "BY"));
        h.Repo.Items.Add(Row(TenantA, 2025, 4, CyclePeriodScopeTypes.Country, "TR"));
        h.Repo.Items.Add(Row(TenantA, 2026, 4, CyclePeriodScopeTypes.Tenant));
        h.Repo.Items.Add(Row(TenantB, 2026, 4, CyclePeriodScopeTypes.Country, "TR"));

        var response = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.Country, "tr", null, null, 2026), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("TR-2026-04", response.Data!.SuggestedCode);
        Assert.Equal(4, response.Data.NextSequenceInYear);
        Assert.Equal(0, h.Repo.Writes);
    }

    [Fact]
    public async Task CS02_Whole_Company_Uses_GM()
    {
        var h = Build(TenantA);
        var response = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.Tenant, null, null, null, 2027), CancellationToken.None);

        Assert.Equal("GM-2027-01", response.Data!.SuggestedCode);
        Assert.Equal(1, response.Data.NextSequenceInYear);
    }

    [Fact]
    public async Task CS03_Legal_Entity_Uses_Its_Catalog_Code_Else_LE()
    {
        var h = Build(TenantA);
        h.Repo.Items.Add(Row(TenantA, 2026, 1, CyclePeriodScopeTypes.LegalEntity, legalEntityId: LegalEntityX));

        var known = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.LegalEntity, null, LegalEntityX, null, 2026),
            CancellationToken.None);
        Assert.Equal("L1-2026-02", known.Data!.SuggestedCode);

        var unknown = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.LegalEntity, null, LegalEntityUnknown, null, 2026),
            CancellationToken.None);
        Assert.Equal("LE-2026-01", unknown.Data!.SuggestedCode);

        // MDM unreachable degrades the prefix; it never fails the suggestion.
        h.LegalEntities.Result = LegalEntityLookupResult.Unavailable;
        var unavailable = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.LegalEntity, null, LegalEntityX, null, 2026),
            CancellationToken.None);
        Assert.Equal("LE-2026-02", unavailable.Data!.SuggestedCode);
    }

    [Fact]
    public async Task CS04_Business_Unit_Uses_Its_Catalog_Code_Else_BU()
    {
        var h = Build(TenantA);

        var known = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.BusinessUnit, null, null, "B1", 2026),
            CancellationToken.None);
        Assert.Equal("B1-2026-01", known.Data!.SuggestedCode);

        var unknown = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.BusinessUnit, null, null, "zz", 2026),
            CancellationToken.None);
        Assert.Equal("BU-2026-01", unknown.Data!.SuggestedCode);
    }

    [Fact]
    public async Task CS05_The_Suggested_Code_Passes_The_Write_Path_Code_Rule()
    {
        var h = Build(TenantA);
        var response = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.Country, "TR", null, null, 2026), CancellationToken.None);

        Assert.Null(CyclePeriodValidation.ValidateCycleCode(response.Data!.SuggestedCode));
    }

    [Fact]
    public async Task CS06_Bad_Input_Is_400_And_A_Full_Year_Is_409()
    {
        var h = Build(TenantA);

        var noYear = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.Country, "TR", null, null, 0), CancellationToken.None);
        Assert.Equal(400, noYear.StatusCode);

        var badScope = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.Country, null, null, null, 2026), CancellationToken.None);
        Assert.Equal(400, badScope.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeReferenceRequired, badScope.Errors!);

        for (var sequence = CyclePeriodLimits.MinSequenceInYear; sequence <= CyclePeriodLimits.MaxSequenceInYear; sequence++)
        {
            h.Repo.Items.Add(Row(TenantA, 2026, sequence, CyclePeriodScopeTypes.Tenant, status: CyclePeriodStatuses.Closed));
        }

        var full = await h.Handler.Handle(
            new GetCyclePeriodCodeSuggestionQuery(CyclePeriodScopeTypes.Tenant, null, null, null, 2026), CancellationToken.None);
        Assert.Equal(409, full.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.SequenceTaken, full.Errors!);
    }

    [Theory]
    [InlineData("l1", "L1")]
    [InlineData("  -gm pharma/by ", "GMPHARMABY")]
    [InlineData("---", null)]
    [InlineData(null, null)]
    public void CS07_Catalog_Codes_Are_Sanitised(string? raw, string? expected)
        => Assert.Equal(expected, CyclePeriodCodeSuggestionRules.Sanitize(raw));
}
