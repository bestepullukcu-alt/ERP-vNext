using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Infrastructure.Upstream;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// WP-0173-PM1 — PRODUCT-MASTER v1.1 at the HTTP edge. The REAL <see cref="ProductMasterHttpClient"/> (the parsing is part of what
/// is measured) talks to an in-memory server that answers in the v1.1 wire shapes (`product-master-bundle.openapi.yaml`
/// 1.1.0-draft.8): <c>validateRefs</c> rows with <c>lifecycleStatus</c> / <c>itemKind</c>, <c>getSku</c> with the SKU-level
/// <c>lotControlled</c> / <c>serialControlled</c> (<c>null</c> = written as JSON null), <c>getProduct</c> with the deprecated
/// product flags. Every field can be set to any JSON value, including values outside the enums (open-enum rule) and
/// <see cref="Absent"/> (the member is not written at all).
/// </summary>
public sealed class FakeProductMaster
{
    /// <summary>Marks a member that is not written at all (different from JSON null).</summary>
    public static readonly object Absent = new();

    private readonly ConcurrentDictionary<Guid, Dictionary<string, object?>> _products = new();
    private readonly ConcurrentDictionary<Guid, Dictionary<string, object?>> _skus = new();
    private readonly ConcurrentDictionary<Guid, bool> _down = new();

    /// <summary>How many requests reached the server, per operation (for the "calls per movement" measurement).</summary>
    public ConcurrentDictionary<string, int> Calls { get; } = new();

    /// <summary>A Global Product (or, with <paramref name="itemKind"/> "Item", a material item) — the getProduct fields.</summary>
    public void Product(Guid itemId, string lifecycle = "Active", object? lotControlled = null, object? serialControlled = null, string itemKind = "GlobalProduct")
        => _products[itemId] = new Dictionary<string, object?>
        {
            ["itemId"] = itemId.ToString(), ["itemKind"] = itemKind, ["canonicalCode"] = "GP-" + itemId.ToString("N")[..12],
            ["name"] = "PM1 product", ["lifecycleStatus"] = lifecycle,
            ["lotControlled"] = lotControlled ?? Absent, ["serialControlled"] = serialControlled ?? Absent
        };

    /// <summary>A SKU at <paramref name="level"/> — the getSku fields; <c>null</c> flags are written as JSON null.</summary>
    public void Sku(Guid skuId, string level, Guid itemId, object? lotControlled, object? serialControlled, string lifecycle = "Active", string baseUomId = "EA")
        => _skus[skuId] = new Dictionary<string, object?>
        {
            ["skuId"] = skuId.ToString(), ["skuLevel"] = level, ["itemId"] = itemId.ToString(),
            ["itemKind"] = level == "Item" ? "Item" : "GlobalProduct", ["canonicalCode"] = "LS-" + skuId.ToString("N")[..12],
            ["name"] = "PM1 sku", ["lifecycleStatus"] = lifecycle, ["baseUomId"] = baseUomId,
            ["lotControlled"] = lotControlled, ["serialControlled"] = serialControlled, ["stockable"] = true
        };

    /// <summary>A material item: the product AND its own Item-level SKU (<c>skuId == itemId</c>).</summary>
    public void MaterialItem(Guid itemId, object? lotControlled, string lifecycle = "Active", object? serialControlled = null)
    {
        Product(itemId, lifecycle, lotControlled, serialControlled ?? false, "Item");
        Sku(itemId, "Item", itemId, lotControlled, serialControlled ?? false, lifecycle);
    }

    public void SetProduct(Guid itemId, string member, object? value) => _products[itemId][member] = value;

    public void SetSku(Guid skuId, string member, object? value) => _skus[skuId][member] = value;

    /// <summary>Product AND its SKUs move to <paramref name="lifecycle"/> together (the item-level decision).</summary>
    public void SetLifecycle(Guid itemId, string lifecycle)
    {
        if (_products.TryGetValue(itemId, out var p)) p["lifecycleStatus"] = lifecycle;
        foreach (var sku in _skus.Values.Where(s => (string?)s["itemId"] == itemId.ToString())) sku["lifecycleStatus"] = lifecycle;
    }

    public void MarkUnavailable(params Guid[] ids)
    {
        foreach (var id in ids) _down[id] = true;
    }

    public int CallsTo(string operation) => Calls.TryGetValue(operation, out var n) ? n : 0;

