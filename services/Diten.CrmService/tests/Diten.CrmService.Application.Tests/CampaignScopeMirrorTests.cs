using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Campaign;
using Diten.CrmService.Application.Features.Campaign.Commands;
using Diten.CrmService.Application.Features.Campaign.Contract;
using Diten.CrmService.Application.Features.Campaign.Handlers;
using Diten.CrmService.Application.Features.Campaign.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Campaign.Queries;
using Diten.CrmService.Application.Features.Campaign.Rules;
using Diten.CrmService.Application.Features.Campaign.Read;
using Diten.CrmService.Application.Features.Campaign.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0165 FU09 — campaign scope mirror + scope-aware cycle binding. Pins down: the discriminated invariant; the
/// pre-FU09 derivation (and that it writes nothing); the governed vocabulary with unpublished-set and unknown-value
/// reported apart; MDM fail-closed with 400 and 503 kept distinct; the applicability set (own address + tenant
/// fallback, and NO cross-axis); that the scope is editable but re-validates the bound period on every write; that a
/// legacy business-unit code stays editable; and that the mirror still behaves identically to the cycle-period rules.
/// </summary>
public sealed class CampaignScopeMirrorTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LegalEntityX = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid LegalEntityY = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private static readonly DateTimeOffset PeriodStart = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ fixture

    private sealed class FakeCampaignRepo : ICampaignRepository
    {
        public List<CampaignEntity> Items { get; } = new();
        public int WriteCount { get; private set; }

        public Task<CampaignEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));

        public Task<IReadOnlyList<CampaignEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CampaignEntity>)Items.Where(c => c.TenantId == t).ToList());

        public Task<CampaignEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.CampaignCode == code && !c.IsArchived()));

        public Task<CampaignEntity?> FindByExternalReferenceAsync(Guid t, string s, string e, CancellationToken ct)
            => Task.FromResult<CampaignEntity?>(null);

        public Task InsertAsync(CampaignEntity campaign, CancellationToken ct)
        {
            WriteCount++;
            Items.Add(campaign);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CampaignEntity campaign, CancellationToken ct)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        public FakeCampaignRepo Campaigns { get; } = new();
        public CampaignScopeTestDoubles.FakeCyclePeriodReader Reader { get; } = new();
        public CampaignScopeTestDoubles.FakeReferenceValidator References { get; } = new();
        public CampaignScopeTestDoubles.FakeLegalEntityValidator LegalEntities { get; } = new();

        public Fixture()
        {
            // The governed sets a healthy tenant would have published.
            References.Publish(CampaignScopeReferenceSets.CountrySet, "TR", "DE");
            References.Publish(CampaignScopeReferenceSets.BusinessUnitSet, "alpha", "beta");
        }

        private TenantContext Tenant()
        {
            var ctx = new TenantContext();
            ctx.SetTenant(TenantA);
            return ctx;
        }

        // FU10 - the targeting gate and code generator. These suites author no targeting mode, so every command
        // derives `manual` and the gate short-circuits before reading a segment: the FU08/FU09 behaviour is unchanged.
        public CampaignScopeTestDoubles.FakeSegmentCatalog SegmentCatalog { get; } = new();

        private CampaignSegmentValidator Targeting => new(SegmentCatalog);

        private ICampaignCodeGenerator CodeGenerator
            => new CampaignCodeGenerator(new CampaignScopeTestDoubles.FakeCampaignCodeSequence(), Campaigns);

        private CampaignScopeWriteValidator Scope => new(References, LegalEntities);

        public CreateCampaignHandler Create()
            => new(Tenant(), new NullActorContext(), Campaigns, new CampaignCycleBindingGuard(Reader), Scope, Targeting, CodeGenerator);

        public UpdateCampaignHandler Update()
            => new(Tenant(), new NullActorContext(), Campaigns, new CampaignCycleBindingGuard(Reader), Scope, Targeting);

        public GetApplicableCyclePeriodsHandler Applicable() => new(Tenant(), Reader);

        public Task<Response<Guid>> CreateAsync(
            string code = "CMP-1",
            string? scopeType = null,
            string? country = null,
            Guid? legalEntityId = null,
            string? businessUnitId = null,
            Guid? cyclePeriodId = null,
            DateTimeOffset? start = null,
            DateTimeOffset? end = null)
            => Create().Handle(
                new CreateCampaignCommand(
                    code, "Campaign", CampaignTypes.ProductCampaign, start ?? Utc(2026, 3, 10),
                    BusinessUnitId: businessUnitId,
                    EndDate: end ?? Utc(2026, 4, 10),
                    CyclePeriodId: cyclePeriodId,
                    ScopeType: scopeType,
                    CountryScope: country,
                    LegalEntityId: legalEntityId),
                default);

        public Task<Response<bool>> UpdateAsync(
            Guid campaignId,
            string? scopeType = null,
            string? country = null,
            Guid? legalEntityId = null,
            string? businessUnitId = null,
            Guid? cyclePeriodId = null,
            DateTimeOffset? start = null,
            DateTimeOffset? end = null,
            string name = "Campaign")
            => Update().Handle(
                new UpdateCampaignCommand(
                    campaignId, name, CampaignTypes.ProductCampaign, start ?? Utc(2026, 3, 10),
                    BusinessUnitId: businessUnitId,
                    EndDate: end ?? Utc(2026, 4, 10),
                    CyclePeriodId: cyclePeriodId,
                    ScopeType: scopeType,
                    CountryScope: country,
                    LegalEntityId: legalEntityId),
                default);

        public CampaignEntity Stored(string code = "CMP-1") => Campaigns.Items.Single(c => c.CampaignCode == code);
    }

    // ============ 1–8 · The discriminated invariant ============

    [Fact]
    public async Task T01_Tenant_Scope_Takes_No_Reference()
    {
        var f = new Fixture();
        Assert.Equal(201, (await f.CreateAsync(scopeType: CampaignScopeTypes.Tenant)).StatusCode);
        Assert.Equal(CampaignScopeTypes.Tenant, f.Stored().ScopeType);
        Assert.Null(f.Stored().ScopeRef());
    }

    [Fact]
    public async Task T02_Tenant_Scope_With_A_Reference_Is_Refused_Not_Cleared()
    {
        var f = new Fixture();
        var created = await f.CreateAsync(scopeType: CampaignScopeTypes.Tenant, businessUnitId: "alpha");

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    [Fact]
    public async Task T03_Two_References_Are_Ambiguous()
    {
        var f = new Fixture();
        var created = await f.CreateAsync(
            scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha", country: "TR");

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    [Fact]
    public async Task T04_A_Level_Without_Its_Reference_Is_Refused()
    {
        var f = new Fixture();
        Assert.Equal(400, (await f.CreateAsync(scopeType: CampaignScopeTypes.Country)).StatusCode);
        Assert.Equal(400, (await f.CreateAsync(scopeType: CampaignScopeTypes.LegalEntity)).StatusCode);
        Assert.Equal(400, (await f.CreateAsync(scopeType: CampaignScopeTypes.BusinessUnit)).StatusCode);
    }

    [Fact]
    public async Task T05_Unknown_Scope_Type_Is_Refused()
        => Assert.Equal(400, (await new Fixture().CreateAsync(scopeType: "region")).StatusCode);

    [Fact]
    public async Task T06_Country_Is_Upper_Cased_So_One_Address_Is_One_Address()
    {
        var f = new Fixture();
        Assert.Equal(201, (await f.CreateAsync(scopeType: CampaignScopeTypes.Country, country: "tr")).StatusCode);

        Assert.Equal("TR", f.Stored().CountryScope);
        Assert.Equal("TR", f.Stored().ScopeRef());
    }

    [Fact]
    public async Task T07_Country_Must_Be_Iso_Alpha2()
        => Assert.Equal(400, (await new Fixture().CreateAsync(
            scopeType: CampaignScopeTypes.Country, country: "TUR")).StatusCode);

    [Fact]
    public async Task T08_Legal_Entity_Scope_Stores_Only_The_Id()
    {
        var f = new Fixture();
        Assert.Equal(201, (await f.CreateAsync(
            scopeType: CampaignScopeTypes.LegalEntity, legalEntityId: LegalEntityX)).StatusCode);

        Assert.Equal(LegalEntityX, f.Stored().LegalEntityId);
        Assert.Null(f.Stored().CountryScope);
        Assert.Null(f.Stored().BusinessUnitId);
    }

    // ============ 9–12 · Derivation (no migration) ============

    [Fact]
    public void T09_Derivation_Mirrors_The_Pre_FU09_Shapes()
    {
        Assert.Equal(CampaignScopeTypes.Tenant, CampaignScopeRules.DeriveScopeType(null, null, null));
        Assert.Equal(CampaignScopeTypes.BusinessUnit, CampaignScopeRules.DeriveScopeType(null, null, "alpha"));
        // A level that did not exist before FU09 cannot be guessed at.
        Assert.Null(CampaignScopeRules.DeriveScopeType("TR", null, null));
        Assert.Null(CampaignScopeRules.DeriveScopeType(null, LegalEntityX, null));
    }

    [Fact]
    public void T10_A_Pre_FU09_Row_Reads_As_The_Scope_It_Always_Had()
    {
        var legacyTenant = new CampaignEntity { CampaignCode = "OLD-1" };
        var legacyBusinessUnit = new CampaignEntity { CampaignCode = "OLD-2", BusinessUnitId = "legacy-x" };

        Assert.Equal(CampaignScopeTypes.Tenant, legacyTenant.EffectiveScopeType());
        Assert.Equal(CampaignScopeTypes.BusinessUnit, legacyBusinessUnit.EffectiveScopeType());
        Assert.Equal("legacy-x", legacyBusinessUnit.ScopeRef());
        Assert.True(legacyTenant.HasConsistentScope());
        Assert.True(legacyBusinessUnit.HasConsistentScope());
    }

    [Fact]
    public void T11_Derivation_Writes_Nothing()
    {
        var legacy = new CampaignEntity { CampaignCode = "OLD", BusinessUnitId = "legacy-x" };

        _ = legacy.EffectiveScopeType();
        _ = legacy.ScopeRef();

        // Read-time only: the stored field is still empty, so nothing needs backfilling.
        Assert.Equal(string.Empty, legacy.ScopeType);
    }

    [Fact]
    public async Task T12_A_Command_Without_A_ScopeType_Still_Works()
    {
        var f = new Fixture();
        Assert.Equal(201, (await f.CreateAsync(businessUnitId: "alpha")).StatusCode);
        Assert.Equal(CampaignScopeTypes.BusinessUnit, f.Stored().ScopeType);
    }

    // ============ 13–17 · Governed vocabulary and MDM ============

    [Fact]
    public async Task T13_Unknown_Country_Is_Refused()
        => Assert.Equal(400, (await new Fixture().CreateAsync(
            scopeType: CampaignScopeTypes.Country, country: "FR")).StatusCode);

    [Fact]
    public async Task T14_Unpublished_Set_And_Unknown_Value_Are_Different_Failures()
    {
        var unpublished = new Fixture();
        unpublished.References.Publish(CampaignScopeReferenceSets.BusinessUnitSet); // published but EMPTY
        var unknownValue = await unpublished.CreateAsync(
            scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha");

        var missingSet = new Fixture();
        // A set that was never published at all.
        var noSet = new CampaignScopeWriteValidator(
            new CampaignScopeTestDoubles.FakeReferenceValidator(),
            new CampaignScopeTestDoubles.FakeLegalEntityValidator());
        var setResult = await noSet.ValidateAsync(
            CampaignScopeTypes.BusinessUnit, null, null, "alpha", null, default);

        Assert.Equal(400, unknownValue.StatusCode);
        Assert.Equal(CampaignReasonCodes.CampaignReferenceSetUnpublished, setResult.Failure!.ReasonCode);
        Assert.NotEqual(setResult.Failure.ReasonCode, CampaignReasonCodes.CampaignBusinessUnitUnknown);
        Assert.Equal(0, missingSet.Campaigns.WriteCount);
    }

    [Fact]
    public async Task T15_Mdm_Says_No_Is_400()
    {
        var f = new Fixture();
        f.LegalEntities.Verdict = CyclePeriodLegalEntityValidation.NotReferenceable;

        var created = await f.CreateAsync(scopeType: CampaignScopeTypes.LegalEntity, legalEntityId: LegalEntityX);

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    /// <summary>Test 16 — "the dependency did not answer" is never told to the author as "your input is wrong".</summary>
    [Fact]
    public async Task T16_Mdm_Unreachable_Is_503_And_Nothing_Is_Written()
    {
        var f = new Fixture();
        f.LegalEntities.Verdict = CyclePeriodLegalEntityValidation.Unavailable;

        var created = await f.CreateAsync(scopeType: CampaignScopeTypes.LegalEntity, legalEntityId: LegalEntityX);

        Assert.Equal(503, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    /// <summary>Test 17 (D-SCOPE-LEGACY-REF) — a campaign carrying a pre-FU09 code stays editable, and the vocabulary
    /// check engages the moment the author touches the reference itself.</summary>
    [Fact]
    public async Task T17_Legacy_Business_Unit_Code_Stays_Editable_Until_It_Is_Changed()
    {
        var f = new Fixture();
        var legacy = new CampaignEntity
        {
            TenantId = TenantA,
            CampaignCode = "OLD",
            CampaignName = "Legacy",
            CampaignType = CampaignTypes.ProductCampaign,
            BusinessUnitId = "ungoverned-code",
            StartDate = Utc(2026, 3, 10),
            EndDate = Utc(2026, 4, 10)
        };
        f.Campaigns.Items.Add(legacy);

        // Editing something else keeps the untouched legacy reference.
        var renamed = await f.UpdateAsync(
            legacy.Id, businessUnitId: "ungoverned-code", name: "Legacy renamed");
        Assert.True(renamed.IsSuccessful);

        // Touching the reference engages the governed set.
        var moved = await f.UpdateAsync(legacy.Id, businessUnitId: "still-ungoverned");
        Assert.Equal(400, moved.StatusCode);

        // Moving it to a published value is accepted.
        var fixedUp = await f.UpdateAsync(legacy.Id, businessUnitId: "alpha");
        Assert.True(fixedUp.IsSuccessful);
    }

    // ============ 18–25 · Applicability (the FU09 headline) ============

    [Fact]
    public void T18_Applicable_Set_Is_Own_Address_Plus_Tenant_Fallback()
    {
        Assert.Equal(
            new[] { (CampaignScopeTypes.BusinessUnit, (string?)"alpha"), (CampaignScopeTypes.Tenant, (string?)null) },
            CampaignCycleApplicability.ApplicableScopes(CampaignScopeTypes.BusinessUnit, "alpha"));

        // A tenant campaign has one address, not two.
        Assert.Equal(
            new[] { (CampaignScopeTypes.Tenant, (string?)null) },
            CampaignCycleApplicability.ApplicableScopes(CampaignScopeTypes.Tenant, null));
    }

    /// <summary>Test 19 — the axis does NOT cross. This is the decision, pinned so it cannot drift silently.</summary>
    [Fact]
    public void T19_Business_Unit_Campaign_Does_Not_See_Country_Periods()
    {
        Assert.True(CampaignCycleApplicability.IsApplicable(
            CampaignScopeTypes.BusinessUnit, "alpha", CampaignScopeTypes.BusinessUnit, "alpha"));
        Assert.True(CampaignCycleApplicability.IsApplicable(
            CampaignScopeTypes.BusinessUnit, "alpha", CampaignScopeTypes.Tenant, null));

        Assert.False(CampaignCycleApplicability.IsApplicable(
            CampaignScopeTypes.BusinessUnit, "alpha", CampaignScopeTypes.BusinessUnit, "beta"));
        Assert.False(CampaignCycleApplicability.IsApplicable(
            CampaignScopeTypes.BusinessUnit, "alpha", CampaignScopeTypes.Country, "TR"));
        Assert.False(CampaignCycleApplicability.IsApplicable(
            CampaignScopeTypes.BusinessUnit, "alpha", CampaignScopeTypes.LegalEntity, LegalEntityX.ToString("D")));
    }

    [Fact]
    public void T20_Tenant_Campaign_Sees_Only_Tenant_Periods()
    {
        Assert.True(CampaignCycleApplicability.IsApplicable(
            CampaignScopeTypes.Tenant, null, CampaignScopeTypes.Tenant, null));
        Assert.False(CampaignCycleApplicability.IsApplicable(
            CampaignScopeTypes.Tenant, null, CampaignScopeTypes.BusinessUnit, "alpha"));
        Assert.False(CampaignCycleApplicability.IsApplicable(
            CampaignScopeTypes.Tenant, null, CampaignScopeTypes.Country, "TR"));
    }

    [Fact]
    public void T21_Address_Comparison_Is_Normalised()
        => Assert.True(CampaignCycleApplicability.IsApplicable(
            CampaignScopeTypes.Country, "TR", CampaignScopeTypes.Country, " tr "));

    [Fact]
    public async Task T22_Binding_A_Period_At_A_Different_Address_Is_Refused()
    {
        var f = new Fixture();
        var beta = f.Reader.Add("C-BETA", CyclePeriodScopeTypes.BusinessUnit, "beta", PeriodStart, PeriodEnd);

        var created = await f.CreateAsync(
            scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha", cyclePeriodId: beta);

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    [Fact]
    public async Task T23_Binding_The_Tenant_Fallback_Is_Accepted_At_Every_Level()
    {
        var f = new Fixture();
        var tenantPeriod = f.Reader.Add("C-TEN", CyclePeriodScopeTypes.Tenant, null, PeriodStart, PeriodEnd);

        Assert.Equal(201, (await f.CreateAsync(
            code: "C1", scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha",
            cyclePeriodId: tenantPeriod)).StatusCode);
        Assert.Equal(201, (await f.CreateAsync(
            code: "C2", scopeType: CampaignScopeTypes.Country, country: "TR",
            cyclePeriodId: tenantPeriod)).StatusCode);
        Assert.Equal(201, (await f.CreateAsync(
            code: "C3", scopeType: CampaignScopeTypes.Tenant, cyclePeriodId: tenantPeriod)).StatusCode);
    }

    [Fact]
    public async Task T24_A_Tenant_Campaign_Cannot_Bind_A_Business_Unit_Period()
    {
        var f = new Fixture();
        var alpha = f.Reader.Add("C-ALPHA", CyclePeriodScopeTypes.BusinessUnit, "alpha", PeriodStart, PeriodEnd);

        var created = await f.CreateAsync(scopeType: CampaignScopeTypes.Tenant, cyclePeriodId: alpha);

        Assert.Equal(400, created.StatusCode);
    }

    /// <summary>Test 25 — the picker and the guard answer from ONE rule: what the picker offers, the save accepts.</summary>
    [Fact]
    public async Task T25_Picker_Offers_Exactly_The_Applicable_Active_Periods()
    {
        var f = new Fixture();
        var alpha = f.Reader.Add("C-ALPHA", CyclePeriodScopeTypes.BusinessUnit, "alpha", PeriodStart, PeriodEnd);
        var tenant = f.Reader.Add("C-TEN", CyclePeriodScopeTypes.Tenant, null, PeriodStart, PeriodEnd);
        f.Reader.Add("C-BETA", CyclePeriodScopeTypes.BusinessUnit, "beta", PeriodStart, PeriodEnd);
        f.Reader.Add("C-TR", CyclePeriodScopeTypes.Country, "TR", PeriodStart, PeriodEnd);
        f.Reader.Add("C-DRAFT", CyclePeriodScopeTypes.BusinessUnit, "alpha", PeriodStart, PeriodEnd,
            CyclePeriodStatuses.Draft);
        f.Reader.Add("C-CLOSED", CyclePeriodScopeTypes.Tenant, null, PeriodStart, PeriodEnd,
            CyclePeriodStatuses.Closed);

        var result = await f.Applicable().Handle(
            new GetApplicableCyclePeriodsQuery(CampaignScopeTypes.BusinessUnit, null, null, "alpha"), default);

        var ids = result.Data!.Items.Select(i => i.CyclePeriodId).ToList();
        Assert.Equal(new[] { alpha, tenant }.OrderBy(x => x), ids.OrderBy(x => x));
    }

    // ============ 26–31 · Editable scope + bound-period re-validation ============

    [Fact]
    public async Task T26_Changing_Scope_Away_From_The_Bound_Period_Is_Refused()
    {
        var f = new Fixture();
        var alpha = f.Reader.Add("C-ALPHA", CyclePeriodScopeTypes.BusinessUnit, "alpha", PeriodStart, PeriodEnd);
        var id = (await f.CreateAsync(
            scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha", cyclePeriodId: alpha)).Data;

        var moved = await f.UpdateAsync(
            id, scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "beta", cyclePeriodId: alpha);

        Assert.Equal(400, moved.StatusCode);
    }

    [Fact]
    public async Task T27_Changing_Scope_While_Unbinding_Is_Allowed()
    {
        var f = new Fixture();
        var alpha = f.Reader.Add("C-ALPHA", CyclePeriodScopeTypes.BusinessUnit, "alpha", PeriodStart, PeriodEnd);
        var id = (await f.CreateAsync(
            scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha", cyclePeriodId: alpha)).Data;

        var moved = await f.UpdateAsync(
            id, scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "beta", cyclePeriodId: null);

        Assert.True(moved.IsSuccessful);
    }

    /// <summary>Test 28 — a tenant-scoped binding survives any scope move, because tenant is applicable everywhere.</summary>
    [Fact]
    public async Task T28_A_Tenant_Binding_Survives_A_Scope_Change()
    {
        var f = new Fixture();
        var tenantPeriod = f.Reader.Add("C-TEN", CyclePeriodScopeTypes.Tenant, null, PeriodStart, PeriodEnd);
        var id = (await f.CreateAsync(
            scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha",
            cyclePeriodId: tenantPeriod)).Data;

        var moved = await f.UpdateAsync(
            id, scopeType: CampaignScopeTypes.Country, country: "TR", cyclePeriodId: tenantPeriod);

        Assert.True(moved.IsSuccessful);
    }

    /// <summary>
    /// Test 29 — close-resilience does not exempt the scope rule. FU08 keeps a binding when its period closes; FU09
    /// still refuses to move the campaign to an address that period does not serve.
    /// </summary>
    [Fact]
    public async Task T29_Closed_Period_Binding_Is_Kept_But_Not_Exempt_From_Scope()
    {
        var f = new Fixture();
        var alpha = f.Reader.Add("C-ALPHA", CyclePeriodScopeTypes.BusinessUnit, "alpha", PeriodStart, PeriodEnd);
        var id = (await f.CreateAsync(
            scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha", cyclePeriodId: alpha)).Data;

        var index = f.Reader.Periods.FindIndex(p => p.CyclePeriodId == alpha);
        f.Reader.Periods[index] = f.Reader.Periods[index] with { CycleStatus = CyclePeriodStatuses.Closed };

        // Same address: FU08's close-resilience still applies.
        Assert.True((await f.UpdateAsync(
            id, scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha",
            cyclePeriodId: alpha, name: "Renamed")).IsSuccessful);

        // Different address: the scope rule refuses regardless of the period being closed.
        Assert.Equal(400, (await f.UpdateAsync(
            id, scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "beta",
            cyclePeriodId: alpha)).StatusCode);
    }

    [Fact]
    public async Task T30_Scope_Is_Editable_When_Nothing_Is_Bound()
    {
        var f = new Fixture();
        var id = (await f.CreateAsync(scopeType: CampaignScopeTypes.Tenant)).Data;

        Assert.True((await f.UpdateAsync(
            id, scopeType: CampaignScopeTypes.Country, country: "TR")).IsSuccessful);
    }

    /// <summary>Test 31 — FU08 regression: containment still applies, and it is checked alongside the scope rule.</summary>
    [Fact]
    public async Task T31_Containment_Still_Applies_Within_An_Applicable_Scope()
    {
        var f = new Fixture();
        var alpha = f.Reader.Add("C-ALPHA", CyclePeriodScopeTypes.BusinessUnit, "alpha", PeriodStart, PeriodEnd);

        var outside = await f.CreateAsync(
            scopeType: CampaignScopeTypes.BusinessUnit, businessUnitId: "alpha", cyclePeriodId: alpha,
            start: Utc(2026, 3, 10), end: Utc(2026, 6, 30));

        Assert.Equal(400, outside.StatusCode);
    }

    // ============ 32–35 · The mirror stays honest ============

    /// <summary>
    /// Test 32 (pack §10.2, MANDATORY) — the campaign scope rules and the cycle-period scope rules must decide the
    /// SAME way for the same input. They are separate implementations on purpose; this test is what stops "separate"
    /// from quietly becoming "different". The day they must diverge, this test fails and the divergence gets written
    /// down instead of discovered.
    /// </summary>
    [Theory]
    // (scopeType, country, legalEntity, businessUnit)
    [InlineData(null, null, null, null)]                       // derive -> tenant
    [InlineData(null, null, null, "alpha")]                    // derive -> business-unit
    [InlineData(null, "TR", null, null)]                       // underivable -> refused
    [InlineData("tenant", null, null, null)]                   // tenant, clean
    [InlineData("tenant", null, null, "alpha")]                // tenant + reference -> refused
    [InlineData("country", "TR", null, null)]                  // country, clean
    [InlineData("country", "tr", null, null)]                  // country, normalised
    [InlineData("country", "TUR", null, null)]                 // not alpha-2 -> refused
    [InlineData("country", null, null, null)]                  // missing reference -> refused
    [InlineData("business-unit", null, null, "alpha")]         // business-unit, clean
    [InlineData("business-unit", "TR", null, "alpha")]         // two references -> refused
    [InlineData("region", null, null, null)]                   // unknown level -> refused
    public void T32_Mirror_Decides_Identically_To_The_Cycle_Period_Rules(
        string? scopeType, string? country, string? legalEntity, string? businessUnit)
    {
        var legalEntityId = legalEntity is null ? (Guid?)null : Guid.Parse(legalEntity);

        var (campaignScope, campaignFailure) =
            CampaignScopeRules.Normalize(scopeType, country, legalEntityId, businessUnit);
        var (periodScope, periodFailure) =
            CyclePeriodScopeRules.Normalize(scopeType, country, legalEntityId, businessUnit);

        Assert.Equal(periodFailure is null, campaignFailure is null);

        if (periodFailure is null)
        {
            Assert.Equal(periodScope!.ScopeType, campaignScope!.ScopeType);
            Assert.Equal(periodScope.ScopeRef, campaignScope.ScopeRef);
            Assert.Equal(periodScope.CountryScope, campaignScope.CountryScope);
            Assert.Equal(periodScope.LegalEntityId, campaignScope.LegalEntityId);
            Assert.Equal(periodScope.BusinessUnitId, campaignScope.BusinessUnitId);
        }
    }

    [Fact]
    public void T33_Both_Sides_Walk_The_Same_Precedence()
        => Assert.Equal(CyclePeriodScopeTypes.ByPrecedence, CampaignScopeTypes.ByPrecedence);

    /// <summary>Test 34 — mirrored, not shared: the campaign side owns its own rule type.</summary>
    [Fact]
    public void T34_Campaign_Rules_Are_Their_Own_Type()
    {
        Assert.NotEqual(typeof(CyclePeriodScopeRules), typeof(CampaignScopeRules));
        Assert.NotEqual(typeof(CyclePeriodScopeTypes), typeof(CampaignScopeTypes));

        // The campaign write path holds no cycle-period REPOSITORY: it reads through seams only.
        foreach (var handler in new[] { typeof(CreateCampaignHandler), typeof(UpdateCampaignHandler) })
        {
            var parameters = handler.GetConstructors().Single().GetParameters().Select(p => p.ParameterType);
            Assert.DoesNotContain(parameters, x => x == typeof(ICyclePeriodRepository) || x == typeof(HttpClient));
        }
    }

    /// <summary>Test 35 — the direction is still one-way: CyclePeriod knows nothing about campaigns.</summary>
    [Fact]
    public void T35_CyclePeriod_Still_Holds_No_Campaign_Reference()
        => Assert.DoesNotContain(
            typeof(Domain.Entities.CyclePeriod).GetProperties().Select(p => p.Name),
            name => name.Contains("Campaign", StringComparison.OrdinalIgnoreCase));

    // ============ 36 · Contract ============

    [Fact]
    public async Task T36_Contract_Declares_Scope_Awareness_And_Its_Limits()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantA);

        var contract = (await new GetCampaignContractHandler(tenant)
            .Handle(new GetCampaignContractQuery(), default)).Data!;

        Assert.True(contract.Features.SupportsScopeAwareCycleBinding);
        Assert.True(contract.Features.SupportsCyclePeriodBinding);

        foreach (var code in new[]
                 {
                     CampaignReasonCodes.CampaignScopeTypeUnknown,
                     CampaignReasonCodes.CampaignScopeReferenceRequired,
                     CampaignReasonCodes.CampaignScopeAmbiguous,
                     CampaignReasonCodes.CampaignCountryUnknown,
                     CampaignReasonCodes.CampaignBusinessUnitUnknown,
                     CampaignReasonCodes.CampaignReferenceSetUnpublished,
                     CampaignReasonCodes.CampaignLegalEntityNotReferenceable,
                     CampaignReasonCodes.CampaignLegalEntityValidationUnavailable,
                     CampaignReasonCodes.CampaignCyclePeriodScopeMismatch
                 })
        {
            Assert.Contains(code, contract.ReasonCodes);
        }

        var limitations = string.Join(" | ", contract.Limitations);
        Assert.Contains("binding IS scope-aware", limitations);
        Assert.Contains("DATA, not authorization", limitations);
        Assert.Contains("business-unit-scoped campaign sees business-unit and tenant periods only", limitations);
        Assert.Contains("MIRRORS MOD-0165 FU07", limitations);
        // The superseded FU08 claim must be gone, not merely contradicted elsewhere.
        Assert.DoesNotContain("the binding does NOT validate scope", limitations);
    }
}
