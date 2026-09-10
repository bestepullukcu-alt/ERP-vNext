using System.Xml.Linq;
using Xunit;

namespace Diten.Platform.Application.Tests.Organization;

/// <summary>
/// MOD-0288-FU04 §8 — the field-definition authoring screens are a TENANT surface and must be complete in all
/// seven tenant languages.
///
/// <para>Sibling of <see cref="OrganizationUnitL10nContractTests"/> and shaped the same way: parity asserted in
/// BOTH directions, no empty values, and the strings §5.1 fixed pinned by name. ⚠ A one-way check would prove
/// only that no English key is missing elsewhere — it says nothing about a key that exists in Turkish alone,
/// and nothing at all about a key present with an empty value, which passes every key-set comparison there is
/// while rendering blank on screen.</para>
/// </summary>
public sealed class OrganizationFieldDefinitionL10nContractTests
{
    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    /// <summary>
    /// The eight data-type labels (§5.1) plus the three rule strings the screens are not allowed to shorten.
    /// Pinned by name so a language that quietly loses one fails the build rather than the screen.
    /// </summary>
    private static readonly string[] RequiredKeys =
    [
        "FieldTypeText", "FieldTypeMultilineText", "FieldTypeInteger", "FieldTypeDecimal",
        "FieldTypeBoolean", "FieldTypeDate", "FieldTypeSingleSelect", "FieldTypeReference",
        "ClassificationNormal", "ClassificationInternal", "ClassificationConfidential", "ClassificationRestricted",
        "CodeImmutableHelp", "InactiveDefinitionHelp", "DefinitionLimitReached", "QueryableHelp",
        "FieldDefinitionsTitle", "PageDescription", "FormTitleCreate", "FormTitleEdit", "DetailsTitle",
        "IdentitySection", "TypeSection", "ConstraintsSection", "PresentationSection", "GovernanceSection",
        "ReadOnlyNotice", "Deactivate", "DeactivateConfirm", "NoReactivateHelp"
    ];

    private static string ResxPath(string language) =>
        Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "Organization", "FieldDefinitions",
            $"OrganizationFieldDefinitionsIndex.{language}.resx");

    [Fact]
    public void Seven_files_carry_identical_key_sets_with_no_empty_value()
    {
        var englishKeys = ResxKeys(ResxPath("en"));
        Assert.NotEmpty(englishKeys);

        foreach (var language in SupportedLanguages)
        {
            var path = ResxPath(language);
            Assert.True(File.Exists(path), $"Missing resx file for '{language}': {path}");

            var values = ResxValues(path);
            var keys = values.Keys.ToHashSet(StringComparer.Ordinal);

            var missing = englishKeys.Except(keys).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0,
                $"OrganizationFieldDefinitionsIndex.{language}.resx is missing {missing.Count} key(s): {string.Join(", ", missing)}");

            var extra = keys.Except(englishKeys).OrderBy(k => k).ToList();
            Assert.True(extra.Count == 0,
                $"OrganizationFieldDefinitionsIndex.{language}.resx carries {extra.Count} key(s) English does not: {string.Join(", ", extra)}");

            var empty = values.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).OrderBy(k => k).ToList();
            Assert.True(empty.Count == 0,
                $"OrganizationFieldDefinitionsIndex.{language}.resx has {empty.Count} empty value(s): {string.Join(", ", empty)}");
        }
    }

    [Fact]
    public void Owner_approved_keys_are_present_and_non_empty_in_all_seven_languages()
    {
        foreach (var language in SupportedLanguages)
        {
            var values = ResxValues(ResxPath(language));
            foreach (var key in RequiredKeys)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"OrganizationFieldDefinitionsIndex.{language}.resx is missing/empty for key: {key}");
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
            // A copied English string is a missing translation that passes every key check.
            Assert.NotEqual(english["FieldTypeMultilineText"], values["FieldTypeMultilineText"]);
            Assert.NotEqual(english["FieldTypeSingleSelect"], values["FieldTypeSingleSelect"]);
            Assert.NotEqual(english["QueryableHelp"], values["QueryableHelp"]);
        }
    }

    /// <summary>
    /// ⚠ TWO NAMING DECISIONS, PINNED BECAUSE BOTH WERE TAKEN AGAINST THE MORE LITERAL ALTERNATIVE (§5.1).
    /// `Boolean` is shown as what the user will see, not as the type's name in the programmer's vocabulary;
    /// `Integer` keeps the word that distinguishes it from `Decimal`, which is the entire reason both exist.
    /// A later "tidy-up" to "Boolean" or "Number" is the regression this test exists to catch.
    /// </summary>
    [Fact]
    public void Boolean_and_integer_keep_the_labels_the_owner_chose()
    {
        var english = ResxValues(ResxPath("en"));
        Assert.Equal("Yes / No", english["FieldTypeBoolean"]);
        Assert.Equal("Whole number", english["FieldTypeInteger"]);

        var turkish = ResxValues(ResxPath("tr"));
        Assert.Equal("Evet / Hayır", turkish["FieldTypeBoolean"]);
        Assert.Equal("Tam sayı", turkish["FieldTypeInteger"]);
    }

    /// <summary>
    /// ⚠ `IsQueryable` IS SERVER-ENFORCED AND THE HELP TEXT MUST SAY SO (pack §3). FU02 wrote the flag two
    /// ways before settling it; a help text calling it a display preference would put the contradiction back
    /// exactly where the user reads it.
    /// </summary>
    [Fact]
    public void Queryable_help_describes_a_server_rule_not_a_display_preference()
    {
        var english = ResxValues(ResxPath("en"));
        Assert.Contains("server", english["QueryableHelp"], StringComparison.OrdinalIgnoreCase);

        var turkish = ResxValues(ResxPath("tr"));
        Assert.Contains("sunucu", turkish["QueryableHelp"], StringComparison.OrdinalIgnoreCase);
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
