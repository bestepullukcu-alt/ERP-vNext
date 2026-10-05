using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// One isolated MOD-0174 world per test: a fresh tenant + company + user, a lot-controlled item with one Lsku (GTIN
/// <see cref="Gtin"/>) registered in both product stubs, and a token holding every <c>trace.*</c> key. Every call goes
/// through the contract-validating client, so each TRACE response is checked against <c>trace.openapi.yaml</c> 1.1.0.
/// </summary>
public sealed class TraceScenario
{
    public const string Gtin = "08699512340407";

    private readonly TraceApiFactory _factory;
    private readonly HttpClient _client;

    public TraceScenario(TraceApiFactory factory, bool lotControlled = true, bool serialControlled = false, string? gtin = Gtin)
    {
        _factory = factory;
        _client = factory.CreateContractClient();
        factory.TrackTraceTenant(Tenant);
        factory.Products.AddItem(Item, lotControlled, serialControlled);
        factory.Products.AddSku(Sku, SkuLevel.Lsku, Item);
        factory.Skus.Add(Sku, SkuLevel.Lsku, Item, gtin);
        Token = TestTokens.Mint(Tenant, LegalEntity, User, TestTokens.TracePermissions);
    }

    public Guid Tenant { get; } = Guid.NewGuid();
    public Guid LegalEntity { get; } = Guid.NewGuid();
    public Guid User { get; } = Guid.NewGuid();
    public Guid Item { get; } = Guid.NewGuid();
    public Guid Sku { get; } = Guid.NewGuid();
    public string Token { get; }
    public TraceApiFactory Factory => _factory;

    /// <summary>A second SKU (Gsku) of the SAME item — the K-G / B-1 case.</summary>
    public Guid AddSecondSku(string? gtin = "08699512340209")
    {
        var sku = Guid.NewGuid();
        _factory.Products.AddSku(sku, SkuLevel.Gsku, Item);
        _factory.Skus.Add(sku, SkuLevel.Gsku, Item, gtin);
        return sku;
    }

    public string TokenWith(params string[] permissions) => TestTokens.Mint(Tenant, LegalEntity, User, permissions);

    public string TokenForCompany(Guid legalEntity) => TestTokens.Mint(Tenant, legalEntity, User, TestTokens.TracePermissions);

    // ── bodies ───────────────────────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> LotBody(string lotNumber, Guid? sku = null, SkuLevel level = SkuLevel.Lsku, string? key = null) => new()
    {
        ["idempotencyKey"] = key ?? "lot-" + Guid.NewGuid().ToString("N"),
        ["itemId"] = Item.ToString("D"),
        ["lotNumber"] = lotNumber,
        ["skuId"] = (sku ?? Sku).ToString("D"),
        ["skuLevel"] = WireNames.ToWire(level)
    };

    public static string LotNo(string prefix = "L") => prefix + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // ── calls ────────────────────────────────────────────────────────────────────────────────────────────

    public Task<HttpResponseMessage> CreateAsync(Dictionary<string, object?> body, string? token = null, string? headerKey = null)
        => Send(HttpMethod.Post, "/api/trace/lots", body, r => r.Headers.TryAddWithoutValidation("Idempotency-Key", headerKey ?? (string?)body.GetValueOrDefault("idempotencyKey") ?? ""), token);

    public Task<HttpResponseMessage> PatchAsync(string lotId, object body, string? ifMatch, string? token = null)
        => Send(HttpMethod.Patch, $"/api/trace/lots/{lotId}", body, r => { if (ifMatch is not null) r.Headers.TryAddWithoutValidation("If-Match", ifMatch); }, token);

    public Task<HttpResponseMessage> StatusAsync(string lotId, string toStatus, string? reason, string? ifMatch, bool? ack = null,
        string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key ?? "st-" + Guid.NewGuid().ToString("N"),
            ["toStatus"] = toStatus,
            ["reason"] = reason
        };
        if (ack.HasValue) body["acknowledgeImpact"] = ack.Value;
        return Send(HttpMethod.Post, $"/api/trace/lots/{lotId}/status", body, r =>
        {
            if (ifMatch is not null) r.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            r.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!);
        }, token);
    }

    public Task<HttpResponseMessage> LinkAsync(string parentId, string childId, string reference, string? key = null, string? token = null,
        string sourceModule = "MOD-0194")
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key ?? "gl-" + Guid.NewGuid().ToString("N"),
            ["parentLotId"] = parentId,
            ["childLotId"] = childId,
            ["transformationReference"] = reference,
            ["sourceModule"] = sourceModule
        };
        return Send(HttpMethod.Post, $"/api/trace/lots/{childId}/genealogy", body,
            r => r.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!), token);
    }

    public Task<HttpResponseMessage> Gs1Async(string code, string lotId, Guid? sku = null, SkuLevel level = SkuLevel.Lsku)
        => Send(HttpMethod.Post, "/api/trace/serials/gs1-check", new Dictionary<string, object?>
        {
            ["code"] = code,
            ["skuId"] = (sku ?? Sku).ToString("D"),
            ["skuLevel"] = WireNames.ToWire(level),
            ["lotId"] = lotId
        });

    public Task<HttpResponseMessage> GetAsync(string pathAndQuery, string? token = null) => Send(HttpMethod.Get, pathAndQuery, null, null, token);

    public Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, Action<HttpRequestMessage>? configure = null, string? token = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        var effective = token ?? Token;
        if (effective.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effective);
        configure?.Invoke(request);
        return _client.SendAsync(request);
    }

    // ── conveniences ─────────────────────────────────────────────────────────────────────────────────────

    public async Task<(string Id, string ETag, JsonElement Json)> CreateLotAsync(string? lotNumber = null, Guid? sku = null, SkuLevel level = SkuLevel.Lsku,
        Dictionary<string, object?>? extra = null)
    {
        var body = LotBody(lotNumber ?? LotNo(), sku, level);
        if (extra is not null) foreach (var (k, v) in extra) body[k] = v;
        using var response = await CreateAsync(body);
        var json = await Read(response);
        Assert.True((int)response.StatusCode == 201, $"create lot: {(int)response.StatusCode} {json}");
        return (json.GetProperty("lotId").GetString()!, response.Headers.ETag!.ToString(), json);
    }

    public async Task<string> ETagOf(string lotId)
    {
        using var response = await GetAsync($"/api/trace/lots/{lotId}");
        Assert.Equal(200, (int)response.StatusCode);
        return response.Headers.ETag!.ToString();
    }

    public async Task<JsonElement> Expect(Task<HttpResponseMessage> call, int status)
    {
        using var response = await call;
        var json = await Read(response);
        Assert.True((int)response.StatusCode == status, $"expected {status}, got {(int)response.StatusCode}: {json}");
        return json;
    }

    public async Task<string> ExpectError(Task<HttpResponseMessage> call, int status, string? code)
    {
        var json = await Expect(call, status);
        var error = json.GetProperty("error");
        Assert.Equal(code, error.TryGetProperty("code", out var c) ? c.GetString() : null);
        return error.GetProperty("message").GetString()!;
    }

    public async Task<List<JsonElement>> History(string lotId)
        => (await Expect(GetAsync($"/api/trace/lots/{lotId}/history?pageSize=200"), 200)).GetProperty("items").EnumerateArray().ToList();

    public List<BsonDocument> Persisted(string collection)
        => _factory.Collection(collection).Find(new BsonDocument("TenantId", Tenant.ToString())).ToList();

    public static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
}
