using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CyclePeriod;
using Diten.CrmService.Application.Features.CyclePeriod.Commands;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.CyclePeriod.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Tests.CyclePeriod;

/// <summary>
/// MOD-0165 FU07 — the scope enrichment. Pins down the four decisions the pack calls load-bearing:
/// <list type="bullet">
/// <item><description>scope is DISCRIMINATED — one level, one reference, and a second reference is refused rather than
/// ignored;</description></item>
/// <item><description>scope is IDENTITY — the level is immutable at every status, and uniqueness is per scope;</description></item>
/// <item><description>the overlap ban is PER SCOPE — periods at different levels may share days, and must be able to,
/// or precedence could never fire;</description></item>
/// <item><description>resolution walks business-unit → legal-entity → country → tenant, SKIPS levels the caller did
/// not name, and STOPS at the first level that answers — including when it answers ambiguous.</description></item>
/// </list>
/// It also pins the write gate: governed vocabulary for the country and the business unit, fail-closed MDM for the
/// legal entity, everything BEFORE the insert, and the territory list as a stamp rather than a gate.
/// </summary>
public sealed class CyclePeriodScopeTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LegalEntityX = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid LegalEntityY = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Mar1 = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Apr30 = new(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset May1 = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun30 = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

    // ── doubles ────────────────────────────────────────────────────────────────────────────────────────────────────

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class FakeRepo : ICyclePeriodRepository
    {
        public List<PeriodEntity> Items { get; } = new();

        private IReadOnlyList<PeriodEntity> Scope(Guid tenantId)
            => Items.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList();

        public Task<PeriodEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Scope(tenantId).FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<PeriodEntity>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(Scope(tenantId));

        public Task<IReadOnlyList<PeriodEntity>> ListByCodeAsync(Guid tenantId, string cycleCode, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(
                Scope(tenantId).Where(x => x.CycleCode == cycleCode).ToList());

        public Task<IReadOnlyList<PeriodEntity>> ListByYearAsync(Guid tenantId, int year, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(Scope(tenantId).Where(x => x.Year == year).ToList());

        public Task<IReadOnlyList<PeriodEntity>> ListActiveAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(Scope(tenantId).Where(x => x.IsActive()).ToList());

        public Task InsertAsync(PeriodEntity entity, CancellationToken ct)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceAsync(PeriodEntity entity, int expectedVersion, CancellationToken ct)
        {
            var existing = Items.FirstOrDefault(x => x.Id == entity.Id && x.TenantId == entity.TenantId);
            if (existing is null || existing.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            entity.Version = expectedVersion + 1;
            Items[Items.IndexOf(existing)] = entity;
            return Task.FromResult(true);
        }
    }

    /// <summary>A governed set with an explicit publication state, so "unpublished set" and "unknown value" can be
    /// told apart the way the runtime tells them apart.</summary>
    private sealed class FakeReferences : IReferenceDataValidator
    {
        private readonly Dictionary<string, HashSet<string>?> _sets = new(StringComparer.OrdinalIgnoreCase);
        public int Calls { get; private set; }

        public FakeReferences Published(string setCode, params string[] values)
        {
            _sets[setCode] = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
            return this;
        }

        /// <summary>null marks a set the operator has not published yet.</summary>
        public FakeReferences Unpublished(string setCode)
        {
            _sets[setCode] = null;
            return this;
        }

        public Task<ReferenceValidationResult> ValidateAsync(
            string setCode, string value, CancellationToken cancellationToken)
        {
            Calls++;
            if (!_sets.TryGetValue(setCode, out var values) || values is null)
            {
                return Task.FromResult(
                    new ReferenceValidationResult(ReferenceValidationStatus.SetMissing, setCode, value));
            }

            return Task.FromResult(new ReferenceValidationResult(
                values.Contains(value) ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue,
                setCode, value));
        }
    }

    private sealed class FakeLegalEntities : ICyclePeriodLegalEntityValidator
    {
        private readonly CyclePeriodLegalEntityValidation _verdict;
        public int Calls { get; private set; }

        public FakeLegalEntities(CyclePeriodLegalEntityValidation verdict) => _verdict = verdict;

        public Task<CyclePeriodLegalEntityValidation> ValidateAsync(Guid legalEntityId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_verdict);
        }
    }

    private sealed class FakeTerritory : ITerritoryBusinessUnitCatalog
    {
        private readonly IReadOnlyList<TerritoryBusinessUnitCandidate> _candidates;
        public int Calls { get; private set; }
        public string? LastCountry { get; private set; }

        public FakeTerritory(params string[] codes)
            => _candidates = codes
                .Select(c => new TerritoryBusinessUnitCandidate(c, new[] { "TM-" + c }))
                .ToList();

        public Task<IReadOnlyList<TerritoryBusinessUnitCandidate>> GetCandidatesAsync(
            string? country, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct)
        {
            Calls++;
            LastCountry = country;
            return Task.FromResult(_candidates);
        }
    }

    private static CyclePeriodScopeWriteValidator Gate(
        FakeReferences? references = null,
        CyclePeriodLegalEntityValidation? legalEntity = null,
        ITerritoryBusinessUnitCatalog? territory = null)
        => new(
            references ?? new FakeReferences()
                .Published(CyclePeriodReferenceSets.CountrySet, "TR", "DE")
                .Published(CyclePeriodReferenceSets.BusinessUnitSet, "rx", "otc"),
            new FakeLegalEntities(legalEntity ?? CyclePeriodLegalEntityValidation.Valid),
            territory ?? new FakeTerritory());

    private static CreateCyclePeriodHandler Create(
        FakeRepo repo, CyclePeriodScopeWriteValidator? gate = null, Guid? tenant = null)
        => new(Tenant(tenant ?? TenantA), new NullActorContext(), repo, gate ?? Gate());

    private static UpdateCyclePeriodHandler Update(FakeRepo repo, CyclePeriodScopeWriteValidator? gate = null)
        => new(Tenant(TenantA), new NullActorContext(), repo, gate ?? Gate());

    private static ActivateCyclePeriodHandler Activate(FakeRepo repo)
        => new(Tenant(TenantA), new NullActorContext(), repo);

    private static CreateCyclePeriodCommand Cmd(
        string code, string scopeType, string? country = null, Guid? legalEntityId = null,
        string? businessUnitId = null, int sequence = 1,
        DateTimeOffset? start = null, DateTimeOffset? end = null)
        => new(code, "period " + code, 2026, sequence, start ?? Mar1, end ?? Apr30,
            scopeType, country, legalEntityId, businessUnitId, null);

    private static async Task<Guid> SeedAsync(
        FakeRepo repo, string code, string scopeType, string? country = null, Guid? legalEntityId = null,
        string? businessUnitId = null, int sequence = 1,
        DateTimeOffset? start = null, DateTimeOffset? end = null, bool activate = false)
    {
        var created = await Create(repo).Handle(
            Cmd(code, scopeType, country, legalEntityId, businessUnitId, sequence, start, end), default);
        Assert.Equal(201, created.StatusCode);

        if (activate)
        {
            var activated = await Activate(repo).Handle(
                new ActivateCyclePeriodCommand(created.Data, null), default);
            Assert.Equal(200, activated.StatusCode);
        }

        return created.Data;
    }

    // ── the single-reference invariant ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CyclePeriodScopeTypes.Tenant)]
    [InlineData(CyclePeriodScopeTypes.Country)]
    [InlineData(CyclePeriodScopeTypes.LegalEntity)]
    [InlineData(CyclePeriodScopeTypes.BusinessUnit)]
    public async Task Create_Accepts_Each_Scope_Level_With_Its_Own_Reference(string scopeType)
    {
        var repo = new FakeRepo();
        var r = await Create(repo).Handle(
            Cmd("c-1", scopeType,
                country: scopeType == CyclePeriodScopeTypes.Country ? "TR" : null,
                legalEntityId: scopeType == CyclePeriodScopeTypes.LegalEntity ? LegalEntityX : null,
                businessUnitId: scopeType == CyclePeriodScopeTypes.BusinessUnit ? "rx" : null),
            default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(repo.Items);
        Assert.Equal(scopeType, row.ScopeType);
        Assert.True(row.HasConsistentScope());
    }

    [Fact]
    public async Task Create_With_A_Second_Reference_Is_400_And_Nothing_Is_Written()
    {
        // Refused, not silently cleared: an author who typed a business unit meant something by it.
        var repo = new FakeRepo();
        var r = await Create(repo).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.Country, country: "TR", businessUnitId: "rx"), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeAmbiguous, r.Errors!);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Create_Tenant_Scope_With_Any_Reference_Is_400()
    {
        var repo = new FakeRepo();
        var r = await Create(repo).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.Tenant, businessUnitId: "rx"), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeAmbiguous, r.Errors!);
    }

    [Theory]
    [InlineData(CyclePeriodScopeTypes.Country)]
    [InlineData(CyclePeriodScopeTypes.LegalEntity)]
    [InlineData(CyclePeriodScopeTypes.BusinessUnit)]
    public async Task Create_Without_The_Required_Reference_Is_400(string scopeType)
    {
        var repo = new FakeRepo();
        var r = await Create(repo).Handle(Cmd("c-1", scopeType), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeReferenceRequired, r.Errors!);
    }

    [Theory]
    [InlineData("organization-unit")]
    [InlineData("region")]
    public async Task Create_With_An_Unknown_Scope_Type_Is_400_And_Never_Falls_Back_To_Tenant(string scopeType)
    {
        var repo = new FakeRepo();
        var r = await Create(repo).Handle(Cmd("c-1", scopeType), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeTypeUnknown, r.Errors!);
        Assert.Empty(repo.Items);
    }

    /// <summary>
    /// An ABSENT scope type is a different fact from a WRONG one, and FU06 backward compatibility turns on the
    /// difference. A caller that never heard of scopes sends nothing, and the two shapes FU06 could express are
    /// derived (see <c>CyclePeriodRuntimeTests</c>); a caller that sends a level the runtime does not know is still
    /// refused above. These rows moved out of the refusal theory when the derivation landed — they are not a
    /// weakening of it.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_With_A_Blank_Scope_Type_And_No_References_Derives_Tenant(string scopeType)
    {
        var repo = new FakeRepo();
        var r = await Create(repo).Handle(Cmd("c-1", scopeType), default);

        Assert.Equal(201, r.StatusCode);
        Assert.Equal(CyclePeriodScopeTypes.Tenant, Assert.Single(repo.Items).ScopeType);
    }

    [Fact]
    public async Task Create_With_An_Empty_Guid_Legal_Entity_Is_400()
    {
        var repo = new FakeRepo();
        var r = await Create(repo).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.LegalEntity, legalEntityId: Guid.Empty), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeReferenceRequired, r.Errors!);
    }

    // ── normalisation ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Country_Is_Upper_Cased_So_One_Country_Cannot_Become_Two_Calendars()
    {
        var repo = new FakeRepo();
        await Create(repo).Handle(Cmd("c-1", CyclePeriodScopeTypes.Country, country: "tr"), default);

        var row = Assert.Single(repo.Items);
        Assert.Equal("TR", row.CountryScope);
        Assert.Equal("TR", row.ScopeRef());
    }

    [Theory]
    [InlineData("T")]
    [InlineData("TUR")]
    [InlineData("T1")]
    public async Task Country_Must_Be_Iso_Alpha_2(string country)
    {
        var repo = new FakeRepo();
        var references = new FakeReferences()
            .Published(CyclePeriodReferenceSets.CountrySet, country)
            .Published(CyclePeriodReferenceSets.BusinessUnitSet, "rx");

        var r = await Create(repo, Gate(references)).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.Country, country: country), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.CountryInvalid, r.Errors!);
    }

    [Fact]
    public async Task Business_Unit_Code_Is_Trimmed()
    {
        var repo = new FakeRepo();
        await Create(repo).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "  rx  "), default);

        var row = Assert.Single(repo.Items);
        Assert.Equal("rx", row.BusinessUnitId);
        Assert.Equal("rx", row.ScopeRef());
    }

    [Fact]
    public void ScopeRef_Is_Derived_Per_Level()
    {
        Assert.Null(new PeriodEntity { ScopeType = CyclePeriodScopeTypes.Tenant }.ScopeRef());
        Assert.Equal("TR", new PeriodEntity
        {
            ScopeType = CyclePeriodScopeTypes.Country, CountryScope = "tr"
        }.ScopeRef());
        Assert.Equal(LegalEntityX.ToString("D"), new PeriodEntity
        {
            ScopeType = CyclePeriodScopeTypes.LegalEntity, LegalEntityId = LegalEntityX
        }.ScopeRef());
        Assert.Equal("rx", new PeriodEntity
        {
            ScopeType = CyclePeriodScopeTypes.BusinessUnit, BusinessUnitId = " rx "
        }.ScopeRef());
    }

    // ── governed vocabulary ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unknown_Country_Value_Is_400()
    {
        var repo = new FakeRepo();
        var r = await Create(repo).Handle(Cmd("c-1", CyclePeriodScopeTypes.Country, country: "ZZ"), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.CountryUnknown, r.Errors!);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Unpublished_Country_Set_Is_Reported_As_Its_Own_Failure()
    {
        // An operator must publish the set; retyping the value would not help, so the two cases cannot share a code.
        var repo = new FakeRepo();
        var references = new FakeReferences()
            .Unpublished(CyclePeriodReferenceSets.CountrySet)
            .Published(CyclePeriodReferenceSets.BusinessUnitSet, "rx");

        var r = await Create(repo, Gate(references)).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.Country, country: "TR"), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ReferenceSetUnpublished, r.Errors!);
        Assert.DoesNotContain(CyclePeriodErrorCodes.CountryUnknown, r.Errors!);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Business_Unit_Is_No_Longer_Opaque_An_Unpublished_Code_Is_400()
    {
        var repo = new FakeRepo();
        var r = await Create(repo).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "made-up"), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.BusinessUnitUnknown, r.Errors!);
    }

    [Fact]
    public async Task Business_Unit_Is_Validated_Against_The_Same_Set_Territory_Uses()
    {
        // One vocabulary across CRM: if the codes were validated against two different sets, a business unit could
        // exist for Territory and not for a period.
        Assert.Equal("business-unit", CyclePeriodReferenceSets.BusinessUnitSet);

        var repo = new FakeRepo();
        var r = await Create(repo).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx"), default);

        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Tenant_Scope_Consults_No_Reference_Set_At_All()
    {
        var repo = new FakeRepo();
        var references = new FakeReferences()
            .Published(CyclePeriodReferenceSets.CountrySet, "TR")
            .Published(CyclePeriodReferenceSets.BusinessUnitSet, "rx");

        await Create(repo, Gate(references)).Handle(Cmd("c-1", CyclePeriodScopeTypes.Tenant), default);

        Assert.Equal(0, references.Calls);
    }

    // ── MDM, fail-closed ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Legal_Entity_That_Mdm_Refuses_Is_400()
    {
        var repo = new FakeRepo();
        var r = await Create(repo, Gate(legalEntity: CyclePeriodLegalEntityValidation.NotReferenceable)).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.LegalEntity, legalEntityId: LegalEntityX), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.LegalEntityNotReferenceable, r.Errors!);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Unreachable_Mdm_Is_503_With_Nothing_Persisted()
    {
        // "We do not know" must never be reported as "your input was wrong", and it must never leave a half-authored
        // period behind.
        var repo = new FakeRepo();
        var r = await Create(repo, Gate(legalEntity: CyclePeriodLegalEntityValidation.Unavailable)).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.LegalEntity, legalEntityId: LegalEntityX), default);

        Assert.Equal(503, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.LegalEntityDependencyUnavailable, r.Errors!);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Mdm_Is_Not_Consulted_For_A_Scope_That_Has_No_Legal_Entity()
    {
        var repo = new FakeRepo();
        var legalEntities = new FakeLegalEntities(CyclePeriodLegalEntityValidation.Valid);
        var gate = new CyclePeriodScopeWriteValidator(
            new FakeReferences()
                .Published(CyclePeriodReferenceSets.CountrySet, "TR")
                .Published(CyclePeriodReferenceSets.BusinessUnitSet, "rx"),
            legalEntities,
            new FakeTerritory());

        await Create(repo, gate).Handle(Cmd("c-1", CyclePeriodScopeTypes.Country, country: "TR"), default);

        Assert.Equal(0, legalEntities.Calls);
    }

    // ── the territory stamp is a stamp, not a gate ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Business_Unit_Covered_By_A_Territory_Plan_Is_Stamped_Territory()
    {
        var repo = new FakeRepo();
        await Create(repo, Gate(territory: new FakeTerritory("rx"))).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx"), default);

        var row = Assert.Single(repo.Items);
        Assert.Equal(CyclePeriodBusinessUnitSources.Territory, row.BusinessUnitSource);
    }

    [Fact]
    public async Task Business_Unit_Outside_Every_Plan_Is_Still_Accepted_And_Stamped_Manual()
    {
        // A hard territory gate would pin the period's identity to MOD-0151's lifecycle, and a period has to be
        // authorable before its field plan exists.
        var repo = new FakeRepo();
        var r = await Create(repo, Gate(territory: new FakeTerritory("otc"))).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx"), default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(repo.Items);
        Assert.Equal(CyclePeriodBusinessUnitSources.Manual, row.BusinessUnitSource);
    }

    [Fact]
    public async Task The_Provenance_Stamp_Is_Never_Part_Of_The_Scope()
    {
        // Two periods with the same code from different sources are the SAME scope, so they collide on sequence.
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-1", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx", sequence: 1);

        var second = await Create(repo, Gate(territory: new FakeTerritory("rx"))).Handle(
            Cmd("c-2", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx", sequence: 1), default);

        Assert.Equal(409, second.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.SequenceTaken, second.Errors!);
    }

    [Fact]
    public async Task A_Non_Business_Unit_Scope_Carries_No_Provenance_Stamp()
    {
        var repo = new FakeRepo();
        await Create(repo, Gate(territory: new FakeTerritory("rx"))).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.Country, country: "TR"), default);

        Assert.Null(Assert.Single(repo.Items).BusinessUnitSource);
    }

    // ── scope is identity: immutability + uniqueness ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Changing_The_Scope_Type_Of_A_Draft_Is_409()
    {
        var repo = new FakeRepo();
        var id = await SeedAsync(repo, "c-1", CyclePeriodScopeTypes.Tenant);

        var r = await Update(repo).Handle(
            new UpdateCyclePeriodCommand(
                id, "c-1", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.Country, "TR", null, null, null, null),
            default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeImmutable, r.Errors!);
    }

    [Fact]
    public async Task Changing_The_Scope_Type_Of_An_Active_Period_Is_409()
    {
        var repo = new FakeRepo();
        var id = await SeedAsync(repo, "c-1", CyclePeriodScopeTypes.Tenant, activate: true);

        var r = await Update(repo).Handle(
            new UpdateCyclePeriodCommand(
                id, "c-1", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.BusinessUnit, null, null, "rx", null, null),
            default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeImmutable, r.Errors!);
    }

    [Fact]
    public async Task A_Draft_May_Correct_Its_Scope_Reference_Within_The_Same_Level()
    {
        // Fixing a mistyped country is not the same act as moving the period to another level.
        var repo = new FakeRepo();
        var id = await SeedAsync(repo, "c-1", CyclePeriodScopeTypes.Country, country: "TR");

        var r = await Update(repo).Handle(
            new UpdateCyclePeriodCommand(
                id, "c-1", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.Country, "DE", null, null, null, null),
            default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal("DE", Assert.Single(repo.Items).CountryScope);
    }

    [Fact]
    public async Task An_Omitted_Scope_Type_Leaves_The_Period_Where_It_Is()
    {
        // An FU06-shaped caller never heard of scopes and must not be able to move a period by accident.
        var repo = new FakeRepo();
        var id = await SeedAsync(repo, "c-1", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx");

        var r = await Update(repo).Handle(
            new UpdateCyclePeriodCommand(
                id, "renamed", 2026, 1, Mar1, Apr30, null, null, null, "rx", null, null),
            default);

        Assert.Equal(200, r.StatusCode);
        var row = Assert.Single(repo.Items);
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, row.ScopeType);
        Assert.Equal("renamed", row.CycleName);
    }

    [Fact]
    public async Task Sequence_Uniqueness_Is_Per_Scope()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-1", CyclePeriodScopeTypes.Country, country: "TR", sequence: 1);

        var same = await Create(repo).Handle(
            Cmd("c-2", CyclePeriodScopeTypes.Country, country: "TR", sequence: 1), default);
        Assert.Equal(409, same.StatusCode);

        var otherCountry = await Create(repo).Handle(
            Cmd("c-3", CyclePeriodScopeTypes.Country, country: "DE", sequence: 1), default);
        Assert.Equal(201, otherCountry.StatusCode);

        var otherLevel = await Create(repo).Handle(
            Cmd("c-4", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx", sequence: 1), default);
        Assert.Equal(201, otherLevel.StatusCode);
    }

    [Fact]
    public async Task Cycle_Code_Uniqueness_Is_Tenant_Wide_Not_Per_Scope()
    {
        // One code names one period, wherever it lives — a code is a permanent historical identifier.
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-1", CyclePeriodScopeTypes.Country, country: "TR");

        var r = await Create(repo).Handle(
            Cmd("c-1", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx", sequence: 2), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.CodeTaken, r.Errors!);
    }

    // ── the overlap ban is per scope ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_Active_Periods_At_The_Same_Scope_May_Not_Share_A_Day()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-1", CyclePeriodScopeTypes.Country, country: "TR", sequence: 1, activate: true);
        var second = await SeedAsync(
            repo, "c-2", CyclePeriodScopeTypes.Country, country: "TR", sequence: 2,
            start: Apr30, end: Jun30);

        var r = await Activate(repo).Handle(new ActivateCyclePeriodCommand(second, null), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.Overlap, r.Errors!);
        Assert.Equal(CyclePeriodStatuses.Draft, repo.Items.Single(p => p.Id == second).CycleStatus);
    }

    [Fact]
    public async Task Two_Active_Periods_At_DIFFERENT_Levels_May_Share_A_Day()
    {
        // Load-bearing: banning this would make the resolver's fallback unreachable, because precedence only ever
        // fires when more than one level covers the same instant.
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);
        var bu = await SeedAsync(repo, "c-rx", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx", sequence: 2);

        var r = await Activate(repo).Handle(new ActivateCyclePeriodCommand(bu, null), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(CyclePeriodStatuses.Active, repo.Items.Single(p => p.Id == bu).CycleStatus);
    }

    [Fact]
    public async Task Two_Active_Periods_At_The_Same_Level_But_Different_References_May_Share_A_Day()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tr", CyclePeriodScopeTypes.Country, country: "TR", sequence: 1, activate: true);
        var de = await SeedAsync(repo, "c-de", CyclePeriodScopeTypes.Country, country: "DE", sequence: 2);

        var r = await Activate(repo).Handle(new ActivateCyclePeriodCommand(de, null), default);

        Assert.Equal(200, r.StatusCode);
    }

    [Fact]
    public async Task The_Overlap_Refusal_Names_The_Blocking_Period_And_Its_Scope()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-1", CyclePeriodScopeTypes.Country, country: "TR", sequence: 1, activate: true);
        var second = await SeedAsync(
            repo, "c-2", CyclePeriodScopeTypes.Country, country: "TR", sequence: 2, start: Apr30, end: Jun30);

        var r = await Activate(repo).Handle(new ActivateCyclePeriodCommand(second, null), default);

        var message = string.Join(" ", r.Errors!);
        Assert.Contains("c-1", message);
        Assert.Contains("country:TR", message);
    }

    // ── resolution: four levels, skip, stop, never merge ────────────────────────────────────────────────────────────

    private static CyclePeriodReader Reader(FakeRepo repo) => new(Tenant(TenantA), repo);

    private static ResolveActiveCyclePeriodHandler Resolve(FakeRepo repo) => new(Tenant(TenantA), Reader(repo));

    [Fact]
    public async Task Business_Unit_Wins_Over_Every_Broader_Level()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);
        await SeedAsync(repo, "c-tr", CyclePeriodScopeTypes.Country, country: "TR", sequence: 2, activate: true);
        await SeedAsync(repo, "c-le", CyclePeriodScopeTypes.LegalEntity, legalEntityId: LegalEntityX, sequence: 3, activate: true);
        await SeedAsync(repo, "c-rx", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx", sequence: 4, activate: true);

        var r = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), "TR", LegalEntityX, "rx"), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.Resolved, r.Data!.Outcome);
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, r.Data.ResolvedScopeType);
        Assert.Equal("c-rx", r.Data.Period!.CycleCode);
    }

    [Fact]
    public async Task Legal_Entity_Wins_When_The_Business_Unit_Has_No_Period()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);
        await SeedAsync(repo, "c-le", CyclePeriodScopeTypes.LegalEntity, legalEntityId: LegalEntityX, sequence: 2, activate: true);

        var r = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), "TR", LegalEntityX, "rx"), default);

        Assert.Equal(CyclePeriodScopeTypes.LegalEntity, r.Data!.ResolvedScopeType);
        Assert.Equal("c-le", r.Data.Period!.CycleCode);
    }

    [Fact]
    public async Task Country_Wins_When_Neither_Business_Unit_Nor_Legal_Entity_Has_One()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);
        await SeedAsync(repo, "c-tr", CyclePeriodScopeTypes.Country, country: "TR", sequence: 2, activate: true);

        var r = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), "TR", LegalEntityX, "rx"), default);

        Assert.Equal(CyclePeriodScopeTypes.Country, r.Data!.ResolvedScopeType);
        Assert.Equal("c-tr", r.Data.Period!.CycleCode);
    }

    [Fact]
    public async Task Tenant_Is_The_Last_Resort_And_Is_Always_Consulted()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);

        var r = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), "TR", LegalEntityX, "rx"), default);

        Assert.Equal(CyclePeriodScopeTypes.Tenant, r.Data!.ResolvedScopeType);
    }

    [Fact]
    public async Task A_Level_The_Caller_Did_Not_Name_Is_Skipped_Even_When_It_Has_A_Covering_Period()
    {
        // This is the whole backward-compatibility mechanism, stated as a test.
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);
        await SeedAsync(repo, "c-le", CyclePeriodScopeTypes.LegalEntity, legalEntityId: LegalEntityX, sequence: 2, activate: true);
        await SeedAsync(repo, "c-tr", CyclePeriodScopeTypes.Country, country: "TR", sequence: 3, activate: true);

        var r = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, "rx"), default);

        Assert.Equal(CyclePeriodScopeTypes.Tenant, r.Data!.ResolvedScopeType);
        Assert.Equal("c-tenant", r.Data.Period!.CycleCode);
    }

    [Fact]
    public async Task An_Fu06_Shaped_Call_Answers_Exactly_What_Fu06_Answered()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);
        await SeedAsync(repo, "c-rx", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx", sequence: 2, activate: true);
        // Country and legal-entity periods exist, and must be invisible to a caller that names neither.
        await SeedAsync(repo, "c-tr", CyclePeriodScopeTypes.Country, country: "TR", sequence: 3, activate: true);
        await SeedAsync(repo, "c-le", CyclePeriodScopeTypes.LegalEntity, legalEntityId: LegalEntityX, sequence: 4, activate: true);

        var scoped = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, "rx"), default);
        Assert.Equal("c-rx", scoped.Data!.Period!.CycleCode);

        var unscoped = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, null), default);
        Assert.Equal("c-tenant", unscoped.Data!.Period!.CycleCode);
    }

    [Fact]
    public async Task Ambiguity_At_One_Level_Stops_The_Walk_And_Never_Falls_Through()
    {
        // Broken data at the business-unit level must be reported, not stepped over into the tenant's calendar.
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);

        var a = new PeriodEntity
        {
            Id = Guid.NewGuid(), TenantId = TenantA, CycleCode = "x-1", CycleName = "x1",
            Year = 2026, SequenceInYear = 8, StartDate = Mar1, EndDate = Apr30,
            ScopeType = CyclePeriodScopeTypes.BusinessUnit, BusinessUnitId = "rx",
            CycleStatus = CyclePeriodStatuses.Active
        };
        var b = new PeriodEntity
        {
            Id = Guid.NewGuid(), TenantId = TenantA, CycleCode = "x-2", CycleName = "x2",
            Year = 2026, SequenceInYear = 9, StartDate = Mar1, EndDate = Apr30,
            ScopeType = CyclePeriodScopeTypes.BusinessUnit, BusinessUnitId = "rx",
            CycleStatus = CyclePeriodStatuses.Active
        };
        repo.Items.Add(a);
        repo.Items.Add(b);

        var r = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, "rx"), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.Ambiguous, r.Data!.Outcome);
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, r.Data.ResolvedScopeType);
        Assert.Equal(2, r.Data.CandidateIds.Count);
        Assert.Null(r.Data.Period);
    }

    [Fact]
    public async Task No_Covering_Period_Anywhere_Is_None_And_Names_No_Level()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);

        var r = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Jun30, "TR", LegalEntityX, "rx"), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.None, r.Data!.Outcome);
        Assert.Null(r.Data.ResolvedScopeType);
        Assert.Null(r.Data.Period);
        Assert.Empty(r.Data.CandidateIds);
    }

    [Fact]
    public async Task Resolution_Echoes_The_Requested_Scope_So_An_Answer_Is_Self_Describing()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);

        var r = await Resolve(repo).Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), "tr", LegalEntityY, " rx "), default);

        Assert.Equal("TR", r.Data!.Country);
        Assert.Equal(LegalEntityY, r.Data.LegalEntityId);
        Assert.Equal("rx", r.Data.BusinessUnitId);
    }

    [Fact]
    public async Task Resolve_Writes_Nothing()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1, activate: true);
        var before = repo.Items.Select(p => (p.Id, p.Version, p.CycleStatus)).ToList();

        await Resolve(repo).Handle(new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), "TR", LegalEntityX, "rx"), default);
        await Resolve(repo).Handle(new ResolveActiveCyclePeriodQuery(Jun30, null, null, null), default);

        Assert.Equal(before, repo.Items.Select(p => (p.Id, p.Version, p.CycleStatus)).ToList());
    }

    [Fact]
    public void The_Precedence_Order_Is_Defined_In_Exactly_One_Place()
    {
        // Two definitions of an order are two orders. The engine walks this array and nothing restates it.
        Assert.Equal(
            new[]
            {
                CyclePeriodScopeTypes.BusinessUnit, CyclePeriodScopeTypes.LegalEntity,
                CyclePeriodScopeTypes.Country, CyclePeriodScopeTypes.Tenant
            },
            CyclePeriodScopeTypes.ByPrecedence);
    }

    // ── listing filters narrow, they never resolve ──────────────────────────────────────────────────────────────────

    private static GetCyclePeriodListHandler List(FakeRepo repo) => new(Tenant(TenantA), repo);

    [Fact]
    public async Task Listing_By_Country_Shows_Country_Periods_Not_What_A_Country_Caller_Would_Resolve_To()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1);
        await SeedAsync(repo, "c-tr", CyclePeriodScopeTypes.Country, country: "TR", sequence: 2);

        var r = await List(repo).Handle(
            new GetCyclePeriodListQuery(null, null, null, "TR", null, null, null, null, null), default);

        var item = Assert.Single(r.Data!.Items);
        Assert.Equal("c-tr", item.CycleCode);
    }

    [Fact]
    public async Task Listing_By_Scope_Type_Narrows_To_That_Level()
    {
        var repo = new FakeRepo();
        await SeedAsync(repo, "c-tenant", CyclePeriodScopeTypes.Tenant, sequence: 1);
        await SeedAsync(repo, "c-rx", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "rx", sequence: 2);

        var r = await List(repo).Handle(
            new GetCyclePeriodListQuery(
                null, null, CyclePeriodScopeTypes.BusinessUnit, null, null, null, null, null, null),
            default);

        Assert.Equal("c-rx", Assert.Single(r.Data!.Items).CycleCode);
    }

    [Fact]
    public async Task An_Unknown_Scope_Type_Filter_Is_400_Rather_Than_Everything()
    {
        var repo = new FakeRepo();
        var r = await List(repo).Handle(
            new GetCyclePeriodListQuery(null, null, "region", null, null, null, null, null, null), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeTypeUnknown, r.Errors!);
    }

    // ── structural boundaries ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_Read_Seam_Holds_No_HttpClient_And_No_Write_Or_Cross_Module_Dependency()
    {
        var dependencies = typeof(CyclePeriodReader)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(HttpClient), dependencies);
        Assert.DoesNotContain(typeof(ICyclePeriodLegalEntityValidator), dependencies);
        Assert.DoesNotContain(typeof(ITerritoryBusinessUnitCatalog), dependencies);
        Assert.DoesNotContain(typeof(ICyclePeriodLegalEntityCatalog), dependencies);
        Assert.Contains(typeof(ICyclePeriodRepository), dependencies);
    }

    [Fact]
    public void No_CyclePeriod_Handler_Injects_A_Territory_Or_Sibling_Module_Repository()
    {
        // The narrow catalog seam exists so this can be asserted structurally rather than promised in review.
        var forbidden = new[]
        {
            typeof(ITerritoryModelRepository), typeof(ITerritoryNodeRepository),
            typeof(ICampaignRepository), typeof(IVisitFrequencyPolicyRepository),
            typeof(IStrategyTemplateRepository)
        };

        var handlers = typeof(CreateCyclePeriodHandler).Assembly
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith(
                "Diten.CrmService.Application.Features.CyclePeriod", StringComparison.Ordinal) == true)
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var handler in handlers)
        {
            foreach (var ctor in handler.GetConstructors())
            {
                foreach (var parameter in ctor.GetParameters())
                {
                    Assert.DoesNotContain(parameter.ParameterType, forbidden);
                }
            }
        }
    }

    [Fact]
    public void Every_Flag_Fu06_Closed_Is_Still_Closed()
    {
        var flags = CyclePeriodFeatureFlags.Current;

        Assert.False(flags.SupportsCampaignBinding);
        Assert.False(flags.SupportsMicroTargetGeneration);
        Assert.False(flags.SupportsFrequencyPolicyWrite);
        Assert.False(flags.SupportsFrequencyPolicyBackReference);
        Assert.False(flags.SupportsStrategyApply);
        Assert.False(flags.SupportsCycleOverlap);
        Assert.False(flags.SupportsCycleCalendarHierarchy);
        Assert.False(flags.SupportsCyclePeriodVersioning);
        Assert.False(flags.SupportsCycleReschedule);
        Assert.False(flags.SupportsCycleAutoClose);
        Assert.False(flags.SupportsWorkingCalendarIntegration);
        Assert.False(flags.SupportsWorkingDayCount);
        Assert.False(flags.SupportsHardDelete);
        Assert.False(flags.SupportsBulkDelete);
    }

    [Fact]
    public void The_New_Flags_Say_What_Fu07_Opened_And_What_It_Refused()
    {
        var flags = CyclePeriodFeatureFlags.Current;

        Assert.True(flags.SupportsCountryScopedCycles);
        Assert.True(flags.SupportsLegalEntityScopedCycles);
        Assert.True(flags.SupportsScopePrecedenceResolution);
        Assert.True(flags.SupportsTerritorySourcedBusinessUnits);

        Assert.False(flags.SupportsScopeTypeMutation);
        Assert.False(flags.SupportsScopeMerge);
        Assert.False(flags.SupportsCrossScopeOverlapBan);
        Assert.False(flags.SupportsScopeInheritance);
        Assert.False(flags.SupportsOrganizationUnitScopedCycles);
    }
}
