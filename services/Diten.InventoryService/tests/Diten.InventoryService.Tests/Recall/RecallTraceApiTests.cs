using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// The WP-0177-R2 world on the real host: the <see cref="RecallScenario"/> plus a production graph posted through the REAL ledger and TRACE
/// APIs — a raw-material lot (PO receipt) issued to a production order, the finished-good lot received from it and linked by genealogy, the
/// finished good in stock and shipped on an MVP-6 shipment (sourceModule MOD-0183, the document is the shipment id). The shipment read
/// lands on <see cref="RecallApiFactory.Shipments"/>. Rows that the APIs refuse by design (a lotless sale of a lot-controlled SKU, another
/// company's movement) are seeded as clones of a real ledger row with the one field changed.
/// </summary>
public sealed class RecallTraceWorld
{
    public static readonly string[] TraceKeys = [.. RecallScenario.QaKeys, "recall.trace.run", "recall.trace.export"];

    private RecallTraceWorld(RecallScenario s) => S = s;

    public RecallScenario S { get; }
    public string Token { get; private set; } = "";
    public string Rm { get; private set; } = "";
    public string Fg { get; private set; } = "";
    public string Shipment { get; private set; } = "";
    public string Order { get; private set; } = "";
    public LotLedgerScenario Lots => S.Lots;

    public static async Task<RecallTraceWorld> CreateAsync(RecallApiFactory factory)
    {
        var w = new RecallTraceWorld(new RecallScenario(factory));
        w.Token = w.S.Mint(w.S.Qa1, TraceKeys);
        w.Rm = await w.S.LotAsync();
        w.Fg = await w.S.LotAsync();
        w.Order = "MO-" + Guid.NewGuid().ToString("N")[..8];
        await w.PostAsync(w.Lots.Receipt("100", w.Rm, movementType: "GOODS_RECEIPT_PO"), "MOD-0172", "PO-" + w.Order);
        await w.PostAsync(w.Lots.Issue("40", w.Rm, movementType: "GOODS_ISSUE_PROD"), "MOD-0194", w.Order);
        await w.PostAsync(w.Lots.Receipt("10", w.Fg, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", w.Order);
        await w.LinkAsync(w.Rm, w.Fg, w.Order);
        w.Shipment = await w.SellAsync(w.Fg, "4");
        return w;
    }

    public async Task<string> PostAsync(Dictionary<string, object?> body, string module, string? document)
    {
        body["sourceModule"] = module;
        if (document is not null) body["sourceDocumentId"] = document;
        return await Lots.PostOkAsync(body);
    }

    /// <summary>A GOODS_ISSUE_SALES of the lot on a new MVP-6 shipment; returns the shipment id. R3: <paramref name="expected"/> 422 = refused (exit block).</summary>
    public async Task<string> SellAsync(string lotId, string quantity, int expected = 201)
    {
        var shipment = Guid.NewGuid().ToString("D");
        var body = Lots.Issue(quantity, lotId);
        body["sourceModule"] = "MOD-0183";
        body["sourceDocumentId"] = shipment;
        await Lots.PostAsync(body, expected);
        return shipment;
    }

    public async Task LinkAsync(string parent, string child, string reference)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = "gl-" + Guid.NewGuid().ToString("N"),
            ["parentLotId"] = parent,
            ["childLotId"] = child,
            ["transformationReference"] = reference,
            ["sourceModule"] = "MOD-0194"
        };
        using var response = await Lots.Send(HttpMethod.Post, $"/api/trace/lots/{child}/genealogy", body,
            r => r.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!));
        Assert.True((int)response.StatusCode is 200 or 201, await response.Content.ReadAsStringAsync());
    }

    public Task<JsonElement> TraceAsync(string root, string direction = "Both", string? token = null, int expected = 200)
        => S.ExpectAsync(HttpMethod.Get, $"/api/recall/trace?rootLotId={root}&direction={direction}", token ?? Token, null, expected);

    /// <summary>A clone of a real ledger row of this world (by its document), with <paramref name="change"/> applied — new id and number.</summary>
    public void CloneTransaction(string sourceDocumentId, Action<BsonDocument> change)
    {
        var original = Assert.Single(S.Persisted("inventory_transactions"), d => d.Contains("SourceDocumentId") && d["SourceDocumentId"] == sourceDocumentId);
        var clone = original.DeepClone().AsBsonDocument;
        clone["_id"] = original["_id"].IsString ? Guid.NewGuid().ToString("D") : new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
        if (clone.Contains("TransactionNumber")) clone["TransactionNumber"] = "INV-CLONE-" + Guid.NewGuid().ToString("N")[..8];
        if (clone.Contains("IdempotencyKey")) clone["IdempotencyKey"] = "clone-" + Guid.NewGuid().ToString("N");
        change(clone);
        S.Factory.Database.GetCollection<BsonDocument>("inventory_transactions").InsertOne(clone);
    }

    public static BsonValue SameShape(BsonValue like, Guid value)
        => like.IsString ? value.ToString("D") : new BsonBinaryData(value, GuidRepresentation.Standard);

    public static IEnumerable<JsonElement> Nodes(JsonElement trace) => trace.GetProperty("nodes").EnumerateArray();

    public static IEnumerable<JsonElement> Gaps(JsonElement trace, string code)
        => trace.GetProperty("gaps").EnumerateArray().Where(g => g.GetProperty("code").GetString() == code);

    public static JsonElement Node(JsonElement trace, string key) => Assert.Single(Nodes(trace), n => n.GetProperty("nodeKey").GetString() == key);
}

