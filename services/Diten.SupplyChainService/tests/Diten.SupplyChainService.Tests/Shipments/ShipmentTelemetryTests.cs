using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.BuildingBlocks.Eventing;
using Diten.SupplyChainService.Application.Behaviors;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.SourceIntake;
using Diten.SupplyChainService.Application.Features.SourceIntake.Contracts;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Domain.Features.SourceIntake;
using Diten.SupplyChainService.Infrastructure.SourceIntake;
using Diten.SupplyChainService.Persistence.Features.Shipments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;
using Xunit.Abstractions;

namespace Diten.SupplyChainService.Tests.Shipments;

[CollectionDefinition("ShipmentTelemetry", DisableParallelization = true)]
public sealed class ShipmentTelemetryCollection { }

// Q288 group 2 — MOD-0183 pack §17: one test per O-3/O-4 counter asserting it moves on the named behaviour and does not
// move on a retry, and one test asserting the O-2 histogram gives a p95. The collection runs alone, so no other test
// can move these process-wide counters while a test is measuring them. O-3 drift was added by Q315 (pack §13 drift line).
[Collection("ShipmentTelemetry")]
public sealed class ShipmentTelemetryTests(ITestOutputHelper output)
{
    private const string Secret = "mod0183-test-signing-secret-not-for-production-123456";
    private const string Database = "diten_mod0183_tests";
    private static readonly string[] AllPermissions = ["read", "create", "dispatch", "pod.capture", "cancel", "reconcile"];
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _le = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _correlation = Guid.NewGuid();
    private readonly string _appName = "q288-" + Guid.NewGuid().ToString("N")[..12];

    private static string Connection => Environment.GetEnvironmentVariable("MOD0183_TEST_MONGO")
        ?? throw new InvalidOperationException("Isolated Mongo required: MOD0183_TEST_MONGO must point to an isolated replica set.");

    [Fact]
    public async Task OperationDuration_WhenOperationsComplete_RecordsOneTaggedMillisecondValueEachSoP95CanBeReported()
    {
        using var recorder = new Recorder();
        var behavior = new PerformanceBehavior<LatencyProbe, int>(NullLogger<PerformanceBehavior<LatencyProbe, int>>.Instance);
        var delays = Enumerable.Range(1, 20).Select(i => i * 3).ToArray();

        foreach (var delay in delays)
            await behavior.Handle(new LatencyProbe(), async () => { await Task.Delay(delay); return delay; }, CancellationToken.None);

        var values = recorder.Doubles("shipment.operation.duration", nameof(LatencyProbe));
        Assert.Equal(delays.Length, values.Length);
        Assert.Equal("ms", recorder.Unit("shipment.operation.duration"));
        var p95 = values.OrderBy(value => value).ElementAt((int)Math.Ceiling(0.95 * values.Length) - 1);
        output.WriteLine($"p95 over {values.Length} recorded operations = {p95:F1} ms");
        Assert.True(p95 >= delays.OrderBy(d => d).ElementAt(18), $"p95 {p95:F1} ms is below the 19th slowest delay; durations are not being recorded.");
    }

    [Fact]
    public async Task IntakeBlocked_WhenWarehouseUnavailableOrCorrelationIncompatible_MovesAndCommitRetryDoesNot()
    {
        var probe = new Probe();
        using var app = new Factory(probe, Connection); using var client = app.CreateClient();
        var scope = new ShipmentScope(_tenant, _le, _actor);
        var warehouse = new Warehouse();
        var coordinator = new WarehouseIntakeCoordinator(warehouse, new SourceIntakeStore(Db(app)), app.Services.GetRequiredService<IShipmentRepository>());
        using var recorder = new Recorder();

        warehouse.Unavailable = true;
        Assert.Equal("Blocked", (await coordinator.IntakeAsync(Context(scope), "OB-1")).State);
        Assert.Equal(1, recorder.Sum("shipment.intake.blocked"));
        Assert.Equal("Quarantined", (await coordinator.IntakeAsync(Context(scope), "OB-1", "not-a-uuid")).State);
        Assert.Equal(2, recorder.Sum("shipment.intake.blocked"));

        // A failed commit is the retryable outcome: it must not count, and neither may the retry that commits.
        warehouse.Unavailable = false; probe.Fail = 1;
        Assert.Equal("COMMIT_FAILED", (await coordinator.IntakeAsync(Context(scope), "OB-1")).Error);
        Assert.Equal("Committed", (await coordinator.IntakeAsync(Context(scope), "OB-1")).State);
        Assert.Equal(2, recorder.Sum("shipment.intake.blocked"));
    }

