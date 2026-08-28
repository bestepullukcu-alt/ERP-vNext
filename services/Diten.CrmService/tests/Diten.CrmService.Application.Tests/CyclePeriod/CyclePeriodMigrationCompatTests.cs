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
/// MOD-0165 FU07 §8.7 — the identity migration, proved rather than asserted.
/// <para>FU07 puts the scope into the uniqueness key, which normally means a data migration. It does not here, and this
/// file is the reason it does not. Rows written by FU06 carry no <c>ScopeType</c>; the field is DERIVED on read (no
/// business unit → tenant, a business unit → business-unit), which maps FU06's two scopes one-to-one onto two of
/// FU07's four. The new levels occupy a disjoint part of the key space, so <b>no existing row can gain or lose a
/// collision</b> — and every row keeps resolving exactly as it did.</para>
/// <para>Every legacy row in this file is written STRAIGHT INTO the repository with no <c>ScopeType</c>, which is
/// precisely what Mongo hands back for a document written before the field existed.</para>
/// </summary>
public sealed class CyclePeriodMigrationCompatTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LegalEntityX = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Mar1 = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Apr30 = new(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);
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

    private sealed class AllowAllReferences : IReferenceDataValidator
    {
        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(new ReferenceValidationResult(ReferenceValidationStatus.Valid, setCode, value));
    }

    private sealed class AllowAllLegalEntities : ICyclePeriodLegalEntityValidator
    {
        public Task<CyclePeriodLegalEntityValidation> ValidateAsync(Guid legalEntityId, CancellationToken ct)
            => Task.FromResult(CyclePeriodLegalEntityValidation.Valid);
    }

    private sealed class NoTerritoryPlans : ITerritoryBusinessUnitCatalog
    {
        public Task<IReadOnlyList<TerritoryBusinessUnitCandidate>> GetCandidatesAsync(
            string? country, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TerritoryBusinessUnitCandidate>>(
                Array.Empty<TerritoryBusinessUnitCandidate>());
    }

    private static CyclePeriodScopeWriteValidator Gate()
        => new(new AllowAllReferences(), new AllowAllLegalEntities(), new NoTerritoryPlans());

    /// <summary>A row exactly as FU06 wrote it: no ScopeType, and BusinessUnitId carrying the whole scope.</summary>
    private static PeriodEntity Legacy(
        string code, int sequence, string? businessUnitId,
        string status = CyclePeriodStatuses.Draft,
        DateTimeOffset? start = null, DateTimeOffset? end = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            CycleCode = code,
            CycleName = "legacy " + code,
            Year = 2026,
            SequenceInYear = sequence,
            StartDate = start ?? Mar1,
            EndDate = end ?? Apr30,
            BusinessUnitId = businessUnitId,
            CycleStatus = status,
            // ScopeType deliberately left at its default empty string — the field did not exist when this was written.
            ScopeType = string.Empty
        };

    // ── the derivation ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_Legacy_Row_Without_A_Business_Unit_Derives_The_Tenant_Scope()
    {
        var row = Legacy("c-1", 1, null);

        Assert.Equal(CyclePeriodScopeTypes.Tenant, row.EffectiveScopeType());
        Assert.Null(row.ScopeRef());
        Assert.True(row.HasConsistentScope());
    }

    [Fact]
    public void A_Legacy_Row_With_A_Business_Unit_Derives_The_Business_Unit_Scope()
    {
        var row = Legacy("c-1", 1, "rx");

        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, row.EffectiveScopeType());
        Assert.Equal("rx", row.ScopeRef());
        Assert.True(row.HasConsistentScope());
    }

    [Fact]
    public void The_Derivation_Never_Invents_A_Country_Or_Legal_Entity_Scope()
    {
        // The new levels are unreachable by derivation, which is what keeps their key space disjoint from the old one.
        foreach (var businessUnit in new string?[] { null, "rx", "otc" })
        {
            var derived = Legacy("c", 1, businessUnit).EffectiveScopeType();
            Assert.NotEqual(CyclePeriodScopeTypes.Country, derived);
            Assert.NotEqual(CyclePeriodScopeTypes.LegalEntity, derived);
        }
    }

    [Fact]
    public void Reading_A_Legacy_Row_Writes_Nothing_Back()
    {
        // EnsureScopeType stamps the in-memory instance only. There is no backfill, and Mongo is never touched.
        var row = Legacy("c-1", 1, "rx");
        Assert.Equal(string.Empty, row.ScopeType);

        var derivedTwice = row.EffectiveScopeType();
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, derivedTwice);
        Assert.Equal(string.Empty, row.ScopeType);
    }

    [Fact]
    public void EnsureScopeType_Is_Idempotent()
    {
        var row = Legacy("c-1", 1, "rx");

        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, row.EnsureScopeType().ScopeType);
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, row.EnsureScopeType().ScopeType);
    }

    // ── the key mapping is one-to-one and onto ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Two_Different_Fu06_Scopes_Never_Collapse_Into_One_Fu07_Scope()
    {
        var tenantWide = Legacy("c-1", 1, null);
        var rx = Legacy("c-2", 1, "rx");
        var otc = Legacy("c-3", 1, "otc");

        var keys = new[] { tenantWide, rx, otc }
            .Select(p => (p.EffectiveScopeType(), p.ScopeRef()))
            .ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void One_Fu06_Scope_Never_Splits_Into_Two_Fu07_Scopes()
    {
        var a = Legacy("c-1", 1, "rx");
        var b = Legacy("c-2", 2, " RX ");

        Assert.True(CyclePeriodOverlapRules.IsAtScope(b, a.EffectiveScopeType(), a.ScopeRef()));
    }

    [Fact]
    public async Task A_Legacy_Row_Keeps_Its_Sequence_Collision()
    {
        var repo = new FakeRepo();
        repo.Items.Add(Legacy("c-1", 1, "rx"));

        var r = await new CreateCyclePeriodHandler(Tenant(TenantA), new NullActorContext(), repo, Gate()).Handle(
            new CreateCyclePeriodCommand(
                "c-2", "new", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.BusinessUnit, null, null, "rx", null),
            default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.SequenceTaken, r.Errors!);
    }

    [Fact]
    public async Task A_Legacy_Row_Gains_No_New_Collision_From_The_New_Levels()
    {
        // The same (year, sequence) at a level FU06 could not express is free — a disjoint part of the key space.
        var repo = new FakeRepo();
        repo.Items.Add(Legacy("c-1", 1, "rx"));
        repo.Items.Add(Legacy("c-2", 1, null));

        var handler = new CreateCyclePeriodHandler(Tenant(TenantA), new NullActorContext(), repo, Gate());

        var country = await handler.Handle(
            new CreateCyclePeriodCommand(
                "c-3", "tr", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.Country, "TR", null, null, null),
            default);
        Assert.Equal(201, country.StatusCode);

        var legalEntity = await handler.Handle(
            new CreateCyclePeriodCommand(
                "c-4", "le", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.LegalEntity, null, LegalEntityX, null, null),
            default);
        Assert.Equal(201, legalEntity.StatusCode);
    }

    [Fact]
    public async Task A_Legacy_Row_Loses_No_Existing_Collision()
    {
        var repo = new FakeRepo();
        repo.Items.Add(Legacy("c-1", 1, null));

        var r = await new CreateCyclePeriodHandler(Tenant(TenantA), new NullActorContext(), repo, Gate()).Handle(
            new CreateCyclePeriodCommand(
                "c-2", "new", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.Tenant, null, null, null, null),
            default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.SequenceTaken, r.Errors!);
    }

    // ── behaviour of a legacy row is unchanged ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Legacy_Row_Still_Blocks_An_Overlapping_Activation_In_Its_Own_Scope()
    {
        var repo = new FakeRepo();
        repo.Items.Add(Legacy("c-1", 1, "rx", CyclePeriodStatuses.Active));
        var second = Legacy("c-2", 2, "rx", CyclePeriodStatuses.Draft, Apr30, Jun30);
        repo.Items.Add(second);

        var r = await new ActivateCyclePeriodHandler(Tenant(TenantA), new NullActorContext(), repo).Handle(
            new ActivateCyclePeriodCommand(second.Id, null), default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.Overlap, r.Errors!);
    }

    [Fact]
    public async Task A_Legacy_Row_Does_Not_Block_A_New_Level()
    {
        var repo = new FakeRepo();
        repo.Items.Add(Legacy("c-1", 1, null, CyclePeriodStatuses.Active));

        var created = await new CreateCyclePeriodHandler(Tenant(TenantA), new NullActorContext(), repo, Gate()).Handle(
            new CreateCyclePeriodCommand(
                "c-2", "tr", 2026, 2, Mar1, Apr30,
                CyclePeriodScopeTypes.Country, "TR", null, null, null),
            default);

        var r = await new ActivateCyclePeriodHandler(Tenant(TenantA), new NullActorContext(), repo).Handle(
            new ActivateCyclePeriodCommand(created.Data, null), default);

        Assert.Equal(200, r.StatusCode);
    }

    [Fact]
    public async Task A_Legacy_Row_Resolves_Exactly_As_It_Did_Under_Fu06()
    {
        var repo = new FakeRepo();
        repo.Items.Add(Legacy("c-tenant", 1, null, CyclePeriodStatuses.Active));
        repo.Items.Add(Legacy("c-rx", 2, "rx", CyclePeriodStatuses.Active));

        var resolve = new ResolveActiveCyclePeriodHandler(
            Tenant(TenantA), new CyclePeriodReader(Tenant(TenantA), repo));

        var scoped = await resolve.Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, "rx"), default);
        Assert.Equal("c-rx", scoped.Data!.Period!.CycleCode);
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, scoped.Data.ResolvedScopeType);

        var fallback = await resolve.Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, "otc"), default);
        Assert.Equal("c-tenant", fallback.Data!.Period!.CycleCode);

        var unscoped = await resolve.Handle(
            new ResolveActiveCyclePeriodQuery(Mar1.AddDays(5), null, null, null), default);
        Assert.Equal("c-tenant", unscoped.Data!.Period!.CycleCode);
    }

    [Fact]
    public async Task A_Legacy_Row_Is_Presented_With_The_Scope_It_Always_Had()
    {
        var repo = new FakeRepo();
        repo.Items.Add(Legacy("c-rx", 1, "rx"));
        repo.Items.Add(Legacy("c-tenant", 2, null));

        var r = await new GetCyclePeriodListHandler(Tenant(TenantA), repo).Handle(
            new GetCyclePeriodListQuery(null, null, null, null, null, null, null, null, null), default);

        var rx = r.Data!.Items.Single(i => i.CycleCode == "c-rx");
        var tenantWide = r.Data.Items.Single(i => i.CycleCode == "c-tenant");

        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, rx.ScopeType);
        Assert.Equal("rx", rx.ScopeRef);
        Assert.Equal(CyclePeriodScopeTypes.Tenant, tenantWide.ScopeType);
        Assert.Null(tenantWide.ScopeRef);
    }

    [Fact]
    public async Task Editing_A_Legacy_Row_Persists_The_Scope_It_Always_Had()
    {
        // The derivation becomes permanent one row at a time, as a side effect of ordinary work — never as a batch job.
        var repo = new FakeRepo();
        var legacy = Legacy("c-1", 1, "rx");
        repo.Items.Add(legacy);

        var r = await new UpdateCyclePeriodHandler(Tenant(TenantA), new NullActorContext(), repo, Gate()).Handle(
            new UpdateCyclePeriodCommand(
                legacy.Id, "renamed", 2026, 1, Mar1, Apr30, null, null, null, "rx", null, null),
            default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(CyclePeriodScopeTypes.BusinessUnit, repo.Items.Single().ScopeType);
    }

    [Fact]
    public async Task Activating_A_Legacy_Row_Persists_The_Scope_It_Always_Had()
    {
        var repo = new FakeRepo();
        var legacy = Legacy("c-1", 1, null);
        repo.Items.Add(legacy);

        var r = await new ActivateCyclePeriodHandler(Tenant(TenantA), new NullActorContext(), repo).Handle(
            new ActivateCyclePeriodCommand(legacy.Id, null), default);

        Assert.Equal(200, r.StatusCode);
        Assert.Equal(CyclePeriodScopeTypes.Tenant, repo.Items.Single().ScopeType);
    }

    [Fact]
    public async Task A_Legacy_Row_Cannot_Be_Moved_To_Another_Level()
    {
        var repo = new FakeRepo();
        var legacy = Legacy("c-1", 1, "rx");
        repo.Items.Add(legacy);

        var r = await new UpdateCyclePeriodHandler(Tenant(TenantA), new NullActorContext(), repo, Gate()).Handle(
            new UpdateCyclePeriodCommand(
                legacy.Id, "c-1", 2026, 1, Mar1, Apr30,
                CyclePeriodScopeTypes.Country, "TR", null, null, null, null),
            default);

        Assert.Equal(409, r.StatusCode);
        Assert.Contains(CyclePeriodErrorCodes.ScopeImmutable, r.Errors!);
    }
}
