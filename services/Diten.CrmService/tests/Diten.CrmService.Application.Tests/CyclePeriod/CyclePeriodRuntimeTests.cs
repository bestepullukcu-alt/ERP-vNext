using System.Reflection;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CyclePeriod;
using Diten.CrmService.Application.Features.CyclePeriod.Commands;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Handlers.CommandHandlers;
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
/// MOD-0165 FU06 — CyclePeriod. Pins down: a period is born draft and TenantId is claim-only; EndDate is inclusive and
/// must be after StartDate; code uniqueness survives closing; (year, sequence) uniqueness is per business-unit scope;
/// ACTIVE periods of one scope may never share a day while drafts may; the lifecycle is one-way with closed terminal;
/// an active period's dates are immutable; resolution is deterministic (resolved / none / ambiguous) with specificity
/// and no merging; time never mutates a row; and the read seam neither writes nor reaches into another module.
/// </summary>
public sealed class CyclePeriodRuntimeTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Mar1 = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Apr30 = new(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset May1 = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun30 = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class FakeRepo : ICyclePeriodRepository
    {
        public List<PeriodEntity> Items { get; } = new();
        public int InsertCount { get; private set; }
        public int ReplaceCount { get; private set; }

        private static IReadOnlyList<PeriodEntity> Scope(IEnumerable<PeriodEntity> rows, Guid tenantId)
            => rows.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList();

        public Task<PeriodEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Scope(Items, tenantId).FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<PeriodEntity>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(Scope(Items, tenantId));

        public Task<IReadOnlyList<PeriodEntity>> ListByCodeAsync(Guid tenantId, string cycleCode, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(
                Scope(Items, tenantId).Where(x => x.CycleCode == cycleCode).ToList());

        public Task<IReadOnlyList<PeriodEntity>> ListByYearAsync(Guid tenantId, int year, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(
                Scope(Items, tenantId).Where(x => x.Year == year).ToList());

        public Task<IReadOnlyList<PeriodEntity>> ListActiveAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(
                Scope(Items, tenantId).Where(x => x.IsActive()).ToList());

        public Task InsertAsync(PeriodEntity entity, CancellationToken ct)
        {
            InsertCount++;
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceAsync(PeriodEntity entity, int expectedVersion, CancellationToken ct)
        {
            ReplaceCount++;
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

    /// <summary>
    /// FU07 test doubles for the write path's scope gate. They are permissive on purpose: THESE tests are about the
    /// period rules, and a governed vocabulary that refused everything would mask them. The gate's own behaviour —
    /// unpublished set, unknown value, unreachable MDM, the territory stamp — is pinned in
    /// <c>CyclePeriodScopeTests</c>, against the real validator.
    /// </summary>
    private sealed class AllowAllReferences : Application.Common.ReferenceValidation.IReferenceDataValidator
    {
        public Task<Application.Common.ReferenceValidation.ReferenceValidationResult> ValidateAsync(
            string setCode, string value, CancellationToken cancellationToken)
            => Task.FromResult(new Application.Common.ReferenceValidation.ReferenceValidationResult(
                Application.Common.ReferenceValidation.ReferenceValidationStatus.Valid, setCode, value));
    }

    private sealed class AllowAllLegalEntities : Application.Features.CyclePeriod.Services.ICyclePeriodLegalEntityValidator
    {
        public Task<Application.Features.CyclePeriod.Services.CyclePeriodLegalEntityValidation> ValidateAsync(
            Guid legalEntityId, CancellationToken cancellationToken)
            => Task.FromResult(Application.Features.CyclePeriod.Services.CyclePeriodLegalEntityValidation.Valid);
    }

    /// <summary>No territory plan matches, so every business unit is stamped <c>manual</c> — the honest default.</summary>
    private sealed class NoTerritoryPlans : ITerritoryBusinessUnitCatalog
    {
        public Task<IReadOnlyList<TerritoryBusinessUnitCandidate>> GetCandidatesAsync(
            string? country, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<TerritoryBusinessUnitCandidate>>(
                Array.Empty<TerritoryBusinessUnitCandidate>());
    }

    private static Application.Features.CyclePeriod.Services.CyclePeriodScopeWriteValidator Scopes()
        => new(new AllowAllReferences(), new AllowAllLegalEntities(), new NoTerritoryPlans());

    private sealed class Fixture
    {
        public FakeRepo Repo { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        public CreateCyclePeriodHandler Create(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Repo, Scopes());

        public UpdateCyclePeriodHandler Update() => new(Tenant(TenantId), new NullActorContext(), Repo, Scopes());

        public ActivateCyclePeriodHandler Activate() => new(Tenant(TenantId), new NullActorContext(), Repo);

        public CloseCyclePeriodHandler Close() => new(Tenant(TenantId), new NullActorContext(), Repo);

        public GetCyclePeriodListHandler List(Guid? tenant = null) => new(Tenant(tenant ?? TenantId), Repo);

        public GetCyclePeriodByIdHandler Get(Guid? tenant = null) => new(Tenant(tenant ?? TenantId), Repo);

        public GetCyclePeriodSelectorHandler Selector() => new(Tenant(TenantId), Repo);

        public GetCyclePeriodContractHandler Contract() => new(Tenant(TenantId));

        public CyclePeriodReader Reader(Guid? tenant = null) => new(Tenant(tenant ?? TenantId), Repo);

        public ResolveActiveCyclePeriodHandler Resolve(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), Reader(tenant));
    }

    private static CreateCyclePeriodCommand Cmd(
        string code = "c-2026-02",
        string name = "2026 / cycle 2",
        int year = 2026,
        int sequence = 2,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string? businessUnitId = null,
        string? description = null,
        string? scopeType = null)
        // FU07: an unstated scope follows the FU06 reading of BusinessUnitId — null means tenant-wide, a value means
        // that business unit. Every FU06 case in this file therefore keeps the exact scope it always had.
        => new(
            code, name, year, sequence, start ?? Mar1, end ?? Apr30,
            scopeType ?? (businessUnitId is null
                ? CyclePeriodScopeTypes.Tenant
                : CyclePeriodScopeTypes.BusinessUnit),
            null, null, businessUnitId, description);

    private static async Task<Guid> SeedAsync(
        Fixture f, string code, int sequence, DateTimeOffset start, DateTimeOffset end,
        string? businessUnitId = null, bool activate = false, bool close = false)
    {
        var created = await f.Create().Handle(
            Cmd(code, "period " + code, 2026, sequence, start, end, businessUnitId), default);
        var id = created.Data;
        if (activate)
        {
            await f.Activate().Handle(new ActivateCyclePeriodCommand(id, null), default);
        }

        if (close)
        {
            await f.Close().Handle(new CloseCyclePeriodCommand(id, null), default);
        }

        return id;
    }

    // ---------------- Create ----------------

    [Fact]
    public async Task Create_Valid_Persists_As_Draft_And_Returns_201()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(), default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(TenantA, row.TenantId);
        Assert.Equal(CyclePeriodStatuses.Draft, row.CycleStatus);
        Assert.Equal("c-2026-02", row.CycleCode);
        Assert.Null(row.ActivatedAt);
        Assert.Null(row.ClosedAt);
    }

    [Fact]
    public async Task Create_Normalizes_Dates_To_Utc_Midnight()
    {
        var f = new Fixture(TenantA);
        // A period is a run of DAYS: the caller's clock time must not survive, or the inclusive end date would
        // silently exclude most of its own last day.
        var start = new DateTimeOffset(2026, 3, 1, 17, 45, 0, TimeSpan.FromHours(3));
        var end = new DateTimeOffset(2026, 4, 30, 23, 15, 0, TimeSpan.FromHours(3));

        await f.Create().Handle(Cmd(start: start, end: end), default);

        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(TimeSpan.Zero, row.StartDate.Offset);
        Assert.Equal(new DateTime(2026, 3, 1), row.StartDate.DateTime);
        Assert.Equal(new DateTime(2026, 4, 30), row.EndDate.DateTime);
    }

    [Fact]
    public async Task Create_Without_Tenant_Is_400()
    {
        var repo = new FakeRepo();
        var handler = new CreateCyclePeriodHandler(new TenantContext(), new NullActorContext(), repo, Scopes());

        var r = await handler.Handle(Cmd(), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Empty(repo.Items);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Has Space")]
    [InlineData("UPPER!")]
    public async Task Create_Invalid_Code_Is_400(string code)
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(code: code), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Empty(f.Repo.Items);
    }

    [Fact]
    public async Task Create_Missing_Name_Is_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(name: "  "), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.NameRequired, r.Errors!);
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public async Task Create_Year_Out_Of_Range_Is_400(int year)
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(year: year), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.YearInvalid, r.Errors!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task Create_Sequence_Out_Of_Range_Is_400(int sequence)
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(sequence: sequence), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.SequenceInvalid, r.Errors!);
    }

    [Fact]
    public async Task Create_EndDate_Before_Start_Is_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(start: Apr30, end: Mar1), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.WindowInvalid, r.Errors!);
    }

    [Fact]
    public async Task Create_EndDate_Equal_To_Start_Is_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Create().Handle(Cmd(start: Mar1, end: Mar1), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.WindowInvalid, r.Errors!);
    }

    // ---------------- Uniqueness ----------------

    [Fact]
    public async Task Create_Duplicate_Code_Is_409()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30);

        var r = await f.Create().Handle(Cmd(code: "c-1", sequence: 5), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.CodeTaken, r.Errors!);
    }

    [Fact]
    public async Task Create_Duplicate_Code_Is_409_Even_When_The_Holder_Is_Closed()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, close: true);

        // A closed period's code is a permanent historical identifier: reusing it would make an old plan ambiguous.
        var r = await f.Create().Handle(Cmd(code: "c-1", sequence: 5), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.CodeTaken, r.Errors!);
    }

    [Fact]
    public async Task Create_Duplicate_Sequence_In_Same_Scope_Is_409()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30);

        var r = await f.Create().Handle(Cmd(code: "c-2", sequence: 1), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.SequenceTaken, r.Errors!);
    }

    [Fact]
    public async Task Create_Same_Sequence_In_A_Different_Business_Unit_Is_Allowed()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-rx-1", 1, Mar1, Apr30, businessUnitId: "rx");

        var r = await f.Create().Handle(Cmd(code: "c-otc-1", sequence: 1, businessUnitId: "otc"), default);

        Assert.Equal(201, r.StatusCode);
        Assert.Equal(2, f.Repo.Items.Count);
    }

    [Fact]
    public async Task Create_Same_Sequence_Tenant_Wide_And_Business_Unit_Are_Separate_Scopes()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-all-1", 1, Mar1, Apr30);

        var r = await f.Create().Handle(Cmd(code: "c-rx-1", sequence: 1, businessUnitId: "rx"), default);

        Assert.Equal(201, r.StatusCode);
    }

    // ---------------- Overlap (activate) ----------------

    [Fact]
    public async Task Activate_Overlapping_Active_Period_In_Same_Scope_Is_409_And_Stays_Draft()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);
        var second = await SeedAsync(f, "c-2", 2, Apr30.AddDays(-5), Jun30);

        var r = await f.Activate().Handle(new ActivateCyclePeriodCommand(second, null), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.Overlap, r.Errors!);
        Assert.Equal(CyclePeriodStatuses.Draft, f.Repo.Items.Single(x => x.Id == second).CycleStatus);
    }

    [Fact]
    public async Task Activate_Refusal_Names_The_Blocking_Period()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);
        var second = await SeedAsync(f, "c-2", 2, Apr30, Jun30);

        var r = await f.Activate().Handle(new ActivateCyclePeriodCommand(second, null), default);

        // An author who cannot see what they collided with cannot fix it.
        Assert.Contains(r.Errors!, e => e.Contains("c-1"));
    }

    [Fact]
    public async Task Activate_Touching_End_Date_Is_An_Overlap_Because_EndDate_Is_Inclusive()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);
        var second = await SeedAsync(f, "c-2", 2, Apr30, Jun30);

        var r = await f.Activate().Handle(new ActivateCyclePeriodCommand(second, null), default);

        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task Activate_Day_After_The_Previous_End_Is_Allowed()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);
        var second = await SeedAsync(f, "c-2", 2, May1, Jun30);

        var r = await f.Activate().Handle(new ActivateCyclePeriodCommand(second, null), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(CyclePeriodStatuses.Active, f.Repo.Items.Single(x => x.Id == second).CycleStatus);
    }

    [Fact]
    public async Task Activate_Overlapping_Draft_Period_Is_Allowed()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-draft", 1, Mar1, Jun30);
        var second = await SeedAsync(f, "c-2", 2, Mar1, Apr30);

        // Drafts are the planning space: sketching two competing calendars must stay possible.
        var r = await f.Activate().Handle(new ActivateCyclePeriodCommand(second, null), default);

        Assert.Equal(200, r.StatusCode);
    }

    [Fact]
    public async Task Activate_Overlapping_Closed_Period_Is_Allowed()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true, close: true);
        var second = await SeedAsync(f, "c-2", 2, Mar1, Apr30);

        // Closing frees the days again — that is the supported way to correct a live calendar.
        var r = await f.Activate().Handle(new ActivateCyclePeriodCommand(second, null), default);

        Assert.Equal(200, r.StatusCode);
    }

    [Fact]
    public async Task Activate_Overlapping_Period_In_A_Different_Business_Unit_Is_Allowed()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-rx", 1, Mar1, Apr30, businessUnitId: "rx", activate: true);
        var second = await SeedAsync(f, "c-otc", 1, Mar1, Apr30, businessUnitId: "otc");

        var r = await f.Activate().Handle(new ActivateCyclePeriodCommand(second, null), default);

        Assert.Equal(200, r.StatusCode);
    }

    [Fact]
    public async Task Activate_Twice_Is_409()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);

        var r = await f.Activate().Handle(new ActivateCyclePeriodCommand(id, null), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.AlreadyActive, r.Errors!);
    }

    // ---------------- Lifecycle ----------------

    [Fact]
    public async Task Close_From_Draft_Is_Allowed()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30);

        var r = await f.Close().Handle(new CloseCyclePeriodCommand(id, null), default);

        Assert.Equal(200, r.StatusCode);
        var row = f.Repo.Items.Single();
        Assert.Equal(CyclePeriodStatuses.Closed, row.CycleStatus);
        Assert.NotNull(row.ClosedAt);
    }

    [Fact]
    public async Task Close_From_Active_Is_Allowed()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);

        var r = await f.Close().Handle(new CloseCyclePeriodCommand(id, null), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(CyclePeriodStatuses.Closed, f.Repo.Items.Single().CycleStatus);
    }

    [Fact]
    public async Task Close_Twice_Is_409()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30, close: true);

        var r = await f.Close().Handle(new CloseCyclePeriodCommand(id, null), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.Closed, r.Errors!);
    }

    [Fact]
    public async Task Activate_After_Close_Is_409_Because_Closed_Is_Terminal()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30, close: true);

        var r = await f.Activate().Handle(new ActivateCyclePeriodCommand(id, null), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.Closed, r.Errors!);
    }

    [Fact]
    public async Task Update_Of_A_Closed_Period_Is_409()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30, close: true);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(id, "renamed", 2026, 1, Mar1, Apr30, null, null, null, null, null, null), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.Closed, r.Errors!);
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task Update_Draft_Can_Move_The_Window()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(id, "moved", 2026, 3, May1, Jun30, null, null, null, null, null, null), default);

        Assert.Equal(200, r.StatusCode);
        var row = f.Repo.Items.Single();
        Assert.Equal(May1, row.StartDate);
        Assert.Equal(3, row.SequenceInYear);
        Assert.Equal("moved", row.CycleName);
    }

    [Fact]
    public async Task Update_Active_Rename_Only_Is_Allowed()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(id, "new name", 2026, 1, Mar1, Apr30, null, null, null, null, "note", null), default);

        Assert.Equal(200, r.StatusCode);
        var row = f.Repo.Items.Single();
        Assert.Equal("new name", row.CycleName);
        Assert.Equal("note", row.Description);
    }

    [Fact]
    public async Task Update_Active_Window_Is_409()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(id, "c-1", 2026, 1, Mar1, Jun30, null, null, null, null, null, null), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.DatesImmutable, r.Errors!);
    }

    [Fact]
    public async Task Update_Active_Business_Unit_Is_409()
    {
        // FU06 promise, expressed in FU07's model: an ACTIVE period's scope cannot be moved. The period is seeded at
        // the business-unit scope and the edit tries to point it at a different unit — the same level, a different
        // address, which is exactly the "structural change" the guard refuses.
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30, businessUnitId: "rx", activate: true);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(
                id, "c-1", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.BusinessUnit, null, null, "otc", null, null),
            default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.DatesImmutable, r.Errors!);
    }

    [Fact]
    public async Task Update_Duplicate_Sequence_On_A_Draft_Is_409()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30);
        var second = await SeedAsync(f, "c-2", 2, May1, Jun30);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(second, "c-2", 2026, 1, May1, Jun30, null, null, null, null, null, null), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.SequenceTaken, r.Errors!);
    }

    [Fact]
    public async Task Update_With_Stale_ExpectedVersion_Is_409()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(id, "x", 2026, 1, Mar1, Apr30, null, null, null, null, null, 42), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ConcurrencyConflict, r.Errors!);
        Assert.Equal("period c-1", f.Repo.Items.Single().CycleName);
    }

    [Fact]
    public async Task Update_Unknown_Id_Is_404()
    {
        var f = new Fixture(TenantA);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(Guid.NewGuid(), "x", 2026, 1, Mar1, Apr30, null, null, null, null, null, null), default);

        Assert.Equal(404, r.StatusCode);
    }

    // ---------------- Tenant isolation ----------------

    [Fact]
    public async Task Another_Tenants_Period_Is_404_Not_403()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30);

        var r = await f.Get(TenantB).Handle(new GetCyclePeriodByIdQuery(id), default);

        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task List_Is_Tenant_Isolated()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30);

        var r = await f.List(TenantB).Handle(
            new GetCyclePeriodListQuery(null, null, null, null, null, null, null, null, null), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Empty(r.Data!.Items);
    }

    [Fact]
    public async Task Create_Ignores_Any_Client_Tenant_And_Uses_The_Claim()
    {
        var f = new Fixture(TenantA);
        await f.Create(TenantB).Handle(Cmd(), default);

        Assert.Equal(TenantB, f.Repo.Items.Single().TenantId);
    }

    // ---------------- List / vocabulary ----------------

    [Fact]
    public async Task List_Unknown_Status_Filter_Is_400()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30);

        // Silently returning everything when the caller asked for something specific is how a UI ends up lying.
        var r = await f.List().Handle(
            new GetCyclePeriodListQuery("archived", null, null, null, null, null, null, null, null), default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.StatusUnknown, r.Errors!);
    }

    [Fact]
    public async Task List_CoversDate_Filters_Rows_Regardless_Of_Status()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30);
        await SeedAsync(f, "c-2", 2, May1, Jun30);

        var r = await f.List().Handle(
            new GetCyclePeriodListQuery(null, null, null, null, null, null, null, Mar1.AddDays(10), null), default);

        Assert.Equal("c-1", Assert.Single(r.Data!.Items).CycleCode);
    }

    [Fact]
    public async Task List_Orders_By_Year_Then_Sequence_Descending()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30);
        await SeedAsync(f, "c-2", 2, May1, Jun30);

        var r = await f.List().Handle(new GetCyclePeriodListQuery(null, null, null, null, null, null, null, null, null), default);

        Assert.Equal(new[] { "c-2", "c-1" }, r.Data!.Items.Select(i => i.CycleCode).ToArray());
    }

    [Fact]
    public async Task Selector_Returns_Only_The_Picker_Shape()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);

        var r = await f.Selector().Handle(
            new GetCyclePeriodSelectorQuery(2026, CyclePeriodStatuses.Active, null, null, null, null), default);

        var item = Assert.Single(r.Data!.Items);
        Assert.Equal("c-1", item.CycleCode);
        Assert.Equal(CyclePeriodStatuses.Active, item.CycleStatus);
    }

    [Fact]
    public async Task Contract_Declares_Every_Boundary_Flag_False()
    {
        var f = new Fixture(TenantA);
        var r = await f.Contract().Handle(new GetCyclePeriodContractQuery(), default);

        var flags = r.Data!.Features;
        Assert.True(flags.SupportsCyclePeriod);
        Assert.True(flags.SupportsActiveCycleResolution);
        Assert.False(flags.SupportsMicroTargetGeneration);
        Assert.False(flags.SupportsCampaignBinding);
        Assert.False(flags.SupportsFrequencyPolicyWrite);
        Assert.False(flags.SupportsStrategyApply);
        Assert.False(flags.SupportsWorkingCalendarIntegration);
        Assert.False(flags.SupportsCycleAutoClose);
        Assert.False(flags.SupportsBulkDelete);
        Assert.False(flags.SupportsHardDelete);
        Assert.False(flags.SupportsCycleOverlap);
        Assert.False(flags.SupportsCycleCalendarHierarchy);
        Assert.False(flags.SupportsCyclePeriodVersioning);
    }

    // ---------------- Resolution ----------------

    [Fact]
    public async Task Resolve_Returns_The_Single_Covering_Active_Period()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);

        var r = await f.Resolve().Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(10), null, null, null), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.Resolved, r.Data!.Outcome);
        Assert.Equal(id, r.Data.Period!.CyclePeriodId);
    }

    [Theory]
    [InlineData(0)]   // first day
    [InlineData(60)]  // last day (Mar 1 + 60 = Apr 30)
    public async Task Resolve_Includes_Both_Window_Ends(int dayOffset)
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);

        var r = await f.Resolve().Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(dayOffset), null, null, null), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.Resolved, r.Data!.Outcome);
    }

    [Fact]
    public async Task Resolve_Outside_Every_Window_Is_None_And_Never_The_Nearest_Period()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);

        var r = await f.Resolve().Handle(new ResolveActiveCyclePeriodQuery(Jun30, null, null, null), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.None, r.Data!.Outcome);
        Assert.Null(r.Data.Period);
        Assert.Empty(r.Data.CandidateIds);
    }

    [Fact]
    public async Task Resolve_Ignores_Draft_And_Closed_Periods()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-draft", 1, Mar1, Apr30);
        await SeedAsync(f, "c-closed", 2, Mar1, Apr30, close: true);

        var r = await f.Resolve().Handle(new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, null), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.None, r.Data!.Outcome);
    }

    [Fact]
    public async Task Resolve_An_Expired_But_Still_Active_Period_Is_None_Because_Time_Never_Closes_A_Row()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);

        var r = await f.Resolve().Handle(new ResolveActiveCyclePeriodQuery(May1, null, null, null), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.None, r.Data!.Outcome);
        // The row is untouched: no job closed it, and nothing was written by asking.
        Assert.Equal(CyclePeriodStatuses.Active, f.Repo.Items.Single().CycleStatus);
    }

    [Fact]
    public async Task Resolve_Prefers_The_Business_Unit_Period_Over_The_Tenant_Wide_One()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-all", 1, Mar1, Apr30, activate: true);
        var rx = await SeedAsync(f, "c-rx", 1, Mar1, Apr30, businessUnitId: "rx", activate: true);

        var r = await f.Resolve().Handle(new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, "rx"), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.Resolved, r.Data!.Outcome);
        Assert.Equal(rx, r.Data.Period!.CyclePeriodId);
    }

    [Fact]
    public async Task Resolve_Falls_Back_To_Tenant_Wide_When_The_Business_Unit_Has_No_Period()
    {
        var f = new Fixture(TenantA);
        var all = await SeedAsync(f, "c-all", 1, Mar1, Apr30, activate: true);

        var r = await f.Resolve().Handle(new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, "otc"), default);

        Assert.Equal(all, r.Data!.Period!.CyclePeriodId);
    }

    [Fact]
    public async Task Resolve_Without_A_Business_Unit_Never_Sees_A_Unit_Specific_Period()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-rx", 1, Mar1, Apr30, businessUnitId: "rx", activate: true);

        var r = await f.Resolve().Handle(new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, null), default);

        Assert.Equal(CyclePeriodResolutionOutcomes.None, r.Data!.Outcome);
    }

    [Fact]
    public void Resolve_With_Two_Covering_Active_Periods_Is_Ambiguous_And_Selects_Nothing()
    {
        // Only reachable when the overlap ban was bypassed (a hand-edited document). Picking a winner here would hide
        // a data defect behind a plausible answer.
        var a = new PeriodEntity
        {
            Id = Guid.NewGuid(), TenantId = TenantA, CycleCode = "c-1", CycleName = "c-1",
            Year = 2026, SequenceInYear = 1, StartDate = Mar1, EndDate = Jun30,
            CycleStatus = CyclePeriodStatuses.Active
        };
        var b = new PeriodEntity
        {
            Id = Guid.NewGuid(), TenantId = TenantA, CycleCode = "c-2", CycleName = "c-2",
            Year = 2026, SequenceInYear = 2, StartDate = Mar1, EndDate = Apr30,
            CycleStatus = CyclePeriodStatuses.Active
        };

        var resolution = CyclePeriodResolveEngine.Resolve(new[] { a, b }, Mar1.AddDays(5), CyclePeriodResolveEngine.ScopeRequest.TenantOnly);

        Assert.Equal(CyclePeriodResolutionOutcomes.Ambiguous, resolution.Outcome);
        Assert.Null(resolution.Period);
        Assert.Equal(2, resolution.CandidateIds.Count);
    }

    [Fact]
    public async Task Resolve_Writes_Nothing()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-1", 1, Mar1, Apr30, activate: true);
        var insertsBefore = f.Repo.InsertCount;
        var replacesBefore = f.Repo.ReplaceCount;

        await f.Resolve().Handle(new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, null), default);
        await f.Resolve().Handle(new ResolveActiveCyclePeriodQuery(Jun30, null, null, null), default);

        Assert.Equal(insertsBefore, f.Repo.InsertCount);
        Assert.Equal(replacesBefore, f.Repo.ReplaceCount);
    }

    [Fact]
    public async Task Reader_ListByYear_Scopes_By_Business_Unit_When_Asked()
    {
        var f = new Fixture(TenantA);
        await SeedAsync(f, "c-all", 1, Mar1, Apr30);
        await SeedAsync(f, "c-rx", 1, Mar1, Apr30, businessUnitId: "rx");

        var scoped = await f.Reader().ListByYearAsync(2026, CyclePeriodScopeTypes.BusinessUnit, "rx", default);
        var all = await f.Reader().ListByYearAsync(2026, null, null, default);

        Assert.Equal("c-rx", Assert.Single(scoped).CycleCode);
        Assert.Equal(2, all.Count);
    }

    // ---------------- Structural boundary ----------------

    [Fact]
    public void Reader_Has_No_HttpClient_And_No_Foreign_Module_Dependency()
    {
        // The seam must stay in-process (no HTTP self-call) and must never become a doorway into another module's
        // aggregate. Asserting the constructor shape keeps that true as the code evolves.
        var parameters = typeof(CyclePeriodReader)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToList();

        Assert.Equal(2, parameters.Count);
        Assert.Contains(typeof(ITenantContext), parameters);
        Assert.Contains(typeof(ICyclePeriodRepository), parameters);
        Assert.DoesNotContain(parameters, t => t.Name.Contains("HttpClient", StringComparison.Ordinal));
    }

    [Fact]
    public void No_Handler_Injects_A_Foreign_Module_Repository()
    {
        // MicroTarget generation, campaign binding and frequency-policy writing are all out of scope, and the cheapest
        // way to keep them out is to prove no handler can even reach those aggregates.
        var forbidden = new[]
        {
            "ICampaignRepository", "ICampaignTargetRepository", "IVisitFrequencyPolicyRepository",
            "IStrategyTemplateRepository", "ISegmentRepository", "ITargetCustomerRepository"
        };

        var handlerTypes = typeof(CreateCyclePeriodHandler).Assembly
            .GetTypes()
            .Where(t => t.Namespace is { } ns
                        && ns.StartsWith("Diten.CrmService.Application.Features.CyclePeriod", StringComparison.Ordinal))
            .ToList();

        var offenders = handlerTypes
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType.Name)
            .Where(n => forbidden.Contains(n, StringComparer.Ordinal))
            .Distinct()
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_Feature_Exposes_No_Delete_Or_Bulk_Command()
    {
        var commandNames = typeof(CreateCyclePeriodHandler).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "Diten.CrmService.Application.Features.CyclePeriod.Commands")
            .Select(t => t.Name)
            .ToList();

        Assert.DoesNotContain(commandNames, n => n.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(commandNames, n => n.Contains("Bulk", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(commandNames, n => n.Contains("Reopen", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(commandNames, n => n.Contains("Apply", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Permissions_Are_Defined_But_Nothing_Is_Seeded()
    {
        Assert.Equal(
            new[] { "crm.cycle-period.read", "crm.cycle-period.manage", "crm.cycle-period.activate" },
            CyclePeriodPermissions.All.ToArray());
    }

    // ---------------- FU06 backward compatibility: an omitted ScopeType (FU07 regression guard) ----------------
    //
    // FU06 had no ScopeType at all. FU07 made it required, which silently broke every caller written against FU06 —
    // create and update started answering "Unknown ScopeType ''". The rule below restores them WITHOUT weakening FU07:
    // the two shapes FU06 could express (tenant-wide, or a business unit) are derived, and the two levels FU07 added
    // still demand an explicit ScopeType, because nothing legacy can be meaning them.

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeriveScopeType_With_No_References_Is_Tenant(string? businessUnitId)
        => Assert.Equal(
            CyclePeriodScopeTypes.Tenant,
            CyclePeriodScopeRules.DeriveScopeType(null, null, businessUnitId));

    [Fact]
    public void DeriveScopeType_With_Only_A_BusinessUnit_Is_BusinessUnit()
        => Assert.Equal(
            CyclePeriodScopeTypes.BusinessUnit,
            CyclePeriodScopeRules.DeriveScopeType(null, null, "alpha"));

    [Fact]
    public void DeriveScopeType_With_A_Country_Is_Null_Because_FU06_Could_Not_Mean_It()
        => Assert.Null(CyclePeriodScopeRules.DeriveScopeType("TR", null, null));

    [Fact]
    public void DeriveScopeType_With_A_LegalEntity_Is_Null_Because_FU06_Could_Not_Mean_It()
        => Assert.Null(CyclePeriodScopeRules.DeriveScopeType(null, Guid.NewGuid(), null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Create_Without_ScopeType_And_Without_References_Is_Tenant_Scoped(string? scopeType)
    {
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            new CreateCyclePeriodCommand(
                "legacy-tenant", "legacy tenant", 2026, 1, Mar1, Apr30, scopeType, null, null, null, null),
            default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(CyclePeriodScopeTypes.Tenant, row.ScopeType);
        Assert.Null(row.ScopeRef());
        Assert.Null(row.BusinessUnitId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Create_Without_ScopeType_But_With_A_BusinessUnit_Is_BusinessUnit_Scoped(string? scopeType)
    {
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            new CreateCyclePeriodCommand(
                "legacy-bu", "legacy bu", 2026, 1, Mar1, Apr30, scopeType, null, null, "alpha", null),
            default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, row.ScopeType);
        Assert.Equal("alpha", row.BusinessUnitId);
        Assert.Equal("alpha", row.ScopeRef());
    }

    [Fact]
    public async Task Create_Without_ScopeType_But_With_A_Country_Is_400()
    {
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            new CreateCyclePeriodCommand(
                "amb-country", "ambiguous", 2026, 1, Mar1, Apr30, null, "TR", null, null, null),
            default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeTypeUnknown, r.Errors!);
        Assert.Empty(f.Repo.Items);
    }

    [Fact]
    public async Task Create_Without_ScopeType_But_With_A_LegalEntity_Is_400()
    {
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            new CreateCyclePeriodCommand(
                "amb-le", "ambiguous", 2026, 1, Mar1, Apr30, null, null, Guid.NewGuid(), null, null),
            default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeTypeUnknown, r.Errors!);
        Assert.Empty(f.Repo.Items);
    }

    [Fact]
    public async Task Create_With_A_Present_But_Unknown_ScopeType_Is_Still_400()
    {
        // Derivation applies to an ABSENT scope type only. A typo is still a typo.
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            new CreateCyclePeriodCommand(
                "typo", "typo", 2026, 1, Mar1, Apr30, "regional", null, null, null, null),
            default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeTypeUnknown, r.Errors!);
    }

    [Fact]
    public async Task Update_Without_ScopeType_Still_Edits_A_Tenant_Row()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "legacy-t", 1, Mar1, Apr30);

        // Exactly the shape an FU06 client posts: the whole form, no scope field, business unit left null.
        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(
                id, "renamed", 2026, 1, Mar1, Apr30, null, null, null, null, "note", null),
            default);

        Assert.Equal(200, r.StatusCode);
        var row = f.Repo.Items.Single();
        Assert.Equal("renamed", row.CycleName);
        Assert.Equal(CyclePeriodScopeTypes.Tenant, row.ScopeType);
    }

    [Fact]
    public async Task Update_Without_ScopeType_Still_Edits_A_BusinessUnit_Row()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "legacy-b", 1, Mar1, Apr30, businessUnitId: "alpha");

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(
                id, "renamed", 2026, 1, Mar1, Apr30, null, null, null, "alpha", null, null),
            default);

        Assert.Equal(200, r.StatusCode);
        var row = f.Repo.Items.Single();
        Assert.Equal("renamed", row.CycleName);
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, row.ScopeType);
        Assert.Equal("alpha", row.BusinessUnitId);
    }

    [Fact]
    public async Task Update_Without_ScopeType_Edits_A_Row_Stored_Without_One()
    {
        // The real legacy case: a document written by FU06 carries ScopeType = "" in storage.
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "legacy-raw", 1, Mar1, Apr30, businessUnitId: "alpha");
        f.Repo.Items.Single().ScopeType = string.Empty;

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(
                id, "renamed", 2026, 1, Mar1, Apr30, null, null, null, "alpha", null, null),
            default);

        Assert.Equal(200, r.StatusCode);
        // The derived type is written down the first time the row is touched — a read-time reading becoming permanent.
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, f.Repo.Items.Single().ScopeType);
    }

    [Fact]
    public async Task Update_Without_ScopeType_But_With_A_Country_Is_400()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "legacy-c", 1, Mar1, Apr30);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(
                id, "renamed", 2026, 1, Mar1, Apr30, null, "TR", null, null, null, null),
            default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeTypeUnknown, r.Errors!);
    }

    [Fact]
    public async Task Update_Without_ScopeType_That_Would_Move_The_Row_Is_Still_Refused()
    {
        // Derivation restores FU06 callers; it does not reopen scope mutation. Clearing the business unit reads as
        // "make this tenant-wide", and FU07 answers that with an actionable 409 rather than silently ignoring it.
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "legacy-m", 1, Mar1, Apr30, businessUnitId: "alpha");

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(
                id, "renamed", 2026, 1, Mar1, Apr30, null, null, null, null, null, null),
            default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeImmutable, r.Errors!);
        Assert.Equal("alpha", f.Repo.Items.Single().BusinessUnitId);
    }

    // ---------------- Year anchor: a period starts in the year it claims ----------------
    //
    // Year is AUTHORED (it is not derived from StartDate) precisely so a cycle can run past new year's eve. That makes
    // it the one field nothing else can catch when it goes wrong: "2026 / cycle 1" beginning in March 2027 would sort,
    // group and resolve as a 2026 period while covering none of it. Anchoring the START closes that hole; the END stays
    // free, because crossing the year is the reason the field exists.

    [Fact]
    public async Task Create_With_A_Start_Inside_The_Planning_Year_Is_201()
    {
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            Cmd("anchor-ok", "anchor ok", 2026, 1,
                new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)),
            default);

        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Create_With_A_Start_In_The_Next_Year_Is_400()
    {
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            Cmd("anchor-bad", "anchor bad", 2026, 1,
                new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 4, 30, 0, 0, 0, TimeSpan.Zero)),
            default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.StartYearMismatch, r.Errors!);
        Assert.Empty(f.Repo.Items);
    }

    [Fact]
    public async Task Create_With_An_End_In_The_Next_Year_Is_201()
    {
        // December → January is a real cycle, and it is exactly what an authored Year is for.
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            Cmd("anchor-cross", "anchor cross", 2026, 6,
                new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(2026, row.Year);
        Assert.Equal(2027, row.EndDate.Year);
    }

    [Fact]
    public async Task Create_With_A_Start_In_The_Previous_Year_Is_400()
    {
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            Cmd("anchor-early", "anchor early", 2026, 1,
                new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.StartYearMismatch, r.Errors!);
    }

    [Fact]
    public void The_Anchor_Judges_The_Normalised_Day_Not_The_Callers_Clock()
    {
        // 1 Jan 2027 00:00 +03:00 IS 31 Dec 2026 in UTC, and UTC is what gets stored and what the resolver reads. The
        // rule has to agree with the persisted date, or a period would be refused for a year it does not actually
        // start in.
        Assert.Null(CyclePeriodValidation.ValidateStartYearAnchor(
            2026, new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.FromHours(3))));

        Assert.NotNull(CyclePeriodValidation.ValidateStartYearAnchor(
            2027, new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.FromHours(3))));
    }

    [Fact]
    public async Task Update_With_A_Start_In_The_Next_Year_Is_400()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "anchor-upd", 1, Mar1, Apr30);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(
                id, "moved", 2026, 1,
                new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 4, 30, 0, 0, 0, TimeSpan.Zero),
                null, null, null, null, null, null),
            default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.StartYearMismatch, r.Errors!);
        Assert.Equal(Mar1, f.Repo.Items.Single().StartDate);
    }

    [Fact]
    public async Task Update_That_Extends_The_End_Into_The_Next_Year_Is_200()
    {
        var f = new Fixture(TenantA);
        var id = await SeedAsync(f, "anchor-ext", 1, Mar1, Apr30);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(
                id, "extended", 2026, 1,
                new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 1, 31, 0, 0, 0, TimeSpan.Zero),
                null, null, null, null, null, null),
            default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(2027, f.Repo.Items.Single().EndDate.Year);
    }

    [Fact]
    public async Task An_Inverted_Window_Is_Still_Reported_As_An_Inverted_Window()
    {
        // Both rules fail here. The more basic one has to win, or the author is told to fix the year when the real
        // problem is that the dates are the wrong way round.
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            Cmd("anchor-inv", "anchor inv", 2026, 1,
                new DateTimeOffset(2027, 4, 30, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero)),
            default);

        Assert.Equal(400, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.WindowInvalid, r.Errors!);
        Assert.DoesNotContain(CyclePeriodErrorCodes.StartYearMismatch, r.Errors!);
    }

    [Fact]
    public void The_Contract_Publishes_The_Start_Year_Error_Code()
        => Assert.Contains(CyclePeriodErrorCodes.StartYearMismatch, CyclePeriodErrorCodes.All);

    // ---------------- Business-unit country context: documentation, never identity ----------------
    //
    // The country a business unit was chosen under is worth keeping — it is the reason that unit was offered at all —
    // but it is the same CLASS of field as BusinessUnitSource: something a reader sees, not something the system keys
    // on. The tests below pin both halves: it is stored and returned, and it is invisible to uniqueness.

    private static CreateCyclePeriodCommand ScopedCmd(
        string code, string scopeType, string? businessUnitId = null, string? countryScope = null,
        Guid? legalEntityId = null, string? businessUnitCountryContext = null, int sequence = 1)
        => new(
            code, "period " + code, 2026, sequence, Mar1, Apr30,
            scopeType, countryScope, legalEntityId, businessUnitId, null, businessUnitCountryContext);

    [Fact]
    public async Task A_Business_Unit_Period_Keeps_The_Country_It_Was_Chosen_Under()
    {
        var f = new Fixture(TenantA);

        var r = await f.Create().Handle(
            ScopedCmd("ctx-1", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha",
                businessUnitCountryContext: "TR"),
            default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal("TR", row.BusinessUnitCountryContext);
        // The identity is still the unit alone.
        Assert.Equal("alpha", row.ScopeRef());
    }

    [Fact]
    public async Task The_Country_Context_Is_Upper_Cased_So_One_Country_Reads_One_Way()
    {
        var f = new Fixture(TenantA);

        await f.Create().Handle(
            ScopedCmd("ctx-case", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha",
                businessUnitCountryContext: " tr "),
            default);

        Assert.Equal("TR", f.Repo.Items.Single().BusinessUnitCountryContext);
    }

    [Fact]
    public async Task The_Country_Context_Reaches_The_List_And_Detail_Payloads()
    {
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(
            ScopedCmd("ctx-dto", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha",
                businessUnitCountryContext: "TR"),
            default);

        var list = await f.List().Handle(
            new GetCyclePeriodListQuery(null, null, null, null, null, null, null, null, null), default);
        var detail = await f.Get().Handle(new GetCyclePeriodByIdQuery(created.Data), default);

        Assert.Equal("TR", Assert.Single(list.Data!.Items).BusinessUnitCountryContext);
        Assert.Equal("TR", detail.Data!.BusinessUnitCountryContext);
    }

    [Fact]
    public async Task Uniqueness_Ignores_The_Country_Context()
    {
        // THE load-bearing test. If the context ever leaked into the key, one business unit could hold two colliding
        // calendars for the same days just because someone picked a different filter.
        var f = new Fixture(TenantA);
        await f.Create().Handle(
            ScopedCmd("ctx-a", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha",
                businessUnitCountryContext: "TR"),
            default);

        var second = await f.Create().Handle(
            ScopedCmd("ctx-b", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha",
                businessUnitCountryContext: "DE"),
            default);

        Assert.Equal(409, second.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.SequenceTaken, second.Errors!);
        Assert.Single(f.Repo.Items);
    }

    [Fact]
    public async Task The_Overlap_Ban_Ignores_The_Country_Context()
    {
        // Same unit, same days, different context — still one calendar, so the second activation collides.
        var f = new Fixture(TenantA);
        var first = await f.Create().Handle(
            ScopedCmd("ovl-a", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha",
                businessUnitCountryContext: "TR", sequence: 1),
            default);
        var second = await f.Create().Handle(
            ScopedCmd("ovl-b", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha",
                businessUnitCountryContext: "DE", sequence: 2),
            default);

        Assert.Equal(200, (await f.Activate().Handle(
            new ActivateCyclePeriodCommand(first.Data, null), default)).StatusCode);

        var clash = await f.Activate().Handle(new ActivateCyclePeriodCommand(second.Data, null), default);

        Assert.Equal(409, clash.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.Overlap, clash.Errors!);
    }

    [Theory]
    [InlineData(CyclePeriodScopeTypes.Tenant)]
    [InlineData(CyclePeriodScopeTypes.Country)]
    [InlineData(CyclePeriodScopeTypes.LegalEntity)]
    public async Task The_Country_Context_Is_Null_At_Every_Other_Scope(string scopeType)
    {
        var f = new Fixture(TenantA);
        var legalEntityId = Guid.NewGuid();

        var r = await f.Create().Handle(
            ScopedCmd(
                "ctx-other", scopeType,
                countryScope: scopeType == CyclePeriodScopeTypes.Country ? "TR" : null,
                legalEntityId: scopeType == CyclePeriodScopeTypes.LegalEntity ? legalEntityId : null,
                // Even when a caller sends one, it is dropped: only a business unit has a country context.
                businessUnitCountryContext: "TR"),
            default);

        Assert.Equal(201, r.StatusCode);
        Assert.Null(Assert.Single(f.Repo.Items).BusinessUnitCountryContext);
    }

    [Fact]
    public async Task A_Legacy_Business_Unit_Period_Without_A_Context_Still_Reads()
    {
        // A row written before the field existed: it simply has no country, and nothing about it breaks.
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(
            ScopedCmd("ctx-legacy", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha"), default);

        var detail = await f.Get().Handle(new GetCyclePeriodByIdQuery(created.Data), default);

        Assert.Null(f.Repo.Items.Single().BusinessUnitCountryContext);
        Assert.Null(detail.Data!.BusinessUnitCountryContext);
        Assert.Equal("alpha", detail.Data.ScopeRef);
    }

    [Fact]
    public async Task An_Edit_Can_Correct_The_Country_Context_Without_Moving_The_Period()
    {
        var f = new Fixture(TenantA);
        var created = await f.Create().Handle(
            ScopedCmd("ctx-edit", CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha",
                businessUnitCountryContext: "TR"),
            default);

        var r = await f.Update().Handle(
            new UpdateCyclePeriodCommand(
                created.Data, "renamed", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.BusinessUnit, null, null, "alpha", null, null, "DE"),
            default);

        Assert.Equal(200, r.StatusCode);
        var row = f.Repo.Items.Single();
        Assert.Equal("DE", row.BusinessUnitCountryContext);
        Assert.Equal("alpha", row.ScopeRef());
    }

    [Fact]
    public void The_Country_Context_Is_Mapped_For_Persistence()
    {
        // A member the class map does not know is a member that never reaches Mongo. String members are covered by
        // AutoMap rather than by a per-member registration, so this asserts the OUTCOME instead of the mechanism.
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var classMap = MongoDB.Bson.Serialization.BsonClassMap.LookupClassMap(typeof(PeriodEntity));

        Assert.Contains(
            classMap.AllMemberMaps,
            m => string.Equals(m.MemberName, nameof(PeriodEntity.BusinessUnitCountryContext), StringComparison.Ordinal));
    }
}
