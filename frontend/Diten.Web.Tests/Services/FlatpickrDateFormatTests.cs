using Diten.Web.Services.Localization;
using Xunit;

namespace Diten.Web.Tests.Services;

/// <summary>
/// WCN-FIX-04 — the culture's short date → flatpickr format, once. The patterns below are the seven tenant languages'
/// ShortDatePattern as MEASURED on .NET 8 + ICU (macOS, 2026-10-10; Arabic carries U+200F before each separator).
/// They are written out, not read from CultureInfo here, because the CI runner's globalization mode can differ
/// (invariant culture) and would turn this into a test of the runner.
/// </summary>
public sealed class FlatpickrDateFormatTests
{
    [Theory]
    [InlineData("M/d/yyyy", "n/j/Y")]          // en
    [InlineData("d.MM.yyyy", "j.m.Y")]         // tr
    [InlineData("dd/MM/yyyy", "d/m/Y")]        // fr
    [InlineData("d/M/yyyy", "j/n/Y")]          // es
    [InlineData("yyyy/M/d", "Y/n/j")]          // zh
    [InlineData("d\u200F/M\u200F/yyyy", "j/n/Y")] // ar — the right-to-left marks are dropped
    [InlineData("dd.MM.yyyy", "d.m.Y")]        // ru
    [InlineData("dd/MM/yy", "d/m/y")]
    public void The_short_date_pattern_becomes_a_flatpickr_format_without_bidi_marks(string net, string expected)
    {
        Assert.Equal(expected, FlatpickrDateFormat.FromShortDatePattern(net));
    }

    [Theory]
    [InlineData("d\u202B/M\u202E/yyyy", "j/n/Y")]  // embedding / override controls U+202A–202E
    [InlineData("d\u2066/M\u2069/yyyy", "j/n/Y")]  // isolates U+2066–2069
    [InlineData("d\u202A/M\u2068/yyyy\u202C", "j/n/Y")]
    public void The_embedding_and_isolate_controls_are_dropped_too(string net, string expected)
    {
        Assert.Equal(expected, FlatpickrDateFormat.FromShortDatePattern(net));
    }

