using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F5 — FEFO-BUNDLE 1.1.0 → 1.2.0 is ADDITIVE: ExceptionRef gains three optional members (the resolution of a case in the Kanıt
/// Paketi: reason, MOD-0022 signature id, time), getEvidenceExportFile lists the new registry code EXPIRY_EVIDENCE_EXPORT_NOT_FOUND (404),
/// and the F5 slice note moves the four evidence operations out of not-yet-implemented. Everything else of 1.1.0 is unchanged.
/// </summary>
public sealed class ExpiryF5ContractTests
{
    private const string FrozenFefoV110Sha256 = "3dd25a6575ba68f16e61ecb5bcc2bf63946c3775d3939df0e7f318025cc80d3c";

    private static readonly string[] EvidenceOperations = ["previewEvidence", "listEvidenceExports", "createEvidenceExport", "getEvidenceExportFile"];

    private static JsonObject Load(string path)
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path));
        return (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(raw))!;
    }

    private static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_fefo_v1_1_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenFefoV110Sha256, Sha(File.ReadAllText(ExpiryF4ContractTests.FixtureV110Path).Replace("\r\n", "\n")));

    [Fact]
    public void The_1_2_0_change_is_additive_and_only_the_evidence_members()
    {
        var before = Load(ExpiryF4ContractTests.FixtureV110Path);
        var after = Load(ContractSchemas.FefoContractPath);
        Assert.Equal("1.1.0", before["info"]!["version"]!.GetValue<string>());
        Assert.Equal("1.2.0", after["info"]!["version"]!.GetValue<string>());

        var slices = after["info"]!["x-implemented-by-slice"]!.AsObject();
        Assert.Equal(EvidenceOperations, Strings(slices["F5"]));
        Assert.Empty(slices["not-yet-implemented"]!.AsArray());
        var changelogBefore = Strings(before["info"]!["x-changelog"]);
        var changelogAfter = Strings(after["info"]!["x-changelog"]);
        Assert.Equal(changelogBefore, changelogAfter.Take(changelogBefore.Count));
        Assert.Contains("WP-0176-F5", Assert.Single(changelogAfter.Skip(changelogBefore.Count)), StringComparison.Ordinal);

        // ExceptionRef: three OPTIONAL members (not in required)
        var exceptionRef = after["components"]!["schemas"]!["ExceptionRef"]!.AsObject();
        foreach (var member in new[] { "resolutionReason", "resolutionSignatureId", "resolvedAtUtc" })
        {
            Assert.Equal("string", exceptionRef["properties"]![member]!["type"]!.GetValue<string>());
            Assert.DoesNotContain(member, Strings(exceptionRef["required"]));
            exceptionRef["properties"]!.AsObject().Remove(member);
        }

        // the new registry code, listed on the file operation only
        Assert.Equal("404", after["x-error-codes"]!["EXPIRY_EVIDENCE_EXPORT_NOT_FOUND"]!.GetValue<string>());
        after["x-error-codes"]!.AsObject().Remove("EXPIRY_EVIDENCE_EXPORT_NOT_FOUND");
        var fileOp = after["paths"]!["/evidence/exports/{exportId}/file"]!["get"]!.AsObject();
        var codes = Strings(fileOp["x-error-codes"]);
        Assert.Equal("EXPIRY_EVIDENCE_EXPORT_NOT_FOUND", codes.Last());
        fileOp["x-error-codes"] = new JsonArray(codes.Take(codes.Count - 1).Select(c => (JsonNode)JsonValue.Create(c)!).ToArray());

        foreach (var doc in new[] { before, after })
        {
            var info = doc["info"]!.AsObject();
            info.Remove("version");
            info.Remove("x-changelog");
            info.Remove("x-implemented-by-slice");
        }

        Assert.True(JsonNode.DeepEquals(before, after),
            "fefo.openapi.yaml 1.2.0 changes more than the three optional ExceptionRef members, the not-found code, the version, the changelog and x-implemented-by-slice.");
    }
}
