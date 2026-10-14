using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R4 — RECALL-BUNDLE 1.2.0 → 1.3.0 is ADDITIVE, measured against the pinned 1.2.0 file (the git blob at 9729409e0): the version, one
/// changelog line, the R4 slice list (prepareSettingsChange, getSignableSubject out of not-yet-implemented), RECALL_SUBJECT_NOT_FOUND appended
/// to getSignableSubject's x-error-codes and to the registry. Everything else of 1.2.0 is byte-equal as data.
/// </summary>
public sealed class RecallContract130Tests
{
    private const string FrozenV120Sha256 = "6308c9b965acfcecfef4b6bc44f6c53859d3b5fe5d395da264cc7cd707a15996";

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_recall_v1_2_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenV120Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            File.ReadAllText(RecallContract120Tests.FixtureV120Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_3_0_change_is_additive_only()
    {
        var before = Load(RecallContract120Tests.FixtureV120Path);
        var after = Load(ContractSchemas.RecallContractPath);
        Assert.Equal(("1.2.0", "1.3.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));

        var r4 = new[] { "prepareSettingsChange", "getSignableSubject" };
        Assert.Equal(r4, Strings(after["info"]!["x-implemented-by-slice"]!["R4"]));
        Assert.Null(before["info"]!["x-implemented-by-slice"]!["R4"]);
        Assert.Equal(Strings(before["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]).Except(r4), Strings(after["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]));
        var oldLog = Strings(before["info"]!["x-changelog"]);
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.StartsWith("1.3.0 — WP-0177-R4", newLog[0]);
        Assert.Equal(oldLog, newLog.Skip(1));

        var subjectOp = after["paths"]!["/esign-subjects/{subjectType}/{subjectId}"]!["get"]!;
        Assert.Empty(Strings(before["paths"]!["/esign-subjects/{subjectType}/{subjectId}"]!["get"]!["x-error-codes"]));
        Assert.Equal(["RECALL_SUBJECT_NOT_FOUND"], Strings(subjectOp["x-error-codes"]));
        Assert.Null(before["x-error-codes"]!["RECALL_SUBJECT_NOT_FOUND"]);
        Assert.Equal("404", after["x-error-codes"]!["RECALL_SUBJECT_NOT_FOUND"]!.ToString());

        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
            doc["info"]!.AsObject().Remove("x-implemented-by-slice");
            doc["paths"]!["/esign-subjects/{subjectType}/{subjectId}"]!["get"]!.AsObject().Remove("x-error-codes");
            doc["x-error-codes"]!.AsObject().Remove("RECALL_SUBJECT_NOT_FOUND");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "recall.openapi.yaml 1.3.0 changes more than the declared additions.");
    }
}
