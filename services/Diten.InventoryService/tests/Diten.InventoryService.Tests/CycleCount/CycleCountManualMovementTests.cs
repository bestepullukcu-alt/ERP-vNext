using System.Diagnostics;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-3 — the "count or manual" split of <c>COUNT_*</c> movements (pack §2.4, Q-21 = A; AC-47 (a)–(e)) and the §19.4 item 2 measurement of the
/// manual read on a large warehouse. 0181 only READS the ledger here; the manual movements are posted through MOD-0173's own endpoint.
/// </summary>
public sealed class CycleCountManualMovementTests(CycleCountApiFactory factory, ITestOutputHelper output) : IClassFixture<CycleCountApiFactory>
{
    private static string Window()
    {
        var from = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        return $"from={from}&to={to}";
    }

    private static async Task<JsonElement> ManualAsync(CycleCountScenario s, (Guid Item, Guid Sku)? sku, string type, decimal quantity, Guid bin, string sourceModule,
        string sourceType, string? key = null, string? sourceDocumentId = null, string? sourceLineId = null)
    {
        var body = s.Inventory.Movement(type, Q(quantity));
        if (sku is { } x)
        {
            body["itemId"] = x.Item.ToString("D");
            body["skuId"] = x.Sku.ToString("D");
        }

        var gain = type == "COUNT_GAIN";
        body[gain ? "toWarehouseId" : "fromWarehouseId"] = s.Inventory.Warehouse;
        body[gain ? "toLocationId" : "fromLocationId"] = bin.ToString("D");
        body["sourceModule"] = sourceModule;
        body["sourceType"] = sourceType;
        if (key is not null) body["idempotencyKey"] = key;
        if (sourceDocumentId is not null) body["sourceDocumentId"] = sourceDocumentId;
        if (sourceLineId is not null) body["sourceLineId"] = sourceLineId;
        return await s.Inventory.PostExpectingAsync(body, 201);
    }

    private static List<JsonElement> Items(JsonElement page) => page.GetProperty("items").EnumerateArray().ToList();

    private static string? MovementId(JsonElement row) => row.TryGetProperty("movement", out var m) ? m.GetProperty("transactionId").GetString() : null;

