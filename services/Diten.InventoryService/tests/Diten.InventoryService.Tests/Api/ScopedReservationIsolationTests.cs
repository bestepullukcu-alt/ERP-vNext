using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-12 (isolation) on the scoped reservation and the new <c>getReservation</c>. Another tenant's or
/// another company's reservation answers 404 — the same body as an unknown id — and its documents stay byte-equal.
/// </summary>
public sealed class ScopedReservationIsolationTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ScopedReservationIsolationTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Other_tenant_reservation_is_404_and_its_documents_stay_byte_equal()
    {
        var a = new ScopedReservationWorld(_factory);
        var b = new ScopedReservationWorld(_factory);
        var lot = await b.LotAsync();
        await b.ReceiveAsync("50", lot);
        var foreign = await b.ReserveOkAsync("20", b.W, lot);
        var reservations = Snapshot(b, "inventory_reservations");
        var positions = b.PositionsSnapshot();

        var notFound = await a.GetReservationAsync(foreign, 404);
        var unknown = await a.GetReservationAsync(Guid.NewGuid().ToString(), 404);
        Assert.Equal(Message(unknown), Message(notFound));
        Assert.False(notFound.GetProperty("error").TryGetProperty("code", out _));
        await a.ReleaseAsync(foreign, "W/\"1\"", 404);
        var notUuid = await a.GetReservationAsync("not-a-reservation", 404);
        Assert.Equal(Message(unknown), Message(notUuid));

        Assert.Equal(reservations, Snapshot(b, "inventory_reservations"));
        Assert.Equal(positions, b.PositionsSnapshot());
    }

    /// <summary>S-6: the LegalEntityId filter dropped from the getReservation read → the same tenant's other company reads it (200), red.</summary>
    [Fact]
    public async Task Other_legal_entity_reservation_is_404()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        var id = await w.ReserveOkAsync("20", w.W, lot);
        var otherCompany = TestTokens.Mint(w.Ledger.Tenant, Guid.NewGuid(), w.Ledger.User, TestTokens.AllPermissions);
        var before = Snapshot(w, "inventory_reservations");

        await w.GetReservationAsync(id, 404, otherCompany);
        using (var release = await w.Ledger.ReleaseAsync(id, "W/\"1\"", otherCompany))
        {
            Assert.Equal(404, (int)release.StatusCode);
        }

        Assert.Equal(before, Snapshot(w, "inventory_reservations"));
        await w.GetReservationAsync(id, 200);
    }

    /// <summary>AC-12: a warehouse of another company of the same tenant is not a warehouse of the caller's company.</summary>
    [Fact]
    public async Task Other_company_warehouse_is_UNKNOWN_LOCATION()
    {
        var w = new ScopedReservationWorld(_factory);
        var (foreignWarehouse, _) = w.Ledger.AddForeignCompanyBin();
        var refused = await w.ReserveAsync(w.Reservation("1", foreignWarehouse), 422);
        Assert.Equal("UNKNOWN_LOCATION", ScopedReservationWorld.Code(refused));
        Assert.Empty(w.Reservations());
    }

    private static string Snapshot(ScopedReservationWorld w, string collection)
        => string.Join("\n", w.Persisted(collection).Select(d => d.ToJson()).OrderBy(x => x, StringComparer.Ordinal));

    private static string Message(System.Text.Json.JsonElement error) => error.GetProperty("error").GetProperty("message").GetString()!;
}
