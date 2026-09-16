using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.BuildingBlocks.Eventing;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Persistence.Features.Shipments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.SupplyChainService.Tests;

public sealed class ShipmentTests(ITestOutputHelper output)
{
    private static readonly string[] AllPermissions = ["read", "create", "dispatch", "pod.capture", "cancel", "reconcile"];
    private const string TestSecret = "mod0183-test-signing-secret-not-for-production-123456";
    private const string Database = "diten_mod0183_tests";
    private static string Connection => Environment.GetEnvironmentVariable("MOD0183_TEST_MONGO")
     ?? throw new InvalidOperationException("MOD0183_TEST_MONGO must point to an isolated replica set.");
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _le = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _correlation = Guid.NewGuid();

    private sealed class Probe : IShipmentCommitProbe
    {
        public int Fail;
        public Task BeforeCommitAsync(CancellationToken ct)
        {
            if (Interlocked.Exchange(ref Fail, 0) == 1) throw new IOException("Injected failure before commit.");
            return Task.CompletedTask;
        }
    }
    private sealed class Factory(Probe probe) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = Connection,
                ["Mongo:DatabaseName"] = Database,
                ["JwtSettings:Secret"] = TestSecret,
                ["JwtSettings:Issuer"] = "mod0183-tests",
                ["JwtSettings:Audience"] = "mod0183-tests"
            }));
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = Connection,
                ["Mongo:DatabaseName"] = Database,
                ["JwtSettings:Secret"] = TestSecret,
                ["JwtSettings:Issuer"] = "mod0183-tests",
                ["JwtSettings:Audience"] = "mod0183-tests"
            }));
            builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IShipmentCommitProbe>(probe)));
        }
    }
    private static JsonObject CreateBody() => JsonNode.Parse("""
 {"sourceModule":"MOD-0141","sourceType":"SALES_ORDER","sourceDocumentId":"SO-900","warehouseReferenceId":"wh-01",
 "shipToReference":"CUST-100/ADDR-2","plannedShipAt":"2026-09-20T08:00:00Z","plannedDeliverAt":"2026-09-21T16:00:00Z",
 "lines":[{"lineNumber":"1","itemId":"b1f2c3d4-0000-0000-0000-000000000001","skuId":"c3d4e5f6-0000-0000-0000-000000000002",
 "quantity":"30.000","uomId":"EA","inventoryReferenceId":"rsv-4001"}]}
 """)!.AsObject();
    private static JsonObject Transition(string status) => new() { ["targetStatus"] = status, ["occurredAt"] = "2026-09-20T08:15:00Z" };
    private static JsonObject Pod() => new()
    {
        ["recipientName"] = "Test Recipient",
        ["receivedAt"] = "2026-09-21T15:42:00Z",
        ["evidenceReferenceIds"] = new JsonArray("test-evidence"),
        ["note"] = "Test only"
    };
    private string Token(Guid tenant, Guid le, string[]? permissions = null, bool omitLe = false)
    {
        var claims = new List<Claim> { new("sub", _actor.ToString()), new("tenant_id", tenant.ToString()), new("actor_type", "tenant_user") };
        if (!omitLe) claims.Add(new("legal_entity_id", le.ToString()));
        claims.AddRange((permissions ?? AllPermissions).Select(p => new Claim("permission", "supplychain.shipments." + p)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("mod0183-tests", "mod0183-tests", claims,
         expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret)), SecurityAlgorithms.HmacSha256)));
    }
    private async Task<(int Status, JsonObject Body)> Send(HttpClient client, string method, string path, JsonObject? body = null, string? key = null,
     Guid? tenant = null, Guid? le = null, string[]? permissions = null, string? correlation = null, bool omitCorrelation = false, bool omitLeClaim = false)
    {
        var t = tenant ?? _tenant; var l = le ?? _le;
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/shipment-bundle/shipments" + path);
        request.Headers.Add("Authorization", "Bearer " + Token(t, l, permissions, omitLeClaim));
        request.Headers.Add("X-Tenant-Id", t.ToString()); request.Headers.Add("X-Legal-Entity-Id", l.ToString());
        if (!omitCorrelation) request.Headers.Add("X-Correlation-Id", correlation ?? _correlation.ToString());
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!.AsObject();
        output.WriteLine($"{method} {path} => {(int)response.StatusCode} {text}");
        return ((int)response.StatusCode, parsed);
    }
    private static IMongoDatabase Db(Factory app) => app.Services.GetRequiredService<IMongoDatabase>();
    private FilterDefinition<BsonDocument> Scope() => new BsonDocument { { "TenantId", _tenant.ToString() }, { "LegalEntityId", _le.ToString() } };
    private async Task<long> Count(Factory app, string name) => await Db(app).GetCollection<BsonDocument>(name).CountDocumentsAsync(Scope());
    private async Task<Guid> Create(Factory app, HttpClient c, string key = "create")
    {
        var r = await Send(c, "POST", "", CreateBody(), key); Assert.Equal(201, r.Status); return Guid.Parse(r.Body["shipmentId"]!.GetValue<string>());
    }
    [Fact]
    public async Task GoldenFlow_Replay_Outbox_AndRestartAreDurable()
    {
        Guid id;
        var probe = new Probe();
        using (var app = new Factory(probe))
        using (var client = app.CreateClient())
        {
            id = await Create(app, client);
            Assert.Equal("Draft", (await Send(client, "GET", $"/{id}")).Body["status"]!.GetValue<string>());
            Assert.Equal(200, (await Send(client, "POST", $"/{id}/transition", Transition("Planned"), "plan")).Status);
            Assert.Equal(200, (await Send(client, "POST", $"/{id}/transition", Transition("Dispatched"), "dispatch")).Status);
            Assert.Equal(201, (await Send(client, "POST", $"/{id}/pod", Pod(), "pod")).Status);
            Assert.Equal(201, (await Send(client, "POST", $"/{id}/pod", Pod(), "pod")).Status);
            Assert.Equal(409, (await Send(client, "POST", $"/{id}/pod", Pod(), "second-pod")).Status);
            Assert.Equal(200, (await Send(client, "POST", $"/{id}/transition", Transition("Closed"), "close")).Status);
            var detail = (await Send(client, "GET", $"/{id}")).Body;
            Assert.Equal("Closed", detail["status"]!.GetValue<string>()); Assert.NotNull(detail["pod"]);
            Assert.Equal(5, await Count(app, "sce_shipment_history")); Assert.Equal(5, await Count(app, "sce_shipment_audit"));
            Assert.Equal(5, await Count(app, "sce_shipment_receipts")); Assert.Equal(6, await Count(app, "sce_shipment_outbox"));
        }
        using (var restarted = new Factory(probe))
        using (var client = restarted.CreateClient())
        {
            var detail = await Send(client, "GET", $"/{id}"); Assert.Equal(200, detail.Status); Assert.Equal("Closed", detail.Body["status"]!.GetValue<string>());
            var replay = await Send(client, "POST", "", CreateBody(), "create"); Assert.Equal(200, replay.Status);
            Assert.Equal(id.ToString(), replay.Body["shipmentId"]!.GetValue<string>()); Assert.True(replay.Body["idempotentReplay"]!.GetValue<bool>());
            Assert.Equal("Draft", replay.Body["status"]!.GetValue<string>());
            Assert.Equal(5, await Count(restarted, "sce_shipment_history")); Assert.Equal(6, await Count(restarted, "sce_shipment_outbox"));
            var rows = await Db(restarted).GetCollection<BsonDocument>("sce_shipment_outbox").Find(Scope()).ToListAsync();
            Assert.All(rows, r => Assert.Equal(_correlation.ToString(), r["CorrelationId"].AsString));
            var audit = await Db(restarted).GetCollection<BsonDocument>("sce_shipment_audit").Find(Scope()).ToListAsync();
            Assert.All(audit, r => { Assert.Equal(_actor.ToString(), r["ActorId"].AsString); Assert.False(r.Contains("Note")); });
            output.WriteLine($"Restart persisted counts: history=5 audit=5 receipts=5 outbox=6; scope={_tenant}/{_le}");
        }
    }
    [Fact]
    public async Task Scope_Rbac_Schema_AndCorrelationFailClosed()
    {
        using var app = new Factory(new()); using var c = app.CreateClient(); var id = await Create(app, c);
        foreach (var path in new[] { "/" + id, "/" + Guid.NewGuid() })
        {
            Assert.Equal(404, (await Send(c, "GET", path, tenant: Guid.NewGuid())).Status);
            Assert.Equal(404, (await Send(c, "GET", path, le: Guid.NewGuid())).Status);
        }
        Assert.Equal(404, (await Send(c, "GET", "/" + Guid.NewGuid())).Status);
        Assert.Equal(403, (await Send(c, "GET", "/" + id, permissions: [])).Status);
        Assert.Equal(403, (await Send(c, "POST", "", CreateBody(), "forbidden", permissions: [])).Status);
        Assert.Equal(403, (await Send(c, "GET", "/" + id, omitLeClaim: true)).Status);
        Assert.Equal(400, (await Send(c, "GET", "/" + id, omitCorrelation: true)).Status);
        Assert.Equal(400, (await Send(c, "GET", "/" + id, correlation: "not-a-uuid")).Status);
        Assert.Equal(400, (await Send(c, "POST", $"/{id}/transition", Transition("Planned"), "other-root", correlation: Guid.NewGuid().ToString())).Status);
        var bad = CreateBody(); bad["tenantId"] = _tenant.ToString(); Assert.Equal(400, (await Send(c, "POST", "", bad, "scope-body")).Status);
        bad = CreateBody(); bad.Remove("plannedShipAt"); Assert.Equal(400, (await Send(c, "POST", "", bad, "missing")).Status);
        bad = CreateBody(); bad["lines"]![0]!["quantity"] = "1e3"; Assert.Equal(400, (await Send(c, "POST", "", bad, "quantity")).Status);
        Assert.Equal(400, (await Send(c, "POST", "", CreateBody(), new string('x', 129))).Status);
        Assert.Equal(400, (await Send(c, "POST", "", CreateBody())).Status);
        Assert.Equal(400, (await Send(c, "GET", "?pageSize=201")).Status);
        Assert.Equal(1, await Count(app, "sce_shipment_history"));
        await Db(app).GetCollection<BsonDocument>("sce_shipments").UpdateOneAsync(Scope() & new BsonDocument("_id", id.ToString()), new BsonDocument("$set", new BsonDocument { { "IsDeleted", true }, { "DeletedAt", DateTimeOffset.UtcNow.ToString("O") } }));
        Assert.Equal(404, (await Send(c, "GET", "/" + id)).Status);
        Assert.Equal(0, (await Send(c, "GET", "")).Body["total"]!.GetValue<int>());
        Assert.Equal(404, (await Send(c, "POST", $"/{id}/transition", Transition("Planned"), "hidden")).Status);
        Assert.Equal(404, (await Send(c, "POST", "", CreateBody(), "create")).Status);
    }
    [Fact]
    public async Task ReplayConflict_AndConcurrentCreateOrTransitionProduceOneWinner()
    {
        using var app = new Factory(new()); using var c = app.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Send(c, "POST", "", CreateBody(), "race-create")));
        Assert.Single(responses, x => x.Status == 201); Assert.Equal(7, responses.Count(x => x.Status == 200));
        var id = Guid.Parse(responses[0].Body["shipmentId"]!.GetValue<string>());
        Assert.Single(responses.Select(r => r.Body["shipmentId"]!.GetValue<string>()).Distinct());
        var changed = CreateBody(); changed["shipToReference"] = "other";
        Assert.Equal(409, (await Send(c, "POST", "", changed, "race-create")).Status);
        Assert.Equal(200, (await Send(c, "POST", $"/{id}/transition", Transition("Planned"), "planned")).Status);
        var race = await Task.WhenAll(Send(c, "POST", $"/{id}/transition", Transition("Dispatched"), "dispatch-a"),
         Send(c, "POST", $"/{id}/transition", Transition("Dispatched"), "dispatch-b"));
        Assert.Equal(new[] { 200, 422 }, race.Select(x => x.Status).Order().ToArray());
        Assert.Equal(1, await Count(app, "sce_shipments")); Assert.Equal(3, await Count(app, "sce_shipment_history")); Assert.Equal(3, await Count(app, "sce_shipment_outbox"));
        var otherLe = await Send(c, "POST", "", CreateBody(), "race-create", le: Guid.NewGuid()); Assert.Equal(201, otherLe.Status);
        Assert.NotEqual(id.ToString(), otherLe.Body["shipmentId"]!.GetValue<string>());
    }
    [Fact]
    public async Task FailureBeforeCommitRollsBackEveryRecordAndRetrySucceeds()
    {
        var probe = new Probe { Fail = 1 }; using var app = new Factory(probe); using var c = app.CreateClient();
        Assert.Equal(500, (await Send(c, "POST", "", CreateBody(), "fault")).Status);
        foreach (var collection in new[] { "sce_shipments", "sce_shipment_history", "sce_shipment_receipts", "sce_shipment_audit", "sce_shipment_outbox" })
            Assert.Equal(0, await Count(app, collection));
        Assert.Equal(201, (await Send(c, "POST", "", CreateBody(), "fault")).Status);
        Assert.Equal(200, (await Send(c, "POST", "", CreateBody(), "fault")).Status);
        Assert.Equal(1, await Count(app, "sce_shipment_audit"));
    }
    private static bool Expected(string from, string to) => from switch
    {
        "Draft" => to is "Planned" or "Cancelled",
        "Planned" => to is "Dispatched" or "Cancelled",
        "Dispatched" => to is "InTransit" or "Delivered" or "Exception",
        "InTransit" => to is "Delivered" or "Exception",
        "Exception" => to is "InTransit" or "Cancelled",
        "Delivered" => to == "Closed",
        _ => false
    };
    [Fact]
    public async Task EveryForbiddenTransitionLeavesPersistenceUnchanged()
    {
        using var app = new Factory(new()); using var c = app.CreateClient(); var id = await Create(app, c); var tested = 0;
        foreach (var from in Enum.GetNames<ShipmentStatus>())
            foreach (var to in Enum.GetNames<ShipmentStatus>())
            {
                Assert.Equal(Expected(from, to), ShipmentLifecycle.Allows(Enum.Parse<ShipmentStatus>(from), Enum.Parse<ShipmentStatus>(to)));
                if (Expected(from, to)) continue;
                await Db(app).GetCollection<BsonDocument>("sce_shipments").UpdateOneAsync(Scope() & new BsonDocument("_id", id.ToString()), new BsonDocument("$set", new BsonDocument("Status", from)));
                Assert.Equal(422, (await Send(c, "POST", $"/{id}/transition", Transition(to), from + "-" + to)).Status);
                Assert.Equal(from, (await Send(c, "GET", "/" + id)).Body["status"]!.GetValue<string>());
                Assert.Equal(1, await Count(app, "sce_shipment_history")); Assert.Equal(1, await Count(app, "sce_shipment_outbox")); tested++;
            }
        output.WriteLine($"Forbidden transitions checked through HTTP and Mongo: {tested}");
    }
    [Fact]
    public async Task CancelPermissionIsRequiredAndCannotDispatch()
    {
        using var app = new Factory(new()); using var c = app.CreateClient(); var id = await Create(app, c);
        Assert.Equal(403, (await Send(c, "POST", $"/{id}/transition", Transition("Cancelled"), "cancel", permissions: ["dispatch"])).Status);
        Assert.Equal(403, (await Send(c, "POST", $"/{id}/transition", Transition("Planned"), "plan", permissions: ["cancel"])).Status);
        Assert.Equal(200, (await Send(c, "POST", $"/{id}/transition", Transition("Cancelled"), "cancel", permissions: ["cancel"])).Status);
        Assert.Equal(2, await Count(app, "sce_shipment_history"));
    }

    [Fact]
    public async Task AllowedBranchesAndPodFailureHaveAtomicHistory()
    {
        var probe = new Probe(); using var app = new Factory(probe); using var c = app.CreateClient();
        var id = await Create(app, c);
        foreach (var status in new[] { "Planned", "Dispatched", "Exception", "InTransit", "Exception", "Cancelled" })
            Assert.Equal(200, (await Send(c, "POST", $"/{id}/transition", Transition(status), Guid.NewGuid().ToString())).Status);
        Assert.Equal(7, await Count(app, "sce_shipment_history"));
        var id2 = await Create(app, c, "pod-failure-create");
        foreach (var status in new[] { "Planned", "Dispatched" })
            Assert.Equal(200, (await Send(c, "POST", $"/{id2}/transition", Transition(status), Guid.NewGuid().ToString())).Status);
        var before = await Count(app, "sce_shipment_outbox");
        probe.Fail = 1;
        Assert.Equal(500, (await Send(c, "POST", $"/{id2}/pod", Pod(), "pod-failure")).Status);
        var current = (await Send(c, "GET", $"/{id2}")).Body;
        Assert.Equal("Dispatched", current["status"]!.GetValue<string>()); Assert.Null(current["pod"]);
        Assert.Equal(before, await Count(app, "sce_shipment_outbox"));
        var race = await Task.WhenAll(Send(c, "POST", $"/{id2}/pod", Pod(), "pod-failure"), Send(c, "POST", $"/{id2}/pod", Pod(), "pod-race"));
        Assert.Single(race, r => r.Status == 201); Assert.Single(race, r => r.Status == 409);
        Assert.Equal(before + 2, await Count(app, "sce_shipment_outbox"));
    }
    [Fact]
    public async Task NormalizedReplayAndCrossScopeListRemainIsolated()
    {
        using var app = new Factory(new()); using var c = app.CreateClient(); var id = await Create(app, c);
        var same = CreateBody(); same["shipToReference"] = "  CUST-100/ADDR-2  ";
        Assert.Equal(200, (await Send(c, "POST", "", same, "create")).Status);
        Assert.Equal(1, await Count(app, "sce_shipment_receipts"));
        Assert.Equal(0, (await Send(c, "GET", "", tenant: Guid.NewGuid())).Body["total"]!.GetValue<int>());
        Assert.Equal(0, (await Send(c, "GET", "", le: Guid.NewGuid())).Body["total"]!.GetValue<int>());
        using var options = new HttpRequestMessage(HttpMethod.Options, "/api/shipment-bundle/shipments");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await c.SendAsync(options)).StatusCode);
    }
    private static readonly string[] StateCollections =
        ["sce_shipments", "sce_shipment_receipts", "sce_shipment_history", "sce_shipment_audit", "sce_shipment_outbox"];

    private async Task<string[]> StateSnapshotAsync(Factory app)
    {
        var snapshots = new List<string>();
        foreach (var name in StateCollections)
        {
            var rows = await Db(app).GetCollection<BsonDocument>(name).Find(Scope()).Sort(new BsonDocument("_id", 1)).ToListAsync();
            snapshots.Add(new BsonArray(rows).ToJson());
        }
        return snapshots.ToArray();
    }

    private async Task AssertUnchangedAsync(Factory app, string[] before, string scenario)
    {
        var after = await StateSnapshotAsync(app);
        Assert.Equal(before, after);
        for (var i = 0; i < before.Length; i++)
            output.WriteLine($"{scenario}: {StateCollections[i]} unchanged SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(after[i])))}");
    }

    [Fact]
    public async Task UnicodeQuantityIsRejectedBeforeAnyPersistence()
    {
        using var app = new Factory(new()); using var c = app.CreateClient();
        var before = await StateSnapshotAsync(app);
        foreach (var quantity in new[] { "٣٠.٠٠٠", "۳۰.۰۰۰", "３０.０００", "3٠.000" })
        {
            var body = CreateBody(); body["lines"]![0]!["quantity"] = quantity;
            Assert.Equal(400, (await Send(c, "POST", "", body, "unicode-" + Guid.NewGuid())).Status);
            await AssertUnchangedAsync(app, before, "Unicode quantity rejection");
        }
        foreach (var collection in StateCollections) Assert.Equal(0, await Count(app, collection));
        var id = await Create(app, c, "ascii-control");
        Assert.Equal("30.000", (await Send(c, "GET", "/" + id)).Body["lines"]![0]!["quantity"]!.GetValue<string>());
    }

    [Fact]
    public async Task TransitionNoteBoundaryCountsScalarsAndRejectsWithoutWrites()
    {
        using var app = new Factory(new()); using var c = app.CreateClient(); var id = await Create(app, c);
        var body = Transition("Planned");
        body["note"] = string.Concat(Enumerable.Repeat("😀", 1001));
        var before = await StateSnapshotAsync(app);
        Assert.Equal(400, (await Send(c, "POST", $"/{id}/transition", body, "note-boundary")).Status);
        await AssertUnchangedAsync(app, before, "1001-code-point transition rejection");
        body["note"] = string.Concat(Enumerable.Repeat("😀", 1000));
        Assert.Equal(200, (await Send(c, "POST", $"/{id}/transition", body, "note-boundary")).Status);
        var history = await Db(app).GetCollection<BsonDocument>("sce_shipment_history")
            .Find(Scope() & new BsonDocument("ToStatus", "Planned")).SingleAsync();
        Assert.Equal(body["note"]!.GetValue<string>(), history["Note"].AsString);
        Assert.Equal(2, await Count(app, "sce_shipment_receipts"));
    }

    [Fact]
    public async Task PodNoteBoundaryCountsScalarsAndRejectsWithoutWrites()
    {
        using var app = new Factory(new()); using var c = app.CreateClient(); var id = await Create(app, c);
        foreach (var status in new[] { "Planned", "Dispatched" })
            Assert.Equal(200, (await Send(c, "POST", $"/{id}/transition", Transition(status), status)).Status);
        var body = Pod(); body["note"] = string.Concat(Enumerable.Repeat("😀", 1001));
        var before = await StateSnapshotAsync(app);
        Assert.Equal(400, (await Send(c, "POST", $"/{id}/pod", body, "note-boundary")).Status);
        await AssertUnchangedAsync(app, before, "1001-code-point POD rejection");
        body["note"] = string.Concat(Enumerable.Repeat("😀", 1000));
        Assert.Equal(201, (await Send(c, "POST", $"/{id}/pod", body, "note-boundary")).Status);
        var detail = (await Send(c, "GET", $"/{id}")).Body;
        Assert.Equal("Delivered", detail["status"]!.GetValue<string>());
        Assert.Equal(body["note"]!.GetValue<string>(), detail["pod"]!["note"]!.GetValue<string>());
        Assert.Equal(4, await Count(app, "sce_shipment_receipts"));
        Assert.Equal(5, await Count(app, "sce_shipment_outbox"));
    }

    private sealed class Transport : IEventTransportPublisher
    {
        public readonly List<EventTransportMessage> Attempts = [];
        public Task PublishAsync(EventTransportMessage message, CancellationToken ct = default)
        {
            Attempts.Add(message);
            if (Attempts.Count == 1) throw new IOException("Injected transport failure.");
            return Task.CompletedTask;
        }
    }
    [Fact]
    public async Task OutboxRetryPreservesCanonicalBytesAndIdentity()
    {
        using var app = new Factory(new()); using var c = app.CreateClient(); await Create(app, c);
        // Isolate this publisher's queue from other test tenants; production still uses the shared store implementation.
        var all = Db(app).GetCollection<BsonDocument>("sce_shipment_outbox");
        var store = new ShipmentOutboxStore(Db(app), _tenant); var transport = new Transport();
        var processor = new EventOutboxPublisherProcessor(store, transport, new EventOutboxPublisherOptions(1, 3, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMinutes(5)));
        Assert.Equal(EventOutboxPublishOutcome.RetryScheduled, (await processor.PublishPendingAsync())[0].Outcome);
        Assert.Equal(EventOutboxPublishOutcome.Published, (await processor.PublishPendingAsync())[0].Outcome);
        Assert.Equal(2, transport.Attempts.Count);
        Assert.Equal(transport.Attempts[0].EventId, transport.Attempts[1].EventId);
        Assert.Equal(transport.Attempts[0].PayloadJson, transport.Attempts[1].PayloadJson);
        Assert.Equal(_correlation, transport.Attempts[1].CorrelationId);
        var saved = await all.Find(Scope()).SingleAsync(); Assert.Equal("Published", saved["Status"]); Assert.Equal(1, saved["AttemptCount"]);
    }
}
