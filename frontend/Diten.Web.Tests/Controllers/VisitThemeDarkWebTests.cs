using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-DARK (CT-4) — the visit workspace and the presentation read in the dark theme: each file carries a
/// <c>[data-bs-theme="dark"]</c> block on its own token roots that redefines the heading / text / muted text / border / soft
/// background / card tokens (00-tema.css dark palette) and keeps the primary the layout's (K-THEME); the light values are
/// unchanged (the new surface tokens default to the former light colours) and no white surface is hard-coded any more.
/// </summary>
public sealed class VisitThemeDarkWebTests
{
    [Theory]
    [InlineData("VisitWorkspace/visit-workspace.css", "vw", "#vw-root")]
    [InlineData("VisitPresentation/visit-presentation.css", "vp", "#vp-root")]
    public void D1_each_file_has_a_dark_block_with_the_heading_text_surface_and_card_tokens(string file, string prefix, string root)
    {
        var css = Css(file);
        var dark = DarkBlock(css, root);
        Assert.False(string.IsNullOrEmpty(dark), $"{file}: no [data-bs-theme=\"dark\"] {root} block");
        foreach (var (token, value) in new[] { ("h", "#d5d5e2"), ("tx", "#b2b2c4"), ("mu", "#8a8ba5"), ("bd", "#3d3e58"), ("soft", "#34354e"), ("card", "#2b2c40") })
        {
            Assert.Contains($"--{prefix}-{token}: {value};", dark);                                    // the 00-tema.css dark palette
        }

        Assert.Contains($"--{prefix}-p: var(--bs-primary);", dark);                                       // K-THEME: the layout's primary
        Assert.Contains($"--{prefix}-pt: color-mix(in srgb, var(--bs-primary) 60%, #fff);", dark);       // a readable text tone
        Assert.Contains($"--{prefix}-pl: rgba(var(--bs-primary-rgb), .22);", dark);
        Assert.Contains("color-scheme: dark;", dark);

        // the light block is as it was (the dark one never leaks into it)
        var light = css[..css.IndexOf("[data-bs-theme=\"dark\"]", StringComparison.Ordinal)];
        Assert.Contains($"--{prefix}-h: #384551;", light);
        Assert.Contains($"--{prefix}-card: #fff;", light);
        Assert.Contains($"--{prefix}-p: var(--bs-primary);", light);
    }

    [Fact]
    public void D2_the_workspace_surfaces_are_tokens_so_the_dark_block_reaches_them()
    {
        var css = Css("VisitWorkspace/visit-workspace.css");
        // every "background: #fff" left is the camera shutter (a white button on purpose); the rest read --vw-card
        var whites = Regex.Matches(css, @"[^\n]*background:\s*#fff\b[^\n]*").Select(m => m.Value).ToList();
        Assert.All(whites, line => Assert.Contains(".vw-e3-shutter", line));
        Assert.Contains(".vw-e3-head, .vw-e3-foot { background: var(--vw-card); }", css);              // the start window (phone)
        Assert.Contains(".vw-e3-box { background: var(--vw-card);", css);
        Assert.Contains("var(--vw-soft) 0 8px, var(--vw-card) 8px 16px)", css);                         // the photo area
        Assert.Contains("--fc-today-bg-color: var(--vw-today);", css);                                   // the calendar's today
        Assert.Contains("--vw-today: rgba(224, 226, 243, .45);", css);                                   // light: as before
        Assert.DoesNotContain("rgba(34, 48, 62, .08)", css[css.IndexOf("[dir=\"rtl\"] .vw-flip", StringComparison.Ordinal)..]); // shadows are tokens
    }

    private static string DarkBlock(string css, string root)
    {
        var at = css.IndexOf("[data-bs-theme=\"dark\"] " + root, StringComparison.Ordinal);
        if (at < 0) return string.Empty;
        var open = css.IndexOf('{', at);
        var close = css.IndexOf('}', open);
        return css[open..close].Replace("\r\n", "\n");
    }

    private static string Css(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        var root = Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web", "wwwroot", "assets", "js", "CRM");
        return File.ReadAllText(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n");
    }
}
