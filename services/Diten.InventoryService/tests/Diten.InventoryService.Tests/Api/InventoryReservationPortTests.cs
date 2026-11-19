using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-16 / AC-39 / AC-17 (e) — the in-process reservation port (§8.7) through the REAL host's DI: the batch is one transaction
/// (all or nothing, 1…100 lines, no mixed replay), every member checks the company (and the key read the tenant) BEFORE any read or write,
/// the key read returns only the named keys of the resolved Tenant + LE, and the scope's free quantity does not count a due reservation.
/// </summary>
public sealed class InventoryReservationPortTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public InventoryReservationPortTests(TraceApiFactory factory) => _factory = factory;

    /// <summary>S-9: CreateBatchAsync writes each line in its own transaction → lines 1 and 2 remain, red.</summary>
    [Fact]
    public async Task Batch_with_one_insufficient_line_writes_nothing()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("25", lot);
        var port = new PortWorld(_factory, w);
        var positions = w.PositionsSnapshot();

        var result = await port.Use(p => p.CreateBatchAsync(
            [PortWorld.Line(w, "10", w.W, lot), PortWorld.Line(w, "10", w.W, lot), PortWorld.Line(w, "10", w.W, lot)], w.Ledger.LegalEntity, w.Ledger.User));

        Assert.Equal((422, "INSUFFICIENT_AVAILABLE"), (result.StatusCode, result.ErrorCode));
        Assert.Equal(("INSUFFICIENT_AVAILABLE", "LOT", "5.000"), (result.Lines[2].ErrorCode, result.Lines[2].Level, result.Lines[2].Available));
        Assert.All(result.Lines, l => Assert.False(l.Written));
        Assert.Empty(w.Reservations());
        Assert.Equal(positions, w.PositionsSnapshot());
    }

    [Fact]
    public async Task Batch_of_valid_lines_writes_them_all_in_one_transaction_and_replays()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("30", lot);
        var port = new PortWorld(_factory, w);
        var lines = Enumerable.Range(0, 3).Select(_ => PortWorld.Line(w, "10", w.W, lot)).ToList();

        var created = await port.Use(p => p.CreateBatchAsync(lines, w.Ledger.LegalEntity, w.Ledger.User));
        Assert.Equal(201, created.StatusCode);
        Assert.Equal(3, created.Lines.Count(l => l.Written));
        Assert.Equal(3, w.Reservations().Count);
        Assert.Equal((30m, 30m), w.Scope(w.W, lot));

        var replay = await port.Use(p => p.CreateBatchAsync(lines, w.Ledger.LegalEntity, w.Ledger.User));
        Assert.Equal(200, replay.StatusCode);
        Assert.Equal(created.Lines.Select(l => l.Reservation!.ReservationId), replay.Lines.Select(l => l.Reservation!.ReservationId));

        var mixed = await port.Use(p => p.CreateBatchAsync([lines[0], PortWorld.Line(w, "1", w.W, lot)], w.Ledger.LegalEntity, w.Ledger.User));
        Assert.Equal((400, "IDEMPOTENCY_KEY_REUSED"), (mixed.StatusCode, mixed.ErrorCode));
        Assert.Equal(3, w.Reservations().Count);
        w.AssertInvariant("batch");
    }

    [Fact]
    public async Task Batch_of_100_lines_is_accepted_and_101_is_refused_with_nothing_written()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("200", lot);
        var port = new PortWorld(_factory, w);

        var tooMany = await port.Use(p => p.CreateBatchAsync(Enumerable.Range(0, 101).Select(_ => PortWorld.Line(w, "1", w.W, lot)).ToList(),
            w.Ledger.LegalEntity, w.Ledger.User));
        Assert.Equal((400, "INVALID_REQUEST"), (tooMany.StatusCode, tooMany.ErrorCode));
        Assert.Empty(w.Reservations());

        var hundred = await port.Use(p => p.CreateBatchAsync(Enumerable.Range(0, 100).Select(_ => PortWorld.Line(w, "1", w.W, lot)).ToList(),
            w.Ledger.LegalEntity, w.Ledger.User));
        Assert.Equal(201, hundred.StatusCode);
        Assert.Equal(100, w.Reservations().Count);
        w.AssertInvariant("100 lines");
    }

    /// <summary>S-37a: the legalEntityId check of CreateAsync removed → the E1 reservation is written, red.</summary>
    [Fact]
    public async Task Create_with_foreign_company_throws_and_writes_nothing()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        var port = new PortWorld(_factory, w);
        var before = Everything(w);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            port.Use(p => p.CreateAsync(PortWorld.Line(w, "10", w.W, lot), Guid.NewGuid(), w.Ledger.User)));

        Assert.Equal(before, Everything(w));
    }

    /// <summary>S-37b: the same check of CreateBatchAsync removed → three reservations are written, red.</summary>
    [Fact]
    public async Task Batch_with_foreign_company_throws_and_writes_nothing()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        var port = new PortWorld(_factory, w);
        var before = Everything(w);

        await Assert.ThrowsAsync<InvalidOperationException>(() => port.Use(p => p.CreateBatchAsync(
            [PortWorld.Line(w, "10", w.W, lot), PortWorld.Line(w, "10", w.W, lot), PortWorld.Line(w, "10", w.W, lot)], Guid.NewGuid(), w.Ledger.User)));

        Assert.Equal(before, Everything(w));
    }

    /// <summary>AC-39 (a) — S-34: the key filter dropped from the query → k4 is returned, red.</summary>
    [Fact]
    public async Task Find_by_keys_returns_only_named_keys()
    {
        var (w, keys) = await KeyFixtureAsync();
        var port = new PortWorld(_factory, w);
        var audit = w.Count("inventory_audit");

        var rows = await port.Use(p => p.FindByIdempotencyKeysAsync(w.Ledger.Tenant, w.Ledger.LegalEntity, ["k1", "k2", "k3", "k-none"]));

        Assert.Equal(["k1", "k2", "k3"], rows.Select(r => r.IdempotencyKey).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(("ACTIVE", (string?)null, "W/\"1\""), Row(rows, "k1"));
        Assert.Equal(("RELEASED", "Released", "W/\"2\""), Row(rows, "k2"));
        Assert.Equal(("EXPIRED", "Expired", "W/\"2\""), Row(rows, "k3"));
        Assert.All(rows, r => Assert.Equal(keys[r.IdempotencyKey], r.ReservationId.ToString()));
        Assert.Equal(audit, w.Count("inventory_audit")); // (e) a read writes no audit row
    }

    /// <summary>AC-39 (b) — S-35: the query by TenantId only (company filter dropped) → E2's k1 is returned too, red.</summary>
    [Fact]
    public async Task Find_by_keys_is_tenant_and_company_scoped()
    {
        var (w, keys) = await KeyFixtureAsync();

        var rows = await new PortWorld(_factory, w).Use(p => p.FindByIdempotencyKeysAsync(w.Ledger.Tenant, w.Ledger.LegalEntity, ["k1", "k2"]));
        Assert.Equal(new[] { keys["k1"], keys["k2"] }.OrderBy(x => x), rows.Select(r => r.ReservationId.ToString()).OrderBy(x => x));

        var tenantB = Guid.Parse(keys["tenantB"]);
        var asB = await new PortWorld(_factory, tenantB, w.Ledger.LegalEntity, Guid.NewGuid())
            .Use(p => p.FindByIdempotencyKeysAsync(tenantB, w.Ledger.LegalEntity, ["k1", "k2", "k3"]));
        Assert.Equal(new[] { keys["B:k1"], keys["B:k2"] }.OrderBy(x => x), asB.Select(r => r.ReservationId.ToString()).OrderBy(x => x));
    }

    /// <summary>AC-39 (c1) — S-36a: the tenantId check removed → no exception, A / E1's rows come back, red.</summary>
    [Fact]
    public async Task Find_by_keys_with_foreign_tenant_throws()
    {
        var (w, keys) = await KeyFixtureAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PortWorld(_factory, w)
            .Use(p => p.FindByIdempotencyKeysAsync(Guid.Parse(keys["tenantB"]), w.Ledger.LegalEntity, ["k1", "k2"])));
    }

    /// <summary>AC-39 (c2) — S-36b: the legalEntityId check removed → no exception, A / E1's rows come back, red.</summary>
    [Fact]
    public async Task Find_by_keys_with_foreign_company_throws()
    {
        var (w, keys) = await KeyFixtureAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PortWorld(_factory, w)
            .Use(p => p.FindByIdempotencyKeysAsync(w.Ledger.Tenant, Guid.Parse(keys["companyE2"]), ["k1", "k2"])));
    }

    /// <summary>AC-39 (d, f): malformed key lists are refused; the recovery flow (found ACTIVE → ReleaseAsync) frees every level.</summary>
    [Fact]
    public async Task Find_by_keys_refuses_malformed_lists_and_the_recovery_release_frees_every_level()
    {
        var (w, keys) = await KeyFixtureAsync();
        var port = new PortWorld(_factory, w);
        foreach (var bad in new[] { Array.Empty<string>(), Enumerable.Range(0, 101).Select(i => "x" + i).ToArray(), ["k1", " "], ["k1", "k1"] })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => port.Use(p => p.FindByIdempotencyKeysAsync(w.Ledger.Tenant, w.Ledger.LegalEntity, bad)));
        }

        var active = (await port.Use(p => p.FindByIdempotencyKeysAsync(w.Ledger.Tenant, w.Ledger.LegalEntity, ["k1", "k4"])))
            .Where(r => r.Status == "ACTIVE").ToList();
        Assert.Equal(2, active.Count);
        foreach (var row in active)
        {
            var released = await port.Use(p => p.ReleaseAsync(row.ReservationId, row.Etag, w.Ledger.User));
            Assert.Equal(200, released.StatusCode);
        }

        Assert.Equal((0m, 0m, 0m), (w.SkuPosition().Reserved, w.Scope(w.W).Reserved, w.Scope(w.W, keys["lot"]).Reserved));
        w.AssertInvariant("recovery");
    }

    /// <summary>AC-17 (e) port side — S-39: the scope read counts Status ACTIVE only (or the position's Reserved) → 20 instead of 30, red.</summary>
    [Fact]
    public async Task Scope_free_excludes_due_reservation()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        await w.ReserveOkAsync("20", w.W, lot);
        var port = new PortWorld(_factory, w);
        Assert.Equal(30m, await port.Use(p => p.GetScopeFreeAsync(w.Ledger.Item, w.Ledger.Sku, w.Ledger.Level, w.W, Guid.Parse(lot))));

        var due = await w.ReserveOkAsync("10", w.W, lot, expiresAt: ScopedReservationWorld.Later(TimeSpan.FromSeconds(2)));
        Assert.Equal(20m, await port.Use(p => p.GetScopeFreeAsync(w.Ledger.Item, w.Ledger.Sku, w.Ledger.Level, w.W, Guid.Parse(lot))));
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        Assert.Equal("Active", w.ReservationDocument(due)["Status"].AsString);
        Assert.Equal(30m, w.Scope(w.W, lot).Reserved); // the projection still holds it until an expiry runs
        Assert.Equal(30m, await port.Use(p => p.GetScopeFreeAsync(w.Ledger.Item, w.Ledger.Sku, w.Ledger.Level, w.W, Guid.Parse(lot))));
        Assert.Equal(30m, await port.Use(p => p.GetScopeFreeAsync(w.Ledger.Item, w.Ledger.Sku, w.Ledger.Level, w.W)));
    }

    // ── fixture of AC-39 ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tenant A / company E1: k1 ACTIVE, k2 RELEASED, k3 EXPIRED, k4 ACTIVE (not asked); A / E2: k1; tenant B: k1, k2 — the foreign ones are
    /// written as documents (the API cannot create them in another scope), so the scope filters are measured, not assumed.
    /// </summary>
    private async Task<(ScopedReservationWorld World, Dictionary<string, string> Keys)> KeyFixtureAsync()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("100", lot);
        var port = new PortWorld(_factory, w);
        var keys = new Dictionary<string, string>(StringComparer.Ordinal) { ["lot"] = lot };
        async Task<string> Create(string key, string? expiresAt = null)
        {
            var response = await port.Use(p => p.CreateAsync(PortWorld.Line(w, "5", w.W, lot, key, expiresAt), w.Ledger.LegalEntity, w.Ledger.User));
            Assert.True(response.StatusCode == 201, $"create {key}: {response.StatusCode} {response.ErrorCode}");
            return keys[key] = response.Data!.ReservationId;
        }

        await Create("k1");
        var k2 = await Create("k2");
        Assert.Equal(200, (await port.Use(p => p.ReleaseAsync(Guid.Parse(k2), "W/\"1\"", w.Ledger.User))).StatusCode);
        await Create("k3", ScopedReservationWorld.Later(TimeSpan.FromSeconds(2)));
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        await Create("k4"); // its transaction expires k3 first

        var companyE2 = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        _factory.TrackTenant(tenantB);
        keys["companyE2"] = companyE2.ToString();
        keys["tenantB"] = tenantB.ToString();
        keys["E2:k1"] = Insert(w, w.Ledger.Tenant, companyE2, "k1");
        keys["B:k1"] = Insert(w, tenantB, w.Ledger.LegalEntity, "k1");
        keys["B:k2"] = Insert(w, tenantB, w.Ledger.LegalEntity, "k2");
        return (w, keys);
    }

    private string Insert(ScopedReservationWorld w, Guid tenant, Guid company, string key)
    {
        var id = Guid.NewGuid().ToString();
        _factory.Collection("inventory_reservations").InsertOne(new BsonDocument
        {
            { "_id", id }, { "TenantId", tenant.ToString() }, { "LegalEntityId", company.ToString() }, { "IsDeleted", false },
            { "CreatedAt", DateTime.UtcNow }, { "Version", 1 }, { "ItemId", w.Ledger.Item.ToString() }, { "SkuId", w.Ledger.Sku.ToString() },
            { "SkuLevel", "Lsku" }, { "Quantity", new BsonDecimal128(5m) }, { "RemainingQuantity", new BsonDecimal128(5m) }, { "Status", "Active" },
            { "ReservationType", "PICK" }, { "SourceModule", "MOD-0178" }, { "IdempotencyKey", key }, { "RequestFingerprint", "x" }
        });
        return id;
    }

    private static (string Status, string? ClosedReason, string Etag) Row(IEnumerable<Diten.InventoryService.Application.Features.Inventory.ReservationKeyMatch> rows, string key)
    {
        var row = Assert.Single(rows, r => r.IdempotencyKey == key);
        return (row.Status, row.ClosedReason, row.Etag);
    }

    private static string Everything(ScopedReservationWorld w)
        => string.Join("\n", new[] { "inventory_reservations", "inventory_sku_positions", "inventory_reservation_positions", "inventory_audit" }
            .SelectMany(c => w.Persisted(c).Select(d => c + d.ToJson())).OrderBy(x => x, StringComparer.Ordinal));
}
