using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Eventing;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Services.Audit;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Diten.Platform.API.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using EventTransportMessage = Diten.BuildingBlocks.Eventing.EventTransportMessage;
using LegacyEventTransportMessage = Diten.Platform.Application.Contracts.Eventing.EventTransportMessage;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class PpmAuditAcceptanceMongoTests
{
    private const string StandaloneConnection =
        "mongodb://localhost:27019/?directConnection=true&serverSelectionTimeoutMS=5000";

    [Fact]
    public async Task ReplicaSetAcceptanceIsAtomicIdempotentAndCreatesOneEffectiveAuditEvent()
    {
        var client = new MongoClient(await EnsureReplicaSetInitializedAsync());
        var databaseName = $"diten_ppm_audit_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        try
        {
            var readiness = await new MongoDbReadinessHealthCheck(
                client,
                new MongoDbSettings { ConnectionString = await EnsureReplicaSetInitializedAsync(), DatabaseName = databaseName },
                Options.Create(new PpmAuditConsumerOptions { Enabled = true }))
                .CheckHealthAsync(new HealthCheckContext());
            Assert.Equal(HealthStatus.Healthy, readiness.Status);

            await MongoDbIndexConfigurations.EnsureIndexesAsync(database);
            var repository = new PpmAuditAcceptanceRepository(client, database);
            var message = Message("created");
            var intent = PpmAuditIntentParser.Parse(message);

            Assert.Equal(PpmAuditAcceptanceResult.Accepted,
                await repository.AcceptAsync(message, intent, CancellationToken.None));
            Assert.Equal(PpmAuditAcceptanceResult.Duplicate,
                await repository.AcceptAsync(message, intent, CancellationToken.None));

            var changed = Message("updated");
            var changedIntent = PpmAuditIntentParser.Parse(changed);
            await Assert.ThrowsAsync<PpmAuditPayloadConflictException>(() =>
                repository.AcceptAsync(changed, changedIntent, CancellationToken.None));

            Assert.Equal(1, await database.GetCollection<BsonDocument>("ppm_audit_inbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(1, await database.GetCollection<BsonDocument>("audit_outbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            var tenantContext = new TenantContext();
            var processor = new AuditOutboxProcessor(
                new AuditOutboxRepository(database),
                new AuditEventRepository(database, tenantContext),
                tenantContext,
                new AuditOutboxPayloadMapper(),
                new AuditOutboxWorkerOptions(),
                NullLogger<AuditOutboxProcessor>.Instance);
            Assert.Equal(1, await processor.ProcessBatchAsync());
            Assert.Equal(0, await processor.ProcessBatchAsync());
            Assert.Equal(1, await database.GetCollection<BsonDocument>("audit_events")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.False(tenantContext.IsResolved);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task AuditOutboxInsertFailureRollsBackInboxInsert()
    {
        var client = new MongoClient(await EnsureReplicaSetInitializedAsync());
        var databaseName = $"diten_ppm_audit_rollback_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        try
        {
            await MongoDbIndexConfigurations.EnsureIndexesAsync(database);
            var message = Message("created");
            var intent = PpmAuditIntentParser.Parse(message);
            await database.GetCollection<AuditOutboxMessage>("audit_outbox").InsertOneAsync(new AuditOutboxMessage
            {
                TenantId = message.TenantId!.Value,
                CorrelationId = message.CorrelationId,
                IdempotencyKey = $"ppm.audit-intent:{message.EventId:D}",
                RequestType = PpmAuditIntentParser.EventName,
                Operation = Diten.Platform.Domain.Enums.AuditOperation.Create,
                EntityType = "Project",
                EntityId = Guid.Parse("44444444-4444-4444-4444-444444444444")
            });

            await Assert.ThrowsAnyAsync<Exception>(() =>
                new PpmAuditAcceptanceRepository(client, database)
                    .AcceptAsync(message, intent, CancellationToken.None));

            Assert.Equal(0, await database.GetCollection<BsonDocument>("ppm_audit_inbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task ConcurrentSameEventRaceProducesOneAcceptanceAndOneDuplicate()
    {
        var connection = await EnsureReplicaSetInitializedAsync();
        var client = new MongoClient(connection);
        var databaseName = $"diten_ppm_audit_race_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        try
        {
            await MongoDbIndexConfigurations.EnsureIndexesAsync(database);
            var message = Message("created");
            var intent = PpmAuditIntentParser.Parse(message);
            var first = new PpmAuditAcceptanceRepository(client, database);
            var second = new PpmAuditAcceptanceRepository(client, database);

            var results = await Task.WhenAll(
                AcceptWithTransportRetryAsync(first, message, intent),
                AcceptWithTransportRetryAsync(second, message, intent));

            Assert.Contains(PpmAuditAcceptanceResult.Accepted, results);
            Assert.Contains(PpmAuditAcceptanceResult.Duplicate, results);
            Assert.Equal(1, await database.GetCollection<BsonDocument>("ppm_audit_inbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(1, await database.GetCollection<BsonDocument>("audit_outbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedAndLegacyContractsWithSameEventIdProduceOneAcceptanceAndOneAuditOutbox(
        bool sharedFirst)
    {
        var client = new MongoClient(await EnsureReplicaSetInitializedAsync());
        var databaseName = $"ppm_dual_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        try
        {
            await MongoDbIndexConfigurations.EnsureIndexesAsync(database);
            var repository = new PpmAuditAcceptanceRepository(client, database);
            var shared = Message("created");
            var mappedLegacy = LegacyEventTransportMessageMapper.Map(LegacyMessage("created"));
            var first = sharedFirst ? shared : mappedLegacy;
            var second = sharedFirst ? mappedLegacy : shared;

            Assert.Equal(
                PpmAuditAcceptanceResult.Accepted,
                await repository.AcceptAsync(
                    first,
                    PpmAuditIntentParser.Parse(first),
                    CancellationToken.None));
            Assert.Equal(
                PpmAuditAcceptanceResult.Duplicate,
                await repository.AcceptAsync(
                    second,
                    PpmAuditIntentParser.Parse(second),
                    CancellationToken.None));

            Assert.Equal(1, await database.GetCollection<BsonDocument>("ppm_audit_inbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(1, await database.GetCollection<BsonDocument>("audit_outbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            var persistedInbox = await database.GetCollection<PpmAuditInboxMessage>("ppm_audit_inbox")
                .Find(FilterDefinition<PpmAuditInboxMessage>.Empty)
                .SingleAsync();
            Assert.Equal(PpmAuditAcceptanceRepository.ConsumerName, persistedInbox.ConsumerName);
            Assert.Equal(first.EventId.ToString("D"), persistedInbox.EventId);
            Assert.Equal(PpmAuditIntentParser.Parse(first).PayloadSha256, persistedInbox.PayloadSha256);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SharedAndLegacyContractsWithSameEventIdButDifferentPayloadFailClosed(
        bool sharedFirst)
    {
        var client = new MongoClient(await EnsureReplicaSetInitializedAsync());
        var databaseName = $"diten_ppm_dc_conf_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        try
        {
            await MongoDbIndexConfigurations.EnsureIndexesAsync(database);
            var repository = new PpmAuditAcceptanceRepository(client, database);
            var shared = Message(sharedFirst ? "created" : "updated");
            var mappedLegacy = LegacyEventTransportMessageMapper.Map(
                LegacyMessage(sharedFirst ? "updated" : "created"));
            var first = sharedFirst ? shared : mappedLegacy;
            var second = sharedFirst ? mappedLegacy : shared;

            Assert.Equal(
                PpmAuditAcceptanceResult.Accepted,
                await repository.AcceptAsync(
                    first,
                    PpmAuditIntentParser.Parse(first),
                    CancellationToken.None));
            await Assert.ThrowsAsync<PpmAuditPayloadConflictException>(() =>
                repository.AcceptAsync(
                    second,
                    PpmAuditIntentParser.Parse(second),
                    CancellationToken.None));

            Assert.Equal(1, await database.GetCollection<BsonDocument>("ppm_audit_inbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(1, await database.GetCollection<BsonDocument>("audit_outbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<PpmAuditAcceptanceResult> AcceptWithTransportRetryAsync(
        PpmAuditAcceptanceRepository repository,
        EventTransportMessage message,
        PpmAuditIntent intent)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await repository.AcceptAsync(message, intent, CancellationToken.None);
            }
            catch (MongoException exception) when (
                attempt < PpmAuditRetryPolicy.RetryCount
                && exception.HasErrorLabel("TransientTransactionError"))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)));
            }
        }
    }

    [SkippableFact]
    public async Task StandaloneMongoFailsClosedWithoutPartialWrites()
    {
        var client = new MongoClient(StandaloneConnection);
        Skip.IfNot(
            await IsReachableAsync(client),
            "Standalone Mongo evidence requires an explicitly managed process on localhost:27019; SKIP is not PASS.");
        var databaseName = $"diten_ppm_audit_standalone_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        try
        {
            var readiness = await new MongoDbReadinessHealthCheck(
                client,
                new MongoDbSettings { ConnectionString = StandaloneConnection, DatabaseName = databaseName },
                Options.Create(new PpmAuditConsumerOptions { Enabled = true }))
                .CheckHealthAsync(new HealthCheckContext());
            Assert.Equal(HealthStatus.Unhealthy, readiness.Status);

            var message = Message("created");
            var intent = PpmAuditIntentParser.Parse(message);
            await Assert.ThrowsAnyAsync<Exception>(() =>
                new PpmAuditAcceptanceRepository(client, database)
                    .AcceptAsync(message, intent, CancellationToken.None));
            Assert.Equal(0, await database.GetCollection<BsonDocument>("ppm_audit_inbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(0, await database.GetCollection<BsonDocument>("audit_outbox")
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }
        finally
        {
            try
            {
                await client.DropDatabaseAsync(databaseName);
            }
            catch (Exception)
            {
                // The standalone process may be intentionally stopped by the environment while the test is
                // completing. Cleanup must not replace the actual fail-closed assertion result.
            }
        }
    }

    private static async Task<bool> IsReachableAsync(IMongoClient client)
    {
        try
        {
            await client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<string> EnsureReplicaSetInitializedAsync()
    {
        var direct = new MongoClient("mongodb://localhost:27018/?directConnection=true&serverSelectionTimeoutMS=5000");
        var admin = direct.GetDatabase("admin");
        var hello = await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
        if (!hello.Contains("setName"))
        {
            try
            {
                await admin.RunCommandAsync<BsonDocument>(new BsonDocument
                {
                    ["replSetInitiate"] = new BsonDocument
                    {
                        ["_id"] = "ppm-audit-rs",
                        ["members"] = new BsonArray
                        {
                            new BsonDocument { ["_id"] = 0, ["host"] = "localhost:27018" }
                        }
                    }
                });
            }
            catch (MongoCommandException exception) when (exception.CodeName == "AlreadyInitialized")
            {
            }
        }

        for (var attempt = 0; attempt < 50; attempt++)
        {
            hello = await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
            if (hello.TryGetValue("isWritablePrimary", out var primary) && primary.ToBoolean())
            {
                var setName = hello["setName"].AsString;
                return $"mongodb://localhost:27018/?replicaSet={Uri.EscapeDataString(setName)}&serverSelectionTimeoutMS=5000";
            }
            await Task.Delay(100);
        }

        throw new InvalidOperationException("Mongo replica set did not become writable primary.");
    }

    private static EventTransportMessage Message(string mutation) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            PpmAuditIntentParser.EventName,
            1,
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            null,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            PpmAuditIntentParser.Producer,
            DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z"),
            $"{{\"actorId\":\"22222222-2222-2222-2222-222222222222\",\"auditIntentId\":\"11111111-1111-1111-1111-111111111111\",\"entityId\":\"44444444-4444-4444-4444-444444444444\",\"entityType\":\"Project\",\"mutation\":\"{mutation}\",\"occurredAtUtc\":\"2026-07-30T10:20:30.0000000Z\"}}");

#pragma warning disable CS0618 // Required compatibility input for the conditional shared-contract migration proof.
    private static LegacyEventTransportMessage LegacyMessage(string mutation) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            PpmAuditIntentParser.EventName,
            1,
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            null,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            PpmAuditIntentParser.Producer,
            DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z"),
            $"{{\"actorId\":\"22222222-2222-2222-2222-222222222222\",\"auditIntentId\":\"11111111-1111-1111-1111-111111111111\",\"entityId\":\"44444444-4444-4444-4444-444444444444\",\"entityType\":\"Project\",\"mutation\":\"{mutation}\",\"occurredAtUtc\":\"2026-07-30T10:20:30.0000000Z\"}}");
#pragma warning restore CS0618
}
