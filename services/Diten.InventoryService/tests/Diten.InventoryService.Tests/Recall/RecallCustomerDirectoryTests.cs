using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Infrastructure.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// MOD-0177-FU01 AC-7, AC-8, AC-9, AC-12, AC-13 (pack §8.6) — the CRM reader (production <see cref="CrmAccountLookupHttpReader"/> over the
/// CRM double, built from the contract text): the classification, the page budget (2 s per read, 3 s per page, 8 in flight, partial result),
/// no cache and the per-request dedup; on the host, the names are read side by side with the MVP-6 read.
/// Timing measurements run alone (the recall performance collection).
/// </summary>
[Collection(RecallPerformanceCollection.Name)]
public sealed class RecallCustomerDirectoryTests : IClassFixture<RecallApiFactory>
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private readonly RecallApiFactory _factory;

    public RecallCustomerDirectoryTests(RecallApiFactory factory) => _factory = factory;

    private static (CrmAccountLookupHttpReader Reader, CrmDirectoryDouble Crm) Reader()
    {
        var crm = new CrmDirectoryDouble();
        var http = new HttpClient(crm) { BaseAddress = new Uri("http://gateway.test/"), Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Mint(Tenant, Guid.NewGuid(), Guid.NewGuid(), []));
        return (new CrmAccountLookupHttpReader(http, NullLogger<CrmAccountLookupHttpReader>.Instance), crm);
    }

    private static async Task<RecallCustomerName> OneAsync(CrmAccountLookupHttpReader reader, Guid id)
        => (await reader.ResolveAsync([id], RecallLookupMode.Page))[id];

    [Fact]
    public async Task Resolved_ignores_billability()
    {
        var (reader, crm) = Reader();
        var id = Guid.NewGuid();
        crm.Accounts[id] = new CrmAccount(Tenant, CrmWorld.NameA, CrmWorld.CodeA, "inactive", false);
        Assert.Equal(new RecallCustomerName(RecallCustomerOutcome.Resolved, CrmWorld.CodeA, CrmWorld.NameA), await OneAsync(reader, id));
    }

    [Theory]
    [InlineData("400")]
    [InlineData("500")]
    [InlineData("503")]
    [InlineData("timeout")]
    [InlineData("unreachable")]
    [InlineData("other-account")]
    [InlineData("empty-name")]
    [InlineData("no-code")]
    [InlineData("version-2")]
    [InlineData("not-json")]
    public async Task Unresolved_every_failure_and_every_invalid_body(string fixture)
    {
        var (reader, crm) = Reader();
        var id = Guid.NewGuid();
        crm.Accounts[id] = new CrmAccount(Tenant, CrmWorld.NameA, CrmWorld.CodeA);
        switch (fixture)
        {
            case "400": crm.Behaviour[id] = CrmBehaviour.BadRequest; break;
            case "500": crm.Behaviour[id] = CrmBehaviour.ServerError; break;
            case "503": crm.Behaviour[id] = CrmBehaviour.ServiceUnavailable; break;
            case "timeout": crm.Behaviour[id] = CrmBehaviour.Hang; break;
            case "unreachable": crm.Behaviour[id] = CrmBehaviour.Unreachable; break;
            default:
                crm.Behaviour[id] = CrmBehaviour.Raw;
                crm.RawBody[id] = fixture switch
                {
                    "other-account" => CrmDirectoryDouble.Body(Guid.NewGuid(), CrmWorld.CodeA, CrmWorld.NameA),
                    "empty-name" => CrmDirectoryDouble.Body(id, CrmWorld.CodeA, ""),
                    "no-code" => """{"data":{"accountId":"ID","name":"Example Hastanesi","status":"active","isBillable":true,"contractVersion":"crm-account-lookup/1.0"}}""".Replace("ID", id.ToString("D")),
                    "version-2" => CrmDirectoryDouble.Body(id, CrmWorld.CodeA, CrmWorld.NameA, version: "crm-account-lookup/2.0"),
                    _ => "<html>not json</html>"
                };
                break;
        }

        var answer = await OneAsync(reader, id);
        Assert.Equal(RecallCustomerOutcome.Unresolved, answer.Outcome);
        Assert.Equal(((string?)null, (string?)null), (answer.Name, answer.Code));
    }

    [Fact]
    public async Task NotFound_is_404_only()
    {
        var (reader, crm) = Reader();
        var other = Guid.NewGuid();
        crm.Accounts[other] = new CrmAccount(Guid.NewGuid(), "Baska Kiraci", "ACC-T2");
        Assert.Equal(RecallCustomerOutcome.NotFound, (await OneAsync(reader, Guid.NewGuid())).Outcome); // unknown
        Assert.Equal(RecallCustomerOutcome.NotFound, (await OneAsync(reader, other)).Outcome);          // another tenant's
    }

    [Fact]
    public async Task Budget_per_read_in_flight_and_partial_result()
    {
        // (b) nine accounts of 1.5 s: never more than 8 in flight
        var (reader, crm) = Reader();
        var nine = Enumerable.Range(0, 9).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in nine)
        {
            crm.Accounts[id] = new CrmAccount(Tenant, "N", "C");
            crm.Delay[id] = TimeSpan.FromSeconds(1.5);
        }

        await reader.ResolveAsync(nine, RecallLookupMode.Page);
        Assert.Equal(8, crm.MaxInFlight);

        // (d) A answers in 0.1 s, C and D never: A Resolved, C and D Unresolved, within 2.5 s (2 s per read)
        (reader, crm) = Reader();
        var (a, c, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        foreach (var id in new[] { a, c, d }) crm.Accounts[id] = new CrmAccount(Tenant, "N", "C");
        crm.Delay[a] = TimeSpan.FromSeconds(0.1);
        crm.Behaviour[c] = CrmBehaviour.Hang;
        crm.Behaviour[d] = CrmBehaviour.Hang;
        var clock = Stopwatch.StartNew();
        var answers = await reader.ResolveAsync([a, c, d], RecallLookupMode.Page);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2.5), clock.Elapsed.ToString());
        Assert.Equal((RecallCustomerOutcome.Resolved, RecallCustomerOutcome.Unresolved, RecallCustomerOutcome.Unresolved), (answers[a].Outcome, answers[c].Outcome, answers[d].Outcome));

        // (c) seventeen accounts, CRM answers nothing: every one Unresolved within 3.5 s (the 3 s page budget)
        (reader, crm) = Reader();
        crm.Everyone = CrmBehaviour.Hang;
        var seventeen = Enumerable.Range(0, 17).Select(_ => Guid.NewGuid()).ToList();
        clock.Restart();
        answers = await reader.ResolveAsync(seventeen, RecallLookupMode.Page);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3.5), clock.Elapsed.ToString());
        Assert.All(seventeen, id => Assert.Equal(RecallCustomerOutcome.Unresolved, answers[id].Outcome));

        // a page takes at most 25 accounts; more is a programming error and CRM is not called
        (reader, crm) = Reader();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ResolveAsync(Enumerable.Range(0, 26).Select(_ => Guid.NewGuid()).ToList(), RecallLookupMode.Page));
        Assert.Equal(0, crm.Calls);
    }

    [Fact]
    public async Task Budget_on_the_host_screen_reads_and_the_scope_command_body()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        w.FirstSaleIs(CrmWorld.A);
        var (caseId, _) = await w.ActiveAsync();
        var crm = _factory.Crm;
        try
        {
            // (a) CRM waits 5 s for A → Unresolved, the read within 2.5 s
            crm.Delay[CrmWorld.A] = TimeSpan.FromSeconds(5);
            var clock = Stopwatch.StartNew();
            var rows = await w.CustomersAsync(caseId);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2.5), clock.Elapsed.ToString());
            Assert.Equal("Unresolved", CrmWorld.Str(CrmWorld.Customer(CrmWorld.Row(rows, CrmWorld.A.ToString("D"))), "state"));
            crm.Delay.TryRemove(CrmWorld.A, out _);

            // (c) seventeen CRM customers, CRM answers nothing, MVP-6 at once → all Unresolved within 3.5 s
            w.SeedCustomers(caseId, 16);
            crm.Everyone = CrmBehaviour.Hang;
            clock.Restart();
            rows = await w.CustomersAsync(caseId);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3.5), clock.Elapsed.ToString());
            Assert.Equal(17, rows.GetArrayLength());
            Assert.All(rows.EnumerateArray(), r => Assert.Equal("Unresolved", CrmWorld.Str(CrmWorld.Customer(r), "state")));

            // (e) MVP-6 1.5 s and CRM silent: the two reads run side by side (≤ 2.5 s, not 1.5 + 2)
            _factory.Shipments.Latency = TimeSpan.FromSeconds(1.5);
            crm.Everyone = null;
            crm.Behaviour[CrmWorld.A] = CrmBehaviour.Hang;
            var single = await CrmWorld.CreateAsync(_factory);
            single.FirstSaleIs(CrmWorld.A);
            var (one, _) = await single.ActiveAsync();
            clock.Restart();
            await single.CustomersAsync(one);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2.5), clock.Elapsed.ToString());
        }
        finally
        {
            _factory.Shipments.Latency = TimeSpan.Zero;
            crm.Everyone = null;
            crm.Behaviour.Clear();
            crm.Delay.Clear();
        }
    }

    [Fact]
    public async Task Budget_the_scope_command_body_answers_200_within_3_5_s_with_CRM_silent()
    {
        // son bakış 04 F-3: markScopeItems' body resolves page 1 in Page mode and never fails the command
        var w = await CrmWorld.CreateAsync(_factory);
        w.FirstSaleIs(CrmWorld.A);
        var draft = await w.DraftAsync();
        var id = RecallScenario.Id(draft);
        _factory.Crm.Everyone = CrmBehaviour.Hang;
        try
        {
            var scope = await w.GetAsync($"/api/recall/cases/{id}/scope");
            var shipment = Assert.Single(scope.GetProperty("items").EnumerateArray(), i => CrmWorld.Str(i, "kind") == "Shipment");
            var clock = Stopwatch.StartNew();
            var (status, body, _) = await w.S.MarkAsync(id, RecallScenario.ETag(scope), CrmWorld.Str(shipment, "nodeKey"), false, "Musteri kapsam disi birakildi, deneme");
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3.5), clock.Elapsed.ToString());
            Assert.True(status == 200, body.ToString());
            var marked = Assert.Single(body.GetProperty("items").EnumerateArray(), i => CrmWorld.Str(i, "kind") == "Shipment");
            Assert.False(marked.GetProperty("included").GetBoolean());
            Assert.Equal("Unresolved", CrmWorld.Str(marked.GetProperty("customer"), "state"));
        }
        finally
        {
            _factory.Crm.Everyone = null;
        }
    }

    [Fact]
    public async Task No_cache_dedup_per_request()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        w.FirstSaleIs(CrmWorld.A);
        await w.SellAsync(CrmWorld.A);
        await w.SellAsync(CrmWorld.A);
        var (caseId, _) = await w.ActiveAsync();
        _factory.Crm.ResetCounts();
        await w.CustomersAsync(caseId);
        Assert.Equal(1, _factory.Crm.CallsFor(CrmWorld.A));
        await w.CustomersAsync(caseId);
        Assert.Equal(2, _factory.Crm.CallsFor(CrmWorld.A));
    }
}

