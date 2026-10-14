using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>Strict parsers for wire strings (UUIDs, UTC timestamps, dates, page numbers).</summary>
public static class WireParsing
{
    /// <summary>ISO-8601 date-time that is explicitly UTC (<c>Z</c> or a zero offset) — pack §12.</summary>
    private static readonly Regex UtcTimestamp = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]00:00)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseUuid(string? raw, out Guid value)
    {
        value = Guid.Empty;
        return raw is not null
               && Guid.TryParseExact(raw, "D", out value)
               && value != Guid.Empty;
    }

    public static bool TryParseUtcTimestamp(string? raw, out DateTimeOffset value)
    {
        value = default;
        return raw is not null
               && UtcTimestamp.IsMatch(raw)
               && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
               && value.Offset == TimeSpan.Zero;
    }

    /// <summary>Contract <c>format: date</c> (<c>yyyy-MM-dd</c>), read as a UTC day.</summary>
    public static bool TryParseUtcDate(string? raw, out DateTimeOffset dayStartUtc)
    {
        dayStartUtc = default;
        if (raw is null || !DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return false;
        }

        dayStartUtc = new DateTimeOffset(day.Year, day.Month, day.Day, 0, 0, 0, TimeSpan.Zero);
        return true;
    }

    public static bool TryParsePositiveInt(string? raw, out int value)
    {
        value = 0;
        return raw is not null
               && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value)
               && value >= 1;
    }
}
