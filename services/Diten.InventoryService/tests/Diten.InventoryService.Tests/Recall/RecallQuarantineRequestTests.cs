using System.Text.Json;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R3 — KR-B, pack §8.4, every row of the table with the REAL MOD-0175 <c>QuarantineStockHandler</c> on the suite's own Mongo (no
/// stand-in): the approval writes one request per Stock item in its transaction and, after the commit, sends ONE bucket per call (partial
/// success); AVAILABLE → Held (source Recall, the case as source document, the approver as actor); QUARANTINE / BLOCKED stock → NotNeeded
/// AlreadyQuarantined (the existing hold shown) / AlreadyBlocked; lot Blocked / Expired → NotNeeded LotBlocked / LotExpired; excluded →
/// NotNeeded Excluded; reserved → BlockedByReservation with the reservation lines, the reservation untouched; a crash after 0175 committed is
/// recovered by the sweeper on the SAME key (one hold); "Yeniden dene" moves to <c>:a{n}</c>; later stock is requested by hand.
/// </summary>
public sealed class RecallQuarantineRequestTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallQuarantineRequestTests(RecallApiFactory factory) => _factory = factory;

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static string Key(RecallScenario s, string lot, string location, string status, string? warehouse = null)
        => $"Stock:{lot}:{warehouse ?? s.Ledger.Warehouse}:{location}:{status}";

    private static BsonDocument Request(RecallScenario s, string caseId, string nodeKey)
        => Assert.Single(RecallR3.Requests(s, caseId), r => r["NodeKey"].AsString == nodeKey);

    private static async Task<JsonElement> StockAsync(RecallScenario s, string caseId)
        => await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/stock", s.Qa1Token, null, 200); // contract-validated StockLine[]

    [Fact]
    public async Task Approval_quarantines_the_available_bucket_with_the_case_as_origin_and_the_approver_as_actor()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("6", lot, movementType: "GOODS_RECEIPT_PO"));

        var (caseId, approved) = await RecallR3.ApproveAsync(s, lot);

        var nodeKey = Key(s, lot, s.Ledger.LocationA, "AVAILABLE");
        var r = Request(s, caseId, nodeKey);
        Assert.Equal(("Held", 6m, 1), (r["Status"].AsString, r["HeldQuantity"].ToDecimal(), r["HoldIds"].AsBsonArray.Count));
        Assert.Equal($"recall:{caseId}:r1:{nodeKey}", r["QuarantineKey"].AsString);
        Assert.Equal(6m, await s.Ledger.OnHandAsync(s.Ledger.LocationA, "QUARANTINE"));

        var hold = Assert.Single(s.Persisted("quarantine_holds"));
        Assert.Equal("Recall", hold["Source"].AsString);
        Assert.Equal(("MOD-0177", "RECALL_CASE", caseId, Str(approved, "number")),
            (hold["SourceDocument"]["Module"].AsString, hold["SourceDocument"]["Type"].AsString, hold["SourceDocument"]["_id"].AsString, hold["SourceDocument"]["Number"].AsString));
        Assert.StartsWith("Geri çağırma " + Str(approved, "number"), hold["SourceDocument"]["Text"].AsString);
        Assert.Equal(s.Qa2.ToString("D"), hold["CreatedBy"].ToString()); // CT-R2: the approver is the actor

        var stock = await StockAsync(s, caseId);
        var requestLine = Assert.Single(stock.EnumerateArray(), l => Str(l, "nodeKey") == nodeKey);
        Assert.Equal(0m, requestLine.GetProperty("quantity").GetDecimal()); // the AVAILABLE bucket is empty now
        Assert.Equal("Held", Str(requestLine.GetProperty("request"), "status"));
        Assert.Equal("Open", Str(requestLine.GetProperty("request").GetProperty("holds")[0], "status")); // 0175's status, read now
        var held = Assert.Single(stock.EnumerateArray(), l => Str(l, "nodeKey") == Key(s, lot, s.Ledger.LocationA, "QUARANTINE"));
        Assert.Equal(6m, held.GetProperty("quantity").GetDecimal());
        Assert.Single(RecallR3.Events(s, caseId, "QuarantineRequested"));
        Assert.Single(s.Audit("recall.quarantine.requested"));
    }

    [Fact]
    public async Task Stock_already_out_of_use_and_excluded_items_need_no_request()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("4", lot, movementType: "GOODS_RECEIPT_PO"));                                  // A AVAILABLE
        await s.Lots.PostOkAsync(s.Lots.Receipt("1", lot, movementType: "GOODS_RECEIPT_PO", toStatus: "BLOCKED"));            // A BLOCKED
        await s.Lots.PostOkAsync(s.Lots.Receipt("2", lot, s.Ledger.LocationB, movementType: "GOODS_RECEIPT_PO", toStatus: "QUARANTINE")); // B QUARANTINE (intake hold)
        await s.Lots.PostOkAsync(s.Lots.Receipt("3", lot, s.Ledger.LocationC, movementType: "GOODS_RECEIPT_PO"));            // C AVAILABLE — excluded below

        var qa1 = s.Mint(s.Qa1, RecallR3.QaKeys);
        var open = RecallScenario.Open(lot);
        open["direction"] = "Forward";
        var (_, opened, _) = await s.PostOpenAsync(qa1, open);
        var caseId = RecallScenario.Id(opened);
        var excludedKey = Key(s, lot, s.Ledger.LocationC, "AVAILABLE", s.Ledger.Warehouse2);
        var (sm, marked, _) = await s.MarkAsync(caseId, RecallScenario.ETag(opened), excludedKey, false, "Ayri depoda, sevk edilmedi");
        Assert.True(sm == 200, marked.ToString());
        var (_, submitted, _) = await s.SubmitAsync(caseId, RecallScenario.ETag(marked), qa1);
        await s.ApproveSignedAsync(caseId, submitted, s.Qa2, s.Mint(s.Qa2, RecallR3.QaKeys));

        Assert.Equal("Held", Request(s, caseId, Key(s, lot, s.Ledger.LocationA, "AVAILABLE"))["Status"].AsString);
        var blocked = Request(s, caseId, Key(s, lot, s.Ledger.LocationA, "BLOCKED"));
        Assert.Equal(("NotNeeded", "AlreadyBlocked"), (blocked["Status"].AsString, blocked["NotNeededReason"].AsString));
        var quarantined = Request(s, caseId, Key(s, lot, s.Ledger.LocationB, "QUARANTINE"));
        Assert.Equal(("NotNeeded", "AlreadyQuarantined"), (quarantined["Status"].AsString, quarantined["NotNeededReason"].AsString));
        var excluded = Request(s, caseId, excludedKey);
        Assert.Equal(("NotNeeded", "Excluded"), (excluded["Status"].AsString, excluded["NotNeededReason"].AsString));
        Assert.Equal(3m, await s.Ledger.OnHandAsync(s.Ledger.LocationC, "AVAILABLE")); // nothing sent for the excluded bucket

        // "durumu gösterilir": the existing intake hold of the quarantined bucket is shown on the request (read live)
        var stock = await StockAsync(s, caseId);
        var line = Assert.Single(stock.EnumerateArray(), l => Str(l, "nodeKey") == Key(s, lot, s.Ledger.LocationB, "QUARANTINE"));
        Assert.Single(line.GetProperty("request").GetProperty("holds").EnumerateArray());
    }

    [Theory]
    [InlineData("Blocked")]
    [InlineData("Expired")]
    public async Task A_held_lot_needs_no_request_LotBlocked_LotExpired(string held)
    {
        var s = new RecallScenario(_factory);
        string lot;
        if (held == "Blocked")
        {
            lot = await s.LotAsync();
            await s.Lots.PostOkAsync(s.Lots.Receipt("5", lot, movementType: "GOODS_RECEIPT_PO"));
            await s.Lots.BlockAsync(lot);
        }
        else
        {
            lot = await s.Lots.CreateLotAsync(expiryDate: "2020-01-31");
            await s.Lots.PostOkAsync(s.Lots.Receipt("5", lot, movementType: "CUSTOMER_RETURN", toStatus: "QUARANTINE")); // CT-R10: the only way in
        }

        var (caseId, _) = await RecallR3.ApproveAsync(s, lot);

        var r = Assert.Single(RecallR3.Requests(s, caseId));
        Assert.Equal(("NotNeeded", held == "Blocked" ? "LotBlocked" : "LotExpired"), (r["Status"].AsString, r["NotNeededReason"].AsString));
        Assert.Equal(0, r["Attempts"].ToInt32()); // no 0175 call
    }

    [Fact]
    public async Task One_bucket_per_call_gives_partial_success_and_reserved_stock_stays_reserved_retry_takes_the_next_key()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", lot, movementType: "GOODS_RECEIPT_PO"));                    // A 10
        await s.Lots.PostOkAsync(s.Lots.Receipt("3", lot, s.Ledger.LocationB, movementType: "GOODS_RECEIPT_PO")); // B 3
        await s.Lots.ReserveAsync("8", 201);                                                                     // unreserved 13 - 8 = 5
        var reservations = s.Persisted("inventory_reservations").Select(r => r.ToJson()).ToList();

        var (caseId, _) = await RecallR3.ApproveAsync(s, lot);

        var a = Request(s, caseId, Key(s, lot, s.Ledger.LocationA, "AVAILABLE"));
        var b = Request(s, caseId, Key(s, lot, s.Ledger.LocationB, "AVAILABLE"));
        Assert.Equal("Held", b["Status"].AsString);                                // the free bucket is held …
        Assert.Equal("BlockedByReservation", a["Status"].AsString);               // … the reserved one reported, not all refused
        Assert.Equal("QUARANTINE_STOCK_RESERVED", a["ErrorCode"].AsString);
        Assert.NotEmpty(a["Reservations"].AsBsonArray);
        Assert.Equal(reservations, s.Persisted("inventory_reservations").Select(r => r.ToJson()).ToList()); // 0177 never cancels a reservation

        // "Yeniden dene": the key family :a1; still reserved; the reservation still untouched
        var qa = s.Mint(s.Qa1, RecallR3.QaKeys);
        var retryKey = RecallScenario.Key("retry");
        var retried = await s.ExpectAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/quarantine-requests/{a["_id"].AsString}/retry", qa,
            new Dictionary<string, object?> { ["idempotencyKey"] = retryKey }, 202, retryKey);
        Assert.Equal("BlockedByReservation", Str(retried, "status"));
        Assert.Equal(1, retried.GetProperty("retryNumber").GetInt32());
        Assert.EndsWith(":a1", Request(s, caseId, a["NodeKey"].AsString)["QuarantineKey"].AsString, StringComparison.Ordinal);
        Assert.Equal(reservations, s.Persisted("inventory_reservations").Select(r => r.ToJson()).ToList());
        var replay = await s.ExpectAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/quarantine-requests/{a["_id"].AsString}/retry", qa,
            new Dictionary<string, object?> { ["idempotencyKey"] = retryKey }, 202, retryKey);
        Assert.Equal(1, replay.GetProperty("retryNumber").GetInt32());
        Assert.Single(RecallR3.Events(s, caseId, "QuarantineRetried"));

        // a held request is not retried
        var heldKey = RecallScenario.Key("r2");
        var notRetryable = await s.ExpectAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/quarantine-requests/{b["_id"].AsString}/retry", qa,
            new Dictionary<string, object?> { ["idempotencyKey"] = heldKey }, 409, heldKey);
        Assert.Equal("RECALL_QUARANTINE_REQUEST_NOT_RETRYABLE", RecallScenario.Code(notRetryable));
    }

    [Fact]
    public async Task A_crash_after_0175_committed_is_recovered_by_the_sweeper_on_the_same_key_with_one_hold()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("6", lot, movementType: "GOODS_RECEIPT_PO"));
        _factory.QuarantineCrash[lot] = "After";

        var (caseId, _) = await RecallR3.ApproveAsync(s, lot);

        var nodeKey = Key(s, lot, s.Ledger.LocationA, "AVAILABLE");
        Assert.Equal("Pending", Request(s, caseId, nodeKey)["Status"].AsString); // 0175 held it; the answer was lost
        Assert.Single(s.Persisted("quarantine_holds"));
        _factory.QuarantineCrash.TryRemove(lot, out _);

        var moved = await _factory.Services.GetRequiredService<RecallQuarantineSweeper>()
            .SweepAsync(DateTimeOffset.UtcNow + RecallQuarantineSweeper.PendingAge + TimeSpan.FromSeconds(5), CancellationToken.None, s.Tenant);

        Assert.Equal(1, moved);
        var r = Request(s, caseId, nodeKey);
        Assert.Equal("Held", r["Status"].AsString);
        Assert.Equal($"recall:{caseId}:r1:{nodeKey}", r["QuarantineKey"].AsString); // the same key — 0175 replayed
        Assert.Single(s.Persisted("quarantine_holds"));                             // no second hold
        Assert.Equal(6m, await s.Ledger.OnHandAsync(s.Ledger.LocationA, "QUARANTINE"));
        Assert.Equal(Assert.Single(s.Persisted("quarantine_holds"))["_id"].ToString(), r["HoldIds"].AsBsonArray.Single().AsString);
    }

    [Fact]
    public async Task The_sweeper_retries_Pending_only_and_a_retry_after_moved_stock_holds_what_is_left()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("6", lot, movementType: "GOODS_RECEIPT_PO"));
        _factory.QuarantineCrash[lot] = "Before";
        var (caseId, _) = await RecallR3.ApproveAsync(s, lot);
        var nodeKey = Key(s, lot, s.Ledger.LocationA, "AVAILABLE");
        Assert.Equal("Pending", Request(s, caseId, nodeKey)["Status"].AsString);
        _factory.QuarantineCrash.TryRemove(lot, out _);

        // meanwhile 2 of the 6 left AVAILABLE (exempt STATUS_TRANSFER); the sweep sends the stored body (6) → 0175 refuses the bucket
        await s.Lots.PostOkAsync(s.Lots.StatusTransfer("2", lot));
        var sweeper = _factory.Services.GetRequiredService<RecallQuarantineSweeper>();
        await sweeper.SweepAsync(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(3), CancellationToken.None, s.Tenant);
        var after = Request(s, caseId, nodeKey);
        Assert.NotEqual("Pending", after["Status"].AsString);
        Assert.Empty(s.Persisted("quarantine_holds"));
        var attempts = after["Attempts"].ToInt32();

        // a second sweep leaves a non-Pending request alone
        Assert.Equal(0, await sweeper.SweepAsync(DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10), CancellationToken.None, s.Tenant));
        Assert.Equal(attempts, Request(s, caseId, nodeKey)["Attempts"].ToInt32());

        // "Yeniden dene" plans again on what is AVAILABLE now: 4 of 6 → PartiallyHeld
        var qa = s.Mint(s.Qa1, RecallR3.QaKeys);
        var key = RecallScenario.Key("retry");
        var retried = await s.ExpectAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/quarantine-requests/{after["_id"].AsString}/retry", qa,
            new Dictionary<string, object?> { ["idempotencyKey"] = key }, 202, key);
        Assert.Equal(("PartiallyHeld", 4m, 6m), (Str(retried, "status"), retried.GetProperty("heldQuantity").GetDecimal(), retried.GetProperty("snapshotQuantity").GetDecimal()));
        Assert.EndsWith(":a1", Request(s, caseId, nodeKey)["QuarantineKey"].AsString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stock_that_arrives_later_is_requested_by_hand_once()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("6", lot, movementType: "GOODS_RECEIPT_PO"));
        var (caseId, _) = await RecallR3.ApproveAsync(s, lot);
        await s.Lots.PostOkAsync(s.Lots.Receipt("5", lot, s.Ledger.LocationB, movementType: "GOODS_RECEIPT_PO")); // exempt: an inbound

        var later = Key(s, lot, s.Ledger.LocationB, "AVAILABLE");
        var line = Assert.Single((await StockAsync(s, caseId)).EnumerateArray(), l => Str(l, "nodeKey") == later);
        Assert.Equal(JsonValueKind.Null, line.TryGetProperty("request", out var none) ? none.ValueKind : JsonValueKind.Null); // "istek yok"

        var qa = s.Mint(s.Qa1, RecallR3.QaKeys);
        var key = RecallScenario.Key("rq");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key, ["nodeKeys"] = new[] { later } };
        var created = await s.ExpectAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/quarantine-requests", qa, body, 202, key);
        Assert.Equal("Held", Str(created[0], "status"));
        Assert.Equal(5m, await s.Ledger.OnHandAsync(s.Ledger.LocationB, "QUARANTINE"));
        var replay = await s.ExpectAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/quarantine-requests", qa, body, 202, key);
        Assert.Equal(Str(created[0], "requestId"), Str(replay[0], "requestId"));

        var again = RecallScenario.Key("rq2");
        var exists = await s.ExpectAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/quarantine-requests", qa,
            new Dictionary<string, object?> { ["idempotencyKey"] = again, ["nodeKeys"] = new[] { later } }, 409, again);
        Assert.Equal("RECALL_QUARANTINE_REQUEST_EXISTS", RecallScenario.Code(exists));
        var unknown = RecallScenario.Key("rq3");
        Assert.Equal(400, (await s.SendAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/quarantine-requests", qa,
            new Dictionary<string, object?> { ["idempotencyKey"] = unknown, ["nodeKeys"] = new[] { "Stock:nope" } }, unknown)).Status);
        Assert.Equal(403, (await s.SendAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/quarantine-requests", s.Qa1Token, body, key)).Status); // R1 QA keys lack it
    }
}
