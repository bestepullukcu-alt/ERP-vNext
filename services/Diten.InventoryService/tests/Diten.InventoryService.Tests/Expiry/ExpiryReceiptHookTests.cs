using System.Text.Json;
using MongoDB.Bson;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F2 — the minimum-remaining-shelf-life hook of GOODS_RECEIPT_PO (pack §8.3.2, §8.3.4 row "Inbound"; KF-C; CT-F1 / CT-F5), through
/// the REAL posting endpoint. Posting date 2026-09-30; the SKU minimum is 90 days unless a test says otherwise. CT-F1: the ledger NEVER turns
/// the requested status — AutoQuarantine into AVAILABLE is a 422 carrying <c>requiredToStockStatus = QUARANTINE</c>, the caller resubmits.
/// </summary>
public sealed class ExpiryReceiptHookTests : IClassFixture<ExpiryApiFactory>
{
    private const string BelowExpiry = "2026-11-09T00:00:00Z"; // 40 days left at the posting date
    private const string OkExpiry = "2027-06-30T00:00:00Z";    // 273 days left

    private readonly ExpiryApiFactory _factory;

    public ExpiryReceiptHookTests(ExpiryApiFactory factory) => _factory = factory;

    private ExpiryScenario World(int? minimum = 90)
    {
        var s = new ExpiryScenario(_factory);
        if (minimum is not null) _factory.ShelfLife[s.Ledger.Sku] = (null, null, minimum);
        return s;
    }

    private static string MinRemaining(JsonElement posted) => posted.GetProperty("expiryChecks").GetProperty("minRemaining").GetString()!;

    private static List<BsonDocument> Below(ExpiryScenario s)
        => s.Persisted("expiry_exceptions").Where(d => d["Type"].AsString == "BelowMinRemaining").ToList();

    // ── not applied ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_SKU_without_a_minimum_posts_NotDefined()
    {
        var s = World(minimum: null);
        // K3 sabotage (e): drop the null guard of the minimum → the rule treats null as 0 / throws and this answer changes
        var posted = await s.Lots.PostAsync(s.Lots.Receipt("5", await s.LotAsync(BelowExpiry)), 201);
        Assert.Equal("NotDefined", MinRemaining(posted));
        Assert.Empty(Below(s));
    }

    [Fact]
    public async Task A_lot_without_expiry_posts_NotApplicable()
    {
        var s = World();
        var lot = await s.Lots.CreateLotAsync();
        await s.Lots.ReleaseAsync(lot);
        Assert.Equal("NotApplicable", MinRemaining(await s.Lots.PostAsync(s.Lots.Receipt("5", lot), 201)));
    }

    [Fact]
    public async Task Enough_shelf_life_posts_Ok_and_other_receipt_types_are_not_checked()
    {
        var s = World();
        Assert.Equal("Ok", MinRemaining(await s.Lots.PostAsync(s.Lots.Receipt("5", await s.LotAsync(OkExpiry)), 201)));

        // CT-F5: KF-C is GOODS_RECEIPT_PO only — an OPENING_BALANCE of a below-minimum lot is not checked
        var opening = await s.Lots.PostAsync(s.Lots.Receipt("5", await s.LotAsync(BelowExpiry), movementType: "OPENING_BALANCE"), 201);
        Assert.False(opening.TryGetProperty("expiryChecks", out _));
        Assert.Empty(Below(s));
    }

    // ── §8.3.4 Inbound × Otomatik karantina (default) ────────────────────────────────────────────────────

    [Fact]
    public async Task AutoQuarantine_refuses_AVAILABLE_with_requiredToStockStatus_and_the_QUARANTINE_resubmission_posts_with_exception_and_hold()
    {
        var s = World();
        var lot = await s.LotAsync(BelowExpiry);

        // K3 sabotage (g): the server turns AVAILABLE into QUARANTINE itself → 201 here, and the requested status is not what was posted
        var refused = await s.Lots.PostAsync(s.Lots.Receipt("5", lot), 422);
        Assert.Equal("LOT_BELOW_MIN_REMAINING_SHELF_LIFE", ExpiryScenario.Code(refused));
        var details = refused.GetProperty("error").GetProperty("details");
        Assert.Equal("QUARANTINE", details.GetProperty("requiredToStockStatus").GetString());
        Assert.Equal(40, details.GetProperty("remainingDays").GetInt32());
        Assert.Equal(90, details.GetProperty("requiredDays").GetInt32());
        Assert.False(details.TryGetProperty("appliedToStockStatus", out _));
        Assert.Empty(s.Persisted("inventory_transactions"));
        Assert.Equal("AutoQuarantine", Assert.Single(s.Audit("expiry.receipt.refused"))["Detail"]["mode"].AsString);

        var posted = await s.Lots.PostAsync(s.Lots.Receipt("5", lot, toStatus: "QUARANTINE"), 201);
        var tx = posted.GetProperty("transactionId").GetString()!;
        Assert.Equal("Warned", MinRemaining(posted));
        var exceptionId = Assert.Single(posted.GetProperty("expiryChecks").GetProperty("exceptionIds").EnumerateArray()).GetString();
        var c = Assert.Single(Below(s));
        Assert.Equal(exceptionId, c["_id"].AsString);
        Assert.True(c["OriginUserId"].IsBsonNull); // CT-F10: no origin user for BELOW_MIN
        Assert.Equal("40", c["Facts"]["remainingDays"].AsString);
        Assert.Equal("QUARANTINE", c["Facts"]["requestedStockStatus"].AsString);
        Assert.Single(s.Persisted("quarantine_holds"), h => h["SourceMovementId"].AsString == tx); // the MOD-0175 intake
        Assert.Equal(5m, await s.Ledger.OnHandAsync(status: "QUARANTINE")); // the requested status, unchanged
        Assert.Equal(0m, await s.Ledger.OnHandAsync(status: "AVAILABLE"));
    }

