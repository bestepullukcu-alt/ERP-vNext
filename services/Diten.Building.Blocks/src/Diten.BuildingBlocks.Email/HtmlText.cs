using System.Net;
using System.Text.RegularExpressions;

namespace Diten.BuildingBlocks.Email;

/// <summary>
/// BL-454 — the plain-text form of a template's HTML body, for a template that has no text body of its own. Every
/// e-mail goes out with a text part; a text part that is only a greeting and a footer is worse than none, so the
/// text is DERIVED from the HTML: paragraphs and line breaks kept, a link written as "label (address)", every other
/// tag dropped, entities decoded.
/// </summary>
public static partial class HtmlText
{
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = ScriptOrStyle().Replace(html, string.Empty);
        text = Link().Replace(text, match =>
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
            var label = WebUtility.HtmlDecode(Tag().Replace(match.Groups["label"].Value, string.Empty)).Trim();
            if (href.Length == 0)
            {
                return label;
            }

            return label.Length == 0 || string.Equals(label, href, StringComparison.Ordinal) ? href : $"{label} ({href})";
        });
        text = LineBreak().Replace(text, "\n");
        text = BlockEnd().Replace(text, "\n\n");
        text = Tag().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Replace("\r\n", "\n");
        text = SpaceRun().Replace(text, " ");
        text = string.Join("\n", text.Split('\n').Select(line => line.Trim()));
        text = BlankRun().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex("<a\\b[^>]*?href\\s*=\\s*(?:\"(?<href>[^\"]*)\"|'(?<href>[^']*)')[^>]*>(?<label>.*?)</a\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Link();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"</(p|div|h[1-6]|li|tr|table|ul|ol)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex SpaceRun();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRun();
}
