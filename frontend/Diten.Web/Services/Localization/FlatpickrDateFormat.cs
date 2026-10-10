using System.Globalization;

namespace Diten.Web.Services.Localization;

/// <summary>
/// WCN-FIX-04 — the request culture's short date as a flatpickr format (the visible altInput's format), in ONE place.
///
/// <para>Five views carried their own copy of this conversion. Measured on .NET (ICU): Arabic's ShortDatePattern is
/// <c>d‏/M‏/yyyy</c> with a RIGHT-TO-LEFT MARK (U+200F) before each separator. The copies passed the marks through,
/// and flatpickr treats every non-token character of a format as "any one character": a reader typing
/// <c>5/3/2026</c> was parsed as 5 February (the mark slot ate the "/" and the "3"), and nobody can type the marks.
/// Bidirectional formatting marks carry no date meaning; they are dropped.</para>
///
/// <para>Tokens: <c>dd→d</c>, <c>d→j</c>, <c>MM→m</c>, <c>M→n</c>, <c>yyyy→Y</c>, <c>yy→y</c>; separators kept.</para>
/// </summary>
public static class FlatpickrDateFormat
{
    private static readonly char[] BidiMarks = ['‎', '‏', '؜'];

    public static string FromShortDatePattern(string netPattern)
    {
        var pattern = string.Concat((netPattern ?? string.Empty).Where(ch => Array.IndexOf(BidiMarks, ch) < 0));
        return pattern
            .Replace("dd", "\x01").Replace("d", "\x02")
            .Replace("MM", "\x03").Replace("M", "\x04")
            .Replace("yyyy", "Y").Replace("yy", "y")
            .Replace("\x01", "d").Replace("\x02", "j").Replace("\x03", "m").Replace("\x04", "n");
    }

    public static string ForCulture(CultureInfo culture) =>
        FromShortDatePattern(culture.DateTimeFormat.ShortDatePattern);

    public static string ForCurrentCulture() => ForCulture(CultureInfo.CurrentCulture);
}