    [Fact]
    public async Task SourceDrifts_WhenRecordedSourceChanges_MovesOnEveryDetectionAndNotOnReplayOrCommitRetry()
    {
        var probe = new Probe();
        using var app = new Factory(probe, Connection); using var client = app.CreateClient();
        var scope = new ShipmentScope(_tenant, _le, _actor);
        var warehouse = new Warehouse();
        var coordinator = new WarehouseIntakeCoordinator(warehouse, new SourceIntakeStore(Db(app)), app.Services.GetRequiredService<IShipmentRepository>());
        using var recorder = new Recorder();

        Assert.Equal("Committed", (await coordinator.IntakeAsync(Context(scope), "OB-1")).State);
        Assert.True((await coordinator.IntakeAsync(Context(scope), "OB-1")).Replay);
        Assert.Equal(0, recorder.Sum("shipment.intake.drift"));

        // The same source identity now returns a different snapshot: pack §13, one Drift evidence row per detection.
        warehouse.Body["lines"]![0]!["quantity"] = "31.000";
        var first = await coordinator.IntakeAsync(Context(scope), "OB-1");
        Assert.Equal(("Drift", "SOURCE_DRIFT"), (first.State, first.Error));
        Assert.Equal(1, recorder.Sum("shipment.intake.drift"));
        Assert.Equal(1, await DriftRowsAsync(app, scope));

        // CT ruling: every detection counts, so a second detection moves it again.
        Assert.Equal("Drift", (await coordinator.IntakeAsync(Context(scope), "OB-1")).State);
        Assert.Equal(2, recorder.Sum("shipment.intake.drift"));
        Assert.Equal(2, await DriftRowsAsync(app, scope));

        // Back to the recorded snapshot: a replay, not a drift.
        warehouse.Body["lines"]![0]!["quantity"] = "30.000";
        Assert.True((await coordinator.IntakeAsync(Context(scope), "OB-1")).Replay);
        Assert.Equal(2, recorder.Sum("shipment.intake.drift"));

        // A failed commit and the retry that commits it are not drift.
        var other = new ShipmentScope(Guid.NewGuid(), _le, _actor); probe.Fail = 1;
        Assert.Equal("COMMIT_FAILED", (await coordinator.IntakeAsync(Context(other), "OB-1")).Error);
        Assert.Equal("Committed", (await coordinator.IntakeAsync(Context(other), "OB-1")).State);
        Assert.Equal(2, recorder.Sum("shipment.intake.drift"));
    }

    [Fact]
    public async Task SourceDrifts_WhenAConcurrentIntakePreparedTheIntentFirst_CountsTheSecondDriftSiteToo()
    {
        using var app = new Factory(new Probe(), Connection); using var client = app.CreateClient();
        var scope = new ShipmentScope(_tenant, _le, _actor);
        var warehouse = new Warehouse(); var store = new SourceIntakeStore(Db(app)); var shipments = app.Services.GetRequiredService<IShipmentRepository>();
        Assert.Equal("Committed", (await new WarehouseIntakeCoordinator(warehouse, store, shipments).IntakeAsync(Context(scope), "OB-1")).State);
        using var recorder = new Recorder();

        // The racing intake did not see the intent at lookup, then finds it at prepare with a different hash.
        warehouse.Body["lines"]![0]!["quantity"] = "31.000";
        var raced = await new WarehouseIntakeCoordinator(warehouse, new LookupMissesStore(store), shipments).IntakeAsync(Context(scope), "OB-1");

        Assert.Equal(("Drift", "SOURCE_DRIFT"), (raced.State, raced.Error));
        Assert.Equal(1, recorder.Sum("shipment.intake.drift"));
        Assert.Equal(1, await DriftRowsAsync(app, scope));
    }