    internal HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        var segments = path.Split('/');
        if (request.Method == HttpMethod.Post && path == "validate")
        {
            Count("validateRefs");
            var body = JsonNode.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())!;
            var results = new JsonArray();
            foreach (var r in body["refs"]!.AsArray())
            {
                var itemId = Guid.TryParse((string?)r!["itemId"], out var i) ? i : (Guid?)null;
                var skuId = Guid.TryParse((string?)r["skuId"], out var s) ? s : (Guid?)null;
                if (Down(itemId) || Down(skuId)) return Unavailable();
                var level = (string?)r["skuLevel"];
                var row = new JsonObject();
                if (skuId is null && itemId is { } item)
                {
                    row["itemId"] = item.ToString();
                    var found = _products.TryGetValue(item, out var p);
                    row["exists"] = found;
                    if (found) { Copy(p!, row, "itemKind"); Copy(p!, row, "lifecycleStatus"); }
                }
                else
                {
                    row["skuId"] = skuId.ToString();
                    if (level is not null) row["skuLevel"] = level;
                    var found = skuId is { } sid && _skus.TryGetValue(sid, out var sk) && (level is null || (string?)sk["skuLevel"] == level);
                    row["exists"] = found;
                    if (found) { var sk2 = _skus[skuId!.Value]; Copy(sk2, row, "itemKind"); Copy(sk2, row, "lifecycleStatus"); if (level is null) Copy(sk2, row, "skuLevel"); }
                }

                results.Add(row);
            }

            return Json(new JsonObject { ["results"] = results, ["contractVersion"] = "v1.1" });
        }

        if (request.Method == HttpMethod.Get && segments is ["products", var pid] && Guid.TryParse(pid, out var productId))
        {
            Count("getProduct");
            if (Down(productId)) return Unavailable();
            return _products.TryGetValue(productId, out var p) ? Json(Node(p)) : NotFound("UNKNOWN_ITEM");
        }

        if (request.Method == HttpMethod.Get && segments is ["skus", var sid2, "uom"] && Guid.TryParse(sid2, out var uomSku))
        {
            Count("getSkuUom");
            if (Down(uomSku)) return Unavailable();
            return _skus.TryGetValue(uomSku, out var sk)
                ? Json(new JsonObject { ["baseUomId"] = (string?)sk["baseUomId"], ["conversions"] = new JsonArray(), ["contractVersion"] = "v1.1" })
                : NotFound("UNKNOWN_SKU");
        }

        if (request.Method == HttpMethod.Get && segments is ["skus", var sid3] && Guid.TryParse(sid3, out var skuIdGet))
        {
            Count("getSku");
            if (Down(skuIdGet)) return Unavailable();
            var level = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["level"];
            return _skus.TryGetValue(skuIdGet, out var sk) && (level is null || (string?)sk["skuLevel"] == level)
                ? Json(Node(sk))
                : NotFound("UNKNOWN_SKU");
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private void Count(string operation) => Calls.AddOrUpdate(operation, 1, (_, n) => n + 1);

    private bool Down(Guid? id) => id is { } v && _down.ContainsKey(v);

    private static void Copy(Dictionary<string, object?> from, JsonObject to, string member)
    {
        if (from.TryGetValue(member, out var v) && !ReferenceEquals(v, Absent)) to[member] = JsonValue(v);
    }

    private static JsonObject Node(Dictionary<string, object?> fields)
    {
        var node = new JsonObject();
        foreach (var (k, v) in fields)
        {
            if (!ReferenceEquals(v, Absent)) node[k] = JsonValue(v);
        }

        node["contractVersion"] = "v1.1";
        return node;
    }

    private static JsonNode? JsonValue(object? v) => v switch
    {
        null => null,
        bool b => System.Text.Json.Nodes.JsonValue.Create(b),
        string s => System.Text.Json.Nodes.JsonValue.Create(s),
        JsonNode n => n.DeepClone(),
        _ => System.Text.Json.Nodes.JsonValue.Create(v.ToString())
    };

    private static HttpResponseMessage Json(JsonNode node)
        => new(HttpStatusCode.OK) { Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage NotFound(string code)
        => new(HttpStatusCode.NotFound)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { error = new { code, message = "not found" }, contractVersion = "v1.1" }), Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage Unavailable() => new(HttpStatusCode.ServiceUnavailable);
}

/// <summary>The HTTP edge of <see cref="FakeProductMaster"/>.</summary>
public sealed class FakeProductMasterHandler(FakeProductMaster master) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(master.Answer(request));
}

/// <summary>
/// The real host with the REAL <see cref="ProductMasterHttpClient"/> over <see cref="FakeProductMaster"/> (v1.1 wire shapes) in
/// place of the in-process stub — the only difference from <see cref="InventoryApiFactory"/>.
/// </summary>
public sealed class ProductMasterV11ApiFactory : InventoryApiFactory
{
    public FakeProductMaster Master { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IProductMasterClient>(_ =>
            new ProductMasterHttpClient(new HttpClient(new FakeProductMasterHandler(Master)) { BaseAddress = new Uri("http://product-master.test/") })));
    }
}
