using System.Xml.Linq;
using Diten.Web.Views.CRM.Claims;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-CL-FE-1 — the claims list is a TENANT page: all SEVEN languages (en, tr, fr, es, zh, ar, ru) carry the same keys, every
/// key the list bridge ships exists in each of them with a non-empty value, and none of the WP-CL-FE-1 keys echoes its
/// own key name (the localizer's silent fallback for a missing translation).
/// </summary>
public sealed class ClaimsIndexL10nTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void All_seven_languages_carry_the_same_keys()
    {
        var reference = Keys("en");
        foreach (var language in Languages)
        {
            var keys = Keys(language);
            Assert.True(reference.SetEquals(keys),
                $"{language}: missing [{string.Join(", ", reference.Except(keys))}] extra [{string.Join(", ", keys.Except(reference))}]");
        }
    }

    [Fact]
    public void Every_bridge_key_has_a_value_in_every_language()
    {
        foreach (var language in Languages)
        {
            var values = Values(language);
            foreach (var key in ClaimsIndexL10nKeys.Bridge)
            {
                Assert.True(values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"{language}: '{key}' is missing or empty");
            }
        }
    }

    [Fact]
    public void No_new_key_echoes_its_name()
    {
        foreach (var language in Languages)
        {
            var values = Values(language);
            foreach (var key in ClaimsIndexL10nKeys.NewKeys)
            {
                Assert.NotEqual(key, values[key]);
            }
        }
    }

    [Fact]
    public void State_labels_cover_every_value_code_the_list_renders()
    {
        string[] codes = ["approved", "in-review", "draft", "review-required", "expiring", "closed", "not-opened",
            "not-applicable", "inactive", "archived"];
        Assert.All(codes, code => Assert.Contains("State_" + code, ClaimsIndexL10nKeys.NewKeys));
    }

    private static HashSet<string> Keys(string language) => Values(language).Keys.ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> Values(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "Claims",
            $"ClaimsIndex.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

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

        throw new DirectoryNotFoundException("Repository root (frontend/Diten.Web/Resources) not found.");
    }
}
