using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>Walks a JSON answer for a text: every path whose string value holds it. A CRM key may only sit in a <c>customerRef</c> member.</summary>
public static class RecallJsonScan
{
    public static List<string> PathsOf(JsonElement e, string needle, string path = "$")
    {
        var found = new List<string>();
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject()) found.AddRange(PathsOf(p.Value, needle, path + "." + p.Name));
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in e.EnumerateArray()) found.AddRange(PathsOf(item, needle, $"{path}[{i++}]"));
                break;
            case JsonValueKind.String:
                if (e.GetString()!.Contains(needle, StringComparison.OrdinalIgnoreCase)) found.Add(path);
                break;
        }

        return found;
    }

    /// <summary>The key only where it is a key: a member named customerRef (refs.customerRef included); nowhere a display text.</summary>
    public static void OnlyAsKey(JsonElement e, string key, string what)
    {
        var bad = PathsOf(e, key).Where(p => !p.EndsWith(".customerRef", StringComparison.Ordinal)).ToList();
        Assert.True(bad.Count == 0, $"{what}: the CRM key is shown at {string.Join(", ", bad)}");
    }
}

/// <summary>
/// MOD-0177-FU01 AC-10 (owner decision: "GUID hiçbir zaman gösterilmez", pack §8.8) — on every read and file of a case with CRM customer A,
/// the account id is only a key: never a label, a text, a customer display member, a CSV / PDF cell or an event text.
/// </summary>
public sealed class RecallCrmNoGuidDisplayTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmNoGuidDisplayTests(RecallApiFactory factory) => _factory = factory;

    private static string A => CrmWorld.A.ToString("D");

    private async Task<(CrmWorld W, string CaseId)> CaseAsync()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        w.FirstSaleIs(CrmWorld.A);
        await w.SellAsync(CrmWorld.A, quantity: "3");
        await w.SellRawAsync(null, shipTo: "CUST-100/ADDR-2", quantity: "2");
        var (caseId, _) = await w.ActiveAsync();
        var depo = w.S.Mint(w.S.Depo, RecallR3.DepoKeys);
        Assert.Equal(201, (await w.S.CommandAsync(caseId, "return-links", depo, w.Link(await w.ReturnAsync(), A), null)).Status);
        Assert.Equal(201, (await w.S.CommandAsync(caseId, "return-links", depo, w.Link(await w.ReturnAsync(), "CUST-100/ADDR-2"), null)).Status);
        // a later finding with A for the scope diff
        var derived = await w.S.LotAsync();
        await w.W.LinkAsync(w.Fg, derived, w.W.Order);
        await w.W.PostAsync(w.W.Lots.Receipt("3", derived, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", w.W.Order);
        var sale = w.W.Lots.Issue("1", derived);
        sale["sourceModule"] = "MOD-0183";
        var doc = Guid.NewGuid().ToString("D");
        sale["sourceDocumentId"] = doc;
        await w.W.Lots.PostAsync(sale, 201);
        _factory.Shipments.CustomerRefJson[doc] = "\"" + A + "\"";
        return (w, caseId);
    }

    private static async Task<IReadOnlyList<(string What, JsonElement Body)>> ReadsAsync(CrmWorld w, string caseId, string page = "")
    {
        var q = page.Length == 0 ? "" : "?customerPage=" + page;
        var t = page.Length == 0 ? "" : "&customerPage=" + page;
        return
        [
            ("customers", await w.GetAsync($"/api/recall/cases/{caseId}/customers{q}")),
            ("effectiveness", await w.GetAsync($"/api/recall/cases/{caseId}/effectiveness{q}")),
            ("scope", await w.GetAsync($"/api/recall/cases/{caseId}/scope{q}")),
            ("diff", await w.GetAsync($"/api/recall/cases/{caseId}/scope/diff{q}")),
            ("returns", await w.GetAsync($"/api/recall/cases/{caseId}/returns{q}")),
            ("timeline", await w.GetAsync($"/api/recall/cases/{caseId}/timeline?pageSize=200")),
            ("trace", await w.GetAsync($"/api/recall/trace?rootLotId={w.Fg}&direction=Forward{t}"))
        ];
    }

    private static async Task<string> FileAsync(CrmWorld w, string path, string language = "tr")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", w.Qa);
        request.Headers.AcceptLanguage.ParseAdd(language);
        using var response = await w.S.Client.SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(response.IsSuccessStatusCode, Encoding.UTF8.GetString(bytes));
        return Encoding.UTF8.GetString(bytes);
    }

    private static async Task<(string Csv, string Pdf, JsonElement Preview)> EvidenceAsync(CrmWorld w, string caseId)
    {
        var preview = await w.GetAsync($"/api/recall/evidence/preview?caseId={caseId}");
        var csv = await RecallCrmEvidence.ExportTextAsync(w, caseId, "Csv", "tr");
        var pdf = await RecallCrmEvidence.ExportTextAsync(w, caseId, "Pdf", "tr");
        return (csv, pdf, preview);
    }

    [Fact]
    public async Task Resolved_unresolved_notfound_forbidden_and_deferred_never_show_the_key()
    {
        var (w, caseId) = await CaseAsync();
        foreach (var state in new[] { "Resolved", "Unresolved", "NotFound", "Forbidden", "Deferred" })
        {
            _factory.Crm.Behaviour.Clear();
            w.Account(CrmWorld.A, CrmWorld.NameA, CrmWorld.CodeA);
            switch (state)
            {
                case "Unresolved": _factory.Crm.Behaviour[CrmWorld.A] = CrmBehaviour.ServiceUnavailable; break;
                case "NotFound": _factory.Crm.Accounts.TryRemove(CrmWorld.A, out _); break;
                case "Forbidden": _factory.Crm.Behaviour[CrmWorld.A] = CrmBehaviour.Forbidden; break;
            }

            foreach (var (what, body) in await ReadsAsync(w, caseId, state == "Deferred" ? "2" : ""))
            {
                RecallJsonScan.OnlyAsKey(body, A, $"{state} {what}");
            }

            if (state is "Forbidden")
            {
                foreach (var refused in new[]
                         {
                             (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/evidence/preview?caseId={caseId}", w.Qa)).Body,
                             (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/trace/export?rootLotId={w.Fg}&direction=Forward", w.Qa)).Body
                         })
                {
                    Assert.DoesNotContain(A, refused.GetRawText(), StringComparison.OrdinalIgnoreCase);
                }

                continue;
            }

            if (state is "Deferred") continue;
            var (csv, pdf, preview) = await EvidenceAsync(w, caseId);
            Assert.DoesNotContain(A, csv, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(A, pdf, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(A, preview.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(A, await FileAsync(w, $"/api/recall/trace/export?rootLotId={w.Fg}&direction=Forward"), StringComparison.OrdinalIgnoreCase);
        }

        _factory.Crm.Behaviour.Clear();
        w.Account(CrmWorld.A, CrmWorld.NameA, CrmWorld.CodeA);
    }

    [Fact]
    public async Task The_return_event_of_a_CRM_customer_carries_no_key_in_its_text()
    {
        var (w, caseId) = await CaseAsync();
        var timeline = await w.GetAsync($"/api/recall/cases/{caseId}/timeline?pageSize=200");
        var linked = timeline.GetProperty("items").EnumerateArray().Where(e => CrmWorld.Str(e, "kind") == "ReturnLinked").ToList();
        var crm = Assert.Single(linked, e => e.GetProperty("refs").GetProperty("customerRef").GetString() == A);
        Assert.DoesNotContain("→", CrmWorld.Str(crm, "text"), StringComparison.Ordinal);
        Assert.DoesNotContain(A, CrmWorld.Str(crm, "text"), StringComparison.OrdinalIgnoreCase);
        var legacy = Assert.Single(linked, e => e.GetProperty("refs").GetProperty("customerRef").GetString() == "CUST-100/ADDR-2");
        Assert.EndsWith(" → CUST-100/ADDR-2", CrmWorld.Str(legacy, "text"), StringComparison.Ordinal);
        var (_, _, preview) = await EvidenceAsync(w, caseId);
        Assert.DoesNotContain(A, preview.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_trace_CSV_words_CRM_cells_and_a_trace_without_CRM_nodes_is_unchanged()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var c = Guid.NewGuid();
        w.Account(c, "C Eczanesi", "ACC-C");
        await w.SellAsync(CrmWorld.A);
        var shipC = await w.SellAsync(c, shipTo: "CUST-200/ADDR-1");
        await w.SellRawAsync(null, shipTo: "CUST-400/ADDR-1");
        _factory.Crm.Behaviour[c] = CrmBehaviour.ServiceUnavailable;
        try
        {
            var trace = await w.GetAsync($"/api/recall/trace?rootLotId={w.Fg}&direction=Forward");
            var numberC = Assert.Single(RecallTraceWorld.Nodes(trace), n => CrmWorld.Str(n, "nodeKey").EndsWith(shipC)).GetProperty("movement").GetProperty("transactionNumber").GetString();
            var csv = await FileAsync(w, $"/api/recall/trace/export?rootLotId={w.Fg}&direction=Forward", "tr");
            Assert.Contains("\"Example Hastanesi · ACC-2026-000042\"", csv, StringComparison.Ordinal);
            Assert.Contains($"\"Çözülemedi (CUST-200/ADDR-1 · {numberC})\"", csv, StringComparison.Ordinal);
            Assert.Contains("\"CUST-400/ADDR-1 (CRM kimliği yok)\"", csv, StringComparison.Ordinal);
            Assert.DoesNotContain(CrmWorld.A.ToString("D"), csv, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _factory.Crm.Behaviour.Clear();
        }

        // a trace with no CRM node: the cell is the stored key, the same bytes in every language (no FU01 word)
        var plain = await CrmWorld.CreateAsync(_factory);
        await plain.SellRawAsync(null, shipTo: "CUST-401/ADDR-1");
        var tr = await FileAsync(plain, $"/api/recall/trace/export?rootLotId={plain.Fg}&direction=Forward", "tr");
        var en = await FileAsync(plain, $"/api/recall/trace/export?rootLotId={plain.Fg}&direction=Forward", "en");
        Assert.Equal(en, tr);
        var nodes = RecallTraceWorld.Nodes(await plain.GetAsync($"/api/recall/trace?rootLotId={plain.Fg}&direction=Forward"))
            .Where(n => CrmWorld.Str(n, "kind") == "Shipment").Select(n => CrmWorld.Str(n, "customerRef")).ToList();
        Assert.All(nodes, key => Assert.Contains($";\"{key}\";", tr, StringComparison.Ordinal));
        Assert.DoesNotContain("CRM", tr, StringComparison.Ordinal);
    }
}

/// <summary>MOD-0177-FU01 AC-10 (d) — a drill run keeps the CRM key only in its <c>customers</c> key list (never drawn, never in the evidence).</summary>
public sealed class RecallCrmDrillKeyTests : IClassFixture<RecallDrillApiFactory>
{
    private readonly RecallDrillApiFactory _factory;

    public RecallCrmDrillKeyTests(RecallDrillApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_drill_run_holds_the_key_only_in_customers()
    {
        var w = await DrillWorld.CreateAsync(_factory);
        _factory.Shipments.CustomerRefJson[w.Trace.Shipment] = "\"" + CrmWorld.A.ToString("D") + "\"";
        var plan = await w.PlanAsync("SelectedLot");
        var run = await w.StartAsync(DrillWorld.PlanId(plan), "Manual", w.Trace.Fg);
        Assert.True(run.Status == 201, run.Body.ToString());
        var done = await w.StepsAsync(run.Body);
        var read = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/drill-runs/{DrillWorld.RunId(done)}", w.Runner, null, 200);
        Assert.Contains(CrmWorld.A.ToString("D"), read.GetProperty("customers").EnumerateArray().Select(c => c.GetString()));
        var bad = RecallJsonScan.PathsOf(read, CrmWorld.A.ToString("D")).Where(p => !Regex.IsMatch(p, @"^\$\.customers\[\d+\]$")).ToList();
        Assert.True(bad.Count == 0, string.Join(", ", bad));
    }
}

/// <summary>The evidence export helper of the FU01 tests: the stored file's text (PDF text through the suite's reader).</summary>
public static class RecallCrmEvidence
{
    public static async Task<(int Status, JsonElement Body)> CreateAsync(CrmWorld w, string caseId, string format, string language, string? key = null, string? token = null)
    {
        key ??= "ex-" + Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/recall/evidence/exports")
        {
            Content = JsonContent.Create(new Dictionary<string, object?> { ["idempotencyKey"] = key, ["caseId"] = caseId, ["format"] = format })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? w.Qa);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        request.Headers.AcceptLanguage.ParseAdd(language);
        using var response = await w.S.Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    public static async Task<byte[]> FileAsync(CrmWorld w, string exportId)
    {
        var (status, bytes, _) = await RecallEvidence.DownloadAsync(w.S, w.Qa, exportId);
        Assert.Equal(200, status);
        return bytes;
    }

    public static async Task<string> ExportTextAsync(CrmWorld w, string caseId, string format, string language)
    {
        var (status, export) = await CreateAsync(w, caseId, format, language);
        Assert.True(status == 201, export.ToString());
        var bytes = await FileAsync(w, CrmWorld.Str(export, "exportId"));
        return format == "Pdf" ? Diten.InventoryService.Tests.Expiry.ExpiryEvidenceTests.PdfText.Read(bytes).Text : Encoding.UTF8.GetString(bytes);
    }
}
