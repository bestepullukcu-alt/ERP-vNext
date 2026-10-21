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
    public async Task StockSnapshotInLaterWeek_StartsFromThatIsoWeek()
    {
        var sources = CreateSources();
        sources.Stock.Value = sources.Stock.Value! with
        {
            AsOf = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)
        };

        var result = await Run(sources);

        Assert.Equal(MrpCalculationStatus.Found, result.Status);
        Assert.NotNull(result.Plan);
        Assert.Equal(51, result.Plan.Weeks.Count);
        Assert.Equal(new DateOnly(2026, 10, 26), result.Plan.Weeks[0].WeekStart);
        Assert.Equal(2, sources.Demand.Reads);
        Assert.Equal(new DateOnly(2026, 10, 26), sources.LeadTime.LastHorizonStart);
    }

    [Fact]
    public async Task StockSnapshotWithinFirstWeek_KeepsFirstIsoWeek()
    {
        var sources = CreateSources();
        sources.Stock.Value = sources.Stock.Value! with
        {
            AsOf = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero)
        };

        var result = await Run(sources);

        Assert.Equal(MrpCalculationStatus.Found, result.Status);
        Assert.Equal(new DateOnly(2026, 10, 19), result.Plan!.Weeks[0].WeekStart);
        Assert.Equal(52, result.Plan.Weeks.Count);
    }

    [Fact]
    public async Task StockSnapshotAfterPublishedHorizon_ProducesNoPlan()
    {
        var sources = CreateSources();
        sources.Stock.Value = sources.Stock.Value! with
        {
            AsOf = new DateTimeOffset(2027, 10, 19, 12, 0, 0, TimeSpan.Zero)
        };

        var result = await Run(sources);

        Assert.Equal(MrpCalculationStatus.Unavailable, result.Status);
        Assert.Null(result.Plan);
    }

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
        return new(new(company, demand, stock, leadTime), company, demand, stock, leadTime);
    }

    private sealed record Sources(MrpLiveCalculation Service, CompanyReader Company,
        DemandReader Demand, StockReader Stock, LeadTimeReader LeadTime);

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
        public DateOnly? LastHorizonStart { get; private set; }
        public Task<MrpApprovedLeadTime?> ReadAsync(Guid tenantId, Guid legalEntityId,
            Guid skuId, Guid warehouseId, DateOnly start, DateOnly end, CancellationToken ct)
        {
            LastHorizonStart = start;
            return Task.FromResult(Value);
        }
    }
}
