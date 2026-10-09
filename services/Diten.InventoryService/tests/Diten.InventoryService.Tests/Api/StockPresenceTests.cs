using System.Net.Http.Headers;
using System.Text.Json;
using Diten.InventoryService.Api.InternalAccess;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Infrastructure.Middleware;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-0173-PM2 — INVENTORY-BUNDLE 1.1.0 <c>getStockPresence</c> through the REAL host, every answer checked against the 1.1.0
/// <c>StockPresence</c> / <c>Error</c> schemas and the operation's four statuses (<see cref="ContractValidatingHandler"/>):
/// <list type="bullet">
/// <item>the key: missing, wrong secret, unknown identifier, a valid key of another operation, a user token (alone or with a
/// key), plain http → the SAME 401, byte for byte;</item>
/// <item>the request: <c>X-Tenant-Id</c> missing / not a UUID, unknown <c>skuLevel</c>, malformed <c>skuId</c> → 400;</item>
/// <item>the answer: never stocked → false; stock only in QUARANTINE / QUALITY_INSPECTION / BLOCKED / IN_TRANSIT, all reserved,
/// in another company of the tenant, a negative row → true; another TENANT's stock → false; back to zero → false; Item;</item>
/// <item>rotation (both secrets pass; a secret that left the list does not), 503 when the store cannot be read, and the audit +
/// log scan (the key identifier is there, no secret anywhere).</item>
/// </list>
/// </summary>
public sealed class StockPresenceTests : IClassFixture<StockPresenceApiFactory>
{
    private const string PresencePath = "/api/internal/inventory/stock-presence";

    private readonly StockPresenceApiFactory _factory;

    public StockPresenceTests(StockPresenceApiFactory factory) => _factory = factory;

    private static string MdmKey(string secret) => $"{StockPresenceApiFactory.Mdm}:{secret}";

