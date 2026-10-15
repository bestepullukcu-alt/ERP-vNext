using System.Reflection;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// BL-552 flow 3 FIX2 K5 — the Global Product screen's sentences in all seven languages (the GSKU refusal-bridge
/// pattern): every language has exactly the keys English has, and every refusal the controller maps to a sentence
/// (`InFlightRefusalKeys`, e.g. `ErrorSubmitStuck`) is among them.
/// </summary>
public sealed class GlobalProductsIndexL10nTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void Every_language_has_exactly_the_english_keys()
    {
        var english = Keys("en");
        foreach (var language in Languages)
        {
            var keys = Keys(language);
            Assert.True(english.SetEquals(keys),
                $"GlobalProductsIndex.{language}.resx differs from English: missing [{string.Join(", ", english.Except(keys))}], extra [{string.Join(", ", keys.Except(english))}]");
        }
    }

    [Fact]
    public void Every_mapped_refusal_has_a_sentence_in_all_seven_languages()
    {
        var mapped = (IReadOnlyDictionary<string, string>)typeof(GlobalProductsController)
            .GetField("InFlightRefusalKeys", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.Contains("ErrorSubmitStuck", mapped.Values);
        foreach (var language in Languages)
        foreach (var key in mapped.Values.Distinct())
            Assert.True(Keys(language).Contains(key), $"GlobalProductsIndex.{language}.resx misses {key}");
    }

    private static HashSet<string> Keys(string language) =>
        XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterDataManagement",
                "GlobalProducts", $"GlobalProductsIndex.{language}.resx"))
            .Root!.Elements("data").Select(x => (string)x.Attribute("name")!).ToHashSet(StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