/// <summary>
/// WP-0177-R2 — runTrace / exportTrace / getCaseScopeDiff and the scope freezing (pack §8.2, §8.3, §8.6) on the real host: seeded graphs
/// through the real APIs, the CT-R8 company rule (no leak), the shipment read (Gateway path, caller token + company, no cache, degrade),
/// open-from-trace, submit snapshot, approval re-trace (409 RECALL_SCOPE_CHANGED with the diff), the approved snapshot after approval,
/// "Yeniden izle" out-of-scope findings, the CSV, permissions and the audit rows. Every response is checked against RECALL-BUNDLE 1.1.0.
/// </summary>
public sealed class RecallTraceApiTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallTraceApiTests(RecallApiFactory factory) => _factory = factory;

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    // ── the trace ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Multi_level_trace_over_the_real_ledger_and_genealogy_with_the_MVP6_shipment()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var before = _factory.Shipments.Requests;

        var trace = await w.TraceAsync(w.Fg);

        Assert.Equal(w.Fg, Str(trace, "rootLotId"));
        Assert.Equal("rt-v1", Str(trace, "canonicalVersion"));
        Assert.Matches("^[0-9a-f]{64}$", Str(trace, "sha256"));
        Assert.True(trace.GetProperty("durationMs").GetInt64() >= 0);
        Assert.False(trace.GetProperty("truncated").GetBoolean());

        var root = RecallTraceWorld.Node(trace, RecallScenario.LotKey(w.Fg));
        Assert.Equal(("FinishedGoodLot", "Root"), (Str(root, "kind"), Str(root, "side")));
        var rm = RecallTraceWorld.Node(trace, RecallScenario.LotKey(w.Rm));
        Assert.Equal(("RawMaterialLot", "Backward", 1), (Str(rm, "kind"), Str(rm, "side"), rm.GetProperty("level").GetInt32()));
        Assert.Contains(trace.GetProperty("edges").EnumerateArray(), e => Str(e, "from") == RecallScenario.LotKey(w.Rm) && Str(e, "to") == RecallScenario.LotKey(w.Fg));

        var stock = Assert.Single(RecallTraceWorld.Nodes(trace), n => Str(n, "kind") == "Stock");
        Assert.Equal(6m, stock.GetProperty("quantity").GetDecimal()); // 10 received − 4 shipped
        var shipment = RecallTraceWorld.Node(trace, RecallScenario.ShipmentKey(w.Fg, w.Shipment));
        Assert.Equal(4m, shipment.GetProperty("quantity").GetDecimal()); // the ledger's quantity — the shipment lines (999999) are never used
        Assert.Equal("Ok", Str(shipment.GetProperty("shipment"), "read"));
        Assert.Equal(ShipmentBundleDouble.ShipTo(w.Shipment), Str(shipment, "customerRef"));
        Assert.Empty(trace.GetProperty("gaps").EnumerateArray());

        // the read went through the Gateway path with the caller's token and company
        Assert.Equal(before + 1, _factory.Shipments.Requests);
        var seen = _factory.Shipments.Seen.Last(x => x.Path.EndsWith(w.Shipment, StringComparison.Ordinal));
        Assert.Equal($"/api/shipment-bundle/shipments/{w.Shipment}", seen.Path);
        Assert.Equal(w.S.Ledger.LegalEntity.ToString("D"), seen.LegalEntity);
        Assert.Equal("Bearer " + w.Token, seen.Authorization);

        var audit = Assert.Single(w.S.Audit("recall.trace.run"), a => a["Detail"]["context"] == "runTrace");
        Assert.Equal(Str(trace, "sha256"), audit["Detail"]["sha256"].AsString);
    }

    [Fact]
    public async Task Two_runs_give_the_same_seal_and_every_run_reads_the_shipment_again()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var before = _factory.Shipments.Requests;

        var first = await w.TraceAsync(w.Fg);
        var second = await w.TraceAsync(w.Fg);

        Assert.Equal(Str(first, "sha256"), Str(second, "sha256"));
        Assert.Equal(RecallTraceWorld.Nodes(first).Select(n => Str(n, "nodeKey")), RecallTraceWorld.Nodes(second).Select(n => Str(n, "nodeKey")));
        Assert.Equal(before + 2, _factory.Shipments.Requests); // no cache (pack §8.6)
    }

    [Theory]
    [InlineData(ShipmentDoubleBehaviour.Unauthorized, "Forbidden")]
    [InlineData(ShipmentDoubleBehaviour.NotFound, "NotFound")]
    [InlineData(ShipmentDoubleBehaviour.Timeout, "Unavailable")]
    public async Task Shipment_read_degrades_and_the_trace_still_answers_with_the_same_seal(ShipmentDoubleBehaviour behaviour, string read)
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var ok = await w.TraceAsync(w.Fg);
        _factory.Shipments.Behaviour[w.Shipment] = behaviour;

        var degraded = await w.TraceAsync(w.Fg);

        var shipment = RecallTraceWorld.Node(degraded, RecallScenario.ShipmentKey(w.Fg, w.Shipment));
        Assert.Equal(read, Str(shipment.GetProperty("shipment"), "read"));
        Assert.Equal("DOC:" + w.Shipment, Str(shipment, "customerRef"));
        Assert.Equal(Str(ok, "sha256"), Str(degraded, "sha256"));
        Assert.Equal(RecallTraceWorld.Nodes(ok).Count(), RecallTraceWorld.Nodes(degraded).Count());
    }

    [Fact]
    public async Task Gap_rules_on_the_real_ledger_lotless_typed_lot_missing_link_and_production_without_output_link()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        // a second raw material issued to the same order without a genealogy link
        var rm2 = await w.S.LotAsync();
        await w.PostAsync(w.Lots.Receipt("50", rm2, movementType: "GOODS_RECEIPT_PO"), "MOD-0172", "PO-2-" + w.Order);
        await w.PostAsync(w.Lots.Issue("5", rm2, movementType: "GOODS_ISSUE_PROD"), "MOD-0194", w.Order);
        // the finished good consumed by an order with no output lot
        await w.PostAsync(w.Lots.Issue("1", w.Fg, movementType: "GOODS_ISSUE_PROD"), "MOD-0194", "MO-NOLINK-" + w.Order);
        // a typed lot text (the migration did not link it) — received, then sold
        await w.PostAsync(w.Lots.Receipt("1", "LOT-TXT-0177", movementType: "GOODS_RECEIPT_PO"), "MOD-0172", "PO-TXT-" + w.Order);
        await w.PostAsync(w.Lots.Issue("1", "LOT-TXT-0177", movementType: "GOODS_ISSUE_SALES"), "MOD-0183", Guid.NewGuid().ToString("D"));
        // a lotless sale of the lot-controlled SKU (the API refuses it — a clone of the real sale without its lot)
        var lotless = Guid.NewGuid().ToString("D");
        w.CloneTransaction(w.Shipment, d => { d["LotId"] = BsonNull.Value; d["SourceDocumentId"] = lotless; });

        var trace = await w.TraceAsync(w.Fg);

        Assert.Single(RecallTraceWorld.Gaps(trace, "MISSING_GENEALOGY_LINK"), g => Str(g, "nodeKey").EndsWith(rm2, StringComparison.Ordinal));
        Assert.Single(RecallTraceWorld.Gaps(trace, "CONSUMED_WITHOUT_OUTPUT_LINK"));
        Assert.Single(RecallTraceWorld.Gaps(trace, "UNRESOLVED_TYPED_LOT"));
        Assert.Single(RecallTraceWorld.Gaps(trace, "LOTLESS_OUTBOUND"));
        Assert.Equal(4, trace.GetProperty("summary").GetProperty("gapCount").GetInt32());
        Assert.DoesNotContain(RecallTraceWorld.Nodes(trace), n => n.TryGetProperty("untracked", out var u) && u.GetBoolean());
    }

    [Fact]
    public async Task Other_company_activity_is_one_count_and_leaks_no_lot_quantity_or_document_CT_R8()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var otherCompany = Guid.NewGuid();
        const string secretDocument = "SHP-OTHER-COMPANY-SECRET-0177";
        w.CloneTransaction(w.Shipment, d =>
        {
            d["LegalEntityId"] = RecallTraceWorld.SameShape(d["LegalEntityId"], otherCompany);
            d["SourceDocumentId"] = secretDocument;
            d["TransactionNumber"] = "INV-OTHER-SECRET";
            foreach (var name in d.Names.Where(n => n.Contains("Quantity", StringComparison.Ordinal) && d[n].IsDecimal128).ToList()) d[name] = new Decimal128(7777.123m);
        });

        var (status, _, raw) = await w.S.SendAsync(HttpMethod.Get, $"/api/recall/trace?rootLotId={w.Fg}", w.Token);
        Assert.Equal(200, status);
        var text = await raw.Content.ReadAsStringAsync();
        var trace = JsonDocument.Parse(text).RootElement;

        var gap = Assert.Single(RecallTraceWorld.Gaps(trace, "OTHER_COMPANY_ACTIVITY"));
        Assert.Equal(1, gap.GetProperty("count").GetInt64());
        Assert.Equal(1, trace.GetProperty("summary").GetProperty("otherCompanyActivityCount").GetInt64());
        foreach (var member in new[] { "lot", "quantity", "movement", "transactionId", "skuId" })
        {
            Assert.True(!gap.TryGetProperty(member, out var v) || v.ValueKind == JsonValueKind.Null, $"the other-company row carries {member}: {gap}");
        }

        Assert.DoesNotContain(secretDocument, text, StringComparison.Ordinal);
        Assert.DoesNotContain("INV-OTHER-SECRET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("7777.123", text, StringComparison.Ordinal);
        Assert.DoesNotContain(otherCompany.ToString("D"), text, StringComparison.OrdinalIgnoreCase);

        // the other company sees its own document, and this company's activity only as a count
        var there = await w.TraceAsync(w.Fg, token: w.S.Mint(w.S.Qa1, RecallTraceWorld.TraceKeys, otherCompany));
        Assert.Contains(RecallTraceWorld.Nodes(there), n => n.GetProperty("nodeKey").GetString() == RecallScenario.ShipmentKey(w.Fg, secretDocument));
        Assert.DoesNotContain(RecallTraceWorld.Nodes(there), n => n.GetProperty("nodeKey").GetString() == RecallScenario.ShipmentKey(w.Fg, w.Shipment));
        Assert.True(Assert.Single(RecallTraceWorld.Gaps(there, "OTHER_COMPANY_ACTIVITY")).GetProperty("count").GetInt64() >= 4);
    }

    [Fact]
    public async Task Trace_refuses_without_the_key_an_unknown_lot_and_a_bad_direction()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);

        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/trace?rootLotId={w.Fg}", w.S.Qa1Token)).Status);
        Assert.Equal(403, (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/trace/export?rootLotId={w.Fg}", w.S.Mint(w.S.Qa1, ["recall.trace.run"]))).Status);
        Assert.Equal("UNKNOWN_LOT", RecallScenario.Code(await w.TraceAsync(Guid.NewGuid().ToString("D"), expected: 404)));
        Assert.Equal("INVALID_REQUEST", RecallScenario.Code(await w.TraceAsync(w.Fg, direction: "Sideways", expected: 400)));
        Assert.Equal("INVALID_REQUEST", RecallScenario.Code(await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/trace?rootLotId=not-a-uuid", w.Token, null, 400)));
    }

    // ── CSV ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Csv_export_is_the_rule_10_order_with_a_BOM_and_is_audited_as_a_trace_not_as_evidence()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var trace = await w.TraceAsync(w.Fg);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/recall/trace/export?rootLotId={w.Fg}&format=csv");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", w.Token);
        using var raw = await w.S.Client.SendAsync(request);

        Assert.Equal(200, (int)raw.StatusCode);
        Assert.Equal($"izleme_{RecallTraceWorld.Node(trace, RecallScenario.LotKey(w.Fg)).GetProperty("lot").GetProperty("lotNumber").GetString()}.csv",
            raw.Content.Headers.ContentDisposition!.FileNameStar ?? raw.Content.Headers.ContentDisposition.FileName);
        Assert.Equal("text/csv", raw.Content.Headers.ContentType!.MediaType);
        var bytes = await raw.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var lines = System.Text.Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("\"Düğüm türü\";\"SKU\";\"Lot\";", lines[0], StringComparison.Ordinal);
        Assert.Equal(RecallTraceWorld.Nodes(trace).Count(), lines.Length - 1);
        Assert.Contains(w.Shipment, lines.Single(l => l.Contains(w.Shipment, StringComparison.Ordinal)));
        Assert.Contains(RecallTraceWorld.Nodes(trace).First().GetProperty("lot").GetProperty("lotNumber").GetString()!, lines[1], StringComparison.Ordinal);

        Assert.Single(w.S.Audit("recall.trace.run"), a => a["Detail"]["context"] == "exportTrace");
        Assert.DoesNotContain(w.S.Persisted("inventory_audit"), a => a["Action"].AsString.StartsWith("recall.evidence", StringComparison.Ordinal));
        Assert.Equal(400, (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/trace/export?rootLotId={w.Fg}&format=xlsx", w.Token)).Status);
    }

    // ── scope freezing (pack §8.3) ────────────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> OpenAsync(RecallTraceWorld w)
    {
        var body = RecallScenario.Open(w.Fg);
        body["direction"] = "Forward";
        var (status, json, _) = await w.S.PostOpenAsync(w.S.Qa1Token, body);
        Assert.True(status == 201, json.ToString());
        return json;
    }

    [Fact]
    public async Task Open_from_trace_submit_freezes_a_snapshot_and_approval_retrace_refuses_a_changed_scope_with_the_diff()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);

        // 1. open: the scope comes from the trace — root, stock, shipment; all Trace items, all included
        var opened = await OpenAsync(w);
        var id = RecallScenario.Id(opened);
        var scope = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope", w.S.Qa1Token, null, 200);
        var items = scope.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { "Lot", "Shipment", "Stock" }, items.Select(i => Str(i, "kind")).Order());
        Assert.All(items, i => Assert.Equal("Trace", Str(i, "origin")));
        Assert.Contains(items, i => Str(i, "nodeKey") == RecallScenario.ShipmentKey(w.Fg, w.Shipment) && Str(i, "customerRef") == ShipmentBundleDouble.ShipTo(w.Shipment));
        Assert.False(scope.TryGetProperty("snapshot", out _));

        // 2. submit: re-trace, canonical rt-v1, SHA-256, Submitted snapshot
        var (ss, submitted, _) = await w.S.SubmitAsync(id, RecallScenario.ETag(scope));
        Assert.True(ss == 200, submitted.ToString());
        var submittedSnapshot = submitted.GetProperty("submittedSnapshot");
        Assert.Equal(("Submitted", "rt-v1"), (Str(submittedSnapshot, "purpose"), Str(submittedSnapshot, "canonicalVersion")));
        Assert.Equal(Str(await w.TraceAsync(w.Fg, "Forward"), "sha256"), Str(submittedSnapshot, "sha256"));
        var stored = Assert.Single(w.S.Persisted("recall_trace_snapshots"), d => d["CaseId"].ToString() == id || d["CaseId"].AsGuidSafe() == id);
        Assert.Equal(Str(submittedSnapshot, "sha256"), stored["Sha256"].AsString);

        // 3. prepare is fine — then a new shipment changes the live trace: approval re-traces and refuses with the diff
        var (sp, prepared, _) = await w.S.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(submitted), w.S.Qa2Token);
        Assert.True(sp == 200, prepared.ToString());
        var late = await w.SellAsync(w.Fg, "1");
        var signature = _factory.Signature.Issue(w.S.Qa2, id, Str(prepared, "actionCode"));
        var (sa, refused, _) = await w.S.ApproveAsync(id, RecallScenario.ETag(prepared), w.S.Qa2Token, signature);
        Assert.Equal(409, sa);
        Assert.Equal("RECALL_SCOPE_CHANGED", RecallScenario.Code(refused));
        var details = refused.GetProperty("error").GetProperty("details");
        Assert.Equal(Str(submittedSnapshot, "sha256"), Str(details, "submittedSha256"));
        Assert.NotEqual(Str(details, "submittedSha256"), Str(details, "currentSha256"));
        Assert.Contains(RecallScenario.ShipmentKey(w.Fg, late), details.GetProperty("added").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("PendingScopeApproval", Str(await w.S.GetAsync(id), "status"));

        // 4. a new preparation sees the drift too (no stale decision is prepared)
        var current = await w.S.GetAsync(id);
        var (sp2, prepared2, _) = await w.S.PrepareAsync(id, "ApproveScope", RecallScenario.ETag(current), w.S.Qa2Token);
        Assert.Equal((409, "RECALL_SCOPE_CHANGED"), (sp2, RecallScenario.Code(prepared2)));

        // 5. return + resubmit: the re-trace adds the new shipment as a new item; approval freezes the Approved snapshot
        var (sr, returned, _) = await w.S.CommandAsync(id, "scope/return", w.S.Qa2Token,
            new Dictionary<string, object?> { ["idempotencyKey"] = RecallScenario.Key("ret"), ["reason"] = "Yeni sevkiyat kapsama girmeli" }, RecallScenario.ETag(current));
        Assert.True(sr == 200, returned.ToString());
        var (ss2, resubmitted, _) = await w.S.SubmitAsync(id, RecallScenario.ETag(returned));
        Assert.True(ss2 == 200, resubmitted.ToString());
        var rescope = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope", w.S.Qa1Token, null, 200);
        var added = Assert.Single(rescope.GetProperty("items").EnumerateArray(), i => Str(i, "nodeKey") == RecallScenario.ShipmentKey(w.Fg, late));
        Assert.True(added.GetProperty("new").GetBoolean());

        var approved = await w.S.ApproveSignedAsync(id, resubmitted, w.S.Qa2, w.S.Qa2Token);
        Assert.Equal("Active", Str(approved, "status"));
        var approvedSnapshot = approved.GetProperty("approvedSnapshot");
        Assert.Equal("Approved", Str(approvedSnapshot, "purpose"));
        Assert.Equal(Str(resubmitted.GetProperty("submittedSnapshot"), "sha256"), Str(approvedSnapshot, "sha256"));

        // 6. after approval the reads show the approved snapshot; snapshots are insert-only (2 submitted + 1 approved, none rewritten)
        var frozen = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope", w.S.Qa1Token, null, 200);
        Assert.Equal(("Approved", Str(approvedSnapshot, "sha256")), (Str(frozen.GetProperty("snapshot"), "purpose"), Str(frozen.GetProperty("snapshot"), "sha256")));
        var snapshots = w.S.Persisted("recall_trace_snapshots").Where(d => d["CaseId"].ToString() == id || d["CaseId"].AsGuidSafe() == id).ToList();
        Assert.Equal(new[] { "Approved", "Submitted", "Submitted" }, snapshots.Select(d => d["Purpose"].ToString()!).Order());
        Assert.Equal(3, snapshots.Select(d => d["Sha256"].AsString + d["_id"]).Distinct().Count());

        // 7. "Yeniden izle": a node after approval is an out-of-scope finding — recorded once, the scope does not change. R3: a sale of the recalled
        // lot is refused now (LOT_UNDER_RECALL), so the finding is the stock line KR-B moved into QUARANTINE (a node the approved snapshot lacks)
        await w.SellAsync(w.Fg, "1", expected: 422);
        var quarantined = $"Stock:{w.Fg}:{w.S.Ledger.Warehouse}:{w.S.Ledger.LocationA}:QUARANTINE";
        var diff = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope/diff", w.S.Qa1Token, null, 200);
        Assert.Equal(Str(approvedSnapshot, "sha256"), Str(diff, "approvedSha256"));
        Assert.Contains(diff.GetProperty("added").EnumerateArray(), n => Str(n, "nodeKey") == quarantined);
        await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope/diff", w.S.Qa1Token, null, 200);
        var timeline = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/timeline?pageSize=200", w.S.Qa1Token, null, 200);
        var findings = timeline.GetProperty("items").EnumerateArray().Where(e => Str(e, "kind") == "OutOfScopeFinding").ToList();
        Assert.Contains(findings, f => f.GetProperty("refs").GetProperty("nodeKey").GetString() == quarantined);
        Assert.Equal(findings.Count, findings.Select(f => f.GetProperty("refs").GetProperty("nodeKey").GetString()).Distinct().Count());
        var unchanged = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope", w.S.Qa1Token, null, 200);
        Assert.DoesNotContain(unchanged.GetProperty("items").EnumerateArray(), i => Str(i, "nodeKey") == quarantined);
        Assert.Equal(Str(frozen, "sha256"), Str(unchanged, "sha256"));

        // AUD-001: trace runs, submit, the scope diff, the finding
        Assert.NotEmpty(w.S.Audit("recall.scope.diffed"));
        Assert.NotEmpty(w.S.Audit("recall.scope.out-of-scope-finding"));
        Assert.Contains(w.S.Audit("recall.trace.run"), a => a["Detail"]["context"] == "submitScope");
        Assert.Contains(w.S.Audit("recall.trace.run"), a => a["Detail"]["context"] == "openCase");
    }

    [Fact]
    public async Task Scope_diff_before_approval_is_an_invalid_transition_and_another_company_gets_403()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var id = RecallScenario.Id(await OpenAsync(w));

        var early = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope/diff", w.S.Qa1Token, null, 409);
        Assert.Equal("RECALL_INVALID_TRANSITION", RecallScenario.Code(early));
        var other = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope/diff", w.S.Mint(w.S.Qa1, RecallScenario.QaKeys, Guid.NewGuid()), null, 403);
        Assert.Equal("CROSS_LE_FORBIDDEN", RecallScenario.Code(other));
    }

    [Fact]
    public async Task Manual_items_survive_every_re_trace()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var opened = await OpenAsync(w);
        var id = RecallScenario.Id(opened);
        var (sa, scope, _) = await w.S.AddItemsAsync(id, RecallScenario.ETag(opened), RecallScenario.ShipmentItem(w.Fg, "SHP-MANUAL-1", "CUST-MANUAL", 2m));
        Assert.True(sa == 200, scope.ToString());

        var (ss, submitted, _) = await w.S.SubmitAsync(id, RecallScenario.ETag(scope));
        Assert.True(ss == 200, submitted.ToString());

        var after = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{id}/scope", w.S.Qa1Token, null, 200);
        var manual = Assert.Single(after.GetProperty("items").EnumerateArray(), i => Str(i, "nodeKey") == RecallScenario.ShipmentKey(w.Fg, "SHP-MANUAL-1"));
        Assert.Equal("Manual", Str(manual, "origin"));
    }
}

internal static class BsonGuidExtensions
{
    public static string? AsGuidSafe(this BsonValue value)
        => value.IsBsonBinaryData ? value.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard).ToString("D") : value.IsString ? value.AsString : null;
}
