using System.Xml.Linq;
using Xunit;

namespace Diten.Platform.Application.Tests.Organization;

// MOD-0288 Phase 4 — the Position Assignments screen (list + full-page create/edit/details) is a TENANT module and
// must be complete in all 7 tenant languages. Reads the REAL SharedResource marker resx
// (PositionAssignmentsIndex.{lang}.resx), enforces full key parity with en, and pins the new field/enum/derived-
// status/one-primary-message keys. Mirrors the Position / Org Unit / LE / Nav guards.
public sealed class PositionAssignmentL10nContractTests
{
    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] RequiredKeys =
    [
        "AssignmentType", "AssignmentTypePrimary", "AssignmentTypeSecondary", "AssignmentTypeActing", "AssignmentTypeDelegated",
        "Reason", "ReasonHire", "ReasonTransfer", "ReasonPromotion", "ReasonBackfill",
        "AllocationPercent", "Notes", "IsCancelled",
        "StatusLabel", "StatusPlanned", "StatusActive", "StatusEnded",
        "SectionBasic", "SectionAdvanced", "OnePrimaryError", "DetailsTitle"
    ];

    private static string ResxPath(string language) =>
        Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "Organization", "PositionAssignments",
            $"PositionAssignmentsIndex.{language}.resx");

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
            Assert.True(missing.Count == 0, $"PositionAssignmentsIndex.{language}.resx is missing {missing.Count} key(s): {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void Required_field_enum_and_message_keys_are_non_empty_in_all_seven_languages()
    {
        foreach (var language in SupportedLanguages)
        {
            var values = ResxValues(ResxPath(language));
            foreach (var key in RequiredKeys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"PositionAssignmentsIndex.{language}.resx is missing/empty for key: {key}");
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
            Assert.NotEqual(english["AssignmentType"], values["AssignmentType"]);
            Assert.NotEqual(english["OnePrimaryError"], values["OnePrimaryError"]);
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
