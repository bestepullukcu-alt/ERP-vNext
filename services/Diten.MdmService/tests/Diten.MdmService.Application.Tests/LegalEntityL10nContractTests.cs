using System.Xml.Linq;
using Xunit;

namespace Diten.MdmService.Application.Tests;

// MOD-0220 finish — the Legal Entity screen (list + wizard + details) is a TENANT module and must be complete in
// all 7 tenant languages. This guard reads the REAL SharedResource marker resx (LegalEntitiesIndex.{lang}.resx)
// and enforces full key parity: every key in the en file must exist in all 7 files (a missing key renders as the
// raw key name and is a defect). Also pins the 4 new statutory keys. Mirrors NavL10nContractTests / the
// password-bridge contract guard.
public sealed class LegalEntityL10nContractTests
{
    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] NewStatutoryKeys =
    [
        "VatNumber", "PlaceOfIncorporation", "IncorporationDate", "DissolutionDate", "SectionStatutoryAdvanced"
    ];

    private static string ResxPath(string language) =>
        Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterData", "LegalEntities",
            $"LegalEntitiesIndex.{language}.resx");

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
            Assert.True(missing.Count == 0, $"LegalEntitiesIndex.{language}.resx is missing {missing.Count} key(s): {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void New_statutory_keys_exist_and_are_non_empty_in_all_seven_languages()
    {
        foreach (var language in SupportedLanguages)
        {
            var values = ResxValues(ResxPath(language));
            foreach (var key in NewStatutoryKeys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"LegalEntitiesIndex.{language}.resx is missing/empty for new key: {key}");
            }
        }
    }

    [Fact]
    public void Non_english_files_are_actually_translated_not_english_copies()
    {
        // Cheap smoke test that a required field label differs from English in a couple of representative
        // non-Latin languages (catches an accidental english-copy that would still pass the parity check).
        var english = ResxValues(ResxPath("en"));
        foreach (var language in new[] { "tr", "ru", "zh", "ar" })
        {
            var values = ResxValues(ResxPath(language));
            Assert.NotEqual(english["LegalName"], values["LegalName"]);
            Assert.NotEqual(english["VatNumber"], values["VatNumber"]);
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
