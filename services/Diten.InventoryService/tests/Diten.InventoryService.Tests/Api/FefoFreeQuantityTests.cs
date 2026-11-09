using System.Text.Json;
using Diten.InventoryService.Tests.Expiry;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-38 (DL-3, §8.5, Q-01 = A) — the FEFO hook decides on the FREE quantity: lot stock reserved for another outbound is not in
/// the order, so issuing a later lot with its own reservation is no FEFO bypass. Company FEFO policy "Engelle" (<c>Block</c>); in W the
/// early lot L1 20 and the late lot L2 20; R_B = L2 / W 20 (outbound B). The FEFO order read (<c>getFefoOrder</c>) keeps
/// <c>basis: onHandAvailable</c> — the known difference. Runs on the expiry host (its tenant clean-up covers the FEFO documents).
/// </summary>
public sealed class FefoFreeQuantityTests : IClassFixture<ExpiryApiFactory>
{
    private readonly ExpiryApiFactory _factory;

    public FefoFreeQuantityTests(ExpiryApiFactory factory) => _factory = factory;

    private sealed record World(ExpiryScenario S, ScopedReservationWorld W, string L1, string L2, string RB);

    private async Task<World> WorldAsync()
    {
        var s = new ExpiryScenario(_factory);
        Assert.Equal(200, (await s.PutPolicyAsync(ExpiryScenario.Policy(fefo: "Block"), "\"0\"")).Status);
        var w = ScopedReservationWorld.Of(s.Lots);
        var l1 = await s.LotAsync("2028-01-31T00:00:00Z");
        var l2 = await s.LotAsync("2028-06-30T00:00:00Z");
        await w.ReceiveAsync("20", l1);
        await w.ReceiveAsync("20", l2);
        var rb = await w.ReserveOkAsync("20", w.W, l2, sourceDocumentId: "OB-B");
        return new World(s, w, l1, l2, rb);
    }

    private static Dictionary<string, object?> IssueB(World x) => ScopedReservationWorld.Naming(x.W.Issue("10", x.L2), x.RB);

    private static async Task AssertViolationAsync(World x)
    {
        var refused = await x.W.PostAsync(IssueB(x), 422);
        Assert.Equal("FEFO_ORDER_VIOLATION", ScopedReservationWorld.Code(refused));
        Assert.Equal(x.L1, ScopedReservationWorld.Detail(refused, "firstEligibleLotId"));
        Assert.Equal(20m, x.W.ReservationDocument(x.RB)["RemainingQuantity"].ToDecimal());
    }

    private static async Task AssertCompliantAsync(World x, string remaining = "10.000")
    {
        var posted = await x.W.PostAsync(IssueB(x), 201);
        Assert.Equal("Compliant", posted.GetProperty("expiryChecks").GetProperty("fefo").GetString());
        Assert.Equal(remaining, posted.GetProperty("reservationConsumption").GetProperty("remainingQuantity").GetString());
    }

    private static async Task<List<(string LotId, decimal Available)>> FefoOrderAsync(World x)
    {
        var query = $"/api/expiry/fefo-order?warehouseId={x.S.Ledger.Warehouse}&itemId={x.S.Ledger.Item}&skuId={x.S.Ledger.Sku}&skuLevel=Lsku";
        var (status, body, _) = await x.S.SendAsync(HttpMethod.Get, query, x.S.Mint(x.S.Qa, ["expiry.fefo-order.read"]));
        Assert.Equal(200, status);
        Assert.Equal("onHandAvailable", body.GetProperty("basis").GetString());
        return body.GetProperty("eligible").EnumerateArray()
            .Select(e => (e.GetProperty("lotId").GetString()!, Available(e)))
            .ToList();
    }

    private static decimal Available(JsonElement eligible) => eligible.GetProperty("available").GetDecimal();

    /// <summary>
    /// (a) — S-31: the hook back on the <c>onHandAvailable</c> base (<c>FefoAsync</c> gives no free base) → 422 FEFO_ORDER_VIOLATION, red.
    /// L1 is wholly reserved for outbound A, so the issue of L2 with R_B is compliant; getFefoOrder still lists L1 first with its 20.
    /// </summary>
    [Fact]
    public async Task Early_lot_reserved_for_another_outbound_is_not_a_violation()
    {
        var x = await WorldAsync();
        await x.W.ReserveOkAsync("20", x.W.W, x.L1, sourceDocumentId: "OB-A");

        Assert.Equal([(x.L1, 20m), (x.L2, 20m)], await FefoOrderAsync(x));
        await AssertCompliantAsync(x);
        Assert.Empty(x.S.Audit("expiry.fefo.refused"));
        x.W.AssertInvariant("AC-38 a");
    }

    /// <summary>(b) L1 reserved by no one and free → the same issue is a violation (the 1.3.0 behaviour).</summary>
    [Fact]
    public async Task Free_early_lot_is_a_violation()
    {
        var x = await WorldAsync();
        await AssertViolationAsync(x);
    }

