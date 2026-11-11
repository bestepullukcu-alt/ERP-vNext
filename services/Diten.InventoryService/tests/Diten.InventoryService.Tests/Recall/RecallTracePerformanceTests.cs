using System.Diagnostics;
using System.Text.Json;
using Diten.InventoryService.Tests.Expiry;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.InventoryService.Tests.Recall;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RecallPerformanceCollection
{
    public const string Name = "recall-performance";
}

/// <summary>
/// WP-0177-R2 item 6 — the trace budget on the real host and the real Mongo source: 3 levels / ~500 nodes p95 ≤ 2 s; ~5000 nodes ≤ 8 s
/// (the default <c>TraceMaxNodes</c>, not truncated). Runs only with <c>INVENTORY_PERF_TESTS=1</c> (F3 pattern), alone. The forward tree is
/// seeded as clones of the world's real rows (lot, genealogy link, MVP-6 sale) — the same shapes and indexes the APIs write.
/// </summary>
[Collection(RecallPerformanceCollection.Name)]
public sealed class RecallTracePerformanceTests
{
    private const int Samples = 20;
    private const int WarmUp = 3;

    private readonly ITestOutputHelper _output;

    public RecallTracePerformanceTests(ITestOutputHelper output) => _output = output;

    private static double P95(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[(int)Math.Ceiling(0.95 * sorted.Count) - 1];
    }

    /// <summary>A forward tree under the world's finished good: <paramref name="fanOut"/> children per level, and one MVP-6 sale on the first <paramref name="sales"/> leaves.</summary>
    private static int Seed(RecallTraceWorld w, int[] fanOut, int sales)
    {
        var db = w.S.Factory.Database;
        var lotTemplate = Assert.Single(w.S.Persisted("trace_lots"), d => d["_id"].ToString() == w.Fg);
        var linkTemplate = Assert.Single(w.S.Persisted("trace_genealogy_links"));
        var saleTemplate = Assert.Single(w.S.Persisted("inventory_transactions"), d => d.Contains("SourceDocumentId") && d["SourceDocumentId"] == w.Shipment);
        var lots = new List<BsonDocument>();
        var links = new List<BsonDocument>();
        var txns = new List<BsonDocument>();
        var frontier = new List<string> { w.Fg };
        var n = 0;
        foreach (var width in fanOut)
        {
            var next = new List<string>();
            foreach (var parent in frontier)
            {
                for (var i = 0; i < width; i++)
                {
                    var id = Guid.NewGuid();
                    var lot = lotTemplate.DeepClone().AsBsonDocument;
                    lot["_id"] = RecallTraceWorld.SameShape(lotTemplate["_id"], id);
                    lot["LotNumber"] = $"PF-{++n:D5}";
                    foreach (var k in lot.Names.Where(k => k.EndsWith("IdempotencyKey", StringComparison.Ordinal) || k == "NormalizedLotNumber").ToList()) lot[k] = $"pf-{id:N}";
                    if (lot.Contains("IdentityKey")) lot["IdentityKey"] = lotTemplate["IdentityKey"].AsString[..lotTemplate["IdentityKey"].AsString.LastIndexOf('|')] + $"|PF-{n:D5}";
                    lots.Add(lot);
                    var link = linkTemplate.DeepClone().AsBsonDocument;
                    link["_id"] = RecallTraceWorld.SameShape(linkTemplate["_id"], Guid.NewGuid());
                    link["ParentLotId"] = RecallTraceWorld.SameShape(linkTemplate["ParentLotId"], Guid.Parse(parent));
                    link["ChildLotId"] = RecallTraceWorld.SameShape(linkTemplate["ChildLotId"], id);
                    link["TransformationReference"] = $"MO-PF-{n:D5}";
                    foreach (var k in link.Names.Where(k => k.EndsWith("IdempotencyKey", StringComparison.Ordinal)).ToList()) link[k] = $"pf-{Guid.NewGuid():N}";
                    links.Add(link);
                    next.Add(id.ToString("D"));
                }
            }

            frontier = next;
        }

        foreach (var leaf in frontier.Take(sales))
        {
            var sale = saleTemplate.DeepClone().AsBsonDocument;
            sale["_id"] = RecallTraceWorld.SameShape(saleTemplate["_id"], Guid.NewGuid());
            sale["LotId"] = leaf;
            sale["SourceDocumentId"] = Guid.NewGuid().ToString("D");
            foreach (var k in sale.Names.Where(k => k.EndsWith("IdempotencyKey", StringComparison.Ordinal) || k == "TransactionNumber").ToList()) sale[k] = $"pf-{Guid.NewGuid():N}";
            txns.Add(sale);
        }

        db.GetCollection<BsonDocument>("trace_lots").InsertMany(lots);
        db.GetCollection<BsonDocument>("trace_genealogy_links").InsertMany(links);
        if (txns.Count > 0) db.GetCollection<BsonDocument>("inventory_transactions").InsertMany(txns);
        return lots.Count + txns.Count;
    }

    private async Task<(double P95, double Max, double ServerP95, int Nodes, bool Truncated)> MeasureAsync(RecallTraceWorld w, int samples)
    {
        var wall = new List<double>();
        var server = new List<double>();
        JsonElement last = default;
        for (var i = 0; i < WarmUp + samples; i++)
        {
            var clock = Stopwatch.StartNew();
            last = await w.TraceAsync(w.Fg, "Forward");
            clock.Stop();
            if (i < WarmUp) continue;
            wall.Add(clock.Elapsed.TotalMilliseconds);
            server.Add(last.GetProperty("durationMs").GetInt64());
        }

        return (P95(wall), wall.Max(), P95(server), last.GetProperty("nodes").GetArrayLength(), last.GetProperty("truncated").GetBoolean());
    }

    [PerformanceFact]
    public async Task Three_levels_about_500_nodes_answer_within_2_s_p95()
    {
        using var factory = new RecallApiFactory();
        var w = await RecallTraceWorld.CreateAsync(factory);
        var seeded = Seed(w, [8, 8, 5], 140); // 8 + 64 + 320 lots + 140 shipments

        var (p95, max, serverP95, nodes, truncated) = await MeasureAsync(w, Samples);

        _output.WriteLine($"R2-PERF 3-level: seeded={seeded} nodes={nodes} truncated={truncated} samples={Samples} p95={p95:F0}ms max={max:F0}ms server-p95={serverP95:F0}ms");
        Assert.False(truncated);
        Assert.InRange(nodes, 500, 600);
        Assert.True(p95 <= 2000, $"p95 {p95:F0} ms > 2000 ms");
    }

    [PerformanceFact]
    public async Task About_5000_nodes_answer_within_8_s()
    {
        using var factory = new RecallApiFactory();
        var w = await RecallTraceWorld.CreateAsync(factory);
        var seeded = Seed(w, [20, 20, 11], 150); // 20 + 400 + 4400 lots + 150 shipments

        var (p95, max, serverP95, nodes, truncated) = await MeasureAsync(w, 5);

        _output.WriteLine($"R2-PERF 5000: seeded={seeded} nodes={nodes} truncated={truncated} samples=5 p95={p95:F0}ms max={max:F0}ms server-p95={serverP95:F0}ms");
        Assert.False(truncated);
        Assert.InRange(nodes, 4900, 5000);
        Assert.True(max <= 8000, $"max {max:F0} ms > 8000 ms");
    }
}
