using Diten.Platform.Domain.Features.RecordCodes;
using Xunit;

namespace Diten.Platform.Application.Tests.RecordCodes;

/// <summary>MOD-0040-FU02 AC-3 / AC-7 / AC-15 — <c>{PREFIX}-{TYPE}-{COUNTER}</c>, six digits at least, never cut, ASCII only.</summary>
public sealed class RecordCodeFormatTests
{
    [Theory]
    [InlineData("MYG", "PAY", 1, "MYG-PAY-000001")]
    [InlineData("MYG", "PAY", 42, "MYG-PAY-000042")]
    [InlineData("MYG", "PAY", 999999, "MYG-PAY-999999")]
    [InlineData("MYG", "PAY", 1000000, "MYG-PAY-1000000")]
    [InlineData("GM", "BNK", 12345678901, "GM-BNK-12345678901")]
    public void AC03_AC07_codes_are_zero_padded_to_six_and_grow_past_them(string prefix, string type, long sequence, string expected) =>
        Assert.Equal(expected, RecordCodeFormat.Format(prefix, type, sequence));

    [Theory]
    [InlineData("MYĞ")]
    [InlineData("my")]
    [InlineData("M")]
    [InlineData("ABCDEF")]
    [InlineData("GMG-CH")]
    [InlineData("MY G")]
    [InlineData("MYG\n")]
    [InlineData("")]
    public void AC15_a_prefix_outside_2_to_5_ASCII_capitals_is_never_formatted(string prefix)
    {
        Assert.False(RecordCodeFormat.IsValidPrefix(prefix));
        Assert.Throws<ArgumentException>(() => RecordCodeFormat.Format(prefix, "PAY", 1));
    }

    [Fact]
    public void AC15_every_issuable_type_and_scope_formats_to_the_DL1_pattern_in_ASCII()
    {
        foreach (var definition in RecordCodeTypeCatalog.All.Where(d => d.AllowedCaller is not null))
        {
            foreach (var prefix in new[] { "MY", "MYG", "GMGXX" })
            {
                var code = RecordCodeFormat.Format(prefix, definition.RecordType, 7);
                Assert.True(RecordCodeFormat.IsValidCode(code), code);
                Assert.Matches("^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}$", code);
                Assert.All(code, c => Assert.True(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-', code));
            }
        }
    }
}
