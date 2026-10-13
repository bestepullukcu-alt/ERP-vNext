using System.Globalization;
using System.Resources;
using Diten.BuildingBlocks.ListExport;
using Diten.ManufacturingService.Application.Features.Boms;

namespace Diten.ManufacturingService.Api.Export;

/// <summary>
/// BL-452 — the BOM list export's columns: the list item's field names in the screen's column order. Headers and status
/// values are the words the screen shows, in the request culture (Resources/BomExport.*.resx, seven tenant languages).
/// </summary>
internal static class BomExportColumns
{
    public const string Screen = "bom-versions";

    private static readonly ResourceManager Text = new("Diten.ManufacturingService.Api.Resources.BomExport", typeof(BomExportColumns).Assembly);

    public static string Label(string key, CultureInfo culture) => Text.GetString(key, culture) ?? key;

    private static string Date(DateTimeOffset? value, CultureInfo culture) =>
        value is { } v ? v.UtcDateTime.ToString("g", culture) : string.Empty;

    public static readonly ListExportColumnSet<BomListItem> Set = new(
    [
        new("itemId", c => Label("ItemId", c), (x, _) => x.ItemId.ToString()),
        new("version", c => Label("Version", c), (x, _) => x.Version.ToString(CultureInfo.InvariantCulture)),
        new("status", c => Label("Status", c), (x, c) => Text.GetString("Status" + x.Status, c) ?? x.Status),
        new("description", c => Label("Description", c), (x, _) => x.Description),
        new("componentCount", c => Label("ComponentCount", c), (x, _) => x.ComponentCount.ToString(CultureInfo.InvariantCulture)),
        new("stepCount", c => Label("StepCount", c), (x, _) => x.StepCount.ToString(CultureInfo.InvariantCulture)),
        new("effectiveFrom", c => Label("EffectiveFrom", c), (x, c) => Date(x.EffectiveFrom, c)),
        new("effectiveTo", c => Label("EffectiveTo", c), (x, c) => Date(x.EffectiveTo, c)),
        new("updatedAt", c => Label("UpdatedAt", c), (x, c) => Date(x.UpdatedAt, c))
    ]);
}