    /// <summary>
    /// WCN-FIX-06 — every file of the date guard is scanned, derived from the folders rather than listed: the C# and JS
    /// sources, and the tests (an editor that strips invisible characters would empty a list or make an Arabic case pass
    /// vacuously; FIX5's own test file carried one). Resource files are NOT scanned: Arabic text legitimately carries
    /// right-to-left marks.
    /// </summary>
    public static TheoryData<string> GuardFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in GuardFilePaths())
        {
            data.Add(file);
        }

        return data;
    }

    private static List<string> GuardFilePaths()
    {
        var web = FindWebRoot();
        var repo = Path.GetFullPath(Path.Combine(web, "..", ".."));
        var files = new List<string>
        {
            Path.Combine(web, "wwwroot", "assets", "js", "shared", "diten-datefield.js"),
            Path.Combine(repo, "frontend", "Diten.Web.Tests", "Services", "FlatpickrDateFormatTests.cs")
        };
        files.AddRange(Directory.GetFiles(Path.Combine(web, "Services", "Localization"), "*.cs"));
        // WCN-FIX-07 — DERIVED, not named: every test that loads the date guard (its typed text is what carries marks),
        // plus the date-field-* family. A file named here by hand silently left the scan when it was renamed.
        files.AddRange(Directory.GetFiles(Path.Combine(web, "tests"), "*.test.js").Where(file =>
            Path.GetFileName(file).StartsWith("date-field-", StringComparison.Ordinal)
            || File.ReadAllText(file).Contains("shared/diten-datefield.js", StringComparison.Ordinal)));
        return files
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(file => Path.GetRelativePath(repo, file).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
    }

    [Fact]
    public void The_scan_reaches_the_guard_files()
    {
        var files = GuardFilePaths();
        Assert.Contains("frontend/Diten.Web/tests/date-field-fix5.test.js", files);
        Assert.Contains("frontend/Diten.Web/wwwroot/assets/js/shared/diten-datefield.js", files);
        // WCN-FIX-07 — the screen tests that type dates reach the scan through the derivation, not a name list.
        Assert.Contains("frontend/Diten.Web/tests/wcn-subtask-due-escape.test.js", files);
        Assert.Contains("frontend/Diten.Web/tests/xmc-date-guard-screens.test.js", files);
        Assert.Contains("frontend/Diten.Web/tests/position-assignments-date-guard.test.js", files);
        Assert.True(files.Count >= 15, $"only {files.Count} files scanned");
    }

    [Theory]
    [MemberData(nameof(GuardFiles))]
    public void The_marks_are_written_as_escapes_never_as_invisible_characters(string relative)
    {
        var repo = Path.GetFullPath(Path.Combine(FindWebRoot(), "..", ".."));
        var source = File.ReadAllText(Path.Combine(repo, relative));
        var at = source.IndexOf(source.FirstOrDefault(FlatpickrDateFormat.IsBidiMark));
        Assert.False(source.Any(FlatpickrDateFormat.IsBidiMark),
            $"{relative} carries an invisible bidi mark at offset {at}; write it as a \\u escape");
    }

    /// <summary>WCN-FIX-06 — the JS normaliser and the C# format conversion drop the SAME marks.</summary>
    [Fact]
    public void The_js_and_the_csharp_mark_sets_are_the_same()
    {
        var js = File.ReadAllText(Path.Combine(FindWebRoot(), "wwwroot", "assets", "js", "shared", "diten-datefield.js"));
        var match = System.Text.RegularExpressions.Regex.Match(js, @"const BIDI_MARKS = /\[([^\]]+)\]/g;");
        Assert.True(match.Success, "BIDI_MARKS moved or changed shape in diten-datefield.js");

        var jsSet = new HashSet<char>();
        var body = match.Groups[1].Value;
        var tokens = System.Text.RegularExpressions.Regex.Matches(body, @"\\u([0-9A-Fa-f]{4})(?:-\\u([0-9A-Fa-f]{4}))?");
        foreach (System.Text.RegularExpressions.Match token in tokens)
        {
            var from = Convert.ToInt32(token.Groups[1].Value, 16);
            var to = token.Groups[2].Success ? Convert.ToInt32(token.Groups[2].Value, 16) : from;
            for (var code = from; code <= to; code++) { jsSet.Add((char)code); }
        }

        var csSet = Enumerable.Range(0, 0x10000).Select(code => (char)code).Where(FlatpickrDateFormat.IsBidiMark).ToHashSet();
        Assert.Equal(csSet.OrderBy(c => c), jsSet.OrderBy(c => c));
    }

    [Fact]
    public void No_bidi_mark_survives_in_any_form()
    {
        var format = FlatpickrDateFormat.FromShortDatePattern("d\u200E/M\u061C/yyyy\u200F");
        Assert.Equal("j/n/Y", format);
    }

    [Theory]
    [InlineData("Views/Organization/PositionAssignments/_FormL10n.cshtml")]
    [InlineData("Views/Organization/OrganizationUnits/_FormL10n.cshtml")]
    [InlineData("Views/Organization/Positions/_FormL10n.cshtml")]
    [InlineData("Views/MasterData/LegalEntities/_IndexL10n.cshtml")]
    [InlineData("Views/MasterData/LegalEntities/_WizardL10n.cshtml")]
    public void Every_date_view_uses_the_one_conversion_not_a_copy(string view)
    {
        var root = FindWebRoot();
        var source = File.ReadAllText(Path.Combine(root, view));
        Assert.Contains("FlatpickrDateFormat.ForCurrentCulture()", source);
        Assert.DoesNotContain("ToFlatpickr", source);
    }

    private static string FindWebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Views")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new DirectoryNotFoundException("frontend/Diten.Web not found above the test output.")
            : Path.Combine(dir.FullName, "frontend", "Diten.Web");
    }
}
