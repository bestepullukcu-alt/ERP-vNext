using System.Net;
using Xunit;

namespace Diten.SupplyChainService.Tests.Loads;

public sealed class LoadReplayTests
{
    [Fact]
    public async Task RootPrecedesFingerprintAndReplayUsesOriginalReceiptWithoutDependencyReads()
    {
        await using var f = new LoadTestContext();
        var body = f.CreateBody();
        var first = await f.Send("POST", "/api/shipment-bundle/loads", body, "root-key");
        Assert.Equal(HttpStatusCode.Created, first.Response.StatusCode);
        var requestCount = f.Calls.Values.Sum();
        var differentRoot = Guid.NewGuid().ToString();
        var rootConflict = await f.Send("POST", "/api/shipment-bundle/loads", body, "root-key", differentRoot);
        Assert.Equal(HttpStatusCode.Conflict, rootConflict.Response.StatusCode);
        Assert.Equal("CORRELATION_ROOT_MISMATCH", rootConflict.Body!["error"]!["code"]!.GetValue<string>());
        var changed = f.CreateBody(); changed["mode"] = "Air";
        var bothDiffer = await f.Send("POST", "/api/shipment-bundle/loads", changed, "root-key", differentRoot);
        Assert.Equal("CORRELATION_ROOT_MISMATCH", bothDiffer.Body!["error"]!["code"]!.GetValue<string>());
        var replay = await f.Send("POST", "/api/shipment-bundle/loads", body, "root-key");
        Assert.Equal(HttpStatusCode.Created, replay.Response.StatusCode);
        Assert.True(replay.Body!["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal(first.Body!["loadId"]!.GetValue<string>(), replay.Body["loadId"]!.GetValue<string>());
        Assert.Equal(requestCount, f.Calls.Values.Sum());
        Assert.Equal(1, await f.Count("loads_audit"));
        Assert.Equal(1, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task HistoricalCreateReplayAfterCancellationDoesNotReacquireAssignment()
    {
        await using var f = new LoadTestContext();
        var body = f.CreateBody();
        var create = await f.Send("POST", "/api/shipment-bundle/loads", body, "old-create");
        var id = create.Body!["loadId"]!.GetValue<string>();
        var cancel = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody("Cancelled"), "cancel");
        Assert.Equal(HttpStatusCode.OK, cancel.Response.StatusCode);
        Assert.Equal(0, await f.Count("load_assignments"));
        var calls = f.Calls.Values.Sum();
        var replay = await f.Send("POST", "/api/shipment-bundle/loads", body, "old-create");
        Assert.Equal(HttpStatusCode.Created, replay.Response.StatusCode);
        Assert.True(replay.Body!["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal("Draft", replay.Body["status"]!.GetValue<string>());
        Assert.Equal(0, await f.Count("load_assignments"));
        Assert.Equal(calls, f.Calls.Values.Sum());
        Assert.Equal(2, await f.Count("loads_audit"));
        Assert.Equal(2, await f.Count("loads_outbox"));
    }

    [Fact]
    public async Task TransitionReplayReturnsHistoricalStatusAndNoteNullNormalizationIsExact()
    {
        await using var f = new LoadTestContext();
        var create = await f.Send("POST", "/api/shipment-bundle/loads", f.CreateBody(), "create");
        var id = create.Body!["loadId"]!.GetValue<string>();
        var firstBody = f.TransitionBody("Planned");
        var first = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", firstBody, "planned");
        Assert.Equal("Planned", first.Body!["status"]!.GetValue<string>());
        var next = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", f.TransitionBody("Tendered"), "tendered");
        Assert.Equal("Tendered", next.Body!["status"]!.GetValue<string>());
        var calls = f.Calls.Values.Sum();
        var replay = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", firstBody, "planned");
        Assert.Equal("Planned", replay.Body!["status"]!.GetValue<string>());
        Assert.True(replay.Body["idempotentReplay"]!.GetValue<bool>());
        Assert.Equal(calls, f.Calls.Values.Sum());
        Assert.Equal(3, await f.Count("loads_audit"));
        Assert.Equal(3, await f.Count("loads_outbox"));

        var omitted = f.TransitionBody("Accepted");
        var accepted = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", omitted, "accepted");
        Assert.Equal(HttpStatusCode.OK, accepted.Response.StatusCode);
        var explicitNull = f.TransitionBody("Accepted", null, includeNote: true);
        var nullReplay = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", explicitNull, "accepted");
        Assert.True(nullReplay.Body!["idempotentReplay"]!.GetValue<bool>());
        var empty = f.TransitionBody("Accepted", "", includeNote: true);
        var emptyConflict = await f.Send("POST", $"/api/shipment-bundle/loads/{id}/transition", empty, "accepted");
        Assert.Equal(HttpStatusCode.Conflict, emptyConflict.Response.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", emptyConflict.Body!["error"]!["code"]!.GetValue<string>());
    }
}
