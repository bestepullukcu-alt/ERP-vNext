using Diten.BuildingBlocks.Email;
using Xunit;

namespace Diten.BuildingBlocks.Email.Tests;

/// <summary>BL-454 fix round 1 — display names, hidden characters, single addresses, the derived text part.</summary>
public sealed class EmailFixRoundOneTests
{
    [Theory]
    [InlineData("Acme\" <evil@attacker.test>, \"", "Acme evil@attacker.test,")]
    [InlineData("Acme\\\" <x@y.z>", "Acme x@y.z")]
    [InlineData("Diten <Pharma>", "Diten Pharma")]
    public void A_display_name_cannot_close_its_quotes_or_open_an_address(string input, string expected)
    {
        var cleaned = EmailHeaderText.CleanDisplayName(input);

        Assert.Equal(expected, cleaned);
        Assert.DoesNotContain('"', cleaned);
        Assert.DoesNotContain('\\', cleaned);
        Assert.DoesNotContain('<', cleaned);
        Assert.DoesNotContain('>', cleaned);
    }

    [Theory]
    [InlineData("Acme, Inc.")]
    [InlineData("Diten: Pharma; QA")]
    public void Commas_colons_and_semicolons_of_a_real_company_name_are_kept(string name)
    {
        Assert.Equal(name, EmailHeaderText.CleanDisplayName(name));
    }

    [Theory]
    [InlineData(0x202A)] [InlineData(0x202B)] [InlineData(0x202C)] [InlineData(0x202D)] [InlineData(0x202E)]
    [InlineData(0x2066)] [InlineData(0x2067)] [InlineData(0x2068)] [InlineData(0x2069)]
    [InlineData(0x200B)] [InlineData(0x2060)] [InlineData(0xFEFF)]
    public void A_direction_override_or_an_invisible_character_never_reaches_a_header(int codePoint)
    {
        var hidden = ((char)codePoint).ToString();
        var subject = "Invoice" + hidden + " 1024" + hidden;

        Assert.Equal("Invoice 1024", EmailHeaderText.CleanSubject(subject));
        Assert.Equal("Acme", EmailHeaderText.CleanDisplayName("Ac" + hidden + "me"));
    }

    [Fact]
    public void Joiners_and_direction_marks_that_Arabic_text_needs_are_kept()
    {
        // A real Arabic task title with a Latin batch code: the right-to-left / left-to-right marks keep the code in
        // its place, the zero-width non-joiner and joiner shape the letters. None of them may be dropped.
        var title = "مراجعة سجل الدفعة " + (char)0x200E + "LOT 24-118" + (char)0x200F + " ال" + (char)0x200C + "تقرير" + (char)0x200D;

        Assert.Equal(title, EmailHeaderText.CleanSubject(title));
        Assert.Equal(title, EmailHeaderText.CleanDisplayName(title));
    }

