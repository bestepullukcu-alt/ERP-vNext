using System.Security.Claims;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// AC-R10 (isolation + RBAC on the new endpoints), AC-R11 (FU-S1-01), AC-R12 (FU-S1-02 — no SuperAdmin shortcut).
/// </summary>
public sealed class ReservationAccessTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ReservationAccessTests(InventoryApiFactory factory) => _factory = factory;

    // ── AC-R10 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_token_is_401_on_both_reservation_endpoints()
    {
        var s = new InventoryScenario(_factory);

        using var create = await s.ReserveAsync(s.Reservation("1"), token: string.Empty);
        using var release = await s.ReleaseAsync(Guid.NewGuid().ToString(), "W/\"1\"", token: string.Empty);

        Assert.Equal(401, (int)create.StatusCode);
        Assert.Equal(401, (int)release.StatusCode);
    }

    [Fact]
    public async Task Reservations_need_reservations_manage_read_and_post_are_not_enough()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        var created = await s.ReserveExpectingAsync(s.Reservation("10"), 201);
        var noManage = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [TestTokens.ReadPermission, TestTokens.PostPermission]);

        using var create = await s.ReserveAsync(s.Reservation("1"), token: noManage);
        using var release = await s.ReleaseAsync(created.GetProperty("reservationId").GetString()!, created.GetProperty("etag").GetString(), noManage);

        Assert.Equal(403, (int)create.StatusCode);
        Assert.Equal(403, (int)release.StatusCode);
        Assert.Single(s.Persisted("inventory_reservations"));

        // Control: manage alone is sufficient for both.
        var manageOnly = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [TestTokens.ReservationsPermission]);
        using var ok = await s.ReserveAsync(s.Reservation("1"), token: manageOnly);
        Assert.Equal(201, (int)ok.StatusCode);
    }

    [Fact]
    public async Task Other_tenant_and_other_LE_cannot_see_reserve_against_or_release_a_reservation()
    {
        var a = new InventoryScenario(_factory);
        await a.PostExpectingAsync(a.Receipt("100"), 201);
        var created = await a.ReserveExpectingAsync(a.Reservation("30"), 201);
        var id = created.GetProperty("reservationId").GetString()!;
        var etag = created.GetProperty("etag").GetString()!;

        var b = new InventoryScenario(_factory);
        using (var foreignRelease = await b.ReleaseAsync(id, etag)) Assert.Equal(404, (int)foreignRelease.StatusCode);
        using (var foreignReserve = await a.ReserveAsync(a.Reservation("1"), token: b.Token)) Assert.Equal(422, (int)foreignReserve.StatusCode);

        var otherLe = TestTokens.Mint(a.Tenant, Guid.NewGuid(), a.User, TestTokens.AllPermissions);
        using (var otherLeRelease = await a.ReleaseAsync(id, etag, otherLe)) Assert.Equal(404, (int)otherLeRelease.StatusCode);

        using (var crossLe = await a.ReserveAsync(a.Reservation("1"), configure: r => r.Headers.Add("X-Legal-Entity-Id", Guid.NewGuid().ToString())))
        {
            Assert.Equal(403, (int)crossLe.StatusCode);
            Assert.Equal("CROSS_LE_FORBIDDEN", (await a.ReadAsync(crossLe)).GetProperty("error").GetProperty("code").GetString());
        }

        Assert.Equal((100m, 30m, 70m), await a.AvailabilityAsync());
        Assert.Single(a.Persisted("inventory_reservations"));
        Assert.Empty(b.Persisted("inventory_reservations"));
    }

    [Fact]
    public async Task Malformed_reservation_id_is_404_and_tenantId_in_the_query_is_400()
    {
        var s = new InventoryScenario(_factory);
        using var malformed = await s.ReleaseAsync("rsv-4001", "W/\"1\"");
        Assert.Equal(404, (int)malformed.StatusCode);

        using var query = await s.ReserveAsync(s.Reservation("1"),
            configure: r => r.RequestUri = new Uri($"/api/inventory/reservations?tenantId={s.Tenant}", UriKind.Relative));
        Assert.Equal(400, (int)query.StatusCode);
    }

    // ── AC-R11 (FU-S1-01) ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_known_sku_without_stock_is_200_with_zeros()
    {
        var s = new InventoryScenario(_factory);

        var json = await s.ReadAsync(await s.GetAsync($"/api/inventory/availability?itemId={s.Item}&skuId={s.Sku}&skuLevel=Lsku"));

        Assert.Equal("0.000", json.GetProperty("onHand").GetString());
        Assert.Equal("0.000", json.GetProperty("reserved").GetString());
        Assert.Equal("0.000", json.GetProperty("available").GetString());
        Assert.Equal("0.000", json.GetProperty("byStatus").GetProperty("AVAILABLE").GetString());
    }

    [Fact]
    public async Task An_unknown_item_or_sku_is_404_UNKNOWN_ITEM()
    {
        var s = new InventoryScenario(_factory);

        using var item = await s.GetAsync($"/api/inventory/availability?itemId={Guid.NewGuid()}");
        using var sku = await s.GetAsync($"/api/inventory/availability?skuId={Guid.NewGuid()}");
        using var wrongLevel = await s.GetAsync($"/api/inventory/availability?skuId={s.Sku}&skuLevel=Gsku");

        foreach (var response in new[] { item, sku, wrongLevel })
        {
            Assert.Equal(404, (int)response.StatusCode);
            Assert.Equal("UNKNOWN_ITEM", (await s.ReadAsync(response)).GetProperty("error").GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Product_master_down_is_503_only_when_there_are_no_ledger_rows()
    {
        var empty = new InventoryScenario(_factory);
        _factory.Products.MarkUnavailable(empty.Item, empty.Sku);
        using var down = await empty.GetAsync($"/api/inventory/availability?itemId={empty.Item}");
        Assert.Equal(503, (int)down.StatusCode);
        Assert.Equal("PRODUCT_MASTER_UNAVAILABLE", (await empty.ReadAsync(down)).GetProperty("error").GetProperty("code").GetString());

        // With rows the read path makes no upstream call: still 200 although the master is down.
        var stocked = new InventoryScenario(_factory);
        await stocked.PostExpectingAsync(stocked.Receipt("5"), 201);
        _factory.Products.MarkUnavailable(stocked.Item, stocked.Sku);
        using var ok = await stocked.GetAsync($"/api/inventory/availability?itemId={stocked.Item}");
        Assert.Equal(200, (int)ok.StatusCode);
    }

    // ── AC-R12 (FU-S1-02) ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuperAdmin_role_without_the_permission_is_403_on_mutations_and_reads()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("100"), 201);
        Claim[] superAdmin = [new("role", "SuperAdmin"), new(ClaimTypes.Role, "SuperAdmin")];
        var token = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [], superAdmin);

        using var post = await s.PostAsync(s.Issue("1"), token: token);
        using var reserve = await s.ReserveAsync(s.Reservation("1"), token: token);
        using var read = await s.GetAsync("/api/inventory/balance", token);

        Assert.Equal(403, (int)post.StatusCode);
        Assert.Equal(403, (int)reserve.StatusCode);
        Assert.Equal(403, (int)read.StatusCode);
        Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Empty(s.Persisted("inventory_reservations"));

        // Control: the same role WITH the permission works — the role neither helps nor hurts.
        var withPermission = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [TestTokens.PostPermission], superAdmin);
        using var allowed = await s.PostAsync(s.Issue("1"), token: withPermission);
        Assert.Equal(201, (int)allowed.StatusCode);
    }
}
