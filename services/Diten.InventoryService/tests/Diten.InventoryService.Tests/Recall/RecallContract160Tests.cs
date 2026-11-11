using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R7a — RECALL-BUNDLE 1.5.0 → 1.6.0 is ADDITIVE (CT decision A, 2026-10-15), measured against the pinned 1.5.0 file (the git blob at
/// 3842938ad): the version, one changelog line, the R7a slice (getCaseCustomers + getCaseEffectiveness out of not-yet-implemented), a description
/// on both operations, CustomerRow.notifications / consumptions, the new schema CaseCustomerShipment, Effectiveness.source. Everything else of
/// 1.5.0 is equal as data — no member removed, no type or REQUIRED list changed.
/// </summary>
public sealed class RecallContract160Tests
{
    private const string FrozenV150Sha256 = "8070b26df07983b15d3694ca75db6be6dc64e587e222c56e13bbddaea1bc52e9";

    internal static string FixtureV150Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "recall-v1.5.0.openapi.yaml");

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_recall_v1_5_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenV150Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            File.ReadAllText(FixtureV150Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_6_0_change_is_additive_only()
    {
        var before = Load(FixtureV150Path);
        var after = Load(RecallContract170Tests.FixtureV160Path); // WP-0177-R7b: 1.6.0 is pinned; 1.6.0 → 1.7.0 is RecallContract170Tests
        Assert.Equal(("1.5.0", "1.6.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));

        Assert.Equal(["getCaseCustomers", "getCaseEffectiveness"], Strings(after["info"]!["x-implemented-by-slice"]!["R7a"]));
        Assert.Null(before["info"]!["x-implemented-by-slice"]!["R7a"]);
        Assert.Equal(Strings(before["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]).Except(["getCaseCustomers", "getCaseEffectiveness"]),
            Strings(after["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]));
        var oldLog = Strings(before["info"]!["x-changelog"]);
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.StartsWith("1.6.0 — WP-0177-R7a", newLog[0]);
        Assert.Equal(oldLog, newLog.Skip(1));

        var schemas = after["components"]!["schemas"]!;
        Assert.Null(before["components"]!["schemas"]!["CaseCustomerShipment"]);
        Assert.NotNull(schemas["CaseCustomerShipment"]);
        foreach (var name in new[] { "CustomerRow", "Effectiveness", "EffectivenessRow", "Notification", "ConsumptionDeclaration" })
        {
            Assert.Equal(Strings(before["components"]!["schemas"]![name]!["required"]), Strings(schemas[name]!["required"]));
        }

        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
            doc["info"]!.AsObject().Remove("x-implemented-by-slice");
            doc["paths"]!["/cases/{caseId}/customers"]!["get"]!.AsObject().Remove("description");
            doc["paths"]!["/cases/{caseId}/effectiveness"]!["get"]!.AsObject().Remove("description");
            var s = doc["components"]!["schemas"]!.AsObject();
            s.Remove("CaseCustomerShipment");
            s["CustomerRow"]!["properties"]!.AsObject().Remove("notifications");
            s["CustomerRow"]!["properties"]!.AsObject().Remove("consumptions");
            s["Effectiveness"]!["properties"]!.AsObject().Remove("source");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "recall.openapi.yaml 1.6.0 changes more than the declared additions.");
    }
}
