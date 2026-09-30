namespace Diten.Web.Views.TimeEntry;

/// <summary>MOD-0280-FU01 T2a — the resource marker of My Timesheet and the top-bar timer chip
/// (<c>Resources/Views/TimeEntry/TimeEntryIndex.{en,tr,fr,es,zh,ar,ru}.resx</c>).</summary>
public sealed class TimeEntryIndex
{
    /// <summary>
    /// The v1 limits the page's sentences NAME (v3 L10: "16 hours" is never typed into a translation). They are
    /// Platform's rules (<c>TimeEntryLimits</c>, pack §4.7) — the web tier cannot reference that assembly, so they are
    /// repeated here ONCE, and <c>TimeEntryViewContractTests</c> reads Platform's source and fails if the two drift. The
    /// page only shows them; every check stays on the server.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> Limits = new Dictionary<string, int>
    {
        ["StepMinutes"] = 15,
        ["MaxRowMinutes"] = 960,
        ["ImplausibleDayMinutes"] = 960,
        ["NoteMaxLength"] = 500
    };
}
