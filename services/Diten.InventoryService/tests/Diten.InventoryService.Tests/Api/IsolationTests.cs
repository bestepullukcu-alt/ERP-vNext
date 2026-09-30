using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// AC10 / pack §13.5 — on EVERY endpoint. GUARDS UNDER TEST: the Tenant + LE entry filter of every repository query
/// (InventoryLedgerRepository.TransactionScope / BalanceScope) and InventoryScopeResolutionMiddleware. Sabotage
/// target of D4.
/// </summary>
public sealed class IsolationTests : IClassFixture<InventoryApiFactory>
{
    public static readonly TheoryData<string> ReadEndpoints = new()
    {
        "/api/inventory/availability?itemId={item}",
        "/api/inventory/balance?itemId={item}",
        "/api/inventory/transactions?itemId={item}"
    };

    private static readonly string[] ReadUrls =
    [
        "/api/inventory/availability?itemId={item}",
        "/api/inventory/balance?itemId={item}",
        "/api/inventory/transactions?itemId={item}"
    ];

    private readonly InventoryApiFactory _factory;

    public IsolationTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Tenant_A_stock_is_invisible_to_tenant_B_on_every_read()
    {
        var a = new InventoryScenario(_factory);
        await a.PostExpectingAsync(a.Receipt("100"), 201);
        var b = new InventoryScenario(_factory);

        using var availability = await b.GetAsync($"/api/inventory/availability?itemId={a.Item}");
        Assert.Equal(404, (int)availability.StatusCode);

        var balance = await b.ReadAsync(await b.GetAsync($"/api/inventory/balance?itemId={a.Item}"));
        Assert.Empty(balance.GetProperty("rows").EnumerateArray());
        Assert.Equal(0, balance.GetProperty("total").GetInt32());

        var unfiltered = await b.ReadAsync(await b.GetAsync("/api/inventory/balance"));
        Assert.Empty(unfiltered.GetProperty("rows").EnumerateArray());

        var history = await b.ReadAsync(await b.GetAsync("/api/inventory/transactions"));
        Assert.Empty(history.GetProperty("items").EnumerateArray());

        // Control: tenant A sees its own stock through the same endpoints.
        Assert.Equal(100m, await a.OnHandAsync());
        var own = await a.ReadAsync(await a.GetAsync("/api/inventory/transactions"));
        Assert.Single(own.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Same_tenant_other_legal_entity_is_isolated_too()
    {
        var a = new InventoryScenario(_factory);
        await a.PostExpectingAsync(a.Receipt("100"), 201);
        var otherLe = TestTokens.Mint(a.Tenant, Guid.NewGuid(), a.User, TestTokens.AllPermissions);

        var balance = await a.ReadAsync(await a.GetAsync($"/api/inventory/balance?itemId={a.Item}", otherLe));
        Assert.Empty(balance.GetProperty("rows").EnumerateArray());
        using var availability = await a.GetAsync($"/api/inventory/availability?itemId={a.Item}", otherLe);
        Assert.Equal(404, (int)availability.StatusCode);
    }

    [Fact]
    public async Task Tenant_B_cannot_issue_tenant_A_stock()
    {
        var a = new InventoryScenario(_factory);
        await a.PostExpectingAsync(a.Receipt("100"), 201);
        var b = new InventoryScenario(_factory);
        var steal = a.Issue("10");

        using var response = await a.PostAsync(steal, token: b.Token);

        Assert.Equal(422, (int)response.StatusCode);
        Assert.Equal(100m, await a.OnHandAsync());
    }

    [Theory]
    [MemberData(nameof(ReadEndpoints))]
    public async Task Mismatching_X_Legal_Entity_Id_is_403_CROSS_LE_FORBIDDEN_on_reads(string endpoint)
    {
        var s = new InventoryScenario(_factory);
        using var response = await s.GetAsync(endpoint.Replace("{item}", s.Item.ToString()),
            configure: r => r.Headers.Add("X-Legal-Entity-Id", Guid.NewGuid().ToString()));
        var json = await s.ReadAsync(response);

        Assert.Equal(403, (int)response.StatusCode);
        Assert.Equal("CROSS_LE_FORBIDDEN", json.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Mismatching_X_Legal_Entity_Id_is_403_CROSS_LE_FORBIDDEN_on_post_and_writes_nothing()
    {
        var s = new InventoryScenario(_factory);
        using var response = await s.PostAsync(s.Receipt("1"),
            configure: r => r.Headers.Add("X-Legal-Entity-Id", Guid.NewGuid().ToString()));
        var json = await s.ReadAsync(response);

        Assert.Equal(403, (int)response.StatusCode);
        Assert.Equal("CROSS_LE_FORBIDDEN", json.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Matching_X_Legal_Entity_Id_is_accepted()
    {
        var s = new InventoryScenario(_factory);
        using var response = await s.PostAsync(s.Receipt("1"),
            configure: r => r.Headers.Add("X-Legal-Entity-Id", s.LegalEntity.ToString()));
        Assert.Equal(201, (int)response.StatusCode);
    }

    [Fact]
    public async Task Mismatching_X_Tenant_Id_is_403_on_every_endpoint()
    {
        var s = new InventoryScenario(_factory);
        void Foreign(HttpRequestMessage r) => r.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        foreach (var endpoint in ReadUrls)
        {
            using var read = await s.GetAsync(endpoint.Replace("{item}", s.Item.ToString()), configure: Foreign);
            Assert.Equal(403, (int)read.StatusCode);
        }

        using var post = await s.PostAsync(s.Receipt("1"), configure: Foreign);
        Assert.Equal(403, (int)post.StatusCode);
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("legalEntityId")]
    [InlineData("TenantId")]
    public async Task Scope_in_the_query_is_400_on_every_endpoint(string key)
    {
        var s = new InventoryScenario(_factory);
        foreach (var endpoint in ReadUrls)
        {
            using var read = await s.GetAsync(endpoint.Replace("{item}", s.Item.ToString()) + $"&{key}={s.Tenant}");
            var json = await s.ReadAsync(read);
            Assert.Equal(400, (int)read.StatusCode);
            Assert.Equal("INVALID_REQUEST", json.GetProperty("error").GetProperty("code").GetString());
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/inventory/movements?{key}={s.Tenant}");
        using var post = await s.PostAsync(s.Receipt("1"), configure: r => r.RequestUri = request.RequestUri);
        Assert.Equal(400, (int)post.StatusCode);
    }

    [Theory]
    [InlineData("tenantId")]
    [InlineData("legalEntityId")]
    [InlineData("LegalEntityId")]
    public async Task Scope_in_the_body_is_400_and_writes_nothing(string key)
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body[key] = Guid.NewGuid().ToString();

        var refused = await s.PostExpectingAsync(body, 400);

        Assert.Equal("INVALID_REQUEST", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Persisted_documents_carry_the_token_scope_only()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("5"), 201);

        foreach (var collection in new[] { "inventory_transactions", "inventory_balances", "inventory_audit", "inventory_outbox" })
        {
            var doc = Assert.Single(s.Persisted(collection));
            Assert.Equal(s.Tenant.ToString(), doc["TenantId"].AsString);
            Assert.Equal(s.LegalEntity.ToString(), doc["LegalEntityId"].AsString);
        }
    }
}
