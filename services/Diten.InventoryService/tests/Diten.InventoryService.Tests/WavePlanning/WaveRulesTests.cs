using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.WavePlanning;

/// <summary>MOD-0180 pure rules (pack §8.4, §8.5, §8.7, §12) — no host, no store.</summary>
public sealed class WaveRulesTests
{
    private static DateTimeOffset Utc(int y, int mo, int d, int h, int mi) => new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-11-20T14:30", "Europe/Istanbul", 2026, 11, 20, 11, 30)]
    [InlineData("2026-11-20T14:30", "Asia/Tokyo", 2026, 11, 20, 5, 30)]
    [InlineData("2026-07-01T14:30", "America/New_York", 2026, 7, 1, 18, 30)]
    [InlineData("2026-01-15T14:30", "America/New_York", 2026, 1, 15, 19, 30)]
    [InlineData("2026-06-01T00:10", "Pacific/Auckland", 2026, 5, 31, 12, 10)]
    public void The_cutoff_is_converted_in_the_named_zone(string local, string zone, int y, int mo, int d, int h, int mi)
    {
        var cutoff = WaveCutoffRules.ToUtc(local, zone);

        Assert.Equal(WaveCutoffOutcome.Converted, cutoff.Outcome);
        Assert.Equal(Utc(y, mo, d, h, mi), cutoff.AtUtc);
        Assert.False(cutoff.AmbiguousResolved);
    }

    [Fact]
    public void A_local_time_in_the_daylight_saving_gap_does_not_exist_and_a_repeated_one_takes_the_earlier_instant()
    {
        Assert.Equal(WaveCutoffOutcome.InGap, WaveCutoffRules.ToUtc("2026-03-08T02:30", "America/New_York").Outcome);
        Assert.Equal(WaveCutoffOutcome.InGap, WaveCutoffRules.ToUtc("2026-03-29T02:30", "Europe/Berlin").Outcome);

        var repeated = WaveCutoffRules.ToUtc("2026-11-01T01:30", "America/New_York");
        Assert.Equal(WaveCutoffOutcome.Converted, repeated.Outcome);
        Assert.True(repeated.AmbiguousResolved);
        Assert.Equal(Utc(2026, 11, 1, 5, 30), repeated.AtUtc); // EDT (UTC-4): the first of the two 01:30s

        Assert.Equal(WaveCutoffOutcome.Malformed, WaveCutoffRules.ToUtc("2026-11-20 14:30", "Europe/Istanbul").Outcome);
        Assert.Equal(WaveCutoffOutcome.Malformed, WaveCutoffRules.ToUtc("2026-11-20T14:30:00", "Europe/Istanbul").Outcome);
        Assert.Equal(WaveCutoffOutcome.UnknownTimeZone, WaveCutoffRules.ToUtc("2026-11-20T14:30", "Mars/Olympus").Outcome);
        Assert.Equal(WaveCutoffOutcome.UnknownTimeZone, WaveCutoffRules.ToUtc("2026-11-20T14:30", null).Outcome);
    }

    [Fact]
    public void A_cutoff_may_be_at_most_one_hour_in_the_past()
    {
        var now = Utc(2026, 11, 20, 12, 0);
        Assert.False(WaveCutoffRules.IsTooOld(Utc(2026, 11, 20, 11, 0), now));
        Assert.True(WaveCutoffRules.IsTooOld(Utc(2026, 11, 20, 10, 59), now));
        Assert.False(WaveCutoffRules.IsTooOld(Utc(2026, 11, 21, 0, 0), now));
    }

    [Theory]
    [InlineData("Europe/Istanbul", true)]
    [InlineData("America/Argentina/Buenos_Aires", true)]
    [InlineData("Turkey Standard Time", false)]
    [InlineData("UTC+3", false)]
    [InlineData(" Europe/Istanbul", false)]
    [InlineData("Mars/Olympus", false)]
    [InlineData(null, false)]
    public void Only_an_IANA_id_the_runtime_knows_is_a_time_zone(string? id, bool known) => Assert.Equal(known, WaveCutoffRules.IsKnownIanaZone(id));

    [Fact]
    public void Business_limits_warn_and_the_technical_ceiling_refuses()
    {
        Assert.Empty(WaveRules.LimitWarnings(null, null, new WaveTotals(900, 9000, 1500)));
        Assert.Empty(WaveRules.LimitWarnings(10, 100, new WaveTotals(10, 100, 50)));
        Assert.Equal([new WaveLimitWarning(WaveLimitKind.MaxOrders, 10, 11), new WaveLimitWarning(WaveLimitKind.MaxLines, 100, 101)],
            WaveRules.LimitWarnings(10, 100, new WaveTotals(11, 101, 50)));

        Assert.False(WaveRules.ExceedsTechnicalLimit(new WaveTotals(500, 0, 2000)));
        Assert.True(WaveRules.ExceedsTechnicalLimit(new WaveTotals(501, 0, 0)));
        Assert.True(WaveRules.ExceedsTechnicalLimit(new WaveTotals(1, 0, 2001)));

        Assert.Equal(200, WaveRules.SuggestionOrderRoom(null, new WaveTotals(7, 0, 0)));
        Assert.Equal(3, WaveRules.SuggestionOrderRoom(10, new WaveTotals(7, 0, 0)));
        Assert.Equal(0, WaveRules.SuggestionOrderRoom(5, new WaveTotals(7, 0, 0)));
        Assert.True(WaveRules.FitsLines(null, 9999, 9999));
        Assert.True(WaveRules.FitsLines(10, 7, 3));
        Assert.False(WaveRules.FitsLines(10, 7, 4));
    }

    [Fact]
    public void A_reason_is_ten_to_five_hundred_characters_after_trim_and_the_setting_ranges_hold()
    {
        Assert.Null(WaveRules.Reason(null));
        Assert.Null(WaveRules.Reason("   kisa    "));
        Assert.Equal("tam on kar", WaveRules.Reason("  tam on kar "));
        Assert.Equal(500, WaveRules.Reason(new string('r', 500))!.Length);
        Assert.Null(WaveRules.Reason(new string('r', 501)));

        Assert.True(WaveRules.RiskPercentInRange(1) && WaveRules.RiskPercentInRange(100));
        Assert.False(WaveRules.RiskPercentInRange(0) || WaveRules.RiskPercentInRange(101));
        Assert.True(WaveRules.RiskMinutesInRange(1440) && WaveRules.PendingMinutesInRange(1440));
        Assert.False(WaveRules.RiskMinutesInRange(1441) || WaveRules.PendingMinutesInRange(0));
        Assert.True(WaveRules.IsActive(WaveOrderState.Planned) && WaveRules.IsActive(WaveOrderState.Issued));
        Assert.DoesNotContain(Enum.GetValues<WaveOrderState>().Except([WaveOrderState.Planned, WaveOrderState.Issued]), WaveRules.IsActive);
    }

    [Theory]
    [InlineData("A-01-02-03", "A")]
    [InlineData("12-01-02-03", "12")]
    [InlineData("b-10-00-99", "b")]
    [InlineData("AB-01-02-03", null)]
    [InlineData("1234-01-02-03", null)]
    [InlineData("A-1-02-03", null)]
    [InlineData("BIN-0001", null)]
    [InlineData(null, null)]
    public void The_aisle_is_read_from_a_bulk_format_bin_code_only(string? code, string? aisle) => Assert.Equal(aisle, WaveLocationRules.Aisle(code));
}