    [Fact]
    public async Task AC47_count_corrections_and_manual_COUNT_movements_are_told_apart_in_both_directions()
    {
        var s = new CycleCountScenario(factory);
        var warehouse = s.Warehouse.ToString("D");

        // (a) a 0181 correction, Posted
        var (countA, lineA) = await s.SubmittedLineAsync(10, 12);
        await Expect(s.ApproveAsync(lineA), 200);
        var a = Assert.Single(s.CountLedgerMovements());
        // (b) a manual COUNT_LOSS (MOD-0173 / MANUAL_MOVEMENT) and (c) a manual COUNT_GAIN that DECLARES MOD-0181 under another key
        var b = await ManualAsync(s, null, "COUNT_LOSS", 1, s.BinA, "MOD-0173", "MANUAL_MOVEMENT");
        var c = await ManualAsync(s, null, "COUNT_GAIN", 1, s.BinA, "MOD-0181", "CYCLE_COUNT", sourceDocumentId: countA, sourceLineId: LineId(lineA));
        // (d) a 0181 correction committed while its line is still Pending (transaction 2 not written — its answer was lost)
        var binD = s.Node();
        var (countD, lineD) = await s.SubmittedLineAsync(10, 7, bin: binD);
        factory.PosterFault[s.Tenant] = CountPosterFault.CommitThenException;
        Assert.Equal(503, (int)(await s.ApproveAsync(lineD)).StatusCode);
        var pendingD = await s.LineAsync(countD, LineId(lineD));
        Assert.Equal("Pending", pendingD.GetProperty("adjustment").GetProperty("status").GetString());
        Assert.False(pendingD.GetProperty("adjustment").TryGetProperty("transactionId", out _));
        var d = s.CountLedgerMovements().Single(m => m["SourceLineId"].AsString == LineId(lineD));
        // (e) BEFORE its line posts, a caller declares the line's own key, source and line on MOD-0173's endpoint → the approval meets an integrity error
        var binE = s.Node();
        var (countE, lineE) = await s.SubmittedLineAsync(10, 12, bin: binE);
        var e = await ManualAsync(s, null, "COUNT_GAIN", 2, binE, "MOD-0181", "CYCLE_COUNT", key: "cycle-count:" + LineId(lineE), sourceDocumentId: countE,
            sourceLineId: LineId(lineE));
        Assert.Equal("COUNT_ADJUSTMENT_INTEGRITY", await ErrorCode(s.ApproveAsync(lineE), 409));

        // a movement of ANOTHER company of the tenant on this warehouse's bin (staged: the ledger would refuse a foreign bin) is never shown
        var foreign = s.Stored("inventory_transactions").Single(m => m["_id"].AsString == b.GetProperty("transactionId").GetString()).DeepClone().AsBsonDocument;
        foreign["_id"] = Guid.NewGuid().ToString("D");
        foreign["LegalEntityId"] = Guid.NewGuid().ToString("D");
        foreign["IdempotencyKey"] = "foreign-" + Guid.NewGuid().ToString("N");
        foreign["TransactionNumber"] = "FOREIGN-" + Guid.NewGuid().ToString("N")[..8];
        factory.Collection("inventory_transactions").InsertOne(foreign);

        var manual = Items(await s.VariancesAsync($"?source=manual&warehouseId={warehouse}&{Window()}&pageSize=200"));
        Assert.All(manual, r => Assert.Equal("Manual", r.GetProperty("source").GetString()));
        var manualIds = manual.Select(MovementId).ToList();
        Assert.Equal(new[] { b.GetProperty("transactionId").GetString(), c.GetProperty("transactionId").GetString(), e.GetProperty("transactionId").GetString() }
            .Order(), manualIds.Order());
        Assert.DoesNotContain(a["_id"].AsString, manualIds);
        Assert.DoesNotContain(d["_id"].AsString, manualIds);
        Assert.DoesNotContain(foreign["_id"].AsString, manualIds);
        bool Mismatch(JsonElement result) => manual.Single(r => MovementId(r) == result.GetProperty("transactionId").GetString())
            .GetProperty("movement").GetProperty("sourceMismatch").GetBoolean();
        Assert.False(Mismatch(b));
        Assert.True(Mismatch(c));
        Assert.True(Mismatch(e));
        Assert.Equal("MOD-0173", manual.Single(r => MovementId(r) == b.GetProperty("transactionId").GetString())
            .GetProperty("movement").GetProperty("sourceModule").GetString());

        var counted = Items(await s.VariancesAsync($"?source=count&warehouseId={warehouse}&pageSize=200"));
        Assert.All(counted, r => Assert.Equal("Count", r.GetProperty("source").GetString()));
        string? Ledger(JsonElement line) => counted.Single(r => r.GetProperty("line").GetProperty("lineId").GetString() == LineId(line))
            .TryGetProperty("ledgerTransactionId", out var id) ? id.GetString() : null;
        Assert.Equal(a["_id"].AsString, Ledger(lineA));
        Assert.Equal(d["_id"].AsString, Ledger(lineD)); // Pending: matched by its key and CreatedAt ≥ FirstAttemptAt, not by a transaction id
        Assert.Null(Ledger(lineE));

        var all = Items(await s.VariancesAsync($"?source=all&warehouseId={warehouse}&{Window()}&pageSize=200"));
        Assert.Equal(3, all.Count(r => r.GetProperty("source").GetString() == "Count"));
        Assert.Equal(3, all.Count(r => r.GetProperty("source").GetString() == "Manual"));

        // (e) after AcceptHolder: the declared movement IS the line's correction — "Sayım", no longer "Elle"
        await Expect(s.ResolveAsync(await s.LineAsync(countE, LineId(lineE)), "AcceptHolder"), 200);
        Assert.DoesNotContain(e.GetProperty("transactionId").GetString(),
            Items(await s.VariancesAsync($"?source=manual&warehouseId={warehouse}&{Window()}&pageSize=200")).Select(MovementId));
        counted = Items(await s.VariancesAsync($"?source=count&warehouseId={warehouse}&pageSize=200"));
        Assert.Equal(e.GetProperty("transactionId").GetString(), Ledger(lineE));

        // 0181 wrote no manual movement and no ledger row other than its corrections
        Assert.All(s.CountLedgerMovements().Where(m => m["SourceType"].AsString == "CYCLE_COUNT" && m["IdempotencyKey"].AsString.StartsWith("cycle-count:")),
            m => Assert.NotEqual(c.GetProperty("transactionId").GetString(), m["_id"].AsString));
    }

