using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Rules;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// MOD-0177-FU01 AC-6 (pack §8.5, SB-19) — the customer key at a re-trace (submitScope → Reconcile) and in a revision copy: a legacy (null)
/// row is never bound to a CRM id, a Manual row keeps its key whatever its Origin, a CRM key changes only to another CRM key (a read failure
/// keeps it; an Ok read without customerRef keeps it and sets the warning), a post-FU01 ShipTo / Document row follows the trace, and the
/// warning is written only where a stored CRM row is kept.
/// </summary>
public sealed class RecallCrmReconcileTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmReconcileTests(RecallApiFactory factory) => _factory = factory;

    private static string A => CrmWorld.A.ToString("D");

    // ── helpers ──

    private static async Task<JsonElement> SubmitAsync(CrmWorld w, string id)
    {
        var current = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}", w.Qa, null, 200);
        var (status, body, _) = await w.S.SubmitAsync(id, RecallScenario.ETag(current), w.S.Mint(w.S.Qa1, RecallR3.QaKeys));
        Assert.True(status == 200, body.ToString());
        return body;
    }

    private static async Task ReturnAsync(CrmWorld w, string id)
    {
        var current = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}", w.Qa, null, 200);
        var (status, body, _) = await w.S.CommandAsync(id, "scope/return", w.S.Mint(w.S.Qa2, RecallR3.QaKeys),
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("ret"), ["reason"] = "Kapsam yeniden izlenmeli, geri gonderildi" }, RecallScenario.ETag(current));
        Assert.True(status == 200, body.ToString());
    }

    private static BsonDocument Item(CrmWorld w, string id, string shipment)
        => Assert.Single(w.ScopeDocs(id), d => d["Kind"] == "Shipment" && d["SourceDocumentId"] == shipment && !(d.Contains("IsDeleted") && d["IsDeleted"].AsBoolean)
                                               && d["Revision"].ToInt32() == w.ScopeDocs(id).Max(x => x["Revision"].ToInt32()));

    private static string? Source(BsonDocument d) => d.Contains("CustomerRefSource") && !d["CustomerRefSource"].IsBsonNull ? d["CustomerRefSource"].AsString : null;

    private static bool Warning(BsonDocument d) => d.Contains("NoCrmWarning") && d["NoCrmWarning"].AsBoolean;

    private void MakeLegacy(CrmWorld w, string id, string shipment)
        => _factory.Database.GetCollection<BsonDocument>("recall_scope_items").UpdateOne(new BsonDocument("_id", Item(w, id, shipment)["_id"]),
            new BsonDocument("$unset", new BsonDocument("CustomerRefSource", "")));

    private static async Task<string> OpenDraftAsync(CrmWorld w) => RecallScenario.Id(await w.DraftAsync());

    private static JsonElement ScopeRow(JsonElement scope, string shipment)
        => Assert.Single(scope.GetProperty("items").EnumerateArray(), i => CrmWorld.Str(i, "sourceDocumentId") == shipment);

    // ── (a) … (a6): legacy and Manual rows keep their key; post-FU01 ShipTo / Document rows follow the trace ──

    [Fact]
    public async Task Legacy_and_manual_rows_keep_their_key_post_FU01_rows_follow()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var legacyShipTo = await w.SellRawAsync(null, shipTo: "CUST-100/ADDR-2");          // (a)
        var legacyDoc = await w.SellRawAsync(null);                                        // (a2) DOC at the opening (read 404)
        var outage = await w.SellRawAsync(null);                                           // (a4) Document written in an MVP-6 outage
        var oldBody = await w.SellRawAsync(null, shipTo: "CUST-501/ADDR-1");               // (a5) ShipTo written from a 3.4.0 body
        _factory.Shipments.Behaviour[legacyDoc] = ShipmentDoubleBehaviour.NotFound;
        _factory.Shipments.Behaviour[outage] = ShipmentDoubleBehaviour.ServerError;
        string id;
        try
        {
            id = await OpenDraftAsync(w);
        }
        finally
        {
            _factory.Shipments.Behaviour.TryRemove(legacyDoc, out _);
            _factory.Shipments.Behaviour.TryRemove(outage, out _);
        }

        // precondition: no record of any kind in the Draft case
        Assert.Equal(0, w.S.Persisted("recall_notifications").Count + w.S.Persisted("recall_consumption_declarations").Count + w.S.Persisted("recall_return_links").Count);
        Assert.Equal(("ShipTo", "Document", "Document"), (Source(Item(w, id, legacyShipTo)), Source(Item(w, id, outage)), Source(Item(w, id, legacyDoc))));
        MakeLegacy(w, id, legacyShipTo);
        MakeLegacy(w, id, legacyDoc);

        // (a3) a Manual row, soft-removed, whose document the trace brings back with customerRef A
        var manualDoc = Guid.NewGuid().ToString("D");
        var current = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}", w.Qa, null, 200);
        var (sa, added, _) = await w.S.AddItemsAsync(id, RecallScenario.ETag(current), RecallScenario.ShipmentItem(w.Fg, manualDoc, "CUST-300/ADDR-1", 1m));
        Assert.True(sa == 200, added.ToString());
        _factory.Database.GetCollection<BsonDocument>("recall_scope_items").UpdateOne(new BsonDocument("_id", Item(w, id, manualDoc)["_id"]),
            new BsonDocument("$set", new BsonDocument { ["IsDeleted"] = true }));
        var sale = w.W.Lots.Issue("1", w.Fg);
        sale["sourceModule"] = "MOD-0183";
        sale["sourceDocumentId"] = manualDoc;
        await w.W.Lots.PostAsync(sale, 201);

        foreach (var s in new[] { legacyShipTo, legacyDoc, outage, oldBody, manualDoc }) _factory.Shipments.CustomerRefJson[s] = "\"" + A + "\"";
        var fresh = await w.SellAsync(CrmWorld.A);                                       // a new node of the same customer
        await SubmitAsync(w, id);

        Assert.Equal(("CUST-100/ADDR-2", (string?)null), (Item(w, id, legacyShipTo)["CustomerRef"].AsString, Source(Item(w, id, legacyShipTo))));    // (a)
        Assert.Equal(("DOC:" + legacyDoc, (string?)null), (Item(w, id, legacyDoc)["CustomerRef"].AsString, Source(Item(w, id, legacyDoc))));          // (a2)
        var restored = Item(w, id, manualDoc);
        Assert.Equal(("CUST-300/ADDR-1", "Manual", "Trace"), (restored["CustomerRef"].AsString, Source(restored), restored["Origin"].AsString));     // (a3)
        Assert.Equal((A, "Crm"), (Item(w, id, outage)["CustomerRef"].AsString, Source(Item(w, id, outage))));                                         // (a4)
        Assert.Equal((A, "Crm"), (Item(w, id, oldBody)["CustomerRef"].AsString, Source(Item(w, id, oldBody))));                                       // (a5)
        Assert.Equal((A, "Crm"), (Item(w, id, fresh)["CustomerRef"].AsString, Source(Item(w, id, fresh))));
        var rows = await w.CustomersAsync(id);
        Assert.Equal("Legacy", CrmWorld.Str(CrmWorld.Customer(CrmWorld.Row(rows, "CUST-100/ADDR-2")), "state"));
        Assert.Equal("Crm", CrmWorld.Str(CrmWorld.Customer(CrmWorld.Row(rows, A)), "source"));

        // (a6) the second re-trace: the restored Manual row (Origin Trace now) keeps its key and source
        await ReturnAsync(w, id);
        await SubmitAsync(w, id);
        var second = Item(w, id, manualDoc);
        Assert.Equal(("CUST-300/ADDR-1", "Manual", "Trace"), (second["CustomerRef"].AsString, Source(second), second["Origin"].AsString));
        var scope = await w.GetAsync($"/api/recall/cases/{id}/scope");
        var manualRow = ScopeRow(scope, manualDoc);
        Assert.Equal(("Manual", "NoCrm", "CUST-300/ADDR-1"), (CrmWorld.Str(manualRow.GetProperty("customer"), "source"), CrmWorld.Str(manualRow.GetProperty("customer"), "state"),
            CrmWorld.Str(manualRow.GetProperty("customer"), "label")));
        Assert.DoesNotContain(A, manualRow.GetRawText(), StringComparison.Ordinal);
    }

    // ── (b), (b2) (1)–(3): a kept CRM key, the warning set, kept and cleared ──

    [Theory]
    [InlineData("CUST-500/ADDR-1")]
    [InlineData("")]
    public async Task Crm_key_is_kept_and_the_warning_follows_the_read(string shipTo)
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var shipment = await w.SellAsync(CrmWorld.A);
        var id = await OpenDraftAsync(w);
        Assert.Equal((A, "Crm", false), (Item(w, id, shipment)["CustomerRef"].AsString, Source(Item(w, id, shipment)), Warning(Item(w, id, shipment))));
        var unwarned = (await w.GetAsync($"/api/recall/cases/{id}/scope")).GetProperty("sha256").GetString();

        // (b) a read failure keeps the key, no warning
        _factory.Shipments.Behaviour[shipment] = ShipmentDoubleBehaviour.ServerError;
        await SubmitAsync(w, id);
        _factory.Shipments.Behaviour.TryRemove(shipment, out _);
        Assert.Equal((A, "Crm", false), (Item(w, id, shipment)["CustomerRef"].AsString, Source(Item(w, id, shipment)), Warning(Item(w, id, shipment))));
        Assert.False(ScopeRow(await w.GetAsync($"/api/recall/cases/{id}/scope"), shipment).GetProperty("customer").TryGetProperty("noCrmWarning", out _));

        // (b2) (1) Ok without customerRef → the key stays A, the warning is set; the seal does not see it
        await ReturnAsync(w, id);
        _factory.Shipments.CustomerRefJson.TryRemove(shipment, out _);
        _factory.Shipments.ShipToOverride[shipment] = shipTo;
        await SubmitAsync(w, id);
        Assert.Equal((A, "Crm", true), (Item(w, id, shipment)["CustomerRef"].AsString, Source(Item(w, id, shipment)), Warning(Item(w, id, shipment))));
        var scope = await w.GetAsync($"/api/recall/cases/{id}/scope");
        var customer = ScopeRow(scope, shipment).GetProperty("customer");
        Assert.Equal(("Crm", "Resolved", CrmWorld.NameA, CrmWorld.CodeA, true), (CrmWorld.Str(customer, "source"), CrmWorld.Str(customer, "state"), CrmWorld.Str(customer, "name"),
            CrmWorld.Str(customer, "code"), customer.GetProperty("noCrmWarning").GetBoolean()));
        Assert.True(CrmWorld.Customer(CrmWorld.Row(await w.CustomersAsync(id), A)).GetProperty("noCrmWarning").GetBoolean());
        Assert.Equal(unwarned, scope.GetProperty("sha256").GetString());

        // (2) a read failure keeps the stored warning
        await ReturnAsync(w, id);
        _factory.Shipments.Behaviour[shipment] = ShipmentDoubleBehaviour.ServerError;
        await SubmitAsync(w, id);
        _factory.Shipments.Behaviour.TryRemove(shipment, out _);
        Assert.True(Warning(Item(w, id, shipment)));
        Assert.True(ScopeRow(await w.GetAsync($"/api/recall/cases/{id}/scope"), shipment).GetProperty("customer").GetProperty("noCrmWarning").GetBoolean());

        // (3) MVP-6 gives the account again → the warning is cleared (PendingScopeApproval, before approveScope)
        await ReturnAsync(w, id);
        _factory.Shipments.CustomerRefJson[shipment] = "\"" + A + "\"";
        await SubmitAsync(w, id);
        Assert.False(Warning(Item(w, id, shipment)));
        Assert.False(ScopeRow(await w.GetAsync($"/api/recall/cases/{id}/scope"), shipment).GetProperty("customer").TryGetProperty("noCrmWarning", out _));
    }

    // ── (b2) (4): the effectiveness and return carriers copy the warning of the scope item ──

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Effectiveness_and_return_carry_the_warning_of_the_scope_item(bool warned)
    {
        var w = await CrmWorld.CreateAsync(_factory);
        w.FirstSaleIs(CrmWorld.A);
        var shipment = await w.SellAsync(CrmWorld.A, quantity: "3");
        var id = await OpenDraftAsync(w);
        if (warned)
        {
            _factory.Shipments.CustomerRefJson.TryRemove(shipment, out _);
            _factory.Shipments.CustomerRefJson.TryRemove(w.FirstShipment, out _);
        }

        var submitted = await SubmitAsync(w, id);
        await w.S.ApproveSignedAsync(id, submitted, w.S.Qa2, w.S.Mint(w.S.Qa2, RecallR3.QaKeys));
        var depo = w.S.Mint(w.S.Depo, RecallR3.DepoKeys);
        Assert.Equal(201, (await w.S.CommandAsync(id, "return-links", depo, w.Link(await w.ReturnAsync(), A), null)).Status);

        var effectiveness = await w.GetAsync($"/api/recall/cases/{id}/effectiveness");
        var row = Assert.Single(effectiveness.GetProperty("rows").EnumerateArray(), r => CrmWorld.Str(r, "customerRef") == A).GetProperty("customer");
        var link = Assert.Single((await w.GetAsync($"/api/recall/cases/{id}/returns")).GetProperty("links").EnumerateArray()).GetProperty("customer");
        foreach (var c in new[] { row, link })
        {
            Assert.Equal(("Crm", "Resolved", CrmWorld.NameA), (CrmWorld.Str(c, "source"), CrmWorld.Str(c, "state"), CrmWorld.Str(c, "name")));
            Assert.Equal(warned, c.TryGetProperty("noCrmWarning", out var flag) && flag.GetBoolean());
        }
    }

    // ── (b3): the rows where the warning is never written ──

    [Fact]
    public async Task The_warning_is_written_only_where_a_stored_crm_row_is_kept()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var postDoc = await w.SellRawAsync(null);
        var legacy = await w.SellRawAsync(null, shipTo: "CUST-100/ADDR-2");
        _factory.Shipments.Behaviour[postDoc] = ShipmentDoubleBehaviour.NotFound;
        var id = await OpenDraftAsync(w);
        _factory.Shipments.Behaviour.TryRemove(postDoc, out _);
        MakeLegacy(w, id, legacy);
        var manualDoc = Guid.NewGuid().ToString("D");
        var current = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}", w.Qa, null, 200);
        Assert.Equal(200, (await w.S.AddItemsAsync(id, RecallScenario.ETag(current), RecallScenario.ShipmentItem(w.Fg, manualDoc, "CUST-300/ADDR-1", 1m))).Status);
        var inserted = await w.SellRawAsync(null, shipTo: "CUST-600/ADDR-1"); // (i) a node new to the case: an insert

        await SubmitAsync(w, id);
        Assert.Equal(("ShipTo", false), (Source(Item(w, id, inserted)), Warning(Item(w, id, inserted))));   // (i)
        Assert.Equal(("ShipTo", false), (Source(Item(w, id, postDoc)), Warning(Item(w, id, postDoc))));     // (ii)
        Assert.Equal(((string?)null, false), (Source(Item(w, id, legacy)), Warning(Item(w, id, legacy))));  // (iii)
        var scope = await w.GetAsync($"/api/recall/cases/{id}/scope");
        foreach (var s in new[] { inserted, postDoc, legacy }) Assert.False(ScopeRow(scope, s).GetProperty("customer").TryGetProperty("noCrmWarning", out _));
    }

    [Fact]
    public async Task The_opening_writes_without_Reconcile_and_a_ShipTo_row_shows_no_warning()
    {
        // (v) CaseCommandHandlers.cs:111: the opening writes ScopeItems as they are; the ShipTo row's stored warning is never read
        var w = await CrmWorld.CreateAsync(_factory);
        var shipment = await w.SellRawAsync(null, shipTo: "CUST-600/ADDR-1");
        var id = await OpenDraftAsync(w);
        Assert.True(Warning(Item(w, id, shipment))); // the traced value is stored on a ShipTo row …
        Assert.False(CrmWorld.Customer(CrmWorld.Row(await w.CustomersAsync(id), "CUST-600/ADDR-1")).TryGetProperty("noCrmWarning", out _));
        Assert.False(ScopeRow(await w.GetAsync($"/api/recall/cases/{id}/scope"), shipment).GetProperty("customer").TryGetProperty("noCrmWarning", out _));
    }

    // ── (c), (c2), (c3), (d) ──

    [Fact]
    public async Task Legacy_follows_shipTo_with_a_sticky_null_and_a_crm_key_moves_only_to_another_crm_key()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var doc = await w.SellRawAsync(null);                       // (c) legacy DOC → shipTo
        var same = await w.SellRawAsync(null, shipTo: "CUST-700/ADDR-1"); // (c2) legacy shipTo → the same shipTo
        var post = await w.SellRawAsync(null);                      // (c3) post-FU01 Document → shipTo
        var crm = await w.SellAsync(CrmWorld.A);                    // (d) A → C
        _factory.Shipments.Behaviour[doc] = ShipmentDoubleBehaviour.NotFound;
        _factory.Shipments.Behaviour[post] = ShipmentDoubleBehaviour.NotFound;
        var id = await OpenDraftAsync(w);
        _factory.Shipments.Behaviour.TryRemove(doc, out _);
        _factory.Shipments.Behaviour.TryRemove(post, out _);
        MakeLegacy(w, id, doc);
        MakeLegacy(w, id, same);
        var c = Guid.NewGuid();
        _factory.Shipments.CustomerRefJson[crm] = "\"" + c.ToString("D") + "\"";
        await SubmitAsync(w, id);

        Assert.Equal((ShipmentBundleDouble.ShipTo(doc), (string?)null), (Item(w, id, doc)["CustomerRef"].AsString, Source(Item(w, id, doc))));
        Assert.Equal(("CUST-700/ADDR-1", (string?)null), (Item(w, id, same)["CustomerRef"].AsString, Source(Item(w, id, same))));
        Assert.Equal((ShipmentBundleDouble.ShipTo(post), "ShipTo"), (Item(w, id, post)["CustomerRef"].AsString, Source(Item(w, id, post))));
        Assert.Equal((c.ToString("D"), "Crm"), (Item(w, id, crm)["CustomerRef"].AsString, Source(Item(w, id, crm))));
        var lines = _factory.Logs.Lines.Where(l => l.Contains("recall.scope.customer_ref_changed", StringComparison.Ordinal)).ToList();
        Assert.Contains(lines, l => l.Contains(RecallScenario.ShipmentKey(w.Fg, crm), StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains(A, StringComparison.OrdinalIgnoreCase) || l.Contains(c.ToString("D"), StringComparison.OrdinalIgnoreCase));
    }

    // ── (e): the revision copy keeps every source and warning; only the delta gets a key by the order ──

    [Fact]
    public async Task Revision_copies_keep_source_and_warning_and_no_copy_is_upgraded()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        w.FirstSaleIs(CrmWorld.A);
        var legacy = await w.SellRawAsync(null, shipTo: "CUST-100/ADDR-2");
        var post = await w.SellRawAsync(null);
        var c = Guid.NewGuid();
        w.Account(c, "C Eczanesi", "ACC-C");
        var warned = await w.SellAsync(c);
        _factory.Shipments.Behaviour[post] = ShipmentDoubleBehaviour.NotFound; // a post-FU01 Document row through the opening and the submit
        var id = await OpenDraftAsync(w);
        MakeLegacy(w, id, legacy);
        _factory.Shipments.CustomerRefJson.TryRemove(warned, out _);  // C's warning at the submit re-trace
        var submitted = await SubmitAsync(w, id);
        Assert.True(Warning(Item(w, id, warned)));
        Assert.Equal("Document", Source(Item(w, id, post)));
        var approved = await w.S.ApproveSignedAsync(id, submitted, w.S.Qa2, w.S.Mint(w.S.Qa2, RecallR3.QaKeys));
        _factory.Shipments.Behaviour.TryRemove(post, out _);
        var ticari = w.S.Mint(w.S.Ticari, RecallScenario.TicariKeys);
        Assert.Equal(201, (await w.S.CommandAsync(id, "notifications", ticari, w.Notify(A), null)).Status);
        Assert.Equal(201, (await w.S.CommandAsync(id, "notifications", ticari, w.Notify("CUST-100/ADDR-2"), null)).Status);

        // MVP-6 now gives A for the legacy shipment and the post-FU01 Document one; a new finding with A appears in the live trace
        _factory.Shipments.CustomerRefJson[legacy] = "\"" + A + "\"";
        _factory.Shipments.CustomerRefJson[post] = "\"" + A + "\"";
        var derived = await w.S.LotAsync();
        await w.W.LinkAsync(w.Fg, derived, w.W.Order);
        await w.W.PostAsync(w.W.Lots.Receipt("5", derived, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", w.W.Order);
        var findingSale = w.W.Lots.Issue("1", derived);
        findingSale["sourceModule"] = "MOD-0183";
        var finding = Guid.NewGuid().ToString("D");
        findingSale["sourceDocumentId"] = finding;
        await w.W.Lots.PostAsync(findingSale, 201);
        _factory.Shipments.CustomerRefJson[finding] = "\"" + A + "\"";
        var qa1 = w.S.Mint(w.S.Qa1, RecallR3.QaKeys);
        var current = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}", qa1, null, 200);
        var (sr, revised, _) = await w.S.CommandAsync(id, "scope/revisions", qa1, new Dictionary<string, object?>
        {
            ["idempotencyKey"] = RecallScenario.Key("rev"), ["addNodeKeys"] = new[] { $"Lot:{derived}", RecallScenario.ShipmentKey(derived, finding) },
            ["reason"] = "Onay sonrasi uretilen lot kapsama alinmali"
        }, RecallScenario.ETag(current));
        Assert.True(sr == 201, revised.ToString());

        Assert.Equal((A, "Crm"), (Item(w, id, w.FirstShipment)["CustomerRef"].AsString, Source(Item(w, id, w.FirstShipment))));
        Assert.Equal(("CUST-100/ADDR-2", (string?)null), (Item(w, id, legacy)["CustomerRef"].AsString, Source(Item(w, id, legacy))));
        Assert.Equal(("DOC:" + post, "Document"), (Item(w, id, post)["CustomerRef"].AsString, Source(Item(w, id, post))));
        Assert.Equal((c.ToString("D"), "Crm", true), (Item(w, id, warned)["CustomerRef"].AsString, Source(Item(w, id, warned)), Warning(Item(w, id, warned))));
        Assert.Equal((A, "Crm"), (Item(w, id, finding)["CustomerRef"].AsString, Source(Item(w, id, finding))));

        // the revision approved, a CRM record on r+1 in upper case → 201 under the canonical key
        var approverToken = w.S.Mint(w.S.Qa2, RecallR3.QaKeys);
        await w.S.ApproveSignedAsync(id, revised, w.S.Qa2, approverToken);
        var (sn, notified, _) = await w.S.CommandAsync(id, "notifications", ticari, w.Notify(A.ToUpperInvariant()), null);
        Assert.True(sn == 201, notified.ToString());
        Assert.Equal(A, CrmWorld.Str(notified, "customerRef"));
    }
}

