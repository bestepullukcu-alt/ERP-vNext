using System.Net;
using MongoDB.Driver;
using Xunit;

namespace Diten.SupplyChainService.Tests.Loads;

public sealed class LoadReferenceTests
{
    [Fact]
    public async Task ReferencesAreGetOnlyAndCarryTrustedScopeAndCorrelation()
    {
        await using var f = new LoadTestContext();
        var response = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "reference-key");
        Assert.Equal(HttpStatusCode.Created, response.Response.StatusCode);
        var calls = f.MockCalls.ToArray();
        Assert.Equal(2, calls.Length);
        Assert.All(calls, call =>
        {
            Assert.Equal("GET", call.Method);
            Assert.Equal(f.Tenant.ToString(), call.Tenant);
            Assert.Equal(f.LegalEntity.ToString(), call.LegalEntity);
            Assert.Equal(f.Root, call.Correlation);
            Assert.True(call.HasAuthorization);
        });
        Assert.Contains(calls, call => call.Path == "/api/shipment-bundle/carriers?status=Active");
        Assert.Contains(calls, call => call.Path == $"/api/shipment-bundle/shipments/{f.ShipmentId}");
    }

    [Fact]
    public async Task CarrierAndShipmentReferenceProfilesFailClosedOnMissingOrContradictoryData()
    {
        await using var f = new LoadTestContext();
        f.CarrierResponse = new { items = new[] { new { carrierId = f.CarrierId, carrierCode = "C-1", displayName = "Carrier", status = "Active" } }, total = 1, contractVersion = "v1" };
        var malformedCarrier = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "bad-carrier");
        Assert.Equal(HttpStatusCode.BadGateway, malformedCarrier.Response.StatusCode);
        Assert.Equal("DEPENDENCY_RESPONSE_INVALID", malformedCarrier.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, await f.Count("loads_receipts"));

        f.CarrierResponse = LoadTestContext.DefaultCarrierResponse(f.CarrierId);
        f.ShipmentResponse = id => new { shipmentId = id, status = "Draft", loadId = (Guid?)null,
            sourceModule = "MOD-0183", sourceType = "Shipment", warehouseReferenceId = "wh-1", shipToReference = "dest-1", lines = Array.Empty<object>(), contractVersion = "v1" };
        var insufficientShipment = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "missing-source-fields");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, insufficientShipment.Response.StatusCode);
        Assert.Equal("REFERENCE_STATE_UNAVAILABLE", insufficientShipment.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, await f.Count("loads_receipts"));

        f.ShipmentResponse = id => LoadTestContext.DefaultShipmentResponse(Guid.NewGuid());
        var wrongId = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "wrong-id");
        Assert.Equal(HttpStatusCode.BadGateway, wrongId.Response.StatusCode);
        Assert.Equal("DEPENDENCY_RESPONSE_INVALID", wrongId.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, await f.Count("loads_receipts"));
    }

    [Fact]
    public async Task BusinessReferenceOutcomesMatchApprovedCarrierAndShipmentRules()
    {
        await using var f = new LoadTestContext();
        f.CarrierResponse = LoadTestContext.DefaultCarrierResponse(Guid.NewGuid());
        var absentCarrier = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "absent-carrier");
        Assert.Equal(HttpStatusCode.NotFound, absentCarrier.Response.StatusCode);
        Assert.Equal("CARRIER_NOT_FOUND", absentCarrier.Body!["error"]!["code"]!.GetValue<string>());

        f.CarrierResponse = LoadTestContext.DefaultCarrierResponse(f.CarrierId, modes: []);
        var unsupportedMode = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "unsupported-mode");
        Assert.Equal((HttpStatusCode)422, unsupportedMode.Response.StatusCode);
        Assert.Equal("CARRIER_MODE_UNSUPPORTED", unsupportedMode.Body!["error"]!["code"]!.GetValue<string>());

        f.CarrierResponse = LoadTestContext.DefaultCarrierResponse(f.CarrierId);
        f.ShipmentResponse = id => LoadTestContext.DefaultShipmentResponse(id, status: "Delivered");
        var ineligible = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "ineligible-shipment");
        Assert.Equal((HttpStatusCode)422, ineligible.Response.StatusCode);
        Assert.Equal("SHIPMENT_NOT_ELIGIBLE", ineligible.Body!["error"]!["code"]!.GetValue<string>());

        f.ShipmentResponse = id => LoadTestContext.DefaultShipmentResponse(id, carrierId: Guid.NewGuid());
        var carrierMismatch = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "shipment-carrier-mismatch");
        Assert.Equal((HttpStatusCode)422, carrierMismatch.Response.StatusCode);
        Assert.Equal("SHIPMENT_CARRIER_MISMATCH", carrierMismatch.Body!["error"]!["code"]!.GetValue<string>());

        f.ShipmentResponse = id => LoadTestContext.DefaultShipmentResponse(id, loadId: Guid.NewGuid());
        var alreadyAssigned = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "shipment-has-load");
        Assert.Equal(HttpStatusCode.Conflict, alreadyAssigned.Response.StatusCode);
        Assert.Equal("SHIPMENT_ALREADY_ASSIGNED", alreadyAssigned.Body!["error"]!["code"]!.GetValue<string>());

        f.ShipmentResponse = _ => null;
        var missingShipment = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "shipment-404");
        Assert.Equal(HttpStatusCode.NotFound, missingShipment.Response.StatusCode);
        Assert.Equal("SHIPMENT_NOT_FOUND", missingShipment.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, await f.Count("loads"));
        Assert.Equal(0, await f.Count("loads_receipts"));
    }

    [Fact]
    public async Task UpstreamAuthServerAndUnexpectedStatusesFailClosedAsSpecified()
    {
        await using var f = new LoadTestContext();
        foreach (var upstreamStatus in new[] { 401, 403, 500 })
        {
            f.CarrierStatusCode = upstreamStatus;
            var unavailable = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "carrier-upstream-" + upstreamStatus);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.Response.StatusCode);
            Assert.Equal("DEPENDENCY_UNAVAILABLE", unavailable.Body!["error"]!["code"]!.GetValue<string>());
        }
        f.CarrierStatusCode = 418;
        var invalid = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "carrier-upstream-unexpected");
        Assert.Equal(HttpStatusCode.BadGateway, invalid.Response.StatusCode);
        Assert.Equal("DEPENDENCY_RESPONSE_INVALID", invalid.Body!["error"]!["code"]!.GetValue<string>());
        Assert.Equal(0, await f.Count("loads_receipts"));
        Assert.Equal(0, await f.Count("loads_audit"));
        Assert.Equal(0, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task FreshRetryReobservesDependenciesButSuccessfulReplayDoesNot()
    {
        await using var f = new LoadTestContext();
        f.Probe.FailAt = "beforeCommit";
        var failed = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "retry-ref");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.Response.StatusCode);
        var callsAfterFailure = f.MockCalls.Count;
        Assert.Equal(2, callsAfterFailure);
        var success = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "retry-ref");
        Assert.Equal(HttpStatusCode.Created, success.Response.StatusCode);
        Assert.Equal(callsAfterFailure + 2, f.MockCalls.Count);
        var replay = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "retry-ref");
        Assert.True(replay.Body!["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal(callsAfterFailure + 2, f.MockCalls.Count);
    }

    [Fact]
    public async Task PendingOutboxAndHistoricalReplaySurviveApiRestart()
    {
        await using var f = new LoadTestContext();
        var created = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "durable-create");
        Assert.Equal(HttpStatusCode.Created, created.Response.StatusCode);
        var id = created.Body!["loadId"]!.GetValue<string>();
        f.RestartApi();
        var replay = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "durable-create");
        Assert.Equal(HttpStatusCode.Created, replay.Response.StatusCode);
        Assert.True(replay.Body!["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal(id, replay.Body["loadId"]!.GetValue<string>());
        Assert.Equal(1, await f.Count("loads"));
        Assert.Equal(1, await f.Count("loads_receipts"));
        Assert.Equal(1, await f.Count("loads_audit"));
        var events = await f.Db.GetCollection<MongoDB.Bson.BsonDocument>("loads_outbox").Find(f.Scope()).ToListAsync();
        Assert.Single(events);
        Assert.Equal("Pending", events[0]["Status"].AsString);
    }
}
