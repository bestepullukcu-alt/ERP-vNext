using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R3 — RECALL-BUNDLE 1.1.0 → 1.2.0 is ADDITIVE, measured against the pinned 1.1.0 file (the git blob at 12a383b74): the version, one
/// changelog line, the R3 slice list (and the six operations out of not-yet-implemented), CaseDetail.pendingRevision + the new PendingRevision
/// schema, six CaseEventKind values appended, one state-machine line appended, and x-error-codes appended (never reordered) on
/// retryQuarantineRequest and reviseScope. Everything else of 1.1.0 is byte-equal as data.
/// </summary>
public sealed class RecallContract120Tests
{
    private const string FrozenV110Sha256 = "41c6496fba1bd4fc4554a621de2ee0800f3fa6387c2dbedfdd33250f6acd5283";

    private static string FixtureV110Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "recall-v1.1.0.openapi.yaml");

    /// <summary>WP-0177-R4: the 1.2.0 file is pinned too (<see cref="RecallContract130Tests"/>); this test now measures 1.1.0 → 1.2.0 between the two fixtures.</summary>
    internal static string FixtureV120Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "recall-v1.2.0.openapi.yaml");

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_recall_v1_1_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenV110Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(FixtureV110Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_2_0_change_is_additive_only()
    {
        var before = Load(FixtureV110Path);
        var after = Load(FixtureV120Path);
        Assert.Equal(("1.1.0", "1.2.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));

        var r3 = new[] { "getCaseStock", "requestQuarantine", "retryQuarantineRequest", "getCaseReturns", "linkReturn", "reviseScope" };
        Assert.Equal(r3, Strings(after["info"]!["x-implemented-by-slice"]!["R3"]));
        Assert.Equal(Strings(before["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]).Except(r3), Strings(after["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]));
        var oldLog = Strings(before["info"]!["x-changelog"]);
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.StartsWith("1.2.0 — WP-0177-R3", newLog[0]);
        Assert.Equal(oldLog, newLog.Skip(1));

        var events = Strings(before["components"]!["schemas"]!["CaseEventKind"]!["enum"]);
        Assert.Equal([.. events, "QuarantineRequested", "QuarantineFailed", "QuarantineRetried", "ExitRefused", "ReturnLinked", "ScopeRevised"],
            Strings(after["components"]!["schemas"]!["CaseEventKind"]!["enum"]));
        Assert.Equal(Strings(before["x-state-machine"]), Strings(after["x-state-machine"]).Take(Strings(before["x-state-machine"]).Count));
        Assert.Single(Strings(after["x-state-machine"]).Skip(Strings(before["x-state-machine"]).Count));
        Assert.Null(before["components"]!["schemas"]!["PendingRevision"]);
        Assert.Equal("#/components/schemas/PendingRevision", after["components"]!["schemas"]!["CaseDetail"]!["properties"]!["pendingRevision"]!["$ref"]!.GetValue<string>());
        Assert.DoesNotContain("pendingRevision", Strings(after["components"]!["schemas"]!["CaseDetail"]!["required"]));

        foreach (var (path, method, appended) in new[]
                 {
                     ("/cases/{caseId}/quarantine-requests/{requestId}/retry", "post", new[] { "RECALL_CONFLICT" }),
                     ("/cases/{caseId}/scope/revisions", "post", new[] { "RECALL_SCOPE_ITEM_NOT_MARKABLE", "RECALL_SCOPE_EXCLUSION_REASON_REQUIRED", "UNKNOWN_LOT" })
                 })
        {
            var old = Strings(before["paths"]![path]![method]!["x-error-codes"]);
            Assert.Equal([.. old, .. appended], Strings(after["paths"]![path]![method]!["x-error-codes"]));
            foreach (var doc in new[] { before, after }) doc["paths"]![path]![method]!.AsObject().Remove("x-error-codes");
        }

        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
            doc["info"]!.AsObject().Remove("x-implemented-by-slice");
            doc["components"]!["schemas"]!["CaseEventKind"]!.AsObject().Remove("enum");
            doc["components"]!["schemas"]!["CaseEventKind"]!.AsObject().Remove("description");
            doc["components"]!["schemas"]!.AsObject().Remove("PendingRevision");
            doc["components"]!["schemas"]!["CaseDetail"]!["properties"]!.AsObject().Remove("pendingRevision");
            doc.Remove("x-state-machine");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "recall.openapi.yaml 1.2.0 changes more than the declared additions.");
    }
}
