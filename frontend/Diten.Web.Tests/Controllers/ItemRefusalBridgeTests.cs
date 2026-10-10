using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Views.MasterDataManagement.Items;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0290-FU04 S5 (§18) — the item code ⇔ sentence bridge, held in lock-step the way GskuRefusalBridgeTests holds the
/// GSKU one: every code MDM's item code can answer with has a sentence key (or is a known detail of a code that carries
/// one), no mapped code is an orphan, and every key exists — and only those keys — in all seven languages. The codes are
/// read from MDM's own source (the production code, not a copy of it).
/// </summary>
public sealed class ItemRefusalBridgeTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly Regex CodeLiteral = new("\"([A-Z][A-Z0-9]+(?:_[A-Z0-9]+)+)\"", RegexOptions.CultureInvariant);

    // Upper-case literals in that code that are NOT answers: the six material type values (ItemRules, the reference set's
    // own values) — they are a label key's suffix (MaterialType_…), never a refusal.
    private static readonly HashSet<string> NotRefusals = new(ItemRefusalSentences.MaterialTypes, StringComparer.Ordinal);

    [Fact]
    public void Every_code_of_mdms_item_code_has_a_sentence()
    {
        var missing = MdmItemCodes()
            .Where(code => !NotRefusals.Contains(code)
                           && !ItemRefusalSentences.KnownDetails.Contains(code)
                           && !ItemRefusalSentences.Keys.ContainsKey(code))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0, "Item codes without a sentence: " + string.Join(", ", missing));
    }

    [Fact]
    public void No_mapped_code_or_known_detail_is_an_orphan()
    {
        var known = MdmItemCodes();
        var ledger = CodeReservationLedgerCodes();
        Assert.All(ItemRefusalSentences.CodeReservationCodes, code => Assert.Contains(code, ledger));

        var orphans = ItemRefusalSentences.Keys.Keys
            .Where(code => !known.Contains(code) && !ItemRefusalSentences.CodeReservationCodes.Contains(code))
            .ToArray();
        Assert.True(orphans.Length == 0, "Mapped codes MDM never answers with: " + string.Join(", ", orphans));
        Assert.Empty(ItemRefusalSentences.KnownDetails.Where(detail => !known.Contains(detail)));
        Assert.Empty(ItemRefusalSentences.KnownDetails.Where(ItemRefusalSentences.Keys.ContainsKey));
        Assert.All(ItemRefusalSentences.DetailBearing.Keys, code => Assert.True(ItemRefusalSentences.Keys.ContainsKey(code), code));
    }

    // The task's list (S5 prompt) and §18's new codes: each one, by name, is said.
    [Theory]
    [InlineData("ITEM_NOT_FOUND")]
    [InlineData("ITEM_FIELD_FORBIDDEN")]
    [InlineData("ITEM_NAME_CONFLICT")]
    [InlineData("ITEM_REFERENCE_VALUE_INVALID")]
    [InlineData("ITEM_REFERENCE_LIST_UNAVAILABLE")]
    [InlineData("ITEM_STORAGE_CONDITION_CONFLICT")]
    [InlineData("UOM_CONVERSION_INVALID")]
    [InlineData("STOCK_BEHAVIOUR_INCOMPLETE")]
    [InlineData("ITEM_CONCURRENCY_CONFLICT")]
    [InlineData("ITEM_CRITICAL_FIELD_LOCKED")]
    [InlineData("ITEM_LIFECYCLE_TRANSITION_INVALID")]
    [InlineData("ITEM_DEACTIVATED_NOT_EDITABLE")]
    [InlineData("CREATION_COMMAND_TOMBSTONED")]
    [InlineData("ITEM_RETIRE_REQUIRES_DEACTIVATION")]
    [InlineData("ITEM_RETIRE_STOCK_NOT_ZERO")]
    [InlineData("ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE")]
    [InlineData("ITEM_ACTIVATION_APPROVAL_UNAVAILABLE")]
    [InlineData("ITEM_BINDING_RECONCILIATION_REQUIRED")]
    [InlineData("ITEM_CODE_BURN_REFUSED")]
    [InlineData("ITEM_STORE_UNAVAILABLE")]
    [InlineData("IDEMPOTENCY_KEY_INVALID")]
    [InlineData("IDEMPOTENCY_KEY_CONFLICT")]
    [InlineData("INVALID_PARAMETER")]
    [InlineData("ITEM_CONTRACT_INVALID")]
    public void The_named_item_codes_are_scanned_and_mapped(string code)
    {
        Assert.Contains(code, MdmItemCodes());
        Assert.True(ItemRefusalSentences.Keys.ContainsKey(code), code);
    }

    [Fact]
    public void Every_key_is_in_all_seven_languages_not_empty_and_no_language_has_more()
    {
        var english = ResxKeys("en");
        var used = ItemRefusalSentences.Keys.Values
            .Concat(ItemRefusalSentences.DetailBearing.Values.SelectMany(details => details.Values))
            .Concat(ItemRefusalSentences.MaterialTypes.Select(ItemRefusalSentences.MaterialTypeKey))
            .Concat(ItemRefusalSentences.LifecycleStatuses.Values.Select(ItemRefusalSentences.LifecycleKey))
            .Concat(ItemRefusalSentences.LockedFieldLabels.Values)
            .Concat(ItemRefusalSentences.ReferenceSetLabels.Values)
            .Concat(["RefusalCriticalFieldLockedNamed", "RefusalReferenceValueInvalidNamed", "ListSeparator"]);
        Assert.Empty(used.Where(key => !english.ContainsKey(key)).Distinct());

        foreach (var language in Languages)
        {
            var file = ResxKeys(language);
            Assert.True(english.Keys.ToHashSet().SetEquals(file.Keys), $"ItemsIndex.{language}.resx differs from ItemsIndex.en.resx.");
            Assert.All(file, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value) && entry.Key != "ListSeparator",
                $"{language}:{entry.Key} is empty"));
            // A sentence with a placeholder keeps it in every language.
            foreach (var (key, value) in english.Where(entry => entry.Value.Contains("{0}")))
                Assert.True(file[key].Contains("{0}"), $"{language}:{key} lost its {{0}}");
        }
    }

    [Fact]
    public void No_sentence_shows_a_raw_code_or_a_status_word()
    {
        foreach (var language in Languages)
        {
            foreach (var (key, value) in ResxKeys(language).Where(entry => entry.Key.StartsWith("Refusal", StringComparison.Ordinal)
                         || entry.Key.StartsWith("Error", StringComparison.Ordinal)))
            {
                Assert.DoesNotMatch(@"\b(403|Forbidden|ITEM_[A-Z_]+|UOM_CONVERSION_INVALID)\b", value);
                _ = key;
            }
        }
    }

    // The item code of MDM: the item master feature, the screen API, the item store and the stock-availability client.
    private static HashSet<string> MdmItemCodes()
    {
        var src = Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src");
        var files = Directory.EnumerateFiles(Path.Combine(src, "Diten.MdmService.Application", "Features", "ItemMaster"), "*.cs",
                SearchOption.AllDirectories)
            .Concat([
                Path.Combine(src, "Diten.MdmService.Api", "Controllers", "ItemsController.cs"),
                Path.Combine(src, "Diten.MdmService.Persistence", "Repositories", "ItemRepository.cs")
            ])
            .Concat(Directory.EnumerateFiles(Path.Combine(src, "Diten.MdmService.Infrastructure", "Inventory"), "*.cs"))
            .Concat(Directory.EnumerateFiles(Path.Combine(src, "Diten.MdmService.Application", "Contracts", "Inventory"), "*.cs"))
            .ToArray();
        Assert.True(files.Length > 15, $"Only {files.Length} item files found — the scan no longer sees MDM's item code.");
        var codes = files.SelectMany(path => CodeLiteral.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(codes.Count > 40, $"Only {codes.Count} codes found — the scan no longer reads MDM's item refusals.");
        return codes;
    }

    // The shared code-reservation ledger an item creation passes answers on from.
    private static HashSet<string> CodeReservationLedgerCodes()
    {
        var src = Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src");
        return new[]
            {
                Path.Combine(src, "Diten.MdmService.Persistence", "Repositories", "CodeReservationRepository.cs"),
                Path.Combine(src, "Diten.MdmService.Domain", "Repositories", "ICodeReservationRepository.cs")
            }
            .SelectMany(path => CodeLiteral.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ResxKeys(string language) =>
        XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterDataManagement",
                "Items", $"ItemsIndex.{language}.resx"))
            .Root!.Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? "",
                StringComparer.Ordinal);

    private static string RepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
        }
        throw new DirectoryNotFoundException("Repository root (AGENTS.md) not found.");
    }
}
