using Diten.InventoryService.Application.Features.Expiry.Handlers;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-MVP1-FIX-LIVE3 (live finding 4) — the FEFO "today" is the UTC date: MOD-0176 ASSUMPTION-0176-05 ("today" = UTC date, same as
/// LotExpirySweeper), MOD-0174 ("ExpiryDate &lt; today (UTC)"). Inventory has no readable tenant / company time zone (measured: no token claim,
/// no read port, no setting), so the rule stays and the screen says "(UTC)". At 21:30 UTC (00:30 in Europe/Istanbul) the day is still the
/// UTC one, and a lot expiring on that UTC day is "0 days" — the panel's arithmetic, measured on the production helpers.
/// </summary>
public sealed class ExpiryTodayIsUtcTests
{
    [Fact]
    public void Today_is_the_UTC_date_even_after_local_midnight()
    {
        var lateEvening = new DateTimeOffset(2026, 10, 19, 21, 30, 0, TimeSpan.Zero);          // Istanbul: 2026-10-20 00:30
        Assert.Equal(new DateOnly(2026, 10, 19), ExpiryLotView.Today(lateEvening));
        Assert.Equal(new DateOnly(2026, 10, 19), ExpiryLotView.Today(lateEvening.ToOffset(TimeSpan.FromHours(3))));   // the same instant
        Assert.Equal(new DateOnly(2026, 10, 19), ExpiryLotView.Day(new DateTimeOffset(2026, 10, 20, 0, 30, 0, TimeSpan.FromHours(3))));
    }
}