/// <summary>MOD-0177-FU01 AC-11 — CRM answers the caller's tenant only (the token); the request carries the caller's token, no tenant header.</summary>
public sealed class RecallCrmTenantTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmTenantTests(RecallApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Another_tenants_account_is_NotFound_and_the_call_carries_the_callers_token_only()
    {
        var t1 = await CrmWorld.CreateAsync(_factory);   // A belongs to T1
        var t2 = await CrmWorld.CreateAsync(_factory);   // a case of T2 whose MVP-6 shipment names A (a data error)
        _factory.Crm.Accounts[CrmWorld.A] = new CrmAccount(t1.S.Tenant, CrmWorld.NameA, CrmWorld.CodeA);
        t2.FirstSaleIs(CrmWorld.A);
        var (caseId, _) = await t2.ActiveAsync();
        _factory.Crm.ResetCounts();
        var token = t2.Qa;
        var rows = await t2.CustomersAsync(caseId, token: token);
        var row = CrmWorld.Row(rows, CrmWorld.A.ToString("D"));
        Assert.Equal("NotFound", CrmWorld.Str(CrmWorld.Customer(row), "state"));
        Assert.DoesNotContain(CrmWorld.NameA, rows.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(CrmWorld.CodeA, rows.GetRawText(), StringComparison.Ordinal);

        // (c) the CRM call: the caller's Authorization and correlation id, no tenant / company header
        var seen = Assert.Single(_factory.Crm.Seen);
        Assert.Equal("Bearer " + token, seen.Authorization);
        Assert.False(string.IsNullOrEmpty(seen.Correlation));
        Assert.Equal(((string?)null, (string?)null), (seen.Tenant, seen.LegalEntity));

        // (b) T1 reading T2's case → 404
        Assert.Equal(404, (await t1.S.SendAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/customers", t1.Qa)).Status);
    }
}

/// <summary>
/// MOD-0177-FU01 AC-18 (CT F-2@fbafd3af, F-2@807c8dc2) — CRM 401 / 403 is "İzin eksik" (Forbidden): the screen reads still answer 200; a
/// record on such a customer is 403 RECALL_CUSTOMER_DIRECTORY_FORBIDDEN and writes nothing; a legacy row is not touched; the exports refuse.
/// </summary>
public sealed class RecallCrmAuthorizationTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmAuthorizationTests(RecallApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Forbidden_reads_200_records_403_exports_403()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var (c, d) = (Guid.NewGuid(), Guid.NewGuid());
        w.Account(c, "C Eczanesi", "ACC-C");
        w.Account(d, "D Eczanesi", "ACC-D");
        var shipC = await w.SellAsync(c, shipTo: "CUST-200/ADDR-1", quantity: "3");
        await w.SellAsync(d, shipTo: "CUST-201/ADDR-1");
        var (caseId, _) = await w.ActiveAsync();
        _factory.Crm.Behaviour[c] = CrmBehaviour.Forbidden;
        _factory.Crm.Behaviour[d] = CrmBehaviour.Unauthorized;
        try
        {
            foreach (var read in new[] { "customers", "scope", "effectiveness", "returns" })
            {
                Assert.Equal(200, (await w.S.SendAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/{read}", w.Qa)).Status);
            }

            var rows = await w.CustomersAsync(caseId);
            foreach (var (account, shipTo) in new[] { (c, "CUST-200/ADDR-1"), (d, "CUST-201/ADDR-1") })
            {
                var customer = CrmWorld.Customer(CrmWorld.Row(rows, account.ToString("D")));
                Assert.Equal(("Crm", "Forbidden", shipTo), (CrmWorld.Str(customer, "source"), CrmWorld.Str(customer, "state"), CrmWorld.Str(customer, "shipTo")));
                Assert.False(string.IsNullOrEmpty(CrmWorld.Str(customer, "shipmentNumber")));
                Assert.False(customer.TryGetProperty("name", out _) || customer.TryGetProperty("code", out _));
            }

            var ticari = w.S.Mint(w.S.Ticari, RecallScenario.TicariKeys);
            var depo = w.S.Mint(w.S.Depo, RecallR3.DepoKeys);
            var ret = await w.ReturnAsync();
            var before = (w.Count("recall_notifications"), w.Count("recall_consumption_declarations"), w.Count("recall_return_links"), w.Count("recall_case_events"),
                w.Count("inventory_audit"));
            foreach (var (verb, token, body) in new[]
                     {
                         ("notifications", ticari, w.Notify(c.ToString("D"))), ("consumption-declarations", ticari, w.Consume(c.ToString("D"))),
                         ("return-links", depo, w.Link(ret, c.ToString("D"))), ("notifications", ticari, w.Notify(d.ToString("D")))
                     })
            {
                var (status, refused, _) = await w.S.CommandAsync(caseId, verb, token, body, null);
                Assert.Equal((403, "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"), (status, RecallScenario.Code(refused)));
                Assert.DoesNotContain(c.ToString("D"), refused.GetRawText(), StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(before, (w.Count("recall_notifications"), w.Count("recall_consumption_declarations"), w.Count("recall_return_links"), w.Count("recall_case_events"),
                w.Count("inventory_audit")));

            // the legacy / ShipTo first sale → 201 and no CRM call
            _factory.Crm.ResetCounts();
            Assert.Equal(201, (await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(ShipmentBundleDouble.ShipTo(w.FirstShipment)), null)).Status);
            Assert.Equal(0, _factory.Crm.Calls);

            // the three exports refuse with the same code
            Assert.Equal((403, "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"), Code(await w.S.SendAsync(HttpMethod.Get, $"/api/recall/evidence/preview?caseId={caseId}", w.Qa)));
            var exportKey = "ex-" + Guid.NewGuid().ToString("N");
            Assert.Equal((403, "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"), Code(await w.S.SendAsync(HttpMethod.Post, "/api/recall/evidence/exports", w.Qa,
                new Dictionary<string, object?> { ["idempotencyKey"] = exportKey, ["caseId"] = caseId, ["format"] = "Csv" }, exportKey)));
            Assert.Equal((403, "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"), Code(await w.S.SendAsync(HttpMethod.Get, $"/api/recall/trace/export?rootLotId={w.Fg}&direction=Forward", w.Qa)));

            // no recall read key → the recall 403 (not the directory code)
            var (status403, body403, _) = await w.S.SendAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/customers", w.S.Mint(w.S.Qa1, ["recall.dashboard.read"]));
            Assert.Equal(403, status403);
            Assert.DoesNotContain("RECALL_CUSTOMER_DIRECTORY_FORBIDDEN", body403.ValueKind == JsonValueKind.Undefined ? "" : body403.GetRawText(), StringComparison.Ordinal);
            _ = shipC;
        }
        finally
        {
            _factory.Crm.Behaviour.Clear();
        }
    }

    private static (int, string) Code((int Status, JsonElement Body, HttpResponseMessage Raw) answer) => (answer.Status, RecallScenario.Code(answer.Body));
}

/// <summary>
/// MOD-0177-FU01 AC-25 (owner decision F-1; F-6@fbafd3af; §8.4) — no record on a CRM customer whose name cannot be read now (503) and the
/// resolution check is the LAST check before the write: an invalid request keeps its 400 / 422 and CRM is not called; a replay answers 200.
/// </summary>
public sealed class RecallCrmUnresolvedWriteTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmUnresolvedWriteTests(RecallApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Unresolved_503_NotFound_writes_and_the_check_is_last()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var (c, d, n) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        w.Account(c, "C Eczanesi", "ACC-C");
        w.Account(d, "D Eczanesi", "ACC-D");
        var shipC = await w.SellAsync(c, shipTo: "CUST-200/ADDR-1", quantity: "3");
        await w.SellAsync(d, shipTo: "CUST-201/ADDR-1", quantity: "3");
        await w.SellAsync(n, quantity: "3");             // N: no such account in CRM → 404
        await w.SellAsync(CrmWorld.A, quantity: "3");
        var (caseId, _) = await w.ActiveAsync();
        var ticari = w.S.Mint(w.S.Ticari, RecallScenario.TicariKeys);
        var depo = w.S.Mint(w.S.Depo, RecallR3.DepoKeys);

        // (e) first, with CRM open: a notification to C (to be replayed while CRM is down)
        var replayKey = RecallScenario.Key("nt");
        var (s0, first, _) = await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(c.ToString("D"), replayKey), null);
        Assert.True(s0 == 201, first.ToString());

        _factory.Crm.Behaviour[c] = CrmBehaviour.ServiceUnavailable;
        _factory.Crm.Behaviour[d] = CrmBehaviour.ServiceUnavailable;
        try
        {
            // (a) the rows: Unresolved with distinct distinguishers, never a GUID; N NotFound with its distinguisher
            var rows = await w.CustomersAsync(caseId);
            var rc = CrmWorld.Customer(CrmWorld.Row(rows, c.ToString("D")));
            var rd = CrmWorld.Customer(CrmWorld.Row(rows, d.ToString("D")));
            Assert.Equal(("Unresolved", "CUST-200/ADDR-1"), (CrmWorld.Str(rc, "state"), CrmWorld.Str(rc, "shipTo")));
            Assert.Equal(("Unresolved", "CUST-201/ADDR-1"), (CrmWorld.Str(rd, "state"), CrmWorld.Str(rd, "shipTo")));
            Assert.NotEqual(CrmWorld.Str(rc, "shipmentNumber"), CrmWorld.Str(rd, "shipmentNumber"));
            var rn = CrmWorld.Customer(CrmWorld.Row(rows, n.ToString("D")));
            Assert.Equal("NotFound", CrmWorld.Str(rn, "state"));
            Assert.False(string.IsNullOrEmpty(CrmWorld.Str(rn, "shipmentNumber")));
            foreach (var display in new[] { rc, rd, rn })
            {
                Assert.False(Diten.InventoryService.Domain.Recall.Rules.RecallCustomerKeyRules.IsDShaped(CrmWorld.Str(display, "shipmentNumber")));
                Assert.DoesNotContain(c.ToString("D"), display.GetRawText(), StringComparison.OrdinalIgnoreCase);
            }

            // (b) the three commands on C → 503 within 2.5 s, nothing written, no key in the body
            var ret = await w.ReturnAsync();
            var before = (w.Count("recall_notifications"), w.Count("recall_consumption_declarations"), w.Count("recall_return_links"), w.Count("recall_case_events"),
                w.Count("inventory_audit"));
            foreach (var (verb, token, body) in new[]
                     {
                         ("notifications", ticari, w.Notify(c.ToString("D"))), ("consumption-declarations", ticari, w.Consume(c.ToString("D"))),
                         ("return-links", depo, w.Link(ret, c.ToString("D")))
                     })
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var (status, refused, _) = await w.S.CommandAsync(caseId, verb, token, body, null);
                Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2.5), clock.Elapsed.ToString());
                Assert.Equal((503, "RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE"), (status, RecallScenario.Code(refused)));
                Assert.DoesNotContain(c.ToString("D"), refused.GetRawText(), StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(before, (w.Count("recall_notifications"), w.Count("recall_consumption_declarations"), w.Count("recall_return_links"), w.Count("recall_case_events"),
                w.Count("inventory_audit")));

            // (c) A (Resolved) and N (NotFound — Q-7 open) → 201
            foreach (var account in new[] { CrmWorld.A, n })
            {
                Assert.Equal(201, (await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(account.ToString("D")), null)).Status);
                Assert.Equal(201, (await w.S.CommandAsync(caseId, "consumption-declarations", ticari, w.Consume(account.ToString("D")), null)).Status);
                Assert.Equal(201, (await w.S.CommandAsync(caseId, "return-links", depo, w.Link(await w.ReturnAsync(), account.ToString("D")), null)).Status);
            }

            // (d) the legacy / ShipTo first sale → 201, no CRM call
            _factory.Crm.ResetCounts();
            var legacy = ShipmentBundleDouble.ShipTo(w.FirstShipment);
            Assert.Equal(201, (await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(legacy), null)).Status);
            Assert.Equal(201, (await w.S.CommandAsync(caseId, "consumption-declarations", ticari, w.Consume(legacy), null)).Status);
            Assert.Equal(201, (await w.S.CommandAsync(caseId, "return-links", depo, w.Link(await w.ReturnAsync(), legacy), null)).Status);
            Assert.Equal(0, _factory.Crm.Calls);

            // (e) the replay of the request written while CRM was open → 200, the same record
            var (se, replayed, _) = await w.S.CommandAsync(caseId, "notifications", ticari, w.Notify(c.ToString("D"), replayKey), null);
            Assert.True(se == 200, replayed.ToString());
            Assert.Equal(CrmWorld.Str(first, "notificationId"), CrmWorld.Str(replayed, "notificationId"));

            // (f) / (g) invalid requests keep their 400 / 422 and CRM is not called — with CRM 503 and with CRM 403
            foreach (var down in new[] { CrmBehaviour.ServiceUnavailable, CrmBehaviour.Forbidden })
            {
                _factory.Crm.Behaviour[c] = down;
                _factory.Crm.ResetCounts();
                var bad = w.Notify(c.ToString("D"));
                bad["supersedesId"] = Guid.NewGuid().ToString("D");
                Assert.Equal((400, "INVALID_REQUEST"), Code(await w.S.CommandAsync(caseId, "notifications", ticari, bad, null)));
                Assert.Equal((422, "RECALL_QUANTITY_EXCEEDS_SHIPPED"), Code(await w.S.CommandAsync(caseId, "consumption-declarations", ticari, w.Consume(c.ToString("D"), 999m), null)));
                var notReturn = w.S.Persisted("inventory_transactions").First(t => t["MovementType"].AsString.Contains("Receipt", StringComparison.OrdinalIgnoreCase))["_id"].ToString()!; // not a CUSTOMER_RETURN
                Assert.Equal((422, "RECALL_RETURN_NOT_ELIGIBLE"), Code(await w.S.CommandAsync(caseId, "return-links", depo, w.Link(notReturn, c.ToString("D")), null)));
                Assert.Equal(0, _factory.Crm.Calls);
            }

            _ = shipC;
        }
        finally
        {
            _factory.Crm.Behaviour.Clear();
        }
    }

    private static (int, string) Code((int Status, JsonElement Body, HttpResponseMessage Raw) answer) => (answer.Status, RecallScenario.Code(answer.Body));
}
