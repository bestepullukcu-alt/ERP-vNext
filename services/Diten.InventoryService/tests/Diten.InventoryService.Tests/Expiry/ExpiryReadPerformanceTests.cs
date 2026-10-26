using System.Diagnostics;
using System.Globalization;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F4 item 6 — panel and lists with 1.000 lots in one company: p95 ≤ 500 ms per read through the real endpoint (and the FEFO order of
/// that SKU). A performance test (F3 pattern): runs only with <c>INVENTORY_PERF_TESTS=1</c>, alone, in the non-parallel collection.
/// </summary>
[Collection(ExpiryPerformanceCollection.Name)]
public sealed class ExpiryReadPerformanceTests
{
    private const int Lots = 1000;
    private const int Samples = 40;
    private const int WarmUp = 5;
    private const double BudgetMs = 500;

    private readonly ITestOutputHelper _output;

    public ExpiryReadPerformanceTests(ITestOutputHelper output) => _output = output;

    private static double P95(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[(int)Math.Ceiling(0.95 * sorted.Count) - 1];
    }

    [PerformanceFact]
    public async Task Panel_lists_and_order_answer_within_500_ms_p95_with_1000_lots()
    {
        using var factory = new ExpiryApiFactory();
        var s = new ExpiryScenario(factory);
        var reader = s.Mint(s.Qa, ["expiry.dashboard.read", "expiry.fefo-order.read"]);
        var today = DateOnly.FromDateTime(factory.Clock.GetUtcNow().UtcDateTime);
        var posting = today.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T10:00:00Z";
        for (var i = 0; i < Lots; i++)
        {
            // spread over −20 … +979 days: expired, critical, approaching and later lots
            var expiry = today.AddDays(i - 20).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00Z";
            var lot = await s.Lots.CreateLotAsync(expiryDate: expiry);
            await s.Lots.ReleaseAsync(lot);
            await s.Lots.PostOkAsync(s.Lots.Receipt("10", lot, movementType: "OPENING_BALANCE", postingDate: posting));
        }

        var reads = new (string Name, string Path)[]
        {
            ("getDashboard", "/api/expiry/dashboard"),
            ("listApproaching (page 1, 25)", "/api/expiry/lots/approaching"),
            ("listExpired (page 1, 25)", "/api/expiry/lots/expired"),
            ("getFefoOrder (1000 candidates)", $"/api/expiry/fefo-order?warehouseId={s.Ledger.Warehouse}&itemId={s.Ledger.Item}&skuId={s.Ledger.Sku}&skuLevel=Lsku&quantity=500")
        };
        var results = new List<(string Name, double P95)>();
        foreach (var (name, path) in reads)
        {
            var times = new List<double>();
            for (var i = 0; i < WarmUp + Samples; i++)
            {
                var watch = Stopwatch.StartNew();
                await s.ExpectAsync(HttpMethod.Get, path, reader, null, 200);
                watch.Stop();
                if (i >= WarmUp) times.Add(watch.Elapsed.TotalMilliseconds);
            }

            results.Add((name, P95(times)));
            _output.WriteLine($"P95 | {name} | lots={Lots} n={Samples} | {P95(times):F1} ms");
        }

        Assert.All(results, r => Assert.True(r.P95 <= BudgetMs, $"{r.Name}: p95 {r.P95:F1} ms (budget {BudgetMs} ms)."));
    }
}
