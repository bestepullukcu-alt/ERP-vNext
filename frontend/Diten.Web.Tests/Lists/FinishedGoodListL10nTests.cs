using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-MDM-FG-ACCEPT-01 item 6 / m9 — the Finished Good register's words, measured on the production files:
/// the seven FinishedGoodsIndex resx files carry the same keys with no empty value; every key the register's views and
/// the proxy controller ask the FG localizer for exists (a missing key renders as its own name); every sentence key
/// the browser's refusal map (index.js refusalKeys) names reaches the page's L10n payload.
/// </summary>
public sealed class FinishedGoodListL10nTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // The FG localizer only: `Localizer[...]` / `_localizer[...]`, never SharedLocalizer / _sharedLocalizer.
    private static readonly Regex FgLocalizerKey = new(@"(?<![A-Za-z_])_?[Ll]ocalizer\[""([A-Za-z0-9]+)""\]",
        RegexOptions.CultureInvariant);

    private static readonly Regex RefusalEntry = new(@"^\s*([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+):\s*'([A-Za-z0-9]+)',?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    [Fact]
    public void All_seven_languages_carry_the_same_keys_and_no_empty_value()
    {
        var english = ResxKeys("en");
        Assert.True(english.Count >= 60, $"Only {english.Count} keys in FinishedGoodsIndex.en.resx.");
        foreach (var language in Languages)
        {
            var file = ResxKeys(language);
            var missing = english.Keys.Except(file.Keys).ToArray();
            var extra = file.Keys.Except(english.Keys).ToArray();
            Assert.True(missing.Length == 0 && extra.Length == 0,
                $"FinishedGoodsIndex.{language}.resx — missing: {string.Join(", ", missing)}; extra: {string.Join(", ", extra)}");
            Assert.All(file, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"{language}:{entry.Key} is empty"));
        }
    }

    [Theory]
    [InlineData("CurrentPageOnly", "Current page only")]
    [InlineData("ErrorInvalidFormAttempt", "This form has expired or belongs to another session. Reload the page and try again.")]
    [InlineData("ErrorTimeout", "The Finished Good service did not answer in time. Try again — the same request will not create a second draft.")]
    [InlineData("LifecycleCancelled", "Cancelled")]
    [InlineData("CancellationPendingAudit", "Cancellation awaiting audit")]
    public void The_item_6_and_m9_keys_say_what_was_agreed_in_english(string key, string expected)
    {
        Assert.Equal(expected, ResxKeys("en")[key]);
    }

    [Fact]
    public void Non_english_values_are_translated_not_copied()
    {
        var english = ResxKeys("en");
        var agreed = new[] { "ErrorInvalidFormAttempt", "ErrorTimeout", "LifecycleCancelled", "CancellationPendingAudit", "CurrentPageOnly" }
            .Concat(english.Keys.Where(key => key.StartsWith("Refusal", StringComparison.Ordinal)))
            .ToArray();
        foreach (var language in Languages.Where(language => language != "en"))
        {
            var file = ResxKeys(language);
            var copied = agreed.Where(key => file[key] == english[key]).ToArray();
            Assert.True(copied.Length == 0, $"{language}: English copied for {string.Join(", ", copied)}");
        }
    }

    [Fact]
    public void Every_key_the_views_and_the_proxy_ask_for_exists()
    {
        var english = ResxKeys("en");
        var sources = Directory.EnumerateFiles(Path.Combine(WebRoot(), "Views", "MasterDataManagement", "FinishedGoods"), "*.cshtml")
            .Append(Path.Combine(WebRoot(), "Controllers", "FinishedGoodsController.cs"))
            .ToArray();
        Assert.True(sources.Length >= 7, $"Only {sources.Length} FG sources found.");
        var asked = sources
            .SelectMany(path => FgLocalizerKey.Matches(File.ReadAllText(path)).Select(match => (path, key: match.Groups[1].Value)))
            .ToArray();
        Assert.Contains(asked, item => item.key == "ErrorInvalidFormAttempt");
        var missing = asked.Where(item => !english.ContainsKey(item.key))
            .Select(item => $"{Path.GetFileName(item.path)}:{item.key}")
            .Distinct()
            .ToArray();
        Assert.True(missing.Length == 0, "Keys asked for but not in FinishedGoodsIndex.en.resx: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_refusal_sentence_key_reaches_the_page_payload()
    {
        var script = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "assets", "js", "MasterDataManagement", "FinishedGoods", "index.js"));
        var start = script.IndexOf("const refusalKeys = Object.freeze({", StringComparison.Ordinal);
        Assert.True(start >= 0, "index.js no longer declares refusalKeys.");
        var end = script.IndexOf("});", start, StringComparison.Ordinal);
        var entries = RefusalEntry.Matches(script[start..end]).Select(match => (code: match.Groups[1].Value, key: match.Groups[2].Value)).ToArray();
        Assert.True(entries.Length >= 40, $"Only {entries.Length} refusal codes mapped.");
        Assert.Equal(entries.Length, entries.Select(entry => entry.code).Distinct(StringComparer.Ordinal).Count());

        var payload = File.ReadAllText(Path.Combine(WebRoot(), "Views", "MasterDataManagement", "FinishedGoods", "_IndexL10n.cshtml"));
        var english = ResxKeys("en");
        foreach (var key in entries.Select(entry => entry.key).Distinct(StringComparer.Ordinal))
        {
            Assert.True(payload.Contains($"{key} = Localizer[\"{key}\"].Value", StringComparison.Ordinal),
                $"_IndexL10n.cshtml does not carry {key} from the FG localizer.");
            Assert.True(english.ContainsKey(key), $"{key} is not in FinishedGoodsIndex.en.resx.");
        }
    }

    private static Dictionary<string, string> ResxKeys(string language) =>
        XDocument.Load(Path.Combine(WebRoot(), "Resources", "Views", "MasterDataManagement", "FinishedGoods",
                $"FinishedGoodsIndex.{language}.resx"))
            .Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? "",
                StringComparer.Ordinal);

    private static string WebRoot() => Path.Combine(RepoRoot(), "frontend", "Diten.Web");

    private static string RepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
    }
}
