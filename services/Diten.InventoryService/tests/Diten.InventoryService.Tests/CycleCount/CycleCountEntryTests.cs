using System.Text.Json;
using Diten.InventoryService.Persistence.CycleCounting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-2 — quantity entry (AC-14) and found stock (AC-16), with their idempotency (AC-8) and the line's If-Match (AC-9), over the REAL host
/// and the REAL ledger; every answer checked against the WMS-COUNT-BUNDLE draft (§4.2).
/// </summary>
public sealed class CycleCountEntryTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    private static readonly string[] HiddenQuantities = ["expectedQuantity", "movementsSinceSnapshot", "expectedAtCount", "varianceQuantity"];

    private async Task<(CycleCountScenario S, JsonElement Count, JsonElement Line)> ReleasedWithOneLineAsync(bool blind = false)
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(blind: blind)));
        return (s, released, Assert.Single(await s.LinesAsync(Id(released))));
    }

    // ── AC-14 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC14_an_entry_counts_the_line_with_the_server_user_and_clock_and_the_first_entry_starts_Counting()
    {
        var (s, released, line) = await ReleasedWithOneLineAsync();
        Assert.Equal("Open", line.GetProperty("status").GetString());
        var before = s.Factory.CountClock.GetUtcNow().AddSeconds(-1);

        var counted = await Expect(s.RecordAsync(Id(released), LineId(line), "4.5", ETag(line), scanRef: "SCAN-0001"), 200);
        Assert.Equal("Counted", counted.GetProperty("status").GetString());
        Assert.Equal("4.500", counted.GetProperty("countedQuantity").GetString());
        Assert.Equal(s.User.ToString("D"), counted.GetProperty("countedBy").GetString());
        var countedAt = DateTimeOffset.Parse(counted.GetProperty("countedAt").GetString()!);
        Assert.InRange(countedAt, before, s.Factory.CountClock.GetUtcNow().AddSeconds(1));
        Assert.Equal("SCAN-0001", counted.GetProperty("scanRef").GetString());
        Assert.Equal(2, counted.GetProperty("version").GetInt32());
        Assert.Equal("Counting", (await s.CurrentAsync(Id(released))).GetProperty("status").GetString());

        // re-entry until submit, with the line's new ETag; a stale one is 409 (AC-9) and writes nothing
        var again = await Expect(s.RecordAsync(Id(released), LineId(line), "5", ETag(counted)), 200);
        Assert.Equal("5.000", again.GetProperty("countedQuantity").GetString());
        Assert.Equal("COUNT_CONFLICT", await ErrorCode(s.RecordAsync(Id(released), LineId(line), "6", ETag(counted)), 409));
        Assert.Equal(5m, decimal.Parse(s.StoredLines(Id(released)).Single()["CountedQuantity"].ToString()!, System.Globalization.CultureInfo.InvariantCulture));

        var entries = s.Events(Id(released)).Where(e => e["Kind"].AsString == "LineCounted").ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("Counting", entries[0]["Data"]["countStatus"].AsString);
        Assert.False(entries[1]["Data"].AsBsonDocument.Contains("countStatus"));
        Assert.Equal("4.500", entries[1]["Data"]["previousCountedQuantity"].AsString);
        Assert.All(entries, e => Assert.DoesNotContain(e["Data"].AsBsonDocument.Names, n => HiddenQuantities.Contains(n)));
        Assert.Equal(2, s.CentralRows("RecordCountLineCommand").Count);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.1234567")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(" 1")]
    [InlineData("1e3")]
    [InlineData("+1")]
    public async Task AC14_a_negative_seven_decimal_or_non_number_quantity_is_400_and_writes_nothing(string raw)
    {
        var (s, released, line) = await ReleasedWithOneLineAsync();
        Assert.Equal("COUNT_QUANTITY_INVALID", await ErrorCode(s.RecordAsync(Id(released), LineId(line), raw, ETag(line)), 400));
        Assert.Equal("COUNT_QUANTITY_INVALID", await ErrorCode(s.RecordAsync(Id(released), LineId(line), 5, ETag(line)), 400)); // a JSON number
        Assert.Equal("COUNT_QUANTITY_INVALID", await ErrorCode(s.RecordAsync(Id(released), LineId(line), null, ETag(line)), 400)); // absent

        var stored = s.StoredLines(Id(released)).Single();
        Assert.Equal("Open", stored["Status"].AsString);
        Assert.Equal(1, stored["Version"].AsInt32);
        Assert.Equal("Released", (await s.CurrentAsync(Id(released))).GetProperty("status").GetString());
        Assert.DoesNotContain(s.Events(Id(released)), e => e["Kind"].AsString == "LineCounted");
        Assert.Empty(s.CentralRows("RecordCountLineCommand"));
    }

    [Fact]
    public async Task AC14_who_and_when_never_come_from_the_body_and_a_Draft_or_Submitted_count_takes_no_entry()
    {
        var (s, released, line) = await ReleasedWithOneLineAsync();
        var key = NewKey();
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key, ["countedQuantity"] = "5", ["countedBy"] = Guid.NewGuid().ToString("D"), ["countedAt"] = "2026-01-01T00:00:00.000Z"
        };
        Assert.Equal("INVALID_REQUEST",
            await ErrorCode(s.Send(HttpMethod.Post, $"/api/warehouse/cycle-counts/{Id(released)}/lines/{LineId(line)}/count", body, key, ETag(line)), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.RecordAsync(Id(released), LineId(line), "5", null!), 400)); // no If-Match
        Assert.Equal("COUNT_LINE_NOT_FOUND", await ErrorCode(s.RecordAsync(Id(released), Guid.NewGuid().ToString("D"), "5", ETag(line)), 404));

        var draft = await s.CreatedAsync(s.Form(scope: [s.BinB]));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.RecordAsync(Id(draft), LineId(line), "5", ETag(line)), 409));

        await s.CountedAsync(Id(released), line, 5);
        await s.SubmittedAsync(Id(released));
        var after = Assert.Single(await s.LinesAsync(Id(released)));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.RecordAsync(Id(released), LineId(after), "6", ETag(after)), 409));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, s.AddSku())), 409));
    }

    // ── AC-16 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC16_found_stock_on_a_scope_bin_is_a_Found_line_with_expected_zero_born_counted_and_a_key_only_once()
    {
        var (s, released, _) = await ReleasedWithOneLineAsync();
        var sku = s.AddSku();

        var found = await Expect(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, sku, 3)), 201);
        Assert.Equal("Found", found.GetProperty("origin").GetString());
        Assert.Equal("0.000", found.GetProperty("expectedQuantity").GetString());
        Assert.Equal("3.000", found.GetProperty("countedQuantity").GetString());
        Assert.Equal("Counted", found.GetProperty("status").GetString());
        Assert.Equal("AVAILABLE", found.GetProperty("stockStatus").GetString());
        Assert.Equal(s.User.ToString("D"), found.GetProperty("countedBy").GetString());
        var count = await s.CurrentAsync(Id(released));
        Assert.Equal("Counting", count.GetProperty("status").GetString());
        Assert.Equal(2, count.GetProperty("lineCount").GetInt32());

        // the same key again (found or snapshot) → 409, nothing written
        Assert.Equal("COUNT_LINE_DUPLICATE", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, sku, 1)), 409));
        Assert.Equal("COUNT_LINE_DUPLICATE", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA)), 409));
        // a bin outside the scope → 422
        Assert.Equal("COUNT_LINE_OUT_OF_SCOPE", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinB, sku)), 422));

        Assert.Equal(2, s.StoredLines(Id(released)).Count);
        var added = Assert.Single(s.Events(Id(released)), e => e["Kind"].AsString == "LineAdded");
        Assert.Equal(LineId(found), added["LineId"].AsString);
        Assert.DoesNotContain(added["Data"].AsBsonDocument.Names, n => HiddenQuantities.Contains(n));
        Assert.Single(s.CentralRows("AddCountLineCommand"));
    }

    [Fact]
    public async Task AC16_with_a_SKU_filter_a_found_SKU_outside_it_is_out_of_scope_and_writes_nothing()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(2, s.BinA);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(skus: [s.Inventory.Sku])));
        var other = s.AddSku();

        Assert.Equal("COUNT_LINE_OUT_OF_SCOPE", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, other)), 422));
        Assert.Single(s.StoredLines(Id(released)));
        Assert.Equal("Released", (await s.CurrentAsync(Id(released))).GetProperty("status").GetString());
        Assert.DoesNotContain(s.Events(Id(released)), e => e["Kind"].AsString == "LineAdded");
    }

    [Fact]
    public async Task AC16_a_lot_controlled_SKU_needs_a_lot_registered_in_MOD_0174_for_it_and_an_unknown_lot_writes_no_line()
    {
        var s = new CycleCountScenario(factory);
        var released = await s.ReleasedAsync(await s.CreatedAsync()); // BinA, no stock: no snapshot line
        var lotSku = s.AddSku(lotControlled: true);
        var otherSku = s.AddSku(lotControlled: true);
        var plain = s.AddSku();

        Assert.Equal("COUNT_LOT_REQUIRED", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, lotSku)), 400));
        Assert.Equal("COUNT_LOT_UNKNOWN", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, lotSku, lot: Guid.NewGuid().ToString("D"))), 422));
        Assert.Equal("COUNT_LOT_UNKNOWN", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, lotSku, lot: "LOT-TYPED-0001")), 422));
        Assert.Equal("COUNT_LOT_UNKNOWN",
            await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, lotSku, lot: s.RegisterLot(otherSku.Item, otherSku.Sku))), 422));
        Assert.Equal("LOT_NOT_ALLOWED", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, plain, lot: s.RegisterLot(plain.Item, plain.Sku))), 422));
        Assert.Empty(s.StoredLines(Id(released)));
        Assert.DoesNotContain(s.Events(Id(released)), e => e["Kind"].AsString == "LineAdded");

        var lot = s.RegisterLot(lotSku.Item, lotSku.Sku);
        var found = await Expect(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, lotSku, 2, lot)), 201);
        Assert.Equal(lot, found.GetProperty("lotId").GetString());
        Assert.Single(s.StoredLines(Id(released)));
    }

    // ── AC-8 / AC-11 (DL-2 writes) ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC8_entry_found_line_and_submit_replay_with_their_key_and_refuse_another_body()
    {
        var (s, released, line) = await ReleasedWithOneLineAsync();

        var key = NewKey();
        var first = await Expect(s.RecordAsync(Id(released), LineId(line), "5", ETag(line), key), 200);
        var replay = await Expect(s.RecordAsync(Id(released), LineId(line), "5", ETag(line), key), 200);
        Assert.Equal(first.GetProperty("version").GetInt32(), replay.GetProperty("version").GetInt32());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.RecordAsync(Id(released), LineId(line), "6", ETag(line), key), 400));

        var addKey = NewKey();
        var sku = s.AddSku();
        var added = await Expect(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, sku, 1), addKey), 201);
        var addReplay = await Expect(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, sku, 1), addKey), 200);
        Assert.Equal(LineId(added), LineId(addReplay));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, sku, 2), addKey), 400));

        var submitKey = NewKey();
        var current = await s.CurrentAsync(Id(released));
        var submitted = await Expect(s.SubmitAsync(Id(released), ETag(current), submitKey), 200);
        var submitReplay = await Expect(s.SubmitAsync(Id(released), ETag(current), submitKey), 200); // the key answers before If-Match
        Assert.Equal(submitted.GetProperty("version").GetInt32(), submitReplay.GetProperty("version").GetInt32());

        Assert.Single(s.Events(Id(released)), e => e["Kind"].AsString == "LineCounted");
        Assert.Single(s.Events(Id(released)), e => e["Kind"].AsString == "LineAdded");
        Assert.Single(s.Events(Id(released)), e => e["Kind"].AsString == "Submitted");
        Assert.Equal(2, s.StoredLines(Id(released)).Count);
        Assert.Single(s.CentralRows("RecordCountLineCommand"));
        Assert.Single(s.CentralRows("AddCountLineCommand"));
        Assert.Single(s.CentralRows("SubmitCycleCountCommand"));
    }
}
