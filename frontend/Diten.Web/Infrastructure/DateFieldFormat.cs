using System.Globalization;

namespace Diten.Web.Infrastructure;

/// <summary>
/// The date a field SHOWS, in the reader's culture: the culture's short date pattern turned into flatpickr tokens — the
/// conversion the Organization / Legal Entities forms already write locally (their _FormL10n.cshtml / _IndexL10n.cshtml
/// <c>ToFlatpickr(CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern)</c>), here once and testable. A page hands
/// it to flatpickr as <c>altFormat</c> with <c>altInput: true</c>; the value the input SENDS stays <c>dateFormat: 'Y-m-d'</c>,
/// so no contract changes. tr → <c>d.m.Y</c> (15.10.2026), en → <c>n/j/Y</c>, ar → the culture's own pattern (its
/// right-to-left marks kept, flatpickr writes unknown characters as they are).
/// </summary>
public static class DateFieldFormat
{
    /// <summary>The flatpickr display format of <paramref name="culture"/>'s short date pattern.</summary>
    public static string Flatpickr(CultureInfo culture) => FromDotNet(culture.DateTimeFormat.ShortDatePattern);

    /// <summary>A .NET date pattern → flatpickr tokens (dd → d, d → j, MM → m, M → n, yyyy → Y, yy → y).</summary>
    public static string FromDotNet(string pattern) =>
        (pattern ?? string.Empty).Replace("dd", "\x01").Replace("d", "\x02")
            .Replace("MM", "\x03").Replace("M", "\x04")
            .Replace("yyyy", "Y").Replace("yy", "y")
            .Replace("\x01", "d").Replace("\x02", "j").Replace("\x03", "m").Replace("\x04", "n");
}
