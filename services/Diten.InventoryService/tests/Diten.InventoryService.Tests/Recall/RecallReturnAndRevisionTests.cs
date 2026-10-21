using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R3 — KR-H returns (pack §8.6) and CT-R5 scope revision (pack §8.3 step 5) on the real host. Returns: candidates are the CUSTOMER_RETURNs
/// of the lots the case blocks; only one posted after the approval into a held status links (422 RECALL_RETURN_NOT_QUARANTINED for AVAILABLE),
/// once (409), to a customer of the case (404); 0177 writes no movement; returned joins the consumption limit and the effectiveness. Revision:
/// r+1 from out-of-scope findings, the approved scope final, the case Active, every existing block kept; the delta's own signed approval (the
/// reviser cannot approve) adds its blocks and quarantine requests.
/// </summary>
public sealed class RecallReturnAndRevisionTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallReturnAndRevisionTests(RecallApiFactory factory) => _factory = factory;

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static Dictionary<string, object?> Link(string transactionId, string customer, string? key = null)
        => new() { ["idempotencyKey"] = key ?? RecallScenario.Key("ln"), ["transactionId"] = transactionId, ["customerRef"] = customer, ["returnReference"] = "IADE-0177" };

    [Fact]
    public async Task Returns_link_only_after_approval_into_a_held_status_once_to_a_customer_of_the_case()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var customer = ShipmentBundleDouble.ShipTo(w.Shipment);
        var early = await w.Lots.PostOkAsync(w.Lots.Receipt("1", w.Fg, movementType: "CUSTOMER_RETURN", toStatus: "QUARANTINE")); // before the case
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var held = await w.Lots.PostOkAsync(w.Lots.Receipt("2", w.Fg, movementType: "CUSTOMER_RETURN", toStatus: "QUARANTINE"));
        var free = await w.Lots.PostOkAsync(w.Lots.Receipt("1", w.Fg, movementType: "CUSTOMER_RETURN", toStatus: "AVAILABLE"));
        var depo = w.S.Mint(w.S.Depo, RecallR3.DepoKeys);

        var returns = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/returns", w.S.Qa1Token, null, 200); // CaseReturns, validated
        Assert.Empty(returns.GetProperty("links").EnumerateArray());
        string? Reason(string tx) => returns.GetProperty("candidates").EnumerateArray().Single(c => Str(c, "transactionId") == tx) is var c
                                    && c.TryGetProperty("ineligibleReason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        Assert.Equal("BeforeApproval", Reason(early));
        Assert.Equal("NotQuarantined", Reason(free));
        Assert.Null(Reason(held));

        var transactions = w.S.Persisted("inventory_transactions").Count;
        async Task<(int, JsonElement)> PostLink(Dictionary<string, object?> body)
        {
            var (status, json, _) = await w.S.SendAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/return-links", depo, body, (string)body["idempotencyKey"]!);
            return (status, json);
        }

        var (s1, notHeld) = await PostLink(Link(free, customer));
        Assert.Equal((422, "RECALL_RETURN_NOT_QUARANTINED"), (s1, RecallScenario.Code(notHeld)));
        var (s2, before) = await PostLink(Link(early, customer));
        Assert.Equal((422, "RECALL_RETURN_NOT_ELIGIBLE"), (s2, RecallScenario.Code(before)));
        var (s3, unknown) = await PostLink(Link(held, "CUST-NOT-OF-THIS-CASE"));
        Assert.Equal((404, "RECALL_UNKNOWN_CUSTOMER"), (s3, RecallScenario.Code(unknown)));

        var key = RecallScenario.Key("ln");
        var (s4, linked) = await PostLink(Link(held, customer, key));
        Assert.True(s4 == 201, linked.ToString());
        Assert.Equal((held, 2m, customer, "ManualCustomerReturn"), (Str(linked, "transactionId"), linked.GetProperty("quantity").GetDecimal(), Str(linked, "customerRef"), Str(linked, "source")));
        var (s5, replay) = await PostLink(Link(held, customer, key));
        Assert.Equal((200, Str(linked, "linkId")), (s5, Str(replay, "linkId")));
        var (s6, twice) = await PostLink(Link(held, customer));
        Assert.Equal((409, "RECALL_RETURN_ALREADY_LINKED"), (s6, RecallScenario.Code(twice)));
        Assert.Equal(transactions, w.S.Persisted("inventory_transactions").Count); // 0177 wrote no movement

        var after = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/returns", w.S.Qa1Token, null, 200);
        Assert.Single(after.GetProperty("links").EnumerateArray());
        Assert.DoesNotContain(after.GetProperty("candidates").EnumerateArray(), c => Str(c, "transactionId") == held);
        Assert.Single(RecallR3.Events(w.S, caseId, "ReturnLinked"));
        Assert.Single(w.S.Audit("recall.return.linked"));
        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Post, $"/api/recall/cases/{caseId}/return-links", w.S.DepoToken, Link(held, customer, key), key)).Status);

        // returned counts: shipped 4 − returned 2 leaves 2 to declare as consumed; 3 is too many
        var ticari = w.S.Mint(w.S.Ticari, RecallScenario.TicariKeys);
        Dictionary<string, object?> Consumed(decimal q) => new()
        {
            ["idempotencyKey"] = RecallScenario.Key("cd"), ["customerRef"] = customer, ["skuId"] = w.S.Ledger.Sku.ToString("D"), ["skuLevel"] = "Lsku",
            ["quantity"] = q, ["declaredOn"] = DateTime.UtcNow.ToString("yyyy-MM-dd"), ["evidence"] = "Musteri yazisi 2026/77"
        };
        Assert.Equal(422, (await w.S.CommandAsync(caseId, "consumption-declarations", ticari, Consumed(3m), null)).Status);
        Assert.Equal(201, (await w.S.CommandAsync(caseId, "consumption-declarations", ticari, Consumed(2m), null)).Status);
        var closed = await RecallR3.CloseAsync(w.S, caseId);
        var total = closed.GetProperty("effectiveness").GetProperty("totals")[0];
        Assert.Equal((4m, 2m, 2m, 0m), (total.GetProperty("shipped").GetDecimal(), total.GetProperty("returned").GetDecimal(),
            total.GetProperty("consumed").GetDecimal(), total.GetProperty("open").GetDecimal()));
    }

    [Fact]
    public async Task A_revision_adds_findings_with_its_own_signed_approval_and_keeps_every_existing_block()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (caseId, approved) = await RecallR3.ApproveAsync(w.S, w.Fg);
        var rootBlock = Assert.Single(RecallR3.Blocks(w.S, w.Fg), b => b["Active"].AsBoolean);

        // a lot made from the recalled one after the approval: a new genealogy link + its receipt = out-of-scope findings
        var derived = await w.S.LotAsync();
        await w.PostAsync(w.Lots.Receipt("3", derived, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", "MO-REV-" + w.Order);
        await w.LinkAsync(w.Fg, derived, "MO-REV-" + w.Order);
        var lotKey = $"Lot:{derived}";
        var stockKey = $"Stock:{derived}:{w.S.Ledger.Warehouse}:{w.S.Ledger.LocationA}:AVAILABLE";
        var diff = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/scope/diff", w.S.Qa1Token, null, 200);
        Assert.Contains(diff.GetProperty("added").EnumerateArray(), n => Str(n, "nodeKey") == lotKey);

        var qa1 = w.S.Mint(w.S.Qa1, RecallR3.QaKeys);
        var qa2 = w.S.Mint(w.S.Qa2, RecallR3.QaKeys);
        var current = await w.S.GetAsync(caseId);
        Task<(int Status, JsonElement Body, HttpResponseMessage Raw)> Revise(object[] add, object[]? marks, string etag, string? key = null)
        {
            var body = new Dictionary<string, object?> { ["idempotencyKey"] = key ?? RecallScenario.Key("rev"), ["addNodeKeys"] = add, ["reason"] = "Onay sonrasi uretilen lot kapsama alinmali" };
            if (marks is not null) body["marks"] = marks;
            return w.S.CommandAsync(caseId, "scope/revisions", qa1, body, etag);
        }

        Assert.Equal(400, (await Revise([$"Lot:{Guid.NewGuid()}"], null, RecallScenario.ETag(current))).Status); // not a finding
        var (nm, notMarkable, _) = await Revise([lotKey], [new { nodeKey = RecallScenario.LotKey(w.Fg), included = false, exclusionReason = "Kok lot cikarilamaz, deneme" }],
            RecallScenario.ETag(current));
        Assert.Equal((400, "RECALL_SCOPE_ITEM_NOT_MARKABLE"), (nm, RecallScenario.Code(notMarkable)));

        var (sr, revised, raw) = await Revise([lotKey, stockKey], null, RecallScenario.ETag(current));
        Assert.True(sr == 201, revised.ToString());
        Assert.Equal(("Active", 2), (Str(revised, "status"), revised.GetProperty("scopeRevision").GetInt32()));
        Assert.Equal(2, revised.GetProperty("pendingRevision").GetProperty("revision").GetInt32());
        Assert.NotNull(raw.Headers.ETag);
        Assert.Empty(RecallR3.Blocks(w.S, derived));                                             // the delta waits for its approval
        Assert.Equal(rootBlock["_id"], Assert.Single(RecallR3.Blocks(w.S, w.Fg), b => b["Active"].AsBoolean)["_id"]); // the approved block stays
        var (twice, twiceBody, _) = await Revise([lotKey], null, RecallScenario.ETag(revised));
        Assert.Equal((409, "RECALL_INVALID_TRANSITION"), (twice, RecallScenario.Code(twiceBody)));
        Assert.Single(RecallR3.Events(w.S, caseId, "ScopeRevised"));
        Assert.Single(w.S.Audit("recall.scope.revised"));

        // CT-R1: the reviser cannot approve the delta; the other QA user approves it with the same signature rule
        var (sod, sodBody, _) = await w.S.PrepareAsync(caseId, "ApproveScope", RecallScenario.ETag(revised), qa1);
        Assert.Equal((409, "RECALL_SOD_VIOLATION"), (sod, RecallScenario.Code(sodBody)));
        var (sp, prepared, _) = await w.S.PrepareAsync(caseId, "ApproveScope", RecallScenario.ETag(revised), qa2);
        Assert.True(sp == 200, prepared.ToString());
        var (unsigned, unsignedBody, _) = await w.S.ApproveAsync(caseId, RecallScenario.ETag(prepared), qa2, null);
        Assert.Equal((400, "RECALL_SIGNATURE_REQUIRED"), (unsigned, RecallScenario.Code(unsignedBody)));
        var (sa, deltaApproved, _) = await w.S.ApproveAsync(caseId, RecallScenario.ETag(prepared), qa2, _factory.Signature.Issue(w.S.Qa2, caseId, Str(prepared, "actionCode")));
        Assert.True(sa == 200, deltaApproved.ToString());
        Assert.Equal("Active", Str(deltaApproved, "status"));
        Assert.False(deltaApproved.TryGetProperty("pendingRevision", out var pending) && pending.ValueKind != JsonValueKind.Null);
        Assert.Equal(2, deltaApproved.GetProperty("approvedScope").GetProperty("revision").GetInt32());

        // the delta's block and request are added; the approved block is the same row, still active
        var derivedBlock = Assert.Single(RecallR3.Blocks(w.S, derived));
        Assert.Equal((true, 2), (derivedBlock["Active"].AsBoolean, derivedBlock["Revision"].ToInt32()));
        Assert.Equal(rootBlock["_id"], Assert.Single(RecallR3.Blocks(w.S, w.Fg), b => b["Active"].AsBoolean)["_id"]);
        var delta = Assert.Single(RecallR3.Requests(w.S, caseId), r => r["NodeKey"].AsString == stockKey);
        Assert.Equal(("Held", 2), (delta["Status"].AsString, delta["Revision"].ToInt32()));
        var (sale, saleBody) = await RecallR3.PostAsync(w.Lots, RecallR3.WithLot(w.Lots.Ledger.Issue("1"), derived, "MOD-0183"));
        Assert.Equal((422, "LOT_UNDER_RECALL"), (sale, RecallR3.Code(saleBody)));
        Assert.Equal(Str(approved, "number"), Str(saleBody.GetProperty("error").GetProperty("details"), "recallCaseNumber"));
    }
}