    private static async Task<(int Status, string Body)> AskAsync(
        HttpClient client, string? key, string? tenant, string? skuId, string? skuLevel, Action<HttpRequestMessage>? configure = null, Guid? correlation = null)
    {
        var query = new List<string>();
        if (skuId is not null) query.Add("skuId=" + Uri.EscapeDataString(skuId));
        if (skuLevel is not null) query.Add("skuLevel=" + Uri.EscapeDataString(skuLevel));
        using var request = new HttpRequestMessage(HttpMethod.Get, PresencePath + (query.Count > 0 ? "?" + string.Join("&", query) : ""));
        if (key is not null) request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", key);
        if (tenant is not null) request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
        if (correlation is not null) request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation.Value.ToString());
        configure?.Invoke(request);
        using var response = await client.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonElement> PresenceAsync(Guid tenant, Guid sku, string level = "Lsku", string? secret = null)
    {
        var (status, body) = await AskAsync(_factory.CreatePresenceClient(), MdmKey(secret ?? StockPresenceApiFactory.MdmSecret1), tenant.ToString(), sku.ToString(), level);
        Assert.True(status == 200, $"expected 200, got {status}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<bool> HasStockAsync(Guid tenant, Guid sku, string level = "Lsku")
        => (await PresenceAsync(tenant, sku, level)).GetProperty("hasNonZeroStock").GetBoolean();

    // ── the key: one refusal ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_refused_key_gets_the_same_401_with_the_same_body_byte_for_byte()
    {
        var s = new InventoryScenario(_factory);
        var https = _factory.CreatePresenceClient();
        var tenant = s.Tenant.ToString();
        var sku = s.Sku.ToString();
        var userToken = TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, TestTokens.AllPermissions);
        void Bearer(HttpRequestMessage r) => r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var refusals = new (string Case, Task<(int Status, string Body)> Call)[]
        {
            ("no key", AskAsync(https, null, tenant, sku, "Lsku")),
            ("wrong secret", AskAsync(https, MdmKey(StockPresenceApiFactory.NewSecret()), tenant, sku, "Lsku")),
            ("unknown identifier", AskAsync(https, "ditenunknownservice:" + StockPresenceApiFactory.MdmSecret1, tenant, sku, "Lsku")),
            ("valid key of another operation", AskAsync(https, $"{StockPresenceApiFactory.Other}:{StockPresenceApiFactory.OtherSecret}", tenant, sku, "Lsku")),
            ("another key's secret", AskAsync(https, MdmKey(StockPresenceApiFactory.OtherSecret), tenant, sku, "Lsku")),
            ("secret without identifier", AskAsync(https, StockPresenceApiFactory.MdmSecret1, tenant, sku, "Lsku")),
            ("identifier only", AskAsync(https, StockPresenceApiFactory.Mdm, tenant, sku, "Lsku")),
            ("empty key", AskAsync(https, "", tenant, sku, "Lsku")),
            ("user token only", AskAsync(https, null, tenant, sku, "Lsku", Bearer)),
            ("user token next to a valid key", AskAsync(https, MdmKey(StockPresenceApiFactory.MdmSecret1), tenant, sku, "Lsku", Bearer)),
            ("valid key over plain http", AskAsync(_factory.CreatePresenceClient(https: false), MdmKey(StockPresenceApiFactory.MdmSecret1), tenant, sku, "Lsku")),
            ("valid key, bad request — the key is judged first", AskAsync(https, MdmKey("x"), "not-a-uuid", "nope", "lsku"))
        };

        var expected = InternalServiceKeyFilter.RefusalBytes;
        foreach (var (name, call) in refusals)
        {
            var (status, body) = await call;
            Assert.True(status == 401, $"{name}: expected 401, got {status}: {body}");
            Assert.True(expected.AsSpan().SequenceEqual(System.Text.Encoding.UTF8.GetBytes(body)), $"{name}: body differs: {body}");
        }

        // The control: the same request with the right key is answered.
        Assert.Equal(200, (await AskAsync(https, MdmKey(StockPresenceApiFactory.MdmSecret1), tenant, sku, "Lsku")).Status);
    }

    [Fact]
    public void The_scope_middleware_does_not_cover_the_internal_path()
        => Assert.DoesNotContain(InventoryScopeResolutionMiddleware.ScopedBasePaths,
            p => PresencePath.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    // ── the request ──────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "sku", "Lsku")]
    [InlineData("not-a-uuid", "sku", "Lsku")]
    [InlineData("00000000-0000-0000-0000-000000000000", "sku", "Lsku")]
    [InlineData("tenant", null, "Lsku")]
    [InlineData("tenant", "not-a-uuid", "Lsku")]
    [InlineData("tenant", "sku", null)]
    [InlineData("tenant", "sku", "lsku")]
    [InlineData("tenant", "sku", "ITEM")]
    [InlineData("tenant", "sku", "Material")]
    public async Task A_malformed_tenant_sku_or_level_is_400_INVALID_REQUEST(string? tenant, string? sku, string? level)
    {
        var (status, body) = await AskAsync(_factory.CreatePresenceClient(), MdmKey(StockPresenceApiFactory.MdmSecret1),
            tenant == "tenant" ? Guid.NewGuid().ToString() : tenant,
            sku == "sku" ? Guid.NewGuid().ToString() : sku,
            level);
        Assert.True(status == 400, $"expected 400, got {status}: {body}");
        Assert.Equal("INVALID_REQUEST", JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    // ── the answer ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_never_stocked_sku_is_200_false_complete_with_no_quantity()
    {
        var sku = Guid.NewGuid();
        var answer = await PresenceAsync(Guid.NewGuid(), sku);
        Assert.False(answer.GetProperty("hasNonZeroStock").GetBoolean());
        Assert.Equal(sku.ToString(), answer.GetProperty("skuId").GetString());
        Assert.Equal("Lsku", answer.GetProperty("skuLevel").GetString());
        Assert.Equal("COMPLETE", answer.GetProperty("coverage").GetString());
        Assert.Equal("v1", answer.GetProperty("contractVersion").GetString());
        Assert.True(DateTimeOffset.UtcNow - DateTimeOffset.Parse(answer.GetProperty("asOf").GetString()!) < TimeSpan.FromMinutes(1));
        Assert.Equal(["asOf", "contractVersion", "coverage", "hasNonZeroStock", "skuId", "skuLevel"],
            answer.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("AVAILABLE")]
    [InlineData("QUALITY_INSPECTION")]
    [InlineData("QUARANTINE")]
    [InlineData("BLOCKED")]
    [InlineData("IN_TRANSIT")]
    public async Task Stock_held_only_in_one_status_is_present(string status)
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("7", "GOODS_RECEIPT_PO", status), 201);
        Assert.Equal(7m, await s.OnHandAsync(status: status));
        Assert.Equal(0m, (await s.OnHandAsync()) - 7m); // nothing in any other status

        Assert.True(await HasStockAsync(s.Tenant, s.Sku));
    }

    [Fact]
    public async Task Stock_that_is_wholly_reserved_is_present()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("5"), 201);
        await s.ReserveExpectingAsync(s.Reservation("5"), 201);
        Assert.Equal((5m, 5m, 0m), await s.AvailabilityAsync());

        Assert.True(await HasStockAsync(s.Tenant, s.Sku));
    }

    [Fact]
    public async Task Stock_in_another_company_of_the_same_tenant_is_present()
    {
        var s = new InventoryScenario(_factory);
        var other = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var site = LocationSeed.Node(s.Tenant, other, Diten.InventoryService.Domain.Enums.LocationType.Site, "PS-" + suffix, null);
        var warehouse = LocationSeed.Node(s.Tenant, other, Diten.InventoryService.Domain.Enums.LocationType.Warehouse, "PW-" + suffix, site);
        var storage = LocationSeed.Node(s.Tenant, other, Diten.InventoryService.Domain.Enums.LocationType.StorageLocation, "PL-" + suffix, warehouse);
        var bin = LocationSeed.Node(s.Tenant, other, Diten.InventoryService.Domain.Enums.LocationType.Bin, "PB-" + suffix, storage);
        LocationSeed.Insert(_factory.Database, site, warehouse, storage, bin);
        var body = s.Receipt("3");
        body["toWarehouseId"] = warehouse.Id.ToString("D");
        body["toLocationId"] = bin.Id.ToString("D");
        using var posted = await s.PostAsync(body, token: TestTokens.Mint(s.Tenant, other, s.User, TestTokens.AllPermissions));
        Assert.Equal(201, (int)posted.StatusCode);
        Assert.Equal(0m, await s.OnHandAsync()); // the scenario's own company holds nothing

        Assert.True(await HasStockAsync(s.Tenant, s.Sku));
    }

    [Fact]
    public async Task Another_tenants_stock_of_the_same_sku_is_not_seen()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("9", "GOODS_RECEIPT_PO", "QUARANTINE"), 201);
        Assert.True(await HasStockAsync(s.Tenant, s.Sku)); // the control: its own tenant sees it

