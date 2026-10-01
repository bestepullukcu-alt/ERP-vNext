using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Models.Inventory;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// MOD-0173-FU01 is a TENANT module: seven languages, every key in every file, no empty value and no English copy in a
/// non-English file (localization-standard; pack §16 RESX parity). Also: every key the error map can return, and
/// every key the pages read, is in all fourteen screen files — a missing resx key prints its own NAME at runtime and
/// no compiler sees it.
/// </summary>
public sealed class InventoryResxParityTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string WebRoot = FindWebRoot();

    public static TheoryData<string, string> Families => new()
    {
        { "Resources/Views/Inventory/StockLedger", "StockLedgerIndex" },
        { "Resources/Views/Inventory/StockMovements", "StockMovementsIndex" }
    };

    [Theory]
    [MemberData(nameof(Families))]
    public void Seven_languages_carry_the_same_keys_with_real_text(string folder, string family)
    {
        var english = Read(folder, family, "en");
        Assert.NotEmpty(english);

        foreach (var lang in Languages)
        {
            var values = Read(folder, family, lang);
            Assert.Equal(english.Keys.OrderBy(k => k, StringComparer.Ordinal), values.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(values, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Value), $"{family}.{lang}: {pair.Key} is empty"));
            if (lang == "en") continue;
            // A missing key renders as its own name; in a translated file a value equal to the key or to English is
            // that defect wearing a disguise. (In English "Actions" may legitimately be the key's own word.)
            Assert.All(values, pair => Assert.NotEqual(pair.Key, pair.Value));
            Assert.All(values, pair => Assert.True(pair.Value != english[pair.Key], $"{family}.{lang}: {pair.Key} is an English copy"));
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void Every_error_sentence_the_proxy_can_choose_exists(string folder, string family)
    {
        var keys = Read(folder, family, "en").Keys.ToHashSet(StringComparer.Ordinal);
        Assert.All(InventoryErrorMessages.AllKeys, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void Every_contract_failure_code_maps_to_its_own_sentence_not_the_generic_one()
    {
        // parent pack §12 / FU01 pack §13 codes the UI can receive
        string[] codes =
        [
            "INSUFFICIENT_STOCK", "ALREADY_REVERSED", "PRODUCT_MASTER_UNAVAILABLE", "LOCATION_MASTER_UNAVAILABLE",
            "CROSS_LE_FORBIDDEN", "IDEMPOTENCY_KEY_REUSED", "INVALID_REQUEST", "INVALID_MOVEMENT_TYPE", "UNKNOWN_ITEM",
            "SKU_ITEM_MISMATCH", "UNKNOWN_LOCATION", "UOM_CONVERSION_MISSING", "LOT_REQUIRED", "SERIAL_COUNT_MISMATCH",
            "RESERVATION_CONFLICT"
        ];
        Assert.All(codes, code => Assert.NotEqual(InventoryErrorMessages.Generic, InventoryErrorMessages.KeyFor(code, 422)));
        Assert.Equal(InventoryErrorMessages.NoPermission, InventoryErrorMessages.KeyFor(null, 403));
        Assert.Equal(InventoryErrorMessages.SessionExpired, InventoryErrorMessages.KeyFor(null, 401));
        Assert.Equal(InventoryErrorMessages.ServiceUnavailable, InventoryErrorMessages.KeyFor(null, 0));
        Assert.Equal(InventoryErrorMessages.Conflict, InventoryErrorMessages.KeyFor(null, 409));
    }

    [Theory]
    [InlineData("Views/Inventory/StockLedger", "Resources/Views/Inventory/StockLedger", "StockLedgerIndex")]
    [InlineData("Views/Inventory/StockMovements", "Resources/Views/Inventory/StockMovements", "StockMovementsIndex")]
    public void Every_key_the_views_ask_for_is_in_the_resx(string viewFolder, string resxFolder, string family)
    {
        var keys = Read(resxFolder, family, "en").Keys.ToHashSet(StringComparer.Ordinal);
        var asked = Directory.EnumerateFiles(Path.Combine(WebRoot, viewFolder), "*.cshtml")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"(?<![A-Za-z])Localizer\[""(?<key>[^""]+)""\]").Select(m => m.Groups["key"].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(asked);
        Assert.All(asked, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void The_nav_keys_of_the_manifest_are_in_all_seven_shared_files()
    {
        foreach (var lang in Languages)
        {
            var shared = Read("Resources", "SharedResource", lang);
            foreach (var key in new[] { "Nav.Domain.SUPPLYCHAINEXECUTION", "Nav.Module.INVENTORYLEDGER", "Nav.Page.STOCKLEDGER" })
                Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault(key)), $"SharedResource.{lang}: {key}");
        }
    }

    private static Dictionary<string, string> Read(string folder, string family, string lang) =>
        XDocument.Load(Path.Combine(WebRoot, folder, $"{family}.{lang}.resx"))
            .Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string FindWebRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "frontend", "Diten.Web");
            if (Directory.Exists(Path.Combine(candidate, "Resources"))) return candidate;
            current = current.Parent;
        }

        throw new InvalidOperationException("frontend/Diten.Web not found above the test binary.");
    }
}