    [Theory]
    [InlineData("ik@ditenpharma.test", true)]
    [InlineData("first.last+tag@sub.example.com", true)]
    [InlineData("ik@ditenpharma.test, other@evil.test", false)]
    [InlineData("ik@ditenpharma.test;other@evil.test", false)]
    [InlineData("ik@ditenpharma.test\r\nBcc: victim@evil.test", false)]
    [InlineData("Name <ik@ditenpharma.test>", false)]
    [InlineData("\"quoted\"@ditenpharma.test", false)]
    [InlineData("no-at-sign", false)]
    [InlineData("two@@ats.test", false)]
    [InlineData("nodot@localhost", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_one_plain_address_is_an_address(string? value, bool expected)
    {
        Assert.Equal(expected, EmailAddressText.IsSingleAddress(value));
    }

    [Fact]
    public void A_footer_never_promises_a_reply_address_that_is_not_one_address()
    {
        var html = EmailShell.Render(new EmailShellModel
        {
            Language = "en", TenantDisplayName = "Acme", Heading = "H", Paragraphs = ["x"],
            ReplyToEmail = "ik@ditenpharma.test, other@evil.test"
        }).Html;

        Assert.DoesNotContain("Your reply goes to", html);
        Assert.DoesNotContain("other@evil.test", html);
    }

    [Fact]
    public void The_text_form_of_an_html_body_keeps_paragraphs_and_writes_each_link_with_its_address()
    {
        var text = HtmlText.ToPlainText(
            "<p>Hello <strong>Ayşe</strong>,</p><p>Open <a href=\"https://di10.example/t/42?a=1&amp;b=2\">the task</a> today.<br>Thanks</p>"
            + "<style>p{color:red}</style><p>Fish &amp; chips &lt;3</p>");

        Assert.Equal(
            "Hello Ayşe,\n\nOpen the task (https://di10.example/t/42?a=1&b=2) today.\nThanks\n\nFish & chips <3",
            text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p> </p><div></div>")]
    public void An_html_body_with_no_words_has_no_text_form(string? html)
    {
        Assert.Equal(string.Empty, HtmlText.ToPlainText(html));
    }
}

/// <summary>BL-454 fix round 1, item 2 — these tests are part of a solution the CI gate builds.</summary>
public sealed class EmailTestsAreBuiltTests
{
    [Fact]
    public void The_email_library_and_its_tests_are_in_the_platform_solution()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var solution = File.ReadAllText(Path.Combine(directory!.FullName, "services", "Diten.Platform", "Diten.Platform.sln"));
        Assert.Contains("Diten.BuildingBlocks.Email.csproj", solution);
        Assert.Contains("Diten.BuildingBlocks.Email.Tests.csproj", solution);
    }
}

/// <summary>BL-454 fix round 2 — the rest of the invisible characters; what an address may not contain.</summary>
public sealed class EmailFixRoundTwoTests
{
    [Theory]
    [InlineData(0x00AD)] [InlineData(0x180E)]
    [InlineData(0x2061)] [InlineData(0x2062)] [InlineData(0x2063)] [InlineData(0x2064)]
    [InlineData(0x206A)] [InlineData(0x206B)] [InlineData(0x206C)] [InlineData(0x206D)] [InlineData(0x206E)] [InlineData(0x206F)]
    public void The_remaining_invisible_characters_never_reach_a_header(int codePoint)
    {
        var hidden = ((char)codePoint).ToString();

        Assert.Equal("Invoice 1024", EmailHeaderText.CleanSubject("Inv" + hidden + "oice 1024"));
        Assert.True(EmailHeaderText.IsHidden((char)codePoint));
    }

    [Theory]
    [InlineData(0x200C)] [InlineData(0x200D)] [InlineData(0x200E)] [InlineData(0x200F)]
    public void Joiners_and_direction_marks_are_still_not_hidden(int codePoint)
    {
        Assert.False(EmailHeaderText.IsHidden((char)codePoint));
    }

    [Theory]
    [InlineData("ayse kaya@ditenpharma.test")]
    [InlineData("ayse\tkaya@ditenpharma.test")]
    [InlineData("ayse@ditenpharma.test ")]
    public void An_address_with_whitespace_is_not_an_address(string value)
    {
        Assert.False(EmailAddressText.IsSingleAddress(value));
    }

    [Fact]
    public void An_address_with_an_invisible_character_is_not_an_address()
    {
        Assert.False(EmailAddressText.IsSingleAddress("ay" + (char)0x200B + "se@ditenpharma.test"));
        Assert.False(EmailAddressText.IsSingleAddress("ayse@ditenpharma.test" + (char)0x202E));
    }

    [Theory]
    [InlineData("ayse,kaya@ditenpharma.test")]
    [InlineData("ayse;kaya@ditenpharma.test")]
    [InlineData("ayse@ditenpharma.test,")]
    public void An_address_with_a_list_separator_is_not_one_address(string value)
    {
        Assert.False(EmailAddressText.IsSingleAddress(value));
    }
}
