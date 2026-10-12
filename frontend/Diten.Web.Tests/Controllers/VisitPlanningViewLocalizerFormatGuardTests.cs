using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// CT hotfix guard (Faz 4 E4, 2026-10-08). <c>@Localizer["Key"]</c> in a Razor view renders a LocalizedHtmlString that
/// runs <c>string.Format</c> on write: a resource value with a <c>{0}</c> placeholder and NO argument throws a
/// FormatException in the middle of the page, so the response is cut off (ERR_INCOMPLETE_CHUNKED_ENCODING) and the
/// browser keeps reloading. The unit tests never render views, which is how 4C's "BulkApplyProducts" shipped. This guard
/// reads every Visit Planning view and every culture of its resources: a key used without arguments must not carry a
/// placeholder in any language.
/// </summary>
public sealed class VisitPlanningViewLocalizerFormatGuardTests
{
    private static readonly Regex ArglessLocalizer = new(@"Localizer\[""(?<key>[^""]+)""\](?!\s*\.Value)", RegexOptions.Compiled);
    private static readonly Regex Placeholder = new(@"\{\d+(,[^}]*)?(:[^}]*)?\}", RegexOptions.Compiled);

    [Fact]
    public void A_view_localizer_key_used_without_arguments_has_no_format_placeholder_in_any_culture()
    {
        var root = WebRoot();
        var resourceDir = Path.Combine(root, "Resources", "Views", "CRM", "VisitPlanning");
        var values = Directory.GetFiles(resourceDir, "VisitPlanningIndex*.resx")
            .SelectMany(file => XDocument.Load(file).Root!.Elements("data")
                .Select(d => (File: Path.GetFileName(file), Key: (string)d.Attribute("name")!, Value: (string?)d.Element("value") ?? string.Empty)))
            .ToList();

        var offenders = new List<string>();
        foreach (var view in Directory.GetFiles(Path.Combine(root, "Views", "CRM", "VisitPlanning"), "*.cshtml"))
        {
            var text = File.ReadAllText(view);
            foreach (Match m in ArglessLocalizer.Matches(text))
            {
                var key = m.Groups["key"].Value;
                offenders.AddRange(values
                    .Where(v => v.Key == key && Placeholder.IsMatch(v.Value))
                    .Select(v => $"{Path.GetFileName(view)}: Localizer[\"{key}\"] without arguments, {v.File} = \"{v.Value}\""));
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
