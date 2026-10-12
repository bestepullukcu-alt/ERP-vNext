using System.Security.Claims;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>AC11 / pack §13.4 — on EVERY endpoint.</summary>
public sealed class RbacTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public RbacTests(InventoryApiFactory factory) => _factory = factory;

    private static readonly string[] Reads =
    [
        "/api/inventory/availability?itemId={item}",
        "/api/inventory/balance",
        "/api/inventory/transactions"
    ];

    [Fact]
    public async Task No_token_is_401_on_every_endpoint()
    {
        var s = new InventoryScenario(_factory);
        foreach (var endpoint in Reads)
        {
            using var read = await s.GetAsync(endpoint.Replace("{item}", s.Item.ToString()), token: string.Empty);
            Assert.Equal(401, (int)read.StatusCode);
        }

        using var post = await s.PostAsync(s.Receipt("1"), token: string.Empty);
        Assert.Equal(401, (int)post.StatusCode);
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Token_signed_with_another_key_or_expired_is_401()
    {
        var s = new InventoryScenario(_factory);
        var forged = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, TestTokens.AllPermissions, signingSecret: new string('x', 64));
        var expired = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, TestTokens.AllPermissions, expires: DateTime.UtcNow.AddMinutes(-5));

        foreach (var token in new[] { forged, expired })
        {
            using var post = await s.PostAsync(s.Receipt("1"), token: token);
            Assert.Equal(401, (int)post.StatusCode);
        }

        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Post_without_the_post_permission_is_403_even_with_read()
    {
        var s = new InventoryScenario(_factory);
        var readOnly = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [TestTokens.ReadPermission]);

        using var post = await s.PostAsync(s.Receipt("1"), token: readOnly);

        Assert.Equal(403, (int)post.StatusCode);
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task Reads_without_the_read_permission_are_403_even_with_post()
    {
        var s = new InventoryScenario(_factory);
        var postOnly = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [TestTokens.PostPermission]);

        foreach (var endpoint in Reads)
        {
            using var read = await s.GetAsync(endpoint.Replace("{item}", s.Item.ToString()), token: postOnly);
            Assert.Equal(403, (int)read.StatusCode);
        }
    }

    [Fact]
    public async Task Control_each_permission_opens_exactly_its_own_endpoints()
    {
        var s = new InventoryScenario(_factory);
        var postOnly = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [TestTokens.PostPermission]);
        var readOnly = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [TestTokens.ReadPermission]);

        using var post = await s.PostAsync(s.Receipt("1"), token: postOnly);
        Assert.Equal(201, (int)post.StatusCode);
        foreach (var endpoint in Reads)
        {
            using var read = await s.GetAsync(endpoint.Replace("{item}", s.Item.ToString()), token: readOnly);
            Assert.Equal(200, (int)read.StatusCode);
        }
    }

    [Theory]
    [InlineData("tenant_id")]
    [InlineData("legal_entity_id")]
    [InlineData("sub")]
    public async Task A_token_missing_a_scope_claim_is_403(string missing)
    {
        var s = new InventoryScenario(_factory);
        var token = TestTokens.Mint(
            missing == "tenant_id" ? null : s.Tenant,
            missing == "legal_entity_id" ? null : s.LegalEntity,
            missing == "sub" ? null : s.User,
            TestTokens.AllPermissions);

        using var post = await s.PostAsync(s.Receipt("1"), token: token);

        Assert.Equal(403, (int)post.StatusCode);
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task A_token_with_two_tenant_claims_is_403()
    {
        var s = new InventoryScenario(_factory);
        var token = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, TestTokens.AllPermissions,
            [new Claim("tenant_id", Guid.NewGuid().ToString())]);

        using var read = await s.GetAsync("/api/inventory/balance", token);

        Assert.Equal(403, (int)read.StatusCode);
    }

    [Fact]
    public async Task Health_is_anonymous_and_names_the_module()
    {
        using var client = _factory.CreateClient();
        var json = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/health")).RootElement;

        Assert.Equal("Healthy", json.GetProperty("status").GetString());
        Assert.Equal("MOD-0173", json.GetProperty("module").GetString());
    }
}
