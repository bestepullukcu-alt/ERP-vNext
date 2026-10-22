using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F4 — FEFO-BUNDLE 1.0.2 → 1.1.0 is ADDITIVE and only FU-0176-F1-UOM: openException's request gains the optional <c>baseUomId</c>;
/// the F4 slice note moves four operations out of not-yet-implemented. Everything else of 1.0.2 is unchanged.
/// </summary>
public sealed class ExpiryF4ContractTests
{
    private const string FrozenFefoV102Sha256 = "c2db927e3b4165556fa5130bcf6f00d0e1f44b47176d06872b2d931d75ee4005";

    private static string FixtureV102Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "fefo-v1.0.2.openapi.yaml");

    /// <summary>WP-0176-F5: the 1.1.0 file is pinned (the current contract is 1.2.0), so this step stays measured on the frozen pair.</summary>
    internal static string FixtureV110Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "fefo-v1.1.0.openapi.yaml");

    private static JsonObject Load(string path)
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path));
        return (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(raw))!;
    }

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public void The_fefo_v1_0_2_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenFefoV102Sha256, Sha(File.ReadAllText(FixtureV102Path).Replace("\r\n", "\n")));

    [Fact]
    public void The_1_1_0_change_is_the_optional_baseUomId_of_openException_and_the_F4_slice_note()
    {
        var before = Load(FixtureV102Path);
        var after = Load(FixtureV110Path);
        Assert.Equal("1.0.2", before["info"]!["version"]!.GetValue<string>());
        Assert.Equal("1.1.0", after["info"]!["version"]!.GetValue<string>());

        var slices = after["info"]!["x-implemented-by-slice"]!.AsObject();
        Assert.Equal(["getDashboard", "listApproaching", "listExpired", "getFefoOrder"], slices["F4"]!.AsArray().Select(v => v!.GetValue<string>()));
        Assert.All(new[] { "getDashboard", "listApproaching", "listExpired", "getFefoOrder" },
            op => Assert.DoesNotContain(op, slices["not-yet-implemented"]!.AsArray().Select(v => v!.GetValue<string>())));
        var changelogBefore = before["info"]!["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        var changelogAfter = after["info"]!["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        Assert.Equal(changelogBefore, changelogAfter.Take(changelogBefore.Count));
        Assert.Contains("FU-0176-F1-UOM", Assert.Single(changelogAfter.Skip(changelogBefore.Count)), StringComparison.Ordinal);

        // the one schema addition: an optional property (not in required) of the openException request
        var request = after["paths"]!["/exceptions"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!.AsObject();
        var baseUom = request["properties"]!["baseUomId"]!.AsObject();
        Assert.Equal("string", baseUom["type"]!.GetValue<string>());
        Assert.DoesNotContain("baseUomId", request["required"]!.AsArray().Select(v => v!.GetValue<string>()));
        request["properties"]!.AsObject().Remove("baseUomId");

        foreach (var doc in new[] { before, after })
        {
            var info = doc["info"]!.AsObject();
            info.Remove("version");
            info.Remove("x-changelog");
            info.Remove("x-implemented-by-slice");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "fefo.openapi.yaml 1.1.0 changes more than openException.baseUomId, the version, the changelog and x-implemented-by-slice.");
    }
}
