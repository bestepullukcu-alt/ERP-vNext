using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// WP-0174-P4 — the typed-lot migration end to end on the REAL host (contract-validating client, shared test database,
/// fresh tenant per test), measured on the persisted state (pack §16 P4, §13 #13, CT K4-1…K4-5):
/// <list type="bullet">
/// <item>groups = one per SKU + typed text of a lot-controlled item, with count, stock and the K-D suggestion;</item>
/// <item>the commit links every typed transaction, re-keys balances / lot positions (totals equal), writes history and
/// closes typing — and <c>inventory_transactions</c> is byte-identical before / after (hash);</item>
/// <item>every refusal (reason, incomplete, typo, stale, clash, completed) writes nothing;</item>
/// <item>after the commit: lot-controlled typed inbound refused, non-lot-controlled typed stock still leaves (K4-4), a
/// migrated transaction reverses through its lot (K4-3), migrated serials leave with their lot (K4-5), reads resolve
/// linked rows (CT-D6).</item>
/// </list>
/// </summary>
public sealed class ManualLotMigrationTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ManualLotMigrationTests(TraceApiFactory factory) => _factory = factory;

    // ── listing ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Groups_are_one_per_SKU_and_typed_text_of_lot_controlled_items_with_count_stock_and_suggestion()
    {
        var w = new MigrationScenario(_factory);
        var registered = await w.CreateLotAsync("L-001");
        await w.ReceiveTypedAsync("L-001", "5");
        await w.ReceiveTypedAsync("L001", "3");
        await w.ReceiveTypedAsync("L001", "2", w.Ledger.LocationB);
        await w.IssueTypedAsync("L001", "1");
        await w.ReceiveTypedAsync("FRESH-1", "4");
        await w.Lots.PostOkAsync(w.Lots.Receipt("7", registered));                       // a registered lot is not a typed group
        var plain = w.AddPlainItem();
        await w.Lots.PostOkAsync(MigrationScenario.ForItem(w.Lots.Receipt("9", "PLAIN-1"), plain)); // K4-4: not lot-controlled

        var page = await w.ListAsync();
        var groups = page.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.Equal(3, page.GetProperty("totals").GetProperty("groups").GetInt32());
        Assert.Equal(5, page.GetProperty("totals").GetProperty("transactions").GetInt32());
        Assert.False(page.TryGetProperty("completed", out _));
        Assert.DoesNotContain(groups, g => g.GetProperty("typedText").GetString() == "PLAIN-1");

        var exact = MigrationScenario.Group(groups, "L-001");
        Assert.Equal(("ExactLot", registered, "exact"), (exact.GetProperty("suggestion").GetProperty("kind").GetString(),
            exact.GetProperty("suggestion").GetProperty("lotId").GetString(), exact.GetProperty("suggestion").GetProperty("note").GetString()));
        var typo = MigrationScenario.Group(groups, "L001");
        Assert.Equal(3, typo.GetProperty("transactionCount").GetInt32());
        Assert.Equal(4m, decimal.Parse(typo.GetProperty("onHand").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("SimilarLot", typo.GetProperty("suggestion").GetProperty("kind").GetString());
        Assert.Equal("None", MigrationScenario.Group(groups, "FRESH-1").GetProperty("suggestion").GetProperty("kind").GetString());

        var readOnly = TestTokens.Mint(w.Ledger.Tenant, w.Ledger.LegalEntity, w.Ledger.User, ["trace.lots.read"]);
        var status = await w.StatusAsync(readOnly);
        Assert.False(status.GetProperty("manualLotEntryClosed").GetBoolean());
        Assert.False(status.TryGetProperty("completedAt", out _));
        // The list is the administrator's (trace.lots.migrate); the status is every lot reader's (K4-1).
        await w.ListAsync(403, readOnly);
    }

    // ── the commit ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_commit_links_every_typed_transaction_rekeys_the_projections_and_leaves_the_ledger_byte_identical()
    {
        var w = new MigrationScenario(_factory);
        var existing = await w.CreateLotAsync("EX-1");
        await w.ReceiveTypedAsync("EX-1", "10");
        await w.ReceiveTypedAsync("L-001", "6");
        await w.ReceiveTypedAsync("L001", "4", w.Ledger.LocationB);
        await w.IssueTypedAsync("L-001", "1");
        await w.ReceiveTypedAsync("NEW-77", "3", toStatus: "QUALITY_INSPECTION");

        var hashBefore = w.TransactionsHash();
        var totalsBefore = w.BalanceTotals();
        var groups = await w.GroupsAsync();
        var lotGroup = MigrationScenario.Group(groups, "L-001").GetProperty("groupId").GetString()!;
        var decisions = new List<object>
        {
            MigrationScenario.Link(MigrationScenario.Group(groups, "EX-1").GetProperty("groupId").GetString()!, existing),
            MigrationScenario.NewLot(lotGroup, "L-001", "2028-01-31"),
            MigrationScenario.Typo(MigrationScenario.Group(groups, "L001").GetProperty("groupId").GetString()!, lotGroup),
            MigrationScenario.NewLot(MigrationScenario.Group(groups, "NEW-77").GetProperty("groupId").GetString()!, "NEW-77")
        };

        var result = await w.CommitAsync(decisions, groups.Count, 200);

        Assert.Equal((5, 3, 2), (result.GetProperty("transactionsLinked").GetInt32(), result.GetProperty("lotsLinked").GetInt32(), result.GetProperty("lotsCreated").GetInt32()));
        Assert.Equal(hashBefore, w.TransactionsHash());                                   // append-only: not one byte changed
        Assert.Equal(totalsBefore.OrderBy(p => p.Key), w.BalanceTotals().OrderBy(p => p.Key)); // re-key: every total equal
        Assert.Empty(w.OpenTypedRows(w.Ledger.Sku));

        var lots = w.Lots.Persisted("trace_lots");
        var l001 = Assert.Single(lots, d => d["LotNumber"].AsString == "L-001");
        Assert.Equal(("Draft", "Migration"), (l001["Status"].AsString, l001["CreationSource"].AsString));
        var l001Id = l001["_id"].AsString;
        Assert.Equal(9m, w.Lots.BalanceOnHandOfLot(l001Id));                               // 6 + 4 - 1, both typed texts merged
        Assert.Equal(9m, w.Lots.LotPositionOnHand(l001Id));
        Assert.Equal(10m, w.Lots.LotPositionOnHand(existing));
        Assert.Equal(5, w.Lots.Persisted("trace_movement_lot_links").Count);

        var history = w.Lots.Persisted("trace_lot_history");
        Assert.Contains(history, h => h["LotId"].AsString == existing && h["Action"].AsString == "Linked" && h["NewValue"].AsString == "EX-1"
                                      && h["Source"].AsString == "migration" && h["Reason"].AsString == MigrationScenario.Reason);
        Assert.Equal(2, history.Count(h => h["LotId"].AsString == l001Id && h["Action"].AsString == "Migrated"));

        var migration = Assert.Single(w.Lots.Persisted("trace_lot_migrations"));
        Assert.True(migration["ManualLotEntryClosed"].AsBoolean);
        var status = await w.StatusAsync();
        Assert.True(status.GetProperty("manualLotEntryClosed").GetBoolean());
        Assert.True(status.TryGetProperty("completedAt", out _));

        var after = await w.ListAsync();
        Assert.Equal(0, after.GetProperty("total").GetInt32());
        Assert.Equal(5, after.GetProperty("completed").GetProperty("transactionsLinked").GetInt32());

        // Restart: the SKU positions rebuilt from the re-keyed rows equal the maintained ones.
        var maintained = w.Lots.SkuPositionAvailableOnHand();
        await w.RebuildSkuPositionsAsync();
        Assert.Equal(maintained, w.Lots.SkuPositionAvailableOnHand());
        Assert.Equal(w.Lots.ExpectedSkuAvailableOnHand(), maintained);
    }

    [Fact]
    public async Task Reads_answer_linked_rows_under_the_registered_lot_and_the_links_are_listed()
    {
        var w = new MigrationScenario(_factory);
        var typedTx = await w.ReceiveTypedAsync("R-1", "5");
        var lot = await w.CreateLotAsync("R-1");
        var ownTx = await w.Lots.PostOkAsync(w.Lots.Receipt("2", lot));
        var groups = await w.GroupsAsync();
        await w.CommitAsync(MigrationScenario.ApplySuggestions(groups), groups.Count, 200);

        using var byLot = await w.Ledger.GetAsync($"/api/inventory/transactions?lotId={lot}", w.Lots.Token);
        var rows = (await w.Ledger.ReadAsync(byLot)).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { ownTx, typedTx }.OrderBy(x => x), rows.Select(r => r.GetProperty("transactionId").GetString()!).OrderBy(x => x));
        Assert.All(rows, r => Assert.Equal(lot, r.GetProperty("lotId").GetString()));

        using var all = await w.Ledger.GetAsync("/api/inventory/transactions", w.Lots.Token);
        var typed = Assert.Single((await w.Ledger.ReadAsync(all)).GetProperty("items").EnumerateArray(), r => r.GetProperty("transactionId").GetString() == typedTx);
        Assert.Equal(lot, typed.GetProperty("lotId").GetString());

        var links = (await w.LinksAsync("lotId=" + lot)).GetProperty("items").EnumerateArray().ToList();
        var link = Assert.Single(links);
        Assert.Equal((typedTx, "R-1"), (link.GetProperty("transactionId").GetString(), link.GetProperty("typedText").GetString()));
    }

    // ── after the commit: typing closed, K4-3 / K4-4 / K4-5 ──────────────────────────────────────────────

    [Fact]
    public async Task After_the_commit_a_lot_controlled_typed_inbound_is_refused_and_a_registered_one_posts()
    {
        var w = new MigrationScenario(_factory);
        await w.ReceiveTypedAsync("C-1", "5");
        var groups = await w.GroupsAsync();
        await w.CommitAsync(MigrationScenario.ApplySuggestions(groups), groups.Count, 200);
        var lot = Assert.Single(w.Lots.Persisted("trace_lots"))["_id"].AsString;

        var message = await w.Lots.RefusedAsync(w.Lots.Receipt("1", "C-2"), "UNKNOWN_LOT");
        Assert.Contains("closed", message);
        await w.Lots.PostOkAsync(w.Lots.Receipt("1", lot));
        await w.Lots.PostOkAsync(w.Lots.Issue("2", lot));
    }

    [Fact]
    public async Task K4_4_typed_stock_of_an_item_that_is_not_lot_controlled_is_not_migrated_and_still_leaves()
    {
        var w = new MigrationScenario(_factory);
        var plain = w.AddPlainItem();
        await w.Lots.PostOkAsync(MigrationScenario.ForItem(w.Lots.Receipt("8", "OLD-TEXT"), plain));
        await w.ReceiveTypedAsync("LC-1", "1");
        var groups = await w.GroupsAsync();
        Assert.Single(groups);
        await w.CommitAsync(MigrationScenario.ApplySuggestions(groups), groups.Count, 200);

        await w.Lots.PostOkAsync(MigrationScenario.ForItem(w.Lots.Issue("3", "OLD-TEXT"), plain));
        Assert.Single(w.Lots.Persisted("inventory_balances"), d => !d["IsDeleted"].AsBoolean && d["LotId"].IsString
                                                                   && d["LotId"].AsString == "OLD-TEXT" && d["OnHand"].ToDecimal() == 5m);
    }

    [Fact]
    public async Task K4_3_a_migrated_transaction_reverses_through_its_lot_and_only_the_reversal_is_new_in_the_ledger()
    {
        var w = new MigrationScenario(_factory);
        var receipt = w.Lots.Receipt("6", "REV-1");
        var original = await w.Lots.PostOkAsync(receipt);
        var groups = await w.GroupsAsync();
        await w.CommitAsync(MigrationScenario.ApplySuggestions(groups), groups.Count, 200);
        var lot = Assert.Single(w.Lots.Persisted("trace_lots"))["_id"].AsString;
        var before = w.Lots.Persisted("inventory_transactions");

        var reversal = await w.Lots.PostOkAsync(w.Lots.Reversal(receipt, original));

        Assert.Equal(0m, w.Lots.BalanceOnHandOfLot(lot));
        Assert.Equal(0m, w.Lots.LotPositionOnHand(lot));
        var after = w.Lots.Persisted("inventory_transactions");
        Assert.Equal(MigrationScenario.HashOf(before), MigrationScenario.HashOf(after.Where(d => d["_id"].AsString != reversal)));
        Assert.Equal(lot, Assert.Single(after, d => d["_id"].AsString == reversal)["LotId"].AsString);
    }

    [Fact]
    public async Task K4_5_serials_received_under_a_typed_lot_join_the_lot_and_leave_without_SERIAL_LOT_MISMATCH()
    {
        var w = new MigrationScenario(_factory, serialControlled: true);
        await w.ReceiveTypedAsync("SER-1", "2", serials: ["SN-A1", "SN-A2"]);
        Assert.True(w.Lots.Serial("SN-A1")["LotId"].IsBsonNull);
        var groups = await w.GroupsAsync();
        await w.CommitAsync(MigrationScenario.ApplySuggestions(groups), groups.Count, 200);
        var lot = Assert.Single(w.Lots.Persisted("trace_lots"))["_id"].AsString;

        Assert.Equal(lot, w.Lots.Serial("SN-A1")["LotId"].AsString);
        await w.Lots.PostOkAsync(w.Lots.Issue("1", lot, serials: ["SN-A1"]));
        Assert.Equal("Consumed", w.Lots.Serial("SN-A1")["Status"].AsString);
    }

    [Fact]
    public async Task Linking_to_a_Blocked_lot_takes_its_available_stock_out_of_availability_and_a_restart_agrees()
    {
        var w = new MigrationScenario(_factory);
        var lot = await w.CreateLotAsync("BL-1");
        await w.Lots.ReleaseAsync(lot);
        await w.Lots.BlockAsync(lot);
        await w.ReceiveTypedAsync("BL-1", "4");
        await w.ReceiveTypedAsync("OTHER-1", "6");
        Assert.Equal(10m, await w.Lots.AvailableAsync());

        var groups = await w.GroupsAsync();
        await w.CommitAsync(MigrationScenario.ApplySuggestions(groups), groups.Count, 200);

        Assert.Equal(6m, await w.Lots.AvailableAsync());
        Assert.Equal(w.Lots.ExpectedSkuAvailableOnHand(), w.Lots.SkuPositionAvailableOnHand());
        await w.RebuildSkuPositionsAsync();
        Assert.Equal(6m, await w.Lots.AvailableAsync());
    }

    // ── refusals write nothing ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_refusal_writes_nothing()
    {
        var w = new MigrationScenario(_factory);
        var clash = await w.CreateLotAsync("TAKEN-1");
        await w.ReceiveTypedAsync("A-1", "2");
        await w.ReceiveTypedAsync("A1", "3");
        await w.ReceiveTypedAsync("Z-9", "1");
        var groups = await w.GroupsAsync();
        string Id(string text) => MigrationScenario.Group(groups, text).GetProperty("groupId").GetString()!;
        var good = new List<object> { MigrationScenario.NewLot(Id("A-1"), "A-1"), MigrationScenario.Typo(Id("A1"), Id("A-1")), MigrationScenario.NewLot(Id("Z-9"), "Z-9") };
        var state = Snapshot(w);

        Assert.Equal("REASON_REQUIRED", MigrationScenario.ErrorCode(await w.CommitAsync(good, 3, 400, reason: "  9 harfli ")));
        Assert.Equal("MIGRATION_DECISIONS_INCOMPLETE", MigrationScenario.ErrorCode(await w.CommitAsync(good.Take(2), 3, 422)));
        Assert.Equal("MIGRATION_DECISIONS_INCOMPLETE", MigrationScenario.ErrorCode(await w.CommitAsync(
            [MigrationScenario.Link(Id("A-1"), Guid.NewGuid().ToString()), good[1], good[2]], 3, 422)));
        Assert.Equal("MIGRATION_TYPO_UNRESOLVED", MigrationScenario.ErrorCode(await w.CommitAsync(
            [MigrationScenario.Typo(Id("A-1"), Id("A1")), MigrationScenario.Typo(Id("A1"), Id("A-1")), good[2]], 3, 422)));
        Assert.Equal("MIGRATION_STALE", MigrationScenario.ErrorCode(await w.CommitAsync(good, 2, 409)));
        Assert.Equal("LOT_ALREADY_EXISTS", MigrationScenario.ErrorCode(await w.CommitAsync(
            [good[0], good[1], MigrationScenario.NewLot(Id("Z-9"), "TAKEN-1")], 3, 409)));
        Assert.Equal("LOT_ALREADY_EXISTS", MigrationScenario.ErrorCode(await w.CommitAsync(
            [good[0], good[1], MigrationScenario.NewLot(Id("Z-9"), "A-1")], 3, 409)));

        Assert.Equal(state, Snapshot(w));
        Assert.Equal("Draft", Assert.Single(w.Lots.Persisted("trace_lots"))["Status"].AsString); // only TAKEN-1

        await w.ReceiveTypedAsync("LATE-1", "1");                                          // a new typed lot after the list
        state = Snapshot(w);
        Assert.Equal("MIGRATION_STALE", MigrationScenario.ErrorCode(await w.CommitAsync(good, 3, 409)));
        Assert.Equal(state, Snapshot(w));
        Assert.NotNull(clash);
    }

    [Fact]
    public async Task A_second_commit_is_409_and_the_same_key_and_body_replay_200()
    {
        var w = new MigrationScenario(_factory);
        await w.ReceiveTypedAsync("ONCE-1", "2");
        var groups = await w.GroupsAsync();
        var decisions = MigrationScenario.ApplySuggestions(groups);
        var key = "mig-" + Guid.NewGuid().ToString("N");
        var first = await w.CommitAsync(decisions, groups.Count, 200, key: key);
        var state = Snapshot(w);

        var replay = await w.CommitAsync(decisions, groups.Count, 200, key: key);
        Assert.Equal(first.GetProperty("migrationId").GetString(), replay.GetProperty("migrationId").GetString());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", MigrationScenario.ErrorCode(await w.CommitAsync(decisions, groups.Count, 400, reason: MigrationScenario.Reason + " 2", key: key)));
        Assert.Equal("MIGRATION_ALREADY_COMPLETED", MigrationScenario.ErrorCode(await w.CommitAsync([], 0, 409)));
        Assert.Equal(state, Snapshot(w));
    }

    // ── property and race ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(11)]
    [InlineData(23)]
    [InlineData(37)]
    public async Task Property_any_typed_posting_sequence_keeps_every_total_through_the_commit_and_a_restart(int seed)
    {
        var w = new MigrationScenario(_factory);
        var random = new Random(seed);
        string[] texts = ["P-1", "P1", "p-1 ", "Q-2", "Q2", "R-3"];
        string[] locations = [w.Ledger.LocationA, w.Ledger.LocationB];
        var held = new Dictionary<(string, string), int>(); // AVAILABLE stock per text + location (issues leave from it)
        for (var i = 0; i < 18; i++)
        {
            var text = texts[random.Next(texts.Length)];
            var location = locations[random.Next(locations.Length)];
            var have = held.GetValueOrDefault((text, location));
            if (have > 1 && random.Next(3) == 0)
            {
                var quantity = random.Next(1, have);
                await w.IssueTypedAsync(text, quantity.ToString(), location);
                held[(text, location)] = have - quantity;
            }
            else
            {
                var quantity = random.Next(1, 9);
                var quality = random.Next(4) == 0;
                await w.ReceiveTypedAsync(text, quantity.ToString(), location, quality ? "QUALITY_INSPECTION" : null);
                held[(text, location)] = quality ? have : have + quantity;
            }
        }

        if (random.Next(2) == 0) await w.CreateLotAsync("Q-2");
        var hash = w.TransactionsHash();
        var totals = w.BalanceTotals();
        var groups = await w.GroupsAsync();

        await w.CommitAsync(MigrationScenario.ApplySuggestions(groups, t => "M-" + Math.Abs(t.GetHashCode() % 100000)), groups.Count, 200);

        Assert.Equal(hash, w.TransactionsHash());
        Assert.Equal(totals.OrderBy(p => p.Key), w.BalanceTotals().OrderBy(p => p.Key));
        Assert.Empty(w.OpenTypedRows(w.Ledger.Sku));
        foreach (var lot in w.Lots.Persisted("trace_lots"))
        {
            var id = lot["_id"].AsString;
            Assert.Equal(w.Lots.BalanceOnHandOfLot(id), w.Lots.LotPositionOnHand(id));
        }

        var maintained = w.Lots.SkuPositionAvailableOnHand();
        await w.RebuildSkuPositionsAsync();
        Assert.Equal(maintained, w.Lots.SkuPositionAvailableOnHand());
        Assert.Equal(totals.OrderBy(p => p.Key), w.BalanceTotals().OrderBy(p => p.Key));
    }

    [Fact]
    public async Task Race_a_typed_inbound_against_the_commit_either_lands_before_and_makes_it_stale_or_is_refused_after()
    {
        for (var round = 0; round < 4; round++)
        {
            var w = new MigrationScenario(_factory);
            await w.ReceiveTypedAsync("RACE-1", "2");
            var groups = await w.GroupsAsync();
            var decisions = MigrationScenario.ApplySuggestions(groups);

            var commit = Task.Run(() => w.Send(HttpMethod.Post, "/api/trace/migration/manual-lots/commit", new Dictionary<string, object?>
            {
                ["idempotencyKey"] = "race-" + round, ["reason"] = MigrationScenario.Reason, ["expectedGroupCount"] = groups.Count, ["decisions"] = decisions
            }, r => r.Headers.TryAddWithoutValidation("Idempotency-Key", "race-" + round)));
            var posting = Task.Run(() => w.Ledger.PostAsync(w.Lots.Receipt("1", "RACE-2"), token: w.Lots.Token));
            using var c = await commit;
            using var p = await posting;

            var committed = (int)c.StatusCode == 200;
            var posted = (int)p.StatusCode == 201;
            Assert.True(committed || (int)c.StatusCode == 409, $"commit {(int)c.StatusCode}");
            Assert.True(posted || (int)p.StatusCode == 422, $"posting {(int)p.StatusCode}");
            Assert.False(committed && posted && w.OpenTypedRows(w.Ledger.Sku).Count > 0, "a typed row landed after typing closed");
            if (committed)
            {
                Assert.Empty(w.OpenTypedRows(w.Ledger.Sku));
            }
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Hash of everything a commit may write: lots, history, links, migration record, balances, positions, serials.</summary>
    private static string Snapshot(MigrationScenario w)
        => string.Join("|", new[] { "trace_lots", "trace_lot_history", "trace_movement_lot_links", "trace_lot_migrations", "inventory_balances",
                "inventory_lot_positions", "inventory_sku_positions", "trace_serials", "inventory_transactions" }
            .Select(c => MigrationScenario.HashOf(w.Lots.Persisted(c).Select(d => Without(d, "GateSeq", "Version", "UpdatedAt")))));

    /// <summary>The gate counter moves on every typed posting and attempt (CT K4-2) — not a business write.</summary>
    private static MongoDB.Bson.BsonDocument Without(MongoDB.Bson.BsonDocument document, params string[] names)
    {
        var copy = document.DeepClone().AsBsonDocument;
        if (copy.Contains("ManualLotEntryClosed"))
        {
            foreach (var name in names) copy.Remove(name);
        }

        return copy;
    }
}
