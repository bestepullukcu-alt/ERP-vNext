using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services.Audit;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX2 A3(b) — the outbox's "already delivered?" question is asked by the message's
/// idempotency key, against the production index built for it. Measured on a real server: the answer by key, and the
/// planner's choice for the repository's own filter (an IXSCAN on that index, never a collection scan).
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class AuditOutboxIdempotencyIndexMongoTests
{
    private static readonly Guid Tenant = Guid.Parse("70070070-0000-4000-8000-0000000000a1");

    [Fact]
    public async Task The_duplicate_check_answers_by_key_and_the_planner_uses_the_key_index()
    {
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        foreach (var collection in PlatformSchemaManifest.For(Enum.GetValues<SchemaProfile>())
                     .Where(c => c.Name == AuditCollectionNames.AuditEvents))
        {
            await collection.ApplyAsync(database, CancellationToken.None);
        }

        var correlation = Guid.NewGuid();
        await database.GetCollection<AuditEvent>(AuditCollectionNames.AuditEvents).InsertManyAsync(
            Enumerable.Range(0, 50).Select(i => new AuditEvent
            {
                TenantId = Tenant,
                CorrelationId = correlation, // one correlation for all: correlation cannot tell these apart, the key can
                Category = AuditCategory.Security,
                EntityType = "TestEntity",
                Operation = AuditOperation.Update,
                Outcome = AuditOutcome.Succeeded,
                SourceService = "Diten.Platform",
                Metadata = new Dictionary<string, object?> { [AuditOutboxPayloadMapper.OutboxIdempotencyMetadataKey] = $"key-{i}" }
            }));

        var tenantContext = new TenantContext();
        var repository = new AuditEventRepository(database, tenantContext);
        using (TenantScope.Begin(tenantContext, Tenant))
        {
            Assert.True(await repository.ExistsByOutboxIdempotencyKeyAsync("key-7"));
            Assert.False(await repository.ExistsByOutboxIdempotencyKeyAsync("key-never-written"));
        }

        var filter = AuditEventRepository.OutboxIdempotencyFilter(Tenant, "key-7")
            .Render(BsonSerializer.SerializerRegistry.GetSerializer<AuditEvent>(), BsonSerializer.SerializerRegistry);
        var explain = await database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["explain"] = new BsonDocument { ["find"] = AuditCollectionNames.AuditEvents, ["filter"] = filter, ["limit"] = 1 },
            ["verbosity"] = "queryPlanner"
        });
        var plan = explain["queryPlanner"]["winningPlan"].ToJson();

        Assert.Contains("\"IXSCAN\"", plan);
        Assert.Contains("ix_audit_events_outbox_idempotency_key", plan);
        Assert.DoesNotContain("COLLSCAN", plan);

        // FIX3 — the index is SPARSE: records written before the outbox carried the key take no room in it.
        var index = (await (await database.GetCollection<AuditEvent>(AuditCollectionNames.AuditEvents).Indexes.ListAsync()).ToListAsync())
            .Single(i => i["name"] == "ix_audit_events_outbox_idempotency_key");
        Assert.True(index.GetValue("sparse", false).ToBoolean());
    }

    [Fact]
    public async Task The_duplicate_check_sees_only_its_own_tenants_live_records()
    {
        // FIX3 — a delivery is "already done" only by a record of the SAME tenant that is not deleted: another tenant's
        // record with the same key, or a deleted one, must not make the outbox drop a delivery.
        await using var mongo = await DisposableMongoReplicaSet.StartAsync();
        var database = mongo.CreateDatabase();
        var other = Guid.NewGuid();
        await database.GetCollection<AuditEvent>(AuditCollectionNames.AuditEvents).InsertManyAsync(
        [
            Event(other, "shared-key", deleted: false),
            Event(Tenant, "deleted-key", deleted: true),
            Event(Tenant, "live-key", deleted: false)
        ]);
        var tenantContext = new TenantContext();
        var repository = new AuditEventRepository(database, tenantContext);

        using (TenantScope.Begin(tenantContext, Tenant))
        {
            Assert.False(await repository.ExistsByOutboxIdempotencyKeyAsync("shared-key"));
            Assert.False(await repository.ExistsByOutboxIdempotencyKeyAsync("deleted-key"));
            Assert.True(await repository.ExistsByOutboxIdempotencyKeyAsync("live-key"));
        }
    }

    private static AuditEvent Event(Guid tenant, string key, bool deleted) => new()
    {
        TenantId = tenant,
        CorrelationId = Guid.NewGuid(),
        Category = AuditCategory.Security,
        EntityType = "TestEntity",
        Operation = AuditOperation.Update,
        Outcome = AuditOutcome.Succeeded,
        SourceService = "Diten.Platform",
        IsDeleted = deleted,
        Metadata = new Dictionary<string, object?> { [AuditOutboxPayloadMapper.OutboxIdempotencyMetadataKey] = key }
    };
}
