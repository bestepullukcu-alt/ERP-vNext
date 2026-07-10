using System.Xml.Linq;
using Xunit;

namespace Diten.Platform.Application.Tests.Organization;

// MOD-0288 Phase 3 — the Positions screen (list + full-page create/edit/details) is a TENANT module and must be
// complete in all 7 tenant languages. Reads the REAL SharedResource marker resx (PositionsIndex.{lang}.resx),
// enforces full key parity with en, and pins the new field/enum keys. Mirrors the Org Unit / LE / Nav guards.
public sealed class PositionL10nContractTests
{
    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] RequiredKeys =
    [
        "JobTitle", "PositionType", "PositionTypePermanent", "PositionTypeTemporary", "PositionTypeContractor", "PositionTypeIntern",
        "Fte", "StatusLabel", "StatusDraft", "StatusFrozen", "StatusClosed",
        "EffectiveFrom", "EffectiveTo", "SectionBasic", "SectionAdvanced",
        "Occupancy", "Vacant", "Occupied", "DetailsTitle"
    ];

    private static string ResxPath(string language) =>
        Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "Organization", "Positions",
            $"PositionsIndex.{language}.resx");

    [Fact]
    public void Every_language_has_full_key_parity_with_english()
    {
        var englishKeys = ResxKeys(ResxPath("en"));
        Assert.NotEmpty(englishKeys);

        foreach (var language in SupportedLanguages)
        {
            var path = ResxPath(language);
            Assert.True(File.Exists(path), $"Missing resx file for '{language}': {path}");
            var keys = ResxKeys(path);
            var missing = englishKeys.Where(k => !keys.Contains(k)).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, $"PositionsIndex.{language}.resx is missing {missing.Count} key(s): {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void Required_field_and_enum_keys_are_non_empty_in_all_seven_languages()
    {
        foreach (var language in SupportedLanguages)
        {
            var values = ResxValues(ResxPath(language));
            foreach (var key in RequiredKeys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"PositionsIndex.{language}.resx is missing/empty for key: {key}");
            }
        }
    }

    [Fact]
    public void Non_english_files_are_actually_translated()
    {
        var english = ResxValues(ResxPath("en"));
        foreach (var language in new[] { "tr", "ru", "zh", "ar" })
        {
            var values = ResxValues(ResxPath(language));
            Assert.NotEqual(english["JobTitle"], values["JobTitle"]);
            Assert.NotEqual(english["PositionTypePermanent"], values["PositionTypePermanent"]);
        }
    }

    private static HashSet<string> ResxKeys(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Select(d => (string?)d.Attribute("name"))
            .Where(n => n is not null)
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> ResxValues(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Where(d => d.Attribute("name") is not null)
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repo root (frontend/Diten.Web/Resources) from the test output directory.");
    }
}
