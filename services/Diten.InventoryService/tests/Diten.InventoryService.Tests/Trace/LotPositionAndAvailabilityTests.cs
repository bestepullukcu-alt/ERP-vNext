using System.Net.Http.Headers;
using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// WP-0174-P2 — K-A per-lot stock and CT-D1 (a), measured on the REAL service and Mongo:
/// <list type="bullet">
/// <item><c>inventory_lot_positions</c> = Σ balance OnHand of the lot (all statuses) after ANY posting sequence — random,
/// concurrent, and after a restart; written in the posting transaction (a posting that aborts after the write leaves it
/// unchanged);</item>
/// <item>a Blocked lot: balance rows keep their stock status, availability and the reservation guard lose the lot's AVAILABLE
/// stock, unblocking brings it back; the SKU position always equals the oracle (Σ AVAILABLE OnHand of the lots that do not
/// hold their stock) — including across a restart (startup rebuild) and under a posting-vs-block race (§13 #12).</item>
/// </list>
/// </summary>
public sealed class LotPositionAndAvailabilityTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public LotPositionAndAvailabilityTests(TraceApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    // ── CT-D1 (a) ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Blocking_a_lot_drops_availability_refuses_the_reservation_and_unblocking_restores_it_rows_unchanged()
    {
        var s = new LotLedgerScenario(_factory);
        var blocked = await s.CreateLotAsync();
        var free = await s.CreateLotAsync();
        await s.ReleaseAsync(blocked);
        await s.ReleaseAsync(free);
        await s.PostOkAsync(s.Receipt("30", blocked));
        await s.PostOkAsync(s.Receipt("10", free, location: s.Ledger.LocationB));
        await s.PostOkAsync(s.Receipt("5", blocked, toStatus: "QUALITY_INSPECTION"));
        Assert.Equal(40m, await s.AvailableAsync());
        var rowsBefore = s.Persisted("inventory_balances").Select(b => $"{b["LotId"]}|{b["StockStatus"]}|{b["OnHand"]}").OrderBy(x => x).ToList();

        await s.BlockAsync(blocked);

        var availability = await s.Ledger.ReadAsync(await s.Ledger.GetAsync($"/api/inventory/availability?skuId={s.Ledger.Sku}"));
        Assert.Equal("10.000", availability.GetProperty("available").GetString());
        Assert.Equal("45.000", availability.GetProperty("onHand").GetString()); // onHand and byStatus keep every row
        Assert.Equal("40.000", availability.GetProperty("byStatus").GetProperty("AVAILABLE").GetString());
        Assert.Equal(rowsBefore, s.Persisted("inventory_balances").Select(b => $"{b["LotId"]}|{b["StockStatus"]}|{b["OnHand"]}").OrderBy(x => x).ToList());
        Assert.Equal(10m, s.SkuPositionAvailableOnHand());
        Assert.Equal(s.ExpectedSkuAvailableOnHand(), s.SkuPositionAvailableOnHand());

        var refused = await s.ReserveAsync("11", 422); // the blocked lot's 30 cannot be reserved
        Assert.Equal("INSUFFICIENT_AVAILABLE", refused.GetProperty("error").GetProperty("code").GetString());

        await s.ReleaseAsync(blocked); // Blocked → Released
        Assert.Equal(40m, await s.AvailableAsync());
        Assert.Equal(40m, s.SkuPositionAvailableOnHand());
        await s.ReserveAsync("11", 201);
        Assert.Equal(rowsBefore, s.Persisted("inventory_balances").Select(b => $"{b["LotId"]}|{b["StockStatus"]}|{b["OnHand"]}").OrderBy(x => x).ToList());
    }

    [Fact]
    public async Task Scrapping_out_of_a_Blocked_lot_leaves_the_SKU_position_alone_and_the_oracle_holds()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync();
        var free = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("12", lot));
        await s.PostOkAsync(s.Receipt("4", free));
        await s.BlockAsync(lot, expected: 422); // Draft → Blocked is not a transition (K-E)
        await s.ReleaseAsync(lot);
        await s.BlockAsync(lot);

        await s.PostOkAsync(s.Issue("5", lot, movementType: "SCRAP"));
        Assert.Equal(4m, s.SkuPositionAvailableOnHand());
        Assert.Equal(s.ExpectedSkuAvailableOnHand(), s.SkuPositionAvailableOnHand());
        await s.ReleaseAsync(lot);
        Assert.Equal(11m, s.SkuPositionAvailableOnHand());
        Assert.Equal(11m, await s.AvailableAsync());
    }

    // ── K-A ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(17)]
    [InlineData(4242)]
    [InlineData(90210)]
    public async Task The_lot_position_equals_the_balance_rows_after_a_random_posting_sequence(int seed)
    {
        var random = new Random(seed);
        var s = new LotLedgerScenario(_factory);
        var lots = new List<string> { await s.CreateLotAsync(), await s.CreateLotAsync(), await s.CreateLotAsync() };
        foreach (var lot in lots) await s.ReleaseAsync(lot);
        var blocked = new HashSet<string>();
        var posted = new List<(Dictionary<string, object?> Body, string Id)>();
        string[] locations = [s.Ledger.LocationA, s.Ledger.LocationB, s.Ledger.LocationC];
        int ok = 0, refused = 0;

        for (var step = 0; step < 60; step++)
        {
            var lot = lots[random.Next(lots.Count)];
            var quantity = random.Next(1, 9).ToString();
            var location = locations[random.Next(locations.Length)];
            Dictionary<string, object?> body;
            switch (random.Next(10))
            {
                case 0 or 1 or 2 or 3: body = s.Receipt(quantity, lot, location: location, toStatus: random.Next(4) == 0 ? "QUALITY_INSPECTION" : null); break;
                case 4: body = s.Issue(quantity, lot, location: location); break;
                case 5: body = s.Transfer(quantity, lot); break;
                case 6: body = s.StatusTransfer(quantity, lot); break;
                case 7: body = s.Issue(quantity, lot, location: location, movementType: "SCRAP"); break;
                case 8 when posted.Count > 0:
                    var (original, originalId) = posted[random.Next(posted.Count)];
                    body = s.Reversal(original, originalId);
                    break;
                default:
                    if (blocked.Remove(lot)) await s.ReleaseAsync(lot);
                    else { await s.BlockAsync(lot); blocked.Add(lot); }
                    AssertPositions(s, lots);
                    continue;
            }

            using var response = await s.Ledger.PostAsync(body, token: s.Token);
            var json = await s.Ledger.ReadAsync(response);
            if ((int)response.StatusCode == 201)
            {
                ok++;
                if ((string)body["movementType"]! != "REVERSAL") posted.Add((body, json.GetProperty("transactionId").GetString()!));
            }
            else
            {
                refused++;
                Assert.True((int)response.StatusCode is 422, $"step {step}: {(int)response.StatusCode} {json}");
                Assert.Contains(json.GetProperty("error").GetProperty("code").GetString(),
                    new[] { "INSUFFICIENT_STOCK", "LOT_BLOCKED", "ALREADY_REVERSED" });
            }

            AssertPositions(s, lots);
        }

        _output.WriteLine($"seed {seed}: {ok} posted, {refused} refused (insufficient / blocked / already reversed)");
        Assert.True(ok >= 10, "the sequence posted too little to mean anything");
    }

    [Fact]
    public async Task Concurrent_postings_keep_the_lot_position_equal_to_the_balance_rows()
    {
        var s = new LotLedgerScenario(_factory);
        var lots = new List<string> { await s.CreateLotAsync(), await s.CreateLotAsync() };
        foreach (var lot in lots) await s.PostOkAsync(s.Receipt("50", lot));

        var calls = Enumerable.Range(0, 24).Select(i =>
        {
            var lot = lots[i % 2];
            var body = (i % 3) switch
            {
                0 => s.Receipt("3", lot, location: s.Ledger.LocationB),
                1 => s.Issue("2", lot),
                _ => s.Transfer("1", lot)
            };
            return s.Ledger.PostAsync(body, token: s.Token);
        }).ToList();
        var responses = await Task.WhenAll(calls);
        Assert.All(responses, r => Assert.Equal(201, (int)r.StatusCode));

        AssertPositions(s, lots);
        Assert.Equal(50m + 4 * 3 - 4 * 2, s.LotPositionOnHand(lots[0]));
    }

    [Fact]
    public async Task A_posting_that_aborts_after_the_lot_position_write_leaves_the_position_unchanged()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("10", lot));
        await s.ReserveAsync("8", 201);

        // The row covers 5 (reservations are SKU-level), the lot position is written, then the S2 SKU guard refuses
        // (10 − 8 < 5) and the whole transaction aborts.
        var json = await s.PostAsync(s.Issue("5", lot), 422);
        Assert.Equal("INSUFFICIENT_STOCK", json.GetProperty("error").GetProperty("code").GetString());

        Assert.Equal(10m, s.LotPositionOnHand(lot));
        Assert.Equal(10m, s.BalanceOnHandOfLot(lot));
    }

    [Fact]
    public async Task The_lot_list_and_detail_read_stockSummary_from_the_position_and_it_survives_a_restart()
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync();
        var blocked = await s.CreateLotAsync();
        await s.ReleaseAsync(blocked);
        await s.PostOkAsync(s.Receipt("7", lot));
        await s.PostOkAsync(s.Receipt("2", lot, toStatus: "QUALITY_INSPECTION"));
        await s.PostOkAsync(s.Receipt("6", blocked));
        await s.BlockAsync(blocked);

        var list = await s.GetTraceAsync($"/api/trace/lots?skuId={s.Ledger.Sku}&includeStock=true&page=1&pageSize=50");
        var summary = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("lotId").GetString() == lot).GetProperty("stockSummary");
        Assert.Equal("9.000", summary.GetProperty("onHand").GetString());
        Assert.Equal("EA", summary.GetProperty("baseUomId").GetString());
        Assert.Equal(s.Ledger.LegalEntity.ToString("D"), summary.GetProperty("legalEntityId").GetString());
        var withoutFlag = await s.GetTraceAsync($"/api/trace/lots?skuId={s.Ledger.Sku}");
        Assert.All(withoutFlag.GetProperty("items").EnumerateArray(), i => Assert.False(i.TryGetProperty("stockSummary", out _)));

        // Restart: a second host on the same database (startup index gate + SKU-position rebuild run again).
        using var restarted = new TraceApiFactory();
        using var client = restarted.CreateContractClient();
        var detail = await GetJson(client, s.Token, $"/api/trace/lots/{lot}/detail");
        Assert.Equal("9.000", detail.GetProperty("stockSummary").GetProperty("onHand").GetString());
        var availability = await GetJson(client, s.Token, $"/api/inventory/availability?skuId={s.Ledger.Sku}");
        Assert.Equal("7.000", availability.GetProperty("available").GetString()); // the blocked lot stays out after the rebuild
        Assert.Equal(7m, s.SkuPositionAvailableOnHand());
        Assert.Equal(s.ExpectedSkuAvailableOnHand(), s.SkuPositionAvailableOnHand());
        AssertPositions(s, [lot, blocked]);
    }

    // ── §13 #12: posting vs block ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task A_posting_racing_a_block_either_commits_before_it_or_is_LOT_BLOCKED_never_into_the_Blocked_lot(int round)
    {
        var s = new LotLedgerScenario(_factory);
        var lot = await s.CreateLotAsync();
        var free = await s.CreateLotAsync();
        await s.ReleaseAsync(lot);
        await s.PostOkAsync(s.Receipt("60", lot));
        await s.PostOkAsync(s.Receipt("10", free));

        var issues = Enumerable.Range(0, 24).Select(_ => s.Ledger.PostAsync(s.Issue("1", lot), token: s.Token)).ToList();
        var block = Task.Run(async () =>
        {
            await Task.Delay(5 * round);
            return await s.BlockAsync(lot);
        });
        var responses = await Task.WhenAll(issues);
        await block;

        var posted = 0;
        foreach (var response in responses)
        {
            var json = await s.Ledger.ReadAsync(response);
            if ((int)response.StatusCode == 201) { posted++; continue; }
            Assert.Equal(422, (int)response.StatusCode);
            Assert.Equal("LOT_BLOCKED", json.GetProperty("error").GetProperty("code").GetString());
        }

        _output.WriteLine($"round {round}: {posted} issues committed before the block, {24 - posted} refused LOT_BLOCKED");
        Assert.Equal(60m - posted, s.BalanceOnHandOfLot(lot));
        Assert.Equal(60m - posted, s.LotPositionOnHand(lot));
        // Never "posted into a Blocked lot": a posting that committed after the block would have moved the SKU position
        // for a lot whose stock was already out of it — the position would differ from the oracle.
        Assert.Equal(s.ExpectedSkuAvailableOnHand(), s.SkuPositionAvailableOnHand());
        Assert.Equal(10m, s.SkuPositionAvailableOnHand());
        await s.RefusedAsync(s.Issue("1", lot), "LOT_BLOCKED");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static void AssertPositions(LotLedgerScenario s, IEnumerable<string> lots)
    {
        foreach (var lot in lots)
        {
            Assert.Equal(s.BalanceOnHandOfLot(lot), s.LotPositionOnHand(lot));
        }

        Assert.Equal(s.ExpectedSkuAvailableOnHand(), s.SkuPositionAvailableOnHand());
    }

    private static async Task<JsonElement> GetJson(HttpClient client, string token, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        var json = await TraceScenario.Read(response);
        Assert.True((int)response.StatusCode == 200, $"{path}: {(int)response.StatusCode} {json}");
        return json;
    }
}