/// <summary>
/// MOD-0177-FU01 AC-15 — the seals: rs-scope-v1 is the pre-FU01 canonical text (no source, no warning in it), the scope seal does not move
/// with a CRM name, and rt-v1 does not see the customer key.
/// </summary>
public sealed class RecallCrmSealTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmSealTests(RecallApiFactory factory) => _factory = factory;

    /// <summary>The rs-scope-v1 text of this fixed scope, pinned from the pre-FU01 writer (RecallRules.cs:282-318).</summary>
    private const string Vector =
        "{\"form\":\"rs-scope-v1\",\"revision\":1,\"items\":[" +
        "{\"nodeKey\":\"Lot:L1\",\"parentKey\":null,\"kind\":\"Lot\",\"isRoot\":true,\"lotId\":\"L1\",\"skuId\":\"11111111-1111-1111-1111-111111111111\",\"warehouseId\":null,\"locationId\":null,\"stockStatus\":null,\"sourceDocumentId\":null,\"customerRef\":null,\"quantity\":null,\"baseUomId\":null,\"included\":true,\"exclusionReason\":null}," +
        "{\"nodeKey\":\"Shipment:L1:D1\",\"parentKey\":\"Lot:L1\",\"kind\":\"Shipment\",\"isRoot\":false,\"lotId\":\"L1\",\"skuId\":\"11111111-1111-1111-1111-111111111111\",\"warehouseId\":null,\"locationId\":null,\"stockStatus\":null,\"sourceDocumentId\":\"D1\",\"customerRef\":\"3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c\",\"quantity\":\"4\",\"baseUomId\":\"EA\",\"included\":true,\"exclusionReason\":null}]}";

    private static List<RecallScopeItem> Items(RecallCustomerRefSource? source, bool warning) =>
    [
        new() { NodeKey = "Lot:L1", Kind = RecallScopeItemKind.Lot, IsRoot = true, LotId = "L1", SkuId = Guid.Parse("11111111-1111-1111-1111-111111111111"), SkuLevel = SkuLevel.Lsku },
        new()
        {
            NodeKey = "Shipment:L1:D1", ParentKey = "Lot:L1", Kind = RecallScopeItemKind.Shipment, LotId = "L1", SkuId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SkuLevel = SkuLevel.Lsku, SourceDocumentId = "D1", CustomerRef = "3c9a1f2e-7b4d-4e61-9a2c-5d8e0f1a2b3c", Quantity = 4m, BaseUomId = "EA",
            CustomerRefSource = source, NoCrmWarning = warning
        }
    ];

    [Fact]
    public void The_scope_seal_is_the_pre_FU01_text_whatever_the_source_and_the_warning()
    {
        foreach (var (source, warning) in new[] { ((RecallCustomerRefSource?)null, false), (RecallCustomerRefSource.Crm, false), (RecallCustomerRefSource.Crm, true) })
        {
            var items = Items(source, warning);
            Assert.Equal(Vector, RecallScopeRules.Canonical(items, 1));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Vector))).ToLowerInvariant(), RecallScopeRules.Sha256(items, 1).ToLowerInvariant());
        }
    }

    [Fact]
    public async Task A_CRM_name_change_moves_no_scope_seal_and_the_trace_seal_does_not_see_the_key()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var shipment = await w.SellAsync(CrmWorld.A);
        var before = await w.GetAsync($"/api/recall/trace?rootLotId={w.Fg}&direction=Forward");
        _factory.Shipments.CustomerRefJson.TryRemove(shipment, out _);
        var withoutKey = await w.GetAsync($"/api/recall/trace?rootLotId={w.Fg}&direction=Forward");
        Assert.Equal(before.GetProperty("sha256").GetString(), withoutKey.GetProperty("sha256").GetString());

        _factory.Shipments.CustomerRefJson[shipment] = "\"" + CrmWorld.A.ToString("D") + "\"";
        var id = RecallScenario.Id(await w.DraftAsync());
        var first = (await w.GetAsync($"/api/recall/cases/{id}/scope")).GetProperty("sha256").GetString();
        w.Account(CrmWorld.A, "Yeni Ad Hastanesi", "ACC-NEW");
        var second = await w.GetAsync($"/api/recall/cases/{id}/scope");
        Assert.Equal("Yeni Ad Hastanesi", CrmWorld.Str(Assert.Single(second.GetProperty("items").EnumerateArray(), i => CrmWorld.Str(i, "sourceDocumentId") == shipment)
            .GetProperty("customer"), "name"));
        Assert.Equal(first, second.GetProperty("sha256").GetString());
    }
}

