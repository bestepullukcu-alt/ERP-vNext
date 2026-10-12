using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// Calls the REAL LOCATION API (v1 + v1.1) of a scenario's company through the contract-validating client. The token
/// carries the ledger permissions plus <c>inventory.locations.read</c> / <c>.manage</c> unless the test passes another.
/// Every write sends a fresh Idempotency-Key (header = body) unless the body carries one.
/// </summary>
public sealed class LocationApi
{
    private readonly InventoryScenario _s;

    public LocationApi(InventoryScenario scenario, string? token = null)
    {
        _s = scenario;
        Token = token ?? TestTokens.Mint(scenario.Tenant, scenario.LegalEntity, scenario.User,
            [.. TestTokens.AllPermissions, .. TestTokens.LocationPermissions]);
    }

    public string Token { get; }

    public InventoryScenario Scenario => _s;

    // ── raw calls ────────────────────────────────────────────────────────────────────────────────────────

    public Task<HttpResponseMessage> CreateAsync(Dictionary<string, object?> body, Action<HttpRequestMessage>? configure = null)
    {
        body.TryAdd("idempotencyKey", "loc-" + Guid.NewGuid().ToString("N"));
        return Send(HttpMethod.Post, "/api/locations", body, r =>
        {
            r.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!);
            configure?.Invoke(r);
        });
    }

    public Task<HttpResponseMessage> PatchAsync(string id, object body, string? ifMatch, Action<HttpRequestMessage>? configure = null)
        => Send(HttpMethod.Patch, $"/api/locations/{id}", body, r =>
        {
            if (ifMatch is not null) r.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            configure?.Invoke(r);
        });

    public Task<HttpResponseMessage> StatusAsync(string id, string status, string? ifMatch, Action<HttpRequestMessage>? configure = null)
        => Send(HttpMethod.Post, $"/api/locations/{id}/status", new Dictionary<string, object?> { ["status"] = status }, r =>
        {
            if (ifMatch is not null) r.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            configure?.Invoke(r);
        });

    public Task<HttpResponseMessage> BulkAsync(string id, Dictionary<string, object?> body, string? headerKey = null)
    {
        body.TryAdd("idempotencyKey", "bulk-" + Guid.NewGuid().ToString("N"));
        return Send(HttpMethod.Post, $"/api/locations/{id}/bins/bulk", body,
            r => r.Headers.TryAddWithoutValidation("Idempotency-Key", headerKey ?? (string)body["idempotencyKey"]!));
    }

    public Task<HttpResponseMessage> GetAsync(string pathAndQuery, string? token = null, Action<HttpRequestMessage>? configure = null)
        => Send(HttpMethod.Get, pathAndQuery, null, configure, token);

    public Task<HttpResponseMessage> ValidateAsync(params string[] ids)
        => Send(HttpMethod.Post, "/api/locations/validate", new Dictionary<string, object?> { ["locationIds"] = ids });

    public Task<HttpResponseMessage> Send(
        HttpMethod method, string pathAndQuery, object? body, Action<HttpRequestMessage>? configure = null, string? token = null)
    {
        var request = new HttpRequestMessage(method, pathAndQuery);
        if (body is not null) request.Content = JsonContent.Create(body);
        var effective = token ?? Token;
        if (effective.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effective);
        configure?.Invoke(request);
        return _s.SendAsync(request);
    }

    // ── conveniences that assert the status ──────────────────────────────────────────────────────────────

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
        var actual = error.TryGetProperty("code", out var c) ? c.GetString() : null;
        Assert.Equal(code, actual);
        return error.GetProperty("message").GetString()!;
    }

    /// <summary>Creates a node and returns (id, ETag).</summary>
    public async Task<(string Id, string ETag)> Node(string type, string? parentId, string? code = null, Dictionary<string, object?>? extra = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["code"] = code ?? Code(type),
            ["name"] = type + " " + Guid.NewGuid().ToString("N")[..4],
            ["type"] = type
        };
        if (parentId is not null) body["parentId"] = parentId;
        if (extra is not null) foreach (var (k, v) in extra) body[k] = v;

        using var response = await CreateAsync(body);
        var json = await Read(response);
        Assert.True((int)response.StatusCode == 201, $"create {type}: {(int)response.StatusCode} {json}");
        return (json.GetProperty("locationId").GetString()!, response.Headers.ETag!.ToString());
    }

    /// <summary>Site → Warehouse → StorageLocation → Zone → Bin, all created through the API.</summary>
    public async Task<Tree> TreeAsync()
    {
        var site = (await Node("Site", null)).Id;
        var warehouse = (await Node("Warehouse", site)).Id;
        var storage = (await Node("StorageLocation", warehouse)).Id;
        var zone = (await Node("Zone", storage)).Id;
        var bin = (await Node("Bin", zone)).Id;
        return new Tree(site, warehouse, storage, zone, bin);
    }

    public async Task<string> ETagOf(string id)
    {
        using var response = await GetAsync($"/api/locations/{id}");
        Assert.Equal(200, (int)response.StatusCode);
        return response.Headers.ETag!.ToString();
    }

    public async Task<JsonElement> Get(string id) => await Expect(GetAsync($"/api/locations/{id}"), 200);

    public async Task<List<JsonElement>> History(string id)
        => (await Expect(GetAsync($"/api/locations/{id}/history?pageSize=200"), 200)).GetProperty("items").EnumerateArray().ToList();

    public async Task<(bool HasStock, bool SubtreeHasStock)> Presence(string id)
    {
        var json = await Expect(GetAsync($"/api/locations/{id}/stock-presence"), 200);
        return (json.GetProperty("hasStock").GetBoolean(), json.GetProperty("subtreeHasStock").GetBoolean());
    }

    public static string Code(string prefix) => prefix[..Math.Min(3, prefix.Length)].ToUpperInvariant() + "-" + Guid.NewGuid().ToString("N")[..10];

    public static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public sealed record Tree(string Site, string Warehouse, string Storage, string Zone, string Bin);
}
