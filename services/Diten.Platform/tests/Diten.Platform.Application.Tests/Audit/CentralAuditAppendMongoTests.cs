using System.Net;
using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/*
 * BL-501 — PLATFORM FILES A FORWARDED EVENT ONCE, UNDER ITS TENANT.
 *
 * The real receiving side: InternalAuditController (X-Internal-Api-Key) → production AuditService (AddApplication) →
 * AuditOutboxRepository over a TEST-OWNED replica set with the AccessGovernance schema (the unique
 * ux_audit_outbox_idempotency_key) → AuditOutboxProcessor → audit_events → AuditEventRepository's tenant-scoped read.
 * The sending side is the shared package's own client (PlatformCentralAuditClient), so the wire shape under test is the
 * one every service sends.
 *
 *   same event id twice       → Queued, then Duplicate (202 both: the sender marks it delivered) → ONE record
 *   two event ids, otherwise identical (same correlation / type / entity / operation) → TWO records — the composite key
 *                               alone would have filed the second as a duplicate and lost it
 *   tenant                    → the record is read under its tenant and is invisible to another tenant
 */
public sealed class CentralAuditAppendMongoTests : IAsyncLifetime
{
    private const string Key = "bl501-platform-append-test-key-0123456789";
    private static readonly Guid TenantA = Guid.Parse("50150150-0000-4000-8000-0000000000a0");
    private static readonly Guid TenantB = Guid.Parse("50150150-0000-4000-8000-0000000000b0");
    private static readonly Guid Actor = Guid.Parse("50150150-0000-4000-8000-0000000000a9");

    private DisposableMongoReplicaSet? _mongo;
    private IMongoDatabase _database = null!;
    private TestServer? _server;

