using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
namespace Diten.SupplyChainService.Tests.Loads;

/// <summary>
/// MOD-0185 §16 (evidence audit 2026-10-19): "Correlation UUID preserved through all load events" asserted 2 of 7 event
/// types. Here every arrow runs (create → planned → tendered → accepted → dispatched → completed, and create → cancelled)
/// and every outbox envelope carries the request root, across all seven event types.
/// </summary>
public sealed class LoadEventCorrelationTests
{
    // MOD-0185 §16 #3 (contract fidelity): live answers — success and error — validate against the frozen OpenAPI.
    [Fact]
    public async Task Live_responses_match_the_frozen_contract()
    {
        await using var f = new LoadTestContext();
        var errors = new List<string>();
        void Check(string path, string method, (HttpResponseMessage Response, System.Text.Json.Nodes.JsonNode? Body) r) =>
            errors.AddRange(Diten.SupplyChainService.Tests.Common.ShipmentBundleContract.ValidateResponse(path, method, (int)r.Response.StatusCode, r.Body).Select(e => $"{method} {path} {(int)r.Response.StatusCode}: {e}"));
        var created = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(Guid.NewGuid()), "contract-create"); Check("/loads", "POST", created);
        var id = created.Body!["loadId"]!.GetValue<string>();
        Check("/loads", "GET", await f.Send("GET", "/api/shipment-bundle/loads"));
        Check("/loads/{loadId}/transition", "POST", await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody("Planned"), "contract-planned"));
        Check("/loads/{loadId}/transition", "POST", await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody("Completed"), "contract-422"));
        Check("/loads/{loadId}/transition", "POST", await f.Send("POST", $"/api/shipment-bundle/loads/{Guid.NewGuid()}/transition", f.TransitionBody("Planned"), "contract-404"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Every_load_event_type_carries_the_root()
    {
        var paths = new[]
        {
            new[] { "Planned", "Tendered", "Accepted", "Dispatched", "Completed" },
            new[] { "Cancelled" }
        };
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            await using var f = new LoadTestContext();
            var created = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(Guid.NewGuid()), "create");
            Assert.Equal(201, (int)created.Response.StatusCode);
            var id = Guid.Parse(created.Body!["loadId"]!.GetValue<string>());
            foreach (var target in path)
                Assert.Equal(200, (int)(await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody(target), "to-" + target)).Response.StatusCode);
            var outbox = await f.Db.GetCollection<BsonDocument>("loads_outbox").Find(f.Scope()).ToListAsync();
            Assert.Equal(path.Length + 1, outbox.Count);
            foreach (var row in outbox)
            {
                Assert.Equal(f.Root, row["Envelope"]["correlationId"].AsString);
                types.Add(row["Envelope"]["eventType"].AsString);
                Assert.Empty(EnvelopeErrors(row["Envelope"].AsBsonDocument));
            }
        }
        Assert.Equal(new[] { "LoadAccepted", "LoadCancelled", "LoadCompleted", "LoadCreated", "LoadDispatched", "LoadPlanned", "LoadTendered" },
            types.Order(StringComparer.Ordinal).ToArray());
    }

    // Contract fidelity for events: each stored envelope validates against the frozen LifecycleEventEnvelope schema.
    private static IEnumerable<string> EnvelopeErrors(BsonDocument envelope) =>
        Diten.SupplyChainService.Tests.Common.ShipmentBundleContract.Validate(
            Diten.SupplyChainService.Tests.Common.ShipmentBundleContract.Pointer("#/components/schemas/LifecycleEventEnvelope"),
            System.Text.Json.Nodes.JsonNode.Parse(envelope.ToJson(new MongoDB.Bson.IO.JsonWriterSettings { OutputMode = MongoDB.Bson.IO.JsonOutputMode.RelaxedExtendedJson })));

}