    /// <summary>(c) L1 partly reserved (R_A 15, free 5) → still eligible and first: a violation as in (b).</summary>
    [Fact]
    public async Task Partly_reserved_early_lot_is_still_first()
    {
        var x = await WorldAsync();
        await x.W.ReserveOkAsync("15", x.W.W, x.L1, sourceDocumentId: "OB-A");
        await AssertViolationAsync(x);
        x.W.AssertInvariant("AC-38 c");
    }

    /// <summary>
    /// (d) — S-38: the lot reader keeps only <c>Status == ACTIVE</c> (or reads the <c>L:</c> position) → 201, red. R_A = L1 / W 20 due in 2 s
    /// is created AFTER R_B (the create path closes due reservations), then 2.5 s pass with nothing touching L1 / the SKU (sweeper off): R_A
    /// is ACTIVE and <c>L:W:L1</c> still holds 20, but the read rule does not count it → L1 is free and first: 422, no movement, no FEFO
    /// snapshot, and R_A is still ACTIVE (the refusal wrote nothing — the verdict came from the filter, not from an expiry close).
    /// </summary>
    [Fact]
    public async Task Due_unclosed_reservation_does_not_hide_early_lot()
    {
        var x = await WorldAsync();
        var ra = await x.W.ReserveOkAsync("20", x.W.W, x.L1, sourceDocumentId: "OB-A", expiresAt: ScopedReservationWorld.Later(TimeSpan.FromSeconds(2)));
        await Task.Delay(TimeSpan.FromMilliseconds(2500));
        Assert.Equal("Active", x.W.ReservationDocument(ra)["Status"].AsString);
        Assert.Equal(20m, x.W.Scope(x.W.W, x.L1).Reserved);
        var movements = x.W.Count("inventory_transactions");

        await AssertViolationAsync(x);

        Assert.Equal(movements, x.W.Count("inventory_transactions"));
        Assert.Empty(x.S.Persisted("expiry_fefo_issue_snapshots"));
        Assert.Equal("Active", x.W.ReservationDocument(ra)["Status"].AsString);
    }

    /// <summary>(d) control — the same R_A expiring in 2099 hides L1 as in (a): 201.</summary>
    [Fact]
    public async Task Reservation_not_yet_due_hides_early_lot()
    {
        var x = await WorldAsync();
        await x.W.ReserveOkAsync("20", x.W.W, x.L1, sourceDocumentId: "OB-A", expiresAt: "2099-01-01T00:00:00.000Z");
        await AssertCompliantAsync(x);
        x.W.AssertInvariant("AC-38 d control");
    }

    /// <summary>
    /// MOD-0175-FU01 DL-1 interaction (47762de1f) — L1's AVAILABLE 20 is wholly reserved for outbound A and 5 more of L1 come in through a
    /// GOODS_RECEIPT_PO into QUARANTINE (a hold opens). Held stock is not free either: the issue of L2 with R_B is compliant. The REVERSAL of
    /// that receipt voids the hold (FU01 close, no stock written by MOD-0175) — L1 holds nothing free still, compliant again. Only when R_A is
    /// released does L1 become free and first: 422.
    /// </summary>
    [Fact]
    public async Task Quarantine_hold_closed_by_source_reversal_does_not_free_a_reserved_early_lot()
    {
        var x = await WorldAsync();
        var ra = await x.W.ReserveOkAsync("20", x.W.W, x.L1, sourceDocumentId: "OB-A");
        var receipt = x.S.Lots.Receipt("5", x.L1, toStatus: "QUARANTINE");
        var receiptId = await x.S.Lots.PostOkAsync(receipt);
        Assert.Equal("Open", Assert.Single(x.S.Persisted("quarantine_holds"))["Status"].AsString);

        await AssertCompliantAsync(x);

        await x.S.Lots.PostOkAsync(x.S.Lots.Reversal(receipt, receiptId));
        Assert.Equal("Voided", Assert.Single(x.S.Persisted("quarantine_holds"))["Status"].AsString);
        await AssertCompliantAsync(x, remaining: "0.000");

        // R_B is consumed and L2 is empty: release R_A, then outbound B reserves fresh L2 stock — L1 is free now and first
        await x.W.ReleaseAsync(ra, "W/\"1\"", 200);
        await x.W.ReceiveAsync("10", x.L2);
        var rb2 = await x.W.ReserveOkAsync("10", x.W.W, x.L2, sourceDocumentId: "OB-B2");
        var refused = await x.W.PostAsync(ScopedReservationWorld.Naming(x.W.Issue("10", x.L2), rb2), 422);
        Assert.Equal("FEFO_ORDER_VIOLATION", ScopedReservationWorld.Code(refused));
        Assert.Equal(x.L1, ScopedReservationWorld.Detail(refused, "firstEligibleLotId"));
        x.W.AssertInvariant("FU01 interaction");
    }
}
