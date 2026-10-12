using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// MOD-0193 BOM &amp; Routings is a TENANT module: its screens' resx carries the same keys in all SEVEN languages with a
/// value in each, and every <c>Localizer["Key"]</c> the BOM views use exists — read from the production views, not from a
/// list kept here, so a key added to a view without its translations turns this red.
/// </summary>
public sealed class BomsL10nTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void All_seven_languages_carry_the_same_keys_each_with_a_value()
    {
        var reference = Values("en").Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            var values = Values(language);
            Assert.True(reference.SetEquals(values.Keys),
                $"{language}: missing [{string.Join(", ", reference.Except(values.Keys))}] extra [{string.Join(", ", values.Keys.Except(reference))}]");
            Assert.All(values, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"{language}: '{pair.Key}' is empty"));
        }
    }

    [Fact]
    public void Every_key_the_bom_views_use_is_translated()
    {
        var viewsDir = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "Manufacturing", "Boms");
        var used = Directory.GetFiles(viewsDir, "*.cshtml")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(used);
        var translated = Values("en").Keys.ToHashSet(StringComparer.Ordinal);
        Assert.Empty(used.Except(translated));
    }

    [Fact]
    public void Turkish_is_not_english_with_another_name()
    {
        var en = Values("en");
        var tr = Values("tr");
        var same = en.Where(p => tr[p.Key] == p.Value && p.Value.Any(char.IsLetter)).Select(p => p.Key).ToList();
        // A few words are genuinely the same in both (none today); the list is measured, not assumed.
        Assert.Empty(same);
    }

    /// <summary>
    /// The history stores changed-field NAMES; the Details view shows each under its translated label. The names are read
    /// from the service code that writes them (the edit diff and the release / supersede entries), so a field added
    /// there without a label here would surface raw — in English, in all seven languages — and turns this red.
    /// </summary>
    [Fact]
    public void Every_history_field_name_the_service_writes_has_a_translated_label()
    {
        var src = Path.Combine(RepoRoot(), "services", "Diten.ManufacturingService", "src");
        var written = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .SelectMany(code => Regex.Matches(code, @"changed\.Add\(""([a-zA-Z]+)""\)").Select(m => m.Groups[1].Value)
                .Concat(Regex.Matches(code, @"BomHistoryOperation\.\w+,[^\[]*\[([^\]]*)\]")
                    .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"""([a-zA-Z]+)""").Select(f => f.Groups[1].Value))))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Superset(new HashSet<string> { "description", "components", "routing", "status", "effectiveFrom", "effectiveTo", "changeControlRef" }, written);

        var details = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "Manufacturing", "Boms", "Details.cshtml"));
        var labelled = Regex.Matches(details, @"""([a-zA-Z]+)""\s*=>\s*(?:Shared)?Localizer\[").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.Empty(written.Except(labelled));
    }

    private static Dictionary<string, string> Values(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "Manufacturing", "Boms", $"BomsIndex.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
