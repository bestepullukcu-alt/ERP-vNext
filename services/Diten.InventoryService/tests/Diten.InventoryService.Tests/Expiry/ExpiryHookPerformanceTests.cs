using System.Diagnostics;
using System.Globalization;
using MongoDB.Bson;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F2 — the posting cost of the expiry hooks (pack §11: p95 added ≤ 50 ms with 200 lots per SKU). The same world — 200 released lots
/// of one SKU with AVAILABLE stock in one warehouse — is posted against the host WITH the hooks (<see cref="ExpiryApiFactory"/>) and the
/// ledger's default "no hook" (<see cref="NoExpiryHookApiFactory"/>), through the real posting endpoint. Measured: a compliant issue (FEFO
/// read + snapshot), a reasoned bypass (FEFO read + exception + snapshot) and a GOODS_RECEIPT_PO (minimum check).
/// </summary>
public sealed class ExpiryHookPerformanceTests
{
    private const int Lots = 200;
    private const int Samples = 60;
    private const int WarmUp = 8;
    private const double BudgetMs = 50;

    private readonly ITestOutputHelper _output;

    public ExpiryHookPerformanceTests(ITestOutputHelper output) => _output = output;

    private sealed record Measured(double Issue, double Bypass, double Receipt);

    private static double P95(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[(int)Math.Ceiling(0.95 * sorted.Count) - 1];
    }

    private static async Task<Measured> MeasureAsync(ExpiryApiFactory factory)
    {
        var s = new ExpiryScenario(factory);
        factory.ShelfLife[s.Ledger.Sku] = (null, null, 30);
        var lots = new List<string>(Lots);
        for (var i = 0; i < Lots; i++)
        {
            var expiry = new DateOnly(2027, 1, 1).AddDays(i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00Z";
            var lot = await s.LotAsync(expiry);
            await s.Lots.PostOkAsync(s.Lots.Receipt("1000", lot, movementType: "OPENING_BALANCE"));
            lots.Add(lot);
        }

        async Task<List<double>> Run(Func<int, Dictionary<string, object?>> body)
        {
            var times = new List<double>();
            for (var i = 0; i < WarmUp + Samples; i++)
            {
                var request = body(i);
                var watch = Stopwatch.StartNew();
                await s.Lots.PostAsync(request, 201);
                watch.Stop();
                if (i >= WarmUp) times.Add(watch.Elapsed.TotalMilliseconds);
            }

            return times;
        }

        var issue = await Run(_ => s.Lots.Issue("1", lots[0]));
        var bypass = await Run(i =>
        {
            var b = s.Lots.Issue("1", lots[Lots - 1 - (i % 50)]);
            b["fefoBypassReason"] = "Performans olcumu: FEFO disi bilincli cikis";
            return b;
        });
        var receipt = await Run(i => s.Lots.Receipt("1", lots[i % Lots]));
        return new Measured(P95(issue), P95(bypass), P95(receipt));
    }

    [Fact]
    public async Task The_hooks_add_at_most_50_ms_p95_with_200_lots_per_SKU()
    {
        Measured baseline, hooked;
        using (var plain = new NoExpiryHookApiFactory()) baseline = await MeasureAsync(plain);
        using (var hooks = new ExpiryApiFactory()) hooked = await MeasureAsync(hooks);

        var rows = new[]
        {
            ("GOODS_ISSUE_SALES compliant", baseline.Issue, hooked.Issue),
            ("GOODS_ISSUE_SALES bypass + reason", baseline.Issue, hooked.Bypass),
            ("GOODS_RECEIPT_PO (minimum check)", baseline.Receipt, hooked.Receipt)
        };
        foreach (var (name, without, with) in rows)
        {
            _output.WriteLine($"P95 | {name} | lots={Lots} n={Samples} | no hook {without:F1} ms | hook {with:F1} ms | added {with - without:F1} ms");
        }

        Assert.All(rows, r => Assert.True(r.Item3 - r.Item2 <= BudgetMs, $"{r.Item1}: the hooks add {r.Item3 - r.Item2:F1} ms p95 (budget {BudgetMs} ms)."));
    }

    /// <summary>
    /// CT-F13 — measured before deciding on an index: the FEFO candidate read (ExpiryPostingPolicy.FefoAsync: tenant, company, not deleted, item,
    /// SKU, level, warehouse, lot set, on hand &gt; 0) is answered by an IXSCAN of an EXISTING balance index, examining only the SKU's rows of that
    /// warehouse. The filter is built from a stored balance row of the world, so its field shapes are the persisted ones.
    /// </summary>
    [Fact]
    public async Task The_FEFO_candidate_read_uses_an_existing_balance_index()
    {
        using var factory = new ExpiryApiFactory();
        var s = new ExpiryScenario(factory);
        for (var i = 0; i < 20; i++)
        {
            await s.Lots.PostOkAsync(s.Lots.Receipt("5", await s.LotAsync($"2027-0{1 + (i % 9)}-15T00:00:00Z"), movementType: "OPENING_BALANCE"));
        }

        var row = s.Persisted("inventory_balances").First(d => !d["LotId"].IsBsonNull);
        var filter = new MongoDB.Bson.BsonDocument
        {
            ["TenantId"] = row["TenantId"], ["LegalEntityId"] = row["LegalEntityId"], ["IsDeleted"] = false, ["ItemId"] = row["ItemId"],
            ["SkuId"] = row["SkuId"], ["SkuLevel"] = row["SkuLevel"], ["WarehouseId"] = row["WarehouseId"],
            ["LotId"] = new MongoDB.Bson.BsonDocument("$ne", MongoDB.Bson.BsonNull.Value),
            ["OnHand"] = new MongoDB.Bson.BsonDocument("$gt", new MongoDB.Bson.BsonDecimal128(0m))
        };
        var explain = await factory.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument
        {
            ["explain"] = new MongoDB.Bson.BsonDocument { ["find"] = "inventory_balances", ["filter"] = filter },
            ["verbosity"] = "executionStats"
        });

        var plan = explain["queryPlanner"]["winningPlan"].AsBsonDocument.ToJson();
        var stats = explain["executionStats"];
        _output.WriteLine($"CT-F13 | winningPlan {plan}");
        _output.WriteLine($"CT-F13 | nReturned {stats["nReturned"]} totalKeysExamined {stats["totalKeysExamined"]} totalDocsExamined {stats["totalDocsExamined"]}");
        Assert.Contains("IXSCAN", plan);
        Assert.DoesNotContain("COLLSCAN", plan);
        Assert.Contains(Diten.InventoryService.Persistence.InventoryIndexConfiguration.BalanceDimensionIndex, plan);
        Assert.Equal(20, stats["nReturned"].ToInt32());
        Assert.Equal(20, stats["totalDocsExamined"].ToInt32());
    }
}
