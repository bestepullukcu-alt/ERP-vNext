using System.Text;

namespace Diten.BuildingBlocks.Email;

/// <summary>
/// BL-454 — the ONE frame every system e-mail is sent in ("A · Card", owner's choice 2026-10-02): a band carrying
/// the tenant's name, a heading, the body, an optional information table, one action button, the same address
/// written out in case the button does not work, and a footer. An e-mail supplies its content
/// (<see cref="EmailShellModel"/>); how it looks is decided here and nowhere else.
///
/// <para>⚠ <b>INLINE STYLES ARE DELIBERATE HERE.</b> The product rule "no inline CSS" (FG-003) is about web pages,
/// where a stylesheet is always loaded. An e-mail client is not a browser: several strip or ignore
/// <c>&lt;style&gt;</c> blocks and none load a stylesheet, so in e-mail HTML an inline style is the only style that
/// reliably arrives. This file is the single, intended exception to FG-003. The one <c>&lt;style&gt;</c> block
/// below is an enhancement only (dark mode, narrow screens): a client that drops it still shows the light card
/// intact.</para>
///
/// <para><b>What a client never has to fetch or run:</b> no remote image, no script, no web font, no tracking
/// pixel. The "logo" on the band is the tenant's initials as text. Layout is nested tables, because that is what
/// Outlook's Word engine lays out; flexbox, gap and box-shadow do not survive it.</para>
///
/// <para><b>Every value is data.</b> Each string of the model is HTML-encoded. The action becomes a link only when
/// its address is an absolute <c>http</c>/<c>https</c> URL — <c>javascript:</c>, <c>data:</c> and anything else
/// produce no button and no address line.</para>
/// </summary>
public static class EmailShell
{
    // #5356d6, not the product's #696cff: white text on #696cff does not reach 4.5:1 contrast.
    private const string Accent = "#5356d6";
    private const string PageBackground = "#eef0f6";
    private const string CardBackground = "#ffffff";
    private const string HeadingColor = "#1f2a44";
    private const string BodyColor = "#46505f";
    private const string TextColor = "#2b3445";
    private const string SecondaryColor = "#5d6779";
    private const string LineColor = "#e3e6ee";
    private const string Font = "-apple-system,'Segoe UI',Helvetica,Arial,Tahoma,sans-serif";

    public static EmailShellResult Render(EmailShellModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var texts = EmailShellTexts.For(model.Language);
        var tenantName = EmailHeaderText.CleanDisplayName(model.TenantDisplayName);
        var bandName = tenantName.Length > 0 ? tenantName : EmailProduct.Name;
        var action = model.Action is { } candidate && IsSafeHttpUrl(candidate.Url) && !string.IsNullOrWhiteSpace(candidate.Label)
            ? candidate
            : null;
        var replyTo = string.IsNullOrWhiteSpace(model.ReplyToEmail) ? null : model.ReplyToEmail.Trim();
        var footer = tenantName.Length > 0
            ? string.Format(texts.FooterOnBehalf, tenantName, EmailProduct.Name)
            : string.Format(texts.FooterPlatform, EmailProduct.Name);
        if (replyTo is not null)
        {
            footer += " " + string.Format(texts.FooterReply, replyTo);
        }

        return new EmailShellResult(
            RenderHtml(model, texts, bandName, action, footer),
            RenderText(model, texts, action, footer));
    }

