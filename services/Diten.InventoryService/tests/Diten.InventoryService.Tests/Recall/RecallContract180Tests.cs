using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-MVP1-NAMES1 — RECALL-BUNDLE 1.7.0 → 1.8.0 is ADDITIVE and OPTIONAL (owner decision A, 2026-10-19), measured against the pinned 1.7.0 file
/// (the git blob at 2452eb7cf): the version, one changelog line, and the member <c>name</c> on <c>UserRef</c> and <c>UserAt</c> — in neither
/// schema's REQUIRED list. Everything else of 1.7.0 is equal as data.
/// </summary>
public sealed class RecallContract180Tests
{
    private const string FrozenV170Sha256 = "a7e483b5b6965c4b8fbc0d6cf7d0a3b27efbd13b7046ccc28cc7dbd77b4540bf";

    internal static string FixtureV170Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "recall-v1.7.0.openapi.yaml");

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_recall_v1_7_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenV170Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            File.ReadAllText(FixtureV170Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_8_0_change_is_the_optional_name_of_UserRef_and_UserAt_only()
    {
        var before = Load(FixtureV170Path);
        var after = Load(ContractSchemas.RecallContractPath);
        Assert.Equal(("1.7.0", "1.8.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));
        var oldLog = Strings(before["info"]!["x-changelog"]);
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.StartsWith("1.8.0 — additive, optional (WP-MVP1-NAMES1", newLog[0]);
        Assert.Equal(oldLog, newLog.Skip(1));

        foreach (var schema in new[] { "UserRef", "UserAt" })
        {
            var s = after["components"]!["schemas"]![schema]!;
            Assert.Equal("string", s["properties"]!["name"]!["type"]!.GetValue<string>());
            Assert.Equal(Strings(before["components"]!["schemas"]![schema]!["required"]), Strings(s["required"])); // name is NOT required
            Assert.DoesNotContain("name", Strings(s["required"]));
        }

        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
            doc["components"]!["schemas"]!["UserRef"]!["properties"]!.AsObject().Remove("name");
            doc["components"]!["schemas"]!["UserAt"]!["properties"]!.AsObject().Remove("name");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "recall.openapi.yaml 1.8.0 changes more than the declared additions.");
    }
}
