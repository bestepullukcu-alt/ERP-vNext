using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.OutboundShipments;
using Diten.Web.Views.Warehouse.Packing;
using Diten.Web.Views.Warehouse.PickTasks;
using Diten.Web.Views.Warehouse.PutawayTasks;
using Diten.Web.Views.Warehouse.WarehouseLocationRoles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-3 CT F-1 — the key of EVERY action of the two warehouse proxies, collected by reflection (not a typed list): each HTTP action except
/// the page itself (Index — UAS-001 is the view's, WarehouseScreensSurfaceTests) must be in <see cref="Keys"/>; a new action without an entry is red.
/// Each action is then called with every warehouse key EXCEPT its own → 403 with the localized refusal and no Gateway call; and with ONLY its own key →
/// the key check is passed (never the 403 refusal). Display / proxy decisions — InventoryService's [HasPermission] stays the authority (UAS-001 §4).
/// <para>DL-6 (AC-34 / AC-73, sabotage (cv)): the three S2 proxies (OutboundShipments, PickTasks, Packing) are in the same reflection — every data action
/// of theirs is in the map; the pages themselves (Index, CreatePage, EditPage, DetailsPage) are the views' UAS-001 (WarehouseS2ScreensTests).</para>
/// </summary>
public sealed class WarehouseProxyActionKeyTests
{
    private const string Id = "b1111111-0000-0000-0000-000000000001";
    private const string Key = "d5555555-0000-4000-8000-000000000005";

    private static readonly string[] All =
    [
        WarehousePermissionKeys.PutawayTasksRead, WarehousePermissionKeys.PutawayTasksCreate, WarehousePermissionKeys.PutawayTasksUpdate,
        WarehousePermissionKeys.PutawayTasksConfirm, WarehousePermissionKeys.PutawayTasksCancel, WarehousePermissionKeys.PutawayTasksResolve,
        WarehousePermissionKeys.LocationRolesRead, WarehousePermissionKeys.LocationRolesManage,
        WarehousePermissionKeys.OutboundShipmentsRead, WarehousePermissionKeys.OutboundShipmentsCreate, WarehousePermissionKeys.OutboundShipmentsUpdate,
        WarehousePermissionKeys.OutboundShipmentsCancel, WarehousePermissionKeys.OutboundShipmentsRelease, WarehousePermissionKeys.PickTasksRead,
        WarehousePermissionKeys.PickTasksUpdate, WarehousePermissionKeys.PickTasksConfirm, WarehousePermissionKeys.PickTasksResolve,
        WarehousePermissionKeys.PackagesRead, WarehousePermissionKeys.PackagesCreate, WarehousePermissionKeys.PackagesUpdate
    ];

