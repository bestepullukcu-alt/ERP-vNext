using System.Xml.Linq;
using Xunit;

namespace Diten.Platform.Application.Tests.Organization;

// MOD-0288 Phase 2 — the Organization Units screen (list + full-page create/edit/details + tree) is a TENANT
// module and must be complete in all 7 tenant languages. This guard reads the REAL SharedResource marker resx
// (OrganizationUnitsIndex.{lang}.resx) and enforces full key parity with en (a missing key renders as the raw
// key name = defect), plus pins the new field/enum keys. Mirrors the Legal Entity / Nav l10n guards.
public sealed class OrganizationUnitL10nContractTests
{
    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // Enum values + new enterprise-field labels that MUST be localized (not reference-data).
    private static readonly string[] RequiredKeys =
    [
        "OrgUnitType", "OrgUnitTypeDepartment", "OrgUnitTypeDivision", "OrgUnitTypeBranch", "OrgUnitTypeTeam", "OrgUnitTypeHQ",
        "StatusLabel", "StatusInactive", "ManagerPosition", "Description", "EffectiveFrom", "EffectiveTo",
        "SectionBasic", "SectionAdvanced", "ViewTree", "ViewFlat", "TreeEmpty", "DetailsTitle",
        // MOD-0288-FU03 §7 — the second reporting line, the Group function type and the custom-value strings.
        // Pinned here so a language that quietly loses one of them fails the build rather than the screen.
        "FunctionalParentLabel", "AdministrativeParentLabel", "AdministrativeParentNone", "AdministrativeParentHelp",
        "OrgUnitTypeGroupFunction", "CustomFieldsSectionTitle", "CustomFieldRequiredError", "CustomFieldTypeError"
    ];

    private static string ResxPath(string language) =>
        Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "Organization", "OrganizationUnits",
            $"OrganizationUnitsIndex.{language}.resx");

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
            Assert.True(missing.Count == 0, $"OrganizationUnitsIndex.{language}.resx is missing {missing.Count} key(s): {string.Join(", ", missing)}");
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
                    $"OrganizationUnitsIndex.{language}.resx is missing/empty for key: {key}");
            }
        }
    }

    /// <summary>
    /// MOD-0288-FU03 §11 — parity is asserted in BOTH directions and against empties.
    ///
    /// <para>⚠ THE ONE-WAY CHECK ABOVE IS NOT ENOUGH. It proves no English key is missing elsewhere; it says
    /// nothing about a key that exists ONLY in, say, Turkish — a leftover that reads fine in the language it was
    /// added to and is invisible everywhere else. And a key present with an EMPTY value passes every key-set
    /// comparison there is while rendering as blank on screen, which is the failure this pack cares about.</para>
    /// </summary>
    [Fact]
    public void Seven_files_carry_identical_key_sets_with_no_empty_value()
    {
        var englishKeys = ResxKeys(ResxPath("en"));

        foreach (var language in SupportedLanguages)
        {
            var values = ResxValues(ResxPath(language));
            var keys = values.Keys.ToHashSet(StringComparer.Ordinal);

            var extra = keys.Except(englishKeys).OrderBy(k => k).ToList();
            Assert.True(extra.Count == 0,
                $"OrganizationUnitsIndex.{language}.resx carries {extra.Count} key(s) English does not: {string.Join(", ", extra)}");

            var missing = englishKeys.Except(keys).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0,
                $"OrganizationUnitsIndex.{language}.resx is missing {missing.Count} key(s): {string.Join(", ", missing)}");

            var empty = values.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).OrderBy(k => k).ToList();
            Assert.True(empty.Count == 0,
                $"OrganizationUnitsIndex.{language}.resx has {empty.Count} empty value(s): {string.Join(", ", empty)}");
        }
    }

    [Fact]
    public void Non_english_files_are_actually_translated()
    {
        var english = ResxValues(ResxPath("en"));
        foreach (var language in new[] { "tr", "ru", "zh", "ar" })
        {
            var values = ResxValues(ResxPath(language));
            Assert.NotEqual(english["ManagerPosition"], values["ManagerPosition"]);
            Assert.NotEqual(english["OrgUnitTypeDepartment"], values["OrgUnitTypeDepartment"]);
            // FU03 — the same proof for the strings this pack adds: a copied English sentence is a missing
            // translation that passes every key check.
            Assert.NotEqual(english["AdministrativeParentLabel"], values["AdministrativeParentLabel"]);
            Assert.NotEqual(english["AdministrativeParentHelp"], values["AdministrativeParentHelp"]);
            Assert.NotEqual(english["OrgUnitTypeGroupFunction"], values["OrgUnitTypeGroupFunction"]);
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
