using System.Diagnostics;
using System.Globalization;
using MongoDB.Bson;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F2 — the posting cost of the expiry hooks (pack §11: p95 added ≤ 50 ms with 200 lots per SKU). The same world — 200 released lots
/// of one SKU with AVAILABLE stock in one warehouse — is built on the host WITH the hooks (<see cref="ExpiryApiFactory"/>) and on the ledger's
/// default "no hook" host (<see cref="NoExpiryHookApiFactory"/>) and posted through the real endpoint. Measured: a compliant issue (FEFO read +
/// snapshot), a reasoned bypass (FEFO read + exception + snapshot) and a GOODS_RECEIPT_PO (minimum check).
/// <para>WP-0176-F2-FIX1 — the samples are INTERLEAVED (no-hook request, then hook request, per sample) and the class runs in a non-parallel
/// collection. Measured at bc7f520e9: with the two sides measured one after the other, an environment stall of the shared test mongod during
/// one phase shifted that whole phase (3 diagnostic full runs: compliant-issue p50 35.8 / 81.4 / 34.9 ms with the hooks, 16.6 / 15.5 / 15.6
/// without; the candidate read itself p95 10–12 ms in all three) and the suite went red 3 of 6 runs although the code path did not change.
/// Interleaving puts such a stall on both sides.</para>
/// </summary>
[Collection(ExpiryPerformanceCollection.Name)]
public sealed class ExpiryHookPerformanceTests
{
    private const int Lots = 200;
    private const int Samples = 60;
    private const int WarmUp = 8;
    private const double BudgetMs = 50;
    private const string BypassReason = "Performans olcumu: FEFO disi bilincli cikis";

    private readonly ITestOutputHelper _output;

    public ExpiryHookPerformanceTests(ITestOutputHelper output) => _output = output;

    private sealed record World(ExpiryScenario S, List<string> Lots);

    private static double P95(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[(int)Math.Ceiling(0.95 * sorted.Count) - 1];
    }

    private static async Task<World> BuildAsync(ExpiryApiFactory factory)
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

        return new World(s, lots);
    }

    private static async Task<double> TimeAsync(World w, Dictionary<string, object?> body)
    {
        var watch = Stopwatch.StartNew();
        await w.S.Lots.PostAsync(body, 201);
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds;
    }

    /// <summary>One phase, interleaved: per sample the no-hook request, then the hook request (same shape on each side's own world).</summary>
    private static async Task<(List<double> Without, List<double> With)> PhaseAsync(World plain, World hooked, Func<World, int, Dictionary<string, object?>> body)
    {
        var without = new List<double>();
        var with = new List<double>();
        for (var i = 0; i < WarmUp + Samples; i++)
        {
            var a = await TimeAsync(plain, body(plain, i));
            var b = await TimeAsync(hooked, body(hooked, i));
            if (i < WarmUp) continue;
            without.Add(a);
            with.Add(b);
        }

        return (without, with);
    }

    [PerformanceFact]
    public async Task The_hooks_add_at_most_50_ms_p95_with_200_lots_per_SKU()
    {
        using var plainHost = new NoExpiryHookApiFactory();
        using var hookHost = new ExpiryApiFactory();
        var plain = await BuildAsync(plainHost);
        var hooked = await BuildAsync(hookHost);

        var issue = await PhaseAsync(plain, hooked, (w, _) => w.S.Lots.Issue("1", w.Lots[0]));
        var bypass = await PhaseAsync(plain, hooked, (w, i) =>
        {
            var b = w.S.Lots.Issue("1", w.Lots[Lots - 1 - (i % 50)]);
            b["fefoBypassReason"] = BypassReason;
            return b;
        });
        var receipt = await PhaseAsync(plain, hooked, (w, i) => w.S.Lots.Receipt("1", w.Lots[i % Lots]));

        var rows = new[]
        {
            ("GOODS_ISSUE_SALES compliant", P95(issue.Without), P95(issue.With)),
            ("GOODS_ISSUE_SALES bypass + reason", P95(bypass.Without), P95(bypass.With)),
            ("GOODS_RECEIPT_PO (minimum check)", P95(receipt.Without), P95(receipt.With))
        };
        foreach (var (name, without, with) in rows)
        {
            _output.WriteLine($"P95 | {name} | lots={Lots} n={Samples} interleaved | no hook {without:F1} ms | hook {with:F1} ms | added {with - without:F1} ms");
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

/// <summary>Timing measurements run alone: xUnit starts a DisableParallelization collection after the parallel ones have finished.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ExpiryPerformanceCollection
{
    public const string Name = "expiry-performance (not parallel)";
}

/// <summary>
/// WP-0176-F3 item 0 (CT decision O-FIX1-2): the p95 budget test is timing-sensitive and was unstable next to the rest of the suite on the
/// MVP-1 machine. It runs only with <c>INVENTORY_PERF_TESTS=1</c> (alone); otherwise it is skipped with an explicit reason. The 50 ms budget
/// is unchanged; the CT runs it at each acceptance.
/// </summary>
public sealed class PerformanceFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "INVENTORY_PERF_TESTS";

    public PerformanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
        {
            // TEST-HIZ-01: the reason starts with a code so CI's "Skipped = 0" step can allow exactly these skips
            // (scripts/ci-skip-allowlist.tsv, inventory-tests INVENTORY_PERF).
            Skip = "INVENTORY_PERF: performance test — run alone with INVENTORY_PERF_TESTS=1, CT measures at acceptance";
        }
    }
}
