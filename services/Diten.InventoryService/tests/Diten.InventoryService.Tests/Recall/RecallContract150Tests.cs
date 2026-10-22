using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R6 — RECALL-BUNDLE 1.4.0 → 1.5.0 is ADDITIVE (CT decision A, 2026-10-15), measured against the pinned 1.4.0 file (the git blob at
/// 9c41cc92c): the version, one changelog line, the R6 slice (getDashboard out of not-yet-implemented), listCases' description + the appended
/// drillStatus parameter, CaseRef.sortAt / position, CasePage.drillRows / drillStatusCounts, the new schemas DrillCaseRow + the five dashboard
/// member schemas, the DashboardView members. Everything else of 1.4.0 is equal as data — no member removed, no type or REQUIRED list changed
/// (CaseRef keeps class / level required: a drill run is not a case).
/// </summary>
public sealed class RecallContract150Tests
{
    private const string FrozenV140Sha256 = "ffaad8acbf30960b8cd02a104f8314c946075aec08ab92988fe0da2e3971c497";

    internal static string FixtureV140Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "recall-v1.4.0.openapi.yaml");

    private static readonly string[] NewSchemas =
        ["DrillCaseRow", "DashboardDrillPoint", "DashboardDrillChart", "DashboardLotCoverage", "DashboardTraceGaps", "DashboardReturned"];

    private static readonly string[] DashboardMembers =
        ["asOfUtc", "legalEntityId", "lastDrill", "drillChart", "lotCoverage", "traceGaps", "returnedOpenCases", "frequencyWarning", "openCaseRows"];

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_recall_v1_4_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenV140Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            File.ReadAllText(FixtureV140Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_5_0_change_is_additive_only()
    {
        var before = Load(FixtureV140Path);
        var after = Load(RecallContract160Tests.FixtureV150Path); // WP-0177-R7a: 1.5.0 is pinned; 1.5.0 → 1.6.0 is RecallContract160Tests
        Assert.Equal(("1.4.0", "1.5.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));

        Assert.Equal(["getDashboard"], Strings(after["info"]!["x-implemented-by-slice"]!["R6"]));
        Assert.Null(before["info"]!["x-implemented-by-slice"]!["R6"]);
        Assert.Equal(Strings(before["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]).Except(["getDashboard"]),
            Strings(after["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]));
        var oldLog = Strings(before["info"]!["x-changelog"]);
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.StartsWith("1.5.0 — WP-0177-R6", newLog[0]);
        Assert.Equal(oldLog, newLog.Skip(1));

        // listCases: a description + ONE parameter appended at the end
        var oldParams = before["paths"]!["/cases"]!["get"]!["parameters"]!.AsArray();
        var newParams = after["paths"]!["/cases"]!["get"]!["parameters"]!.AsArray();
        Assert.Equal(oldParams.Count + 1, newParams.Count);
        Assert.True(JsonNode.DeepEquals(new JsonArray(oldParams.Select(p => p!.DeepClone()).ToArray()), new JsonArray(newParams.Take(oldParams.Count).Select(p => p!.DeepClone()).ToArray())));
        Assert.Equal("drillStatus", newParams[^1]!["name"]!.GetValue<string>());
        Assert.Null(before["paths"]!["/cases"]!["get"]!["description"]);

        var schemas = after["components"]!["schemas"]!;
        Assert.All(NewSchemas, s => Assert.Null(before["components"]!["schemas"]![s]));
        Assert.All(NewSchemas, s => Assert.NotNull(schemas[s]));
        Assert.DoesNotContain("class", Strings(schemas["DrillCaseRow"]!["required"]));
        Assert.Null(schemas["DrillCaseRow"]!["properties"]!["class"]);
        Assert.Null(schemas["DrillCaseRow"]!["properties"]!["level"]);
        Assert.Equal(Strings(before["components"]!["schemas"]!["CaseRef"]!["required"]), Strings(schemas["CaseRef"]!["required"]));
        Assert.Equal(Strings(before["components"]!["schemas"]!["CasePage"]!["required"]), Strings(schemas["CasePage"]!["required"]));
        Assert.Equal(Strings(before["components"]!["schemas"]!["DashboardView"]!["required"]), Strings(schemas["DashboardView"]!["required"]));

        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
            doc["info"]!.AsObject().Remove("x-implemented-by-slice");
            var list = doc["paths"]!["/cases"]!["get"]!.AsObject();
            list.Remove("description");
            var parameters = list["parameters"]!.AsArray();
            if (parameters[^1]!["name"]?.GetValue<string>() == "drillStatus") parameters.RemoveAt(parameters.Count - 1);
            var s = doc["components"]!["schemas"]!.AsObject();
            foreach (var name in NewSchemas) s.Remove(name);
            s["CaseRef"]!["properties"]!.AsObject().Remove("sortAt");
            s["CaseRef"]!["properties"]!.AsObject().Remove("position");
            s["CasePage"]!["properties"]!.AsObject().Remove("drillRows");
            s["CasePage"]!["properties"]!.AsObject().Remove("drillStatusCounts");
            foreach (var member in DashboardMembers) s["DashboardView"]!["properties"]!.AsObject().Remove(member);
        }

        Assert.True(JsonNode.DeepEquals(before, after), "recall.openapi.yaml 1.5.0 changes more than the declared additions.");
    }
}
