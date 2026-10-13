using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Recall.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R1 — the recall case through the real API (pack §8.7 state machine, §8.3 scope seal, §12, §13, §16 R1; CT-R1 four-eyes both ways,
/// CT-R6 subtree exclusion, CT-R7 tenant-wide lot status) with the signature gate behind the test double. The production fail-closed gate
/// is <see cref="RecallFailClosedSignatureTests"/>.
/// </summary>
public sealed class RecallCaseApiTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCaseApiTests(RecallApiFactory factory) => _factory = factory;

    private static string[] Kinds(JsonElement timeline) => timeline.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("kind").GetString()!).ToArray();

    [Fact]
    public async Task A_case_opens_in_Draft_with_number_root_item_event_and_audit_and_one_active_case_per_root_lot()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = RecallScenario.Id(c);

        Assert.Matches(@"^RC-\d{4}-\d{4}$", c.GetProperty("number").GetString()!);
        Assert.Equal("Draft", c.GetProperty("status").GetString());
        Assert.Equal("Real", c.GetProperty("type").GetString());
        Assert.Equal(2, c.GetProperty("class").GetInt32());
        Assert.Equal("Pharmacy", c.GetProperty("level").GetString());
        Assert.Equal(lot, c.GetProperty("rootLot").GetProperty("lotId").GetString());
        Assert.Equal(s.Qa1.ToString("D"), c.GetProperty("owner").GetProperty("userId").GetString());
        Assert.Equal(1, c.GetProperty("counts").GetProperty("scopeItems").GetInt32());

        var scope = await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope", s.Qa1Token, null, 200);
        var root = Assert.Single(scope.GetProperty("items").EnumerateArray());
        Assert.Equal(RecallScenario.LotKey(lot), root.GetProperty("nodeKey").GetString());
        Assert.True(root.GetProperty("isRoot").GetBoolean());
        Assert.True(scope.GetProperty("editable").GetBoolean());

        Assert.Single(s.Audit("recall.case.opened"));
        Assert.Equal(["Opened", "TraceRun"], Kinds(await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/timeline", s.Qa1Token, null, 200)));

        var (dup, dupBody, _) = await s.PostOpenAsync(s.Qa1Token, RecallScenario.Open(lot));
        Assert.Equal(409, dup);
        Assert.Equal("RECALL_CASE_ALREADY_OPEN", RecallScenario.Code(dupBody));
        Assert.Equal(id, dupBody.GetProperty("error").GetProperty("details").GetProperty("caseId").GetString());

        // replay of the same key + body = the same case; a second number is not taken
        var body = RecallScenario.Open(await s.LotAsync());
        var first = await s.ExpectAsync(HttpMethod.Post, "/api/recall/cases", s.Qa1Token, body, 201, (string)body["idempotencyKey"]!);
        var again = await s.ExpectAsync(HttpMethod.Post, "/api/recall/cases", s.Qa1Token, body, 200, (string)body["idempotencyKey"]!);
        Assert.Equal(RecallScenario.Id(first), RecallScenario.Id(again));
        var next = int.Parse(first.GetProperty("number").GetString()![8..]);
        Assert.Equal(int.Parse(c.GetProperty("number").GetString()![8..]) + 1, next);
    }

    [Fact]
    public async Task Opening_is_refused_for_an_unknown_lot_a_short_reason_or_a_bad_class()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var (unknown, u, _) = await s.PostOpenAsync(s.Qa1Token, RecallScenario.Open(Guid.NewGuid().ToString("D")));
        Assert.Equal(404, unknown);
        Assert.Equal("UNKNOWN_LOT", RecallScenario.Code(u));
        var (shortReason, r, _) = await s.PostOpenAsync(s.Qa1Token, RecallScenario.Open(lot, reason: "kisa"));
        Assert.Equal(400, shortReason);
        Assert.Equal("REASON_REQUIRED", RecallScenario.Code(r));
        Assert.Equal(400, (await s.PostOpenAsync(s.Qa1Token, RecallScenario.Open(lot, @class: 4))).Status);
        Assert.Equal(403, (await s.PostOpenAsync(s.DepoToken, RecallScenario.Open(lot))).Status);
    }

    [Fact]
    public async Task The_full_flow_runs_the_state_machine_with_four_eyes_both_ways_CT_R1()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = RecallScenario.Id(c);
        var (sa, scope, _) = await s.AddItemsAsync(id, RecallScenario.ETag(c), s.StockItem(lot),
            RecallScenario.ShipmentItem(lot, "SHP-2026-0820", "CUST-100/ADDR-2", 300m), RecallScenario.ShipmentItem(lot, "SHP-2026-0790", "CUST-200/ADDR-1", 200m));
        Assert.True(sa == 200, scope.ToString());
        Assert.Equal(4, scope.GetProperty("items").GetArrayLength());

        var (ss, submitted, _) = await s.SubmitAsync(id, RecallScenario.ETag(scope));
        Assert.True(ss == 200, submitted.ToString());
        Assert.Equal("PendingScopeApproval", submitted.GetProperty("status").GetString());
        Assert.Equal(scope.GetProperty("sha256").GetString(), submitted.GetProperty("submittedScope").GetProperty("sha256").GetString());
        Assert.Equal(s.Qa1.ToString("D"), submitted.GetProperty("scopeSubmittedBy").GetProperty("userId").GetString());

        // CT-R1 (a): the submitter cannot approve — refused at preparation AND at approval. K3 sabotage (d) turns these red.
        var (sodPrep, sodPrepBody, _) = await s.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(submitted), s.Qa1Token);
        Assert.Equal(409, sodPrep);
        Assert.Equal("RECALL_SOD_VIOLATION", RecallScenario.Code(sodPrepBody));
        var (sodAppr, sodApprBody, _) = await s.ApproveAsync(id, RecallScenario.ETag(submitted), s.Qa1Token, s.Factory.Signature.Issue(s.Qa1, id, "recall.scope.approve"));
        Assert.Equal(409, sodAppr);
        Assert.Equal("RECALL_SOD_VIOLATION", RecallScenario.Code(sodApprBody));

        // without a prepared decision → 409 PENDING; prepared but unsigned → 400
        var (noPrep, noPrepBody, _) = await s.ApproveAsync(id, RecallScenario.ETag(submitted), s.Qa2Token, null);
        Assert.Equal(409, noPrep);
        Assert.Equal("RECALL_PENDING_ACTION_MISMATCH", RecallScenario.Code(noPrepBody));
        var (sp, prepared, _) = await s.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(submitted), s.Qa2Token);
        Assert.True(sp == 200, prepared.ToString());
        Assert.True(prepared.GetProperty("signatureRequired").GetBoolean());
        Assert.Equal("recall.scope.approve", prepared.GetProperty("actionCode").GetString());
        Assert.Equal("recall.case", prepared.GetProperty("subject").GetProperty("subjectType").GetString());
        var (unsigned, unsignedBody, _) = await s.ApproveAsync(id, RecallScenario.ETag(prepared), s.Qa2Token, null);
        Assert.Equal(400, unsigned);
        Assert.Equal("RECALL_SIGNATURE_REQUIRED", RecallScenario.Code(unsignedBody));
        var (foreign, foreignBody, _) = await s.ApproveAsync(id, RecallScenario.ETag(prepared), s.Qa2Token, s.Factory.Signature.Issue(s.Qa1, id, "recall.scope.approve"));
        Assert.Equal(409, foreign);
        Assert.Equal("RECALL_SIGNATURE_MISMATCH", RecallScenario.Code(foreignBody));
        var signature = s.Factory.Signature.Issue(s.Qa2, id, "recall.scope.approve");
        var (ap, approved, _) = await s.ApproveAsync(id, RecallScenario.ETag(prepared), s.Qa2Token, signature);
        Assert.True(ap == 200, approved.ToString());
        Assert.Equal("Active", approved.GetProperty("status").GetString());
        Assert.Equal(signature, approved.GetProperty("scopeApprovalSignatureId").GetString());
        Assert.Equal(submitted.GetProperty("submittedScope").GetProperty("sha256").GetString(), approved.GetProperty("approvedScope").GetProperty("sha256").GetString());
        Assert.False((await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope", s.Qa1Token, null, 200)).GetProperty("editable").GetBoolean());

        // the lot is under recall
        var status = await s.ExpectAsync(HttpMethod.Get, $"/api/recall/lots/{lot}/status", s.DepoToken, null, 200);
        Assert.True(status.GetProperty("underRecall").GetBoolean());
        Assert.Equal(id, status.GetProperty("cases")[0].GetProperty("caseId").GetString());

        // records while Active
        var notify = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("n"), ["customerRef"] = "CUST-100/ADDR-2", ["notifiedOn"] = DateTime.UtcNow.ToString("yyyy-MM-dd"), ["channel"] = "Phone",
            ["note"] = "Eczane sorumlusu"
        };
        Assert.Equal(201, (await s.CommandAsync(id, "notifications", s.TicariToken, notify, null)).Status);
        var consumed = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("d"), ["customerRef"] = "CUST-200/ADDR-1", ["skuId"] = s.Ledger.Sku.ToString("D"), ["skuLevel"] = "Lsku",
            ["quantity"] = 200m, ["declaredOn"] = DateTime.UtcNow.ToString("yyyy-MM-dd"), ["evidence"] = "Musteri yazisi 2026/41"
        };
        Assert.Equal(201, (await s.CommandAsync(id, "consumption-declarations", s.TicariToken, consumed, null)).Status);
        var segregation = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("g"), ["nodeKey"] = s.StockKey(lot), ["locationId"] = s.Ledger.LocationA, ["quantity"] = 450m
        };
        Assert.Equal(201, (await s.CommandAsync(id, "segregations", s.DepoToken, segregation, null)).Status);

        // effectiveness: start → prepare → complete (Qa1); the completer cannot close (CT-R1 (b))
        var current = await s.GetAsync(id);
        var (st, started, _) = await s.CommandAsync(id, "effectiveness/start", s.Qa1Token, new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("st") },
            RecallScenario.ETag(current));
        Assert.True(st == 200, started.ToString());
        Assert.Equal("EffectivenessCheck", started.GetProperty("status").GetString());
        const string note = "Etkinlik kontrolu yapildi, acik miktar degerlendirildi";
        var (cp, compPrep, _) = await s.PrepareAsync(id, "CompleteEffectiveness", RecallScenario.ETag(started), s.Qa1Token, note: note);
        Assert.True(cp == 200, compPrep.ToString());
        var (cc, completed, _) = await s.CommandAsync(id, "effectiveness/complete", s.Qa1Token, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("cmp"), ["note"] = note, ["signatureId"] = s.Factory.Signature.Issue(s.Qa1, id, "recall.effectiveness.complete")
        }, RecallScenario.ETag(compPrep));
        Assert.True(cc == 200, completed.ToString());
        var totals = completed.GetProperty("effectiveness").GetProperty("totals")[0];
        Assert.Equal(500m, totals.GetProperty("shipped").GetDecimal());
        Assert.Equal(200m, totals.GetProperty("consumed").GetDecimal());
        Assert.Equal(300m, totals.GetProperty("open").GetDecimal());
        Assert.Equal(40m, totals.GetProperty("percent").GetDecimal());
        Assert.Equal(95m, completed.GetProperty("effectiveness").GetProperty("targetPercent").GetDecimal());
        Assert.False(totals.GetProperty("met").GetBoolean());

        const string summary = "Geri cagirma tamamlandi, kalan miktar musteride tuketildi";
        const string acceptance = "Acik miktar musteri beyani ile kabul edildi";
        var (sodClose, sodCloseBody, _) = await s.PrepareAsync(id, "Close", RecallScenario.ETag(completed), s.Qa1Token, summary: summary, acceptance: acceptance);
        Assert.Equal(409, sodClose);
        Assert.Equal("RECALL_SOD_VIOLATION", RecallScenario.Code(sodCloseBody));
        var (noAccept, noAcceptBody, _) = await s.PrepareAsync(id, "Close", RecallScenario.ETag(completed), s.Qa2Token, summary: summary);
        Assert.Equal(400, noAccept);
        Assert.Equal("RECALL_OPEN_QUANTITY_ACCEPTANCE_REQUIRED", RecallScenario.Code(noAcceptBody));
        var (sodCloseDirect, sodCloseDirectBody, _) = await s.CommandAsync(id, "close", s.Qa1Token, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("cl1"), ["summary"] = summary, ["openQuantityAcceptance"] = acceptance,
            ["signatureId"] = s.Factory.Signature.Issue(s.Qa1, id, "recall.case.close")
        }, RecallScenario.ETag(completed));
        Assert.Equal(409, sodCloseDirect);
        Assert.Equal("RECALL_SOD_VIOLATION", RecallScenario.Code(sodCloseDirectBody));

        var (pc, closePrep, _) = await s.PrepareAsync(id, "Close", RecallScenario.ETag(completed), s.Qa2Token, summary: summary, acceptance: acceptance);
        Assert.True(pc == 200, closePrep.ToString());
        var (cl, closed, _) = await s.CommandAsync(id, "close", s.Qa2Token, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("cl2"), ["summary"] = summary, ["openQuantityAcceptance"] = acceptance,
            ["signatureId"] = s.Factory.Signature.Issue(s.Qa2, id, "recall.case.close")
        }, RecallScenario.ETag(closePrep));
        Assert.True(cl == 200, closed.ToString());
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(acceptance, closed.GetProperty("closure").GetProperty("openQuantityAcceptance").GetString());

        // closure releases the lot; the timeline is complete; audit rows per step
        Assert.False((await s.ExpectAsync(HttpMethod.Get, $"/api/recall/lots/{lot}/status", s.DepoToken, null, 200)).GetProperty("underRecall").GetBoolean());
        Assert.Equal(["Opened", "TraceRun", "ScopeItemsAdded", "ScopeSubmitted", "TraceRun", "ScopeApproved", "CustomerNotified", "ConsumptionDeclared", "SegregationConfirmed",
                "EffectivenessStarted", "EffectivenessCompleted", "Closed"],
            Kinds(await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/timeline", s.Qa1Token, null, 200)));
        foreach (var action in new[] { "recall.scope.submitted", "recall.scope.approved", "recall.effectiveness.completed", "recall.case.closed" })
        {
            Assert.Single(s.Audit(action));
        }

        // a closed case takes no record and no transition; the root lot is free for a new case
        Assert.Equal(409, (await s.CommandAsync(id, "notifications", s.TicariToken, new Dictionary<string, object?>(notify) { ["idempotencyKey"] = RecallScenario.Key("n2") }, null)).Status);
        Assert.Equal(201, (await s.PostOpenAsync(s.Qa1Token, RecallScenario.Open(lot))).Status);
    }

    [Fact]
    public async Task Only_the_transitions_of_pack_8_7_are_possible()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = RecallScenario.Id(c);
        var key = new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("x") };

        // Draft: no approval, no effectiveness, no close, no records
        Assert.Equal("RECALL_INVALID_TRANSITION", RecallScenario.Code((await s.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(c), s.Qa2Token)).Body));
        Assert.Equal("RECALL_INVALID_TRANSITION", RecallScenario.Code((await s.CommandAsync(id, "effectiveness/start", s.Qa1Token, key, RecallScenario.ETag(c))).Body));

        // return needs PendingScopeApproval; cancel works from Draft; Cancelled is terminal — K3 sabotage (c)
        var (ret, retBody, _) = await s.CommandAsync(id, "scope/return", s.Qa2Token,
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("r"), ["reason"] = "Kapsam eksik, yeniden bakilmali" }, RecallScenario.ETag(c));
        Assert.Equal(409, ret);
        Assert.Equal("RECALL_INVALID_TRANSITION", RecallScenario.Code(retBody));
        var (cn, cancelled, _) = await s.CommandAsync(id, "cancel", s.Qa1Token,
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("cn"), ["reason"] = "Yanlis lot ile acildi, iptal" }, RecallScenario.ETag(c));
        Assert.True(cn == 200, cancelled.ToString());
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(409, (await s.SubmitAsync(id, RecallScenario.ETag(cancelled))).Status);
        Assert.Equal(409, (await s.AddItemsAsync(id, RecallScenario.ETag(cancelled), s.StockItem(lot))).Status);
        Assert.Equal(409, (await s.CommandAsync(id, "cancel", s.Qa1Token,
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("cn2"), ["reason"] = "Yanlis lot ile acildi, iptal" }, RecallScenario.ETag(cancelled))).Status);
        Assert.Equal("RECALL_INVALID_TRANSITION", RecallScenario.Code((await s.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(cancelled), s.Qa2Token)).Body));
        Assert.Equal("RECALL_INVALID_TRANSITION", RecallScenario.Code((await s.ApproveAsync(id, RecallScenario.ETag(cancelled), s.Qa2Token, null)).Body));

        // an Active case cannot be cancelled and cannot go back to Draft
        var (activeId, _, active) = await s.ActiveCaseAsync();
        Assert.Equal("RECALL_INVALID_TRANSITION", RecallScenario.Code((await s.CommandAsync(activeId, "cancel", s.Qa1Token,
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("cn3"), ["reason"] = "Aktif vakada iptal denemesi" }, RecallScenario.ETag(active))).Body));
        Assert.Equal("RECALL_INVALID_TRANSITION", RecallScenario.Code((await s.CommandAsync(activeId, "scope/return", s.Qa2Token,
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("r2"), ["reason"] = "Aktif vakada geri gonderme" }, RecallScenario.ETag(active))).Body));
        // closing before the effectiveness check is completed
        Assert.Equal("RECALL_INVALID_TRANSITION", RecallScenario.Code((await s.PrepareAsync(activeId, "Close", RecallScenario.ETag(active), s.Qa2Token,
            summary: "Erken kapatma denemesi yapiliyor")).Body));
    }

    [Fact]
    public async Task Return_to_Draft_drops_the_seal_and_a_stale_If_Match_is_refused()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = RecallScenario.Id(c);
        var (_, submitted, _) = await s.SubmitAsync(id, RecallScenario.ETag(c));

        // K3 sabotage (b): skip If-Match → the stale write passes
        var (stale, staleBody, _) = await s.CommandAsync(id, "scope/return", s.Qa2Token,
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("r"), ["reason"] = "Kapsam eksik, yeniden bakilmali" }, RecallScenario.ETag(c));
        Assert.Equal(409, stale);
        Assert.Equal("RECALL_CONFLICT", RecallScenario.Code(staleBody));

        var (ok, returned, _) = await s.CommandAsync(id, "scope/return", s.Qa2Token,
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("r"), ["reason"] = "Kapsam eksik, yeniden bakilmali" }, RecallScenario.ETag(submitted));
        Assert.True(ok == 200, returned.ToString());
        Assert.Equal("Draft", returned.GetProperty("status").GetString());
        Assert.False(returned.TryGetProperty("submittedScope", out _));
        Assert.False(returned.TryGetProperty("scopeSubmittedBy", out _));

        // the returner may now submit and another QA approve (four-eyes follows the LAST submitter)
        var (_, resubmitted, _) = await s.SubmitAsync(id, RecallScenario.ETag(returned), s.Qa2Token);
        Assert.Equal(409, (await s.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(resubmitted), s.Qa2Token)).Status);
        var approved = await s.ApproveSignedAsync(id, resubmitted, s.Qa1, s.Qa1Token);
        Assert.Equal("Active", approved.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_scope_changed_after_submit_is_refused_at_approval_RECALL_SCOPE_CHANGED()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = RecallScenario.Id(c);
        var (_, scope, _) = await s.AddItemsAsync(id, RecallScenario.ETag(c), s.StockItem(lot, 450m));
        var (_, submitted, _) = await s.SubmitAsync(id, RecallScenario.ETag(scope));
        var (_, prepared, _) = await s.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(submitted), s.Qa2Token);

        // The scope source changes under the sealed submission (R1: the stored items; from R2 the live trace). K3 sabotage (i): drop the
        // re-seal comparison of the approval → the approval passes on a scope nobody submitted.
        var changed = _factory.Collection("recall_scope_items").UpdateOne(
            new BsonDocument { ["TenantId"] = s.Tenant.ToString(), ["CaseId"] = id, ["NodeKey"] = s.StockKey(lot) },
            new BsonDocument("$set", new BsonDocument("LocationId", s.Ledger.LocationB)));
        Assert.Equal(1, changed.ModifiedCount);

        Assert.NotEqual(scope.GetProperty("sha256").GetString(),
            (await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope", s.Qa1Token, null, 200)).GetProperty("sha256").GetString());
        var (status, body, _) = await s.ApproveAsync(id, RecallScenario.ETag(prepared), s.Qa2Token, s.Factory.Signature.Issue(s.Qa2, id, "recall.scope.approve"));
        Assert.Equal(409, status);
        Assert.Equal("RECALL_SCOPE_CHANGED", RecallScenario.Code(body));
        Assert.Equal("PendingScopeApproval", (await s.GetAsync(id)).GetProperty("status").GetString());
        Assert.Empty(s.Persisted("recall_lot_blocks"));
        // preparing again is refused for the same reason
        Assert.Equal("RECALL_SCOPE_CHANGED", RecallScenario.Code((await s.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(prepared), s.Qa2Token)).Body));
    }

    [Fact]
    public async Task CT_R6_excluding_a_derived_lot_excludes_its_subtree_needs_a_reason_and_leaves_the_lot_unblocked()
    {
        var s = new RecallScenario(_factory);
        var root = await s.LotAsync();
        var child = await s.LotAsync();
        var c = await s.OpenAsync(root);
        var id = RecallScenario.Id(c);
        var (sa, scope, _) = await s.AddItemsAsync(id, RecallScenario.ETag(c), RecallScenario.LotItem(child), s.StockItem(child, 300m),
            RecallScenario.ShipmentItem(child, "SHP-1", "CUST-9", 50m));
        Assert.True(sa == 200, scope.ToString());

        var (noReason, noReasonBody, _) = await s.MarkAsync(id, RecallScenario.ETag(scope), RecallScenario.LotKey(child), false);
        Assert.Equal(400, noReason);
        Assert.Equal("RECALL_SCOPE_EXCLUSION_REASON_REQUIRED", RecallScenario.Code(noReasonBody));
        var (rootMark, rootMarkBody, _) = await s.MarkAsync(id, RecallScenario.ETag(scope), RecallScenario.LotKey(root), false, "Kok lot haric tutulamaz deneme");
        Assert.Equal(400, rootMark);
        Assert.Equal("RECALL_SCOPE_ITEM_NOT_MARKABLE", RecallScenario.Code(rootMarkBody));

        var (sm, marked, _) = await s.MarkAsync(id, RecallScenario.ETag(scope), RecallScenario.LotKey(child), false, "Farkli hammadde partisinden uretildi");
        Assert.True(sm == 200, marked.ToString());
        Assert.Equal(3, marked.GetProperty("excludedCount").GetInt32()); // the lot + its stock + its shipment
        var stock = marked.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("nodeKey").GetString() == s.StockKey(child));
        Assert.True(stock.GetProperty("included").GetBoolean());
        Assert.False(stock.GetProperty("effectivelyIncluded").GetBoolean());

        var (_, submitted, _) = await s.SubmitAsync(id, RecallScenario.ETag(marked));
        await s.ApproveSignedAsync(id, submitted, s.Qa2, s.Qa2Token);
        Assert.True((await s.ExpectAsync(HttpMethod.Get, $"/api/recall/lots/{root}/status", s.DepoToken, null, 200)).GetProperty("underRecall").GetBoolean());
        Assert.False((await s.ExpectAsync(HttpMethod.Get, $"/api/recall/lots/{child}/status", s.DepoToken, null, 200)).GetProperty("underRecall").GetBoolean());
        // the excluded shipment's customer is not a customer of the case
        var notify = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("n"), ["customerRef"] = "CUST-9", ["notifiedOn"] = DateTime.UtcNow.ToString("yyyy-MM-dd"), ["channel"] = "Fax"
        };
        var (nf, nfBody, _) = await s.CommandAsync(id, "notifications", s.TicariToken, notify, null);
        Assert.Equal(404, nf);
        Assert.Equal("RECALL_UNKNOWN_CUSTOMER", RecallScenario.Code(nfBody));
    }

    [Fact]
    public async Task CT_R7_the_lot_status_is_tenant_wide_but_another_company_sees_no_case_content()
    {
        var s = new RecallScenario(_factory);
        var (id, lot, _) = await s.ActiveCaseAsync();

        // K3 sabotage (h): bind getLotRecallStatus to the caller's company → the other company reads underRecall false
        var otherCompany = s.Mint(s.Depo, RecallScenario.DepoKeys, Guid.NewGuid());
        var seen = await s.ExpectAsync(HttpMethod.Get, $"/api/recall/lots/{lot}/status", otherCompany, null, 200);
        Assert.True(seen.GetProperty("underRecall").GetBoolean());
        var foreign = Assert.Single(seen.GetProperty("cases").EnumerateArray());
        Assert.False(foreign.GetProperty("sameCompany").GetBoolean());
        Assert.False(foreign.TryGetProperty("caseId", out _));
        Assert.Equal((await s.GetAsync(id)).GetProperty("number").GetString(), foreign.GetProperty("number").GetString());

        var otherTenant = s.Mint(Guid.NewGuid(), RecallScenario.DepoKeys, Guid.NewGuid(), Guid.NewGuid());
        var (unknown, unknownBody, _) = await s.SendAsync(HttpMethod.Get, $"/api/recall/lots/{lot}/status", otherTenant);
        Assert.Equal(404, unknown);
        Assert.Equal("UNKNOWN_LOT", RecallScenario.Code(unknownBody));
        Assert.Equal(403, (await s.SendAsync(HttpMethod.Get, $"/api/recall/lots/{lot}/status", s.ManagementToken)).Status); // no lots.status.read
        Assert.False((await s.ExpectAsync(HttpMethod.Get, $"/api/recall/lots/{await s.LotAsync()}/status", s.DepoToken, null, 200)).GetProperty("underRecall").GetBoolean());
    }

    [Fact]
    public async Task Another_company_is_refused_and_another_tenant_sees_nothing()
    {
        var s = new RecallScenario(_factory);
        var c = await s.OpenAsync(await s.LotAsync());
        var id = RecallScenario.Id(c);
        var otherCompany = s.Mint(s.Qa2, RecallScenario.QaKeys, Guid.NewGuid());
        var otherTenant = s.Mint(Guid.NewGuid(), RecallScenario.QaKeys, Guid.NewGuid(), Guid.NewGuid());

        // K3 sabotage (a): drop the LegalEntityId filter of the case repository → the other company reads 200 and lists the case
        var (s1, cross, _) = await s.SendAsync(HttpMethod.Get, $"/api/recall/cases/{id}", otherCompany);
        Assert.Equal(403, s1);
        Assert.Equal("CROSS_LE_FORBIDDEN", RecallScenario.Code(cross));
        Assert.Equal(403, (await s.SubmitAsync(id, RecallScenario.ETag(c), otherCompany)).Status);
        Assert.Equal(0, (await s.ExpectAsync(HttpMethod.Get, "/api/recall/cases", otherCompany, null, 200)).GetProperty("total").GetInt32());

        Assert.Equal(404, (await s.SendAsync(HttpMethod.Get, $"/api/recall/cases/{id}", otherTenant)).Status);
        Assert.Equal(404, (await s.SubmitAsync(id, RecallScenario.ETag(c), otherTenant)).Status);
        Assert.Equal("Draft", (await s.GetAsync(id)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_list_filters_status_class_and_search_and_counts_the_chips()
    {
        var s = new RecallScenario(_factory);
        var a = await s.OpenAsync(await s.LotAsync(), @class: 1);
        var (bId, _, _) = await s.ActiveCaseAsync();
        var list = await s.ExpectAsync(HttpMethod.Get, "/api/recall/cases", s.ManagementToken, null, 200);
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Equal(1, list.GetProperty("statusCounts").GetProperty("Draft").GetInt32());
        Assert.Equal(1, list.GetProperty("statusCounts").GetProperty("Active").GetInt32());

        var active = await s.ExpectAsync(HttpMethod.Get, "/api/recall/cases?status=Active", s.ManagementToken, null, 200);
        Assert.Equal(bId, RecallScenario.Id(Assert.Single(active.GetProperty("items").EnumerateArray())));
        var affected = active.GetProperty("items")[0].GetProperty("affectedQuantity")[0];
        Assert.Equal(950m, affected.GetProperty("quantity").GetDecimal()); // stock 450 + shipped 300 + 200, one SKU + unit
        Assert.Equal(1, (await s.ExpectAsync(HttpMethod.Get, "/api/recall/cases?class=1", s.ManagementToken, null, 200)).GetProperty("total").GetInt32());
        var number = a.GetProperty("number").GetString()!;
        Assert.Equal(1, (await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases?q={number}", s.ManagementToken, null, 200)).GetProperty("total").GetInt32());
        Assert.Equal(0, (await s.ExpectAsync(HttpMethod.Get, "/api/recall/cases?type=Drill", s.ManagementToken, null, 200)).GetProperty("total").GetInt32());
        Assert.Equal(400, (await s.SendAsync(HttpMethod.Get, "/api/recall/cases?status=draft", s.ManagementToken)).Status);
        Assert.Equal(400, (await s.SendAsync(HttpMethod.Get, "/api/recall/cases?class=4", s.ManagementToken)).Status);
    }

    [Fact]
    public async Task Records_validate_customer_quantity_and_stock_item_and_replay_by_key()
    {
        var s = new RecallScenario(_factory);
        var (id, lot, _) = await s.ActiveCaseAsync(shippedA: 300m, shippedB: 200m);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        Dictionary<string, object?> Declare(decimal quantity, string? key = null) => new()
        {
            ["idempotencyKey"] = key ?? RecallScenario.Key("d"), ["customerRef"] = "CUST-100/ADDR-2", ["skuId"] = s.Ledger.Sku.ToString("D"), ["skuLevel"] = "Lsku",
            ["quantity"] = quantity, ["declaredOn"] = today, ["evidence"] = "Musteri beyan yazisi"
        };

        var key = RecallScenario.Key("d");
        var first = await s.ExpectAsync(HttpMethod.Post, $"/api/recall/cases/{id}/consumption-declarations", s.TicariToken, Declare(250m, key), 201, key);
        Assert.Equal("EA", first.GetProperty("baseUomId").GetString());
        var replay = await s.ExpectAsync(HttpMethod.Post, $"/api/recall/cases/{id}/consumption-declarations", s.TicariToken, Declare(250m, key), 200, key);
        Assert.Equal(first.GetProperty("declarationId").GetString(), replay.GetProperty("declarationId").GetString());
        Assert.Equal(400, (await s.CommandAsync(id, "consumption-declarations", s.TicariToken, Declare(10m, key), null)).Status);
        var (over, overBody, _) = await s.CommandAsync(id, "consumption-declarations", s.TicariToken, Declare(60m), null);
        Assert.Equal(422, over);
        Assert.Equal("RECALL_QUANTITY_EXCEEDS_SHIPPED", RecallScenario.Code(overBody));
        // a superseding declaration replaces the first one in the sum
        var correction = Declare(300m);
        correction["supersedesId"] = first.GetProperty("declarationId").GetString();
        Assert.Equal(201, (await s.CommandAsync(id, "consumption-declarations", s.TicariToken, correction, null)).Status);

        var (future, _, _) = await s.CommandAsync(id, "notifications", s.TicariToken, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("n"), ["customerRef"] = "CUST-100/ADDR-2", ["notifiedOn"] = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"),
            ["channel"] = "Phone"
        }, null);
        Assert.Equal(400, future);
        Assert.Equal(400, (await s.CommandAsync(id, "notifications", s.TicariToken, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("n"), ["customerRef"] = "CUST-100/ADDR-2", ["notifiedOn"] = today, ["channel"] = "E-posta"
        }, null)).Status);

        var (seg, segBody, _) = await s.CommandAsync(id, "segregations", s.DepoToken, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("g"), ["nodeKey"] = RecallScenario.ShipmentKey(lot, "SHP-2026-0820"), ["locationId"] = s.Ledger.LocationA, ["quantity"] = 1m
        }, null);
        Assert.Equal(400, seg);
        Assert.Equal("INVALID_REQUEST", RecallScenario.Code(segBody));
        Assert.Equal(403, (await s.CommandAsync(id, "segregations", s.TicariToken, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("g"), ["nodeKey"] = s.StockKey(lot), ["locationId"] = s.Ledger.LocationA, ["quantity"] = 1m
        }, null)).Status);
        Assert.Single(s.Audit("recall.consumption.declared"), d => d["Detail"]["quantity"].AsString == "250");
    }

    [Fact]
    public async Task Scope_items_are_validated_and_keys_are_unique()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var other = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = RecallScenario.Id(c);

        Assert.Equal("RECALL_SCOPE_ITEM_EXISTS", RecallScenario.Code((await s.AddItemsAsync(id, RecallScenario.ETag(c), RecallScenario.LotItem(lot))).Body));
        Assert.Equal(400, (await s.AddItemsAsync(id, RecallScenario.ETag(c), s.StockItem(other))).Status); // its Lot item first
        Assert.Equal(400, (await s.AddItemsAsync(id, RecallScenario.ETag(c),
            new Dictionary<string, object?> { ["kind"] = "Stock", ["lotId"] = lot, ["warehouseId"] = s.Ledger.Warehouse, ["quantity"] = 1m })).Status);
        Assert.Equal(404, (await s.AddItemsAsync(id, RecallScenario.ETag(c),
            new Dictionary<string, object?> { ["kind"] = "Stock", ["lotId"] = lot, ["warehouseId"] = Guid.NewGuid().ToString("D"), ["stockStatus"] = "AVAILABLE",
                ["quantity"] = 1m, ["baseUomId"] = "EA" })).Status);
        Assert.Equal(404, (await s.AddItemsAsync(id, RecallScenario.ETag(c), RecallScenario.LotItem(Guid.NewGuid().ToString("D")))).Status);
        Assert.Equal(400, (await s.AddItemsAsync(id, RecallScenario.ETag(c), new Dictionary<string, object?> { ["kind"] = "Stock", ["lotId"] = lot, ["legalEntityId"] = "x" })).Status);

        var (ok, scope, _) = await s.AddItemsAsync(id, RecallScenario.ETag(c), RecallScenario.LotItem(other), s.StockItem(other));
        Assert.True(ok == 200, scope.ToString());
        Assert.Equal(c.GetProperty("version").GetInt32() + 1, scope.GetProperty("version").GetInt32());
        Assert.Equal("RECALL_SCOPE_ITEM_EXISTS", RecallScenario.Code((await s.AddItemsAsync(id, RecallScenario.ETag(scope), s.StockItem(other))).Body));
    }

    [Fact]
    public async Task The_drill_counter_is_a_separate_series_RC_YYYY_DNNN()
    {
        var s = new RecallScenario(_factory);
        await s.OpenAsync(await s.LotAsync());
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, s.Ledger.LegalEntity, s.Qa1);
        var repository = scope.ServiceProvider.GetRequiredService<IRecallRepository>();
        var year = DateTime.UtcNow.Year;
        var (d1, d2, c2) = await repository.InTransactionAsync(async (session, ct) =>
            (await session.NextDrillNumberAsync(year, ct), await session.NextDrillNumberAsync(year, ct), await session.NextCaseNumberAsync(year, ct)), CancellationToken.None);
        Assert.Equal($"RC-{year}-D001", d1);
        Assert.Equal($"RC-{year}-D002", d2);
        Assert.Equal($"RC-{year}-0002", c2);
    }
}

