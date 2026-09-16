using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using Diten.SupplyChainService.Infrastructure.SourceIntake;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Features.SourceIntake;
using Diten.SupplyChainService.Application.Features.SourceIntake.Contracts;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Domain.Features.SourceIntake;
using Diten.SupplyChainService.Persistence.Features.Shipments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.SupplyChainService.Tests;

public sealed class SourceIntakeTests(ITestOutputHelper output)
{
    private static readonly string[] Collections = ["sce_shipments", "sce_shipment_receipts", "sce_shipment_history", "sce_shipment_audit", "sce_shipment_outbox", "sce_shipment_source_links"];
    private readonly ShipmentScope _scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    private sealed class Probe : IShipmentCommitProbe
    {
        public int Fail;
        public Task BeforeCommitAsync(CancellationToken ct)
        {
            if (Interlocked.Exchange(ref Fail, 0) == 1) throw new IOException("Intake precommit fault");
            return Task.CompletedTask;
        }
    }
    private sealed class Factory(Probe probe) : WebApplicationFactory<Program>
    {
        private static Dictionary<string, string?> Configuration() => new()
        {
            ["Mongo:ConnectionString"] = Environment.GetEnvironmentVariable("MOD0183_TEST_MONGO") ?? throw new InvalidOperationException("Isolated Mongo required"),
            ["Mongo:DatabaseName"] = "diten_mod0183_tests",
            ["JwtSettings:Secret"] = "mod0183-test-signing-secret-not-for-production-123456",
            ["JwtSettings:Issuer"] = "mod0183-tests", ["JwtSettings:Audience"] = "mod0183-tests"
        };
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Configuration()));
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Configuration()));
            builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IShipmentCommitProbe>(probe)));
        }
    }
    private sealed class Warehouse : HttpMessageHandler, IWarehouseReadClient
    {
        public JsonObject Body = JsonNode.Parse("""
        {"outboundId":"OB-1","status":"ReadyToShip","warehouseId":"WH-1","shipTo":{"name":"Recipient","country":"tr","line1":"First address"},
         "lines":[{"itemId":"b1f2c3d4-0000-0000-0000-000000000001","skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Gsku","quantity":"30.000","uomId":"EA"},
                  {"itemId":"b1f2c3d4-0000-0000-0000-000000000003","skuId":"c3d4e5f6-0000-0000-0000-000000000004","skuLevel":"Lsku","quantity":"2.000","uomId":"EA"}]}
        """)!.AsObject();
        public string? Error;
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/warehouse/outbound-shipments/OB-1", request.RequestUri!.AbsolutePath);
            Assert.True(request.Headers.Contains("X-Tenant-Id")); Assert.True(request.Headers.Contains("X-Legal-Entity-Id"));
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Task.FromResult(new HttpResponseMessage(Error is null ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(Body) });
        }
        public async Task<DependencyResult<JsonElement>> GetAsync(TrustedSourceContext context, string outboundId, CancellationToken cancellationToken = default)
        {
            using var http = new HttpClient(this, false) { BaseAddress = new Uri("https://warehouse.mock") };
            return await new WarehouseReadClient(http).GetAsync(context, outboundId, cancellationToken);
        }
        public Task<DependencyResult<JsonElement>> ListAsync(TrustedSourceContext context, string? status = null, string? warehouseId = null, string? cursor = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private static TrustedSourceContext Context(ShipmentScope scope, Guid? root = null) => TrustedSourceContext.FromValidatedPrincipal(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", scope.TenantId.ToString()), new Claim("legal_entity_id", scope.LegalEntityId.ToString()), new Claim("sub", scope.ActorId.ToString()), new Claim("permission", "supplychain.shipments.create")], "ValidatedTestIdentity")), scope, "test-token", root);
    private static IMongoDatabase Db(Factory app) => app.Services.GetRequiredService<IMongoDatabase>();
    private BsonDocument Filter() => new() { { "TenantId", _scope.TenantId.ToString() }, { "LegalEntityId", _scope.LegalEntityId.ToString() } };
    private static WarehouseIntakeCoordinator Coordinator(Factory app, Warehouse warehouse) => new(warehouse, new SourceIntakeStore(Db(app)), app.Services.GetRequiredService<IShipmentRepository>());
    private async Task<string[]> Snapshot(Factory app)
    {
        var result = new List<string>();
        foreach (var name in Collections)
            result.Add(new BsonArray(await Db(app).GetCollection<BsonDocument>(name).Find(Filter()).Sort(new BsonDocument("_id", 1)).ToListAsync()).ToJson());
        return result.ToArray();
    }
    private async Task AssertEmpty(Factory app)
    {
        foreach (var name in Collections) Assert.Equal(0, await Db(app).GetCollection<BsonDocument>(name).CountDocumentsAsync(Filter()));
    }
    [Fact]
    public async Task EightConcurrentIntakesCommitOnceAndIdenticalReplayChangesNothing()
    {
        using var app = new Factory(new()); using var client = app.CreateClient();
        var warehouse = new Warehouse(); var coordinator = Coordinator(app, warehouse); var context = Context(_scope);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => coordinator.IntakeAsync(context, "OB-1")));
        Assert.All(results, r => Assert.Equal("Committed", r.State));
        Assert.Single(results, r => !r.Replay); Assert.Equal(7, results.Count(r => r.Replay));
        Assert.Single(results.Select(r => r.Shipment!.Id).Distinct());
        foreach (var name in Collections) Assert.Equal(1, await Db(app).GetCollection<BsonDocument>(name).CountDocumentsAsync(Filter()));
        var before = await Snapshot(app); var replay = await coordinator.IntakeAsync(context, "OB-1");
        Assert.True(replay.Replay); Assert.Equal(before, await Snapshot(app));
        var wrongRoot = await coordinator.IntakeAsync(Context(_scope, Guid.NewGuid()), "OB-1");
        Assert.Equal("CONFLICTING_PERSISTED_ROOT", wrongRoot.Error); Assert.Equal("Quarantined", wrongRoot.State);
        Assert.Equal(before, await Snapshot(app));
        var shipment = replay.Shipment!;
        Assert.Equal("Draft", shipment.Status.ToString()); Assert.Null(shipment.PlannedDeliverAt);
        Assert.All(shipment.Lines, line => Assert.Null(line.InventoryReferenceId));
        Assert.Equal(new[] { "1", "2" }, shipment.Lines.Select(l => l.LineNumber));
        var link = await Db(app).GetCollection<BsonDocument>("sce_shipment_source_links").Find(Filter()).SingleAsync();
        Assert.Equal("LocalPollRoot", link["CorrelationOrigin"].AsString);
        Assert.True(link["SourceCorrelationId"].IsBsonNull);
        Assert.Equal(shipment.CorrelationId.ToString(), link["LocalCorrelationId"].AsString);
        Assert.False(link["IsDeleted"].AsBoolean);
        using var sourceSnapshot = JsonDocument.Parse(link["Snapshot"].AsString);
        Assert.Equal("First address", sourceSnapshot.RootElement.GetProperty("shipTo").GetProperty("line1").GetString());
        Assert.Equal("Gsku", sourceSnapshot.RootElement.GetProperty("lines")[0].GetProperty("skuLevel").GetString());
        using var defaults = JsonDocument.Parse(link["Defaults"].AsString);
        Assert.Equal("NotApplicable", defaults.RootElement.GetProperty("inventoryReconciliation").GetString());
        output.WriteLine($"8 concurrent calls: one commit, seven replay; six collections unchanged on replay; shipment={shipment.Id}");
    }
    [Fact]
    public async Task TrustedSuppliedRootIsPreservedAsSourceAndLocalCorrelation()
    {
        using var app = new Factory(new()); using var client = app.CreateClient();
        var root = Guid.NewGuid();
        var result = await Coordinator(app, new()).IntakeAsync(Context(_scope, root), "OB-1", root.ToString(), root);
        Assert.Equal("Committed", result.State);
        Assert.Equal(root, result.Shipment!.CorrelationId);
        var link = await Db(app).GetCollection<BsonDocument>("sce_shipment_source_links").Find(Filter()).SingleAsync();
        Assert.Equal("TrustedSuppliedRoot", link["CorrelationOrigin"].AsString);
        Assert.Equal(root.ToString(), link["SourceCorrelationId"].AsString);
        Assert.Equal(root.ToString(), link["LocalCorrelationId"].AsString);
    }
    [Theory]
    [InlineData("quantity")]
    [InlineData("order")]
    [InlineData("orderRef")]
    [InlineData("lines")]
    [InlineData("warehouse")]
    [InlineData("address")]
    [InlineData("status")]
    [InlineData("empty-lines")]
    public async Task ChangedSourceIsDriftAndNeverRewritesShipment(string change)
    {
        using var app = new Factory(new()); using var client = app.CreateClient(); var warehouse = new Warehouse(); var coordinator = Coordinator(app, warehouse);
        Assert.Equal("Committed", (await coordinator.IntakeAsync(Context(_scope), "OB-1")).State); var before = await Snapshot(app);
        switch (change)
        {
            case "status": warehouse.Body["status"] = "Shipped"; break;
            case "empty-lines": warehouse.Body["lines"] = new JsonArray(); break;
            case "orderRef": warehouse.Body["orderRef"] = "SO-2"; break;
            case "quantity": warehouse.Body["lines"]![0]!["quantity"] = "31.000"; break;
            case "order": var lines = warehouse.Body["lines"]!.AsArray(); var first = lines[0]!.DeepClone(); lines.RemoveAt(0); lines.Add(first); break;
            case "lines": warehouse.Body["lines"]!.AsArray().RemoveAt(1); break;
            case "warehouse": warehouse.Body["warehouseId"] = "WH-2"; break;
            case "address": warehouse.Body["shipTo"]!["line1"] = "Other address"; break;
        }
        var drift = await coordinator.IntakeAsync(Context(_scope), "OB-1"); Assert.Equal("Drift", drift.State); Assert.Equal("SOURCE_DRIFT", drift.Error);
        Assert.Equal(before, await Snapshot(app));
        var evidence = await Db(app).GetCollection<BsonDocument>("sce_shipment_source_evidence").Find(Filter()).SingleAsync(); Assert.Equal("Drift", evidence["State"].AsString);
        output.WriteLine($"Source {change} drift: six collections byte-equivalent, evidence recorded");
    }
    [Fact]
    public async Task SameSourceIdentityIsIsolatedByTenantAndLegalEntity()
    {
        using var app = new Factory(new()); using var client = app.CreateClient(); var coordinator = Coordinator(app, new());
        var scopes = new[] { _scope, new ShipmentScope(Guid.NewGuid(), _scope.LegalEntityId, _scope.ActorId), new ShipmentScope(_scope.TenantId, Guid.NewGuid(), _scope.ActorId) };
        var results = new List<SourceIntakeResult>();
        foreach (var scope in scopes) results.Add(await coordinator.IntakeAsync(Context(scope), "OB-1"));
        Assert.All(results, r => Assert.Equal("Committed", r.State)); Assert.Equal(3, results.Select(r => r.Shipment!.Id).Distinct().Count());
        Assert.Equal(1, await Db(app).GetCollection<BsonDocument>("sce_shipments").CountDocumentsAsync(Filter()));
    }
    [Fact]
    public async Task InvalidCorrelationAndDependencyFailuresCannotClaimSource()
    {
        using var app = new Factory(new()); using var client = app.CreateClient(); var warehouse = new Warehouse(); var coordinator = Coordinator(app, warehouse);
        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.IntakeAsync(null!, "OB-1")); Assert.Equal(0, warehouse.Calls);
        Assert.Throws<UnauthorizedAccessException>(() => TrustedSourceContext.FromValidatedPrincipal(new ClaimsPrincipal(), _scope, "token"));
        Assert.Equal("Quarantined", (await coordinator.IntakeAsync(Context(_scope), "OB-1", "invalid")).State); Assert.Equal(0, warehouse.Calls);
        Assert.Equal("Quarantined", (await coordinator.IntakeAsync(Context(_scope, Guid.NewGuid()), "OB-1", Guid.NewGuid().ToString())).State); Assert.Equal(0, warehouse.Calls);
        warehouse.Error = "DEPENDENCY_UNAVAILABLE"; Assert.Equal("Blocked", (await coordinator.IntakeAsync(Context(_scope), "OB-1")).State);
        warehouse.Error = null;
        foreach (var status in new[] { "Allocated", "Picked", "Packed", "Shipped", "Cancelled" })
        {
            warehouse.Body["status"] = status;
            Assert.Equal("SOURCE_NOT_READY", (await coordinator.IntakeAsync(Context(_scope), "OB-1")).Error);
        }
        warehouse.Body["status"] = "ReadyToShip"; warehouse.Body["lines"] = new JsonArray(); Assert.Equal("EMPTY_SOURCE_LINES", (await coordinator.IntakeAsync(Context(_scope), "OB-1")).Error);
        await AssertEmpty(app); Assert.Equal(0, await Db(app).GetCollection<BsonDocument>("sce_shipment_source_intents").CountDocumentsAsync(Filter()));
    }
    [Fact]
    public async Task FailedTransactionRetainsOnlyPendingDefaultsAndRestartCommitsThoseExactDefaults()
    {
        SourceIntent intent;
        using (var app = new Factory(new Probe { Fail = 1 }))
        using (var client = app.CreateClient())
        {
            Assert.Equal("COMMIT_FAILED", (await Coordinator(app, new()).IntakeAsync(Context(_scope), "OB-1")).Error); await AssertEmpty(app);
            var row = await Db(app).GetCollection<BsonDocument>("sce_shipment_source_intents").Find(Filter()).SingleAsync(); Assert.Equal("Pending", row["State"].AsString);
            intent = JsonSerializer.Deserialize<SourceIntent>(row["IntentJson"].AsString)!;
        }
        using (var app = new Factory(new()))
        using (var client = app.CreateClient())
        {
            var result = await Coordinator(app, new()).IntakeAsync(Context(_scope), "OB-1"); Assert.Equal("Committed", result.State);
            Assert.Equal(intent.Shipment.Id, result.Shipment!.Id); Assert.Equal(intent.Shipment.CorrelationId, result.Shipment.CorrelationId);
            Assert.Equal(intent.Shipment.CreatedAt, result.Shipment.CreatedAt); Assert.Equal(intent.Shipment.PlannedShipAt, result.Shipment.PlannedShipAt);
            var link = await Db(app).GetCollection<BsonDocument>("sce_shipment_source_links").Find(Filter()).SingleAsync(); Assert.Equal(intent.Defaults, link["Defaults"].AsString);
            Assert.Equal(intent.CommandId.ToString(), link["CommandId"].AsString);
            Assert.Equal(intent.LocalCorrelationId.ToString(), link["LocalCorrelationId"].AsString);
            Assert.Equal(intent.CorrelationOrigin, link["CorrelationOrigin"].AsString);
            foreach (var (collection, expectedId) in new[] { ("sce_shipment_history", intent.HistoryId), ("sce_shipment_audit", intent.AuditId), ("sce_shipment_outbox", intent.EventId), ("sce_shipment_receipts", intent.ReceiptId) })
            {
                var committed = await Db(app).GetCollection<BsonDocument>(collection).Find(Filter()).SingleAsync();
                Assert.Equal(expectedId.ToString(), committed["_id"].AsString);
                Assert.Equal(intent.CommandId.ToString(), committed[collection == "sce_shipment_outbox" ? "CausationId" : "CommandId"].AsString);
            }
            var row = await Db(app).GetCollection<BsonDocument>("sce_shipment_source_intents").Find(Filter()).SingleAsync(); Assert.Equal("Committed", row["State"].AsString);
            foreach (var name in Collections) Assert.Equal(1, await Db(app).GetCollection<BsonDocument>(name).CountDocumentsAsync(Filter()));
            output.WriteLine($"Rollback six collections empty; reconstructed host recovered durable id/root/defaults {result.Shipment.Id}");
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledOrSoftDeletedShipmentKeepsSourceReserved(bool deleted)
    {
        using var app = new Factory(new()); using var client = app.CreateClient(); var coordinator = Coordinator(app, new());
        var first = await coordinator.IntakeAsync(Context(_scope), "OB-1"); Assert.Equal("Committed", first.State);
        var changes = deleted ? new BsonDocument { { "IsDeleted", true }, { "DeletedAt", DateTimeOffset.UtcNow.ToString("O") } } : new BsonDocument("Status", "Cancelled");
        await Db(app).GetCollection<BsonDocument>("sce_shipments").UpdateOneAsync(Filter(), new BsonDocument("$set", changes));
        var before = await Snapshot(app); var replay = await coordinator.IntakeAsync(Context(_scope), "OB-1"); Assert.True(replay.Replay);
        if (deleted)
        {
            Assert.Equal("Blocked", replay.State); Assert.Equal("SHIPMENT_NOT_FOUND", replay.Error); Assert.Null(replay.Shipment);
        }
        else
        {
            Assert.Equal("Committed", replay.State); Assert.Equal(first.Shipment!.Id, replay.Shipment!.Id);
        }
        Assert.Equal(before, await Snapshot(app));
        Assert.Equal(1, await Db(app).GetCollection<BsonDocument>("sce_shipment_source_links").CountDocumentsAsync(Filter()));
    }
}
