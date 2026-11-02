using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.PutawayTasks;
using Diten.Web.Views.Warehouse.WarehouseLocationRoles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-3 — the two same-origin warehouse proxies driven on the REAL controllers with a Gateway handler (pack §11.2, §15). Measures: the list
/// wire → listPutawayTasks translation (one call, filters forwarded, refused before the Gateway when outside the contract); the permission of every
/// action (refused without its key, never reaching the Gateway); the W-0173-02 company header on /api/warehouse and the no-company refusal; the WMS
/// writes' Idempotency-Key AND If-Match on one request and their bodies (the source never in an edit, AC-15 / §8.15); the candidate list keeping a
/// locked row with its reason and blocking document number (AC-41); the exact bin-code match (AC-44); contract refusals as localized sentences with
/// their status and code kept.
/// <para>⚠ The fake answers are NOT bound to a contract file: WMS-BUNDLE is still a draft (pack §19.7 freeze step) — there is no
/// <c>wms-bundle.openapi.yaml</c> for <c>ContractRoutes</c> to load. The service side checks every answer against the draft (WmsDraftContractHandler).</para>
/// </summary>
public sealed class WarehouseProxyControllerTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid TokenCompanyId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private const string TaskId = "b1111111-0000-0000-0000-000000000001";
    private const string WarehouseId = "a2222222-0000-0000-0000-000000000002";
    private const string Bin = "a3333333-0000-0000-0000-000000000003";
    private const string User = "c4444444-0000-0000-0000-000000000004";
    private const string Key = "d5555555-0000-4000-8000-000000000005";

    private static readonly string[] ReadOnly = [WarehousePermissionKeys.PutawayTasksRead];

    private static string Task(string status = "Open", string flag = "None", string allowed = "\"Confirm\",\"Update\",\"Cancel\"", string disabled = "{\"action\":\"Retry\",\"reasonCode\":\"NoPendingConfirmation\"}") =>
        $$"""{"taskId":"{{TaskId}}","number":"MG-PUT-000042","companyCodePrefix":"MG","kind":"Putaway","status":"{{status}}","origin":"Manual","legalEntityId":"{{TokenCompanyId}}","warehouseId":"{{WarehouseId}}","item":{"itemId":"e1","skuId":"f1","skuLevel":"Lsku"},"lotNumber":"L-7","stockStatus":"AVAILABLE","fromLocationId":"a1","toLocationId":"{{Bin}}","targetSource":"Manual","plannedQuantity":"10","confirmedQuantity":"0","pendingQuantity":"0","remainingQuantity":"10","baseUomId":"EA","priority":"Normal","serialControlled":false,"confirmations":[],"integrationFlag":"{{flag}}","allowedActions":[{{allowed}}],"disabledActions":[{{disabled}}],"created":{"userId":"{{User}}","atUtc":"2026-11-01T08:00:00Z"},"version":3,"contractVersion":"v1"}""";

    // ── list ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_page_with_filters_is_one_listPutawayTasks_call_and_rows_carry_the_service_menu_rules()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/putaway-tasks", 200, $$"""{"items":[{{Task(flag: "PostingPendingLong")}}],"total":51,"page":3,"pageSize":25,"contractVersion":"v1"}""");
        var controller = Putaway(gateway, ReadOnly, ("start", "50"), ("length", "25"), ("draw", "4"), ("status", "Open"), ("status", "InProgress"),
            ("priority", "High"), ("warehouseId", WarehouseId.ToUpperInvariant()), ("assigneeUserId", User), ("search", "L-7"));

        var json = Body(await controller.List(CancellationToken.None));

        var call = Assert.Single(gateway.Requests, r => r.PathAndQuery.StartsWith("/api/warehouse/", StringComparison.Ordinal));
        Assert.Equal($"/api/warehouse/putaway-tasks?page=3&pageSize=25&orderBy=-createdAt&status=Open&status=InProgress&priority=High&warehouseId={WarehouseId}&assigneeUserId={User}&q=L-7", call.PathAndQuery);
        Assert.Equal(TenantId.ToString(), call.TenantHeader);
        var row = json.GetProperty("data").GetProperty("items")[0];
        Assert.Equal(51, json.GetProperty("data").GetProperty("total").GetInt32());
        Assert.Equal("PostingPendingLong", row.GetProperty("integrationFlag").GetString());   // the flag is its own field (AC-46)
        Assert.Equal("Open", row.GetProperty("status").GetString());
        Assert.Equal(["Confirm", "Update", "Cancel"], row.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal("NoPendingConfirmation", row.GetProperty("disabledActions")[0].GetProperty("reasonCode").GetString());
        Assert.Equal("W/\"3\"", row.GetProperty("etag").GetString());
    }

    [Theory]
    [InlineData("status", "Deleted")]
    [InlineData("priority", "Urgent")]
    [InlineData("warehouseId", "not-a-uuid")]
    [InlineData("start", "7")]
    [InlineData("length", "500")]
    public async Task A_query_outside_the_contract_is_refused_before_the_gateway(string key, string value)
    {
        var gateway = new FakeGateway();
        var query = new List<(string, string)> { ("start", "0"), ("length", "25") };
        query.RemoveAll(p => p.Item1 == key);
        query.Add((key, value));

        var result = Assert.IsType<JsonResult>(await Putaway(gateway, ReadOnly, [.. query]).List(CancellationToken.None));

        Assert.Equal(400, result.StatusCode);
        Assert.Empty(gateway.Requests);
        Assert.Equal("Warehouse.Error.VALIDATION_ERROR", Body(result).GetProperty("error").GetProperty("message").GetString());
    }

    // ── permissions ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_action_without_its_key_is_refused_and_never_reaches_the_gateway()
    {
        var gateway = new FakeGateway();
        var write = new PutawayWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"3\"", Quantity = "1", ToLocationId = Bin, Reason = "moved back to dock" };
        var results = new[]
        {
            await Putaway(gateway, [], ("start", "0"), ("length", "25")).List(CancellationToken.None),
            await Putaway(gateway, []).Detail(TaskId, CancellationToken.None),
            await Putaway(gateway, ReadOnly).Candidates(null, null, CancellationToken.None),
            await Putaway(gateway, ReadOnly).Create(write, CancellationToken.None),
            await Putaway(gateway, ReadOnly).Confirm(TaskId, write, CancellationToken.None),
            await Putaway(gateway, ReadOnly).Retry(TaskId, 1, write, CancellationToken.None),
            await Putaway(gateway, ReadOnly).Claim(TaskId, write, CancellationToken.None),
            await Putaway(gateway, ReadOnly).Returned(TaskId, 1, write, CancellationToken.None),
            await Putaway(gateway, ReadOnly).CloseShort(TaskId, write, CancellationToken.None),
            await Putaway(gateway, [WarehousePermissionKeys.PutawayTasksRead, WarehousePermissionKeys.PutawayTasksConfirm]).Cancel(TaskId, write, CancellationToken.None),
            await Putaway(gateway, [WarehousePermissionKeys.PutawayTasksRead, WarehousePermissionKeys.PutawayTasksConfirm]).Resolve(TaskId, 1, write, CancellationToken.None),
            await Putaway(gateway, [WarehousePermissionKeys.PutawayTasksRead, WarehousePermissionKeys.PutawayTasksConfirm]).BulkAssign(write, CancellationToken.None),
            await Roles(gateway, [], ("start", "0"), ("length", "25")).List(CancellationToken.None),
            await Roles(gateway, [WarehousePermissionKeys.LocationRolesRead]).Create(new LocationRoleWriteRequest { IdempotencyKey = Key, LocationId = Bin }, CancellationToken.None),
            await Roles(gateway, [WarehousePermissionKeys.LocationRolesRead]).Delete(TaskId, new LocationRoleWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"1\"" }, CancellationToken.None)
        };

        Assert.All(results, r => Assert.Equal(403, Assert.IsType<JsonResult>(r).StatusCode));
        Assert.All(results, r => Assert.Equal("Warehouse.Error.NoPermission", Body(r).GetProperty("error").GetProperty("message").GetString()));
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task Without_a_company_no_warehouse_call_is_sent_and_a_cookie_company_travels_as_the_header()
    {
        var none = new FakeGateway();
        var refused = await PutawayAs(new Caller(null, null), none, ReadOnly, ("start", "0"), ("length", "25")).List(CancellationToken.None);
        Assert.Equal(403, Assert.IsType<JsonResult>(refused).StatusCode);
        Assert.Equal("Warehouse.Error.COMPANY_NOT_SELECTED", Body(refused).GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(none.Requests);

        var chosen = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/putaway-tasks", 200, """{"items":[],"total":0,"page":1,"pageSize":25,"contractVersion":"v1"}""");
        await PutawayAs(new Caller(null, chosen), gateway, ReadOnly, ("start", "0"), ("length", "25")).List(CancellationToken.None);
        Assert.Equal(chosen.ToString("D"), Assert.Single(gateway.Requests).LegalEntity);
    }

    // ── writes ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Confirm_sends_the_key_and_If_Match_on_one_request_and_a_pending_lock_comes_back_localized()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/warehouse/putaway-tasks/{TaskId}/confirmations", 409,
            """{"error":{"code":"WMS_CONFIRMATION_PENDING","message":"pending","correlationId":"c-1"},"contractVersion":"v1"}""");
        var controller = Putaway(gateway, [WarehousePermissionKeys.PutawayTasksConfirm]);

        var result = Assert.IsType<JsonResult>(await controller.Confirm(TaskId,
            new PutawayWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"3\"", Quantity = "4,5", ToLocationId = Bin.ToUpperInvariant(), DeviationReason = " broken rack " }, CancellationToken.None));

        var call = Assert.Single(gateway.Requests);
        Assert.Equal("POST", call.Method);
        Assert.Equal(Key, call.IdempotencyKey);
        Assert.Equal("W/\"3\"", call.IfMatch);
        using var sent = JsonDocument.Parse(call.Body);
        Assert.Equal("4.5", sent.RootElement.GetProperty("quantity").GetString());
        Assert.Equal(Bin, sent.RootElement.GetProperty("toLocationId").GetString());
        Assert.Equal("Ui", sent.RootElement.GetProperty("channel").GetString());
        Assert.Equal("broken rack", sent.RootElement.GetProperty("deviationReason").GetString());
        Assert.Equal(409, result.StatusCode);
        var error = Body(result).GetProperty("error");
        Assert.Equal("WMS_CONFIRMATION_PENDING", error.GetProperty("code").GetString());
        Assert.Equal("Warehouse.Error.WMS_CONFIRMATION_PENDING", error.GetProperty("message").GetString());
        Assert.Equal("c-1", error.GetProperty("correlationId").GetString());
    }

    [Theory]
    [InlineData("""{"candidateKey":"x"}""")]
    [InlineData("""{"fromLocationId":"a3333333-0000-0000-0000-000000000003"}""")]
    [InlineData("""{"warehouseId":"a3333333-0000-0000-0000-000000000003"}""")]
    [InlineData("""{"targetSource":"SuggestionApplied"}""")]
    [InlineData("""{"plannedQuantity":"1e3"}""")]
    public async Task An_edit_never_forwards_the_source_or_an_off_contract_member(string changes)
    {
        var gateway = new FakeGateway();
        var result = Assert.IsType<JsonResult>(await Putaway(gateway, [WarehousePermissionKeys.PutawayTasksUpdate]).Edit(TaskId,
            new PutawayWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"3\"", Changes = JsonDocument.Parse(changes).RootElement }, CancellationToken.None));

        Assert.Equal(400, result.StatusCode);
        Assert.Empty(gateway.Requests);
    }

    [Fact]
    public async Task An_edit_is_one_PUT_with_only_the_changed_members_the_key_and_If_Match()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/warehouse/putaway-tasks/{TaskId}", 200, Task(), "W/\"4\"");
        var result = await Putaway(gateway, [WarehousePermissionKeys.PutawayTasksUpdate]).Edit(TaskId, new PutawayWriteRequest
        {
            IdempotencyKey = Key, IfMatch = "W/\"3\"",
            Changes = JsonDocument.Parse($$"""{"toLocationId":"{{Bin}}","targetSource":"SuggestionApplied","assigneeUserId":null}""").RootElement
        }, CancellationToken.None);

        var call = Assert.Single(gateway.Requests);
        Assert.Equal("PUT", call.Method);
        Assert.Equal((Key, "W/\"3\""), (call.IdempotencyKey, call.IfMatch));
        using var sent = JsonDocument.Parse(call.Body);
        Assert.Equal(["assigneeUserId", "idempotencyKey", "targetSource", "toLocationId"], sent.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(JsonValueKind.Null, sent.RootElement.GetProperty("assigneeUserId").ValueKind);
        Assert.Equal("SuggestionApplied", sent.RootElement.GetProperty("targetSource").GetString());
        Assert.Equal("W/\"4\"", Body(result).GetProperty("etag").GetString());
    }

    [Fact]
    public async Task AC41_the_candidate_list_keeps_a_locked_row_with_its_reason_and_blocking_document_number()
    {
        var gateway = new FakeGateway();
        gateway.On("/api/warehouse/putaway-candidates", 200, $$$"""
            {"items":[
              {"candidateKey":"k1","warehouseId":"{{{WarehouseId}}}","locationId":"a1","locationCode":"RCV-01","item":{"itemId":"e1","skuId":"f1","skuLevel":"Lsku"},"stockStatus":"AVAILABLE","onHand":"10","openTaskQuantity":"0","openQuantity":"10","baseUomId":"EA","eligible":true,"lotNumber":"L-1","storageConditions":[]},
              {"candidateKey":"k2","warehouseId":"{{{WarehouseId}}}","locationId":"a1","locationCode":"RCV-01","item":{"itemId":"e1","skuId":"f1","skuLevel":"Lsku"},"stockStatus":"AVAILABLE","onHand":"5","openTaskQuantity":"0","openQuantity":"5","baseUomId":"EA","eligible":false,"lotNumber":"L-2","notEligibleReason":"LotUnderRecall","blockingDocument":{"kind":"RecallCase","id":"r1","number":"MG-RCL-000007"}}
            ],"total":2,"page":1,"pageSize":200,"contractVersion":"v1"}
            """);

        var json = Body(await Putaway(gateway, [WarehousePermissionKeys.PutawayTasksCreate]).Candidates("rcv", null, CancellationToken.None));

        Assert.StartsWith("/api/warehouse/putaway-candidates?page=1&pageSize=200&q=rcv", gateway.Requests[0].PathAndQuery, StringComparison.Ordinal);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);                                                             // the locked row is not hidden
        var locked = items.Single(i => i.GetProperty("key").GetString() == "k2");
        Assert.False(locked.GetProperty("eligible").GetBoolean());
        Assert.Equal("LotUnderRecall", locked.GetProperty("reason").GetString());
        Assert.Equal(("RecallCase", "MG-RCL-000007"), (locked.GetProperty("blockingKind").GetString(), locked.GetProperty("blockingNumber").GetString()));

        // DL-3 CT F-2 (§11.3a, draft §3.3): an empty list is a KNOWN "no condition"; an absent member is UNKNOWN — the screen's badge
        var open = items.Single(i => i.GetProperty("key").GetString() == "k1");
        Assert.True(open.GetProperty("conditionsKnown").GetBoolean());
        Assert.Empty(open.GetProperty("storageConditions").EnumerateArray());
        Assert.False(locked.GetProperty("conditionsKnown").GetBoolean());
    }

    [Theory]
    [InlineData("A-01-02", "a2")]
    [InlineData(" a-01-02 ", "a2")]
    [InlineData("A-01", "a1")]
    [InlineData("A-0", null)]       // a "contains" hit is not a match
    [InlineData("B-99", null)]
    [InlineData("", null)]
    public void AC44_only_an_exact_bin_code_selects_a_bin(string code, string? expected)
    {
        var options = new[] { new PickerOption("a1", "A-01 · Aisle one"), new PickerOption("a2", "A-01-02"), new PickerOption("a3", "A-01-03 · Upper") };
        Assert.Equal(expected, PutawayTasksController.BinMatch(options, code)?.Value);
    }

    [Fact]
    public async Task Roles_writes_carry_their_key_and_If_Match_and_the_node_never_changes_on_edit()
    {
        var gateway = new FakeGateway();
        gateway.On($"/api/warehouse/location-roles/{TaskId}", 200, $$"""{"roleId":"{{TaskId}}","legalEntityId":"{{TokenCompanyId}}","warehouseId":"{{WarehouseId}}","locationId":"{{Bin}}","roles":[],"acceptedStorageConditions":["COOL_2_8"],"version":2,"contractVersion":"v1"}""", "W/\"2\"");
        var manage = new[] { WarehousePermissionKeys.LocationRolesRead, WarehousePermissionKeys.LocationRolesManage };

        var moved = await Roles(gateway, manage).Edit(TaskId, new LocationRoleWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"1\"", LocationId = Bin }, CancellationToken.None);
        Assert.Equal(400, Assert.IsType<JsonResult>(moved).StatusCode);
        var unknown = await Roles(gateway, manage).Edit(TaskId, new LocationRoleWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"1\"", Roles = ["Storage"] }, CancellationToken.None);
        Assert.Equal(400, Assert.IsType<JsonResult>(unknown).StatusCode);
        Assert.Empty(gateway.Requests);

        // §11.3a: no role, conditions only — a valid edit
        await Roles(gateway, manage).Edit(TaskId, new LocationRoleWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"1\"", Roles = [], AcceptedStorageConditions = ["COOL_2_8"] }, CancellationToken.None);
        await Roles(gateway, manage).Delete(TaskId, new LocationRoleWriteRequest { IdempotencyKey = Key, IfMatch = "W/\"2\"" }, CancellationToken.None);
        Assert.Equal(["PUT", "DELETE"], gateway.Requests.Select(r => r.Method));
        Assert.All(gateway.Requests, r => Assert.Equal(Key, r.IdempotencyKey));
        Assert.Equal(["W/\"1\"", "W/\"2\""], gateway.Requests.Select(r => r.IfMatch));
        Assert.All(gateway.Requests, r => Assert.Null(r.LegalEntity)); // the token names the company: no header, the service reads the claim
    }

    [Fact]
    public async Task An_unreachable_gateway_is_503_with_the_service_unavailable_sentence()
    {
        var gateway = new FakeGateway { Unreachable = true };
        var result = Assert.IsType<JsonResult>(await Putaway(gateway, ReadOnly, ("start", "0"), ("length", "25")).List(CancellationToken.None));
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("Warehouse.Error.ServiceUnavailable", Body(result).GetProperty("error").GetProperty("message").GetString());
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────

    private static JsonElement Body(IActionResult result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement;

    private sealed record Caller(Guid? TokenCompany, Guid? CookieCompany);

    private static PutawayTasksController Putaway(FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
        => PutawayAs(new Caller(TokenCompanyId, null), gateway, permissions, query);

    private static PutawayTasksController PutawayAs(Caller caller, FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
    {
        var controller = new PutawayTasksController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<PutawayTasksIndex>(), NullLogger<PutawayTasksController>.Instance);
        controller.ControllerContext = Context(caller, query);
        return controller;
    }

    private static WarehouseLocationRolesController Roles(FakeGateway gateway, string[] permissions, params (string Key, string Value)[] query)
    {
        var controller = new WarehouseLocationRolesController(new HttpClient(gateway), Configuration(), new Permissions(permissions),
            new KeyLocalizer<WarehouseLocationRolesIndex>(), NullLogger<WarehouseLocationRolesController>.Instance);
        controller.ControllerContext = Context(new Caller(TokenCompanyId, null), query);
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
        public bool Unreachable { get; init; }

        private List<(string Prefix, int Status, string Body, string? ETag)> Routes { get; } = [];

        public List<(string Method, string PathAndQuery, string? TenantHeader, string? IdempotencyKey, string? IfMatch, string Body, string? LegalEntity)> Requests { get; } = [];

        public void On(string prefix, int status, string body, string? etag = null) => Routes.Add((prefix, status, body, etag));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Unreachable) throw new HttpRequestException("down");
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            // the master-name reads (product-master, locations, users) are not the subject here: no route = 404, names fall back to ids
            if (pathAndQuery.StartsWith("/api/warehouse", StringComparison.Ordinal))
            {
                lock (Requests)
                {
                    Requests.Add((request.Method.Method, pathAndQuery,
                        request.Headers.TryGetValues("X-Tenant-Id", out var tenant) ? tenant.Single() : null,
                        request.Headers.TryGetValues("Idempotency-Key", out var key) ? key.Single() : null,
                        request.Headers.TryGetValues("If-Match", out var ifMatch) ? ifMatch.Single() : null,
                        body,
                        request.Headers.TryGetValues("X-Legal-Entity-Id", out var le) ? le.Single() : null));
                }
            }

            var route = Routes.Where(r => pathAndQuery.StartsWith(r.Prefix, StringComparison.Ordinal)).OrderByDescending(r => r.Prefix.Length).FirstOrDefault();
            if (route.Prefix is null) return new HttpResponseMessage(HttpStatusCode.NotFound);
            var response = new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") };
            if (route.ETag is not null) response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(route.ETag[2..], isWeak: true);
            return response;
        }
    }
}
