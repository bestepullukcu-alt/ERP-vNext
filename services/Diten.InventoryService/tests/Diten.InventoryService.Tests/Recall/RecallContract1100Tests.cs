using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// MOD-0177-FU01 AC-16 — RECALL-BUNDLE 1.9.0 → 1.10.0 is "additive + one input restriction", measured against the pinned 1.9.0 file (the git
/// blob at 6cd16ab00): the version, one changelog line naming the change, the CustomerDisplay schema and the five optional customer members,
/// the customerPage parameter on six reads (INVALID_REQUEST / 400 where missing), the ServiceUnavailable response and the two directory codes
/// on the three record commands, RECALL_CUSTOMER_DIRECTORY_FORBIDDEN on the three exports, DrillRun.customers named, and the addScopeItems
/// description of the one input restriction (Q-6). Everything else of 1.9.0 is equal as data.
/// </summary>
public sealed class RecallContract1100Tests
{
    private const string FrozenV190Sha256 = "7d97b13de27e662cc7f2775ca7fac3458d62bb749ff12e0d370fa768a47f930c";

    internal static string FixtureV190Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "recall-v1.9.0.openapi.yaml");

    private static readonly string[] PagedReads = ["getCaseCustomers", "getCaseScope", "getCaseScopeDiff", "getCaseReturns", "getCaseEffectiveness", "runTrace"];

    private static readonly string[] RecordCommands = ["recordNotification", "declareConsumption", "linkReturn"];

    private static readonly string[] Exports = ["exportTrace", "previewEvidence", "createEvidenceExport"];

    private const string PageRef = "#/components/parameters/CustomerPage";

    private const string DisplayRef = "#/components/schemas/CustomerDisplay";

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node is null ? [] : node.AsArray().Select(v => v!.GetValue<string>()).ToList();

    private static Dictionary<string, JsonObject> Operations(JsonObject doc)
        => doc["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject().Where(m => m.Value is JsonObject o && o["operationId"] is not null))
            .ToDictionary(m => m.Value!["operationId"]!.GetValue<string>(), m => m.Value!.AsObject());

    [Fact]
    public void The_recall_v1_9_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenV190Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            File.ReadAllText(FixtureV190Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_10_0_change_is_additive_plus_one_input_restriction_and_nothing_else()
    {
        var before = Load(FixtureV190Path);
        var after = Load(ContractSchemas.RecallContractPath);
        Assert.Equal(("1.9.0", "1.10.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.StartsWith("1.10.0 — additive + one input restriction (MOD-0177-FU01", newLog[0]);
        Assert.All(new[] { "addScopeItems", "RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE", "RECALL_CUSTOMER_DIRECTORY_FORBIDDEN", "customerPage", "DrillRun.customers" },
            t => Assert.Contains(t, newLog[0], StringComparison.Ordinal));
        Assert.Equal(Strings(before["info"]!["x-changelog"]), newLog.Skip(1));

        var schemas = after["components"]!["schemas"]!;
        var display = schemas["CustomerDisplay"]!;
        Assert.Equal(["source", "state"], Strings(display["required"]));
        Assert.Equal(["Crm", "ShipTo", "Document", "Manual", "Unknown"], Strings(display["properties"]!["source"]!["enum"]));
        Assert.Equal(["Resolved", "Unresolved", "NotFound", "Forbidden", "Deferred", "Legacy", "NoCrm"], Strings(display["properties"]!["state"]!["enum"]));
        Assert.Equal("boolean", display["properties"]!["noCrmWarning"]!["type"]!.GetValue<string>());
        foreach (var carrier in new[] { "CustomerRow", "EffectivenessRow", "ReturnLink", "ScopeItem", "TraceNode" })
        {
            Assert.Equal(DisplayRef, schemas[carrier]!["properties"]!["customer"]!["$ref"]!.GetValue<string>());
            Assert.DoesNotContain("customer", Strings(schemas[carrier]!["required"]));
        }

        Assert.Equal("array", schemas["DrillRun"]!["properties"]!["customers"]!["type"]!.GetValue<string>());
        Assert.Equal("integer", after["components"]!["parameters"]!["CustomerPage"]!["schema"]!["type"]!.GetValue<string>());
        Assert.NotNull(after["components"]!["responses"]!["ServiceUnavailable"]);
        Assert.Equal(("503", "403"), (after["x-error-codes"]!["RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE"]!.ToString(), after["x-error-codes"]!["RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"]!.ToString()));

        var ops = Operations(after);
        foreach (var read in PagedReads)
        {
            Assert.Contains(ops[read]["parameters"]!.AsArray(), p => p!["$ref"]?.GetValue<string>() == PageRef);
            Assert.Contains("INVALID_REQUEST", Strings(ops[read]["x-error-codes"]));
            Assert.NotNull(ops[read]["responses"]!["400"]);
        }

        foreach (var command in RecordCommands)
        {
            var codes = Strings(ops[command]["x-error-codes"]);
            Assert.Contains(RecallErrorCodes.CustomerDirectoryUnavailable, codes);
            Assert.Contains(RecallErrorCodes.CustomerDirectoryForbidden, codes);
            Assert.Equal("#/components/responses/ServiceUnavailable", ops[command]["responses"]!["503"]!["$ref"]!.GetValue<string>());
        }

        foreach (var export in Exports)
        {
            var codes = Strings(ops[export]["x-error-codes"]);
            Assert.Contains(RecallErrorCodes.CustomerDirectoryForbidden, codes);
            Assert.DoesNotContain(RecallErrorCodes.CustomerDirectoryUnavailable, codes);
            var text = ops[export]["description"]!.GetValue<string>();
            Assert.Contains("403 RECALL_CUSTOMER_DIRECTORY_FORBIDDEN when the caller may not read a CRM customer of the subject (CRM 401 / 403) — no file, no log row, no audit row", text);
            Assert.Contains("wave of 8", text, StringComparison.Ordinal); // the breaker sentence
        }

        Assert.Contains("one input restriction", ops["addScopeItems"]["description"]!.GetValue<string>(), StringComparison.Ordinal);

        // remove exactly the declared changes; the rest must equal 1.9.0 as data
        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
        }

        var a = after["components"]!.AsObject();
        Assert.True(a["schemas"]!.AsObject().Remove("CustomerDisplay"));
        Assert.True(a["parameters"]!.AsObject().Remove("CustomerPage"));
        Assert.True(a["responses"]!.AsObject().Remove("ServiceUnavailable"));
        foreach (var carrier in new[] { "CustomerRow", "EffectivenessRow", "ReturnLink", "ScopeItem", "TraceNode" })
        {
            Assert.True(a["schemas"]![carrier]!["properties"]!.AsObject().Remove("customer"), carrier);
        }

        Assert.True(a["schemas"]!["CustomerRow"]!["properties"]!["label"]!.AsObject().Remove("description"));
        Assert.True(a["schemas"]!["DrillRun"]!["properties"]!.AsObject().Remove("customers"));
        var codesRoot = after["x-error-codes"]!.AsObject();
        Assert.True(codesRoot.Remove(RecallErrorCodes.CustomerDirectoryUnavailable));
        Assert.True(codesRoot.Remove(RecallErrorCodes.CustomerDirectoryForbidden));
        var beforeOps = Operations(before);
        foreach (var read in PagedReads)
        {
            var op = ops[read];
            var parameters = op["parameters"]!.AsArray();
            parameters.Remove(parameters.Single(p => p!["$ref"]?.GetValue<string>() == PageRef));
            if (!Strings(beforeOps[read]["x-error-codes"]).Contains("INVALID_REQUEST"))
            {
                Remove(op, "INVALID_REQUEST");
                Assert.True(op["responses"]!.AsObject().Remove("400"), read);
            }
        }

        foreach (var command in RecordCommands)
        {
            Remove(ops[command], RecallErrorCodes.CustomerDirectoryUnavailable);
            Remove(ops[command], RecallErrorCodes.CustomerDirectoryForbidden);
            Assert.True(ops[command]["responses"]!.AsObject().Remove("503"));
            Assert.True(ops[command].Remove("description"), command);
        }

        foreach (var export in Exports)
        {
            Remove(ops[export], RecallErrorCodes.CustomerDirectoryForbidden);
            Assert.True(ops[export].Remove("description"), export);
        }

        Assert.True(ops["addScopeItems"].Remove("description"));
        Assert.True(JsonNode.DeepEquals(before, after), "recall.openapi.yaml 1.10.0 changes more than the declared additions and the one input restriction.");
    }

    private static void Remove(JsonObject op, string code)
    {
        var codes = op["x-error-codes"]!.AsArray();
        codes.Remove(codes.Single(c => c!.GetValue<string>() == code));
    }
}
