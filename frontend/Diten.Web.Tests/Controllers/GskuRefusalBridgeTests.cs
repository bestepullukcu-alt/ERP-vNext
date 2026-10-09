using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX3 item 4 (B18) — the code ⇔ sentence bridge, held in lock-step the way the password
/// error-code bridge is: every code MDM's GSKU code can answer with has a sentence key (FIX1/FIX2's GskusIndex keys or
/// the GskuRefusals map), no mapped code is an orphan, and every key exists in all seven languages.
/// The codes are read from MDM's own source (the production code, not a copy of it).
/// </summary>
public sealed class GskuRefusalBridgeTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // The GSKU code of MDM: the controller and every file of the GSKU flows and the shared product-identity workflow.
    // MOD-0290-FU04 S3b: + StockFlagGate (the GSKU submit's lot-flag refusal).
    private static readonly Regex GskuFile = new(
        @"^(Gsku|FirstGsku|ProductIdentityWorkflow|IFirstGsku|IGsku|PlatformWorkflowVerifiedGsku|IVerifiedGsku|CreateFirstGsku|UpdateGsku|RetireGsku|StartGsku|StartFirstGsku|WithdrawFirstGsku|GetGsku|StockFlagGate)[^/]*\.cs$",
        RegexOptions.CultureInvariant);

    private static readonly Regex CodeLiteral = new("\"([A-Z][A-Z0-9]+(?:_[A-Z0-9]+)+)\"", RegexOptions.CultureInvariant);

    // Upper-case literals in that code that are NOT answers: flow labels, action and state names, a reference value.
    private static readonly HashSet<string> NotRefusals = new(StringComparer.Ordinal)
    {
        "GSKU_CORRECTION", "GSKU_RETIREMENT_REQUEST", "REQUEST_CORRECTION", "REQUEST_RETIREMENT", "WITHDRAW_APPROVAL",
        "REPEAT_REQUEST",
        "RESUBMIT_REQUIRED", "SUBMIT_IN_FLIGHT", "SUBMISSION_ENDED", "RECONCILIATION_REQUIRED",
        "SCALAR_QUANTITY_APPLIES", "VERIFIED_GSKU_RESOLVE",
        // The recovery workers' flow names, their start-up and command-line refusals, and the log's placeholder for an
        // unreadable code: none of them is ever an answer of an endpoint.
        "FIRST_GSKU_IDENTITY", "GSKU_PAIR_RETIREMENT", "UNEXPECTED_FAILURE",
        // BL-552 flow 3: the Global Product identity runner's flow name (its gauge label).
        "GLOBAL_PRODUCT_IDENTITY",
        // LSKU FIX1 (CT 2026-10-06): the stuck child-creation admission gauge's flow name.
        "GSKU_CHILD_ADMISSION",
        // FG part B (CT 2026-10-07, takeover merge): the same gauge's Finished Good cancellation flow name.
        "FINISHED_GOOD_DRAFT_CANCELLATION",
        "FIRST_GSKU_IDENTITY_WORKFLOW_RECOVERY_ARGUMENT_DUPLICATE", "FIRST_GSKU_IDENTITY_WORKFLOW_RECOVERY_ARGUMENT_INVALID",
        "FIRST_GSKU_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID", "GSKU_CORRECTION_RECOVERY_ARGUMENT_DUPLICATE",
        "GSKU_CORRECTION_RECOVERY_ARGUMENT_INVALID", "GSKU_CORRECTION_WORKER_CONFIGURATION_INVALID",
        "GSKU_RETIREMENT_REQUEST_RECOVERY_ARGUMENT_DUPLICATE", "GSKU_RETIREMENT_REQUEST_RECOVERY_ARGUMENT_INVALID",
        "GSKU_RETIREMENT_REQUEST_WORKER_CONFIGURATION_INVALID", "PRODUCT_IDENTITY_WORKFLOW_RECOVERY_ARGUMENT_DUPLICATE",
        "PRODUCT_IDENTITY_WORKFLOW_RECOVERY_ARGUMENT_INVALID", "PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID"
    };

    [Fact]
    public void Every_code_of_mdms_gsku_code_has_a_sentence()
    {
        var mapped = MappedCodes();
        var missing = MdmCodes().Where(code => !NotRefusals.Contains(code) && !mapped.ContainsKey(code)).ToArray();

        Assert.True(missing.Length == 0, "Codes without a sentence: " + string.Join(", ", missing));
    }

    [Fact]
    public void No_mapped_code_is_an_orphan()
    {
        var known = MdmCodes();
        var orphans = GskuRefusalSentences.Keys.Keys.Where(code => !known.Contains(code)).ToArray();

        Assert.True(orphans.Length == 0, "Mapped codes MDM never answers with: " + string.Join(", ", orphans));
        Assert.Empty(NotRefusals.Where(code => MappedCodes().ContainsKey(code)));
    }

    [Fact]
    public void Every_refusal_key_is_in_all_seven_languages_and_no_language_has_more()
    {
        var keys = GskuRefusalSentences.Keys.Values.ToHashSet(StringComparer.Ordinal);
        foreach (var language in Languages)
        {
            var file = ResxKeys($"GskuRefusals.{language}.resx");
            Assert.True(keys.SetEquals(file.Keys), $"GskuRefusals.{language}.resx differs from the map.");
            Assert.All(file, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"{language}:{entry.Key} is empty"));
        }
    }

    [Fact]
    public void Every_fix1_and_fix2_key_is_in_all_seven_languages()
    {
        foreach (var language in Languages)
        {
            var file = ResxKeys($"GskusIndex.{language}.resx");
            foreach (var key in KnownFailureKeys().Values.Distinct())
                Assert.True(file.ContainsKey(key), $"GskusIndex.{language}.resx misses {key}");
        }
    }

    [Fact]
    public void The_two_maps_do_not_overlap()
    {
        Assert.Empty(KnownFailureKeys().Keys.Intersect(GskuRefusalSentences.Keys.Keys));
    }

    private static Dictionary<string, string> MappedCodes()
    {
        var all = new Dictionary<string, string>(KnownFailureKeys(), StringComparer.Ordinal);
        foreach (var (code, key) in GskuRefusalSentences.Keys)
            all[code] = key;
        return all;
    }

    private static IReadOnlyDictionary<string, string> KnownFailureKeys() =>
        (IReadOnlyDictionary<string, string>)typeof(GskusController)
            .GetField("KnownFailureKeys", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    private static HashSet<string> MdmCodes()
    {
        var src = Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src");
        var files = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => GskuFile.IsMatch(Path.GetFileName(path))
                           || path.EndsWith(Path.Combine("Controllers", "GskusController.cs"), StringComparison.Ordinal))
            .ToArray();
        Assert.True(files.Length > 50, $"Only {files.Length} GSKU files found — the scan no longer sees MDM's code.");
        return files.SelectMany(path => CodeLiteral.Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static Dictionary<string, string> ResxKeys(string file) =>
        XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "MasterDataManagement",
                "Gskus", file))
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
