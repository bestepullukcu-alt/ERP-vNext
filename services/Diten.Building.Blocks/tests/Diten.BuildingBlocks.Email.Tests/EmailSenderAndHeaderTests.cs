using Diten.BuildingBlocks.Email;
using Xunit;

namespace Diten.BuildingBlocks.Email.Tests;

public sealed class EmailSenderAndHeaderTests
{
    [Fact]
    public void The_product_has_one_name()
    {
        Assert.Equal("Di10", EmailProduct.Name);
    }

    [Fact]
    public void A_sender_name_the_tenant_wrote_itself_is_used_as_written()
    {
        Assert.Equal("Diten Pharma İK", EmailSender.ComposeDisplayName("Diten Pharma İK", "Diten Pharma", "tr"));
    }

    [Theory]
    [InlineData("en", "Diten Pharma (via Di10)")]
    [InlineData("tr", "Diten Pharma (Di10 üzerinden)")]
    [InlineData("fr", "Diten Pharma (via Di10)")]
    [InlineData("es", "Diten Pharma (a través de Di10)")]
    [InlineData("zh", "Diten Pharma（通过 Di10）")]
    [InlineData("ar", "Diten Pharma (عبر Di10)")]
    [InlineData("ru", "Diten Pharma (через Di10)")]
    [InlineData("tr-TR", "Diten Pharma (Di10 üzerinden)")]
    [InlineData("de", "Diten Pharma (via Di10)")]
    public void Without_its_own_sender_name_a_tenant_sends_as_its_display_name_via_the_product(string language, string expected)
    {
        Assert.Equal(expected, EmailSender.ComposeDisplayName(null, "Diten Pharma", language));
        Assert.Equal(expected, EmailSender.ComposeDisplayName("   ", "Diten Pharma", language));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void With_no_tenant_the_sender_is_the_product(string? tenantDisplayName)
    {
        Assert.Equal("Di10", EmailSender.ComposeDisplayName(null, tenantDisplayName, "tr"));
    }

    [Fact]
    public void A_line_break_in_a_tenant_name_never_reaches_the_sender_name()
    {
        var name = EmailSender.ComposeDisplayName(null, "Acme\r\nBcc: victim@evil.test", "en");

        Assert.DoesNotContain('\r', name);
        Assert.DoesNotContain('\n', name);
        Assert.Equal("Acme Bcc: victim@evil.test (via Di10)", name);
    }

    [Fact]
    public void A_very_long_tenant_name_is_shortened_but_keeps_the_via_part()
    {
        var name = EmailSender.ComposeDisplayName(null, new string('A', 400), "en");

        Assert.True(name.Length <= EmailHeaderText.MaxDisplayNameLength);
        Assert.EndsWith("… (via Di10)", name);
    }

    [Theory]
    [InlineData("Task\r\nBcc: x@y.test", "Task Bcc: x@y.test")]
    [InlineData("Task\nSubject: other", "Task Subject: other")]
    [InlineData("a\tb\0c", "a b c")]
    [InlineData("  padded  ", "padded")]
    [InlineData("a\r\n\r\n  b", "a b")]
    public void Control_characters_in_a_header_value_become_single_spaces(string input, string expected)
    {
        Assert.Equal(expected, EmailHeaderText.CleanSubject(input));
    }

    [Fact]
    public void Unicode_line_separators_in_a_header_value_become_spaces()
    {
        var input = "a" + (char)0x2028 + "b" + (char)0x2029 + "c" + (char)0x0085 + "d";

        Assert.Equal("a b c d", EmailHeaderText.CleanSubject(input));
    }

    [Fact]
    public void A_subject_is_capped()
    {
        Assert.Equal(EmailHeaderText.MaxSubjectLength, EmailHeaderText.CleanSubject(new string('x', 2000)).Length);
    }
}
