// WP-CI-SERVICE-TESTS-05 (BL-646 item C) — the Linux start-time branch of ProcessTiming cannot run on the macOS
// developer machine (no /proc), so its parser is measured here on fixed /proc/<pid>/stat lines; the /proc read
// itself is measured on the Ubuntu runner.

using Xunit;

namespace Diten.AuthService.AccountKindAcceptanceHost.Tests;

public sealed class ProcessTimingLinuxParserTests
{
    // Field 22 (starttime) = 4321 ticks; with 100 ticks/s that is 43.21 s after boot.
    private const string PlainLine =
        "1234 (dotnet) S 1 1234 1234 0 -1 4194560 100 0 0 0 5 3 0 0 20 0 9 0 4321 1000000 500 18446744073709551615";

    [Fact]
    public void Start_time_is_boot_time_plus_field_22_in_clock_ticks()
    {
        Assert.Equal(1_700_000_000_000L + 43_210L,
            ProcessTiming.ParseLinuxStartTimeUnixMs(PlainLine, 1_700_000_000L, 100));
    }

    [Fact]
    public void A_comm_with_spaces_and_a_closing_parenthesis_does_not_shift_the_fields()
    {
        var line = PlainLine.Replace("(dotnet)", "(a b) c)", StringComparison.Ordinal);
        Assert.Equal(1_700_000_000_000L + 43_210L,
            ProcessTiming.ParseLinuxStartTimeUnixMs(line, 1_700_000_000L, 100));
    }

    [Fact]
    public void A_truncated_line_or_a_bad_tick_rate_is_refused()
    {
        Assert.Throws<FormatException>(() => ProcessTiming.ParseLinuxStartTimeUnixMs("1234 (dotnet) S 1 2 3", 0, 100));
        Assert.Throws<FormatException>(() => ProcessTiming.ParseLinuxStartTimeUnixMs("1234 dotnet S", 0, 100));
        Assert.Throws<InvalidOperationException>(() => ProcessTiming.ParseLinuxStartTimeUnixMs(PlainLine, 0, 0));
    }
}
