using System.Text.Json.Nodes;
using Diten.SupplyChainService.Tests.Shipments.Dispatch;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;
using static Diten.SupplyChainService.Tests.Returns.Receipt.ReceiptScenario;

namespace Diten.SupplyChainService.Tests.Returns.Receipt;

/// <summary>
/// MOD-0186 §34.3 (pack r8), DL-1: R34-1, R34-2, R34-4…R34-7, R34-9…R34-17 and the non-text part of R34-3, through the real
/// host. QUARANTINE and AVAILABLE are INVENTORY's own sums of what it stored (<see cref="FakeInventory.Quarantine"/>,
/// <see cref="FakeInventory.Net"/>). R34-8 (joint scenario with MOD-0183) is in <see cref="ReturnReceiptJointScenarioTests"/>.
/// </summary>
[Collection("Returns Mongo")]
public sealed class ReturnReceiptTests(ITestOutputHelper output)
{
    private static Task<HttpResponseMessage?> Answer(HttpResponseMessage response) => Task.FromResult<HttpResponseMessage?>(response);
    private static Task<HttpResponseMessage?> Default => Task.FromResult<HttpResponseMessage?>(null);
    private static bool IsReceipt(FakeInventory.Call c, string line) => c.MovementType == "CUSTOMER_RETURN" && c.Line == line;
    private static FakeInventory.Call[] Receipts(ReturnReceiptHost app, string? line = null) =>
        app.Inventory.Calls.Where(c => c.MovementType == "CUSTOMER_RETURN" && (line is null || c.Line == line)).ToArray();

    private (ReturnReceiptHost App, ReceiptScenario S) Start(bool? stockIssued = true, params (string Line, string Quantity, string? Issue)[] lines)
    {
        var app = new ReturnReceiptHost();
        var s = new ReceiptScenario(app, output);
        app.Shipments.Detail = s.Shipment(stockIssued, lines.Length == 0 ? [("1", "10", "tx-issue-1"), ("2", "10", "tx-issue-2"), ("3", "10", "tx-issue-3")] : lines);
        return (app, s);
    }

    // ---- R34-1 -------------------------------------------------------------------------------------------------------
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task R34_1_EachLinePostsOneCustomerReturnIntoQuarantine_WithTheIssuedStockAndTheReturnLegalEntity(int count)
    {
        var (app, s) = Start();
        using var _ = app;
        var lines = Enumerable.Range(1, count).Select(i => (i.ToString(), (i + 1).ToString())).ToArray();
        var id = await s.InTransit(lines);
        var centralBefore = await app.Db.GetCollection<BsonDocument>("central_audit_outbox").CountDocumentsAsync(new BsonDocument("EntityId", id.ToString()));

        var r = await s.Receive(id, "receive-1");

        Assert.Equal(200, r.Status);
        Assert.Equal("Received", r.Body["status"]!.GetValue<string>());
        var calls = Receipts(app);
        Assert.Equal(count, calls.Length);
        foreach (var (line, quantity) in lines)
        {
            var call = Assert.Single(calls, c => c.Line == line);
            var b = call.Body;
            Assert.Equal($"{id}:{line}:customer-return:a1", call.Key);
            Assert.Equal(("QUARANTINE", Item(line), Sku(line), "Lsku", "lot-" + line), (b["toStockStatus"]!.GetValue<string>(), b["itemId"]!.GetValue<string>(),
                b["skuId"]!.GetValue<string>(), b["skuLevel"]!.GetValue<string>(), b["lotId"]!.GetValue<string>()));
            Assert.Equal((quantity, "wh-q", "loc-q1", s.ShipmentId.ToString()), (b["quantity"]!.GetValue<string>(), b["toWarehouseId"]!.GetValue<string>(),
                b["toLocationId"]!.GetValue<string>(), b["documentGroupId"]!.GetValue<string>()));
            Assert.Equal((s.LegalEntity.ToString(), s.Correlation.ToString()), (call.LegalEntity, call.Correlation));
            Assert.StartsWith("Bearer ", call.Authorization);
            Assert.False(b.ContainsKey("serialIds"));
            var wire = Line(r.Body, line);
            Assert.Equal(("Posted", null), (wire["inventoryPosting"]!.GetValue<string>(), wire["inventoryPostingReason"]?.GetValue<string>()));
            Assert.False(string.IsNullOrEmpty(wire["inventoryTransactionReferenceId"]!.GetValue<string>()));
        }
        Assert.Equal(lines.Sum(l => decimal.Parse(l.Item2)), app.Inventory.Quarantine);
        Assert.Equal(0m, app.Inventory.Net);
        var stored = await s.Stored(id);
        Assert.Equal("Received", stored["Status"].AsString);
        Assert.All(stored["Lines"].AsBsonArray, l => Assert.Equal("Posted", l["InventoryPosting"].AsString));
        Assert.Equal(1, await s.Events(id, "ReturnReceived"));
        // AUD-001: the Received transaction enlists the central record next to the in-product audit row.
        Assert.Equal(centralBefore + 1, await app.Db.GetCollection<BsonDocument>("central_audit_outbox").CountDocumentsAsync(new BsonDocument("EntityId", id.ToString())));
        Assert.Equal("inventory-customer-return", (await s.ReceivedAudit(id))!["EvidenceType"].AsString);
    }