/// <summary>MOD-0177-FU01 AC-20 (Q-6) — a hand-entered Shipment item never names a CRM customer: a "D" GUID refuses the whole request.</summary>
public sealed class RecallCrmManualScopeTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmManualScopeTests(RecallApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_hand_entry_is_Manual_and_a_D_GUID_refuses_the_whole_request()
    {
        var s = new RecallScenario(_factory);
        var lot = await s.LotAsync();
        var c = await s.OpenAsync(lot);
        var id = RecallScenario.Id(c);
        var etag = RecallScenario.ETag(c);
        var a = CrmWorld.A.ToString("D");
        _factory.Crm.ResetCounts();

        // (a) a ship-to text → Manual / NoCrm, no CRM call
        var (sa, scope, _) = await s.AddItemsAsync(id, etag, RecallScenario.ShipmentItem(lot, "SHP-M-1", "CUST-300/ADDR-1", 2m));
        Assert.True(sa == 200, scope.ToString());
        var row = Assert.Single(scope.GetProperty("items").EnumerateArray(), i => CrmWorld.Str(i, "sourceDocumentId") == "SHP-M-1").GetProperty("customer");
        Assert.Equal(("Manual", "NoCrm", "CUST-300/ADDR-1"), (CrmWorld.Str(row, "source"), CrmWorld.Str(row, "state"), CrmWorld.Str(row, "label")));
        var stored = Assert.Single(s.Persisted("recall_scope_items"), d => d.Contains("SourceDocumentId") && d["SourceDocumentId"] == "SHP-M-1");
        Assert.Equal("Manual", stored["CustomerRefSource"].AsString);
        var customers = await s.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/customers", s.Qa1Token, null, 200);
        Assert.Equal(("Manual", "NoCrm", "CUST-300/ADDR-1"), (CrmWorld.Str(CrmWorld.Customer(customers[0]), "source"), CrmWorld.Str(CrmWorld.Customer(customers[0]), "state"),
            CrmWorld.Str(CrmWorld.Customer(customers[0]), "label")));
        Assert.Equal(0, _factory.Crm.Calls);
        etag = RecallScenario.ETag(scope);

        // (b) (c) (d) a "D" GUID in any case, padded → 400; (e) a batch with one GUID item writes nothing
        var counts = (s.Persisted("recall_scope_items").Count, s.Persisted("recall_case_events").Count, s.Persisted("inventory_audit").Count);
        foreach (var value in new[] { a, a.ToUpperInvariant(), "  " + a + " " })
        {
            var (st, body, _) = await s.AddItemsAsync(id, etag, RecallScenario.ShipmentItem(lot, "SHP-G-" + value.Trim()[..4], value, 1m));
            Assert.Equal((400, "INVALID_REQUEST"), (st, RecallScenario.Code(body)));
        }

        var (se, batch, _) = await s.AddItemsAsync(id, etag, RecallScenario.ShipmentItem(lot, "SHP-M-2", "CUST-301/ADDR-1", 1m), RecallScenario.ShipmentItem(lot, "SHP-M-3", a, 1m));
        Assert.Equal((400, "INVALID_REQUEST"), (se, RecallScenario.Code(batch)));
        Assert.Equal(counts, (s.Persisted("recall_scope_items").Count, s.Persisted("recall_case_events").Count, s.Persisted("inventory_audit").Count));

        // (e2) the B and N forms are free text
        var (sb, ok, _) = await s.AddItemsAsync(id, etag, RecallScenario.ShipmentItem(lot, "SHP-M-4", "{" + a + "}", 1m), RecallScenario.ShipmentItem(lot, "SHP-M-5", CrmWorld.A.ToString("N"), 1m));
        Assert.True(sb == 200, ok.ToString());
        foreach (var doc in new[] { "SHP-M-4", "SHP-M-5" })
        {
            var item = Assert.Single(ok.GetProperty("items").EnumerateArray(), i => CrmWorld.Str(i, "sourceDocumentId") == doc).GetProperty("customer");
            Assert.Equal(("Manual", "NoCrm"), (CrmWorld.Str(item, "source"), CrmWorld.Str(item, "state")));
        }
    }
}