    public async Task InitializeAsync()
    {
        _mongo = await DisposableMongoReplicaSet.StartAsync();
        _database = _mongo.CreateDatabase();
        await PlatformSchemaManifest.ApplyAsync(_database, new[] { SchemaProfile.AccessGovernance });
        var dbContext = new PlatformDbContext(_mongo.Client, _database);

        var builder = new WebHostBuilder()
            .UseEnvironment("Test")
            .ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?> { ["AuthService:InternalApiKey"] = Key }))
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLogging();
                services.AddHttpContextAccessor();
                services.AddApplication();
                services.AddScoped<ITenantContext, TenantContext>();
                services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                services.AddSingleton<IPlatformDbContext>(dbContext);
                services.AddScoped<AuditOutboxRepository>();
                services.AddScoped<IAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                services.AddControllers().AddApplicationPart(typeof(InternalAuditController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        _server = new TestServer(builder);
    }

    public async Task DisposeAsync()
    {
        _server?.Dispose();
        if (_mongo is not null)
        {
            await _mongo.DisposeAsync();
        }
    }

    [Fact]
    public async Task The_same_event_id_sent_twice_is_one_record_and_both_sends_are_accepted()
    {
        var record = Event(TenantA, Guid.NewGuid());

        var first = await RawAppendAsync(record);
        var second = await RawAppendAsync(record);

        Assert.Equal(HttpStatusCode.Accepted, first.Status);
        Assert.Equal("Queued", first.Body.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Accepted, second.Status);
        Assert.Equal("Duplicate", second.Body.GetProperty("status").GetString());

        await ProcessOutboxAsync();
        var filed = Assert.Single(await ReadAsync(TenantA, record.CorrelationId));
        Assert.Equal(record.Id.ToString(), filed.Metadata["eventId"]?.ToString());

        // The shared client treats both answers as delivered: a resend after a lost acknowledgement costs nothing.
        Assert.Equal(CentralAuditDeliveryOutcome.Accepted, (await Client().SendAsync(record, CancellationToken.None)).Outcome);
        await ProcessOutboxAsync();
        Assert.Single(await ReadAsync(TenantA, record.CorrelationId));
    }

    [Fact]
    public async Task Two_events_that_share_correlation_type_entity_and_operation_are_two_records()
    {
        var correlation = Guid.NewGuid();
        var entity = Guid.NewGuid();
        var one = Event(TenantA, correlation, entity);
        var two = Event(TenantA, correlation, entity);

        Assert.Equal(CentralAuditDeliveryOutcome.Accepted, (await Client().SendAsync(one, CancellationToken.None)).Outcome);
        Assert.Equal(CentralAuditDeliveryOutcome.Accepted, (await Client().SendAsync(two, CancellationToken.None)).Outcome);
        await ProcessOutboxAsync();

        var filed = await ReadAsync(TenantA, correlation);
        Assert.Equal(2, filed.Count);
        Assert.Equal(
            new[] { one.Id.ToString(), two.Id.ToString() }.Order(),
            filed.Select(e => e.Metadata["eventId"]?.ToString()).Order());
    }

    [Fact]
    public async Task The_record_is_filed_under_its_tenant_with_who_what_when_and_another_tenant_cannot_read_it()
    {
        var record = Event(TenantA, Guid.NewGuid());

        Assert.Equal(CentralAuditDeliveryOutcome.Accepted, (await Client().SendAsync(record, CancellationToken.None)).Outcome);
        await ProcessOutboxAsync();

        var filed = Assert.Single(await ReadAsync(TenantA, record.CorrelationId));
        Assert.Equal(TenantA, filed.TenantId);
        Assert.Equal(TenantA, filed.TargetTenantId);
        Assert.Equal(Actor, filed.ActorId);
        Assert.Equal(AuditActorType.TenantUser, filed.ActorType);
        Assert.Equal(AuditCategory.MasterData, filed.Category);
        Assert.Equal(AuditOperation.Create, filed.Operation);
        Assert.Equal(AuditOutcome.Succeeded, filed.Outcome);
        Assert.Equal("InventoryTransaction", filed.EntityType);
        Assert.Equal(record.EntityId, filed.EntityId);
        Assert.Equal("Diten.InventoryService", filed.SourceService);
        Assert.Equal("INVENTORY-LEDGER", filed.SourceModule);
        Assert.Equal(nameof(Event), filed.RequestType);
        Assert.Equal(record.OccurredAtUtc, filed.OccurredAtUtc.UtcDateTime, TimeSpan.FromMilliseconds(1));
        Assert.NotNull(filed.ActorDisplayNameMasked);
        Assert.DoesNotContain("Denetçi", filed.ActorDisplayNameMasked!, StringComparison.Ordinal);

        Assert.Empty(await ReadAsync(TenantB, record.CorrelationId));
    }

    [Fact]
    public async Task A_wrong_key_is_refused_and_files_nothing()
    {
        var record = Event(TenantA, Guid.NewGuid());

        var result = await Client(key: "not-the-key").SendAsync(record, CancellationToken.None);

        Assert.Equal(CentralAuditDeliveryOutcome.Retry, result.Outcome);
        Assert.Contains("401", result.Error, StringComparison.Ordinal);
        await ProcessOutboxAsync();
        Assert.Empty(await ReadAsync(TenantA, record.CorrelationId));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    private static CentralAuditOutboxRecord Event(Guid tenant, Guid correlation, Guid? entity = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenant,
        CompanyId = Guid.NewGuid(),
        ActorId = Actor,
        ActorDisplayName = "Ayşe Denetçi",
        ActorType = CentralAuditActorTypes.TenantUser,
        SourceService = "Diten.InventoryService",
        SourceModule = "INVENTORY-LEDGER",
        Category = "MasterData",
        Operation = "Create",
        EntityType = "InventoryTransaction",
        EntityId = entity ?? Guid.NewGuid(),
        RequestType = nameof(Event),
        CorrelationId = correlation,
        OccurredAtUtc = DateTime.SpecifyKind(DateTime.UtcNow.AddSeconds(-5), DateTimeKind.Utc)
    };

    private PlatformCentralAuditClient Client(string key = Key) => new(
        new TestServerClientFactory(_server!),
        new FixedOptions(new CentralAuditOptions { PlatformBaseUrl = "http://localhost", InternalApiKey = key }));

    /// <summary>The client's request, read back as JSON — the status Platform answered.</summary>
    private async Task<(HttpStatusCode Status, JsonElement Body)> RawAppendAsync(CentralAuditOutboxRecord record)
    {
        var handler = new CapturingHandler(_server!.CreateHandler());
        var client = new PlatformCentralAuditClient(
            new TestServerClientFactory(handler),
            new FixedOptions(new CentralAuditOptions { PlatformBaseUrl = "http://localhost", InternalApiKey = Key }));
        await client.SendAsync(record, CancellationToken.None);
        return (handler.LastStatus, JsonDocument.Parse(handler.LastBody).RootElement.Clone());
    }

    private async Task ProcessOutboxAsync()
    {
        using var scope = _server!.Services.CreateScope();
        var processor = new AuditOutboxProcessor(
            scope.ServiceProvider.GetRequiredService<AuditOutboxRepository>(),
            new AuditEventRepository(_database, scope.ServiceProvider.GetRequiredService<ITenantContext>()),
            scope.ServiceProvider.GetRequiredService<ITenantContext>(),
            new AuditOutboxPayloadMapper(),
            new AuditOutboxWorkerOptions { BatchSize = 50, MaxAttempts = 3, ProcessingStaleAfter = TimeSpan.FromMinutes(5) },
            NullLogger<AuditOutboxProcessor>.Instance);
        while (await processor.ProcessBatchAsync() > 0)
        {
        }
    }

    private async Task<IReadOnlyList<AuditEvent>> ReadAsync(Guid tenant, Guid correlation)
    {
        var context = new TenantContext();
        using var _ = TenantScope.Begin(context, tenant);
        return await new AuditEventRepository(_database, context).GetByCorrelationIdAsync(correlation);
    }

    private sealed class FixedOptions(CentralAuditOptions value) : IOptionsMonitor<CentralAuditOptions>
    {
        public CentralAuditOptions CurrentValue => value;
        public CentralAuditOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<CentralAuditOptions, string?> listener) => null;
    }

    private sealed class TestServerClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public TestServerClientFactory(TestServer server) => _handler = server.CreateHandler();

        public TestServerClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
    }

    private sealed class CapturingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public HttpStatusCode LastStatus { get; private set; }
        public string LastBody { get; private set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            LastStatus = response.StatusCode;
            LastBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
