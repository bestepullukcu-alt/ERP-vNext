using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Persistence.Traceability;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-31 (DL-3, §8.5, Q-01 = A) — <see cref="ILotReservationReader.ReservedByLotAsync"/> resolved from the REAL host's DI in a
/// scope set like the scope middleware sets it. Fixture (tenant A / company E1, one SKU, enough stock for every reservation): in W, for L1
/// R1 20 and R2 5 (ACTIVE) and R3 7 (ACTIVE, due in 2 s, created LAST through the API — the create path closes due reservations — then 2.5 s
/// waited, sweeper off); for L2 R4 10 (RELEASED); in W a lot-less warehouse reservation R5 8; in W2 for L1 R6 9; written straight into
/// <c>inventory_reservations</c> (the API cannot create these scopes) the ACTIVE R7 11 of company E2 (tenant A) and the ACTIVE R8 13 of
/// tenant B carrying E1's Guid as its company, both for the same item / SKU / W / L1. The answer is exactly { L1: 25 }.
/// </summary>
public sealed class LotReservationReaderTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public LotReservationReaderTests(TraceApiFactory factory) => _factory = factory;

    private sealed record Fixture(ScopedReservationWorld W, Guid L1, Guid L2, string R3);

    private async Task<Fixture> FixtureAsync()
    {
        var w = new ScopedReservationWorld(_factory);
        var tenantB = new ScopedReservationWorld(_factory); // tracked, so its documents leave with it
        var l1 = await w.LotAsync();
        var l2 = await w.LotAsync();
        await w.ReceiveAsync("40", l1);
        await w.ReceiveAsync("10", l2);
        await w.ReceiveAsync("10", l1, w.Ledger.LocationC);

        var r4 = await w.ReserveOkAsync("10", w.W, l2);
        await w.ReleaseAsync(r4, "W/\"1\"", 200);
        var r1 = await w.ReserveOkAsync("20", w.W, l1);
        await w.ReserveOkAsync("5", w.W, l1);
        await w.ReserveOkAsync("8", w.W);
        await w.ReserveOkAsync("9", w.W2, l1);
        Insert(Copy(w.ReservationDocument(r1), w.Ledger.Tenant, Guid.NewGuid(), 11m));              // R7: company E2 of tenant A
        Insert(Copy(w.ReservationDocument(r1), tenantB.Ledger.Tenant, w.Ledger.LegalEntity, 13m));  // R8: tenant B, E1's Guid
        var r3 = await w.ReserveOkAsync("7", w.W, l1, expiresAt: ScopedReservationWorld.Later(TimeSpan.FromSeconds(2)));
        await Task.Delay(TimeSpan.FromMilliseconds(2500));
        return new Fixture(w, Guid.Parse(l1), Guid.Parse(l2), r3);
    }

    /// <summary>An ACTIVE reservation document without expiry, shaped like the service's own (copied from one of the world's).</summary>
    private static BsonDocument Copy(BsonDocument template, Guid tenant, Guid legalEntity, decimal quantity)
    {
        var doc = template.DeepClone().AsBsonDocument;
        doc["_id"] = Guid.NewGuid().ToString("D");
        doc["TenantId"] = tenant.ToString("D");
        doc["LegalEntityId"] = legalEntity.ToString("D");
        doc["IdempotencyKey"] = "direct-" + Guid.NewGuid().ToString("N");
        doc["Quantity"] = new BsonDecimal128(quantity);
        doc["RemainingQuantity"] = new BsonDecimal128(quantity);
        doc["ExpiresAt"] = BsonNull.Value; // never due, whichever ACTIVE reservation is the template
        return doc;
    }

    private void Insert(BsonDocument doc, IClientSessionHandle? session = null)
    {
        var reservations = _factory.Collection("inventory_reservations");
        if (session is null) reservations.InsertOne(doc);
        else reservations.InsertOne(session, doc);
    }

    private IServiceScope Scope(ScopedReservationWorld w)
    {
        var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(w.Ledger.Tenant, w.Ledger.LegalEntity, w.Ledger.User, LegalEntitySources.Token);
        return scope;
    }

    private static Task<IReadOnlyDictionary<Guid, decimal>> ReadAsync(IServiceScope scope, ScopedReservationWorld w)
        => scope.ServiceProvider.GetRequiredService<ILotReservationReader>().ReservedByLotAsync(w.Ledger.Item, w.Ledger.Sku, w.Ledger.Level, w.W);

    private async Task<string> ReadAsync(ScopedReservationWorld w)
    {
        using var scope = Scope(w);
        return Show(await ReadAsync(scope, w));
    }

    private static string Show(IReadOnlyDictionary<Guid, decimal> reserved)
        => string.Join(", ", reserved.OrderBy(r => r.Key).Select(r => r.Key + "=" + r.Value.ToString("0.###", CultureInfo.InvariantCulture)));

    /// <summary>(a) R3 (due, document ACTIVE), R4 (released), R5 (no lot), R6 (other warehouse) are not counted; L2 is not in the dictionary.</summary>
    [Fact]
    public async Task Reads_the_active_lot_reservations_of_the_warehouse_per_lot()
    {
        var f = await FixtureAsync();
        Assert.Equal(f.L1 + "=25", await ReadAsync(f.W));
    }

    /// <summary>
    /// (a) — S-38: the reader keeps only <c>Status == ACTIVE</c> (or reads the <c>L:</c> position's Reserved) → R3 is counted (32), red. The
    /// due R3 is still ACTIVE and still in the position: only the read rule leaves it out.
    /// </summary>
    [Fact]
    public async Task Due_reservation_is_not_counted()
    {
        var f = await FixtureAsync();
        Assert.Equal("Active", f.W.ReservationDocument(f.R3)["Status"].AsString);
        Assert.Equal(32m, f.W.Scope(f.W.W, f.L1.ToString()).Reserved);
        Assert.Equal(f.L1 + "=25", await ReadAsync(f.W));
    }

    /// <summary>(c) — S-41: the reader without the LegalEntityId filter → R7 (company E2) is counted (36), red.</summary>
    [Fact]
    public async Task Other_company_reservation_is_not_counted()
    {
        var f = await FixtureAsync();
        Assert.Equal(f.L1 + "=25", await ReadAsync(f.W));
    }

    /// <summary>(c) — S-42: the reader without the TenantId filter → R8 (tenant B, E1's Guid) is counted (38), red.</summary>
    [Fact]
    public async Task Other_tenant_reservation_is_not_counted()
    {
        var f = await FixtureAsync();
        Assert.Equal(f.L1 + "=25", await ReadAsync(f.W));
    }

    /// <summary>
    /// (b) — the production session path: a transaction is started and bound with <see cref="LedgerTransactionBinding.Bind"/> (the ledger's own
    /// call, <c>InventoryLedgerRepository</c>), an ACTIVE 3 of L1 / W is written in it → the bound read sees 28 while an unbound read of
    /// another DI scope sees 25 (uncommitted); after the abort 25. S-40: the reader ignores the binding (always outside) → 25 bound, red.
    /// </summary>
    [Fact]
    public async Task Bound_read_sees_same_transaction_change()
    {
        var f = await FixtureAsync();
        using var scope = Scope(f.W);
        using var session = await scope.ServiceProvider.GetRequiredService<IMongoClient>().StartSessionAsync();
        session.StartTransaction();
        using (scope.ServiceProvider.GetRequiredService<LedgerTransactionBinding>().Bind(session))
        {
            var template = f.W.Reservations().First(d => d["LotId"].IsString && d["LotId"].AsString == f.L1.ToString()
                                                     && d["WarehouseId"].AsString == f.W.W && d["Status"].AsString == "Active");
            Insert(Copy(template, f.W.Ledger.Tenant, f.W.Ledger.LegalEntity, 3m), session);

            Assert.Equal(f.L1 + "=28", Show(await ReadAsync(scope, f.W)));
            Assert.Equal(f.L1 + "=25", await ReadAsync(f.W));
        }

        await session.AbortTransactionAsync();
        Assert.Equal(f.L1 + "=25", await ReadAsync(f.W));
    }
}