        var otherTenant = Guid.NewGuid();
        _factory.TrackTenant(otherTenant);
        Assert.False(await HasStockAsync(otherTenant, s.Sku));
    }

    [Fact]
    public async Task A_sku_whose_balance_went_back_to_zero_is_absent()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("4"), 201);
        await s.PostExpectingAsync(s.Issue("4"), 201);
        Assert.Single(s.Persisted("inventory_balances")); // the row exists, at zero
        Assert.Equal(0m, await s.OnHandAsync());

        Assert.False(await HasStockAsync(s.Tenant, s.Sku));
    }

    /// <summary>
    /// The ledger never writes a negative row (DEC-INV-12, INSUFFICIENT_STOCK) — measured, not assumed — so the row is written the
    /// way only legacy / migrated data could carry it. A negative balance is non-zero.
    /// </summary>
    [Fact]
    public async Task A_negative_balance_is_present()
    {
        var s = new InventoryScenario(_factory);
        var refused = await s.PostExpectingAsync(s.Issue("1"), 422);
        Assert.Equal("INSUFFICIENT_STOCK", refused.GetProperty("error").GetProperty("code").GetString());

        InsertBalanceRow(s.Tenant, s.LegalEntity, s.Item, s.Sku, "Lsku", -2m);
        Assert.True(await HasStockAsync(s.Tenant, s.Sku));
    }

    [Fact]
    public async Task A_closed_balance_row_is_not_stock()
    {
        var s = new InventoryScenario(_factory);
        InsertBalanceRow(s.Tenant, s.LegalEntity, s.Item, s.Sku, "Lsku", 6m, deleted: true);
        Assert.False(await HasStockAsync(s.Tenant, s.Sku));
    }

    // ── Item (1.1.0) ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Item_level_is_accepted_and_the_level_is_part_of_the_question()
    {
        var s = new InventoryScenario(_factory);
        var never = await PresenceAsync(s.Tenant, s.Sku, "Item");
        Assert.False(never.GetProperty("hasNonZeroStock").GetBoolean());
        Assert.Equal("Item", never.GetProperty("skuLevel").GetString());

        // An Item-level row as WP-0173-PM1 will write it (skuId == itemId) — presence finds it at Item, not at another level.
        InsertBalanceRow(s.Tenant, s.LegalEntity, s.Sku, s.Sku, "Item", 12m);
        Assert.True(await HasStockAsync(s.Tenant, s.Sku, "Item"));
        Assert.False(await HasStockAsync(s.Tenant, s.Sku, "Lsku"));
    }

    /// <summary>
    /// Measurement for the evidence (WP-0173-PM2 NE-1): an Item-level MOVEMENT is refused today — at request validation, 400
    /// INVALID_REQUEST, before PRODUCT-MASTER is asked — and PM2 does not change that (Item handling is WP-0173-PM1).
    /// </summary>
    [Fact]
    public async Task An_Item_level_movement_is_still_refused_with_400_before_product_master()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("1");
        body["skuLevel"] = "Item";
        var refused = await s.PostExpectingAsync(body, 400);
        Assert.Equal("INVALID_REQUEST", refused.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("skuLevel", refused.GetProperty("error").GetProperty("message").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    // ── rotation ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Both_secrets_of_the_rotation_overlap_are_accepted()
    {
        var sku = Guid.NewGuid();
        Assert.False((await PresenceAsync(Guid.NewGuid(), sku, secret: StockPresenceApiFactory.MdmSecret1)).GetProperty("hasNonZeroStock").GetBoolean());
        Assert.False((await PresenceAsync(Guid.NewGuid(), sku, secret: StockPresenceApiFactory.MdmSecret2)).GetProperty("hasNonZeroStock").GetBoolean());
    }

    // ── audit + logs ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_call_is_audited_with_the_key_identifier_and_no_secret_is_logged_or_audited()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("2"), 201);
        var https = _factory.CreatePresenceClient();
        var ok = Guid.NewGuid();
        var refused = Guid.NewGuid();
        var otherKey = Guid.NewGuid();
        var invalid = Guid.NewGuid();
        Assert.Equal(200, (await AskAsync(https, MdmKey(StockPresenceApiFactory.MdmSecret2), s.Tenant.ToString(), s.Sku.ToString(), "Lsku", correlation: ok)).Status);
        Assert.Equal(401, (await AskAsync(https, MdmKey(StockPresenceApiFactory.MdmSecret1 + "x"), s.Tenant.ToString(), s.Sku.ToString(), "Lsku", correlation: refused)).Status);
        Assert.Equal(401, (await AskAsync(https, $"{StockPresenceApiFactory.Other}:{StockPresenceApiFactory.OtherSecret}", s.Tenant.ToString(), s.Sku.ToString(), "Lsku", correlation: otherKey)).Status);
        Assert.Equal(400, (await AskAsync(https, MdmKey(StockPresenceApiFactory.MdmSecret1), s.Tenant.ToString(), s.Sku.ToString(), "lsku", correlation: invalid)).Status);

        var rows = _factory.Collection("inventory_audit")
            .Find(new BsonDocument { { "TenantId", s.Tenant.ToString() }, { "Action", "INTERNAL_STOCK_PRESENCE_READ" } })
            .ToList()
            .ToDictionary(d => Guid.Parse(d["CorrelationId"].AsString));

        var answered = rows[ok];
        Assert.Equal(StockPresenceApiFactory.Mdm, answered["KeyIdentifier"].AsString);
        Assert.Equal(s.Sku.ToString(), answered["SkuId"].AsString);
        Assert.Equal("Lsku", answered["SkuLevel"].AsString);
        Assert.Equal(200, answered["StatusCode"].AsInt32);
        Assert.Equal("present", answered["Outcome"].AsString);
        Assert.Equal(BsonNull.Value, answered["LegalEntityId"]);
        Assert.Equal(BsonNull.Value, rows[refused]["KeyIdentifier"]); // a wrong secret names no key
        Assert.Equal("unauthorized", rows[refused]["Outcome"].AsString);
        Assert.Equal(StockPresenceApiFactory.Other, rows[otherKey]["KeyIdentifier"].AsString); // authenticated, not authorised
        Assert.Equal(401, rows[otherKey]["StatusCode"].AsInt32);
        Assert.Equal("invalid-request", rows[invalid]["Outcome"].AsString);
        Assert.Equal(BsonNull.Value, rows[invalid]["SkuLevel"]); // an unknown level is not stored raw

        var everything = string.Join("\n", _factory.Logs.Lines) + "\n" + string.Join("\n", rows.Values.Select(r => r.ToJson()));
        foreach (var secret in StockPresenceApiFactory.AllSecrets)
        {
            Assert.DoesNotContain(secret, everything);
        }

        Assert.Contains(_factory.Logs.Lines, l => l.Contains("Stock presence answered", StringComparison.Ordinal) && l.Contains(StockPresenceApiFactory.Mdm, StringComparison.Ordinal));
        Assert.Contains(_factory.Logs.Lines, l => l.Contains("refused: unknown key", StringComparison.Ordinal));
    }

    private void InsertBalanceRow(Guid tenant, Guid legalEntity, Guid item, Guid sku, string level, decimal onHand, bool deleted = false)
        => _factory.Collection("inventory_balances").InsertOne(new BsonDocument
        {
            { "_id", Guid.NewGuid().ToString() },
            { "TenantId", tenant.ToString() },
            { "LegalEntityId", legalEntity.ToString() },
            { "ItemId", item.ToString() },
            { "SkuId", sku.ToString() },
            { "SkuLevel", level },
            { "WarehouseId", "wh-legacy-" + Guid.NewGuid().ToString("N")[..6] },
            { "LocationId", BsonNull.Value },
            { "LotId", BsonNull.Value },
            { "StockStatus", nameof(StockStatus.Available) },
            { "OwnerId", BsonNull.Value },
            { "ProjectId", BsonNull.Value },
            { "OnHand", new Decimal128(onHand) },
            { "Reserved", new Decimal128(0m) },
            { "Available", new Decimal128(onHand) },
            { "InTransit", new Decimal128(0m) },
            { "Blocked", new Decimal128(0m) },
            { "QualityInspection", new Decimal128(0m) },
            { "BaseUomId", "EA" },
            { "IsDeleted", deleted },
            { "Version", 1 },
            { "CreatedAt", DateTime.UtcNow }
        });
}

