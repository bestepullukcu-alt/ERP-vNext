using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R5 — RECALL-BUNDLE 1.3.0 → 1.4.0 is ADDITIVE, measured against the pinned 1.3.0 file (the git blob at 394c05550): the version, one
/// changelog line, the R5 slice (the ten drill operations out of not-yet-implemented), x-error-codes APPENDED on the drill operations
/// (RECALL_DRILL_NOT_FOUND everywhere a plan / run id is named; R5-3 / R5-4 / R5-5), the 400 response on completeDrillStep / cancelDrill,
/// RECALL_DRILL_NOT_FOUND in the registry, DrillRunPage.frequencyWarning + the new schema DrillFrequencyWarning (R5-2). Everything else of
/// 1.3.0 is equal as data — no member removed, no type or required list changed.
/// </summary>
public sealed class RecallContract140Tests
{
    private const string FrozenV130Sha256 = "4a2ee67d98e1324eed2ed3ea316eff463c05fe58f1031e7f77fdb2b35ae90736";

    private static readonly string[] R5 =
        ["listDrillPlans", "createDrillPlan", "updateDrillPlan", "listDrillRuns", "startDrill", "getDrillRun", "completeDrillStep", "prepareDrillResult", "signDrillResult", "cancelDrill"];

    /// <summary>(path, method, codes appended at the END of the 1.3.0 list).</summary>
    private static readonly (string Path, string Method, string[] Appended)[] AppendedCodes =
    [
        ("/drill-plans", "get", []),
        ("/drill-plans", "post", []),
        ("/drill-plans/{planId}", "put", ["RECALL_DRILL_NOT_FOUND"]),
        ("/drill-runs", "get", []),
        ("/drill-runs", "post", ["RECALL_DRILL_NOT_FOUND"]),
        ("/drill-runs/{runId}", "get", ["RECALL_DRILL_NOT_FOUND"]),
        ("/drill-runs/{runId}/steps/{step}", "post", ["INVALID_REQUEST", "UNKNOWN_LOT", "RECALL_DRILL_NOT_FOUND"]),
        ("/drill-runs/{runId}/pending-result", "post", ["RECALL_DRILL_NOT_FOUND"]),
        ("/drill-runs/{runId}/result", "post", ["REASON_REQUIRED", "RECALL_INVALID_TRANSITION", "RECALL_DRILL_NOT_FOUND"]),
        ("/drill-runs/{runId}/cancel", "post", ["INVALID_REQUEST", "REASON_REQUIRED", "RECALL_DRILL_NOT_FOUND"])
    ];

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_recall_v1_3_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenV130Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            File.ReadAllText(RecallContract130Tests.FixtureV130Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_4_0_change_is_additive_only()
    {
        var before = Load(RecallContract130Tests.FixtureV130Path);
        var after = Load(ContractSchemas.RecallContractPath);
        Assert.Equal(("1.3.0", "1.4.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));

        Assert.Equal(R5, Strings(after["info"]!["x-implemented-by-slice"]!["R5"]));
        Assert.Null(before["info"]!["x-implemented-by-slice"]!["R5"]);
        Assert.Equal(Strings(before["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]).Except(R5),
            Strings(after["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]));
        var oldLog = Strings(before["info"]!["x-changelog"]);
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.StartsWith("1.4.0 — WP-0177-R5", newLog[0]);
        Assert.Equal(oldLog, newLog.Skip(1));

        foreach (var (path, method, appended) in AppendedCodes)
        {
            var oldCodes = Strings(before["paths"]![path]![method]!["x-error-codes"]);
            Assert.Equal(oldCodes.Concat(appended), Strings(after["paths"]![path]![method]!["x-error-codes"]));
        }

        foreach (var path in new[] { "/drill-runs/{runId}/steps/{step}", "/drill-runs/{runId}/cancel" })
        {
            Assert.Null(before["paths"]![path]!["post"]!["responses"]!["400"]);
            Assert.NotNull(after["paths"]![path]!["post"]!["responses"]!["400"]);
        }

        Assert.Null(before["x-error-codes"]!["RECALL_DRILL_NOT_FOUND"]);
        Assert.Equal("404", after["x-error-codes"]!["RECALL_DRILL_NOT_FOUND"]!.ToString());
        Assert.Null(before["components"]!["schemas"]!["DrillFrequencyWarning"]);
        Assert.Null(before["components"]!["schemas"]!["DrillRunPage"]!["properties"]!["frequencyWarning"]);
        Assert.DoesNotContain("frequencyWarning", Strings(after["components"]!["schemas"]!["DrillRunPage"]!["required"]));

        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
            doc["info"]!.AsObject().Remove("x-implemented-by-slice");
            foreach (var (path, method, _) in AppendedCodes) doc["paths"]![path]![method]!.AsObject().Remove("x-error-codes");
            doc["paths"]!["/drill-runs/{runId}/steps/{step}"]!["post"]!["responses"]!.AsObject().Remove("400");
            doc["paths"]!["/drill-runs/{runId}/cancel"]!["post"]!["responses"]!.AsObject().Remove("400");
            doc["x-error-codes"]!.AsObject().Remove("RECALL_DRILL_NOT_FOUND");
            doc["components"]!["schemas"]!.AsObject().Remove("DrillFrequencyWarning");
            doc["components"]!["schemas"]!["DrillRunPage"]!["properties"]!.AsObject().Remove("frequencyWarning");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "recall.openapi.yaml 1.4.0 changes more than the declared additions.");
    }
}
