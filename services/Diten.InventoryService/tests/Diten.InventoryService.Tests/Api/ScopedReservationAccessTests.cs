using System.Security.Claims;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-13 — the new read needs <c>inventory.stock.read</c>, a scoped create <c>inventory.reservations.manage</c>; a SuperAdmin role
/// without the key is still 403 (ebeveyn ASSUMPTION-0173-19). Every endpoint carrying <c>[HasPermission]</c> is AuthorizationGuardTests'.
/// </summary>
public sealed class ScopedReservationAccessTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ScopedReservationAccessTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>AC-13 — S-7: [HasPermission(inventory.stock.read)] dropped from GET /reservations/{id} → 200 without the key, red.</summary>
    [Fact]
    public async Task Get_without_stock_read_is_403()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        var id = await w.ReserveOkAsync("20", w.W, lot);
        var noRead = TestTokens.Mint(w.Ledger.Tenant, w.Ledger.LegalEntity, w.Ledger.User,
            [TestTokens.PostPermission, TestTokens.ReservationsPermission]);
        Claim[] superAdmin = [new("role", "SuperAdmin"), new(ClaimTypes.Role, "SuperAdmin")];
        var superAdminWithoutKeys = TestTokens.Mint(w.Ledger.Tenant, w.Ledger.LegalEntity, w.Ledger.User, [], superAdmin);

        using (var response = await w.Ledger.GetAsync($"/api/inventory/reservations/{id}", noRead))
        {
            Assert.Equal(403, (int)response.StatusCode);
            Assert.DoesNotContain(id, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        using (var response = await w.Ledger.GetAsync($"/api/inventory/reservations/{id}", superAdminWithoutKeys))
        {
            Assert.Equal(403, (int)response.StatusCode);
        }

        var noManage = TestTokens.Mint(w.Ledger.Tenant, w.Ledger.LegalEntity, w.Ledger.User, [TestTokens.ReadPermission, TestTokens.PostPermission]);
        using (var create = await w.Ledger.ReserveAsync(w.Reservation("1", w.W, lot), noManage))
        {
            Assert.Equal(403, (int)create.StatusCode);
        }

        using (var create = await w.Ledger.ReserveAsync(w.Reservation("1", w.W, lot), superAdminWithoutKeys))
        {
            Assert.Equal(403, (int)create.StatusCode);
        }

        Assert.Single(w.Reservations());
    }
}
