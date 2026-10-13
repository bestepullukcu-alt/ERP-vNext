using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Models.Inventory;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// MOD-0173-FU02 is a TENANT module: seven languages, every key in every file, no empty value and no English copy in a
/// non-English file (localization-standard; FU02 pack AC-19). Also: every error sentence the location proxies can
/// choose, every key the location views ask for (their own localizer and the shared <c>LocationsIndex</c> one), every
/// key the location scripts read from window.L10n, and the one shared nav key — a missing resx key prints its own NAME
/// at runtime and no compiler sees it.
/// </summary>
public sealed class LocationResxParityTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string WebRoot = FindWebRoot();

    public static TheoryData<string, string> Families => new()
    {
        { "Resources/Views/Inventory/Locations", "LocationsIndex" },
        { "Resources/Views/Inventory/LocationTree", "LocationTreeIndex" }
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
            Assert.All(values, pair => Assert.NotEqual(pair.Key, pair.Value));
            Assert.All(values, pair => Assert.True(pair.Value != english[pair.Key], $"{family}.{lang}: {pair.Key} is an English copy"));
        }
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void Placeholders_survive_translation(string folder, string family)
    {
        var english = Read(folder, family, "en");
        foreach (var lang in Languages)
        {
            var values = Read(folder, family, lang);
            Assert.All(english.Where(e => e.Value.Contains("{0}", StringComparison.Ordinal)),
                e => Assert.True(values[e.Key].Contains("{0}", StringComparison.Ordinal), $"{family}.{lang}: {e.Key} lost its {{0}}"));
        }
    }

    [Fact]
    public void Every_error_sentence_the_location_proxies_can_choose_exists()
    {
        var keys = Read("Resources/Views/Inventory/Locations", "LocationsIndex", "en").Keys.ToHashSet(StringComparer.Ordinal);
        Assert.All(LocationErrorMessages.AllKeys, key => Assert.Contains(key, keys));
        // the proxies' own non-error sentences
        Assert.All(new[] { "LocationCreated", "LocationSaved", "StatusChanged", "BinsCreated", "OrgUnitsUnavailable" }, key => Assert.Contains(key, keys));
    }

    [Fact]
    public void Every_location_contract_code_maps_to_its_own_sentence_not_the_generic_one()
    {
        string[] codes =
        [
            "LOCATION_CODE_TAKEN", "LOCATION_LEVEL_INVALID", "LOCATION_PARENT_INACTIVE", "LOCATION_MOVE_INTO_SUBTREE",
            "LOCATION_HAS_STOCK", "LOCATION_HAS_ACTIVE_CHILDREN", "LOCATION_ARCHIVED", "LOCATION_CONFLICT",
            "LOCATION_BULK_LIMIT", "UNKNOWN_LOCATION", "ORG_UNIT_INVALID", "ORG_UNIT_DIRECTORY_UNAVAILABLE",
            "IDEMPOTENCY_KEY_REUSED", "INVALID_REQUEST", "CROSS_LE_FORBIDDEN", "LEGAL_ENTITY_MASTER_UNAVAILABLE"
        ];
        Assert.All(codes, code => Assert.NotEqual(LocationErrorMessages.Generic, LocationErrorMessages.KeyFor(code, 422)));
        Assert.Equal(codes.Length, codes.Select(c => LocationErrorMessages.KeyFor(c, 422)).Distinct().Count());
        Assert.Equal(LocationErrorMessages.NoPermission, LocationErrorMessages.KeyFor(null, 403));
        Assert.Equal(LocationErrorMessages.ServiceUnavailable, LocationErrorMessages.KeyFor(null, 0));
    }

    [Theory]
    [InlineData("Views/Inventory/Locations", "Resources/Views/Inventory/Locations", "LocationsIndex")]
    [InlineData("Views/Inventory/LocationTree", "Resources/Views/Inventory/LocationTree", "LocationTreeIndex")]
    public void Every_key_the_views_ask_for_is_in_the_resx(string viewFolder, string resxFolder, string family)
    {
        var own = Read(resxFolder, family, "en").Keys.ToHashSet(StringComparer.Ordinal);
        var shared = Read("Resources/Views/Inventory/Locations", "LocationsIndex", "en").Keys.ToHashSet(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(Path.Combine(WebRoot, viewFolder), "*.cshtml").Select(File.ReadAllText).ToList();

        var asked = files.SelectMany(t => Regex.Matches(t, @"(?<![A-Za-z])Localizer\[""(?<key>[^""]+)""\]").Select(m => m.Groups["key"].Value)).Distinct().ToList();
        var askedShared = files.SelectMany(t => Regex.Matches(t, @"ListLocalizer\[""(?<key>[^""]+)""\]").Select(m => m.Groups["key"].Value)).Distinct().ToList();

        Assert.NotEmpty(asked);
        Assert.All(asked, key => Assert.Contains(key, own));
        Assert.All(askedShared, key => Assert.Contains(key, shared));
    }

    [Fact]
    public void Every_key_the_location_scripts_read_is_delivered_by_a_payload()
    {
        // What the payload partials serialize (Name = …Localizer["Key"]) — the names window.L10n will carry.
        var payloadNames = new[] { "Views/Inventory/Locations/_IndexL10n.cshtml", "Views/Inventory/LocationTree/_IndexL10n.cshtml" }
            .SelectMany(p => Regex.Matches(File.ReadAllText(Path.Combine(WebRoot, p)), @"^\s*(?<name>\w+)\s*=", RegexOptions.Multiline).Select(m => m.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

        var scripts = new[] { "Locations/index.js", "Locations/form.js", "Locations/bulk-bins.js", "LocationTree/index.js" }
            .Select(p => File.ReadAllText(Path.Combine(WebRoot, "wwwroot", "assets", "js", "Inventory", p)));
        var read = scripts
            .SelectMany(s => Regex.Matches(s, @"L\(\)\.(?<key>[A-Z]\w*)|L\(\)\[(?:'|"")(?<key>[A-Z]\w*)(?:'|"")\]").Select(m => m.Groups["key"].Value))
            .Distinct()
            .ToList();
        // keys composed at runtime: 'Type' + type, 'Status' + status, words[...]
        var composed = new[] { "TypeSite", "TypeWarehouse", "TypeStorageLocation", "TypeZone", "TypeBin", "StatusActive", "StatusInactive", "StatusArchived",
            "ConfirmActivateTitle", "ConfirmActivateText", "ConfirmDeactivateTitle", "ConfirmDeactivateText", "ConfirmArchiveTitle", "ConfirmArchiveText",
            "HistAction_Created", "HistAction_Updated", "HistAction_Moved", "HistAction_StatusChanged", "HistAction_BulkCreated",
            "HistField_code", "HistField_name", "HistField_parentId", "HistField_status", "HistField_isStockable", "HistField_operatingMode", "HistField_orgUnitRef", "HistField_costCenterCode" };

        Assert.NotEmpty(read);
        Assert.All(read.Concat(composed), key => Assert.True(payloadNames.Contains(key), $"window.L10n.{key} is read but no payload delivers it"));
    }

    [Fact]
    public void The_nav_key_of_the_location_page_is_in_all_seven_shared_files()
    {
        foreach (var lang in Languages)
        {
            var shared = Read("Resources", "SharedResource", lang);
            Assert.False(string.IsNullOrWhiteSpace(shared.GetValueOrDefault("Nav.Page.LOCATIONTREE")), $"SharedResource.{lang}: Nav.Page.LOCATIONTREE");
            if (lang != "en") Assert.NotEqual(Read("Resources", "SharedResource", "en")["Nav.Page.LOCATIONTREE"], shared["Nav.Page.LOCATIONTREE"]);
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
