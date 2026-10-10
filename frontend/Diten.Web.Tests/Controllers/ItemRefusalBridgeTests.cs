using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Views.MasterDataManagement.Items;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0290-FU04 S2 — the item activation gate's refusal and its field names, read from MDM's own gate (the production
/// code, not a copy): each has a sentence key, and every key is in all seven languages, none empty, no language with more.
/// </summary>
public sealed class ItemRefusalBridgeTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void The_gate_code_and_every_field_it_names_have_a_sentence()
    {
        var gate = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src",
            "Diten.MdmService.Application", "Features", "ItemMaster", "Activation", "ItemActivationGate.cs"));
        var code = Regex.Match(gate, "StockFieldsRequired = \"([A-Z_]+)\"").Groups[1].Value;
        var fields = Regex.Matches(gate, "Field = \"([a-zA-Z]+)\"").Select(match => match.Groups[1].Value).ToArray();

        Assert.Equal("ITEM_STOCK_FIELDS_REQUIRED_FOR_ACTIVATION", code);
        Assert.True(ItemRefusalSentences.Keys.ContainsKey(code), "the gate's code has no sentence");
        Assert.True(fields.Length == 4, "the scan no longer sees the gate's field names");
        Assert.True(fields.ToHashSet(StringComparer.Ordinal).SetEquals(ItemRefusalSentences.FieldKeys.Keys),
            "gate fields and labels differ: " + string.Join(", ", fields));
    }

    [Fact]
    public void Every_item_refusal_key_is_in_all_seven_languages_and_no_language_has_more()
    {
        var keys = ItemRefusalSentences.Keys.Values.Concat(ItemRefusalSentences.FieldKeys.Values).ToHashSet(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            var file = XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views",
                    "MasterDataManagement", "Items", $"ItemRefusals.{language}.resx")).Root!.Elements("data")
                .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? "",
                    StringComparer.Ordinal);
            Assert.True(keys.SetEquals(file.Keys), $"ItemRefusals.{language}.resx differs from the map.");
            Assert.All(file, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"{language}:{entry.Key} is empty"));
        }
    }

    private static string RepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
    }
}
