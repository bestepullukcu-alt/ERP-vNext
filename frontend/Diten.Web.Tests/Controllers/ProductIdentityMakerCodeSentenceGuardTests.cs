using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Diten.Web.Views.MasterDataManagement.Items;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-MDM-NO-ELIGIBLE-APPROVER-01 — every product-identity maker code (ProductIdentityWorkflowMakerCodes.IsMakerCode, read
/// from MDM's source: Web.Tests does not reference MDM) has a sentence on each of the four screens that can receive it,
/// in all seven languages. The Global Product and LSKU maps are read only on a 409, so there the set is the maker codes
/// StatusFor answers 409; on the GSKU screen (its known codes ∪ GskuRefusalSentences, read in that order) and the Items
/// screen every maker code. A new maker code without a sentence turns this red.
/// </summary>
public sealed class ProductIdentityMakerCodeSentenceGuardTests
{
    private const string NoEligibleApprover = "PRODUCT_IDENTITY_WORKFLOW_NO_ELIGIBLE_APPROVER";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void The_maker_codes_are_the_measured_eight_and_five_of_them_answer_409()
    {
        var makers = MakerCodes();
        var conflicts = Maker409Codes();

        Assert.Equal(8, makers.Count);
        Assert.Contains(NoEligibleApprover, makers);
        Assert.Equal(
            [
                "PRODUCT_IDENTITY_WORKFLOW_APPROVAL_CHANGING", "PRODUCT_IDENTITY_WORKFLOW_APPROVER_NOT_ASSIGNED",
                "PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY", "PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED",
                NoEligibleApprover
            ],
            conflicts.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_409_maker_code_has_a_global_product_sentence_in_all_seven_languages()
    {
        var map = ControllerMap(typeof(GlobalProductsController), "InFlightRefusalKeys");
        AssertCovered("Global Products", Maker409Codes(), map);
        Assert.Equal("ErrorNoEligibleApprover", map[NoEligibleApprover]);
        AssertSentences(map, Mapped409Codes(map), "GlobalProducts", "GlobalProductsIndex");
    }

    [Fact]
    public void Every_409_maker_code_has_an_lsku_sentence_in_all_seven_languages()
    {
        var map = ControllerMap(typeof(LskusController), "RefusalSentences");
        AssertCovered("LSKU", Maker409Codes(), map);
        Assert.Equal("ErrorNoEligibleApprover", map[NoEligibleApprover]);
        AssertSentences(map, Mapped409Codes(map), "Lskus", "LskusIndex");
    }

    [Fact]
    public void Every_maker_code_has_a_gsku_sentence_in_all_seven_languages()
    {
        var known = ControllerMap(typeof(GskusController), "KnownFailureKeys");
        var refusals = GskuRefusalSentences.Keys;
        var union = known.Keys.Concat(refusals.Keys).ToHashSet(StringComparer.Ordinal);
        var missing = MakerCodes().Where(code => !union.Contains(code)).Order(StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, "GSKU maker codes without a sentence: " + string.Join(", ", missing));
        Assert.Equal("RefusalNoEligibleApprover", refusals[NoEligibleApprover]);
        AssertSentences(known, MakerCodes().Where(known.ContainsKey), "Gskus", "GskusIndex");
        AssertSentences(refusals, MakerCodes().Where(code => !known.ContainsKey(code)), "Gskus", "GskuRefusals");
    }

    [Fact]
    public void Every_maker_code_has_an_item_sentence_in_all_seven_languages()
    {
        var map = ItemRefusalSentences.ApprovalKeys;
        AssertCovered("Items", MakerCodes(), map);
        Assert.Equal("RefusalApprovalNoEligibleApprover", map[NoEligibleApprover]);
        AssertSentences(map, MakerCodes(), "Items", "ItemRefusals");
    }

    private static IEnumerable<string> Mapped409Codes(IReadOnlyDictionary<string, string> map) =>
        Maker409Codes().Where(map.ContainsKey);

    private static void AssertCovered(string screen, IEnumerable<string> codes, IReadOnlyDictionary<string, string> map)
    {
        var missing = codes.Where(code => !map.ContainsKey(code)).Order(StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, $"{screen} maker codes without a sentence: " + string.Join(", ", missing));
    }

    // Each mapped code's sentence key is a non-empty value in all seven languages of the screen's resource.
    private static void AssertSentences(IReadOnlyDictionary<string, string> map, IEnumerable<string> codes, string folder, string resource)
    {
        foreach (var language in Languages)
        {
            var values = Values(folder, resource, language);
            foreach (var code in codes)
                Assert.True(values.TryGetValue(map[code], out var sentence) && !string.IsNullOrWhiteSpace(sentence),
                    $"{resource}.{language}.resx has no sentence {map[code]} (for {code})");
        }
    }

    private static IReadOnlyDictionary<string, string> ControllerMap(Type controller, string field) =>
        (IReadOnlyDictionary<string, string>)controller
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static Dictionary<string, string> Values(string folder, string resource, string language) =>
        XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterDataManagement",
                folder, $"{resource}.{language}.resx"))
            .Root!.Elements("data")
            .ToDictionary(x => (string)x.Attribute("name")!, x => (string?)x.Element("value") ?? string.Empty, StringComparer.Ordinal);

    // The codes ProductIdentityWorkflowMakerCodes.IsMakerCode accepts, read from MDM's source (ItemRefusalBridgeTests'
    // pattern): the predicate's constant names resolved to their values.
    private static HashSet<string> MakerCodes()
    {
        var (policy, constants) = Policy();
        var predicate = Regex.Match(policy, "IsMakerCode\\(string\\? code\\) => code is (?<names>[^;]+);");
        Assert.True(predicate.Success, "IsMakerCode was not found.");
        return Names(predicate.Groups["names"].Value)
            .Select(name => constants.TryGetValue(name, out var value) ? value : throw new InvalidOperationException("unresolved " + name))
            .ToHashSet(StringComparer.Ordinal);
    }

    // The maker codes StatusFor answers 409: every one its switch does not send elsewhere (the default arm is 409).
    private static HashSet<string> Maker409Codes()
    {
        var (policy, constants) = Policy();
        var statusFor = Regex.Match(policy, "StatusFor\\(string\\? code\\) => code switch\\s*\\{(?<arms>[^}]+)\\}");
        Assert.True(statusFor.Success, "StatusFor was not found.");
        Assert.Contains("_ => 409", statusFor.Groups["arms"].Value);
        var elsewhere = Regex.Matches(statusFor.Groups["arms"].Value, "(?<name>[A-Z]\\w+)\\s*=>\\s*(?<status>\\d{3})")
            .Where(match => match.Groups["status"].Value != "409")
            .Select(match => constants[match.Groups["name"].Value])
            .ToHashSet(StringComparer.Ordinal);
        return MakerCodes().Where(code => !elsewhere.Contains(code)).ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> Names(string text) =>
        Regex.Matches(text, "\\b([A-Z]\\w+)\\b").Select(match => match.Groups[1].Value).Where(name => name != "or");

    private static (string Source, Dictionary<string, string> Constants) Policy()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src", "Diten.MdmService.Application",
            "Features", "ProductItemSkuMaster", "Workflow", "ProductIdentityWorkflowFaultPolicy.cs"));
        var constants = Regex.Matches(source, "public const string (\\w+) = \"([A-Z_]+)\"")
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value, StringComparer.Ordinal);
        return (source, constants);
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