    [Fact]
    public async Task FIX1_c_a_receipt_claiming_sourceModule_MOD_0175_is_checked_like_any_other()
    {
        // WP-0176-F2-FIX1 (CT O-F2-2) — K3 sabotage (s1): put the sourceModule exemption back → 201 into AVAILABLE below the minimum
        var s = World();
        var claimed = s.Lots.Receipt("5", await s.LotAsync(BelowExpiry));
        claimed["sourceModule"] = "MOD-0175";
        var refused = await s.Lots.PostAsync(claimed, 422);
        Assert.Equal("LOT_BELOW_MIN_REMAINING_SHELF_LIFE", ExpiryScenario.Code(refused));
        Assert.Equal("QUARANTINE", refused.GetProperty("error").GetProperty("details").GetProperty("requiredToStockStatus").GetString());
        Assert.Empty(s.Persisted("inventory_transactions"));
        Assert.Empty(Below(s));
    }

    [Fact]
    public async Task AutoQuarantine_into_QUALITY_INSPECTION_posts_with_exception()
    {
        var s = World();
        var posted = await s.Lots.PostAsync(s.Lots.Receipt("5", await s.LotAsync(BelowExpiry), toStatus: "QUALITY_INSPECTION"), 201);
        Assert.Equal("Warned", MinRemaining(posted));
        Assert.Equal(5m, await s.Ledger.OnHandAsync(status: "QUALITY_INSPECTION"));
        Assert.Single(Below(s));
    }

    // ── §8.3.4 Inbound × Reddet / Uyar ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_refuses_without_requiredToStockStatus_whatever_the_status()
    {
        var s = World();
        Assert.Equal(200, (await s.PutPolicyAsync(ExpiryScenario.Policy(below: "Reject"), "\"0\"")).Status);
        var lot = await s.LotAsync(BelowExpiry);
        foreach (var status in new string?[] { null, "QUARANTINE" })
        {
            var refused = await s.Lots.PostAsync(s.Lots.Receipt("5", lot, toStatus: status), 422);
            Assert.Equal("LOT_BELOW_MIN_REMAINING_SHELF_LIFE", ExpiryScenario.Code(refused));
            Assert.False(refused.GetProperty("error").GetProperty("details").TryGetProperty("requiredToStockStatus", out _));
        }

        Assert.Empty(s.Persisted("inventory_transactions"));
        Assert.Empty(Below(s));
    }

    [Fact]
    public async Task Warn_posts_into_AVAILABLE_with_exception()
    {
        var s = World();
        Assert.Equal(200, (await s.PutPolicyAsync(ExpiryScenario.Policy(below: "Warn"), "\"0\"")).Status);
        var posted = await s.Lots.PostAsync(s.Lots.Receipt("5", await s.LotAsync(BelowExpiry)), 201);
        Assert.Equal(5m, await s.Ledger.OnHandAsync(status: "AVAILABLE"));
        Assert.Equal("Warned", MinRemaining(posted));
        Assert.Equal("Warn", Assert.Single(Below(s))["Facts"]["mode"].AsString);
    }

    // ── no reservation effect (pack §8.3.4) ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_hooks_do_not_touch_reservations()
    {
        var s = World();
        Assert.Equal("Ok", MinRemaining(await s.Lots.PostAsync(s.Lots.Receipt("5", await s.LotAsync(OkExpiry)), 201)));
        Assert.Empty(s.Persisted("inventory_reservations"));
        Assert.All(s.Persisted("inventory_sku_positions"), p => Assert.Equal(0m, p["Reserved"].AsDecimal));
    }
}
