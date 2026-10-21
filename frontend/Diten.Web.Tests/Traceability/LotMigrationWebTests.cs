using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Views.Inventory.StockMovements;
using Diten.Web.Views.Traceability.LotMigration;
using Diten.Web.Views.Traceability.Lots;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Traceability;

/// <summary>
/// WP-0174-P4 on the REAL views and controllers (fake Gateway, never the network):
/// <list type="bullet">
/// <item>Elle Girilmiş Lotları Aktarma: the toolbar buttons and the row-action table only with <c>trace.lots.migrate</c>;
/// the MVC proxy (all groups at once with labels, the commit's shape, its key, the error sentence, nothing sent when the
/// MVC refuses);</item>
/// <item>W-UI-02 closure (CT K4-1): the typed-lot input and the "unregistered" badges disappear once the tenant's migration
/// closed typing (read with the user's own <c>trace.lots.read</c>), and stay as today while it is open;</item>
/// <item>FU-0174-P3-07: the K-F retest confirmation is enforced by the movement form post.</item>
/// </list>
/// </summary>
public sealed class LotMigrationWebTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "http://gateway.test";
    private const string Index = "/Views/Traceability/LotMigration/Index.cshtml";
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly string Lot = "a1111111-1111-1111-1111-111111111111";

    private readonly WebApplicationFactory<Program> _factory;

    public LotMigrationWebTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ── the page ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_toolbar_and_the_row_action_table_are_the_migrate_holder_s_only()
    {
        var html = await TraceViewRenderer.RenderAsync(_factory, Index, null, "trace.lots.migrate");
        Assert.Contains("data-trace-action=\"APPLYALLSUGGESTIONS\"", html, StringComparison.Ordinal);
        Assert.Contains("data-trace-action=\"CONFIRMMIGRATION\"", html, StringComparison.Ordinal);
        var table = JsonDocument.Parse(Between(html, "<script id=\"trace-actions\" type=\"application/json\">", "</script>")).RootElement;
        Assert.Equal(TraceActions.MigrationOnly.OrderBy(c => c), table.EnumerateObject().Select(p => p.Name).OrderBy(c => c));
        Assert.All(table.EnumerateObject(), p => Assert.True(p.Value.GetBoolean()));

        var reader = await TraceViewRenderer.RenderAsync(_factory, Index, null, "trace.lots.read", "trace.lots.create", "trace.lots.change-status");
        Assert.Contains("diten-access-denied", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("data-trace-action", reader, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_page_loads_every_group_with_labels_normalised_text_and_the_suggested_lot_number()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/trace/migration/manual-lots", 200, """
            {"items":[{"groupId":"mlg-1","itemId":"b1f2c3d4-0000-0000-0000-000000000001","skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku","typedText":" l-001 ","transactionCount":3,"onHand":"4.000","baseUomId":"EA","suggestion":{"kind":"ExactLot","lotId":"a1111111-1111-1111-1111-111111111111","note":"case"}},
                      {"groupId":"mlg-2","itemId":"b1f2c3d4-0000-0000-0000-000000000001","skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku","typedText":"bad lot!","transactionCount":1,"onHand":"0.000","suggestion":{"kind":"None","note":"new-invalid"}}],
             "total":2,"totals":{"groups":2,"transactions":4},"contractVersion":"v1"}
            """);
        gateway.On("/api/trace/lots", 200, """{"items":[{"lotId":"a1111111-1111-1111-1111-111111111111","lotNumber":"L-001","status":"Released"}],"total":1}""");

        var json = Body(await Migration(gateway, ["trace.lots.migrate"]).Groups(CancellationToken.None));

        var call = Assert.Single(gateway.Requests, r => r.Path.StartsWith("/api/trace/migration", StringComparison.Ordinal));
        Assert.Equal("/api/trace/migration/manual-lots?view=all&page=1&pageSize=200", call.Path);
        var rows = json.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(("L-001", true, "L-001"), (rows[0].GetProperty("normalized").GetString(), rows[0].GetProperty("normalizedValid").GetBoolean(),
            rows[0].GetProperty("suggestion").GetProperty("lotNumber").GetString()));
        Assert.Equal("Lsku:c3d4e5f6-0000-0000-0000-000000000002", rows[0].GetProperty("sku").GetString());
        Assert.False(rows[1].GetProperty("normalizedValid").GetBoolean());
        Assert.Equal(4, json.GetProperty("totals").GetProperty("transactions").GetInt32());
    }

    [Fact]
    public async Task The_commit_carries_every_decision_with_the_page_s_key_and_normalised_new_lots()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/trace/migration/manual-lots/commit", 200, """{"migrationId":"m-1","completedAt":"2026-10-06T10:00:00Z","transactionsLinked":4,"lotsLinked":2,"lotsCreated":1,"contractVersion":"v1"}""");
        var key = Guid.NewGuid();
        var input = new MigrationCommitInput
        {
            IdempotencyKey = key.ToString(), Reason = "  W-UI-02 kapanisi icin aktarim  ", ExpectedGroupCount = 3,
            Decisions =
            [
                new() { GroupId = "mlg-1", Kind = "LinkExisting", LotId = Lot.ToUpperInvariant() },
                new() { GroupId = "mlg-2", Kind = "Typo", TargetGroupId = "mlg-3" },
                new() { GroupId = "mlg-3", Kind = "NewLot", NewLot = new LotFormInput { LotNumber = " l-9 ", ExpiryDate = "2028-01-31", CountryOfOrigin = "tr" } }
            ]
        };

        var json = Body(await Migration(gateway, ["trace.lots.migrate"]).Commit(input, CancellationToken.None));

        Assert.True(json.GetProperty("success").GetBoolean());
        var call = Assert.Single(gateway.Requests);
        Assert.Equal(("POST", "/api/trace/migration/manual-lots/commit", "lot-migration-" + key.ToString("N")), (call.Method, call.Path, call.IdempotencyKey));
        using var body = JsonDocument.Parse(call.Body);
        var root = body.RootElement;
        Assert.Equal(("W-UI-02 kapanisi icin aktarim", 3), (root.GetProperty("reason").GetString(), root.GetProperty("expectedGroupCount").GetInt32()));
        var decisions = root.GetProperty("decisions").EnumerateArray().ToList();
        Assert.Equal(Lot, decisions[0].GetProperty("lotId").GetString());
        Assert.Equal("mlg-3", decisions[1].GetProperty("targetGroupId").GetString());
        var newLot = decisions[2].GetProperty("newLot");
        Assert.Equal(("L-9", "2028-01-31T00:00:00Z", "TR"), (newLot.GetProperty("lotNumber").GetString(), newLot.GetProperty("expiryDate").GetString(),
            newLot.GetProperty("countryOfOrigin").GetString()));
    }

    [Fact]
    public async Task A_short_reason_or_an_invalid_new_lot_never_reaches_the_gateway_and_a_stale_list_says_so()
    {
        var gateway = new FakeGateway();
        var shortReason = await Migration(gateway, ["trace.lots.migrate"]).Commit(new MigrationCommitInput
        {
            IdempotencyKey = Guid.NewGuid().ToString(), Reason = "dokuz har", ExpectedGroupCount = 0, Decisions = []
        }, CancellationToken.None);
        var badLot = await Migration(gateway, ["trace.lots.migrate"]).Commit(new MigrationCommitInput
        {
            IdempotencyKey = Guid.NewGuid().ToString(), Reason = "W-UI-02 kapanisi icin aktarim", ExpectedGroupCount = 1,
            Decisions = [new() { GroupId = "mlg-1", Kind = "NewLot", NewLot = new LotFormInput { LotNumber = "bad lot!" } }]
        }, CancellationToken.None);
        var noKey = await Migration(gateway, ["trace.lots.read"]).Commit(new MigrationCommitInput(), CancellationToken.None);

        Assert.Equal(400, Status(shortReason));
        Assert.Equal("Validation.ReasonMin", Body(shortReason).GetProperty("fields").GetProperty("reason").GetString());
        Assert.Equal("Validation.LotFormat", Body(badLot).GetProperty("fields").GetProperty("decision:mlg-1").GetString());
        Assert.Equal(403, Status(noKey));
        Assert.Empty(gateway.Requests);

        gateway.On("/api/trace/migration/manual-lots/commit", 409, """{"error":{"code":"MIGRATION_STALE","message":"x","correlationId":"c-9"},"contractVersion":"v1"}""");
        var stale = await Migration(gateway, ["trace.lots.migrate"]).Commit(new MigrationCommitInput
        {
            IdempotencyKey = Guid.NewGuid().ToString(), Reason = "W-UI-02 kapanisi icin aktarim", ExpectedGroupCount = 0, Decisions = []
        }, CancellationToken.None);
        Assert.Equal(409, Status(stale));
        Assert.Equal(("Error.MIGRATION_STALE", "c-9"), (Body(stale).GetProperty("error").GetProperty("message").GetString(),
            Body(stale).GetProperty("error").GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task The_status_is_read_with_trace_lots_read_and_without_it_nothing_is_asked()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/trace/migration/status", 200, """{"manualLotEntryClosed":true,"completedAt":"2026-10-06T10:00:00Z","contractVersion":"v1"}""");

        var without = Body(await Migration(gateway, ["inventory.stock.read"]).Status(CancellationToken.None));
        Assert.False(without.GetProperty("closed").GetBoolean());
        Assert.Empty(gateway.Requests);

        var with = Body(await Migration(gateway, ["trace.lots.read"]).Status(CancellationToken.None));
        Assert.True(with.GetProperty("closed").GetBoolean());
        Assert.Equal("/api/trace/migration/status", Assert.Single(gateway.Requests).Path);
    }

    // ── W-UI-02 closure ──────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task The_typed_lot_input_is_drawn_only_while_typing_is_open(bool closed, bool drawn)
    {
        var model = StockMovementFormViewModel.ForCreate(new QueryCollection());
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Inventory/StockMovements/Create.cshtml", model, closed,
            "inventory.stock.read", "inventory.movements.create", "trace.lots.read");
        Assert.Equal(drawn, html.Contains("id=\"btnTypedLot\"", StringComparison.Ordinal));
        Assert.Equal(drawn, html.Contains("id=\"lotIdTyped\"", StringComparison.Ordinal));
        Assert.Contains("id=\"lotId\"", html, StringComparison.Ordinal); // the registered-lot picker stays
    }

    [Theory]
    [InlineData("TYPED-1", false, true)]
    [InlineData("TYPED-1", true, false)]
    [InlineData("a1111111-1111-1111-1111-111111111111", false, false)]
    public async Task The_receipt_badges_a_typed_lot_only_while_typing_is_open(string lotId, bool closed, bool badge)
    {
        var receipt = new MovementReceiptViewModel { TransactionNumber = "INV-1", LotId = lotId };
        var html = await TraceViewRenderer.RenderAsync(_factory, "/Views/Inventory/StockMovements/Details.cshtml", receipt, closed,
            "inventory.stock.read", "inventory.movements.create", "trace.lots.read");
        Assert.Equal(badge, html.Contains("<span class=\"badge bg-label-secondary ms-1\" title=", StringComparison.Ordinal));
    }

    [Fact]
    public void The_list_scripts_badge_a_typed_lot_only_and_ask_the_closure_once()
    {
        foreach (var file in new[] { "StockMovements/index.js", "StockLedger/index.js" })
        {
            var js = File.ReadAllText(Path.Combine(TraceViewRenderer.WebRoot, "wwwroot", "assets", "js", "Inventory", file));
            Assert.Contains("fetch('/LotMigration/status'", js, StringComparison.Ordinal);
            Assert.Contains("REGISTERED_LOT.test(data) || lotTypingClosed", js, StringComparison.Ordinal);
            Assert.DoesNotContain("every lot is an \"unregistered lot", js, StringComparison.Ordinal);
            Assert.Contains("lotBadge(data)", js, StringComparison.Ordinal);
        }
    }

    // ── FU-0174-P3-07: K-F on the form post ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_retest_past_lot_is_refused_by_the_form_post_without_the_confirmation_and_posts_with_it()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/trace/lots/{Lot}", 200, $$"""{"lotId":"{{Lot}}","lotNumber":"RT-1","status":"Released","retestDue":true,"contractVersion":"v1"}""");
        gateway.On("/api/inventory/movements", 201, """{"transactionId":"t-1","transactionNumber":"INV-2026-0000009","balanceSnapshot":{"onHand":"5.000"}}""");
        string[] permissions = ["inventory.stock.read", "inventory.movements.create", "trace.lots.read"];

        var refused = await Movements(gateway, permissions, confirmed: false).PostMovement(Inbound(Lot), CancellationToken.None);
        Assert.Equal(400, Status(refused));
        Assert.Equal("Lot.RetestRequired", Body(refused).GetProperty("fields").GetProperty("lotId").GetString());
        Assert.DoesNotContain(gateway.Requests, r => r.Path.StartsWith("/api/inventory/movements", StringComparison.Ordinal));

        var posted = await Movements(gateway, permissions, confirmed: true).PostMovement(Inbound(Lot), CancellationToken.None);
        Assert.True(Body(posted).GetProperty("success").GetBoolean());
        Assert.Single(gateway.Requests, r => r.Path.StartsWith("/api/inventory/movements", StringComparison.Ordinal));

        // A typed lot and a lot without a passed retest date are not asked about.
        var typed = await Movements(gateway, permissions, confirmed: false).PostMovement(Inbound("TYPED-1"), CancellationToken.None);
        Assert.True(Body(typed).GetProperty("success").GetBoolean());
    }

    // ── harness ──────────────────────────────────────────────────────────────────────────────────────────

    private static MovementSubmitViewModel Inbound(string lotId) => new()
    {
        IdempotencyKey = Guid.NewGuid().ToString(), MovementType = "OPENING_BALANCE", ItemId = "b1f2c3d4-0000-0000-0000-000000000001",
        Sku = "Lsku:c3d4e5f6-0000-0000-0000-000000000002", ToWarehouseId = "11111111-1111-1111-1111-111111111111",
        ToLocationId = "33333333-3333-3333-3333-333333333333", ToStockStatus = "AVAILABLE", LotId = lotId, Quantity = "5", UomId = "EA",
        TransactionDate = "2026-10-02T09:00:00.000Z", PostingDate = "2026-10-02T09:00:00.000Z"
    };

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal) + start.Length;
        return text[from..text.IndexOf(end, from, StringComparison.Ordinal)];
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static DefaultHttpContext Http(string[] permissions)
    {
        // The token names its company (W-0173-02): no selector cookie, the company header goes with every /api/trace call.
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "55555555-0000-0000-0000-000000000001"), new("legal_entity_id", Guid.NewGuid().ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        return new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
    }

    private static LotMigrationController Migration(FakeGateway gateway, string[] permissions)
    {
        var controller = new LotMigrationController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<LotMigrationIndex>(), NullLogger<LotMigrationController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = Http(permissions) };
        return controller;
    }

    private static StockMovementsController Movements(FakeGateway gateway, string[] permissions, bool confirmed)
    {
        var controller = new StockMovementsController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<StockMovementsIndex>(), NullLogger<StockMovementsController>.Instance, new KeyLocalizer<LotFieldIndex>());
        var http = Http(permissions);
        if (confirmed) http.Request.Headers[LotRetestGate.Header] = "true";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new MemoryTempData());
        return controller;
    }

    private static int Status(IActionResult result) => Assert.IsType<JsonResult>(result).StatusCode ?? 200;

    private static JsonElement Body(IActionResult result)
        => JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement.Clone();

    private sealed class MemoryTempData : ITempDataProvider
    {
        private IDictionary<string, object> _values = new Dictionary<string, object>();
        public IDictionary<string, object> LoadTempData(HttpContext context) => _values;
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => _values = values;
    }

    private sealed class Permissions(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class FakeGateway : HttpMessageHandler
    {
        private readonly List<(string Prefix, int Status, string Body)> _routes = [];

        public List<(string Method, string Path, string? IdempotencyKey, string Body, string? LegalEntity)> Requests { get; } = [];

        public void On(string prefix, int status, string body) { Contracts.ContractRoutes.Require(prefix, status, body); _routes.Insert(0, (prefix, status, body)); } // FU-WEB-CONTRACT-FAKES

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add((request.Method.Method, path,
                    request.Headers.TryGetValues("Idempotency-Key", out var key) ? key.Single() : null, body,
                    request.Headers.TryGetValues("X-Legal-Entity-Id", out var le) ? le.Single() : null));
            }

            var route = _routes.FirstOrDefault(r => path.StartsWith(r.Prefix, StringComparison.Ordinal));
            return route.Prefix is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
        }
    }
}
