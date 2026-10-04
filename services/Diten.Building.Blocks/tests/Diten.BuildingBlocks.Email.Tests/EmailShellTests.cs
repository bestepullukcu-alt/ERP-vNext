using Diten.BuildingBlocks.Email;
using Xunit;

namespace Diten.BuildingBlocks.Email.Tests;

public sealed class EmailShellTests
{
    private static EmailShellModel Full(string language = "tr") => new()
    {
        Language = language,
        TenantDisplayName = "Diten Pharma",
        Heading = "Burak Şen size bir görev atadı",
        Paragraphs = ["Merhaba Ayşe, aşağıdaki görev sizde."],
        InfoRows = [new("Görev", "Parti kaydı incelemesi"), new("Öncelik", "Yüksek")],
        Action = new("Görevi aç", "https://di10.example/WorkCenterNext/Details/42"),
        ActionNote = "Bağlantı 7 gün geçerlidir.",
        Footnote = "Bu bildirimi görev size atandığı için aldınız.",
        ReplyToEmail = "ik@ditenpharma.test"
    };

    [Fact]
    public void The_shell_carries_band_heading_body_table_button_address_and_footer()
    {
        var html = EmailShell.Render(Full()).Html;

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains(">DP</td>", html);
        Assert.Contains(">Diten Pharma</td>", html);
        Assert.Contains("<h1", html);
        Assert.Contains("Burak Şen size bir görev atadı</h1>", html);
        Assert.Contains("Merhaba Ayşe, aşağıdaki görev sizde.</p>", html);
        Assert.Contains(">Öncelik</td>", html);
        Assert.Contains(">Yüksek</td>", html);
        Assert.Contains("<a href=\"https://di10.example/WorkCenterNext/Details/42\"", html);
        Assert.Contains("Düğme çalışmıyorsa bu adresi tarayıcınıza yapıştırın:", html);
        Assert.Contains("Bu e-posta Diten Pharma adına Di10 üzerinden gönderildi. Yanıtınız ik@ditenpharma.test adresine ulaşır.", html);
    }

    [Fact]
    public void Optional_parts_are_absent_when_the_email_does_not_supply_them()
    {
        var html = EmailShell.Render(new EmailShellModel
        {
            Language = "en",
            TenantDisplayName = "Acme",
            Heading = "Hello",
            Paragraphs = ["Only a body."]
        }).Html;

        Assert.DoesNotContain("<a ", html);
        Assert.DoesNotContain("v:roundrect", html);
        Assert.DoesNotContain("If the button does not work", html);
        Assert.DoesNotContain("border:1px solid #e3e6ee;border-radius:8px", html);
        Assert.DoesNotContain("Your reply goes to", html);
        Assert.Contains("This e-mail was sent by Di10 on behalf of Acme.", html);
    }

    [Fact]
    public void A_platform_email_carries_the_product_name_on_the_band_and_in_the_footer()
    {
        var result = EmailShell.Render(new EmailShellModel { Language = "en", Heading = "Reset", Paragraphs = ["x"] });

        Assert.Contains(">Di10</td>", result.Html);
        Assert.Contains("This e-mail was sent by Di10.", result.Html);
        Assert.DoesNotContain("on behalf of", result.Html);
    }

