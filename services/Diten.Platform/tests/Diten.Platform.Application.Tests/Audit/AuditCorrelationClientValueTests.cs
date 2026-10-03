using Diten.Platform.Application.Contracts.Audit;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX3 — the client's correlation is kept as metadata only when it is a short run of ASCII
/// letters, digits and <c>-_.</c>: the bound and the alphabet, each at its edge.
/// </summary>
public sealed class AuditCorrelationClientValueTests
{
    [Fact]
    public void A_client_value_of_exactly_128_characters_is_kept_and_one_of_129_is_not()
    {
        Assert.Equal(new string('a', 128), AuditCorrelation.ClientValue(new string('a', 128)));
        Assert.Null(AuditCorrelation.ClientValue(new string('a', 129)));
    }

    [Theory]
    [InlineData("çağrı-1")]   // letters, but not ASCII
    [InlineData("корреляция")]
    [InlineData("٣٤٥")]        // digits, but not ASCII
    public void A_client_value_with_a_letter_or_digit_outside_ASCII_is_not_kept(string value)
        => Assert.Null(AuditCorrelation.ClientValue(value));

    [Fact]
    public void An_ASCII_value_with_the_three_allowed_marks_is_kept()
        => Assert.Equal("req-42_a.b", AuditCorrelation.ClientValue("req-42_a.b"));
}
