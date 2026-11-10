using System.Text.Json;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-3 — the recount (pack §8.5, §4.7; AC-32 with r5 SB1/F-1 and r6 SB2/F-2, AC-13 (e)) over the REAL host: a new count of the selected keys
/// only, its own snapshot, the claims handed over, the chain limit, and the keys left out of it.
/// </summary>
public sealed class CycleCountRecountTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    private static string Status(JsonElement e) => e.GetProperty("status").GetString()!;

    private static string Sku(JsonElement line) => line.GetProperty("item").GetProperty("skuId").GetString()!;

    [Fact]
    public async Task AC32_a_recount_opens_a_new_released_count_of_exactly_the_selected_keys_found_ones_included()
    {
        var s = new CycleCountScenario(factory);
        var other = s.AddSku();
        var found = s.AddSku();
        await s.ReceiveAsync(5, s.BinA);
        await s.ReceiveAsync(3, s.BinA, other);
        await s.ReceiveAsync(4, s.BinB);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [s.BinA, s.BinB], blind: true)));
        var countId = Id(released);
        var lines = await s.LinesAsync(countId);
        await s.CountedAsync(countId, lines.Single(l => LocationOf(l) == s.BinA && Sku(l) == s.Inventory.Sku.ToString("D")), 6);
        await s.CountedAsync(countId, lines.Single(l => Sku(l) == other.Sku.ToString("D")), 3);
        await s.CountedAsync(countId, LineOf(lines, s.BinB), 2);
        await Expect(s.AddLineAsync(countId, s.FoundLine(s.BinA, found, 2)), 201);
        var submitted = await s.SubmittedAsync(countId);
        var after = await s.LinesAsync(countId, s.ApproverToken);
        var mainA = after.Single(l => LocationOf(l) == s.BinA && Sku(l) == s.Inventory.Sku.ToString("D"));
        var foundLine = after.Single(l => l.GetProperty("origin").GetString() == "Found");
        var assignee = Guid.NewGuid();
        factory.ActiveUsers[(s.Tenant, assignee)] = true;

        // shape and state first: blind / scope are not members; a NoVariance line is not recounted
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.RecountAsync(countId, ETag(submitted), [LineId(mainA)],
            extra: new Dictionary<string, object?> { ["blind"] = false }), 400));
        Assert.Equal("COUNT_REASON_REQUIRED", await ErrorCode(s.RecountAsync(countId, ETag(submitted), [LineId(mainA)], reason: null), 400));
        var noVariance = after.Single(l => Sku(l) == other.Sku.ToString("D"));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.RecountAsync(countId, ETag(submitted), [LineId(noVariance)]), 409));

        var recount = await Expect(s.RecountAsync(countId, ETag(submitted), [LineId(mainA), LineId(foundLine)],
            extra: new Dictionary<string, object?> { ["assignedTo"] = assignee.ToString("D"), ["note"] = "ikinci sayim" }), 201);
        Assert.Equal("Released", Status(recount));
        Assert.Equal("Cycle", recount.GetProperty("countType").GetString());
        Assert.True(recount.GetProperty("blind").GetBoolean()); // copied: the recount of a blind count is blind
        Assert.Equal(countId, recount.GetProperty("recountOfId").GetString());
        Assert.Equal(s.Warehouse.ToString("D"), recount.GetProperty("warehouseId").GetString());
        Assert.Equal([s.BinA.ToString("D")], recount.GetProperty("scopeLocationIds").EnumerateArray().Select(x => x.GetString()).ToList());
        Assert.Equal(new[] { s.Inventory.Sku.ToString("D"), found.Sku.ToString("D") }.Order(),
            recount.GetProperty("scopeSkuIds").EnumerateArray().Select(x => x.GetString()!).Order());
        Assert.Equal(assignee.ToString("D"), recount.GetProperty("assignedTo").GetString());
        Assert.Equal("ikinci sayim", recount.GetProperty("note").GetString());
        Assert.Matches("^GMG-CNT-\\d{6}$", recount.GetProperty("number").GetString());
        Assert.NotEqual(submitted.GetProperty("number").GetString(), recount.GetProperty("number").GetString());

        // exactly the two selected keys — the other SKU of the same bin is no line; the found key is a Recount line at expected 0
        var recountLines = await s.LinesAsync(Id(recount), s.ApproverToken);
        Assert.Equal(2, recountLines.Count);
        Assert.All(recountLines, l => Assert.Equal("Recount", l.GetProperty("origin").GetString()));
        Assert.All(recountLines, l => Assert.Equal(1, l.GetProperty("recountDepth").GetInt32()));
        // blind before its submit: no answer carries the expected quantity — read the stored evidence
        decimal Expected(Guid sku) => decimal.Parse(s.StoredLines(Id(recount)).Single(l => l["SkuId"].AsString == sku.ToString("D"))["ExpectedQuantity"].ToString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(5m, Expected(s.Inventory.Sku));
        Assert.Equal(0m, Expected(found.Sku));
        Assert.All(recountLines, l => Assert.False(l.TryGetProperty("expectedQuantity", out _)));
        Assert.Equal(LineId(foundLine), recountLines.Single(l => Sku(l) == found.Sku.ToString("D")).GetProperty("recountOfLineId").GetString());
        // blind: the Sayan (counts.read + counts.record) sees no expected quantity of the recount
        Assert.All(await s.LinesAsync(Id(recount)), l => Assert.False(l.TryGetProperty("expectedQuantity", out _)));

        // the source: the selected lines Recounted (the decision), the others untouched; BinA's claim moved, BinB's stays with the source
        var source = await s.LinesAsync(countId, s.ApproverToken);
        Assert.Equal("Recounted", Status(source.Single(l => LineId(l) == LineId(mainA))));
        Assert.Equal("Recount", source.Single(l => LineId(l) == LineId(mainA)).GetProperty("decision").GetProperty("kind").GetString());
        Assert.Equal("AwaitingApproval", Status(LineOf(source, s.BinB)));
        var moved = Assert.Single(s.Claims(Id(recount)));
        Assert.Equal(s.BinA.ToString("D"), moved["BinId"].AsString);
        Assert.Equal(countId, moved["TransferredFromCountId"].AsString);
        Assert.True(Assert.Single(s.Claims(countId))["Open"].AsBoolean);
        Assert.Single(s.CentralRows("RequestCountRecountCommand"));

        // AC-13 (e): an unselected source line of the same bin still takes its decision; the source closes and keeps only its own claims
        var submittedNow = await s.CurrentAsync(countId);
        Assert.Equal("Submitted", Status(submittedNow));
        await Expect(s.RejectAsync(LineOf(source, s.BinB)), 200);
        Assert.Equal("Closed", Status(await s.CurrentAsync(countId)));
        Assert.All(s.Claims(countId), c => Assert.False(c["Open"].AsBoolean));
        Assert.True(Assert.Single(s.Claims(Id(recount)))["Open"].AsBoolean);
        Assert.Equal(0, s.CountMovements());
    }

    private static Guid LocationOf(JsonElement line) => Guid.Parse(line.GetProperty("locationId").GetString()!);

    [Fact]
    public async Task AC32_the_chain_stops_at_MaxRecounts_for_the_whole_request()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 9);
        var current = (CountId: countId, Line: line);
        for (var depth = 1; depth <= 2; depth++)
        {
            var recount = await Expect(s.RecountAsync(current.CountId, ETag(await s.CurrentAsync(current.CountId)), [LineId(current.Line)]), 201);
            var recountLine = Assert.Single(await s.LinesAsync(Id(recount)));
            Assert.Equal(depth, recountLine.GetProperty("recountDepth").GetInt32());
            await s.CountedAsync(Id(recount), recountLine, 8);
            await s.SubmittedAsync(Id(recount));
            current = (Id(recount), await s.LineAsync(Id(recount), LineId(recountLine)));
        }

        var limit = await Expect(s.RecountAsync(current.CountId, ETag(await s.CurrentAsync(current.CountId)), [LineId(current.Line)]), 409);
        Assert.Equal("COUNT_RECOUNT_LIMIT", limit.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("AwaitingApproval", Status(await s.LineAsync(current.CountId, LineId(current.Line))));
        Assert.Equal(3, s.Stored(Diten.InventoryService.Persistence.CycleCounting.CycleCountCollections.Counts).Count); // no third recount was opened
    }

    [Fact]
    public async Task AC32_r6_an_unselected_lot_of_the_same_bin_and_SKU_is_out_of_scope_in_the_recount_and_a_later_lot_is_not()
    {
        var s = new CycleCountScenario(factory, lotControlled: true);
        var lot1 = s.RegisterLot(s.Inventory.Item, s.Inventory.Sku);
        var lot2 = s.RegisterLot(s.Inventory.Item, s.Inventory.Sku);
        var lot3 = s.RegisterLot(s.Inventory.Item, s.Inventory.Sku);
        await s.ReceiveAsync(5, s.BinA, lot: lot1);
        await s.ReceiveAsync(4, s.BinA, lot: lot2);
        var released = await s.ReleasedAsync(await s.CreatedAsync());
        var countId = Id(released);
        var lines = await s.LinesAsync(countId);
        await s.CountedAsync(countId, lines.Single(l => l.GetProperty("lotId").GetString() == lot1), 6);
        await s.CountedAsync(countId, lines.Single(l => l.GetProperty("lotId").GetString() == lot2), 4);
        var submitted = await s.SubmittedAsync(countId);
        var lot1Line = (await s.LinesAsync(countId, s.ApproverToken)).Single(l => l.GetProperty("lotId").GetString() == lot1);

        var recount = await Expect(s.RecountAsync(countId, ETag(submitted), [LineId(lot1Line)]), 201);
        Assert.Single(await s.LinesAsync(Id(recount)));
        var excluded = s.StoredCount(Id(recount))["RecountExcludedKeys"].AsBsonArray;
        Assert.Equal(lot2, Assert.Single(excluded)["LotId"].AsString);

        // lot-2 had stock in the recount's snapshot but was not selected → a found line would drift forever
        Assert.Equal("COUNT_LINE_OUT_OF_SCOPE", await ErrorCode(s.AddLineAsync(Id(recount), s.FoundLine(s.BinA, lot: lot2, quantity: 4)), 422));
        // lot-3 is born AFTER the snapshot by a movement: a found line, and its submit has no drift
        await s.ReceiveAsync(1, s.BinA, lot: lot3);
        await Expect(s.AddLineAsync(Id(recount), s.FoundLine(s.BinA, lot: lot3, quantity: 1)), 201);
        await s.CountedAsync(Id(recount), (await s.LinesAsync(Id(recount))).Single(l => l.GetProperty("origin").GetString() == "Recount"), 6);
        await s.SubmittedAsync(Id(recount));
        Assert.All(await s.LinesAsync(Id(recount), s.ApproverToken), l => Assert.False(l.GetProperty("snapshotDrift").GetBoolean()));
    }

    [Fact]
    public async Task AC13e_the_recount_takes_over_its_sources_bin_claim_a_third_count_cannot_take_the_bin()
    {
        var s = new CycleCountScenario(factory);
        var (countId, line) = await s.SubmittedLineAsync(10, 9);
        var recount = await Expect(s.RecountAsync(countId, ETag(await s.CurrentAsync(countId)), [LineId(line)]), 201);
        Assert.Equal(Id(recount), Assert.Single(s.Claims(Id(recount)))["CountId"].AsString);
        Assert.Empty(s.Claims(countId).Where(c => c["Open"].AsBoolean));

        var third = await s.CreatedAsync();
        Assert.Equal("COUNT_SCOPE_OVERLAP", await ErrorCode(s.ReleaseAsync(Id(third), ETag(third)), 409));
    }
}