    [Fact]
    public async Task SourceDrifts_WhenTheCommitTimeHashGuardFires_EndsAsCommitFailedAndIsNotCounted()
    {
        // Q315 decision: ShipmentRepository's "Source drift must be reconciled before commit" guard ends as COMMIT_FAILED.
        // The product's writers cannot break it (the link stores the intent's own hash); only a tampered link reaches it.
        using var app = new Factory(new Probe(), Connection); using var client = app.CreateClient();
        var scope = new ShipmentScope(_tenant, _le, _actor);
        var coordinator = new WarehouseIntakeCoordinator(new Warehouse(), new SourceIntakeStore(Db(app)), app.Services.GetRequiredService<IShipmentRepository>());
        Assert.Equal("Committed", (await coordinator.IntakeAsync(Context(scope), "OB-1")).State);
        await Db(app).GetCollection<BsonDocument>("sce_shipment_source_links").UpdateOneAsync(ScopeOf(scope), new BsonDocument("$set", new BsonDocument("Hash", "TAMPERED")));
        using var recorder = new Recorder();

        var guarded = await coordinator.IntakeAsync(Context(scope), "OB-1");

        Assert.Equal("COMMIT_FAILED", guarded.Error);
        Assert.Equal(0, recorder.Sum("shipment.intake.drift"));
        Assert.Equal(0, recorder.Sum("shipment.intake.blocked"));
    }

    // Delegates to the real store but misses at lookup, reproducing the race window between FindAsync and PrepareAsync.
    private sealed class LookupMissesStore(ISourceIntakeStore inner) : ISourceIntakeStore
    {
        public Task<SourceIntent?> FindAsync(ShipmentScope scope, string key, CancellationToken ct) => Task.FromResult<SourceIntent?>(null);
        public Task<SourceIntent> PrepareAsync(ShipmentScope scope, SourceIntent candidate, CancellationToken ct) => inner.PrepareAsync(scope, candidate, ct);
        public Task RecordEvidenceAsync(ShipmentScope scope, string key, string outboundId, string state, string evidence, CancellationToken ct) =>
            inner.RecordEvidenceAsync(scope, key, outboundId, state, evidence, ct);
    }

    private static BsonDocument ScopeOf(ShipmentScope scope) => new() { { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() } };
    private static async Task<long> DriftRowsAsync(Factory app, ShipmentScope scope) =>
        await Db(app).GetCollection<BsonDocument>("sce_shipment_source_evidence").CountDocumentsAsync(ScopeOf(scope).Add("State", "Drift"));

    [Fact]
    public async Task IdempotentReplays_WhenDuplicateKeyIsReplayed_MovesOnceAndTransientTransactionRetryDoesNot()
    {
        var commits = new CommitCounter();
        using var app = new Factory(new Probe(), WithAppName(Connection, _appName), commits); using var client = app.CreateClient();
        using var recorder = new Recorder();

        Assert.Equal(201, (await SendAsync(client, "POST", "", CreateBody(), "create")).Status);
        Assert.Equal(0, recorder.Sum("shipment.idempotency.replays"));
        Assert.Equal(200, (await SendAsync(client, "POST", "", CreateBody(), "create")).Status);
        Assert.Equal(1, recorder.Sum("shipment.idempotency.replays"));

        // The driver re-runs the whole transaction body after a transient commit failure; still one replay.
        await FailNextCommitAsync(app);
        try { Assert.Equal(200, (await SendAsync(client, "POST", "", CreateBody(), "create")).Status); }
        finally { await FailPointOffAsync(app); }
        Assert.Equal(1, commits.Failed);
        Assert.Equal(2, recorder.Sum("shipment.idempotency.replays"));
    }

    [Fact]
    public async Task ScopeDenials_WhenHeaderScopeIsAnotherTenant_MovesAndInScopeRequestDoesNot()
    {
        using var app = new Factory(new Probe(), Connection); using var client = app.CreateClient();
        using var recorder = new Recorder();

        Assert.Equal(200, (await SendAsync(client, "GET", "")).Status);
        Assert.Equal(0, recorder.Sum("shipment.scope.denials"));
        var denied = await SendAsync(client, "GET", "", headerTenant: Guid.NewGuid());
        Assert.Equal(404, denied.Status);
        Assert.Equal(ContractErrorCodes.ShipmentNotFound, denied.Body["error"]!["code"]!.GetValue<string>());
        Assert.Equal(1, recorder.Sum("shipment.scope.denials"));
    }

