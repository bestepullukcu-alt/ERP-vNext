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

    [Theory]
    [InlineData("frontend/Diten.Web/Services/Localization/FlatpickrDateFormat.cs")]
    [InlineData("frontend/Diten.Web.Tests/Services/FlatpickrDateFormatTests.cs")]
    [InlineData("frontend/Diten.Web/wwwroot/assets/js/shared/diten-datefield.js")]
    public void The_marks_are_written_as_escapes_never_as_invisible_characters(string relative)
    {
        // An editor that strips invisible characters would empty the list (and make the Arabic case pass vacuously).
        var source = File.ReadAllText(Path.Combine(Path.GetDirectoryName(FindWebRoot())!, "..", relative));
        Assert.DoesNotContain(source, ch => FlatpickrDateFormat.IsBidiMark(ch));
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
