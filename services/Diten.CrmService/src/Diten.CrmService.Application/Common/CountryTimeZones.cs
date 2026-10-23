namespace Diten.CrmService.Application.Common;

/// <summary>
/// WP-VW-W6 W6-FIX-c (V-17) — the time zone a document prints its times in: the SELLING COMPANY's country (the legal
/// entity's MDM <c>CountryCode</c>), never UTC by default. MDM's legal entity carries no time zone and CRM has no country
/// time zone master (FU-MDM-LE-TIMEZONE), so the IANA zone of the country's capital is kept here, next to the country
/// language rule (<see cref="ExternalRecipientLanguages"/>) — for a country of several zones (RU, KZ …) that is the
/// capital's. A country outside the table, or a zone the host cannot load, prints in UTC and says so ("UTC").
/// </summary>
public static class CountryTimeZones
{
    public const string Utc = "UTC";

    private static readonly IReadOnlyDictionary<string, string> Zones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // the tenant's selling companies and the supported languages' countries
        ["TR"] = "Europe/Istanbul", ["FR"] = "Europe/Paris", ["ES"] = "Europe/Madrid", ["CN"] = "Asia/Shanghai",
        ["RO"] = "Europe/Bucharest", ["BG"] = "Europe/Sofia", ["GR"] = "Europe/Athens", ["DE"] = "Europe/Berlin", ["IT"] = "Europe/Rome",
        ["GB"] = "Europe/London", ["NL"] = "Europe/Amsterdam", ["PL"] = "Europe/Warsaw", ["CY"] = "Asia/Nicosia",
        // the Russian-speaking countries (ExternalRecipientLanguages.RussianCountries)
        ["RU"] = "Europe/Moscow", ["KZ"] = "Asia/Almaty", ["KG"] = "Asia/Bishkek", ["BY"] = "Europe/Minsk", ["UZ"] = "Asia/Tashkent",
        ["TJ"] = "Asia/Dushanbe", ["TM"] = "Asia/Ashgabat", ["AZ"] = "Asia/Baku", ["GE"] = "Asia/Tbilisi", ["UA"] = "Europe/Kiev",
        ["MD"] = "Europe/Chisinau",
        // the Arabic-speaking countries (ExternalRecipientLanguages.ArabicCountries)
        ["AE"] = "Asia/Dubai", ["BH"] = "Asia/Bahrain", ["DJ"] = "Africa/Djibouti", ["DZ"] = "Africa/Algiers", ["EG"] = "Africa/Cairo",
        ["IQ"] = "Asia/Baghdad", ["JO"] = "Asia/Amman", ["KM"] = "Indian/Comoro", ["KW"] = "Asia/Kuwait", ["LB"] = "Asia/Beirut",
        ["LY"] = "Africa/Tripoli", ["MA"] = "Africa/Casablanca", ["MR"] = "Africa/Nouakchott", ["OM"] = "Asia/Muscat", ["PS"] = "Asia/Hebron",
        ["QA"] = "Asia/Qatar", ["SA"] = "Asia/Riyadh", ["SD"] = "Africa/Khartoum", ["SO"] = "Africa/Mogadishu", ["SY"] = "Asia/Damascus",
        ["TN"] = "Africa/Tunis", ["YE"] = "Asia/Aden"
    };

    /// <summary>The country's zone and the name a document prints next to a time (the IANA id, or <c>UTC</c>).</summary>
    public static (TimeZoneInfo Zone, string Name) ForCountry(string? countryCode)
    {
        if (countryCode is { } code && Zones.TryGetValue(code.Trim(), out var id))
        {
            try
            {
                return (TimeZoneInfo.FindSystemTimeZoneById(id), id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // the host has no such zone: UTC, named
            }
        }

        return (TimeZoneInfo.Utc, Utc);
    }

    /// <summary>A moment as the wall clock of the country's zone (the offset is the zone's at that moment).</summary>
    public static DateTimeOffset? Local(DateTimeOffset? at, TimeZoneInfo zone) => at is { } moment ? TimeZoneInfo.ConvertTime(moment, zone) : null;
}