    [Fact]
    public async Task OutboxPending_WhenMutationCommits_MovesPerEventAndReplayOrTransientRetryDoesNotAddMore()
    {
        var commits = new CommitCounter();
        using var app = new Factory(new Probe(), WithAppName(Connection, _appName), commits); using var client = app.CreateClient();
        using var recorder = new Recorder();

        var created = await SendAsync(client, "POST", "", CreateBody(), "create");
        Assert.Equal(201, created.Status);
        Assert.Equal(1, recorder.Sum("shipment.outbox.pending"));
        Assert.Equal(200, (await SendAsync(client, "POST", "", CreateBody(), "create")).Status);
        Assert.Equal(1, recorder.Sum("shipment.outbox.pending"));

        // One committed transition after a transient commit failure is one event, not two.
        var id = created.Body["shipmentId"]!.GetValue<string>();
        await FailNextCommitAsync(app);
        try { Assert.Equal(200, (await SendAsync(client, "POST", $"/{id}/transition", Transition("Planned"), "plan")).Status); }
        finally { await FailPointOffAsync(app); }
        Assert.Equal(1, commits.Failed);
        Assert.Equal(2, recorder.Sum("shipment.outbox.pending"));
        Assert.Equal(2, await Db(app).GetCollection<BsonDocument>("sce_shipment_outbox").CountDocumentsAsync(Scope()));
    }

    [Fact]
    public async Task OutboxRetries_WhenPublishFailsBelowMaxAttempts_MovesOnceAndStaleRepeatDoesNot()
    {
        var outbox = OutboxCollection(); var store = new ShipmentOutboxStore(outbox.Database);
        var id = await InsertPublishingAsync(outbox, attemptCount: 0);
        using var recorder = new Recorder();

        await store.FailPublishAsync(id, "transport down", DateTimeOffset.UtcNow.AddMinutes(1), maxAttempts: 3);
        Assert.Equal(1, recorder.Sum("shipment.outbox.retries"));
        Assert.Equal(0, recorder.Sum("shipment.outbox.dead_letters"));

        // The row is Pending now; a repeated failure report for the same attempt must not count again.
        await store.FailPublishAsync(id, "transport down", DateTimeOffset.UtcNow.AddMinutes(1), maxAttempts: 3);
        Assert.Equal(1, recorder.Sum("shipment.outbox.retries"));
        Assert.Equal("Pending", (await outbox.Find(new BsonDocument("_id", id.ToString())).SingleAsync())["Status"].AsString);
    }

    [Fact]
    public async Task OutboxDeadLetters_WhenAttemptsExhaustOrFailureIsTerminal_MovesOnceEachAndRepeatDoesNot()
    {
        var outbox = OutboxCollection(); var store = new ShipmentOutboxStore(outbox.Database);
        var exhausted = await InsertPublishingAsync(outbox, attemptCount: 2);
        var terminal = await InsertPublishingAsync(outbox, attemptCount: 0);
        using var recorder = new Recorder();

        await store.FailPublishAsync(exhausted, "transport down", DateTimeOffset.UtcNow.AddMinutes(1), maxAttempts: 3);
        Assert.Equal(1, recorder.Sum("shipment.outbox.dead_letters"));
        Assert.Equal(0, recorder.Sum("shipment.outbox.retries"));

        var failure = new EventOutboxTerminalFailure(EventOutboxTerminalFailureKind.Unsupported, "q288.terminal");
        await store.DeadLetterPublishAsync(terminal, failure);
        Assert.Equal(2, recorder.Sum("shipment.outbox.dead_letters"));
        await store.DeadLetterPublishAsync(terminal, failure);
        Assert.Equal(2, recorder.Sum("shipment.outbox.dead_letters"));
        Assert.Equal("DeadLettered", (await outbox.Find(new BsonDocument("_id", terminal.ToString())).SingleAsync())["Status"].AsString);
    }

    private sealed record LatencyProbe;