    /// <summary>The key each proxy action needs (pack §11.3, §14.2) — "Controller.Action" → key.</summary>
    private static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PutawayTasks.List"] = WarehousePermissionKeys.PutawayTasksRead,
        ["PutawayTasks.Export"] = WarehousePermissionKeys.PutawayTasksRead,
        ["PutawayTasks.Detail"] = WarehousePermissionKeys.PutawayTasksRead,
        ["PutawayTasks.Candidates"] = WarehousePermissionKeys.PutawayTasksCreate,
        ["PutawayTasks.Warehouses"] = WarehousePermissionKeys.PutawayTasksRead,
        ["PutawayTasks.Bins"] = WarehousePermissionKeys.PutawayTasksRead,
        ["PutawayTasks.People"] = WarehousePermissionKeys.PutawayTasksRead,
        ["PutawayTasks.Create"] = WarehousePermissionKeys.PutawayTasksCreate,
        ["PutawayTasks.Edit"] = WarehousePermissionKeys.PutawayTasksUpdate,
        ["PutawayTasks.Confirm"] = WarehousePermissionKeys.PutawayTasksConfirm,
        ["PutawayTasks.Retry"] = WarehousePermissionKeys.PutawayTasksConfirm,
        ["PutawayTasks.Claim"] = WarehousePermissionKeys.PutawayTasksConfirm,
        ["PutawayTasks.Returned"] = WarehousePermissionKeys.PutawayTasksConfirm,
        ["PutawayTasks.Resolve"] = WarehousePermissionKeys.PutawayTasksResolve,
        ["PutawayTasks.CloseShort"] = WarehousePermissionKeys.PutawayTasksConfirm,
        ["PutawayTasks.Cancel"] = WarehousePermissionKeys.PutawayTasksCancel,
        ["PutawayTasks.BulkAssign"] = WarehousePermissionKeys.PutawayTasksUpdate,
        ["WarehouseLocationRoles.List"] = WarehousePermissionKeys.LocationRolesRead,
        ["WarehouseLocationRoles.Export"] = WarehousePermissionKeys.LocationRolesRead,
        ["WarehouseLocationRoles.Role"] = WarehousePermissionKeys.LocationRolesRead,
        ["WarehouseLocationRoles.Warehouses"] = WarehousePermissionKeys.LocationRolesRead,
        ["WarehouseLocationRoles.Nodes"] = WarehousePermissionKeys.LocationRolesManage,
        ["WarehouseLocationRoles.Create"] = WarehousePermissionKeys.LocationRolesManage,
        ["WarehouseLocationRoles.Edit"] = WarehousePermissionKeys.LocationRolesManage,
        ["WarehouseLocationRoles.Delete"] = WarehousePermissionKeys.LocationRolesManage,
        ["WarehouseLocationRoles.BulkDelete"] = WarehousePermissionKeys.LocationRolesManage,
        // DL-6 — S2 (pack §11.3, §14.2)
        ["OutboundShipments.List"] = WarehousePermissionKeys.OutboundShipmentsRead,
        ["OutboundShipments.Export"] = WarehousePermissionKeys.OutboundShipmentsRead,
        ["OutboundShipments.Outbound"] = WarehousePermissionKeys.OutboundShipmentsRead,
        ["OutboundShipments.Warehouses"] = WarehousePermissionKeys.OutboundShipmentsRead,
        ["OutboundShipments.Stagings"] = WarehousePermissionKeys.OutboundShipmentsCreate,
        ["OutboundShipments.Products"] = WarehousePermissionKeys.OutboundShipmentsCreate,
        ["OutboundShipments.Skus"] = WarehousePermissionKeys.OutboundShipmentsCreate,
        ["OutboundShipments.SkuFacts"] = WarehousePermissionKeys.OutboundShipmentsCreate,
        ["OutboundShipments.Preview"] = WarehousePermissionKeys.OutboundShipmentsCreate,
        ["OutboundShipments.Create"] = WarehousePermissionKeys.OutboundShipmentsCreate,
        ["OutboundShipments.Edit"] = WarehousePermissionKeys.OutboundShipmentsUpdate,
        ["OutboundShipments.Release"] = WarehousePermissionKeys.OutboundShipmentsRelease,
        ["OutboundShipments.Sync"] = WarehousePermissionKeys.OutboundShipmentsUpdate,
        ["OutboundShipments.Cancel"] = WarehousePermissionKeys.OutboundShipmentsCancel,
        ["PickTasks.List"] = WarehousePermissionKeys.PickTasksRead,
        ["PickTasks.Export"] = WarehousePermissionKeys.PickTasksRead,
        ["PickTasks.Detail"] = WarehousePermissionKeys.PickTasksRead,
        ["PickTasks.Warehouses"] = WarehousePermissionKeys.PickTasksRead,
        ["PickTasks.People"] = WarehousePermissionKeys.PickTasksRead,
        ["PickTasks.Confirm"] = WarehousePermissionKeys.PickTasksConfirm,
        ["PickTasks.Retry"] = WarehousePermissionKeys.PickTasksConfirm,
        ["PickTasks.Claim"] = WarehousePermissionKeys.PickTasksConfirm,
        ["PickTasks.Returned"] = WarehousePermissionKeys.PickTasksConfirm,
        ["PickTasks.Resolve"] = WarehousePermissionKeys.PickTasksResolve,
        ["PickTasks.CloseShort"] = WarehousePermissionKeys.PickTasksConfirm,
        ["PickTasks.BulkAssign"] = WarehousePermissionKeys.PickTasksUpdate,
        ["Packing.Packages"] = WarehousePermissionKeys.PackagesRead,
        ["Packing.Create"] = WarehousePermissionKeys.PackagesCreate,
        ["Packing.Edit"] = WarehousePermissionKeys.PackagesUpdate,
        ["Packing.Close"] = WarehousePermissionKeys.PackagesUpdate
    };

    /// <summary>The page actions: each view draws its own UAS-001 (no data, no Gateway call from the action).</summary>
    private static readonly string[] Pages = ["Index", "CreatePage", "EditPage", "DetailsPage"];

    private static readonly Type[] Controllers =
    [
        typeof(PutawayTasksController), typeof(WarehouseLocationRolesController), typeof(OutboundShipmentsController), typeof(PickTasksController),
        typeof(PackingController)
    ];

    /// <summary>Every HTTP action of the two controllers but the page itself.</summary>
    private static IEnumerable<MethodInfo> Actions() => Controllers
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any() && !Pages.Contains(m.Name));

    private static string Name(MethodInfo m) => $"{m.DeclaringType!.Name.Replace("Controller", string.Empty)}.{m.Name}";

    public static TheoryData<string> ActionNames()
    {
        var data = new TheoryData<string>();
        foreach (var m in Actions()) data.Add(Name(m));
        return data;
    }

    [Fact]
    public void Every_proxy_action_is_in_the_key_map_and_the_map_names_no_action_that_is_gone()
    {
        var reflected = Actions().Select(Name).Order(StringComparer.Ordinal).ToList();
        Assert.True(reflected.Count >= 56, $"only {reflected.Count} actions reflected");
        Assert.Equal(Keys.Keys.Order(StringComparer.Ordinal), reflected);
        Assert.All(Controllers, t => Assert.NotNull(t.GetMethod(nameof(PutawayTasksController.Index))));
    }

    [Theory]
    [MemberData(nameof(ActionNames))]
    public async Task Every_proxy_action_without_its_own_key_is_403_and_never_reaches_the_gateway(string action)
    {
        Assert.True(Keys.TryGetValue(action, out var key), $"{action} has no key entry — every proxy action needs one");
        var gateway = new FakeGateway();

        var result = await InvokeAsync(action, All.Where(k => k != key).ToArray(), gateway);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(403, json.StatusCode);
        Assert.Equal("Warehouse.Error.NoPermission", Body(json).GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(gateway.Requests);
    }

    [Theory]
    [MemberData(nameof(ActionNames))]
    public async Task Every_proxy_action_passes_the_key_check_with_only_its_own_key(string action)
    {
        var gateway = new FakeGateway();

        var result = await InvokeAsync(action, [Keys[action]], gateway);

        var refused = result is JsonResult { StatusCode: 403 } json
                      && Body(json).TryGetProperty("error", out var error) && error.GetProperty("message").GetString() == "Warehouse.Error.NoPermission";
        Assert.False(refused, $"{action} refuses its own key {Keys[action]}");
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<IActionResult> InvokeAsync(string action, string[] permissions, FakeGateway gateway)
    {
        var method = Actions().Single(m => Name(m) == action);
        var http = new HttpClient(gateway);
        var keys = new Permissions(permissions);
        Controller controller = method.DeclaringType!.Name switch
        {
            nameof(PutawayTasksController) => new PutawayTasksController(http, Configuration(), keys, new KeyLocalizer<PutawayTasksIndex>(), NullLogger<PutawayTasksController>.Instance),
            nameof(WarehouseLocationRolesController) => new WarehouseLocationRolesController(http, Configuration(), keys, new KeyLocalizer<WarehouseLocationRolesIndex>(), NullLogger<WarehouseLocationRolesController>.Instance),
            nameof(OutboundShipmentsController) => new OutboundShipmentsController(http, Configuration(), keys, new KeyLocalizer<OutboundShipmentsIndex>(), NullLogger<OutboundShipmentsController>.Instance),
            nameof(PickTasksController) => new PickTasksController(http, Configuration(), keys, new KeyLocalizer<PickTasksIndex>(), NullLogger<PickTasksController>.Instance),
            _ => new PackingController(http, Configuration(), keys, new KeyLocalizer<PackingIndex>(), NullLogger<PackingController>.Instance)
        };
        controller.ControllerContext = Context();
        var args = method.GetParameters().Select(Argument).ToArray();
        return await (Task<IActionResult>)method.Invoke(controller, args)!;
    }

    /// <summary>A well-formed argument for every parameter, so only the key decides the refusal.</summary>
    private static object? Argument(ParameterInfo p) => p.ParameterType switch
    {
        var t when t == typeof(CancellationToken) => CancellationToken.None,
        var t when t == typeof(int) => 1,
        var t when t == typeof(string) => p.Name is "id" or "warehouseId" or "outboundId" or "packageId" or "itemId" or "skuId" ? Id : p.Name == "skuLevel" ? "Lsku" : null,
        var t when t == typeof(OutboundWriteRequest) => new OutboundWriteRequest
        {
            IdempotencyKey = Key, IfMatch = "W/\"1\"", WarehouseId = Id, Reason = "a reason of ten",
            ShipTo = new OutboundAddressRequest { Name = "Recipient", Country = "TR" },
            Lines = [new OutboundLineRequest { OrderLineId = "1", ItemId = Id, SkuId = Id, SkuLevel = "Lsku", UomId = "EA", Quantity = "1" }],
            Changes = JsonDocument.Parse("""{"note":"x"}""").RootElement
        },
        var t when t == typeof(PickWriteRequest) => new PickWriteRequest
        {
            IdempotencyKey = Key, IfMatch = "W/\"1\"", Quantity = "1", Reason = "a reason of ten", AssigneeUserId = Id, Items = [new WriteItem { Id = Id, Version = 1 }]
        },
        var t when t == typeof(PackageWriteRequest) => new PackageWriteRequest
        {
            IdempotencyKey = Key, IfMatch = "W/\"1\"", Contents = [new PackageContentRequest { AllocSeq = 1, Quantity = "1" }]
        },
        var t when t == typeof(PutawayWriteRequest) => new PutawayWriteRequest
        {
            IdempotencyKey = Key, IfMatch = "W/\"1\"", CandidateKey = "k", PlannedQuantity = "1", Quantity = "1", ToLocationId = Id, AssigneeUserId = Id,
            Reason = "a reason of ten", Items = [new WriteItem { Id = Id, Version = 1 }],
            Changes = JsonDocument.Parse("""{"priority":"High"}""").RootElement
        },
        var t when t == typeof(LocationRoleWriteRequest) => new LocationRoleWriteRequest
        {
            IdempotencyKey = Key, IfMatch = "W/\"1\"", Roles = ["Receiving"], AcceptedStorageConditions = [], Items = [new WriteItem { Id = Id, Version = 1 }]
        },
        _ => throw new InvalidOperationException($"no test argument for {p.Name} ({p.ParameterType.Name}) — extend Argument()")
    };

    private static JsonElement Body(JsonResult result) =>
        JsonDocument.Parse(JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))).RootElement;

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();

    private static ControllerContext Context()
    {
        var claims = new List<Claim>
        {
            new("tenantId", "66666666-6666-6666-6666-666666666666"), new("sub", "55555555-0000-0000-0000-000000000001"),
            new("legal_entity_id", "77777777-7777-7777-7777-777777777777")
        };
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
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

    /// <summary>Records every request and answers 404 (no route): what reaches the Gateway is the subject, not the answer.</summary>
    private sealed class FakeGateway : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Requests) Requests.Add($"{request.Method} {request.RequestUri!.PathAndQuery}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
