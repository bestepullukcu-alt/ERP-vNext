using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Views.Traceability.Lots;
using Diten.Web.Views.Traceability.Serials;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Traceability;

/// <summary>
/// MOD-0174 P3 MVC proxies on the REAL controllers with a fake Gateway: one TRACE call per list page with the translated
/// query; the W-0173-02 company header on /api/trace for a token without a company (and no call without one); permission
/// refusals that never reach the Gateway; the quick lot's own key; the GS1 answer with the GTIN-null warning; the Stock
/// Ledger picker with Blocked / Expired disabled.
/// </summary>
public sealed class TraceProxyControllerTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Company = Guid.Parse("77777777-0000-0000-0000-000000000001");
    private static readonly string Sku = "Lsku:c3d4e5f6-0000-0000-0000-000000000002";

    [Fact]
    public async Task The_lot_list_is_one_listLots_call_with_the_stock_and_the_selected_company()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/trace/lots", 200, """{"items":[{"lotId":"11111111-1111-1111-1111-111111111111","lotNumber":"L-1","itemId":"b1f2c3d4-0000-0000-0000-000000000001","status":"Released","stockSummary":{"onHand":"12.000","baseUomId":"EA"}}],"total":1,"page":2,"pageSize":10,"contractVersion":"v1"}""");
        var controller = Lots(gateway, [TracePermissionKeys.LotsRead], cookieCompany: true, ("start", "10"), ("length", "10"));

        var json = Body(await controller.List(CancellationToken.None));

        var call = Assert.Single(gateway.Requests, r => r.Path.StartsWith("/api/trace/lots", StringComparison.Ordinal));
        Assert.Equal("/api/trace/lots?page=2&pageSize=10&includeStock=true", call.Path);
        Assert.Equal(Company.ToString("D"), call.LegalEntity);
        Assert.Equal(TenantId.ToString(), call.Tenant);
        var row = json.GetProperty("data").GetProperty("items")[0];
        Assert.Equal("12.000", row.GetProperty("onHand").GetString());
        Assert.Equal(1, json.GetProperty("data").GetProperty("total").GetInt32());
    }

    /// <summary>
    /// E4 regression (WP-0174-P3, measured live): once a lot's PRODUCT resolved, FU01 MasterLabelResolver indexed an
    /// empty SKU list and Lot Gezgini answered 500. The trace pages hand it the rows' SKUs; a row without one is still
    /// listed (raw ids), never a crash.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_lot_list_survives_a_resolving_product_master_with_or_without_row_SKUs(bool rowHasSku)
    {
        var gateway = new FakeGateway();
        var sku = rowHasSku ? "\"skuId\":\"c3d4e5f6-0000-0000-0000-000000000002\",\"skuLevel\":\"Lsku\"," : string.Empty;
        gateway.On("/api/trace/lots", 200, "{\"items\":[{\"lotId\":\"11111111-1111-1111-1111-111111111111\",\"lotNumber\":\"L-1\"," + sku + "\"itemId\":\"b1f2c3d4-0000-0000-0000-000000000001\",\"status\":\"Released\"}],\"total\":1}");
        gateway.On("/api/product-master/products/", 200, """{"canonicalCode":"GP-000123","name":"Amoksisilin"}""");
        gateway.On("/api/product-master/skus/", 200, """{"canonicalCode":"LSKU-TR-000045"}""");

        var json = Body(await Lots(gateway, [TracePermissionKeys.LotsRead], true, ("start", "0"), ("length", "10")).List(CancellationToken.None));

        var row = json.GetProperty("data").GetProperty("items")[0];
        if (rowHasSku)
        {
            Assert.Equal("GP-000123 · Amoksisilin", row.GetProperty("product").GetString());
            Assert.Equal("LSKU-TR-000045", row.GetProperty("sku").GetString());
        }
        else
        {
            Assert.DoesNotContain(gateway.Requests, r => r.Path.StartsWith("/api/product-master", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Without_a_selected_company_no_trace_call_is_made()
    {
        var gateway = new FakeGateway();
        var result = Assert.IsType<JsonResult>(await Lots(gateway, [TracePermissionKeys.LotsRead], cookieCompany: false, ("start", "0"), ("length", "10")).List(CancellationToken.None));
        Assert.Equal(403, result.StatusCode);
        Assert.DoesNotContain(gateway.Requests, r => r.Path.StartsWith("/api/trace", StringComparison.Ordinal));
        Assert.Equal("Error.COMPANY_NOT_SELECTED", Body(result).GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Without_the_key_nothing_reaches_the_gateway()
    {
        var gateway = new FakeGateway();
        var list = await Lots(gateway, [], true, ("start", "0"), ("length", "10")).List(CancellationToken.None);
        var create = await Lots(gateway, [TracePermissionKeys.LotsRead], true).CreateLot(new LotFormInput { IdempotencyKey = Guid.NewGuid().ToString() }, CancellationToken.None);
        var status = await Lots(gateway, [TracePermissionKeys.LotsRead], true).ChangeStatus(Guid.NewGuid().ToString(), new LotStatusInput(Guid.NewGuid().ToString(), "Blocked", "QA kararı uzun gerekçe", true, "W/\"1\""), CancellationToken.None);
        var serials = await Serials(gateway, [TracePermissionKeys.LotsRead], ("start", "0"), ("length", "15")).List(CancellationToken.None);

        Assert.All(new[] { list, create, status, serials }, r => Assert.Equal(403, Assert.IsType<JsonResult>(r).StatusCode));
        Assert.Empty(gateway.Requests);
        Assert.Equal("Error.NoPermission", Body(list).GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task The_quick_lot_creates_a_Draft_with_its_own_key_on_the_movement_s_SKU()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/trace/lots", 201, """{"lotId":"22222222-2222-2222-2222-222222222222","lotNumber":"Q-1","status":"Draft","contractVersion":"v1"}""");
        var key = Guid.NewGuid();
        var form = new LotFormInput
        {
            IdempotencyKey = key.ToString(), ItemId = "b1f2c3d4-0000-0000-0000-000000000001", Sku = Sku, LotNumber = "q-1",
            ExpiryDate = "2028-01-01", Origin = "ledger-shortcut"
        };

        var json = Body(await Lots(gateway, [TracePermissionKeys.LotsCreate], true).CreateLot(form, CancellationToken.None));

        var call = Assert.Single(gateway.Requests);
        Assert.Equal("POST", call.Method);
        Assert.Equal("quick-lot-" + key.ToString("N"), call.IdempotencyKey);
        using var body = JsonDocument.Parse(call.Body);
        Assert.Equal("Q-1", body.RootElement.GetProperty("lotNumber").GetString());
        Assert.Equal("c3d4e5f6-0000-0000-0000-000000000002", body.RootElement.GetProperty("skuId").GetString());
        Assert.Equal("Lsku", body.RootElement.GetProperty("skuLevel").GetString());
        Assert.Equal("2028-01-01T00:00:00Z", body.RootElement.GetProperty("expiryDate").GetString());
        Assert.False(body.RootElement.TryGetProperty("status", out _)); // the service makes it Draft
        Assert.Equal("Draft", json.GetProperty("status").GetString());
        Assert.Equal("22222222-2222-2222-2222-222222222222", json.GetProperty("option").GetProperty("value").GetString());
    }

    [Fact]
    public async Task A_refused_create_comes_back_to_its_field()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/trace/lots", 409, """{"error":{"code":"LOT_ALREADY_EXISTS","message":"x","correlationId":"c-1"},"contractVersion":"v1"}""");
        var form = new LotFormInput { IdempotencyKey = Guid.NewGuid().ToString(), ItemId = Guid.NewGuid().ToString(), Sku = Sku, LotNumber = "L-1" };

        var result = Assert.IsType<JsonResult>(await Lots(gateway, [TracePermissionKeys.LotsCreate], true).CreateLot(form, CancellationToken.None));
        Assert.Equal(409, result.StatusCode);
        var error = Body(result).GetProperty("error");
        Assert.Equal("lotNumber", error.GetProperty("field").GetString());
        Assert.Equal("Error.LOT_ALREADY_EXISTS", error.GetProperty("message").GetString());
        Assert.Equal("c-1", error.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task The_picker_lists_the_SKU_s_lots_with_Blocked_and_Expired_disabled()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/trace/lots", 200, """{"items":[{"lotId":"a1111111-1111-1111-1111-111111111111","lotNumber":"OK","status":"Released"},{"lotId":"b1111111-1111-1111-1111-111111111111","lotNumber":"BLK","status":"Blocked"}],"total":2}""");

        var json = Body(await Lots(gateway, [TracePermissionKeys.LotsRead], true).FormLots(Sku, CancellationToken.None));

        Assert.StartsWith("/api/trace/lots?skuId=c3d4e5f6-0000-0000-0000-000000000002&skuLevel=Lsku", Assert.Single(gateway.Requests).Path);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.False(items.Single(i => i.GetProperty("lotNumber").GetString() == "OK").GetProperty("disabled").GetBoolean());
        var blocked = items.Single(i => i.GetProperty("lotNumber").GetString() == "BLK");
        Assert.True(blocked.GetProperty("disabled").GetBoolean());
        Assert.Equal("Lot.BlockedDisabled", blocked.GetProperty("disabledReason").GetString());
    }

    [Fact]
    public async Task The_GS1_check_carries_the_GTIN_null_warning()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/trace/serials/gs1-check", 200, """{"serialNumber":"SN1","accepted":true,"gtinSkippedReason":"PRODUCT_MASTER_GTIN_NULL","checks":[{"field":"Gtin","result":"Skipped"},{"field":"Lot","result":"Ok"},{"field":"Expiry","result":"Ok"}],"contractVersion":"v1"}""");

        var json = Body(await Lots(gateway, [TracePermissionKeys.LotsRead], true).Gs1(new Gs1Input("(01)08690000000017(21)SN1(17)290630(10)L1", Sku, "a1111111-1111-1111-1111-111111111111"), CancellationToken.None));

        Assert.True(json.GetProperty("accepted").GetBoolean());
        Assert.True(json.GetProperty("gtinSkipped").GetBoolean());
        Assert.Equal("Serial.GtinSkippedWarning", json.GetProperty("warning").GetString());
        Assert.Equal("POST", Assert.Single(gateway.Requests).Method);
    }

    // ── harness ──────────────────────────────────────────────────────────────────────────────────────

    private static LotsController Lots(FakeGateway gateway, string[] permissions, bool cookieCompany, params (string Key, string Value)[] query)
    {
        var controller = new LotsController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<LotsIndex>(), new KeyLocalizer<LotFieldIndex>(), NullLogger<LotsController>.Instance);
        controller.ControllerContext = Context(cookieCompany, query);
        return controller;
    }

    private static SerialsController Serials(FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
    {
        var controller = new SerialsController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<SerialsIndex>(), NullLogger<SerialsController>.Instance);
        controller.ControllerContext = Context(true, query);
        return controller;
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static ControllerContext Context(bool cookieCompany, (string Key, string Value)[] query)
    {
        // AuthService issues no legal_entity_id claim (W-0173-02): the company comes from the selector cookie.
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "55555555-0000-0000-0000-000000000001") };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        if (cookieCompany) http.Request.Headers.Cookie = $"{CompanySelection.CookieName(http.User)}={Company}";
        http.Request.Query = new QueryCollection(query.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => new StringValues(g.Select(p => p.Value).ToArray())));
        return new ControllerContext { HttpContext = http };
    }

    private static JsonElement Body(IActionResult result)
    {
        var json = JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return JsonDocument.Parse(json).RootElement.Clone();
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

        public List<(string Method, string Path, string? Tenant, string? IdempotencyKey, string Body, string? LegalEntity)> Requests { get; } = [];

        public void On(string prefix, int status, string body) => _routes.Add((prefix, status, body));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add((request.Method.Method, path,
                    request.Headers.TryGetValues("X-Tenant-Id", out var tenant) ? tenant.Single() : null,
                    request.Headers.TryGetValues("Idempotency-Key", out var key) ? key.Single() : null,
                    body,
                    request.Headers.TryGetValues("X-Legal-Entity-Id", out var le) ? le.Single() : null));
            }

            var route = _routes.FirstOrDefault(r => path.StartsWith(r.Prefix, StringComparison.Ordinal));
            return route.Prefix is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
        }
    }
}
