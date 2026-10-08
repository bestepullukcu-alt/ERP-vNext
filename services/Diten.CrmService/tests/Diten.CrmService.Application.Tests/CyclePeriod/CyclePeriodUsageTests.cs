using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CyclePeriod;
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
/// WP-CYC-UI-1 — <c>GET /api/crm/cycle-periods/{id}/usage</c> and the list's batch usage summary.
/// <para>Sabotage target: drop the cancelled filter in <see cref="CyclePeriodUsageRules.CountsAsDemand"/> →
/// <see cref="CU02_Demand_By_Month_Excludes_Cancelled_And_Archived_Visits"/> and
/// <see cref="CU06_List_Rows_Carry_A_Batch_Usage_Summary"/> go red.</para>
/// </summary>
public sealed class CyclePeriodUsageTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OwnerUser = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

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
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(Scope(tenantId).Where(x => x.CycleCode == cycleCode).ToList());

        public Task<IReadOnlyList<PeriodEntity>> ListByYearAsync(Guid tenantId, int year, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(Scope(tenantId).Where(x => x.Year == year).ToList());

        public Task<IReadOnlyList<PeriodEntity>> ListActiveAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PeriodEntity>>(Scope(tenantId).Where(x => x.IsActive()).ToList());

        public Task InsertAsync(PeriodEntity entity, CancellationToken ct) => throw new InvalidOperationException("read only");

        public Task<bool> ReplaceAsync(PeriodEntity entity, int expectedVersion, CancellationToken ct)
            => throw new InvalidOperationException("read only");
    }

    /// <summary>Tenant-keyed rows, filtered exactly as the Mongo reader filters: by tenant, then by period.</summary>
    private sealed class FakeUsageReader : ICyclePeriodUsageReader
    {
        public Dictionary<Guid, CyclePeriodUsageSnapshot> ByTenant { get; } = new();
        public int Calls { get; private set; }
        public bool Throw { get; set; }

        public Task<CyclePeriodUsageSnapshot> ReadAsync(
            Guid tenantId, IReadOnlyCollection<Guid> cyclePeriodIds, CancellationToken ct)
        {
            Calls++;
            if (Throw)
            {
                throw new TimeoutException("mongo down");
            }

            if (!ByTenant.TryGetValue(tenantId, out var all))
            {
                return Task.FromResult(CyclePeriodUsageSnapshot.Empty);
            }

            var ids = cyclePeriodIds.ToHashSet();
            var sessions = all.Sessions.Where(s => ids.Contains(s.CyclePeriodId)).ToList();
            var visitIds = sessions.SelectMany(s => s.CommittedPlannedVisitIds).ToHashSet();
            return Task.FromResult(new CyclePeriodUsageSnapshot(
                all.Capacities.Where(c => ids.Contains(c.CyclePeriodId)).ToList(),
                all.Campaigns.Where(c => ids.Contains(c.CyclePeriodId)).ToList(),
                sessions,
                all.Visits.Where(v => visitIds.Contains(v.PlannedVisitId)).ToList()));
        }
    }

    private sealed class FakeNames : IUserDisplayNameResolver
    {
        public List<Guid> Asked { get; } = new();

        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
        {
            Asked.AddRange(userIds);
            IReadOnlyDictionary<Guid, string> map = userIds.Where(id => id == OwnerUser)
                .ToDictionary(id => id, _ => "Ayşe Yılmaz");
            return Task.FromResult(map);
        }
    }

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private static PeriodEntity Period(Guid tenantId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TenantId = tenantId,
        CycleCode = $"p-{Guid.NewGuid():N}",
        CycleName = "period",
        Year = 2026,
        SequenceInYear = 4,
        ScopeType = CyclePeriodScopeTypes.Tenant,
        CycleStatus = CyclePeriodStatuses.Active
    };

    private static CyclePeriodUsageVisitRow Visit(string status, int year, int month, int day = 10)
        => new(Guid.NewGuid(), status, new DateOnly(year, month, day));

    /// <summary>A period with a live capacity, two campaigns, two sessions and seven committed visits.</summary>
    private static (Guid PeriodId, CyclePeriodUsageSnapshot Snapshot) Fixture()
    {
        var periodId = Guid.NewGuid();
        var oct = new[]
        {
            Visit(PlannedVisitStatus.Planned, 2026, 10), Visit(PlannedVisitStatus.Confirmed, 2026, 10),
            Visit(PlannedVisitStatus.Cancelled, 2026, 10)
        };
        var nov = new[]
        {
            Visit(PlannedVisitStatus.Draft, 2026, 11), Visit(PlannedVisitStatus.Archived, 2026, 11),
            Visit(PlannedVisitStatus.Planned, 2026, 11)
        };
        var dec = new[] { Visit(PlannedVisitStatus.Cancelled, 2026, 12) };

        var sessionA = new CyclePeriodUsageSessionRow(
            Guid.NewGuid(), periodId, OwnerUser.ToString("D"), PlanningSessionStatus.Committed, "2026-10-05",
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            oct.Concat(dec).Select(v => v.PlannedVisitId).ToList());
        var sessionB = new CyclePeriodUsageSessionRow(
            Guid.NewGuid(), periodId, "rep-not-a-guid", PlanningSessionStatus.Committed, null,
            new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero),
            // One id shared with session A: a visit is counted ONCE for the period.
            nov.Select(v => v.PlannedVisitId).Append(oct[0].PlannedVisitId).ToList());

        return (periodId, new CyclePeriodUsageSnapshot(
            new[]
            {
                new CyclePeriodUsageCapacityRow(Guid.NewGuid(), periodId, IsArchived: true),
                new CyclePeriodUsageCapacityRow(Guid.NewGuid(), periodId, IsArchived: false)
            },
            new[]
            {
                new CyclePeriodUsageCampaignRow(Guid.NewGuid(), periodId, "ONK-Q4", "Onkoloji Q4", "active"),
                new CyclePeriodUsageCampaignRow(Guid.NewGuid(), periodId, "KAR-Q4", "Kardiyoloji", "draft")
            },
            new[] { sessionA, sessionB },
            oct.Concat(nov).Concat(dec).ToList()));
    }

    private static (GetCyclePeriodUsageHandler Handler, FakeRepo Repo, FakeUsageReader Usage, FakeNames Names)
        Build(Guid tenantId)
    {
        var repo = new FakeRepo();
        var usage = new FakeUsageReader();
        var names = new FakeNames();
        return (new GetCyclePeriodUsageHandler(Tenant(tenantId), repo, usage, names), repo, usage, names);
    }

    [Fact]
    public async Task CU01_Usage_Counts_Campaigns_Sessions_And_Visits()
    {
        var (handler, repo, usage, _) = Build(TenantA);
        var (periodId, snapshot) = Fixture();
        repo.Items.Add(Period(TenantA, periodId));
        usage.ByTenant[TenantA] = snapshot;

        var response = await handler.Handle(new GetCyclePeriodUsageQuery(periodId), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var dto = response.Data!;
        Assert.NotNull(dto.Capacity);
        Assert.False(dto.Capacity!.IsArchived);                // the live capacity wins over the archived one
        Assert.Equal(new[] { "KAR-Q4", "ONK-Q4" }, dto.Campaigns.Select(c => c.Code));
        Assert.Equal(2, dto.PlanningSessions.Count);
        Assert.Equal(7, dto.PlannedVisits.Total);              // 4 + 4 ids, one shared → 7 distinct
        Assert.Equal(2, dto.PlannedVisits.ByStatus[PlannedVisitStatus.Cancelled]);
        Assert.Equal(2, dto.PlannedVisits.ByStatus[PlannedVisitStatus.Planned]);
        Assert.Equal(1, dto.PlannedVisits.ByStatus[PlannedVisitStatus.Archived]);
        Assert.Equal(new[] { 4, 4 }, dto.PlanningSessions.OrderBy(s => s.Name).Select(s => s.CommittedVisitCount));
    }

    [Fact]
    public async Task CU02_Demand_By_Month_Excludes_Cancelled_And_Archived_Visits()
    {
        var (handler, repo, usage, _) = Build(TenantA);
        var (periodId, snapshot) = Fixture();
        repo.Items.Add(Period(TenantA, periodId));
        usage.ByTenant[TenantA] = snapshot;

        var dto = (await handler.Handle(new GetCyclePeriodUsageQuery(periodId), CancellationToken.None)).Data!;

        // Oct: planned + confirmed (cancelled out) · Nov: draft + planned (archived out) · Dec: only a cancelled → absent.
        Assert.Equal(
            new[] { (2026, 10, 2), (2026, 11, 2) },
            dto.DemandByMonth.Select(d => (d.Year, d.Month, d.PlannedVisits)));
    }

    [Fact]
    public async Task CU03_Another_Tenants_Period_Is_404_And_Its_Rows_Never_Leak()
    {
        var (handler, repo, usage, _) = Build(TenantA);
        var (periodId, snapshot) = Fixture();
        repo.Items.Add(Period(TenantB, periodId));      // the period belongs to tenant B
        usage.ByTenant[TenantB] = snapshot;

        var response = await handler.Handle(new GetCyclePeriodUsageQuery(periodId), CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
        Assert.Equal(0, usage.Calls);                    // nothing about B was even asked for

        // Same id in tenant A, but B's rows are B's: A sees an empty usage.
        repo.Items.Add(Period(TenantA, periodId));
        var own = (await handler.Handle(new GetCyclePeriodUsageQuery(periodId), CancellationToken.None)).Data!;
        Assert.Null(own.Capacity);
        Assert.Empty(own.Campaigns);
        Assert.Equal(0, own.PlannedVisits.Total);
    }

    [Fact]
    public async Task CU04_No_Personal_Data_Only_The_Owner_Display_Name()
    {
        var (handler, repo, usage, names) = Build(TenantA);
        var (periodId, snapshot) = Fixture();
        repo.Items.Add(Period(TenantA, periodId));
        usage.ByTenant[TenantA] = snapshot;

        var dto = (await handler.Handle(new GetCyclePeriodUsageQuery(periodId), CancellationToken.None)).Data!;

        Assert.Equal(new[] { OwnerUser }, names.Asked);  // a non-Guid resource id is never sent to the resolver
        var sessions = dto.PlanningSessions.OrderBy(s => s.Name).ToList();
        Assert.Equal("2026-10-02", sessions[0].Name);      // no plan week → creation day
        Assert.Null(sessions[0].OwnerDisplayName);
        Assert.Equal("2026-10-05", sessions[1].Name);
        Assert.Equal("Ayşe Yılmaz", sessions[1].OwnerDisplayName);

        // The answer's shape carries no target, contact, account or resource identifier.
        var forbidden = new[] { "Contact", "Account", "Target", "ResourceId", "Email", "Phone" };
        foreach (var type in new[]
                 {
                     typeof(CyclePeriodUsageDto), typeof(CyclePeriodUsageSessionDto), typeof(CyclePeriodUsageCampaignDto),
                     typeof(CyclePeriodUsageVisitsDto), typeof(CyclePeriodUsageDemandMonthDto), typeof(CyclePeriodUsageCapacityDto)
                 })
        {
            Assert.DoesNotContain(type.GetProperties(), p => forbidden.Any(f => p.Name.Contains(f, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public async Task CU05_An_Unused_Period_Answers_Empty_Not_Null()
    {
        var (handler, repo, _, _) = Build(TenantA);
        var period = Period(TenantA);
        repo.Items.Add(period);

        var dto = (await handler.Handle(new GetCyclePeriodUsageQuery(period.Id), CancellationToken.None)).Data!;

        Assert.Null(dto.Capacity);
        Assert.Empty(dto.Campaigns);
        Assert.Empty(dto.PlanningSessions);
        Assert.Equal(0, dto.PlannedVisits.Total);
        Assert.Empty(dto.PlannedVisits.ByStatus);
        Assert.Empty(dto.DemandByMonth);
    }

    [Fact]
    public async Task CU06_List_Rows_Carry_A_Batch_Usage_Summary()
    {
        var repo = new FakeRepo();
        var usage = new FakeUsageReader();
        var (periodId, snapshot) = Fixture();
        var used = Period(TenantA, periodId);
        var unused = Period(TenantA);
        unused.SequenceInYear = 3;
        repo.Items.AddRange(new[] { used, unused });
        usage.ByTenant[TenantA] = snapshot;

        var list = (await new GetCyclePeriodListHandler(Tenant(TenantA), repo, usage).Handle(
            new GetCyclePeriodListQuery(null, null, null, null, null, null, null, null, null), CancellationToken.None)).Data!;

        Assert.Equal(1, usage.Calls);                    // ONE read for the whole page
        var row = list.Items.Single(i => i.CyclePeriodId == periodId);
        Assert.True(row.HasCapacity);
        Assert.Equal(2, row.CampaignCount);
        Assert.Equal(4, row.PlannedVisitCount);         // 7 visits − 2 cancelled − 1 archived
        var empty = list.Items.Single(i => i.CyclePeriodId == unused.Id);
        Assert.Equal((false, 0, 0), (empty.HasCapacity!.Value, empty.CampaignCount!.Value, empty.PlannedVisitCount!.Value));
    }

    [Fact]
    public async Task CU07_A_Failed_Usage_Read_Leaves_The_List_Summary_Unknown()
    {
        var repo = new FakeRepo();
        repo.Items.Add(Period(TenantA));
        var usage = new FakeUsageReader { Throw = true };

        var response = await new GetCyclePeriodListHandler(Tenant(TenantA), repo, usage).Handle(
            new GetCyclePeriodListQuery(null, null, null, null, null, null, null, null, null), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var row = Assert.Single(response.Data!.Items);
        Assert.Null(row.HasCapacity);
        Assert.Null(row.PlannedVisitCount);
    }

    [Fact]
    public void CU08_Only_Archived_Capacity_Is_Reported_As_Archived_And_Not_Counted_As_Present()
    {
        var periodId = Guid.NewGuid();
        var snapshot = CyclePeriodUsageSnapshot.Empty with
        {
            Capacities = new[] { new CyclePeriodUsageCapacityRow(Guid.NewGuid(), periodId, IsArchived: true) }
        };

        var dto = CyclePeriodUsageRules.Build(periodId, snapshot, new Dictionary<Guid, string>());

        Assert.True(dto.Capacity!.IsArchived);
        Assert.False(CyclePeriodUsageRules.Summarize(periodId, snapshot).HasCapacity);
    }
}
