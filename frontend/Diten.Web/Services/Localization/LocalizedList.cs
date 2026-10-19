namespace Diten.Web.Services.Localization;

/// <summary>
/// MOD-0290-FU04 S5 (D-19) — a list of labels joined the way the reader's language joins them: two items with its own
/// conjunction ("{0} and {1}", "{0} ve {1}", "{0}和{1}", "{0} و{1}" …), more with its own separator before the last pair.
/// The two patterns come from the page's resx (ListJoinTwo, ListSeparator), never from code.
/// </summary>
public static class LocalizedList
{
    public static string Join(IReadOnlyList<string> items, string joinTwo, string separator) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => string.Format(System.Globalization.CultureInfo.CurrentCulture, joinTwo,
            string.Join(separator, items.Take(items.Count - 1)), items[^1])
    };
}