    // Captures measurements of the shipment meter only, from the moment it is created.
    private sealed class Recorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentQueue<(string Name, double Value, string? Operation)> _measurements = new();
        private readonly ConcurrentDictionary<string, string?> _units = new();
        public Recorder()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != ShipmentTelemetry.MeterName) return;
                _units[instrument.Name] = instrument.Unit; listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => _measurements.Enqueue((instrument.Name, value, Operation(tags))));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => _measurements.Enqueue((instrument.Name, value, Operation(tags))));
            _listener.Start();
        }
        public long Sum(string name) => (long)_measurements.Where(m => m.Name == name).Sum(m => m.Value);
        public double[] Doubles(string name, string operation) => _measurements.Where(m => m.Name == name && m.Operation == operation).Select(m => m.Value).ToArray();
        public string? Unit(string name) => _units.GetValueOrDefault(name);
        public void Dispose() => _listener.Dispose();
        private static string? Operation(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            foreach (var tag in tags) if (tag.Key == ShipmentTelemetry.OperationTag) return tag.Value as string;
            return null;
        }
    }

    private sealed class Probe : IShipmentCommitProbe
    {
        public int Fail;
        public Task BeforeCommitAsync(CancellationToken ct) =>
            Interlocked.Exchange(ref Fail, 0) == 1 ? throw new IOException("Injected failure before commit.") : Task.CompletedTask;
    }

    // Counts the service client's commitTransaction outcomes, so a test can prove the driver really retried.
    private sealed class CommitCounter
    {
        public int Failed;
        public int Succeeded;
    }

    private sealed class Factory(Probe probe, string connection, CommitCounter? commits = null) : WebApplicationFactory<Program>
    {
        private Dictionary<string, string?> Settings() => new()
        {
            ["Mongo:ConnectionString"] = connection, ["Mongo:DatabaseName"] = Database,
            ["JwtSettings:Secret"] = Secret, ["JwtSettings:Issuer"] = "mod0183-tests", ["JwtSettings:Audience"] = "mod0183-tests"
        };
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Settings()));
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings()));
            builder.ConfigureServices(s =>
            {
                s.Replace(ServiceDescriptor.Singleton<IShipmentCommitProbe>(probe));
                if (commits is null) return;
                var settings = MongoClientSettings.FromConnectionString(connection);
                settings.ClusterConfigurator = cluster => cluster
                    .Subscribe<CommandFailedEvent>(e => { if (e.CommandName == "commitTransaction") Interlocked.Increment(ref commits.Failed); })
                    .Subscribe<CommandSucceededEvent>(e => { if (e.CommandName == "commitTransaction") Interlocked.Increment(ref commits.Succeeded); });
                s.Replace(ServiceDescriptor.Singleton<IMongoClient>(new MongoClient(settings)));
            });
        }
    }

    private sealed class Warehouse : HttpMessageHandler, IWarehouseReadClient
    {
        public readonly JsonObject Body = JsonNode.Parse("""
        {"outboundId":"OB-1","status":"ReadyToShip","warehouseId":"WH-1","shipTo":{"name":"Recipient","country":"tr","line1":"First address"},
         "lines":[{"itemId":"b1f2c3d4-0000-0000-0000-000000000001","skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Gsku","quantity":"30.000","uomId":"EA"}]}
        """)!.AsObject();
        public bool Unavailable;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(Unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = JsonContent.Create(Body) });
        public async Task<DependencyResult<JsonElement>> GetAsync(TrustedSourceContext context, string outboundId, CancellationToken cancellationToken = default)
        {
            using var http = new HttpClient(this, false) { BaseAddress = new Uri("https://warehouse.mock") };
            return await new WarehouseReadClient(http).GetAsync(context, outboundId, cancellationToken);
        }
        public Task<DependencyResult<JsonElement>> ListAsync(TrustedSourceContext context, string? status = null, string? warehouseId = null,
            string? cursor = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static TrustedSourceContext Context(ShipmentScope scope) => TrustedSourceContext.FromValidatedPrincipal(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenant_id", scope.TenantId.ToString()), new Claim("legal_entity_id", scope.LegalEntityId.ToString()),
            new Claim("sub", scope.ActorId.ToString()), new Claim("permission", "supplychain.shipments.create")], "ValidatedTestIdentity")), scope, "test-token", null);

    private static string WithAppName(string connection, string appName) =>
        connection + (connection.Contains('?') ? "&" : "?") + "appName=" + appName;

    private static IMongoDatabase Db(Factory app) => app.Services.GetRequiredService<IMongoDatabase>();
    private FilterDefinition<BsonDocument> Scope() => new BsonDocument { { "TenantId", _tenant.ToString() }, { "LegalEntityId", _le.ToString() } };

    // One transient commit failure, scoped to this test's client by appName so no other client can trip it.
    private async Task FailNextCommitAsync(Factory app) =>
        await Db(app).Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "configureFailPoint", "failCommand" }, { "mode", new BsonDocument("times", 1) },
            { "data", new BsonDocument { { "failCommands", new BsonArray { "commitTransaction" } }, { "appName", _appName },
                { "errorCode", 251 }, { "errorLabels", new BsonArray { "TransientTransactionError" } } } }
        });
    private static async Task FailPointOffAsync(Factory app) =>
        await Db(app).Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument { { "configureFailPoint", "failCommand" }, { "mode", "off" } });

    private static IMongoCollection<BsonDocument> OutboxCollection() =>
        new MongoClient(Connection).GetDatabase(Database).GetCollection<BsonDocument>("sce_shipment_outbox");
    private async Task<Guid> InsertPublishingAsync(IMongoCollection<BsonDocument> outbox, int attemptCount)
    {
        var id = Guid.NewGuid();
        await outbox.InsertOneAsync(new BsonDocument
        {
            { "_id", id.ToString() }, { "TenantId", _tenant.ToString() }, { "LegalEntityId", _le.ToString() }, { "ShipmentId", Guid.NewGuid().ToString() },
            { "EventType", "ShipmentCreated" }, { "CorrelationId", _correlation.ToString() }, { "CausationId", Guid.NewGuid().ToString() },
            { "OccurredAt", DateTime.UtcNow }, { "PayloadJson", "{}" }, { "Status", "Publishing" }, { "AttemptCount", attemptCount },
            { "NextAttemptAt", DateTime.UtcNow }, { "ClaimedAt", DateTime.UtcNow }
        });
        return id;
    }

    private static JsonObject CreateBody() => JsonNode.Parse("""
     {"sourceModule":"MOD-0141","sourceType":"SALES_ORDER","sourceDocumentId":"SO-900","warehouseReferenceId":"wh-01",
     "shipToReference":"CUST-100/ADDR-2","plannedShipAt":"2026-09-20T08:00:00Z","plannedDeliverAt":"2026-09-21T16:00:00Z",
     "lines":[{"lineNumber":"1","itemId":"b1f2c3d4-0000-0000-0000-000000000001","skuId":"c3d4e5f6-0000-0000-0000-000000000002",
     "quantity":"30.000","uomId":"EA","inventoryReferenceId":"rsv-4001"}]}
     """)!.AsObject();
    private static JsonObject Transition(string status) => new() { ["targetStatus"] = status, ["occurredAt"] = "2026-09-20T08:15:00Z" };

    private string Token() =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("mod0183-tests", "mod0183-tests",
            new[] { new Claim("sub", _actor.ToString()), new Claim("tenant_id", _tenant.ToString()), new Claim("legal_entity_id", _le.ToString()), new Claim("actor_type", "tenant_user") }
                .Concat(AllPermissions.Select(p => new Claim("permission", "supplychain.shipments." + p))),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

    private async Task<(int Status, JsonObject Body)> SendAsync(HttpClient client, string method, string path, JsonObject? body = null,
        string? key = null, Guid? headerTenant = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/shipment-bundle/shipments" + path);
        request.Headers.Add("Authorization", "Bearer " + Token());
        request.Headers.Add("X-Tenant-Id", (headerTenant ?? _tenant).ToString()); request.Headers.Add("X-Legal-Entity-Id", _le.ToString());
        request.Headers.Add("X-Correlation-Id", _correlation.ToString());
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        output.WriteLine($"{method} {path} => {(int)response.StatusCode} {text}");
        return ((int)response.StatusCode, string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!.AsObject());
    }
}
