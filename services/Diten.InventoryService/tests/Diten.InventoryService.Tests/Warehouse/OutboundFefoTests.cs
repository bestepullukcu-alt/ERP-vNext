using Diten.InventoryService.Domain.Expiry.Enums;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.OutboundScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-4 AC-28 (pack §8.17 step 3, r21–r24) — the lot override: a lot outside the FEFO eligible list is refused with FEFO's reason; a lot
/// that is not the first FREE lot is decided by the company's FEFO outbound mode (<c>Block</c> → 422 <c>FEFO_ORDER_VIOLATION</c>,
/// <c>RequireReason</c> — also the default without a policy row — → reason 10–1000 or 422 <c>FEFO_BYPASS_REASON_REQUIRED</c>, <c>Warn</c> →
/// optional); a reason over 1000 is 400 <c>INVALID_REQUEST</c> in every mode. Every refusal writes nothing and reserves nothing.
/// Fixture: L1 (2027-01-31) 30 EA on A-02, L2 (2027-03-31) 50 EA on A-01.
/// </summary>
public sealed class OutboundFefoTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public OutboundFefoTests(WarehouseApiFactory factory) => _factory = factory;

    private async Task<(OutboundScenario S, string L1, string L2)> WorldAsync(FefoOutboundMode? mode = null)
    {
        var s = await new OutboundScenario(_factory).ReadyAsync();
        if (mode is { } m) s.Policy(m);
        var l1 = s.Lot("2027-01-31");
        var l2 = s.Lot("2027-03-31");
        await s.ReceiveAsync(30, s.A02, l1);
        await s.ReceiveAsync(50, s.A01, l2);
        return (s, l1, l2);
    }

    private static void NothingWritten(OutboundScenario s)
    {
        Assert.Empty(s.StoredOutbounds());
        Assert.Empty(s.OutboundEvents());
        Assert.Empty(s.Reservations());
        Assert.Empty(s.CentralRows("CreateOutboundShipmentCommand"));
    }

    private static string? BypassReason(System.Text.Json.JsonElement outbound)
        => Allocations(outbound).Single().TryGetProperty("fefoBypassReason", out var r) ? r.GetString() : null;

    /// <summary>Sabotage (ch): the reason check removed → the reason-less override is 201 (red). Runs without a policy row (default RequireReason).</summary>
    [Fact]
    public async Task AC28_require_reason_is_the_default_and_a_deviation_needs_10_characters()
    {
        var (s, l1, l2) = await WorldAsync();

        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", await ErrorCode(s.CreateAsync(s.Body(s.Line(20, l2))), 422));
        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", await ErrorCode(s.CreateAsync(s.Body(s.Line(20, l2, "123456789"))), 422));
        Assert.Equal("FEFO_BYPASS_REASON_REQUIRED", await ErrorCode(s.CreateAsync(s.Body(s.Line(20, l2, "          "))), 422));
        NothingWritten(s);

        var deviated = await s.CreatedAsync(s.Line(20, l2, Reason10));
        Assert.Equal([(l2, 20m, 2)], AllocationSummary(deviated));          // the whole line from L2
        Assert.Equal(Reason10, BypassReason(deviated));

        var first = await s.CreatedAsync(s.Line(10, l1));                    // the first eligible lot: no reason asked, none written
        Assert.Equal([(l1, 10m, 1)], AllocationSummary(first));
        Assert.Null(BypassReason(first));
    }

    [Fact]
    public async Task AC28_a_recalled_lot_and_a_lot_outside_the_FEFO_list_are_refused()
    {
        var (s, _, l2) = await WorldAsync();
        s.P.Recall(l2);
        var empty = s.Lot("2027-02-15");                                     // registered, no stock → not in the eligible list

        var recalled = await Expect(s.CreateAsync(s.Body(s.Line(10, l2, Reason10))), 422);
        Assert.Equal("LOT_UNDER_RECALL", Code(recalled));
        Assert.Equal("WMS_LOT_NOT_ALLOCATABLE", await ErrorCode(s.CreateAsync(s.Body(s.Line(10, empty, Reason10))), 422));
        Assert.Equal("WMS_LOT_NOT_ALLOCATABLE", await ErrorCode(s.CreateAsync(s.Body(s.Line(10, Guid.NewGuid().ToString("D"), Reason10))), 422));
        NothingWritten(s);
    }

    /// <summary>Sabotage (dg): the deviation decided on the FEFO read's order (L1 counted though wholly reserved) → 422 (red).</summary>
    [Fact]
    public async Task AC28_with_the_first_lot_wholly_reserved_the_next_lot_is_no_deviation()
    {
        var (s, l1, l2) = await WorldAsync();
        await s.ReserveOtherAsync(30, l1);

        var outbound = await s.CreatedAsync(s.Line(20, l2));

        Assert.Equal([(l2, 20m, 1)], AllocationSummary(outbound));
        Assert.Null(BypassReason(outbound));
    }

    /// <summary>Sabotage (dk): the mode not read (RequireReason in every mode) → the reasoned Block override is 201 (red).</summary>
    [Fact]
    public async Task AC28_block_refuses_a_deviation_with_or_without_a_reason()
    {
        var (s, l1, l2) = await WorldAsync(FefoOutboundMode.Block);

        foreach (var reason in new string?[] { null, Reason10 })
        {
            var refused = await Expect(s.CreateAsync(s.Body(s.Line(20, l2, reason))), 422);
            Assert.Equal("FEFO_ORDER_VIOLATION", Code(refused));
            Assert.Equal(l1, Details(refused).GetProperty("firstEligibleLotId").GetString());
        }

        NothingWritten(s);
    }

    [Fact]
    public async Task AC28_warn_takes_the_reason_when_given_and_asks_for_none()
    {
        var (s, _, l2) = await WorldAsync(FefoOutboundMode.Warn);

        Assert.Null(BypassReason(await s.CreatedAsync(s.Line(10, l2))));
        Assert.Equal(Reason10, BypassReason(await s.CreatedAsync(s.Line(10, l2, Reason10))));
    }

    /// <summary>Sabotage (ef): only the lower bound checked (as the hook) → 1001 characters in RequireReason is 201 (red).</summary>
    [Theory]
    [InlineData(FefoOutboundMode.Block)]
    [InlineData(FefoOutboundMode.RequireReason)]
    [InlineData(FefoOutboundMode.Warn)]
    public async Task AC28_a_reason_over_1000_characters_is_400_in_every_mode(FefoOutboundMode mode)
    {
        var (s, _, l2) = await WorldAsync(mode);

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Body(s.Line(20, l2, new string('x', 1001)))), 400));
        NothingWritten(s);
        if (mode == FefoOutboundMode.RequireReason)
        {
            Assert.Equal(new string('x', 1000), BypassReason(await s.CreatedAsync(s.Line(20, l2, new string('x', 1000)))));
        }
    }
}
