namespace Diten.Web.Models.MasterData;

/// <summary>
/// P-2026-10-07-BRAND-FIX4 (K4) — the web's one copy of the calendar-day rule for validity dates. A validity date is a
/// calendar day: the day the operator entered, read in the offset it was entered in — never shifted to UTC. The web
/// cannot call MDM's Domain, so every place the web compares or defaults a validity date goes through here.
/// </summary>
public static class CalendarDay
{
    /// <summary>The calendar day the value was entered as (its own offset), never its UTC day.</summary>
    public static DateTime Of(DateTimeOffset value) => value.DateTime.Date;

    /// <summary>True when both days are entered and the end day is earlier than the start day.</summary>
    public static bool EndsBeforeStart(DateTimeOffset? from, DateTimeOffset? to)
        => from.HasValue && to.HasValue && Of(to.Value) < Of(from.Value);

    /// <summary>A create form's default validity start: today's UTC calendar day at midnight +00:00.</summary>
    public static DateTimeOffset TodayUtc() => new(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
}
