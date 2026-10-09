using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 Q5 (O-Q4-2) — QUARANTINE-BUNDLE <c>getLotQualityStatus</c>, through the REAL API with the contract-validating client
/// (every answer is checked against <c>LotQualityStatus</c> / the Error schema of the frozen contract):
/// <list type="bullet">
/// <item>an open hold → its QUARANTINE quantity and the hold in <c>openHolds</c>; no blocked stock, no pending release;</item>
/// <item>an approved Reject → the quantity moves to <c>blockedQuantity</c> / <c>openBlocked</c>, the hold is no longer open;</item>
/// <item>a gated lot release (KQ-I, TRACE 202) → <c>pendingLotRelease</c> = that decision until it is applied, then absent;</item>
/// <item>an unknown lot → 404 <c>UNKNOWN_LOT</c>; a caller without <c>quarantine.holds.read</c> → 403.</item>
/// </list>
/// </summary>
public sealed class QuarantineQualityStatusTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public QuarantineQualityStatusTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static Task<JsonElement> StatusAsync(QuarantineScenario q, string lot, int expected = 200, string? token = null)
        => q.ExpectAsync(HttpMethod.Get, $"/api/quarantine/lots/{lot}/quality-status", null, expected, token: token);

    [Fact]
    public async Task An_open_hold_is_reported_with_its_quarantined_quantity()
    {
        var q = new QuarantineScenario(_factory);
        var lot = await q.ReleasedLotAsync();
        var (_, hold) = await q.HoldAsync("120", lot);

        var status = await StatusAsync(q, lot);
        Assert.Equal(lot, Id(status, "lotId"));
        Assert.False(status.GetProperty("retestDue").GetBoolean());
        Assert.Equal(120m, QuarantineScenario.D(status.GetProperty("quarantinedQuantity")));
        Assert.Equal(0m, QuarantineScenario.D(status.GetProperty("qualityInspectionQuantity")));
        Assert.Equal(0m, QuarantineScenario.D(status.GetProperty("blockedQuantity")));
        Assert.Equal(Id(hold, "holdId"), Id(Assert.Single(status.GetProperty("openHolds").EnumerateArray()), "holdId"));
        Assert.Empty(status.GetProperty("openBlocked").EnumerateArray());
        Assert.False(status.TryGetProperty("pendingLotRelease", out _));
    }

    [Fact]
    public async Task An_approved_reject_moves_the_lot_quantity_to_blocked()
    {
        var q = new QuarantineScenario(_factory);
        var lot = await q.ReleasedLotAsync();
        var (_, hold) = await q.HoldAsync("100", lot);
        await q.DecideAndApproveAsync(q.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Reject", 100m)], "DEVIATION"));

        var status = await StatusAsync(q, lot);
        Assert.Equal(0m, QuarantineScenario.D(status.GetProperty("quarantinedQuantity")));
        Assert.Equal(100m, QuarantineScenario.D(status.GetProperty("blockedQuantity")));
        Assert.Empty(status.GetProperty("openHolds").EnumerateArray());
        Assert.Equal(lot, Id(Assert.Single(status.GetProperty("openBlocked").EnumerateArray()).GetProperty("lot"), "lotId"));
    }

    [Fact]
    public async Task A_gated_lot_release_is_pending_until_it_is_applied()
    {
        var q = new QuarantineScenario(_factory);
        LotGate.On(_factory.Database, q.Tenant, q.Ledger.LegalEntity);
        var lot = await q.Lots.CreateLotAsync();
        var decisionId = Id(await q.Lots.SetStatusAsync(lot, "Released", expected: 202), "pendingRequestId");

        var pending = (await StatusAsync(q, lot)).GetProperty("pendingLotRelease");
        Assert.Equal(decisionId, Id(pending, "decisionId"));
        Assert.StartsWith("QKR-", Id(pending, "decisionNumber"));

        await q.SubmitAsync(decisionId);
        Assert.Equal(decisionId, Id((await StatusAsync(q, lot)).GetProperty("pendingLotRelease"), "decisionId")); // still waiting
        await q.OutcomeAsync(decisionId);
        Assert.False((await StatusAsync(q, lot)).TryGetProperty("pendingLotRelease", out _));
    }

    [Fact]
    public async Task An_unknown_lot_is_404_and_a_caller_without_holds_read_is_refused()
    {
        var q = new QuarantineScenario(_factory);
        var unknown = await StatusAsync(q, Guid.NewGuid().ToString("D"), expected: 404);
        Assert.Equal("UNKNOWN_LOT", QuarantineScenario.Code(unknown));

        var lot = await q.ReleasedLotAsync();
        var noRead = TestTokens.Mint(q.Tenant, q.Ledger.LegalEntity, q.Ledger.User, TestTokens.AllPermissions);
        await StatusAsync(q, lot, expected: 403, token: noRead);
    }
}
