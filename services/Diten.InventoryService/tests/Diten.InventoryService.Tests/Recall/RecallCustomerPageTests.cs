using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// MOD-0177-FU01 AC-28 (CT F-1@fbafd3af; pack §8.6) — the customer page on a healthy but slow CRM (V200: 200 CRM customers, 200 ms each): a
/// screen read resolves exactly the requested 25-customer slice of its own customer keys (ordinal), the rest is Deferred, within 3 s.
/// Timing measurements run alone (the recall performance collection: after the parallel ones have finished).
/// </summary>
[Collection(RecallPerformanceCollection.Name)]
public sealed class RecallCustomerPageTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCustomerPageTests(RecallApiFactory factory) => _factory = factory;

    private async Task<(CrmWorld W, string CaseId, List<string> Keys)> V200Async()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        w.FirstSaleIs(CrmWorld.A);
        var (id, _) = await w.ActiveAsync();
        var seeded = w.SeedCustomers(id, 199);
        var keys = seeded.Select(g => g.ToString("D")).Append(CrmWorld.A.ToString("D")).Order(StringComparer.Ordinal).ToList();
        return (w, id, keys);
    }

    private static Dictionary<string, string> States(JsonElement rows)
        => rows.EnumerateArray().ToDictionary(r => CrmWorld.Str(r, "customerRef"), r => CrmWorld.Str(CrmWorld.Customer(r), "state"));

    [Fact]
    public async Task Customers_page_by_page_within_3_s()
    {
        var (w, id, keys) = await V200Async();
        _factory.Crm.DefaultDelay = TimeSpan.FromMilliseconds(200);
        try
        {
            // (a) no parameter = page 1
            _factory.Crm.ResetCounts();
            var clock = Stopwatch.StartNew();
            var rows = await w.CustomersAsync(id);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), clock.Elapsed.ToString());
            Assert.Equal(200, rows.GetArrayLength());
            Assert.Equal(keys, rows.EnumerateArray().Select(r => CrmWorld.Str(r, "customerRef")));
            var states = States(rows);
            Assert.All(keys.Take(25), k => Assert.Equal("Resolved", states[k]));
            Assert.All(keys.Skip(25), k => Assert.Equal("Deferred", states[k]));
            Assert.All(rows.EnumerateArray().Skip(25), r =>
            {
                var c = CrmWorld.Customer(r);
                Assert.False(c.TryGetProperty("name", out _) || c.TryGetProperty("code", out _) || c.TryGetProperty("shipTo", out _) || c.TryGetProperty("shipmentNumber", out _));
            });
            Assert.Equal(25, _factory.Crm.Calls);

            // (b) pages 2 … 8: each its own slice; together every account exactly once
            var resolved = new List<string>();
            for (var page = 2; page <= 8; page++)
            {
                var pageStates = States(await w.CustomersAsync(id, page));
                Assert.All(keys.Skip((page - 1) * 25).Take(25), k => Assert.Equal("Resolved", pageStates[k]));
                resolved.AddRange(pageStates.Where(p => p.Value == "Resolved").Select(p => p.Key));
            }

            Assert.Equal(keys.Skip(25).Order(StringComparer.Ordinal), resolved.Order(StringComparer.Ordinal));

            // (c) past the end: 0 calls, all Deferred; 0 and x → 400
            _factory.Crm.ResetCounts();
            Assert.All(States(await w.CustomersAsync(id, 9)).Values, s => Assert.Equal("Deferred", s));
            Assert.Equal(0, _factory.Crm.Calls);
            foreach (var bad in new[] { "0", "x", "-1", "1.5" })
            {
                var (status, body, _) = await w.S.SendAsync(HttpMethod.Get, $"/api/recall/cases/{id}/customers?customerPage={bad}", w.Qa);
                Assert.Equal((400, "INVALID_REQUEST"), (status, RecallScenario.Code(body)));
            }

            // (f) two pages: the same rows, only "customer" differs
            static string Without(JsonElement rows)
            {
                var node = JsonNode.Parse(rows.GetRawText())!.AsArray();
                foreach (var row in node) row!.AsObject().Remove("customer");
                return node.ToJsonString();
            }

            Assert.Equal(Without(await w.CustomersAsync(id, 1)), Without(await w.CustomersAsync(id, 3)));

            // (e) 500 ms (the healthy bound): page 1 fully resolved within 3 s
            _factory.Crm.DefaultDelay = TimeSpan.FromMilliseconds(500);
            clock.Restart();
            states = States(await w.CustomersAsync(id));
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), clock.Elapsed.ToString());
            Assert.All(keys.Take(25), k => Assert.Equal("Resolved", states[k]));
        }
        finally
        {
            _factory.Crm.DefaultDelay = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task The_same_window_on_the_five_other_reads()
    {
        var (w, id, keys) = await V200Async();
        // 30 return links (cloned in the store: one link per customer of 30)
        var depo = w.S.Mint(w.S.Depo, RecallR3.DepoKeys);
        Assert.Equal(201, (await w.S.CommandAsync(id, "return-links", depo, w.Link(await w.ReturnAsync(), CrmWorld.A.ToString("D")), null)).Status);
        var links = _factory.Database.GetCollection<BsonDocument>("recall_return_links");
        var template = w.S.Persisted("recall_return_links").Single();
        foreach (var key in keys.Where(k => k != CrmWorld.A.ToString("D")).Take(29))
        {
            var clone = template.DeepClone().AsBsonDocument;
            clone["_id"] = template["_id"].IsString ? Guid.NewGuid().ToString("D") : new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            clone["TransactionId"] = "TXN-" + Guid.NewGuid().ToString("N");
            clone["CustomerRef"] = key;
            clone["IdempotencyKey"] = "clone-" + Guid.NewGuid().ToString("N");
            links.InsertOne(clone);
        }

        _factory.Crm.DefaultDelay = TimeSpan.FromMilliseconds(50);
        try
        {
            // runTrace, getCaseScope, getCaseEffectiveness, getCaseReturns: customerPage = 2 → the 26th–50th CRM keys of THAT answer
            foreach (var (what, path, rowsOf) in new (string, string, Func<JsonElement, IEnumerable<JsonElement>>)[]
                     {
                         ("trace", $"/api/recall/trace?rootLotId={w.Fg}&direction=Forward&customerPage=2",
                             t => RecallTraceWorld.Nodes(t).Where(n => CrmWorld.Str(n, "kind") == "Shipment")),
                         ("scope", $"/api/recall/cases/{id}/scope?customerPage=2", s => s.GetProperty("items").EnumerateArray().Where(i => CrmWorld.Str(i, "kind") == "Shipment")),
                         ("effectiveness", $"/api/recall/cases/{id}/effectiveness?customerPage=2", e => e.GetProperty("rows").EnumerateArray()),
                         ("returns", $"/api/recall/cases/{id}/returns?customerPage=2", r => r.GetProperty("links").EnumerateArray())
                     })
            {
                _factory.Crm.ResetCounts();
                var rows = rowsOf(await w.GetAsync(path)).ToList();
                var answerKeys = rows.Select(r => CrmWorld.Str(r, "customerRef")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                var window = answerKeys.Skip(25).Take(25).ToHashSet(StringComparer.Ordinal);
                Assert.True(_factory.Crm.Calls <= 25, $"{what}: {_factory.Crm.Calls} calls");
                Assert.All(rows, r => Assert.Equal(window.Contains(CrmWorld.Str(r, "customerRef")) ? "Resolved" : "Deferred", CrmWorld.Str(r.GetProperty("customer"), "state")));
            }

            // getCaseScopeDiff: 30 new findings of 30 accounts outside V200 → the window of the diff's own keys
            var derived = await w.S.LotAsync();
            await w.W.LinkAsync(w.Fg, derived, w.W.Order);
            await w.W.PostAsync(w.W.Lots.Receipt("50", derived, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", w.W.Order);
            for (var i = 0; i < 30; i++)
            {
                var sale = w.W.Lots.Issue("1", derived);
                sale["sourceModule"] = "MOD-0183";
                var doc = Guid.NewGuid().ToString("D");
                sale["sourceDocumentId"] = doc;
                await w.W.Lots.PostAsync(sale, 201);
                var account = Guid.NewGuid();
                w.Account(account, $"Yeni {i}", $"ACC-N-{i}");
                _factory.Shipments.CustomerRefJson[doc] = "\"" + account.ToString("D") + "\"";
            }

            _factory.Crm.ResetCounts();
            var diff = await w.GetAsync($"/api/recall/cases/{id}/scope/diff?customerPage=2");
            var added = diff.GetProperty("added").EnumerateArray().Where(n => CrmWorld.Str(n, "kind") == "Shipment").ToList();
            var diffKeys = added.Select(n => CrmWorld.Str(n, "customerRef")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            Assert.True(diffKeys.Count > 50, diffKeys.Count.ToString()); // the 30 findings and the seeded nodes outside the approved snapshot
            var diffWindow = diffKeys.Skip(25).Take(25).ToHashSet(StringComparer.Ordinal);
            Assert.All(added, n => Assert.Equal(diffWindow.Contains(CrmWorld.Str(n, "customerRef")) ? "Resolved" : "Deferred", CrmWorld.Str(n.GetProperty("customer"), "state")));
            Assert.True(_factory.Crm.Calls <= 25, _factory.Crm.Calls.ToString());
        }
        finally
        {
            _factory.Crm.DefaultDelay = TimeSpan.Zero;
        }
    }
}

/// <summary>
/// MOD-0177-FU01 AC-29 (CT F-1@fbafd3af, F-1@807c8dc2, F-2@807c8dc2; pack §8.6 Export, §8.10) — the exports read EVERY CRM customer (no total
/// budget) in ordinal waves of 8; a wave whose every read times out / fails leaves the rest unread (breaker); a forbidden trace node refuses
/// the trace CSV before its audit row.
/// Timing measurements run alone (the recall performance collection).
/// </summary>
[Collection(RecallPerformanceCollection.Name)]
public sealed class RecallCustomerExportBudgetTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCustomerExportBudgetTests(RecallApiFactory factory) => _factory = factory;

    private async Task<(CrmWorld W, string CaseId, List<Guid> Ordered)> WorldAsync(int count)
    {
        var w = await CrmWorld.CreateAsync(_factory); // the first sale stays a ship-to customer: every CRM customer is seeded (V200 / V400)
        var (id, _) = await w.ActiveAsync();
        var accounts = w.SeedCustomers(id, count).OrderBy(g => g.ToString("D"), StringComparer.Ordinal).ToList();
        return (w, id, accounts);
    }

    private static List<string> Cells(string csvSection) => csvSection.Split("\r\n").ToList();

    private static async Task<string> CsvAsync(CrmWorld w, string id) => await RecallCrmEvidence.ExportTextAsync(w, id, "Csv", "tr");

    private static int Count(string text, string word) => (text.Length - text.Replace(word, string.Empty, StringComparison.Ordinal).Length) / word.Length;

    private void Reset()
    {
        _factory.Crm.Behaviour.Clear();
        _factory.Crm.Delay.Clear();
        _factory.Crm.Everyone = null;
        _factory.Crm.DefaultDelay = TimeSpan.Zero;
        _factory.Crm.ResetCounts();
    }

    [Fact]
    public async Task V200_healthy_and_slow_every_customer_is_resolved_in_every_export()
    {
        var (w, id, accounts) = await WorldAsync(200);
        try
        {
            _factory.Crm.DefaultDelay = TimeSpan.FromMilliseconds(200);
            var csv = await CsvAsync(w, id);
            Assert.Equal(200, _factory.Crm.Calls);
            Assert.True(_factory.Crm.MaxInFlight <= 8, _factory.Crm.MaxInFlight.ToString());
            Assert.DoesNotContain("Çözülemedi", csv, StringComparison.Ordinal);
            Assert.Equal(200, System.Text.RegularExpressions.Regex.Matches(csv, @"Musteri \d{4} · ACC-V-\d{4}").Select(m => m.Value).Distinct().Count());
            var preview = await w.GetAsync($"/api/recall/evidence/preview?caseId={id}");
            var cells = RecallEvidence.Rows(RecallEvidence.Section(preview, "4_customers")).Select(r => r[0]).ToList();
            Assert.Equal(200, cells.Count(c => c.Contains(" · ACC-V-", StringComparison.Ordinal))); // + the first sale's ship-to customer (marked)
            var traceCsv = await RecallCrmExport.TraceCsvAsync(w);
            Assert.Equal(200, Count(traceCsv, " · ACC-V-"));
        }
        finally
        {
            Reset();
        }
    }

    [Fact]
    public async Task Silent_CRM_one_wave_then_the_breaker_and_the_kept_answers()
    {
        // (b) 17 accounts, CRM silent → 17 marked cells, 8 calls, ≤ 3 s
        var (w, id, accounts) = await WorldAsync(17);
        try
        {
            _factory.Crm.Everyone = CrmBehaviour.Hang;
            var clock = Stopwatch.StartNew();
            var csv = await CsvAsync(w, id);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), clock.Elapsed.ToString());
            Assert.Equal(17, Count(csv, "Çözülemedi ("));
            Assert.Equal(8, _factory.Crm.Calls);
            Assert.Equal(accounts.Take(8).ToHashSet(), _factory.Crm.Seen.Select(s => s.Account).ToHashSet());

            // (c) 9 accounts: 8 answer in 0.1 s, one never → 8 named, 1 marked (the wave had an answer: no breaker)
            Reset();
            foreach (var a in accounts.Take(9)) _factory.Crm.Delay[a] = TimeSpan.FromMilliseconds(100);
            _factory.Crm.Behaviour[accounts[3]] = CrmBehaviour.Hang;
            foreach (var a in accounts.Skip(9)) _factory.Crm.Behaviour[a] = CrmBehaviour.ServiceUnavailable;
            csv = await CsvAsync(w, id);
            Assert.Equal(8, Count(csv, " · ACC-"));
            Assert.Equal(9, Count(csv, "Çözülemedi ("));

            // (h) 16 accounts: the first wave 404, the second Resolved → 404 is not a breaker answer
            Reset();
            foreach (var a in accounts.Take(8)) _factory.Crm.Accounts.TryRemove(a, out _);
            foreach (var a in accounts.Skip(16)) _factory.Crm.Behaviour[a] = CrmBehaviour.ServiceUnavailable;
            csv = await CsvAsync(w, id);
            Assert.Equal(8, Count(csv, "Bulunamadı ("));
            Assert.Equal(8, Count(csv, " · ACC-"));
            Assert.Equal(17, _factory.Crm.Calls);
        }
        finally
        {
            Reset();
        }
    }

    [Fact]
    public async Task V400_the_breaker_keeps_every_export_far_below_the_gateway_limit()
    {
        var (w, id, accounts) = await WorldAsync(400);
        try
        {
            // (d) CRM silent: evidence CSV, preview and trace CSV — each 8 calls, 392 Breaker lines, ≤ 4 s
            foreach (var export in new[] { "csv", "preview", "trace" })
            {
                Reset();
                _factory.Crm.Everyone = CrmBehaviour.Hang;
                var logBefore = _factory.Logs.Lines.Count(l => l.Contains(": Breaker", StringComparison.Ordinal));
                var clock = Stopwatch.StartNew();
                var text = export switch
                {
                    "csv" => await CsvAsync(w, id),
                    "preview" => string.Join('\n', RecallEvidence.Rows(RecallEvidence.Section(await w.GetAsync($"/api/recall/evidence/preview?caseId={id}"), "4_customers"))
                        .Select(r => r[0])),
                    _ => await RecallCrmExport.TraceCsvAsync(w)
                };
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"{export}: {clock.Elapsed}");
                Assert.Equal(8, _factory.Crm.Calls);
                Assert.Equal(accounts.Take(8).ToHashSet(), _factory.Crm.Seen.Select(s => s.Account).ToHashSet());
                Assert.Equal(392, _factory.Logs.Lines.Count(l => l.Contains(": Breaker", StringComparison.Ordinal)) - logBefore);
                var word = export == "preview" ? "Could not be resolved (" : "Çözülemedi ("; // the preview read sends no Accept-Language (en)
                Assert.True(Count(text, word) == 400, export + ": " + text[..Math.Min(text.Length, 1500)]);
            }

            // (f) the first two waves answer, then CRM goes silent: 16 named, 384 marked, 24 calls, ≤ 4 s
            Reset();
            foreach (var a in accounts.Take(16)) _factory.Crm.Delay[a] = TimeSpan.FromMilliseconds(100);
            foreach (var a in accounts.Skip(16)) _factory.Crm.Behaviour[a] = CrmBehaviour.Hang;
            var watch = Stopwatch.StartNew();
            var mid = await CsvAsync(w, id);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(4), watch.Elapsed.ToString());
            Assert.Equal((16, 384, 24), (Count(mid, " · ACC-"), Count(mid, "Çözülemedi ("), _factory.Crm.Calls));

            // (e) healthy, 200 ms: every customer named, 400 calls, no Breaker line, ≤ 15 s
            Reset();
            _factory.Crm.DefaultDelay = TimeSpan.FromMilliseconds(200);
            var before = _factory.Logs.Lines.Count(l => l.Contains(": Breaker", StringComparison.Ordinal));
            watch.Restart();
            var healthy = await CsvAsync(w, id);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), watch.Elapsed.ToString());
            Assert.Equal((400, 400), (Count(healthy, " · ACC-"), _factory.Crm.Calls));
            Assert.Equal(before, _factory.Logs.Lines.Count(l => l.Contains(": Breaker", StringComparison.Ordinal)));
        }
        finally
        {
            Reset();
        }
    }

    [Fact]
    public async Task A_partial_outage_in_every_wave_opens_no_breaker()
    {
        // (g) 40 accounts: the last (ordinal) of each wave never answers, the others in 0.1 s → 35 named, 5 marked, 40 calls, ≤ 12 s
        var (w, id, accounts) = await WorldAsync(40);
        try
        {
            foreach (var a in accounts) _factory.Crm.Delay[a] = TimeSpan.FromMilliseconds(100);
            for (var wave = 0; wave < 5; wave++) _factory.Crm.Behaviour[accounts[wave * 8 + 7]] = CrmBehaviour.Hang;
            var watch = Stopwatch.StartNew();
            var csv = await CsvAsync(w, id);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(12), watch.Elapsed.ToString());
            Assert.Equal((35, 5, 40), (Count(csv, " · ACC-"), Count(csv, "Çözülemedi ("), _factory.Crm.Calls));
        }
        finally
        {
            Reset();
        }
    }

    [Fact]
    public async Task A_forbidden_trace_node_refuses_the_trace_CSV_before_its_audit_row()
    {
        // (i)
        var w = await CrmWorld.CreateAsync(_factory);
        var c = Guid.NewGuid();
        w.Account(c, "C Eczanesi", "ACC-C");
        await w.SellAsync(CrmWorld.A);
        await w.SellAsync(c);
        _factory.Crm.Behaviour[c] = CrmBehaviour.Forbidden;
        try
        {
            var before = w.S.Audit("recall.trace.run").Count;
            var (status, body, _) = await w.S.SendAsync(HttpMethod.Get, $"/api/recall/trace/export?rootLotId={w.Fg}&direction=Forward", w.Qa);
            Assert.Equal((403, "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"), (status, RecallScenario.Code(body)));
            foreach (var key in new[] { CrmWorld.A, c }) Assert.DoesNotContain(key.ToString("D"), body.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(before, w.S.Audit("recall.trace.run").Count);
        }
        finally
        {
            Reset();
        }

        var plain = await CrmWorld.CreateAsync(_factory);
        _factory.Crm.Everyone = CrmBehaviour.Forbidden;
        try
        {
            Assert.DoesNotContain("CRM", await RecallCrmExport.TraceCsvAsync(plain), StringComparison.Ordinal); // no CRM node: 200 and the plain cells
        }
        finally
        {
            Reset();
        }
    }
}

/// <summary>The trace CSV of a CRM world (Accept-Language tr).</summary>
public static class RecallCrmExport
{
    public static async Task<string> TraceCsvAsync(CrmWorld w, string language = "tr")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/recall/trace/export?rootLotId={w.Fg}&direction=Forward");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", w.Qa);
        request.Headers.AcceptLanguage.ParseAdd(language);
        using var response = await w.S.Client.SendAsync(request);
        var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.True(response.IsSuccessStatusCode, text);
        return text;
    }
}