    // ---- R34-2 -------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_2_ReplayOfTheReceiptCommand_PostsNoSecondMovementAndNoSecondEvent()
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        var first = await s.Receive(id, "receive-1");
        Assert.Equal(200, first.Status);
        var calls = app.Inventory.Calls.Count;

        var replay = await s.Receive(id, "receive-1");

        Assert.Equal(200, replay.Status);
        Assert.True(replay.Body["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal(calls, app.Inventory.Calls.Count);
        Assert.Equal(first.Body["lines"]!.ToJsonString(), replay.Body["lines"]!.ToJsonString());
        Assert.Equal(1, await s.Events(id, "ReturnReceived"));
        Assert.Equal(7m, app.Inventory.Quarantine);
    }

    // ---- R34-3 (non-text) --------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_3_ARefusedLine_LeavesTheReturnInTransit_KeepsPostedLines_AndTheRetryCompletes()
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        var refuse = true;
        app.Inventory.Script = c => IsReceipt(c, "2") && refuse ? Answer(FakeInventory.Error(422, "UNKNOWN_LOCATION")) : Default;

        var refused = await s.Receive(id, "receive-1");

        Assert.Equal((422, "INVENTORY_REFUSED"), (refused.Status, Code(refused.Body)));
        Assert.Equal(("2", "UNKNOWN_LOCATION"), (Details(refused.Body)!["shipmentLineNumber"]!.GetValue<string>(), Details(refused.Body)!["inventoryCode"]!.GetValue<string>()));
        var stored = await s.Stored(id);
        Assert.Equal("InTransit", stored["Status"].AsString);
        var line1 = stored["ReceiptAttempt"]["Lines"][0];
        Assert.Equal("Posted", line1["State"].AsString);
        var posted = line1["TransactionId"].AsString;
        Assert.DoesNotContain(app.Inventory.Calls, c => c.MovementType == "REVERSAL");
        Assert.Equal(0, await s.Events(id, "ReturnReceived"));
        Assert.Equal(4m, app.Inventory.Quarantine);

        refuse = false;
        var retry = await s.Receive(id, "receive-2");

        Assert.Equal(200, retry.Status);
        Assert.Equal(posted, Line(retry.Body, "1")["inventoryTransactionReferenceId"]!.GetValue<string>());
        // Line 1 replays its stored request; line 2's a1 answered 422 and is never sent again: a2.
        Assert.Equal(2, Receipts(app, "1").Length);
        Assert.Equal(Receipts(app, "1")[0].BodyJson, Receipts(app, "1")[1].BodyJson);
        Assert.Equal(new[] { $"{id}:2:customer-return:a1", $"{id}:2:customer-return:a2" }, Receipts(app, "2").Select(c => c.Key));
        Assert.Equal(1, await s.Events(id, "ReturnReceived"));
        Assert.Equal(7m, app.Inventory.Quarantine);
    }

