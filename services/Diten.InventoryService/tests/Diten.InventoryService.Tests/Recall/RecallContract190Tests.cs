using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-MVP1-FU-CLEAN2 (FU-0177-DRILL-STEP-SCHEMA, O-NAMES1-2) — RECALL-BUNDLE 1.8.0 → 1.9.0 is ADDITIVE and OPTIONAL, measured against the pinned
/// 1.8.0 file (the git blob at 60f5d2b2f): the version, one changelog line, and the person members the service already sent on
/// additionalProperties, named — <c>DrillRun.startedBy / steps[] / pendingResult / completed / cancelled</c>, <c>ReturnLink.linkedBy</c>. None of
/// them is REQUIRED, no <c>additionalProperties</c> changes. Everything else of 1.8.0 is equal as data. The members are the ones the service
/// DTOs carry (measured: <c>RecallDrillRunDto</c>, <c>RecallDrillStepDto</c>, <c>RecallDrillPendingResultDto</c>, <c>RecallReturnLinkDto</c>).
/// </summary>
public sealed class RecallContract190Tests
{
    private const string FrozenV180Sha256 = "a6127312a0b375221a6e26984ba2c5f4b51b492b065e0c14fd4f77096a65ba7b";

    internal static string FixtureV180Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "recall-v1.8.0.openapi.yaml");

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node is null ? [] : node.AsArray().Select(v => v!.GetValue<string>()).ToList();

    private static string Ref(JsonNode? node) => node!["$ref"]!.GetValue<string>();

    [Fact]
    public void The_recall_v1_8_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenV180Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            File.ReadAllText(FixtureV180Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_9_0_change_names_the_drill_and_return_link_people_only_optional()
    {
        var before = Load(FixtureV180Path);
        var after = Load(ContractSchemas.RecallContractPath);
        Assert.Equal(("1.8.0", "1.9.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));
        var oldLog = Strings(before["info"]!["x-changelog"]);
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.StartsWith("1.9.0 — additive, optional (WP-MVP1-FU-CLEAN2", newLog[0]);
        Assert.Equal(oldLog, newLog.Skip(1));

        var schemas = after["components"]!["schemas"]!;
        var oldSchemas = before["components"]!["schemas"]!;
        var run = schemas["DrillRun"]!;
        var p = run["properties"]!;
        Assert.Equal("#/components/schemas/UserRef", Ref(p["startedBy"]));
        Assert.Equal("#/components/schemas/UserAt", Ref(p["completed"]));
        Assert.Equal("#/components/schemas/UserAt", Ref(p["cancelled"]));
        var step = p["steps"]!["items"]!;
        Assert.Equal("#/components/schemas/UserRef", Ref(step["properties"]!["completedBy"]));
        Assert.Equal("#/components/schemas/DrillStep", Ref(step["properties"]!["step"]));
        var pending = p["pendingResult"]!;
        Assert.Equal("#/components/schemas/UserRef", Ref(pending["properties"]!["setBy"]));
        var link = schemas["ReturnLink"]!;
        Assert.Equal("#/components/schemas/UserAt", Ref(link["properties"]!["linkedBy"]));

        // nothing new is required, additionalProperties stays open where it was
        Assert.Equal(Strings(oldSchemas["DrillRun"]!["required"]), Strings(run["required"]));
        Assert.Equal(Strings(oldSchemas["ReturnLink"]!["required"]), Strings(link["required"]));
        Assert.Empty(Strings(step["required"]));
        Assert.Empty(Strings(pending["required"]));
        foreach (var open in new[] { run, link, step, pending }) Assert.Equal("true", open["additionalProperties"]!.ToString());

        // remove exactly the declared additions; the rest must equal 1.8.0 as data
        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
        }
        var ap = after["components"]!["schemas"]!["DrillRun"]!["properties"]!.AsObject();
        foreach (var member in new[] { "startedBy", "pendingResult", "completed", "cancelled" }) Assert.True(ap.Remove(member), member);
        var items = ap["steps"]!["items"]!.AsObject();
        Assert.True(items.Remove("properties"));
        after["components"]!["schemas"]!["ReturnLink"]!["properties"]!.AsObject().Remove("linkedBy");

        Assert.True(JsonNode.DeepEquals(before, after), "recall.openapi.yaml 1.9.0 changes more than the declared additions.");
    }
}