    /// <summary>An absolute http/https address with no control character in it. Nothing else becomes a link.</summary>
    public static bool IsSafeHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Any(char.IsControl))
        {
            return false;
        }

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>The initials shown where a logo would be: the first letters of the first two words.</summary>
    public static string Initials(string name)
    {
        var words = (name ?? string.Empty).Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        // One word: its first two letters ("Acme" → "AC"). More: the first letter of the first two words.
        var letters = words.Length == 1
            ? words[0].Where(char.IsLetterOrDigit).Take(2).ToArray()
            : words
                .Select(word => word.FirstOrDefault(char.IsLetterOrDigit))
                .Where(ch => ch != default)
                .Take(2)
                .ToArray();

        return new string(letters).ToUpperInvariant();
    }

    private static string RenderHtml(
        EmailShellModel model, EmailShellTexts texts, string bandName, EmailShellAction? action, string footer)
    {
        var dir = texts.RightToLeft ? "rtl" : "ltr";
        var start = texts.RightToLeft ? "right" : "left";
        var html = new StringBuilder(4096);

        html.Append("<!doctype html>\n");
        html.Append($"<html lang=\"{texts.Language}\" dir=\"{dir}\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\">\n");
        html.Append("<head>\n<meta charset=\"utf-8\">\n");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        html.Append("<meta name=\"color-scheme\" content=\"light dark\">\n<meta name=\"supported-color-schemes\" content=\"light dark\">\n");
        html.Append($"<title>{Encode(model.Heading)}</title>\n");
        html.Append("<style>\n");
        html.Append("@media (prefers-color-scheme: dark){.es-page{background:#161a23 !important}.es-card{background:#202634 !important}");
        html.Append(".es-h{color:#f1f3f8 !important}.es-t{color:#d5d9e3 !important}.es-s{color:#a9b1c2 !important}");
        html.Append(".es-line{border-color:#343c4f !important}}\n");
        html.Append("@media only screen and (max-width:620px){.es-wrap{width:100% !important}.es-pad{padding-left:20px !important;padding-right:20px !important}}\n");
        html.Append("</style>\n</head>\n");

        html.Append($"<body class=\"es-page\" style=\"margin:0;padding:0;background:{PageBackground};\">\n");
        html.Append($"<table role=\"presentation\" class=\"es-page\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" dir=\"{dir}\" style=\"background:{PageBackground};\">\n");
        html.Append("<tr><td align=\"center\" style=\"padding:32px 16px;\">\n");
        html.Append($"<table role=\"presentation\" class=\"es-wrap\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" dir=\"{dir}\" style=\"width:600px;max-width:600px;\">\n");

        // The band: initials where a logo would be, then the name.
        html.Append($"<tr><td class=\"es-pad\" style=\"background:{Accent};padding:18px 32px;border-radius:10px 10px 0 0;\">\n");
        html.Append($"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" dir=\"{dir}\"><tr>\n");
        var initials = Initials(bandName);
        if (initials.Length > 0)
        {
            html.Append($"<td width=\"36\" height=\"36\" align=\"center\" valign=\"middle\" style=\"width:36px;height:36px;background:#7275de;border-radius:8px;color:#ffffff;font-family:{Font};font-size:14px;font-weight:700;\">{Encode(initials)}</td>\n");
            html.Append("<td width=\"12\" style=\"width:12px;font-size:0;line-height:0;\">&nbsp;</td>\n");
        }

        html.Append($"<td valign=\"middle\" style=\"color:#ffffff;font-family:{Font};font-size:16px;font-weight:600;text-align:{start};\">{Encode(bandName)}</td>\n");
        html.Append("</tr></table>\n</td></tr>\n");

        // The card.
        html.Append($"<tr><td class=\"es-card es-pad\" style=\"background:{CardBackground};padding:32px;border-radius:0 0 10px 10px;text-align:{start};\">\n");

        if (!string.IsNullOrWhiteSpace(model.Heading))
        {
            html.Append($"<h1 class=\"es-h\" style=\"margin:0 0 18px;font-family:{Font};font-size:24px;line-height:1.3;font-weight:700;color:{HeadingColor};\">{Encode(model.Heading)}</h1>\n");
        }

        foreach (var paragraph in model.Paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            html.Append($"<p class=\"es-t\" style=\"margin:0 0 18px;font-family:{Font};font-size:15px;line-height:1.65;color:{BodyColor};\">{Encode(paragraph)}</p>\n");
        }

        if (!string.IsNullOrWhiteSpace(model.BodyHtmlFragment))
        {
            html.Append($"<div class=\"es-t\" style=\"margin:0 0 18px;font-family:{Font};font-size:15px;line-height:1.65;color:{BodyColor};\">{model.BodyHtmlFragment}</div>\n");
        }

        var rows = model.InfoRows.Where(r => !string.IsNullOrWhiteSpace(r.Label) && !string.IsNullOrWhiteSpace(r.Value)).ToList();
        if (rows.Count > 0)
        {
            html.Append($"<table role=\"presentation\" class=\"es-line\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" dir=\"{dir}\" style=\"margin:0 0 18px;border:1px solid {LineColor};border-radius:8px;border-collapse:separate;\">\n");
            for (var i = 0; i < rows.Count; i++)
            {
                var border = i < rows.Count - 1 ? $"border-bottom:1px solid {LineColor};" : string.Empty;
                html.Append("<tr>");
                html.Append($"<td class=\"es-s es-line\" width=\"120\" valign=\"top\" style=\"width:120px;padding:10px 18px;{border}font-family:{Font};font-size:14px;color:{SecondaryColor};text-align:{start};\">{Encode(rows[i].Label)}</td>");
                html.Append($"<td class=\"es-h es-line\" valign=\"top\" style=\"padding:10px 18px;{border}font-family:{Font};font-size:14px;font-weight:600;color:{HeadingColor};text-align:{start};\">{Encode(rows[i].Value)}</td>");
                html.Append("</tr>\n");
            }

            html.Append("</table>\n");
        }

        if (action is not null)
        {
            AppendButton(html, action, dir);
        }

        if (!string.IsNullOrWhiteSpace(model.ActionNote))
        {
            html.Append($"<p class=\"es-s\" style=\"margin:0 0 18px;font-family:{Font};font-size:13px;line-height:1.6;color:{SecondaryColor};\">{Encode(model.ActionNote)}</p>\n");
        }

        if (action is not null)
        {
            // The address itself is always left-to-right, whatever the language of the page.
            html.Append($"<div class=\"es-line\" style=\"margin:0 0 18px;border-top:1px solid {LineColor};padding-top:16px;\">\n");
            html.Append($"<div class=\"es-s\" style=\"font-family:{Font};font-size:12px;line-height:1.6;color:{SecondaryColor};\">{Encode(texts.ActionFallback)}</div>\n");
            html.Append($"<div class=\"es-t\" dir=\"ltr\" style=\"font-family:{Font};font-size:12px;line-height:1.6;color:{TextColor};word-break:break-all;text-align:{start};\">{Encode(action.Url.Trim())}</div>\n");
            html.Append("</div>\n");
        }

        if (!string.IsNullOrWhiteSpace(model.Footnote))
        {
            html.Append($"<p class=\"es-s\" style=\"margin:0;font-family:{Font};font-size:12px;line-height:1.6;color:{SecondaryColor};\">{Encode(model.Footnote)}</p>\n");
        }

        html.Append("</td></tr>\n");

        // The footer, outside the card.
        html.Append($"<tr><td class=\"es-s\" style=\"padding:18px 8px 0;font-family:{Font};font-size:12px;line-height:1.6;color:{SecondaryColor};text-align:{start};\">{Encode(footer)}</td></tr>\n");
        html.Append("</table>\n</td></tr>\n</table>\n</body>\n</html>\n");
        return html.ToString();
    }

    private static void AppendButton(StringBuilder html, EmailShellAction action, string dir)
    {
        var url = Encode(action.Url.Trim());
        var label = Encode(action.Label.Trim());
        // Outlook's Word engine ignores padding and border-radius on a link, so it gets a VML rounded rectangle of a
        // fixed width estimated from the label. "--" would end the conditional comment early.
        var width = Math.Clamp((action.Label.Trim().Length * 9) + 56, 160, 420);
        html.Append($"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" dir=\"{dir}\" style=\"margin:0 0 18px;\"><tr><td>\n");
        html.Append("<!--[if mso]>");
        html.Append($"<v:roundrect xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:w=\"urn:schemas-microsoft-com:office:word\" href=\"{CommentSafe(url)}\" style=\"height:46px;v-text-anchor:middle;width:{width}px;\" arcsize=\"18%\" stroke=\"f\" fillcolor=\"{Accent}\">");
        html.Append($"<w:anchorlock/><center style=\"color:#ffffff;font-family:'Segoe UI',Arial,sans-serif;font-size:15px;font-weight:bold;\">{CommentSafe(label)}</center></v:roundrect>");
        html.Append("<![endif]-->\n");
        html.Append("<!--[if !mso]><!-->");
        html.Append($"<a href=\"{url}\" style=\"display:inline-block;background:{Accent};color:#ffffff;text-decoration:none;font-family:{Font};font-size:15px;font-weight:600;padding:14px 24px;border-radius:8px;\">{label}</a>");
        html.Append("<!--<![endif]-->\n");
        html.Append("</td></tr></table>\n");
    }

    private static string RenderText(EmailShellModel model, EmailShellTexts texts, EmailShellAction? action, string footer)
    {
        var text = new StringBuilder(1024);
        void Block(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (text.Length > 0)
            {
                text.Append("\n\n");
            }

            text.Append(value.Trim());
        }

        Block(model.Heading);
        foreach (var paragraph in model.Paragraphs)
        {
            Block(paragraph);
        }

        Block(model.BodyText);

        var rows = model.InfoRows.Where(r => !string.IsNullOrWhiteSpace(r.Label) && !string.IsNullOrWhiteSpace(r.Value)).ToList();
        if (rows.Count > 0)
        {
            Block(string.Join("\n", rows.Select(r => $"{r.Label.Trim()}: {r.Value.Trim()}")));
        }

        if (action is not null)
        {
            Block($"{action.Label.Trim()}:\n{action.Url.Trim()}");
        }

        Block(model.ActionNote);
        Block(model.Footnote);
        Block("--\n" + footer);
        text.Append('\n');
        return text.ToString();
    }

    /// <summary>
    /// The five characters that are markup, and nothing else. <c>WebUtility.HtmlEncode</c> also turns every Latin-1
    /// letter into a numeric entity ("görev" → "g&amp;#246;rev"), which is valid and unreadable in the source of a
    /// UTF-8 message.
    /// </summary>
    public static string Encode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 16);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '&': builder.Append("&amp;"); break;
                case '<': builder.Append("&lt;"); break;
                case '>': builder.Append("&gt;"); break;
                case '"': builder.Append("&quot;"); break;
                case '\'': builder.Append("&#39;"); break;
                default: builder.Append(ch); break;
            }
        }

        return builder.ToString();
    }

    private static string CommentSafe(string encoded) => encoded.Replace("--", "-&#45;", StringComparison.Ordinal);
}
