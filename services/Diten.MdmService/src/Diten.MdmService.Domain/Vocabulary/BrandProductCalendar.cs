namespace Diten.MdmService.Domain.Vocabulary;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX3 (R-5) — THE one rule for validity dates. EffectiveFrom / EffectiveTo are CALENDAR days: the day
/// the user entered, whatever clock offset travelled with it (an end day typed in Istanbul at +03:00 is that day, not the day
/// before in UTC). <see cref="Day"/> reads that day from the value's own clock, so documents already stored with an offset
/// read correctly too; <see cref="Normalize"/> stores new values as midnight of that day at offset zero.
/// <para>"Today" is the tenant's day. MDM has no source for a tenant time zone (measured 2026-10-07: no tenant time-zone
/// setting reaches MDM, and Platform offers no endpoint for one), so it is the UTC day — never the server's offset.</para>
/// </summary>
public static class BrandProductCalendar
{
    public static DateOnly Day(DateTimeOffset value) => DateOnly.FromDateTime(value.DateTime);

    public static DateOnly Today(DateTimeOffset asOf) => DateOnly.FromDateTime(asOf.UtcDateTime);

    public static DateTimeOffset Normalize(DateTimeOffset value) => new(value.DateTime.Date, TimeSpan.Zero);

    public static DateTimeOffset? Normalize(DateTimeOffset? value) => value is { } v ? Normalize(v) : null;

    /// <summary>Inside the window on <paramref name="asOf"/>: from its first day through its last day, both included.</summary>
    public static bool IsWithin(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, DateTimeOffset asOf)
    {
        var today = Today(asOf);
        return Day(effectiveFrom) <= today && (effectiveTo is null || Day(effectiveTo.Value) >= today);
    }

    /// <summary>The end day is not before the start day.</summary>
    public static bool IsValidWindow(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
        => effectiveTo is null || Day(effectiveTo.Value) >= Day(effectiveFrom);
}