/// <summary>
/// WP-0177-R1 item 2 / K3 sabotage (g) — the PRODUCTION signature gate (fail-closed until R4): no scope approval, effectiveness completion or
/// closure passes — unsigned 400, signed 409 — and nothing is written.
/// </summary>
public sealed class RecallFailClosedSignatureTests : IClassFixture<ProductionSignatureRecallApiFactory>
{
    private readonly ProductionSignatureRecallApiFactory _factory;

    public RecallFailClosedSignatureTests(ProductionSignatureRecallApiFactory factory) => _factory = factory;

    [Fact]
    public async Task No_scope_approval_passes_unsigned_or_unverified_in_production()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = RecallScenario.Id(c);
        var (_, submitted, _) = await s.SubmitAsync(id, RecallScenario.ETag(c));
        var (sp, prepared, _) = await s.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(submitted), s.Qa2Token);
        Assert.True(sp == 200, prepared.ToString());
        Assert.True(prepared.GetProperty("signatureRequired").GetBoolean());

        var (s1, r1, _) = await s.ApproveAsync(id, RecallScenario.ETag(prepared), s.Qa2Token, null);
        Assert.Equal(400, s1);
        Assert.Equal("RECALL_SIGNATURE_REQUIRED", RecallScenario.Code(r1));
        var (s2, r2, _) = await s.ApproveAsync(id, RecallScenario.ETag(prepared), s.Qa2Token, "any-signature-id");
        Assert.Equal(409, s2);
        Assert.Equal("RECALL_SIGNATURE_MISMATCH", RecallScenario.Code(r2));

        Assert.Equal("PendingScopeApproval", (await s.GetAsync(id)).GetProperty("status").GetString());
        Assert.Empty(s.Persisted("recall_lot_blocks"));
        Assert.Empty(s.Audit("recall.scope.approved"));
        Assert.False((await s.ExpectAsync(HttpMethod.Get, $"/api/recall/lots/{lot}/status", s.DepoToken, null, 200)).GetProperty("underRecall").GetBoolean());
    }

    [Fact]
    public async Task A_settings_save_passes_without_a_signature_in_production_CT_R16()
    {
        var s = new RecallScenario(_factory);
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("set"), ["drillTargetHours"] = 6, ["drillFrequencyWarningDays"] = 180,
            ["effectivenessTargetPercent"] = new Dictionary<string, decimal> { ["1"] = 100, ["2"] = 95, ["3"] = 90 }, ["traceMaxDepth"] = 20, ["traceMaxNodes"] = 5000,
            ["reason"] = "Tatbikat hedefi 6 saate cikarildi"
        };
        Assert.Equal(200, (await s.SendAsync(HttpMethod.Put, "/api/recall/settings", s.AdminToken, body, (string)body["idempotencyKey"]!, "W/\"0\"")).Status);
    }
}
