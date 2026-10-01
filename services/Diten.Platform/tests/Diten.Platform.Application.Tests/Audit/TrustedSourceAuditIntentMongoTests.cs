using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Tests.Persistence;
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
    /*
     * DB-010 / BL-395 — a FIXED-name scoped database on the shared mongod, taken through the harness: it holds the
     * machine-wide lock and empties the scope on open. The scope is database-global on purpose: the outbox worker
     * under test drains the WHOLE outbox, so a tenant id cannot isolate it, and several assertions below count
     * every row. Until 2026-10-01 this class named a database per run (fu01_<guid>) and dropped it on dispose,
     * without the lock — both guards (MongoTestDatabaseGuardTests, PlatformMongoTestLockTests) were red for it.
     */
    private MongoIntegrationHarness _harness = null!;
    private IMongoDatabase _database = null!;
    private AuditOutboxRepository _repository = null!;
    private TrustedSourceAuditIntentAcceptanceService _service = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync(
            "trusted_source_audit_intent",
            SchemaProfile.AccessGovernance);
        _database = _harness.Database;
        _repository = new AuditOutboxRepository(
            _database,
            new AuditOutboxTemporalMigrationRepository(_database));
        _service = new(_repository, new FixedTimeProvider(TrustedSourceAuditIntentTestData.Now));
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

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
