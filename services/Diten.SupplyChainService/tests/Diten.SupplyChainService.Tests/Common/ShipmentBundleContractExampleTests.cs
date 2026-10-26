using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// Every example the frozen shipment-bundle contract publishes (request bodies and responses, `example` and `examples`)
/// validates against its own schema — the "contract examples pass" half of the §16 contract-fidelity criteria
/// (0183 #10, 0184 #2, 0185 #3, 0187 #2). The live-response half is in each module's HTTP tests.
/// </summary>
public sealed class ShipmentBundleContractExampleTests(ITestOutputHelper output)
{
    [Fact]
    public void Every_published_example_validates_against_its_schema()
    {
        var checkedCount = 0;
        var errors = new List<string>();
        foreach (var (path, item) in (JsonObject)ShipmentBundleContract.Root["paths"]!)
        foreach (var (method, operationNode) in (JsonObject)item!)
        {
            if (operationNode is not JsonObject operation) continue;
            var places = new List<(string Where, JsonObject Container)>();
            if (operation["requestBody"] is JsonObject request) places.Add(($"{method} {path} request", Deref(request)));
            if (operation["responses"] is JsonObject responses)
                foreach (var (status, response) in responses) places.Add(($"{method} {path} {status}", Deref((JsonObject)response!)));
            foreach (var (where, container) in places)
            {
                var media = container["content"]?["application/json"] as JsonObject;
                if (media?["schema"] is not JsonNode schema) continue;
                var examples = new List<(string, JsonNode?)>();
                if (media.ContainsKey("example")) examples.Add(("example", media["example"]));
                if (media["examples"] is JsonObject named)
                    foreach (var (name, ex) in named)
                    {
                        var e = ex is JsonObject o && o["$ref"] is JsonValue r ? ShipmentBundleContract.Pointer(r.GetValue<string>()) : ex;
                        examples.Add((name, (e as JsonObject)?["value"]));
                    }
                foreach (var (name, value) in examples)
                {
                    checkedCount++;
                    foreach (var error in ShipmentBundleContract.Validate(schema, value?.DeepClone())) errors.Add($"{where} [{name}] {error}");
                }
            }
        }
        output.WriteLine($"examples checked: {checkedCount}");
        foreach (var e in errors) output.WriteLine(e);
        Assert.True(checkedCount > 50, $"only {checkedCount} examples found — the walk is not reading the contract");
        Assert.Empty(errors);
    }

    // The validator is not vacuous: a published example, broken one way at a time, is refused.
    [Fact]
    public void The_validator_refuses_a_broken_shipment_detail()
    {
        var example = ShipmentBundleContract.Root["paths"]!["/shipments/{shipmentId}"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["example"]!;
        Assert.Empty(ShipmentBundleContract.ValidateResponse("/shipments/{shipmentId}", "GET", 200, example.DeepClone()));
        foreach (var (mutate, expected) in new (Action<JsonObject>, string)[]
        {
            // (ShipmentSummary publishes no required list, so removing shipmentId is contract-valid — recorded as a
            //  contract looseness in mvp6-pack16-evidence-audit-01; a required field of ShipmentDetail is used instead.)
            (o => o.Remove("lines"), "missing required 'lines'"),
            (o => o["shipmentId"] = "not-a-uuid", "is not a uuid"),
            (o => o["status"] = "Teleported", "not in enum"),
            (o => o["contractVersion"] = "v9", "const"),
            (o => o["lines"] = "x", "expected array"),
            (o => o["pod"] = "signed", "matches none of anyOf"),
            (o => o["lines"] = new JsonArray(new JsonObject { ["lineNumber"] = "1" }), "missing required")
        })
        {
            var broken = (JsonObject)example.DeepClone();
            mutate(broken);
            var errors = ShipmentBundleContract.ValidateResponse("/shipments/{shipmentId}", "GET", 200, broken);
            Assert.Contains(errors, e => e.Contains(expected));
        }
        Assert.NotEmpty(ShipmentBundleContract.ValidateResponse("/shipments/{shipmentId}", "GET", 418, example.DeepClone()));
    }

    private static JsonObject Deref(JsonObject node)
    {
        while (node["$ref"] is JsonValue r) node = (JsonObject)ShipmentBundleContract.Pointer(r.GetValue<string>());
        return node;
    }
}
