using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Tests.Traceability;
using Xunit;

namespace Diten.Web.Tests.Warehouse;

/// <summary>
/// MOD-0178-FU01 DL-1 CT live tour (2026-11-13): <c>@Localizer["LinePanel.Unit"]</c> with the value <c>Base unit: {0}</c> and no argument made
/// the HTML localizer format the string and throw, so <c>/Warehouse/OutboundShipments/Create</c> stopped rendering mid-page
/// (ERR_INCOMPLETE_CHUNKED_ENCODING). A key whose value carries a <c>{n}</c> placeholder is written either with its arguments
/// (<c>@Localizer["Key", arg]</c>) or as a raw template for the script (<c>@Localizer["Key"].Value</c>; emsal CRM LegalProfiles _Form.cshtml).
/// This guard reads every Warehouse view and the Warehouse view resources (en) and refuses a bare <c>@Localizer["Key"]</c> on such a key.
/// </summary>
public sealed class WarehouseLocalizerPlaceholderGuardTests
{
    private static readonly Regex Bare = new(@"@Localizer\[""(?<key>[^""]+)""\](?!\s*\.Value)", RegexOptions.Compiled);
    private static readonly Regex Placeholder = new(@"\{\d+(,[^}]*)?(:[^}]*)?\}", RegexOptions.Compiled);

    [Fact]
    public void No_warehouse_view_renders_a_placeholder_key_without_its_arguments()
    {
        var root = TraceViewRenderer.RepoRoot();
        var resources = Path.Combine(root, "frontend", "Diten.Web", "Resources", "Views", "Warehouse");
        var withPlaceholder = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resx in Directory.EnumerateFiles(resources, "*.en.resx", SearchOption.AllDirectories))
        {
            foreach (var data in XDocument.Load(resx).Root!.Elements("data"))
            {
                var value = data.Element("value")?.Value ?? "";
                if (Placeholder.IsMatch(value))
                {
                    withPlaceholder.Add((string)data.Attribute("name")!);
                }
            }
        }

        Assert.Contains("LinePanel.Unit", withPlaceholder); // the measured key: the guard reads the real resources

        var views = Path.Combine(root, "frontend", "Diten.Web", "Views", "Warehouse");
        var offenders = new List<string>();
        foreach (var view in Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(view);
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (Match m in Bare.Matches(lines[i]))
                {
                    if (withPlaceholder.Contains(m.Groups["key"].Value))
                    {
                        offenders.Add($"{Path.GetRelativePath(root, view)}:{i + 1} {m.Groups["key"].Value}");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "A key with a {n} placeholder is rendered without its arguments (the HTML localizer throws while rendering): "
            + string.Join("; ", offenders));
    }
}
