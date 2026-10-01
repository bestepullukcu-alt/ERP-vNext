using System.Globalization;

namespace Diten.BuildingBlocks.ListExport;

public enum ListExportFormat
{
    Csv,
    Xlsx
}

/// <summary>
/// BL-452 package 1 — THE FILE IS THE SCREEN. The wire contract every server-mode list's export endpoint answers:
///
/// <code>GET {list}/export?format=csv|xlsx&amp;columns=a,b,…&amp;search=…&amp;orderBy=…&amp;orderDir=…&amp;{filter keys}</code>
///
/// <list type="bullet">
/// <item>The SAME query parameters as the list (search, orderBy, orderDir, filters) run through the SAME query — the export
/// is not a second query that could disagree with what the reader saw. <c>start</c>/<c>length</c> are NOT part of it: the
/// file holds every matching row, not the page on screen.</item>
/// <item><c>columns</c> = the visible columns' keys in screen order (repeated or comma-separated). Each service owns a
/// whitelist (<see cref="ListExportColumnSet{T}"/>); an unknown key is a 400, never a silently skipped or leaked column.
/// None given = every exportable column.</item>
/// <item>At most <see cref="MaxRows"/> rows. More → 413 <see cref="TooLargeCode"/>; the background job of package 4 takes
/// those, a silently truncated file never ships.</item>
/// <item>File name <c>{screen}-{yyyyMMdd-HHmm}.{csv|xlsx}</c> (UTC); headers and display values in the request culture
/// (<c>Accept-Language</c>, one of the seven tenant languages, English otherwise).</item>
/// </list>
/// </summary>
public static class ListExportContract
{
    public const int MaxRows = 50_000;

    public const string TooLargeCode = "EXPORT_TOO_LARGE";
    public const string FormatInvalidCode = "EXPORT_FORMAT_INVALID";
    public const string ColumnsInvalidCode = "EXPORT_COLUMNS_INVALID";

    public const string CsvContentType = "text/csv; charset=utf-8";
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>The tenant languages (localization-standard: Tenant = 7). The first is the fallback.</summary>
    public static readonly IReadOnlyList<string> SupportedCultures = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>No <c>format</c> = CSV. Anything but csv/xlsx (case-insensitive) is refused.</summary>
    public static bool TryParseFormat(string? value, out ListExportFormat format)
    {
        format = ListExportFormat.Csv;
        if (string.IsNullOrWhiteSpace(value)) return true;
        switch (value.Trim().ToLowerInvariant())
        {
            case "csv":
                return true;
            case "xlsx":
                format = ListExportFormat.Xlsx;
                return true;
            default:
                return false;
        }
    }

    public static string ContentType(ListExportFormat format) => format == ListExportFormat.Xlsx ? XlsxContentType : CsvContentType;

    public static string Extension(ListExportFormat format) => format == ListExportFormat.Xlsx ? "xlsx" : "csv";

    public static string FileName(string screen, DateTimeOffset at, ListExportFormat format)
        => $"{screen}-{at.UtcDateTime.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.{Extension(format)}";

    /// <summary>
    /// The request culture from <c>Accept-Language</c>: the first listed tag (in the caller's q-order) whose language is a
    /// tenant language. <c>tr-TR</c> → tr, <c>zh-Hans</c> → zh. Nothing usable → English. Read HERE, per request, instead of
    /// a service-wide RequestLocalization middleware that would also change every other endpoint's formatting.
    /// </summary>
    public static CultureInfo ResolveCulture(string? acceptLanguage)
    {
        var ranked = (acceptLanguage ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((part, position) =>
            {
                var pieces = part.Split(';', StringSplitOptions.TrimEntries);
                var quality = 1.0;
                foreach (var piece in pieces.Skip(1))
                {
                    if (piece.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                        && double.TryParse(piece[2..], NumberStyles.Float, CultureInfo.InvariantCulture, out var q))
                        quality = q;
                }

                var language = pieces[0].Split('-', '_')[0].ToLowerInvariant();
                return (language, quality, position);
            })
            .Where(x => x.quality > 0)
            .OrderByDescending(x => x.quality)
            .ThenBy(x => x.position);

        foreach (var (language, _, _) in ranked)
        {
            if (SupportedCultures.Contains(language)) return CultureInfo.GetCultureInfo(language);
        }

        return CultureInfo.GetCultureInfo(SupportedCultures[0]);
    }
}
