using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Services;
using Diten.Web.Views.Inventory.StockLedger;
using Diten.Web.Views.Inventory.StockMovements;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// MOD-0173-FU01 MVC proxies driven on the REAL controllers with a Gateway handler that answers what Gateway 5000 +
/// InventoryService answer. Measures: one /balance or /transactions call per page with the translated paging; the
/// KPI folded into the same response; raw ids when a master is unreachable; the reversal payload (parent
/// ASSUMPTION-0173-05) and its Idempotency-Key; permission refusals that never reach the Gateway; W-UI-01 (Model A
/// selector routes only, never /api/mdm/products).
/// </summary>
public sealed class InventoryProxyControllerTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly string Item = "b1f2c3d4-0000-0000-0000-000000000001";
    private static readonly string Sku = "c3d4e5f6-0000-0000-0000-000000000002";

    private static readonly string[] ReadAndCreate = [InventoryPermissionKeys.Read, InventoryPermissionKeys.CreateMovements];

    [Fact]
    public async Task Stock_list_page_2_is_one_balance_call_with_page_2_pageSize_50()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/balance", 200, $$"""{"rows":[{"itemId":"{{Item}}","skuId":"{{Sku}}","skuLevel":"Lsku","warehouseId":"wh-01","locationId":"loc-1","stockStatus":"AVAILABLE","onHand":"100.000","reserved":"0","available":"100.000","inTransit":"0","blocked":"0","qualityInspection":"0","baseUomId":"EA"}],"total":51,"contractVersion":"v1"}""");
        var controller = Ledger(gateway, ReadAndCreate, ("start", "50"), ("length", "50"), ("draw", "2"));

        var json = Body(await controller.List(CancellationToken.None));

        var balanceCalls = gateway.Requests.Where(r => r.PathAndQuery.StartsWith("/api/inventory/balance", StringComparison.Ordinal)).ToList();
        var call = Assert.Single(balanceCalls);
        Assert.Equal("/api/inventory/balance?page=2&pageSize=50", call.PathAndQuery);
        Assert.Equal(TenantId.ToString(), call.TenantHeader);
        Assert.DoesNotContain(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/inventory/availability", StringComparison.Ordinal));
        Assert.Equal(51, json.GetProperty("data").GetProperty("total").GetInt32());
        Assert.Equal(51, json.GetProperty("data").GetProperty("filteredTotal").GetInt32());
        Assert.Equal("100.000", json.GetProperty("data").GetProperty("items")[0].GetProperty("onHand").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("summary").ValueKind);
    }

    [Fact]
    public async Task With_a_product_filter_the_kpi_comes_from_availability_in_the_same_response()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/balance", 200, """{"rows":[],"total":0}""");
        gateway.On("/api/inventory/availability", 200, $$"""{"itemId":"{{Item}}","onHand":"100.000","reserved":"30.000","available":"70.000","byStatus":{"QUALITY_INSPECTION":"5.000","QUARANTINE":"1.000","BLOCKED":"2.000"},"baseUomId":"EA"}""");
        var controller = Ledger(gateway, ReadAndCreate, ("start", "0"), ("length", "50"), ("itemId", Item));

        var json = Body(await controller.List(CancellationToken.None));

        Assert.Single(gateway.Requests, r => r.PathAndQuery == $"/api/inventory/availability?itemId={Item}");
        var summary = json.GetProperty("summary");
        Assert.Equal("100.000", summary.GetProperty("onHand").GetString());
        Assert.Equal("70.000", summary.GetProperty("available").GetString());
        Assert.Equal("5.000", summary.GetProperty("qualityInspection").GetString());
        Assert.Equal("3.000", summary.GetProperty("quarantineBlocked").GetString());
    }

    [Fact]
    public async Task An_unreachable_master_leaves_raw_ids_and_says_so_but_the_list_still_answers()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/balance", 200, $$"""{"rows":[{"itemId":"{{Item}}","skuId":"{{Sku}}","skuLevel":"Lsku","warehouseId":"wh-01","onHand":"1"}],"total":1}""");
        // Gateway has no /api/product-master or /api/locations route (BG-10): an empty 404 is not the contract's answer.
        gateway.On("/api/product-master", 404, string.Empty);
        gateway.On("/api/locations", 404, string.Empty);
        var controller = Ledger(gateway, ReadAndCreate, ("start", "0"), ("length", "50"));

        var result = await controller.List(CancellationToken.None);
        var json = Body(result);

        Assert.False(json.GetProperty("labelsAvailable").GetBoolean());
        var row = json.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(JsonValueKind.Null, row.GetProperty("productLabel").ValueKind);
        Assert.Equal(Item, row.GetProperty("itemId").GetString());
        // One probe per master, not one per id.
        Assert.Single(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/product-master", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_the_read_permission_nothing_reaches_the_gateway()
    {
        var gateway = new FakeGateway();
        var ledger = await Ledger(gateway, [], ("start", "0"), ("length", "50")).List(CancellationToken.None);
        var history = await Movements(gateway, [], ("start", "0"), ("length", "50")).List(CancellationToken.None);
        var lookup = await Movements(gateway, []).Lookups("products", "a", null, CancellationToken.None);

        Assert.All(new[] { ledger, history, lookup }, r => Assert.Equal(403, Assert.IsType<JsonResult>(r).StatusCode));
        Assert.Empty(gateway.Requests);
        Assert.Equal("ErrNoPermission", Body(ledger).GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_service_refusal_keeps_its_status_and_becomes_a_localized_sentence()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/transactions", 503, """{"error":{"code":"PRODUCT_MASTER_UNAVAILABLE","message":"down","correlationId":"abc"},"contractVersion":"v1"}""");

        var result = Assert.IsType<JsonResult>(await Movements(gateway, ReadAndCreate, ("start", "0"), ("length", "50")).List(CancellationToken.None));

        Assert.Equal(503, result.StatusCode);
        var error = Body(result).GetProperty("error");
        Assert.Equal("PRODUCT_MASTER_UNAVAILABLE", error.GetProperty("code").GetString());
        Assert.Equal("ErrMasterUnavailable", error.GetProperty("message").GetString());
        Assert.Equal("abc", error.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task History_list_marks_automatic_and_reversible_rows()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/transactions", 200, $$"""{"items":[{"transactionId":"{{Guid.NewGuid()}}","transactionNumber":"INV-1","movementType":"COUNT_LOSS","sourceModule":"MOD-0173","itemId":"{{Item}}","quantity":"1.000","uomId":"EA"},{"transactionId":"{{Guid.NewGuid()}}","transactionNumber":"INV-2","movementType":"GOODS_RECEIPT_PO","sourceModule":"MOD-0142","itemId":"{{Item}}","quantity":"5","uomId":"EA"}],"page":1,"pageSize":50,"total":2}""");

        var json = Body(await Movements(gateway, ReadAndCreate, ("start", "0"), ("length", "50")).List(CancellationToken.None));

        Assert.Single(gateway.Requests, r => r.PathAndQuery == "/api/inventory/transactions?page=1&pageSize=50");
        var items = json.GetProperty("data").GetProperty("items");
        Assert.True(items[0].GetProperty("canReverse").GetBoolean());
        Assert.False(items[0].GetProperty("isAutomatic").GetBoolean());
        Assert.False(items[1].GetProperty("canReverse").GetBoolean());
        Assert.True(items[1].GetProperty("isAutomatic").GetBoolean());
    }

    [Fact]
    public async Task Reversal_posts_a_REVERSAL_of_the_original_with_one_idempotency_key()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/movements", 201, """{"transactionId":"t-2","transactionNumber":"INV-2026-0000002","idempotentReplay":false}""");
        var original = Guid.NewGuid();
        var key = Guid.NewGuid();

        var json = Body(await Movements(gateway, ReadAndCreate).Reverse(original.ToString(), Reversal(key), CancellationToken.None));

        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("INV-2026-0000002", json.GetProperty("transactionNumber").GetString());
        var post = Assert.Single(gateway.Requests);
        Assert.Equal("POST", post.Method);
        Assert.Equal("/api/inventory/movements", post.PathAndQuery);
        Assert.Equal("reversal-" + key.ToString("N"), post.IdempotencyKey);
        using var body = JsonDocument.Parse(post.Body);
        var root = body.RootElement;
        Assert.Equal(post.IdempotencyKey, root.GetProperty("idempotencyKey").GetString());
        Assert.Equal("REVERSAL", root.GetProperty("movementType").GetString());
        Assert.Equal("MOD-0173", root.GetProperty("sourceModule").GetString());
        Assert.Equal("REVERSAL", root.GetProperty("sourceType").GetString());
        Assert.Equal(original.ToString("D"), root.GetProperty("sourceDocumentId").GetString());
        Assert.Equal(Item, root.GetProperty("itemId").GetString());
        Assert.Equal(Sku, root.GetProperty("skuId").GetString());
        Assert.Equal("Lsku", root.GetProperty("skuLevel").GetString());
        Assert.Equal("12.500", root.GetProperty("quantity").GetString());
        Assert.Equal("EA", root.GetProperty("uomId").GetString());
        // Sides, lot, serials and scope are never sent: the service derives them from the original (parent §8).
        foreach (var absent in new[] { "lotId", "serialIds", "fromWarehouseId", "toWarehouseId", "tenantId", "legalEntityId" })
            Assert.False(root.TryGetProperty(absent, out _), absent);
    }

    [Fact]
    public async Task The_same_dialog_submitted_twice_sends_the_same_key_so_the_service_replays()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/movements", 201, """{"transactionId":"t-2","transactionNumber":"INV-2","idempotentReplay":false}""");
        var original = Guid.NewGuid().ToString();
        var key = Guid.NewGuid();

        await Movements(gateway, ReadAndCreate).Reverse(original, Reversal(key), CancellationToken.None);
        await Movements(gateway, ReadAndCreate).Reverse(original, Reversal(key), CancellationToken.None);

        Assert.Equal(2, gateway.Requests.Count);
        Assert.Single(gateway.Requests.Select(r => r.IdempotencyKey).Distinct());
    }

    [Fact]
    public async Task Already_reversed_comes_back_as_422_with_the_localized_reason()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/movements", 422, """{"error":{"code":"ALREADY_REVERSED","message":"x","correlationId":"c1"},"contractVersion":"v1"}""");

        var result = Assert.IsType<JsonResult>(await Movements(gateway, ReadAndCreate).Reverse(Guid.NewGuid().ToString(), Reversal(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(422, result.StatusCode);
        Assert.Equal("ErrAlreadyReversed", Body(result).GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task Reversal_without_the_create_permission_or_with_a_malformed_row_never_reaches_the_gateway()
    {
        var gateway = new FakeGateway();
        var readOnly = await Movements(gateway, [InventoryPermissionKeys.Read]).Reverse(Guid.NewGuid().ToString(), Reversal(Guid.NewGuid()), CancellationToken.None);
        var badQuantity = Reversal(Guid.NewGuid());
        badQuantity.Quantity = "1e3";
        var malformed = await Movements(gateway, ReadAndCreate).Reverse(Guid.NewGuid().ToString(), badQuantity, CancellationToken.None);
        var notAGuid = await Movements(gateway, ReadAndCreate).Reverse("txn-1", Reversal(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(403, Assert.IsType<JsonResult>(readOnly).StatusCode);
        Assert.Equal(400, Assert.IsType<JsonResult>(malformed).StatusCode);
        Assert.Equal(400, Assert.IsType<JsonResult>(notAGuid).StatusCode);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Product_and_sku_pickers_call_only_the_Model_A_selector_routes()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/global-products/selector", 200, $$"""{"data":{"items":[{"id":"{{Item}}","canonicalCode":"GP-1","globalProductName":"Amoxicillin"}]},"isSuccessful":true}""");
        gateway.On("/api/gskus", 200, $$$"""{"data":{"items":[{"id":"{{{Sku}}}","canonicalCode":"GSKU-1","globalProductName":"Amoxicillin"}]}}""");
        gateway.On("/api/lskus", 200, """{"data":{"items":[]}}""");

        var products = Body(await Movements(gateway, ReadAndCreate).Lookups("products", "amo", null, CancellationToken.None));
        var skus = Body(await Movements(gateway, ReadAndCreate).Lookups("skus", "amo", null, CancellationToken.None));

        Assert.Equal(Item, products.GetProperty("items")[0].GetProperty("value").GetString());
        Assert.Equal("GP-1 · Amoxicillin", products.GetProperty("items")[0].GetProperty("text").GetString());
        Assert.Equal("Gsku:" + Sku, skus.GetProperty("items")[0].GetProperty("value").GetString());
        Assert.All(gateway.Requests, r => Assert.Contains(ProductPickerSource.AllowedPaths, p => r.PathAndQuery.StartsWith(p + "?", StringComparison.Ordinal)));
        Assert.DoesNotContain(gateway.Requests, r => r.PathAndQuery.Contains("/api/mdm/products", StringComparison.Ordinal));
        Assert.Contains(gateway.Requests, r => r.PathAndQuery == "/api/global-products/selector?pageNumber=1&pageSize=20&search=amo");
    }

    [Fact]
    public async Task A_picker_whose_source_is_down_is_empty_and_says_why_with_no_invented_options()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/global-products/selector", 503, string.Empty);
        gateway.On("/api/locations", 404, string.Empty);

        var products = Body(await Movements(gateway, ReadAndCreate).Lookups("products", null, null, CancellationToken.None));
        var warehouses = Body(await Movements(gateway, ReadAndCreate).Lookups("warehouses", null, null, CancellationToken.None));

        foreach (var answer in new[] { products, warehouses })
        {
            Assert.Equal(0, answer.GetProperty("items").GetArrayLength());
            Assert.False(answer.GetProperty("available").GetBoolean());
            Assert.Equal("PickerUnavailable", answer.GetProperty("message").GetString());
        }
    }

    // ── W-0173-02 company selector (WP-0173-LE1) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_token_with_a_company_sends_no_company_header_even_with_a_selector_cookie()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/balance", 200, """{"rows":[],"total":0}""");

        await LedgerAs(new Caller(TokenCompanyId, Guid.NewGuid()), gateway, ReadAndCreate, ("start", "0"), ("length", "10")).List(CancellationToken.None);

        var call = Assert.Single(gateway.Requests);
        Assert.Null(call.LegalEntity);
    }

    [Fact]
    public async Task A_claim_less_user_with_a_selected_company_sends_it_on_lists_and_on_the_reversal()
    {
        var gateway = new FakeGateway();
        var company = Guid.NewGuid();
        gateway.On("/api/inventory/balance", 200, """{"rows":[],"total":0}""");
        gateway.On("/api/inventory/movements", 201, """{"transactionId":"t","transactionNumber":"INV-9","idempotentReplay":false}""");
        var caller = new Caller(null, company);

        await LedgerAs(caller, gateway, ReadAndCreate, ("start", "0"), ("length", "10")).List(CancellationToken.None);
        await MovementsAs(caller, gateway, ReadAndCreate).Reverse(Guid.NewGuid().ToString(), Reversal(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(2, gateway.Requests.Count);
        Assert.All(gateway.Requests, r => Assert.Equal(company.ToString("D"), r.LegalEntity));
        // the company travels in the header only — never in a query or a body
        Assert.All(gateway.Requests, r => Assert.DoesNotContain("legalEntity", r.PathAndQuery, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("legalEntity", gateway.Requests[1].Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_claim_less_user_without_a_company_makes_no_inventory_call_and_is_told_to_select_one()
    {
        var gateway = new FakeGateway();
        var caller = new Caller(null, null);

        var list = Assert.IsType<JsonResult>(await LedgerAs(caller, gateway, ReadAndCreate, ("start", "0"), ("length", "10"), ("itemId", Item)).List(CancellationToken.None));
        var history = Assert.IsType<JsonResult>(await MovementsAs(caller, gateway, ReadAndCreate, ("start", "0"), ("length", "10")).List(CancellationToken.None));
        var reverse = Assert.IsType<JsonResult>(await MovementsAs(caller, gateway, ReadAndCreate).Reverse(Guid.NewGuid().ToString(), Reversal(Guid.NewGuid()), CancellationToken.None));

        Assert.Empty(gateway.Requests);
        foreach (var result in new[] { list, history, reverse })
        {
            Assert.Equal(403, result.StatusCode);
            Assert.Equal("ErrSelectCompany", Body(result).GetProperty("error").GetProperty("message").GetString());
        }
    }

    [Fact]
    public async Task The_company_check_being_unavailable_is_a_localized_503()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/inventory/transactions", 503, """{"error":{"code":"LEGAL_ENTITY_MASTER_UNAVAILABLE","message":"x","correlationId":"c"}}""");

        var result = Assert.IsType<JsonResult>(await MovementsAs(new Caller(null, Guid.NewGuid()), gateway, ReadAndCreate, ("start", "0"), ("length", "10")).List(CancellationToken.None));

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("ErrCompanyCheckUnavailable", Body(result).GetProperty("error").GetProperty("message").GetString());
    }

    // ── harness ──────────────────────────────────────────────────────────────────────────────────────────────

    private static ReversalRequestViewModel Reversal(Guid key) => new()
    {
        IdempotencyKey = key.ToString(),
        ItemId = Item,
        SkuId = Sku,
        SkuLevel = "Lsku",
        Quantity = "12.500",
        UomId = "EA"
    };

    private static JsonElement Body(IActionResult result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement;

    /// <summary>Who calls: the token's company (claim) and/or the company in the selector cookie (W-0173-02).</summary>
    private sealed record Caller(Guid? TokenCompany, Guid? CookieCompany);

    private static readonly Guid TokenCompanyId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Caller WithTokenCompany = new(TokenCompanyId, null);

    private static StockLedgerController Ledger(FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
        => LedgerAs(WithTokenCompany, gateway, permissions, query);

    private static StockMovementsController Movements(FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
        => MovementsAs(WithTokenCompany, gateway, permissions, query);

    private static StockLedgerController LedgerAs(Caller caller, FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
    {
        var controller = new StockLedgerController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<StockLedgerIndex>(), NullLogger<StockLedgerController>.Instance);
        controller.ControllerContext = Context(caller, query);
        return controller;
    }

    private static StockMovementsController MovementsAs(Caller caller, FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
    {
        var controller = new StockMovementsController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<StockMovementsIndex>(), NullLogger<StockMovementsController>.Instance);
        controller.ControllerContext = Context(caller, query);
        return controller;
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static ControllerContext Context(Caller caller, (string Key, string Value)[] query)
    {
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()), new("sub", "55555555-0000-0000-0000-000000000001") };
        if (caller.TokenCompany is not null) claims.Add(new Claim("legal_entity_id", caller.TokenCompany.Value.ToString()));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        if (caller.CookieCompany is not null)
            http.Request.Headers.Cookie = $"{CompanySelection.CookieName(http.User)}={caller.CookieCompany.Value}";
        http.Request.Query = new QueryCollection(query.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => new StringValues(g.Select(p => p.Value).ToArray())));
        return new ControllerContext { HttpContext = http };
    }

    private sealed class Permissions(string[] keys) : IPermissionSnapshot
    {
        public bool Has(string permissionKey) => keys.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> Keys => keys;
    }

    /// <summary>Answers with the KEY, so a test can assert which resx sentence was chosen.</summary>
    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class FakeGateway : HttpMessageHandler
    {
        private readonly List<(string Prefix, int Status, string Body)> _routes = [];

        public List<(string Method, string PathAndQuery, string? TenantHeader, string? IdempotencyKey, string Body, string? LegalEntity)> Requests { get; } = [];

        public void On(string prefix, int status, string body) => _routes.Add((prefix, status, body));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add((request.Method.Method, pathAndQuery,
                    request.Headers.TryGetValues("X-Tenant-Id", out var tenant) ? tenant.Single() : null,
                    request.Headers.TryGetValues("Idempotency-Key", out var key) ? key.Single() : null,
                    body,
                    request.Headers.TryGetValues("X-Legal-Entity-Id", out var le) ? le.Single() : null));
            }

            var route = _routes.FirstOrDefault(r => pathAndQuery.StartsWith(r.Prefix, StringComparison.Ordinal));
            return route.Prefix is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
        }
    }
}
