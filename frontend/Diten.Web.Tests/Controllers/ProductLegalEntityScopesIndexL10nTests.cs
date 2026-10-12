using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// BL-598 FIX2 item 5 — the product scope screen's sentences in all seven languages (the GlobalProductsIndexL10nTests
/// pattern): every language has exactly the English keys, none empty; every refusal the controller maps to a sentence
/// (<c>RefusalKeys</c>, e.g. <c>ErrorStoreNotReady</c>) and every key the page emits to the browser (_IndexL10n.cshtml)
/// is among them.
/// </summary>
public sealed class ProductLegalEntityScopesIndexL10nTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void Every_language_has_exactly_the_english_keys_and_none_is_empty()
    {
        var english = Sentences("en").Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            var sentences = Sentences(language);
            Assert.True(english.SetEquals(sentences.Keys),
                $"ProductLegalEntityScopesIndex.{language}.resx differs from English: missing [{string.Join(", ", english.Except(sentences.Keys))}], extra [{string.Join(", ", sentences.Keys.Except(english))}]");
            Assert.All(sentences, sentence => Assert.False(string.IsNullOrWhiteSpace(sentence.Value),
                $"ProductLegalEntityScopesIndex.{language}.resx: {sentence.Key} is empty"));
        }
    }

    [Fact]
    public void Every_mapped_refusal_has_a_sentence_in_all_seven_languages()
    {
        Assert.Contains("ErrorStoreNotReady", ProductLegalEntityScopesController.RefusalKeys.Values);
        foreach (var language in Languages)
        foreach (var key in ProductLegalEntityScopesController.RefusalKeys.Values.Distinct())
            Assert.True(Sentences(language).ContainsKey(key), $"ProductLegalEntityScopesIndex.{language}.resx misses {key}");
    }

    [Fact]
    public void Every_key_the_page_emits_has_a_sentence()
    {
        var view = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "MasterDataManagement",
            "ProductLegalEntityScopes", "_IndexL10n.cshtml"));
        // The page's OWN localizer only (SharedLocalizer keys live in SharedResource).
        var emitted = Regex.Matches(view, "(?<![A-Za-z])Localizer\\[\"([A-Za-z0-9]+)\"\\]").Select(match => match.Groups[1].Value)
            .Distinct().ToArray();
        Assert.NotEmpty(emitted);
        foreach (var language in Languages)
        foreach (var key in emitted)
            Assert.True(Sentences(language).ContainsKey(key), $"ProductLegalEntityScopesIndex.{language}.resx misses {key}");
    }

    private static Dictionary<string, string> Sentences(string language) =>
        XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterDataManagement",
                "ProductLegalEntityScopes", $"ProductLegalEntityScopesIndex.{language}.resx"))
            .Root!.Elements("data")
            .ToDictionary(x => (string)x.Attribute("name")!, x => (string?)x.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
