using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R7b — RECALL-BUNDLE 1.6.0 → 1.7.0 is ADDITIVE (CT decision A, 2026-10-15), measured against the pinned 1.6.0 file (the git blob at
/// a62f8a39a): the version, one changelog line, the R7b slice (the four evidence operations out of not-yet-implemented), the new error code
/// RECALL_EVIDENCE_EXPORT_NOT_FOUND (registry + getEvidenceFile's list, appended), ReturnLink.holdId / holdNumber (D-28), EvidenceExport.sections /
/// dmsRef. Everything else of 1.6.0 is equal as data — no member removed, no type or REQUIRED list changed.
/// </summary>
public sealed class RecallContract170Tests
{
    private const string FrozenV160Sha256 = "c32090aeae98e3dea3733aea1356bce6f627fa70444a628e3b30ca7fea152e23";
    private const string NewCode = "RECALL_EVIDENCE_EXPORT_NOT_FOUND";

    internal static string FixtureV160Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "recall-v1.6.0.openapi.yaml");

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_recall_v1_6_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenV160Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            File.ReadAllText(FixtureV160Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_7_0_change_is_additive_only()
    {
        var before = Load(FixtureV160Path);
        var after = Load(ContractSchemas.RecallContractPath);
        Assert.Equal(("1.6.0", "1.7.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));

        string[] evidence = ["previewEvidence", "listEvidenceExports", "getEvidenceFile", "createEvidenceExport"];
        Assert.Equal(evidence, Strings(after["info"]!["x-implemented-by-slice"]!["R7b"]));
        Assert.Null(before["info"]!["x-implemented-by-slice"]!["R7b"]);
        Assert.Equal(Strings(before["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]).Except(evidence),
            Strings(after["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]));
        var oldLog = Strings(before["info"]!["x-changelog"]);
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.StartsWith("1.7.0 — WP-0177-R7b", newLog[0]);
        Assert.Equal(oldLog, newLog.Skip(1));

        // the new code: in the registry and APPENDED to getEvidenceFile's list
        Assert.Null(before["x-error-codes"]![NewCode]);
        Assert.Equal("404", after["x-error-codes"]![NewCode]!.ToString());
        var oldFileCodes = Strings(before["paths"]!["/evidence/exports/{exportId}/file"]!["get"]!["x-error-codes"]);
        Assert.Equal(oldFileCodes.Append(NewCode), Strings(after["paths"]!["/evidence/exports/{exportId}/file"]!["get"]!["x-error-codes"]));

        var schemas = after["components"]!["schemas"]!;
        foreach (var name in new[] { "ReturnLink", "EvidenceExport", "EvidencePreview", "EvidenceExportPage" })
        {
            Assert.Equal(Strings(before["components"]!["schemas"]![name]!["required"]), Strings(schemas[name]!["required"]));
        }

        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
            doc["info"]!.AsObject().Remove("x-implemented-by-slice");
            doc["x-error-codes"]!.AsObject().Remove(NewCode);
            var codes = doc["paths"]!["/evidence/exports/{exportId}/file"]!["get"]!["x-error-codes"]!.AsArray();
            if (codes[^1]!.GetValue<string>() == NewCode) codes.RemoveAt(codes.Count - 1);
            var s = doc["components"]!["schemas"]!.AsObject();
            s["ReturnLink"]!["properties"]!.AsObject().Remove("holdId");
            s["ReturnLink"]!["properties"]!.AsObject().Remove("holdNumber");
            s["EvidenceExport"]!["properties"]!.AsObject().Remove("sections");
            s["EvidenceExport"]!["properties"]!.AsObject().Remove("dmsRef");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "recall.openapi.yaml 1.7.0 changes more than the declared additions.");
    }
}