    [Fact]
    public void Markup_in_a_user_name_a_task_title_or_a_tenant_name_arrives_as_text()
    {
        const string attack = "<script>alert(1)</script><a href=\"https://evil.example\">x</a>";
        var html = EmailShell.Render(new EmailShellModel
        {
            Language = "en",
            TenantDisplayName = "Acme " + attack,
            Heading = "Task " + attack,
            Paragraphs = ["Hello " + attack],
            InfoRows = [new("Task " + attack, "Value " + attack)],
            Action = new("Open " + attack, "https://di10.example/t?a=1&b=\"2\""),
            ActionNote = attack,
            Footnote = attack,
            ReplyToEmail = "a@b.test\"><script>"
        }).Html;

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("https://evil.example\">", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("href=\"https://di10.example/t?a=1&amp;b=&quot;2&quot;\"", html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("/relative/path")]
    [InlineData("https://di10.example/\r\nBcc: x@y.test")]
    [InlineData("")]
    public void An_action_that_is_not_an_absolute_http_address_produces_no_button_and_no_address_line(string url)
    {
        var result = EmailShell.Render(Full() with { Action = new("Görevi aç", url) });

        Assert.DoesNotContain("<a ", result.Html);
        Assert.DoesNotContain("v:roundrect", result.Html);
        Assert.DoesNotContain("Düğme çalışmıyorsa", result.Html);
        Assert.DoesNotContain("Görevi aç", result.Text);
        if (url.Length > 0)
        {
            Assert.DoesNotContain(url, result.Text);
        }
    }

    [Fact]
    public void Nothing_in_the_shell_is_fetched_or_run_by_the_mail_client()
    {
        var html = EmailShell.Render(Full()).Html;

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@font-face", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("url(", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("display:flex", html.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_plain_text_part_says_everything_the_card_says_without_markup()
    {
        var text = EmailShell.Render(Full()).Text;

        Assert.DoesNotContain("<", text);
        Assert.Contains("Burak Şen size bir görev atadı", text);
        Assert.Contains("Merhaba Ayşe, aşağıdaki görev sizde.", text);
        Assert.Contains("Görev: Parti kaydı incelemesi", text);
        Assert.Contains("Öncelik: Yüksek", text);
        Assert.Contains("Görevi aç:\nhttps://di10.example/WorkCenterNext/Details/42", text);
        Assert.Contains("Bağlantı 7 gün geçerlidir.", text);
        Assert.Contains("Bu bildirimi görev size atandığı için aldınız.", text);
        Assert.Contains("Bu e-posta Diten Pharma adına Di10 üzerinden gönderildi.", text);
    }

    [Fact]
    public void An_Arabic_email_is_right_to_left_and_its_address_line_stays_left_to_right()
    {
        var html = EmailShell.Render(Full("ar")).Html;

        Assert.Contains("<html lang=\"ar\" dir=\"rtl\"", html);
        Assert.Contains("text-align:right", html);
        Assert.DoesNotContain("text-align:left", html);
        Assert.Contains("dir=\"ltr\" style=", html);
        Assert.Contains("https://di10.example/WorkCenterNext/Details/42</div>", html);
    }

    [Fact]
    public void A_left_to_right_language_is_not_marked_right_to_left()
    {
        var html = EmailShell.Render(Full("tr")).Html;

        Assert.Contains("<html lang=\"tr\" dir=\"ltr\"", html);
        Assert.DoesNotContain("dir=\"rtl\"", html);
    }

    [Fact]
    public void A_rendered_template_fragment_is_placed_as_markup_and_its_text_form_goes_to_the_text_part()
    {
        var result = EmailShell.Render(new EmailShellModel
        {
            Language = "en",
            TenantDisplayName = "Acme",
            Heading = "A task was assigned",
            BodyHtmlFragment = "<p>Task: <strong>Batch &lt;7&gt;</strong></p>",
            BodyText = "Task: Batch <7>"
        });

        Assert.Contains("<p>Task: <strong>Batch &lt;7&gt;</strong></p>", result.Html);
        Assert.Contains("Task: Batch <7>", result.Text);
    }

    [Fact]
    public void A_label_cannot_close_the_outlook_conditional_comment()
    {
        var html = EmailShell.Render(Full() with { Action = new("Open --> now", "https://di10.example/a--b") }).Html;

        var start = html.IndexOf("<!--[if mso]>", StringComparison.Ordinal);
        var end = html.IndexOf("<![endif]-->", start, StringComparison.Ordinal);
        var block = html[(start + "<!--[if mso]>".Length)..end];
        Assert.DoesNotContain("--", block);
    }
}
