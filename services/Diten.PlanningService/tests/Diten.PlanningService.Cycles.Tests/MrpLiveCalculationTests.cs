using Diten.PlanningService.Application.Features.Mrp;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class MrpLiveCalculationTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _company = Guid.NewGuid();
    private readonly Guid _sku = Guid.NewGuid();
    private readonly Guid _warehouse = Guid.NewGuid();

    [Fact]
    public async Task VerifiedSources_CalculateWithBothDegradedReasons()
    {
        var sources = CreateSources();
        var result = await sources.Service.CalculateAsync(_tenant, _company, "2026-Q4",
            _sku, _warehouse, CancellationToken.None);
        Assert.Equal(MrpCalculationStatus.Found, result.Status);
        Assert.NotNull(result.Plan);
        Assert.Equal(52, result.Plan.Weeks.Count);
        Assert.Contains(MrpNetting.OpenOrdersUnavailable, result.Plan.DegradedReason);
        Assert.Contains(MrpNetting.SafetyStockUnavailable, result.Plan.DegradedReason);
        Assert.Equal("checksum", result.Plan.DemandChecksum);
        Assert.Equal(sources.Stock.Value!.AsOf, result.Plan.InventoryAsOf);
        Assert.Equal(3m, result.Plan.Weeks[0].ProposedLotForLotQuantity);
        Assert.All(result.Plan.Weeks, week => Assert.Equal(0m, week.ApprovedSafetyStock));
    }

    [Fact]
    public async Task InactiveCompany_DoesNotReadBusinessSources()
    {
        var sources = CreateSources();
        sources.Company.Result = LegalEntityReferenceResult.NotFound;
        var result = await sources.Service.CalculateAsync(_tenant, _company, "2026-Q4",
            _sku, _warehouse, CancellationToken.None);
        Assert.Equal(MrpCalculationStatus.Forbidden, result.Status);
        Assert.Equal(0, sources.Demand.Reads);
        Assert.Equal(0, sources.Stock.Reads);
    }

    [Fact]
    public async Task DemandOrInventoryOrLeadTimeUnavailable_ProducesNoPlan()
    {
        var sources = CreateSources();
        sources.Demand.Value = null;
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
        sources = CreateSources();
        sources.Stock.Value = null;
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
        sources = CreateSources();
        sources.LeadTime.Value = null;
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
    }

    [Fact]
    public async Task MismatchedInventoryScopeOrUnit_ProducesNoPlan()
    {
        var sources = CreateSources();
        sources.Stock.Value = sources.Stock.Value! with { WarehouseId = Guid.NewGuid() };
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
        sources = CreateSources();
        sources.Stock.Value = sources.Stock.Value! with { BaseUomId = "BOX" };
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
    }

    [Fact]
    public async Task MismatchedPublishedDemandScope_ProducesNoPlan()
    {
        var sources = CreateSources();
        sources.Demand.Value = sources.Demand.Value! with { TenantId = Guid.NewGuid() };
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
    }

    [Fact]
    public async Task ReplacedDemandDuringInventoryRead_ProducesNoPlan()
    {
        var sources = CreateSources();
        sources.Demand.ValueAfterFirstRead = sources.Demand.Value! with
        {
            RevisionId = Guid.NewGuid()
        };
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
        Assert.Equal(2, sources.Demand.Reads);
    }

    [Fact]
    public async Task WeeklySafetyStock_UsesEachApprovedRevision_AndMarksOnlyMissingWeek()
    {
        var sources = CreateSources();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var start = sources.Demand.Value!.Weeks[0].WeekStart;
        sources.Safety.Values[start] = Approved(first, start, 2m);
        sources.Safety.Values[start.AddDays(7)] = Approved(second, start.AddDays(7), 4m);

        var result = await Run(sources);

        Assert.Equal(MrpCalculationStatus.Found, result.Status);
        Assert.Equal(2m, result.Plan!.Weeks[0].ApprovedSafetyStock);
        Assert.Equal(first.ToString("D"), result.Plan.Weeks[0].SafetyEvidence!.PolicyRevisionId);
        Assert.Equal(4m, result.Plan.Weeks[1].ApprovedSafetyStock);
        Assert.Equal(second.ToString("D"), result.Plan.Weeks[1].SafetyEvidence!.PolicyRevisionId);
        Assert.False(result.Plan.Weeks[2].SafetyEvidence!.Found);
        Assert.Null(result.Plan.Weeks[0].DegradedReason);
        Assert.Null(result.Plan.Weeks[1].DegradedReason);
        Assert.Equal(MrpNetting.SafetyStockUnavailable, result.Plan.Weeks[2].DegradedReason);
        Assert.Contains(MrpNetting.SafetyStockUnavailable, result.Plan.DegradedReason);
    }

    [Fact]
    public async Task EveryWeekHasApprovedSafetyStock_LeavesOnlyOpenOrdersDegraded()
    {
        var sources = CreateSources();
        foreach (var week in sources.Demand.Value!.Weeks)
            sources.Safety.Values[week.WeekStart] = Approved(Guid.NewGuid(), week.WeekStart, 1m);

        var result = await Run(sources);

        Assert.Equal(MrpCalculationStatus.Found, result.Status);
        Assert.Equal(MrpNetting.OpenOrdersUnavailable, result.Plan!.DegradedReason);
        Assert.All(result.Plan.Weeks, week =>
        {
            Assert.Equal(1m, week.ApprovedSafetyStock);
            Assert.True(week.SafetyEvidence!.Found);
            Assert.Null(week.DegradedReason);
        });
    }

    [Fact]
    public async Task SafetyStockServiceFailure_OrUnitMismatch_ProducesNoPlan()
    {
        var sources = CreateSources();
        sources.Safety.Unavailable = true;
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
        sources = CreateSources();
        var start = sources.Demand.Value!.Weeks[0].WeekStart;
        sources.Safety.Values[start] = Approved(Guid.NewGuid(), start, 2m) with
        {
            BaseUomId = "BOX"
        };
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
    }

    [Fact]
    public async Task SafetyStockChangesDuringCalculation_ProducesNoPlan()
    {
        var sources = CreateSources();
        var start = sources.Demand.Value!.Weeks[0].WeekStart;
        sources.Safety.Values[start] = Approved(Guid.NewGuid(), start, 2m);
        sources.Safety.ChangeOnSecondPass = true;
        Assert.Equal(MrpCalculationStatus.Unavailable, (await Run(sources)).Status);
    }

    private static MrpApprovedSafetyStock Approved(Guid revision, DateOnly week, decimal quantity) =>
        new(quantity, "EA", revision, week, week.AddDays(6), 1, "Manual");

    private Task<MrpCalculationResult> Run(Sources sources) =>
        sources.Service.CalculateAsync(_tenant, _company, "2026-Q4", _sku,
            _warehouse, CancellationToken.None);

    private Sources CreateSources()
    {
        var start = new DateOnly(2026, 10, 19);
        var company = new CompanyReader();
        var demand = new DemandReader
        {
            Value = new(_tenant, _company, Guid.NewGuid(), 2, "checksum", _sku,
                _warehouse, "EA", Enumerable.Range(0, 52)
                    .Select(i => new MrpDemandWeek(start.AddDays(7 * i), 5m)).ToArray())
        };
        var stock = new StockReader
        {
            Value = new(_tenant, _company, _sku, _warehouse, "EA", 2m,
                new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero))
        };
        var leadTime = new LeadTimeReader
        {
            Value = new(_tenant, _company, _sku, _warehouse, Guid.NewGuid().ToString("D"),
                5, start, null)
        };
        var safety = new SafetyReader();
        return new(new(company, demand, stock, leadTime, safety), company, demand, stock,
            leadTime, safety);
    }

    private sealed record Sources(MrpLiveCalculation Service, CompanyReader Company,
        DemandReader Demand, StockReader Stock, LeadTimeReader LeadTime, SafetyReader Safety);

    private sealed class SafetyReader : IMrpApprovedSafetyStockReader
    {
        public Dictionary<DateOnly, MrpApprovedSafetyStock> Values { get; } = [];
        public bool Unavailable { get; set; }
        public bool ChangeOnSecondPass { get; set; }
        private int _reads;

        public Task<MrpSafetyStockRead> ReadAsync(Guid tenantId, Guid legalEntityId,
            Guid skuId, Guid warehouseId, DateOnly asOf, CancellationToken ct)
        {
            _reads++;
            if (Unavailable) return Task.FromResult(new MrpSafetyStockRead(MrpSafetyStockStatus.Unavailable));
            if (!Values.TryGetValue(asOf, out var value))
                return Task.FromResult(new MrpSafetyStockRead(MrpSafetyStockStatus.NotFound));
            if (ChangeOnSecondPass && _reads > 52)
                value = value with { RecordVersion = 2 };
            return Task.FromResult(new MrpSafetyStockRead(MrpSafetyStockStatus.Found, value));
        }
    }

    private sealed class CompanyReader : ILegalEntityScopeReader
    {
        public LegalEntityReferenceResult Result { get; set; } = LegalEntityReferenceResult.Active;
        public Task<LegalEntityReferenceResult> ValidateAsync(Guid tenantId, Guid legalEntityId,
            CancellationToken ct) => Task.FromResult(Result);
        public Task<IReadOnlyList<Guid>?> ListActiveAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Guid>?>([]);
    }

    private sealed class DemandReader : IMrpPublishedDemandReader
    {
        public MrpPublishedDemand? Value { get; set; }
        public MrpPublishedDemand? ValueAfterFirstRead { get; set; }
        public int Reads { get; private set; }
        public Task<MrpPublishedDemand?> ReadAsync(Guid tenantId, Guid legalEntityId,
            string period, Guid skuId, Guid warehouseId, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult(Reads > 1 ? ValueAfterFirstRead ?? Value : Value);
        }
    }

    private sealed class StockReader : IMrpInventoryReader
    {
        public MrpAvailableStock? Value { get; set; }
        public int Reads { get; private set; }
        public Task<MrpAvailableStock?> ReadAsync(Guid tenantId, Guid legalEntityId,
            Guid skuId, Guid warehouseId, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult(Value);
        }
    }

    private sealed class LeadTimeReader : IMrpApprovedLeadTimeReader
    {
        public MrpApprovedLeadTime? Value { get; set; }
        public Task<MrpApprovedLeadTime?> ReadAsync(Guid tenantId, Guid legalEntityId,
            Guid skuId, Guid warehouseId, DateOnly start, DateOnly end, CancellationToken ct) =>
            Task.FromResult(Value);
    }
}
