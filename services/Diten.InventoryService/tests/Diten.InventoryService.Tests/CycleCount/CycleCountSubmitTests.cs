using System.Text.Json;
using Diten.InventoryService.Persistence.CycleCounting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-2 — submit and the variance (pack §8.3, §8.4; AC-15, AC-17…AC-20, AC-52 (b) (c)) over the REAL host and the REAL ledger: stock and
/// movements are posted through the ledger API, the variance is read from what the submit wrote. Only MOD-0181's own clock is moved (AC-52 (b),
/// r5 SB1/F-3) — the ledger's <c>CreatedAt</c> is the system clock — and only inside one test, put back in <c>finally</c>.
/// </summary>
public sealed class CycleCountSubmitTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    private static readonly string[] HiddenQuantities = ["expectedQuantity", "movementsSinceSnapshot", "expectedAtCount", "varianceQuantity"];

    private static void HasNoHiddenQuantity(JsonElement line)
        => Assert.All(HiddenQuantities, name => Assert.False(line.TryGetProperty(name, out _), $"{name} is written: {line}"));

    private static void HasNoHiddenData(JsonElement e)
        => Assert.All(HiddenQuantities, name => Assert.False(e.GetProperty("data").TryGetProperty(name, out _), $"data.{name} is written: {e}"));

    // ── AC-15 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC15_before_submit_a_blind_count_writes_no_expected_movement_or_variance_member_anywhere_for_any_caller()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(blind: true)));
        var widest = s.TokenFor(s.User, Read, Create, Update, BulkDelete, Cancel, CountsRecord, VariancesRead);

        var line = Assert.Single(await s.LinesAsync(Id(released), widest));
        HasNoHiddenQuantity(line);
        var counted = await Expect(s.RecordAsync(Id(released), LineId(line), "4", ETag(line), token: widest), 200);
        HasNoHiddenQuantity(counted);
        var found = await Expect(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, s.AddSku()), token: widest), 201);
        HasNoHiddenQuantity(found);
        Assert.All(await s.LinesAsync(Id(released), widest), HasNoHiddenQuantity);
        var detail = await Expect(s.GetAsync($"/api/warehouse/cycle-counts/{Id(released)}", widest), 200);
        HasNoHiddenQuantity(detail);

        var events = await s.EventListAsync(Id(released), widest);
        Assert.All(events, HasNoHiddenData);
        Assert.Equal(["binCount", "claimedBinCount", "lineCount", "snapshotAt"],
            events.Single(e => e.GetProperty("kind").GetString() == "Released").GetProperty("data").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));

        // the Released event of a NON-blind count carries counts only as well — never a per-line expected quantity (F-13)
        var open = await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [s.BinB])));
        var stored = s.Events(Id(open)).Single(e => e["Kind"].AsString == "Released");
        Assert.Equal(["binCount", "claimedBinCount", "lineCount", "snapshotAt"], stored["Data"].AsBsonDocument.Names.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AC15_after_submit_a_blind_count_shows_the_quantities_only_to_a_variances_read_holder()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(blind: true)));
        await s.CountedAsync(Id(released), Assert.Single(await s.LinesAsync(Id(released))), 4);
        Assert.Equal("Submitted", (await s.SubmittedAsync(Id(released))).GetProperty("status").GetString());

        var counter = s.TokenFor(s.User, Read, CountsRecord);
        var counterLine = Assert.Single(await s.LinesAsync(Id(released), counter));
        HasNoHiddenQuantity(counterLine);
        Assert.Equal("AwaitingApproval", counterLine.GetProperty("status").GetString()); // the state is visible
        var counterVariance = (await s.EventListAsync(Id(released), counter)).Single(e => e.GetProperty("kind").GetString() == "VarianceComputed");
        HasNoHiddenData(counterVariance);
        Assert.Equal("4.000", counterVariance.GetProperty("data").GetProperty("countedQuantity").GetString());

        var holder = s.TokenFor(s.User, Read, VariancesRead);
        var holderLine = Assert.Single(await s.LinesAsync(Id(released), holder));
        Assert.Equal("5.000", holderLine.GetProperty("expectedQuantity").GetString());
        Assert.Equal("-1.000", holderLine.GetProperty("varianceQuantity").GetString());
        var holderVariance = (await s.EventListAsync(Id(released), holder)).Single(e => e.GetProperty("kind").GetString() == "VarianceComputed");
        Assert.Equal("5.000", holderVariance.GetProperty("data").GetProperty("expectedQuantity").GetString());
        Assert.Equal("-1.000", holderVariance.GetProperty("data").GetProperty("varianceQuantity").GetString());

        // the stored timeline row is never changed by the projection
        var stored = s.Events(Id(released)).Single(e => e["Kind"].AsString == "VarianceComputed");
        Assert.Equal("-1.000", stored["Data"]["varianceQuantity"].AsString);
    }

    // ── AC-17 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC17_submit_needs_every_Snapshot_and_Recount_line_counted_a_Found_line_never_blocks_it()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(3, s.BinA);
        await s.ReceiveAsync(2, s.BinB);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [s.BinA, s.BinB])));
        var lines = await s.LinesAsync(Id(released));
        Assert.Equal(2, lines.Count);

        var refused = await Expect(s.SubmitAsync(Id(released), ETag(released)), 422);
        Assert.Equal("COUNT_LINES_UNCOUNTED", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(2, refused.GetProperty("error").GetProperty("details").GetProperty("uncountedLineCount").GetInt32());
        var unchanged = await s.CurrentAsync(Id(released));
        Assert.Equal("Released", unchanged.GetProperty("status").GetString());
        Assert.Equal(released.GetProperty("version").GetInt32(), unchanged.GetProperty("version").GetInt32());

        await s.CountedAsync(Id(released), LineOf(lines, s.BinA), 3);
        await Expect(s.AddLineAsync(Id(released), s.FoundLine(s.BinA, s.AddSku(), 1)), 201); // born counted
        // r6 (SB2/F-1): the bin-B line becomes a Recount line (DL-3 writes such lines; staged here) — still uncounted, still blocking
        await factory.Collection(CycleCountCollections.Lines).UpdateOneAsync(new BsonDocument("_id", LineId(LineOf(lines, s.BinB))),
            new BsonDocument("$set", new BsonDocument { ["Origin"] = "Recount", ["RecountDepth"] = 1 })); // DL-3: a recount line is at depth ≥ 1
        Assert.Equal("COUNT_LINES_UNCOUNTED", await ErrorCode(s.SubmitAsync(Id(released), ETag(await s.CurrentAsync(Id(released)))), 422));
        Assert.Equal("Counting", (await s.CurrentAsync(Id(released))).GetProperty("status").GetString());
        Assert.DoesNotContain(s.Events(Id(released)), e => e["Kind"].AsString == "Submitted");
        Assert.All(s.StoredLines(Id(released)), l => Assert.True(l["VarianceQuantity"].IsBsonNull));

        await s.CountedAsync(Id(released), LineOf(await s.LinesAsync(Id(released)), s.BinB), 2);
        var submitted = await s.SubmittedAsync(Id(released));
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString()); // the found line has a variance: not every line is final
        Assert.Equal(s.User.ToString("D"), submitted.GetProperty("submittedBy").GetString());
        Assert.True(submitted.TryGetProperty("submittedAt", out _));
        var after = await s.LinesAsync(Id(released));
        Assert.All(after, l => Assert.True(l.TryGetProperty("varianceQuantity", out _)));
        Assert.Equal("NoVariance", LineOf(after, s.BinA, s.Inventory.Sku).GetProperty("status").GetString());
        Assert.Equal("NoVariance", LineOf(after, s.BinB).GetProperty("status").GetString());
        Assert.Single(after, l => l.GetProperty("status").GetString() == "AwaitingApproval" && l.GetProperty("origin").GetString() == "Found");
        Assert.Equal(3, s.Events(Id(released)).Count(e => e["Kind"].AsString == "VarianceComputed"));
        Assert.Single(s.CentralRows("SubmitCycleCountCommand"));
        Assert.All(s.Claims(Id(released)), c => Assert.True(c["Open"].AsBoolean)); // Submitted keeps the bins
    }

    // ── AC-18 ────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(3, "0.000")]
    [InlineData(1, "-2.000")]
    public async Task AC18_a_movement_after_the_snapshot_moves_the_expected_at_count_and_a_snapshot_movement_is_not_counted_twice(int countedAbove, string variance)
    {
        var s = new CycleCountScenario(factory);
        var receipt = await s.ReceiveAsync(5, s.BinA);
        await s.ReceiveAsync(10, s.BinB);
        var released = await s.ReleasedAsync(await s.CreatedAsync());
        Assert.Contains(MovementId(receipt).ToString("D"), s.StoredCount(Id(released))["SnapshotTransactionIds"].AsBsonArray.Select(x => x.AsString));

        await s.TransferAsync(3, s.BinB, s.BinA); // after the snapshot, before the entry
        await s.CountedAsync(Id(released), Assert.Single(await s.LinesAsync(Id(released))), 5 + countedAbove);
        await s.SubmittedAsync(Id(released));

        var line = Assert.Single(await s.LinesAsync(Id(released)));
        Assert.Equal("5.000", line.GetProperty("expectedQuantity").GetString());
        Assert.Equal("3.000", line.GetProperty("movementsSinceSnapshot").GetString());
        Assert.Equal("8.000", line.GetProperty("expectedAtCount").GetString());
        Assert.Equal(variance, line.GetProperty("varianceQuantity").GetString());
        Assert.False(line.GetProperty("snapshotDrift").GetBoolean());
        Assert.Equal(0, s.CountMovements());
    }

    // ── AC-19 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC19_a_movement_on_the_key_during_the_count_flags_the_line_and_it_awaits_a_decision_even_at_variance_zero()
    {
        var s = new CycleCountScenario(factory);
        var quiet = s.Node();
        await s.ReceiveAsync(4, s.BinA);
        await s.ReceiveAsync(6, quiet);
        await s.ReceiveAsync(5, s.BinB);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [s.BinA, quiet])));

        await s.TransferAsync(2, s.BinB, s.BinA); // touches the key …
        await s.TransferAsync(2, s.BinA, s.BinB); // … and nets to zero
        var lines = await s.LinesAsync(Id(released));
        await s.CountedAsync(Id(released), LineOf(lines, s.BinA), 4);
        await s.CountedAsync(Id(released), LineOf(lines, quiet), 6);
        Assert.Equal("Submitted", (await s.SubmittedAsync(Id(released))).GetProperty("status").GetString());

        var after = await s.LinesAsync(Id(released));
        var touched = LineOf(after, s.BinA);
        Assert.Equal("0.000", touched.GetProperty("varianceQuantity").GetString());
        Assert.Equal("0.000", touched.GetProperty("movementsSinceSnapshot").GetString());
        Assert.True(touched.GetProperty("movementDuringCount").GetBoolean());
        Assert.False(touched.GetProperty("snapshotDrift").GetBoolean());
        Assert.Equal("AwaitingApproval", touched.GetProperty("status").GetString());
        var still = LineOf(after, quiet);
        Assert.False(still.GetProperty("movementDuringCount").GetBoolean());
        Assert.Equal("NoVariance", still.GetProperty("status").GetString());
    }

    // ── AC-20 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC20_an_unreadable_ledger_is_503_and_the_count_and_its_lines_stay_as_they_were()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(3, s.BinA);
        var released = await s.ReleasedAsync(await s.CreatedAsync());
        await s.CountedAsync(Id(released), Assert.Single(await s.LinesAsync(Id(released))), 3);
        var before = await s.CurrentAsync(Id(released));
        var linesBefore = s.StoredLines(Id(released)).Select(l => l.ToJson()).ToList();
        var eventsBefore = s.Events(Id(released)).Count;

        factory.LedgerReadDown[s.Tenant] = true;
        try
        {
            Assert.Equal("COUNT_INVENTORY_UNAVAILABLE", await ErrorCode(s.SubmitAsync(Id(released), ETag(before)), 503));
        }
        finally
        {
            factory.LedgerReadDown.TryRemove(s.Tenant, out _);
        }

        var after = await s.CurrentAsync(Id(released));
        Assert.Equal("Counting", after.GetProperty("status").GetString());
        Assert.Equal(before.GetProperty("version").GetInt32(), after.GetProperty("version").GetInt32());
        Assert.Equal(linesBefore, s.StoredLines(Id(released)).Select(l => l.ToJson()).ToList());
        Assert.Equal(eventsBefore, s.Events(Id(released)).Count);
        Assert.Empty(s.CentralRows("SubmitCycleCountCommand"));

        // readable again: the same request submits — every line NoVariance → the count closes and frees its bins
        var closed = await Expect(s.SubmitAsync(Id(released), ETag(before)), 200);
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.All(s.Claims(Id(released)), c => Assert.False(c["Open"].AsBoolean));
        Assert.Equal(["Submitted", "VarianceComputed", "Closed"], s.Events(Id(released)).Skip(eventsBefore).Select(e => e["Kind"].AsString));
    }

    // ── AC-52 (b) (c) ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The kurgu of AC-52 (b) / (c): MOD-0181's clock is moved past W BEFORE the release, so a receipt posted after the release (system clock) has a
    /// <c>CreatedAt</c> older than <c>SnapshotAt − W</c>: the snapshot did not see it and the window read does not either — only the balance does.
    /// </summary>
    private async Task<(CycleCountScenario S, JsonElement Count)> LateCommitAsync(decimal counted)
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        factory.CountClock.Advance(TimeSpan.FromMinutes(15));
        try
        {
            var released = await s.ReleasedAsync(await s.CreatedAsync());
            await s.ReceiveAsync(2, s.BinA);
            await s.CountedAsync(Id(released), Assert.Single(await s.LinesAsync(Id(released))), counted);
            return (s, await s.SubmittedAsync(Id(released)));
        }
        finally
        {
            factory.CountClock.Advance(TimeSpan.FromMinutes(-15));
        }
    }

    [Fact]
    public async Task AC52b_a_commit_older_than_the_window_breaks_the_balance_equation_and_the_line_drifts_instead_of_a_wrong_variance()
    {
        var (s, submitted) = await LateCommitAsync(7);
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        var line = Assert.Single(await s.LinesAsync(Id(submitted)));
        Assert.True(line.GetProperty("snapshotDrift").GetBoolean());
        Assert.Equal("0.000", line.GetProperty("movementsSinceSnapshot").GetString());
        Assert.Equal("2.000", line.GetProperty("varianceQuantity").GetString());
        Assert.False(line.GetProperty("movementDuringCount").GetBoolean());
        Assert.Equal("AwaitingApproval", line.GetProperty("status").GetString());
        Assert.Equal(0, s.CountMovements());
    }

    [Fact]
    public async Task AC52c_a_drift_line_at_variance_zero_awaits_a_decision_and_the_count_does_not_close()
    {
        var (s, submitted) = await LateCommitAsync(5);
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        var line = Assert.Single(await s.LinesAsync(Id(submitted)));
        Assert.Equal("0.000", line.GetProperty("varianceQuantity").GetString());
        Assert.True(line.GetProperty("snapshotDrift").GetBoolean());
        Assert.Equal("AwaitingApproval", line.GetProperty("status").GetString());
        Assert.All(s.Claims(Id(submitted)), c => Assert.True(c["Open"].AsBoolean));
        Assert.DoesNotContain(s.Events(Id(submitted)), e => e["Kind"].AsString == "Closed");
    }
}