    // ---- R34-4 -------------------------------------------------------------------------------------------------------
    [Theory]
    [InlineData("503")]
    [InlineData("timeout")]
    [InlineData("403")]
    public async Task R34_4_InventoryUnavailableIs503_ForbiddenIs403_NoTransitionNoEvent(string fault)
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"));
        app.Inventory.Script = c => fault switch
        {
            "503" => Answer(FakeInventory.Error(503, "SERVICE_UNAVAILABLE")),
            "403" => Answer(FakeInventory.Error(403, "CROSS_LE_FORBIDDEN")),
            _ => throw new HttpRequestException("connection lost")
        };

        var r = await s.Receive(id, "receive-1");

        if (fault == "403")
        {
            Assert.Equal((403, "INVALID_REQUEST"), (r.Status, Code(r.Body)));
            Assert.Equal("CROSS_LE_FORBIDDEN", Details(r.Body)!["inventoryCode"]!.GetValue<string>());
        }
        else Assert.Equal((503, "DEPENDENCY_UNAVAILABLE"), (r.Status, Code(r.Body)));
        Assert.Equal("InTransit", (await s.Stored(id))["Status"].AsString);
        Assert.Equal(0, await s.Events(id, "ReturnReceived"));
        Assert.Equal(0m, app.Inventory.Quarantine);
    }

    // ---- R34-5 -------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_5_CrossTenantOrCrossLegalEntityReturn_Is404_WithNoMovement()
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"));
        var reads = app.Shipments.Calls;

        var foreignTenant = await s.Send("POST", $"/{id}/transition", Received(), "foreign-tenant", tenant: Guid.NewGuid());
        var foreignLe = await s.Send("POST", $"/{id}/transition", Received(), "foreign-le", le: Guid.NewGuid());

        Assert.Equal((404, "RETURN_NOT_FOUND"), (foreignTenant.Status, Code(foreignTenant.Body)));
        Assert.Equal((404, "RETURN_NOT_FOUND"), (foreignLe.Status, Code(foreignLe.Body)));
        Assert.Empty(app.Inventory.Calls);
        Assert.Equal(reads, app.Shipments.Calls);
        Assert.Equal("InTransit", (await s.Stored(id))["Status"].AsString);
    }

    // ---- R34-6 -------------------------------------------------------------------------------------------------------
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task R34_6_StockIssuedButALineHasNoIssue_Is422SourceMovementMissing_NoMovementNoLotGuessed(bool stockIssued)
    {
        var (app, s) = Start(stockIssued, ("1", "10", "tx-issue-1"), ("2", "10", null));
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));

        var r = await s.Receive(id, "receive-1");

        Assert.Equal((422, "RETURN_SOURCE_MOVEMENT_MISSING"), (r.Status, Code(r.Body)));
        Assert.Equal(stockIssued ? "2" : "2", Details(r.Body)!["shipmentLineNumber"]!.GetValue<string>());
        Assert.Empty(app.Inventory.Calls);
        var stored = await s.Stored(id);
        Assert.Equal("InTransit", stored["Status"].AsString);
        Assert.True(stored["ReceiptAttempt"].IsBsonNull);
    }

    // ---- R34-7 -------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_7_NoRefusedOrUnknownLine_OtherValuesAre409WithTheStoredOnes_SameValuesResendByteForByte()
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        app.Inventory.Script = c => IsReceipt(c, "2") ? Answer(FakeInventory.Error(400, "INVALID_REQUEST")) : Default;
        Assert.Equal(500, (await s.Receive(id, "receive-1")).Status);      // line 1 Posted, line 2 Blocked
        var calls = app.Inventory.Calls.Count;

        foreach (var other in new[] { Received(location: "loc-other"), Received(at: "2026-10-29T09:00:00Z"), Received(warehouse: "wh-other") })
        {
            var conflict = await s.Receive(id, "receive-" + Guid.NewGuid(), other);
            Assert.Equal((409, "RETURN_RECEIPT_IN_PROGRESS"), (conflict.Status, Code(conflict.Body)));
            Assert.Equal(("wh-q", "loc-q1", "2026-10-29T08:00:00Z"), (Details(conflict.Body)!["toWarehouseId"]!.GetValue<string>(),
                Details(conflict.Body)!["toLocationId"]!.GetValue<string>(), Details(conflict.Body)!["occurredAt"]!.GetValue<string>()));
        }
        Assert.Equal(calls, app.Inventory.Calls.Count);

        var same = await s.Receive(id, "receive-same");
        Assert.Equal(500, same.Status);   // line 2 stays Blocked until unpostedLines
        var line1 = Receipts(app, "1");
        Assert.Equal(2, line1.Length);
        Assert.Equal(line1[0].BodyJson, line1[1].BodyJson);
        Assert.Equal(line1[0].Key, line1[1].Key);
    }

    // ---- R34-9 -------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_9_SerialTrackedSku_ShowsInventorysSerialRefusal_NothingPostedForThatLine()
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"));
        app.Inventory.Script = c => IsReceipt(c, "1") && !c.Body.ContainsKey("serialIds") ? Answer(FakeInventory.Error(422, "SERIAL_COUNT_MISMATCH")) : Default;

        var r = await s.Receive(id, "receive-1");

        Assert.Equal((422, "INVENTORY_REFUSED"), (r.Status, Code(r.Body)));
        Assert.Equal("SERIAL_COUNT_MISMATCH", Details(r.Body)!["inventoryCode"]!.GetValue<string>());
        Assert.All(Receipts(app), c => Assert.False(c.Body.ContainsKey("serialIds")));
        Assert.Equal(0m, app.Inventory.Quarantine);
        Assert.Equal(0, app.Inventory.Stored);
    }

    // ---- R34-10 ------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_10_PreWpP1Shipment_IsReceivedWithoutAnyInventoryCall_LinesNotPostedPreWpP1()
    {
        var (app, s) = Start(null, ("1", "10", null), ("2", "10", null));
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));

        var r = await s.Receive(id, "receive-1");

        Assert.Equal(200, r.Status);
        Assert.Empty(app.Inventory.Calls);
        foreach (var line in new[] { "1", "2" })
        {
            var wire = Line(r.Body, line);
            Assert.Equal(("NotPosted", "PRE_WP_P1"), (wire["inventoryPosting"]!.GetValue<string>(), wire["inventoryPostingReason"]!.GetValue<string>()));
            Assert.Null(wire["inventoryTransactionReferenceId"]);
        }
        var audit = (await s.ReceivedAudit(id))!;
        Assert.All(audit["ReceiptLines"].AsBsonArray, l => Assert.Equal("PRE_WP_P1", l["InventoryPostingReason"].AsString));
        Assert.Equal(1, await s.Events(id, "ReturnReceived"));
    }

    [Fact]
    public void R34_10_TheReceiptReadsTheShipmentOnlyOverTheSeam_NeverMod0183sStore()
    {
        // §34.2: stockIssued and the issue come over SHIPMENT-BUNDLE 3.4.0; no receipt type may take MOD-0183's repository or Mongo.
        var forbidden = new[] { typeof(Diten.SupplyChainService.Domain.Features.Shipments.IShipmentRepository), typeof(IMongoDatabase), typeof(IMongoClient) };
        foreach (var type in new[] { typeof(Diten.SupplyChainService.Application.Features.Returns.Receipt.ReturnReceiptCoordinator),
                     typeof(Diten.SupplyChainService.Application.Features.Returns.Receipt.ReturnLateWriterPolicy),
                     typeof(Diten.SupplyChainService.Infrastructure.Features.Returns.ReturnReferenceReader) })
            Assert.DoesNotContain(type.GetConstructors().SelectMany(c => c.GetParameters()), p => forbidden.Contains(p.ParameterType));
    }

    // ---- R34-11 ------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_11_CorrectingARefusedLine_PostsItUnderA2WithTheNewLocation_Line1ReplaysUnchanged()
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        app.Inventory.Script = c => IsReceipt(c, "2") && c.Body["toLocationId"]!.GetValue<string>() == "loc-q1" ? Answer(FakeInventory.Error(422, "UNKNOWN_LOCATION")) : Default;
        Assert.Equal(422, (await s.Receive(id, "receive-1")).Status);

        var corrected = await s.Receive(id, "receive-2", Received(location: "loc-q2", at: "2026-10-29T09:15:00Z"));

        Assert.Equal(200, corrected.Status);
        var line2 = Receipts(app, "2");
        Assert.Equal($"{id}:2:customer-return:a2", line2[^1].Key);
        Assert.Equal("loc-q2", line2[^1].Body["toLocationId"]!.GetValue<string>());
        Assert.Equal("2026-10-29T09:15:00.0000000Z", line2[^1].Body["transactionDate"]!.GetValue<string>());
        // Line 1 keeps its stored request — location and dates — byte for byte.
        var line1 = Receipts(app, "1");
        Assert.Equal(line1[0].BodyJson, line1[^1].BodyJson);
        Assert.Equal("loc-q1", line1[^1].Body["toLocationId"]!.GetValue<string>());
        Assert.Equal(7m, app.Inventory.Quarantine);
    }

    // ---- R34-12 ------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_12_PermanentRefusal_UnpostedLinesCompletes_ButNotForAFixableRefusal()
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        app.Inventory.Script = c => IsReceipt(c, "2") ? Answer(FakeInventory.Error(422, "ITEM_NOT_ACTIVE")) : Default;
        Assert.Equal(422, (await s.Receive(id, "receive-1")).Status);

        var wrongReason = await s.Receive(id, "receive-2", Received(unposted: Unposted("2", "UNKNOWN_ITEM")));
        Assert.Equal((422, "RETURN_LINE_NOT_UNPOSTABLE"), (wrongReason.Status, Code(wrongReason.Body)));
        var done = await s.Receive(id, "receive-3", Received(unposted: Unposted("2", "ITEM_NOT_ACTIVE")));

        Assert.Equal(200, done.Status);
        Assert.Equal(("NotPosted", "ITEM_NOT_ACTIVE"), (Line(done.Body, "2")["inventoryPosting"]!.GetValue<string>(), Line(done.Body, "2")["inventoryPostingReason"]!.GetValue<string>()));
        Assert.Equal("Posted", Line(done.Body, "1")["inventoryPosting"]!.GetValue<string>());
        var audit = (await s.ReceivedAudit(id))!;
        var auditLine = audit["ReceiptAttempt"]["lines"].AsBsonArray.Single(l => l["ShipmentLineNumber"].AsString == "2");
        Assert.Equal(("ITEM_NOT_ACTIVE", "ITEM_NOT_ACTIVE", 422), (auditLine["PostingReason"].AsString, auditLine["LastInventoryCode"].AsString, auditLine["LastHttpStatus"].AsInt32));

        // The same field for a line refused UNKNOWN_LOCATION (fixable) → 422, the return stays InTransit.
        var (app2, s2) = Start();
        using var __ = app2;
        var other = await s2.InTransit(("1", "4"));
        app2.Inventory.Script = c => IsReceipt(c, "1") ? Answer(FakeInventory.Error(422, "UNKNOWN_LOCATION")) : Default;
        Assert.Equal(422, (await s2.Receive(other, "receive-1")).Status);
        var refused = await s2.Receive(other, "receive-2", Received(unposted: Unposted("1", "ITEM_NOT_ACTIVE")));
        Assert.Equal((422, "RETURN_LINE_NOT_UNPOSTABLE"), (refused.Status, Code(refused.Body)));
        Assert.Equal(("1", "UNKNOWN_LOCATION"), (Details(refused.Body)!["shipmentLineNumber"]!.GetValue<string>(), Details(refused.Body)!["lastInventoryCode"]!.GetValue<string>()));
        Assert.Equal("InTransit", (await s2.Stored(other))["Status"].AsString);
    }

    // ---- R34-13 ------------------------------------------------------------------------------------------------------
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R34_13_UnknownIsNotCorrectable_TheStoredRequestIsResentFirst(bool resendRefused)
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        var phase = 0;
        app.Inventory.Script = c =>
        {
            if (!IsReceipt(c, "2")) return Default;
            if (phase == 0) throw new HttpRequestException("timeout");
            if (phase == 1 && resendRefused) return Answer(FakeInventory.Error(422, "UNKNOWN_LOCATION"));
            return Default;
        };
        Assert.Equal(503, (await s.Receive(id, "receive-1")).Status);
        phase = 1;

        var next = await s.Receive(id, "receive-2", Received(location: "loc-new"));

        var line2 = Receipts(app, "2");
        Assert.Equal(line2[0].BodyJson, line2[1].BodyJson);
        Assert.Equal("loc-q1", line2[1].Body["toLocationId"]!.GetValue<string>());
        if (!resendRefused)
        {
            Assert.Equal(200, next.Status);
            return;
        }
        Assert.Equal(422, next.Status);
        phase = 2;
        var corrected = await s.Receive(id, "receive-3", Received(location: "loc-new"));
        Assert.Equal(200, corrected.Status);
        Assert.Equal(($"{id}:2:customer-return:a2", "loc-new"), (Receipts(app, "2")[^1].Key, Receipts(app, "2")[^1].Body["toLocationId"]!.GetValue<string>()));
    }

    // ---- R34-14 ------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_14_Inventory400Blocks_IntegrityFaultExitCompletes_PostedOrKeyReusedLinesAreNotUnpostable()
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        app.Inventory.Script = c => IsReceipt(c, "2") ? Answer(FakeInventory.Error(400, "INVALID_REQUEST")) : Default;

        var blocked = await s.Receive(id, "receive-1");
        Assert.Equal((500, "INTERNAL_ERROR"), (blocked.Status, Code(blocked.Body)));
        Assert.Equal("Blocked", (await s.Stored(id))["ReceiptAttempt"]["Lines"][1]["State"].AsString);

        var posted = await s.Receive(id, "receive-2", Received(unposted: Unposted("1", "INTEGRITY_FAULT")));
        Assert.Equal((422, "RETURN_LINE_NOT_UNPOSTABLE"), (posted.Status, Code(posted.Body)));
        var exit = await s.Receive(id, "receive-3", Received(unposted: Unposted("2", "INTEGRITY_FAULT")));
        Assert.Equal(200, exit.Status);
        Assert.Equal(("NotPosted", "INTEGRITY_FAULT"), (Line(exit.Body, "2")["inventoryPosting"]!.GetValue<string>(), Line(exit.Body, "2")["inventoryPostingReason"]!.GetValue<string>()));
        var auditLine = (await s.ReceivedAudit(id))!["ReceiptAttempt"]["lines"].AsBsonArray.Single(l => l["ShipmentLineNumber"].AsString == "2");
        Assert.Equal(("INVALID_REQUEST", 400), (auditLine["LastInventoryCode"].AsString, auditLine["LastHttpStatus"].AsInt32));

        // IDEMPOTENCY_KEY_REUSED: INVENTORY may hold a movement for that key, so receiving the line NotPosted would hide stock.
        var (app2, s2) = Start();
        using var __ = app2;
        var other = await s2.InTransit(("1", "4"));
        app2.Inventory.Script = c => IsReceipt(c, "1") ? Answer(FakeInventory.Error(400, "IDEMPOTENCY_KEY_REUSED")) : Default;
        Assert.Equal(500, (await s2.Receive(other, "receive-1")).Status);
        var reused = await s2.Receive(other, "receive-2", Received(unposted: Unposted("1", "INTEGRITY_FAULT")));
        Assert.Equal((422, "RETURN_LINE_NOT_UNPOSTABLE"), (reused.Status, Code(reused.Body)));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", Details(reused.Body)!["lastInventoryCode"]!.GetValue<string>());
    }

    // ---- R34-15 ------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task R34_15_LostAnswerOnACorrectedLine_TheNextCommandReplaysA2_PostedAtXNeverTwice()
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        var phase = 0;
        app.Inventory.Script = c =>
        {
            if (!IsReceipt(c, "2")) return Default;
            if (phase == 0) return Answer(FakeInventory.Error(422, "UNKNOWN_LOCATION"));
            if (phase == 1) { app.Inventory.Answer(c).Dispose(); throw new HttpRequestException("answer lost"); }   // INVENTORY posts C2
            return Default;
        };
        Assert.Equal(422, (await s.Receive(id, "receive-1")).Status);
        phase = 1;
        Assert.Equal(503, (await s.Receive(id, "receive-2", Received(location: "loc-x"))).Status);
        Assert.Equal("Unknown", (await s.Stored(id))["ReceiptAttempt"]["Lines"][1]["State"].AsString);
        phase = 2;

        var done = await s.Receive(id, "receive-3", Received(location: "loc-y"));

        Assert.Equal(200, done.Status);
        var line2 = Receipts(app, "2");
        Assert.Equal(new[] { "a1", "a2", "a2" }, line2.Select(c => c.Key[^2..]));
        Assert.All(line2.Skip(1), c => Assert.Equal("loc-x", c.Body["toLocationId"]!.GetValue<string>()));
        Assert.Equal(7m, app.Inventory.Quarantine);          // line 2 posted once
        Assert.Equal("Posted", Line(done.Body, "2")["inventoryPosting"]!.GetValue<string>());
    }

    // ---- R34-16 / R34-17: late writer ---------------------------------------------------------------------------------
    /// <summary>Starts command A and pauses it inside INVENTORY on line 2's first <c>a1</c> send; <c>Release</c> lets it post.</summary>
    private static (Task<(int Status, JsonObject Body)> A, Task Paused, TaskCompletionSource Release) PauseA(ReturnReceiptHost app, ReceiptScenario s, Guid id,
        Func<FakeInventory.Call, Task<HttpResponseMessage?>> afterwards)
    {
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = 1;
        app.Inventory.Script = async c =>
        {
            if (IsReceipt(c, "2") && c.Key.EndsWith(":a1", StringComparison.Ordinal) && Interlocked.Exchange(ref first, 0) == 1)
            {
                paused.SetResult();
                await release.Task;
                return null;          // INVENTORY posts A's a1
            }
            return await afterwards(c);
        };
        return (s.Receive(id, "receive-A"), paused.Task, release);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R34_16_LateWriterWithASupersededVersion_ReversesItsReceipt_QuarantineNetOnce(bool correctionAnswerLost)
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        var phase = 0;
        var (a, paused, release) = PauseA(app, s, id, c =>
        {
            if (!IsReceipt(c, "2")) return Default;
            if (phase == 0) return Answer(FakeInventory.Error(422, "UNKNOWN_LOCATION"));                      // B's resend of a1
            if (phase == 1 && correctionAnswerLost) { app.Inventory.Answer(c).Dispose(); throw new HttpRequestException("lost"); }
            return Default;
        });
        await paused;
        var held = await s.Receive(id, "receive-early");
        Assert.Equal((409, "RETURN_RECEIPT_IN_PROGRESS"), (held.Status, Code(held.Body)));

        app.Clock.Advance(ReturnReceiptHost.PastLease);
        Assert.Equal(422, (await s.Receive(id, "receive-B")).Status);
        phase = 1;
        var c = await s.Receive(id, "receive-C", Received(location: "loc-q2"));
        Assert.Equal(correctionAnswerLost ? 503 : 200, c.Status);
        release.SetResult();
        var late = await a;

        Assert.Equal((409, "RETURN_RECEIPT_IN_PROGRESS"), (late.Status, Code(late.Body)));
        var aReceipt = app.Inventory.Calls.First(x => IsReceipt(x, "2") && x.Key.EndsWith(":a1", StringComparison.Ordinal));
        var reversal = Assert.Single(app.Inventory.Calls, x => x.MovementType == "REVERSAL");
        Assert.Equal($"{id}:2:customer-return-reversal:a1", reversal.Key);
        var history = (await s.Stored(id))["ReceiptAuditHistory"].AsBsonArray;
        var entry = Assert.Single(history);
        Assert.Equal(("2", 1, "Reversed"), (entry["ShipmentLineNumber"].AsString, entry["K"].AsInt32, entry["Outcome"].AsString));
        Assert.Equal(reversal.Body["sourceDocumentId"]!.GetValue<string>(), entry["ReceiptTransactionId"].AsString);
        phase = 2;
        if (correctionAnswerLost) Assert.Equal(200, (await s.Receive(id, "receive-D")).Status);   // resends a2: replay
        Assert.Equal(7m, app.Inventory.Quarantine);           // +4 line 1, +3 line 2 once
        Assert.Equal("Received", (await s.Stored(id))["Status"].AsString);
        Assert.NotNull(aReceipt);
    }

    [Theory]
    [InlineData("UNKNOWN_LOCATION")]
    [InlineData("ITEM_NOT_ACTIVE")]
    public async Task R34_17_LateWriterWithTheSameVersion_ReversesWhenTheLineIsRefusedOrNotPosted(string refusal)
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"), ("2", "3"));
        var phase = 0;
        var (a, paused, release) = PauseA(app, s, id, c => IsReceipt(c, "2") && phase == 0 ? Answer(FakeInventory.Error(422, refusal)) : Default);
        await paused;
        app.Clock.Advance(ReturnReceiptHost.PastLease);
        Assert.Equal(422, (await s.Receive(id, "receive-B")).Status);
        phase = 1;
        if (refusal == "ITEM_NOT_ACTIVE")
        {
            // The user receives line 2 without stock posting; the item is reactivated and A's a1 posts after all.
            Assert.Equal(200, (await s.Receive(id, "receive-C", Received(unposted: Unposted("2", "ITEM_NOT_ACTIVE")))).Status);
            release.SetResult();
            Assert.Equal(409, (await a).Status);
            Assert.Equal(4m, app.Inventory.Quarantine);        // A's receipt reversed: NotPosted hides no stock
        }
        else
        {
            release.SetResult();
            Assert.Equal(409, (await a).Status);
            Assert.Equal(4m, app.Inventory.Quarantine);
            Assert.Equal(200, (await s.Receive(id, "receive-C", Received(location: "loc-q2"))).Status);
            Assert.Equal($"{id}:2:customer-return:a2", Receipts(app, "2")[^1].Key);
            Assert.Equal(7m, app.Inventory.Quarantine);
        }
        var reversal = Assert.Single(app.Inventory.Calls, x => x.MovementType == "REVERSAL");
        Assert.Equal($"{id}:2:customer-return-reversal:a1", reversal.Key);
        Assert.Equal(2, Receipts(app, "2").Count(x => x.Key.EndsWith(":a1", StringComparison.Ordinal)));   // A's and B's; nobody else
    }

    // ---- client reference and target --------------------------------------------------------------------------------
    [Theory]
    [InlineData("client-reference")]
    [InlineData("no-warehouse")]
    public async Task Received_RefusesAClientReferenceOrAMissingWarehouse_With400_AndNoCall(string variant)
    {
        var (app, s) = Start();
        using var _ = app;
        var id = await s.InTransit(("1", "4"));
        var body = Received();
        if (variant == "client-reference") body["inventoryTransactionReferenceId"] = "txn-8801"; else body.Remove("toWarehouseId");

        var r = await s.Receive(id, "receive-1", body);

        Assert.Equal((400, "INVALID_REQUEST"), (r.Status, Code(r.Body)));
        Assert.Empty(app.Inventory.Calls);
        Assert.Equal(0, app.Shipments.Calls - 1);    // only the create read the shipment
    }
}
