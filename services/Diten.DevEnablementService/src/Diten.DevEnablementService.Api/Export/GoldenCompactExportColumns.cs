using System.Globalization;
using System.Resources;
using Diten.BuildingBlocks.ListExport;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact;

namespace Diten.DevEnablementService.Api.Export;

/// <summary>
/// BL-452 — the Golden Compact export's columns: the list DTO's field names, in the screen's column order (<c>code, name,
/// referenceType, category, owner, version, priority, isActive</c>). Headers, the status and the reference type are the
/// words the screen shows, in the request culture (Resources/GoldenCompactExport.*.resx, seven tenant languages).
/// </summary>
internal static class GoldenCompactExportColumns
{
    public const string Screen = "golden-reference-compact";

    private static readonly ResourceManager Text = new("Diten.DevEnablementService.Api.Resources.GoldenCompactExport", typeof(GoldenCompactExportColumns).Assembly);

    public static string Label(string key, CultureInfo culture) => Text.GetString(key, culture) ?? key;

    // A value the resources do not name (a reference type added later) is written as it came, never as a resource key.
    private static string Value(string prefix, string? value, CultureInfo culture)
        => string.IsNullOrEmpty(value) ? string.Empty : Text.GetString(prefix + value, culture) ?? value;

    public static readonly ListExportColumnSet<GoldenReferenceCompactListItemDto> Set = new(
    [
        new("code", c => Label("Code", c), (x, _) => x.Code),
        new("name", c => Label("Name", c), (x, _) => x.Name),
        new("referenceType", c => Label("ReferenceType", c), (x, c) => Value("ReferenceType", x.ReferenceType, c)),
        new("category", c => Label("Category", c), (x, _) => x.Category),
        new("owner", c => Label("Owner", c), (x, _) => x.Owner),
        new("version", c => Label("Version", c), (x, _) => x.Version),
        new("priority", c => Label("Priority", c), (x, _) => x.Priority.ToString(CultureInfo.InvariantCulture)),
        new("isActive", c => Label("Status", c), (x, c) => Label(x.IsActive ? "StatusActive" : "StatusPassive", c))
    ]);
}
