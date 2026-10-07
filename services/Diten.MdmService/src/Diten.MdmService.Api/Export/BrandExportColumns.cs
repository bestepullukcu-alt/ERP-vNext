using System.Globalization;
using System.Resources;
using Diten.BuildingBlocks.ListExport;
using Diten.MdmService.Application.Features.Brand;

namespace Diten.MdmService.Api.Export;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (item 1, BL-452) — the brand register export's columns: the list DTO's field names in the
/// screen's column order. Headers, the status and yes / no are the words the screen shows, in the request culture
/// (Resources/BrandExport.*.resx, seven tenant languages). The business unit and therapeutic area read "could not be
/// resolved" for a stored reference, exactly as the screen does — never the raw id. Dates are ISO (yyyy-MM-dd; updatedAt
/// with UTC time) so a spreadsheet sorts them.
/// </summary>
internal static class BrandExportColumns
{
    public const string Screen = "brands";

    private static readonly ResourceManager Text = new("Diten.MdmService.Api.Resources.BrandExport", typeof(BrandExportColumns).Assembly);

    public static string Label(string key, CultureInfo culture) => Text.GetString(key, culture) ?? key;

    // A status the resources do not name (added later) is written as it came, never as a resource key.
    private static string Status(string? value, CultureInfo culture)
        => string.IsNullOrEmpty(value) ? string.Empty : Text.GetString("Status_" + value, culture) ?? value;

    private static string Reference(Guid? value, CultureInfo culture)
        => value is { } id && id != Guid.Empty ? Label("Unresolved", culture) : string.Empty;

    // FIX3 (R-5) — validity dates are calendar days (BrandProductCalendar): written as the day entered, never shifted by UTC.
    private static string Date(DateTimeOffset? value)
        => value is { } v
            ? Diten.MdmService.Domain.Vocabulary.BrandProductCalendar.Day(v).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Empty;

    public static readonly ListExportColumnSet<BrandDetailDto> Set = new(
    [
        new("brandCode", c => Label("BrandCode", c), (x, _) => x.BrandCode),
        new("brandName", c => Label("BrandName", c), (x, _) => x.BrandName),
        new("brandStatus", c => Label("BrandStatus", c), (x, c) => Status(x.BrandStatus, c)),
        new("businessUnitId", c => Label("BusinessUnitId", c), (x, c) => Reference(x.BusinessUnitId, c)),
        new("therapeuticAreaId", c => Label("TherapeuticAreaId", c), (x, c) => Reference(x.TherapeuticAreaId, c)),
        new("effectiveFrom", c => Label("EffectiveFrom", c), (x, _) => Date(x.EffectiveFrom)),
        new("effectiveTo", c => Label("EffectiveTo", c), (x, _) => Date(x.EffectiveTo)),
        new("isArchived", c => Label("Archived", c), (x, c) => Label(x.IsArchived ? "Yes" : "No", c)),
        new("updatedAt", c => Label("UpdatedAt", c),
            (x, _) => x.UpdatedAt?.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty)
    ]);
}
