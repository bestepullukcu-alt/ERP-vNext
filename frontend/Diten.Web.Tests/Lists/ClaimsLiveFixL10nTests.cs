using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-CL-FIX-1 — live E2E findings on the claim screens' texts: a closed country cell reads "Closed" (never the same
/// word as a not-opened cell) in all seven languages, the document lifecycle states have labels (ClaimsForm, used by
/// the evidence picker and cards), and the country version page title is "open country version · {country}" style.
/// </summary>
public sealed class ClaimsLiveFixL10nTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void Closed_is_not_the_same_word_as_not_opened_in_any_language()
    {
        foreach (var language in Languages)
        {
            var values = Values("ClaimsIndex", language);
            Assert.NotEqual(values["State_not-opened"], values["State_closed"]);
            Assert.StartsWith(values["State_closed"], values["ClosedTooltip"]);
            Assert.Contains("{0}", values["ClosedTooltip"]);
        }

        Assert.Equal("Kapatıldı", Values("ClaimsIndex", "tr")["State_closed"]);
        Assert.Equal("Açılmadı", Values("ClaimsIndex", "tr")["State_not-opened"]);
    }

    [Fact]
    public void Every_document_state_has_a_label_in_all_seven_languages()
    {
        string[] states = ["effective", "suspended", "retired", "withdrawn", "unknown"];
        foreach (var language in Languages)
        {
            var values = Values("ClaimsForm", language);
            Assert.All(states, s =>
            {
                Assert.True(values.TryGetValue("DocStateLabel_" + s, out var label) && !string.IsNullOrWhiteSpace(label),
                    $"{language}: DocStateLabel_{s} missing");
                Assert.NotEqual(s, label);
            });
        }

        Assert.Equal("Yürürlükte", Values("ClaimsForm", "tr")["DocStateLabel_effective"]);
    }

    [Fact]
    public void Country_version_titles_take_the_country_name()
    {
        foreach (var language in Languages)
        {
            var values = Values("ClaimsCountryVersion", language);
            Assert.Contains("{0}", values["PageTitleCreate"]);
            Assert.Contains("{0}", values["PageTitleEdit"]);
        }

        Assert.Equal("Ülke sürümü aç · {0}", Values("ClaimsCountryVersion", "tr")["PageTitleCreate"]);
        Assert.Equal("Ülke sürümü · {0}", Values("ClaimsCountryVersion", "tr")["PageTitleEdit"]);
    }

    private static Dictionary<string, string> Values(string family, string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "Claims", $"{family}.{language}.resx");
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

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
