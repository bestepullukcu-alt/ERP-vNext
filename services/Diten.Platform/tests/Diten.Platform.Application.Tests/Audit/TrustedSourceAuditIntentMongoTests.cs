using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Migrations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class TrustedSourceAuditIntentMongoTests : IAsyncLifetime
{
    private readonly string _databaseName = $"fu01_{Guid.NewGuid():N}";
    private IMongoClient _client = null!;
    private IMongoDatabase _database = null!;
    private AuditOutboxRepository _repository = null!;
    private TrustedSourceAuditIntentAcceptanceService _service = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.GuidRepresentation = GuidRepresentation.Standard;
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
        _client = new MongoClient(settings);
        await _client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _database = _client.GetDatabase(_databaseName);
        await PlatformSchemaManifest.ApplyAsync(_database, new[] { SchemaProfile.AccessGovernance });
        _repository = new AuditOutboxRepository(
            new PlatformDbContext(_client, _database),
            new AuditOutboxTemporalMigrationRepository(_database));
        _service = new(_repository, new FixedTimeProvider(TrustedSourceAuditIntentTestData.Now));
    }

    public async Task DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DropDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task NewAndSequentialReplay_PersistOneRowAndOriginalReceipt()
    {
        var envelope = TrustedSourceAuditIntentTestData.Envelope();

        var first = await _service.AcceptAsync(envelope);
        var replay = await _service.AcceptAsync(envelope);

        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Accepted, first.Status);
        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Duplicate, replay.Status);
        Assert.Equal(first.Receipt!.CentralAcknowledgement, replay.Receipt!.CentralAcknowledgement);
        Assert.Equal(first.Receipt.AcceptedAt, replay.Receipt.AcceptedAt);
        Assert.Single(await Outbox().Find(FilterDefinition<BsonDocument>.Empty).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentExactSubmissions_ProduceOneAcceptedAndDuplicateReceipts()
    {
        var envelope = TrustedSourceAuditIntentTestData.Envelope(intentId: Guid.NewGuid());

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _service.AcceptAsync(envelope)));

        Assert.Equal(1, results.Count(x => x.Status == TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Accepted));
        Assert.Equal(7, results.Count(x => x.Status == TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Duplicate));
        Assert.Single(results.Select(x => x.Receipt!.CentralAcknowledgement).Distinct(StringComparer.Ordinal));
        Assert.Equal(1, await Outbox().CountDocumentsAsync(new BsonDocument("IdempotencyKey", TrustedSourceAuditIntentCanonicalizer.BuildCentralIdempotencyKey(envelope))));
    }

    [Fact]
    public async Task SequentialAndConcurrentDrift_PreserveWinningRow()
    {
        var envelope = TrustedSourceAuditIntentTestData.Envelope(intentId: Guid.NewGuid());
        var accepted = await _service.AcceptAsync(envelope);
        var drift = envelope with { EvidenceHash = new string('B', 64) };

        var sequential = await _service.AcceptAsync(drift);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _service.AcceptAsync(drift)));
        var persisted = await Outbox().Find(new BsonDocument("IdempotencyKey", accepted.Receipt!.CentralIdempotencyKey)).SingleAsync();

        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.IdempotencyConflict, sequential.Status);
        Assert.All(concurrent, item => Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.IdempotencyConflict, item.Status));
        Assert.Equal(
            TrustedSourceAuditIntentCanonicalizer.ComputeFingerprint(envelope),
            persisted["Payload"].AsBsonDocument["SourceIntentFingerprint"].AsString);
    }

    [Fact]
    public async Task ResponseLossReplayAndTenantIsolation_AreDurableAndNonColliding()
    {
        var intentId = Guid.NewGuid();
        var tenantA = TrustedSourceAuditIntentTestData.Envelope(tenantId: Guid.NewGuid(), intentId: intentId);
        var tenantB = TrustedSourceAuditIntentTestData.Envelope(tenantId: Guid.NewGuid(), intentId: intentId);

        _ = await _service.AcceptAsync(tenantA); // Simulated lost HTTP response after durable insert.
        var replay = await _service.AcceptAsync(tenantA);
        var otherTenant = await _service.AcceptAsync(tenantB);

        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Duplicate, replay.Status);
        Assert.Equal(TrustedSourceAuditIntentAcceptanceResult.AcceptanceStatus.Accepted, otherTenant.Status);
        Assert.NotEqual(replay.Receipt!.CentralIdempotencyKey, otherTenant.Receipt!.CentralIdempotencyKey);
        Assert.Equal(2, await Outbox().CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Fact]
    public async Task ExistingWorker_ProducesExactlyOneMasterDataAuditEvent()
    {
        var envelope = TrustedSourceAuditIntentTestData.Envelope(intentId: Guid.NewGuid());
        await _service.AcceptAsync(envelope);
        await _service.AcceptAsync(envelope);
        var tenantContext = new TenantContext();
        var processor = new AuditOutboxProcessor(
            _repository,
            new AuditEventRepository(_database, tenantContext),
            tenantContext,
            new AuditOutboxPayloadMapper(),
            new AuditOutboxWorkerOptions
            {
                BatchSize = 10,
                MaxAttempts = 5,
                InitialRetryDelay = TimeSpan.FromSeconds(1),
                MaxRetryDelay = TimeSpan.FromMinutes(1),
                ProcessingStaleAfter = TimeSpan.FromMinutes(5)
            },
            NullLogger<AuditOutboxProcessor>.Instance);

        Assert.Equal(1, await processor.ProcessBatchAsync());

        var events = await _database.GetCollection<AuditEvent>(AuditCollectionNames.AuditEvents)
            .Find(x => x.TenantId == envelope.TenantId)
            .ToListAsync();
        var auditEvent = Assert.Single(events);
        Assert.Equal("ProductLegalEntityScopeRolloutState", auditEvent.EntityType);
        Assert.Equal("Diten.MDM", auditEvent.SourceService);
        Assert.Equal("product-item-sku-master", auditEvent.SourceModule);
        Assert.Equal(envelope.CorrelationId, auditEvent.CorrelationId);
        Assert.Equal(envelope.EvidenceHash, auditEvent.Metadata["EvidenceHash"]?.ToString());
    }

    [Fact]
    public async Task Insert_PreservesFu02TemporalStorageShape()
    {
        var envelope = TrustedSourceAuditIntentTestData.Envelope(intentId: Guid.NewGuid());
        await _service.AcceptAsync(envelope);

        var row = await Outbox().Find(new BsonDocument("IdempotencyKey", TrustedSourceAuditIntentCanonicalizer.BuildCentralIdempotencyKey(envelope))).SingleAsync();

        Assert.Equal(BsonType.Array, row["CreatedAtUtc"].BsonType);
        Assert.Equal(BsonType.Array, row["NextAttemptAtUtc"].BsonType);
        Assert.Equal(BsonType.Int64, row["CreatedAtUtcTicksV1"].BsonType);
        Assert.Equal(BsonType.Int64, row["NextAttemptAtUtcTicksV1"].BsonType);
        Assert.Equal(1, row["TemporalStorageVersion"].AsInt32);
    }

    private IMongoCollection<BsonDocument> Outbox() =>
        _database.GetCollection<BsonDocument>(AuditCollectionNames.AuditOutbox);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