    [Fact]
    public async Task AC47_manual_and_all_need_a_warehouse_and_a_window_of_at_most_31_days()
    {
        var s = new CycleCountScenario(factory);
        var warehouse = s.Warehouse.ToString("D");
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync($"/api/warehouse/count-variances?source=manual&{Window()}", s.ApproverToken), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync($"/api/warehouse/count-variances?source=all&warehouseId={warehouse}", s.ApproverToken), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync(
            $"/api/warehouse/count-variances?source=manual&warehouseId={warehouse}&from=2026-09-01&to=2026-10-03", s.ApproverToken), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/count-variances?source=elle", s.ApproverToken), 400));
        await Expect(s.GetAsync($"/api/warehouse/count-variances?source=manual&warehouseId={warehouse}&from=2026-09-01&to=2026-10-02", s.ApproverToken), 200);
        Assert.Equal(403, (int)(await s.GetAsync("/api/warehouse/count-variances")).StatusCode); // counts.* only: no variances.read
    }

    /// <summary>
    /// Pack §19.4 item 2 (DL-3 first measurement): <c>source=manual</c> over a LARGE warehouse — 500 bins, 120 000 movements of the tenant in the last
    /// 30 days (10 % COUNT_*, the rest receipts / transfers), staged straight into the ledger's collection (the read under test is production code).
    /// The measured time is written to the test output; the bound only guards against a pathological plan.
    /// </summary>
    [Fact]
    public async Task S19_4_the_manual_read_on_a_large_warehouse_is_measured()
    {
        const int bins = 500;
        const int perBin = 240;
        var s = new CycleCountScenario(factory);
        var binIds = Enumerable.Range(0, bins).Select(_ => s.Node()).ToList();
        var now = DateTime.UtcNow;
        var random = new Random(181);
        var documents = new List<BsonDocument>(bins * perBin);
        var countRows = 0;
        foreach (var bin in binIds)
        {
            for (var i = 0; i < perBin; i++)
            {
                var kind = random.Next(10);
                var type = kind == 0 ? (random.Next(2) == 0 ? "COUNT_GAIN" : "COUNT_LOSS") : kind < 6 ? "GOODS_RECEIPT" : "LOCATION_TRANSFER";
                if (type.StartsWith("COUNT_")) countRows++;
                var inbound = type is "COUNT_GAIN" or "GOODS_RECEIPT";
                var created = now.AddMinutes(-random.Next(30 * 24 * 60));
                documents.Add(new BsonDocument
                {
                    ["_id"] = Guid.NewGuid().ToString("D"), ["TenantId"] = s.Tenant.ToString("D"), ["LegalEntityId"] = s.LegalEntity.ToString("D"),
                    ["CreatedAt"] = new BsonArray { created.Ticks, 0 }, ["Version"] = 1, ["IsDeleted"] = false,
                    ["TransactionNumber"] = "PERF-" + Guid.NewGuid().ToString("N")[..12], ["MovementType"] = type == "COUNT_GAIN" ? "CountGain"
                        : type == "COUNT_LOSS" ? "CountLoss" : type == "GOODS_RECEIPT" ? "GoodsReceipt" : "LocationTransfer",
                    ["ItemId"] = s.Inventory.Item.ToString("D"), ["SkuId"] = s.Inventory.Sku.ToString("D"), ["SkuLevel"] = "Lsku",
                    ["FromWarehouseId"] = inbound ? BsonNull.Value : s.Warehouse.ToString("D"), ["FromLocationId"] = inbound ? BsonNull.Value : bin.ToString("D"),
                    ["ToWarehouseId"] = inbound || type == "LOCATION_TRANSFER" ? s.Warehouse.ToString("D") : BsonNull.Value,
                    ["ToLocationId"] = inbound ? bin.ToString("D") : type == "LOCATION_TRANSFER" ? binIds[random.Next(bins)].ToString("D") : BsonNull.Value,
                    ["Quantity"] = new BsonDecimal128(1m), ["UomId"] = "EA", ["BaseQuantity"] = new BsonDecimal128(1m), ["BaseUomId"] = "EA",
                    ["FromStockStatus"] = inbound ? BsonNull.Value : "Available", ["ToStockStatus"] = inbound ? "Available" : BsonNull.Value,
                    ["SourceModule"] = "MOD-PERF", ["SourceType"] = "PERF", ["IdempotencyKey"] = "perf-" + Guid.NewGuid().ToString("N"),
                    ["RequestFingerprint"] = "perf", ["CorrelationId"] = Guid.NewGuid().ToString("D"), ["CreatedBy"] = s.User.ToString("D"),
                    ["TransactionDate"] = new BsonArray { created.Ticks, 0 }, ["PostingDate"] = new BsonArray { created.Ticks, 0 }
                });
            }
        }

        // the stored DateTimeOffset shape of this service, taken from a real movement (never guessed)
        var real = await s.ReceiveAsync(1, s.BinA);
        var shape = s.Stored("inventory_transactions").Single(m => m["_id"].AsString == real.GetProperty("transactionId").GetString())["CreatedAt"];
        if (!shape.IsBsonArray)
        {
            foreach (var doc in documents)
                foreach (var field in new[] { "CreatedAt", "TransactionDate", "PostingDate" })
                    doc[field] = new BsonDateTime(new DateTime(doc[field].AsBsonArray[0].AsInt64, DateTimeKind.Utc));
        }

        var insert = Stopwatch.StartNew();
        foreach (var chunk in documents.Chunk(10_000)) await factory.Collection("inventory_transactions").InsertManyAsync(chunk);
        insert.Stop();

        var query = $"?source=manual&warehouseId={s.Warehouse:D}&from={DateTime.UtcNow.AddDays(-30):yyyy-MM-dd}&to={DateTime.UtcNow.AddDays(1):yyyy-MM-dd}&pageSize=200";
        await s.VariancesAsync(query); // warm-up (plan cache, connection)
        var timings = new List<long>();
        JsonElement page = default;
        for (var run = 0; run < 3; run++)
        {
            var watch = Stopwatch.StartNew();
            page = await s.VariancesAsync(query);
            watch.Stop();
            timings.Add(watch.ElapsedMilliseconds);
        }

        var total = page.GetProperty("total").GetInt64();
        output.WriteLine($"S19.4 manual read: bins={bins} movements={documents.Count} countMovements={countRows} returned total={total} " +
                         $"pageSize=200 insert={insert.ElapsedMilliseconds} ms runs(ms)={string.Join(",", timings)} median={timings.Order().ElementAt(1)} ms");
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "mod0181-s19-4-measurement.txt"),
            $"{DateTime.UtcNow:O} bins={bins} movements={documents.Count} countMovements={countRows} total={total} runs={string.Join(",", timings)}{Environment.NewLine}");
        Assert.Equal(countRows, total); // every staged COUNT_* row is in the window and matches no line
        Assert.True(timings.Order().ElementAt(1) < 30_000, $"median {timings.Order().ElementAt(1)} ms");
    }
}
