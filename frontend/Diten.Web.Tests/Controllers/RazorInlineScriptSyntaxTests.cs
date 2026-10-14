using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// CT guard (W3 live E4, 2026-10-13): an inline L10n bridge <c>&lt;script&gt;</c> whose JavaScript brackets do not balance
/// once its <c>@Html.Raw(...)</c> expressions are rendered. Razor swallows the parentheses of <c>@Html.Raw(...)</c> itself,
/// so a missing <c>)</c> for the surrounding <c>Object.assign(</c> compiles fine and only fails in the browser
/// ("missing ) after argument list") — the whole bridge is skipped and every label of the page comes out empty.
/// The objection library shipped exactly that; key-presence tests and fake-DOM smokes could not see it.
/// <para>Each <c>@Html.Raw(...)</c> is replaced with <c>{}</c> (its balanced C# argument removed) and the remaining
/// JavaScript must balance <c>() [] {}</c> outside strings and comments. Scripts with any other Razor construct
/// (<c>@if</c>, <c>@foreach</c>, <c>@Model</c>…) are out of scope: their text is not JavaScript yet.</para>
/// </summary>
public sealed class RazorInlineScriptSyntaxTests
{
    private static readonly Regex InlineScript = new(@"<script(?<attrs>[^>]*)>(?<body>.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    [Fact]
    public void Inline_bridge_scripts_balance_their_brackets_once_html_raw_is_rendered()
    {
        var failures = new List<string>();
        var checkedCount = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(WebRoot(), "Views"), "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file).Replace("\r\n", "\n");
            foreach (Match m in InlineScript.Matches(text))
            {
                if (m.Groups["attrs"].Value.Contains("src=", StringComparison.OrdinalIgnoreCase)) continue;
                if (m.Groups["attrs"].Value.Contains("application/json", StringComparison.OrdinalIgnoreCase)) continue;
                var body = m.Groups["body"].Value;
                if (!body.Contains("@Html.Raw(", StringComparison.Ordinal)) continue;
                var js = RenderHtmlRaw(body);
                if (js is null || js.Contains('@')) continue; // other Razor inside: not plain JavaScript
                checkedCount++;
                var error = BracketError(js);
                if (error is not null)
                {
                    failures.Add(Path.GetRelativePath(WebRoot(), file) + ": " + error);
                }
            }
        }

        Assert.True(checkedCount > 0, "no inline @Html.Raw bridge script found — the scan is broken");
        Assert.True(failures.Count == 0, "inline script brackets do not balance:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void The_check_catches_the_objection_library_defect()
    {
        const string fixedLine = "window.L10n = Object.assign({}, window.L10n || {}, @Html.Raw(Serialize(new[] { \"a\" }.ToDictionary(k => k, k => k))));";
        const string defect = "window.L10n = Object.assign({}, window.L10n || {}, @Html.Raw(Serialize(new[] { \"a\" }.ToDictionary(k => k, k => k)));";
        Assert.Null(BracketError(RenderHtmlRaw(fixedLine)!));
        Assert.NotNull(BracketError(RenderHtmlRaw(defect)!));
    }

    /// <summary>Each <c>@Html.Raw( … )</c> (balanced, C# strings respected) → <c>{}</c>; null when one never closes.</summary>
    private static string? RenderHtmlRaw(string body)
    {
        const string token = "@Html.Raw(";
        var sb = new StringBuilder();
        var i = 0;
        while (true)
        {
            var at = body.IndexOf(token, i, StringComparison.Ordinal);
            if (at < 0) { sb.Append(body, i, body.Length - i); return sb.ToString(); }
            sb.Append(body, i, at - i).Append("{}");
            var depth = 1;
            var j = at + token.Length;
            for (; j < body.Length && depth > 0; j++)
            {
                var c = body[j];
                if (c == '"') { j = SkipString(body, j, '"'); continue; }
                if (c == '(') depth++;
                else if (c == ')') depth--;
            }
            if (depth != 0) return null;
            i = j;
        }
    }

    /// <summary>First bracket error of the JavaScript, or null when <c>() [] {}</c> balance outside strings and comments.</summary>
    private static string? BracketError(string js)
    {
        var stack = new Stack<(char Open, int At)>();
        for (var i = 0; i < js.Length; i++)
        {
            var c = js[i];
            if (c is '"' or '\'' or '`') { i = SkipString(js, i, c); continue; }
            if (c == '/' && i + 1 < js.Length && js[i + 1] == '/') { i = js.IndexOf('\n', i) is var n && n < 0 ? js.Length : n; continue; }
            if (c == '/' && i + 1 < js.Length && js[i + 1] == '*') { i = js.IndexOf("*/", i + 2, StringComparison.Ordinal) is var e && e < 0 ? js.Length : e + 1; continue; }
            if (c is '(' or '[' or '{') { stack.Push((c, i)); continue; }
            if (c is ')' or ']' or '}')
            {
                var want = c == ')' ? '(' : c == ']' ? '[' : '{';
                if (stack.Count == 0 || stack.Peek().Open != want) return $"unexpected '{c}' at {i}: …{Around(js, i)}…";
                stack.Pop();
            }
        }
        return stack.Count == 0 ? null : $"'{stack.Peek().Open}' at {stack.Peek().At} never closes: …{Around(js, stack.Peek().At)}…";
    }

    private static int SkipString(string s, int start, char quote)
    {
        for (var i = start + 1; i < s.Length; i++)
        {
            if (s[i] == '\\') { i++; continue; }
            if (s[i] == quote) return i;
        }
        return s.Length;
    }

    private static string Around(string s, int at) => s.Substring(Math.Max(0, at - 40), Math.Min(80, s.Length - Math.Max(0, at - 40))).Replace("\n", " ");

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