/// <summary>After the rotation: the secret that left the list is refused; the one that stayed is answered.</summary>
public sealed class StockPresenceRotationTests : IClassFixture<RotatedStockPresenceApiFactory>
{
    private readonly RotatedStockPresenceApiFactory _factory;

    public StockPresenceRotationTests(RotatedStockPresenceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_secret_removed_from_the_list_no_longer_opens_the_operation()
    {
        var client = _factory.CreatePresenceClient();
        async Task<int> Ask(string secret)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/inventory/stock-presence?skuId={Guid.NewGuid()}&skuLevel=Gsku");
            request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", $"{StockPresenceApiFactory.Mdm}:{secret}");
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", Guid.NewGuid().ToString());
            using var response = await client.SendAsync(request);
            return (int)response.StatusCode;
        }

        Assert.Equal(401, await Ask(StockPresenceApiFactory.MdmSecret1));
        Assert.Equal(200, await Ask(StockPresenceApiFactory.MdmSecret2));
    }
}

/// <summary>A store that cannot be read answers 503 INVENTORY_UNAVAILABLE — never a partial or a guessed answer.</summary>
public sealed class StockPresenceUnavailableTests : IClassFixture<UnreadableStockPresenceApiFactory>
{
    private readonly UnreadableStockPresenceApiFactory _factory;

    public StockPresenceUnavailableTests(UnreadableStockPresenceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task An_unreadable_store_is_503_INVENTORY_UNAVAILABLE()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/inventory/stock-presence?skuId={Guid.NewGuid()}&skuLevel=Item");
        request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", $"{StockPresenceApiFactory.Mdm}:{StockPresenceApiFactory.MdmSecret1}");
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", Guid.NewGuid().ToString());
        using var response = await _factory.CreatePresenceClient().SendAsync(request);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("INVENTORY_UNAVAILABLE", body.GetProperty("error").GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("hasNonZeroStock", out _));
    }
}
