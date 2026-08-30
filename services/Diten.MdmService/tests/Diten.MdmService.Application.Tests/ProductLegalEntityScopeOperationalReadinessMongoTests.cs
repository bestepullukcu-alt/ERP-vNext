using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductLegalEntityScopes;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeOperationalReadinessMongoTests
{
    private const string DatabaseName = "diten_mdm_product_scope_operational_tests";
    private static readonly string[] CollectionNames =
    [
        "mdm_global_products",
        "mdm_product_definition_revisions",
        "mdm_gskus",
        "mdm_lskus",
        "mdm_finished_goods",
        "mdm_product_abbreviation_register",
        "mdm_product_abbreviation_allocation_ledger",
        "mdm_product_abbreviation_history",
        "mdm_product_legal_entity_scope_policies",
        "mdm_product_legal_entity_scope_rollout_states"
    ];

    [Fact]
    public async Task Canonical_identity_hashes_cover_the_full_missing_inventory_and_exclude_retired_products()
    {
        RegisterGuidSerializer();
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.GuidRepresentation = GuidRepresentation.Standard;
        var database = new MongoClient(settings).GetDatabase(DatabaseName);
        var tenantId = Guid.NewGuid();
        await CleanupTenantAsync(database, tenantId);

        try
        {
            var activeIds = Enumerable.Range(1, 201)
                .Select(index => Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"))
                .ToArray();
            var retiredId = Guid.Parse("00000000-0000-0000-0000-999999999999");
            var products = activeIds
                .Select((id, index) => ActiveDocument(id, tenantId,
                    ("CanonicalCode", $"GP-HASH-{index + 1:000}"),
                    ("CodeReservationId", GuidValue(id)),
                    ("GlobalProductNameNormalized", $"GP HASH {index + 1:000}"),
                    ("LifecycleStatus", (int)ProductIdentityLifecycleStatus.Draft)))
                .Append(ActiveDocument(retiredId, tenantId,
                    ("CanonicalCode", "GP-HASH-RETIRED"),
                    ("CodeReservationId", GuidValue(retiredId)),
                    ("GlobalProductNameNormalized", "GP HASH RETIRED"),
                    ("LifecycleStatus", (int)ProductIdentityLifecycleStatus.Retired)))
                .ToArray();
            var collection = database.GetCollection<BsonDocument>("mdm_global_products");
            await collection.InsertManyAsync(products);

            var context = new TenantContext();
            context.SetTenant(tenantId);
            var readiness = new ProductLegalEntityScopeOperationalReadinessRepository(database, context);
            var productsRepository = new GlobalProductRepository(database, context);
            var observedAt = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
            var request = new ProductLegalEntityScopeInventorySnapshotRequest(
                tenantId,
                Guid.NewGuid(),
                ProductLegalEntityScopeRolloutMode.Preparation,
                0,
                "SuspendFailClosed",
                Guid.NewGuid(),
                Guid.NewGuid(),
                "OWNER_APPROVED",
                observedAt);

            var withRetired = await readiness.CaptureInventorySnapshotAsync(request);
            var sampleBefore = await productsRepository
                .GetProductLegalEntityScopeCompletenessInventoryAsync(observedAt, 200);
            Assert.Equal(201, sampleBefore.EligibleGlobalProductCount);
            Assert.Equal(200, sampleBefore.MissingGlobalProductIds.Count);
            Assert.DoesNotContain(retiredId, sampleBefore.MissingGlobalProductIds);

            await collection.DeleteOneAsync(new BsonDocument
            {
                { "TenantId", GuidValue(tenantId) },
                { "_id", GuidValue(retiredId) }
            });
            var withoutRetired = await readiness.CaptureInventorySnapshotAsync(request);
            Assert.Equal(Fact(withRetired, "eligibleGlobalProductCount"),
                Fact(withoutRetired, "eligibleGlobalProductCount"));
            Assert.Equal(Fact(withRetired, "eligibleGlobalProductHash"),
                Fact(withoutRetired, "eligibleGlobalProductHash"));
            Assert.Equal(Fact(withRetired, "missingGlobalProductHash"),
                Fact(withoutRetired, "missingGlobalProductHash"));

            var outsideSample = Assert.Single(activeIds.Except(sampleBefore.MissingGlobalProductIds));
            var replacement = Guid.Parse("00000000-0000-0000-0000-999999999998");
            await collection.DeleteOneAsync(new BsonDocument
            {
                { "TenantId", GuidValue(tenantId) },
                { "_id", GuidValue(outsideSample) }
            });
            await collection.InsertOneAsync(ActiveDocument(replacement, tenantId,
                ("CanonicalCode", "GP-HASH-REPLACEMENT"),
                ("CodeReservationId", GuidValue(replacement)),
                ("GlobalProductNameNormalized", "GP HASH REPLACEMENT"),
                ("LifecycleStatus", (int)ProductIdentityLifecycleStatus.Draft)));

            var changedOutsideSample = await readiness.CaptureInventorySnapshotAsync(request);
            var sampleAfter = await productsRepository
                .GetProductLegalEntityScopeCompletenessInventoryAsync(observedAt, 200);
            Assert.Equal(sampleBefore.MissingGlobalProductIds, sampleAfter.MissingGlobalProductIds);
            Assert.Equal("201", Fact(changedOutsideSample, "eligibleGlobalProductCount"));
            Assert.NotEqual(Fact(withoutRetired, "eligibleGlobalProductHash"),
                Fact(changedOutsideSample, "eligibleGlobalProductHash"));
            Assert.NotEqual(Fact(withoutRetired, "missingGlobalProductHash"),
                Fact(changedOutsideSample, "missingGlobalProductHash"));
        }
        finally
        {
            await CleanupTenantAsync(database, tenantId);
        }
    }

    [Fact]
    public async Task Inspect_ReportsBoundedTenantSafeIntegrityAndRawAuditFactsWithoutMutation()
    {
        RegisterGuidSerializer();
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.GuidRepresentation = GuidRepresentation.Standard;
        var database = new MongoClient(settings).GetDatabase(DatabaseName);
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        await CleanupTenantAsync(database, tenantId);
        await CleanupTenantAsync(database, foreignTenantId);

        try
        {
            var globalProductId = Guid.NewGuid();
            var foreignGlobalProductId = Guid.NewGuid();
            await InsertAsync(database, "mdm_global_products",
                ActiveDocument(globalProductId, tenantId),
                ActiveDocument(foreignGlobalProductId, foreignTenantId));

            var validRevisionId = Guid.NewGuid();
            var crossTenantRevisionId = Guid.NewGuid();
            var revisions = new List<BsonDocument>
            {
                ActiveDocument(validRevisionId, tenantId, ("GlobalProductId", GuidValue(globalProductId))),
                ActiveDocument(crossTenantRevisionId, tenantId,
                    ("GlobalProductId", GuidValue(foreignGlobalProductId)))
            };
            for (var index = 0; index < 201; index++)
            {
                revisions.Add(ActiveDocument(Guid.NewGuid(), tenantId,
                    ("GlobalProductId", GuidValue(Guid.NewGuid()))));
            }
            await database.GetCollection<BsonDocument>("mdm_product_definition_revisions")
                .InsertManyAsync(revisions);

            var validGskuId = Guid.NewGuid();
            await InsertAsync(database, "mdm_gskus",
                ActiveDocument(validGskuId, tenantId,
                    ("ProductDefinitionRevisionId", GuidValue(validRevisionId))),
                ActiveDocument(Guid.NewGuid(), tenantId,
                    ("ProductDefinitionRevisionId", GuidValue(Guid.NewGuid()))));
            await InsertAsync(database, "mdm_lskus",
                ActiveDocument(Guid.NewGuid(), tenantId, ("GskuId", GuidValue(validGskuId))),
                ActiveDocument(Guid.NewGuid(), tenantId, ("GskuId", GuidValue(Guid.NewGuid()))));
            await InsertAsync(database, "mdm_finished_goods",
                ActiveDocument(Guid.NewGuid(), tenantId, ("GskuId", GuidValue(validGskuId))),
                ActiveDocument(Guid.NewGuid(), tenantId, ("GskuId", GuidValue(Guid.NewGuid()))));
            foreach (var collectionName in CollectionNames.Where(name => name.StartsWith(
                         "mdm_product_abbreviation_", StringComparison.Ordinal)))
            {
                await InsertAsync(database, collectionName,
                    ActiveDocument(Guid.NewGuid(), tenantId,
                        ("GlobalProductId", GuidValue(globalProductId))),
                    ActiveDocument(Guid.NewGuid(), tenantId,
                        ("GlobalProductId", GuidValue(foreignGlobalProductId))));
            }

            var pendingIntentId = Guid.NewGuid();
            var deliveredIntentId = Guid.NewGuid();
            var malformedIntentId = Guid.NewGuid();
            var compactedIntentId = Guid.NewGuid();
            var malformedReceiptId = Guid.NewGuid();
            var policyId = Guid.NewGuid();
            var policy = ActiveDocument(policyId, tenantId,
                ("AuditIntents", new BsonArray
                {
                    Intent(pendingIntentId, tenantId, policyId,
                        AuditAggregateType.ProductLegalEntityScopePolicy, AuditIntentDeliveryState.Pending),
                    Intent(deliveredIntentId, tenantId, policyId,
                        AuditAggregateType.ProductLegalEntityScopePolicy, AuditIntentDeliveryState.Delivered,
                        acknowledged: true),
                    Intent(malformedIntentId, tenantId, policyId,
                        AuditAggregateType.ProductLegalEntityScopeRolloutState, AuditIntentDeliveryState.DeadLetter)
                }),
                ("AuditIntentReceipts", new BsonArray
                {
                    Receipt(compactedIntentId, tenantId),
                    Receipt(malformedReceiptId, tenantId, malformed: true)
                }));
            await InsertAsync(database, "mdm_product_legal_entity_scope_policies", policy);

            var rolloutId = Guid.NewGuid();
            await InsertAsync(database, "mdm_product_legal_entity_scope_rollout_states",
                ActiveDocument(rolloutId, tenantId,
                    ("Mode", (int)ProductLegalEntityScopeRolloutMode.Preparation),
                    ("CreationCommandId", GuidValue(Guid.NewGuid())),
                    ("CreatedByActorId", GuidValue(Guid.NewGuid())),
                    ("AuditIntents", new BsonArray
                    {
                        Intent(Guid.NewGuid(), tenantId, rolloutId,
                            AuditAggregateType.ProductLegalEntityScopeRolloutState,
                            AuditIntentDeliveryState.Processing)
                    }),
                    ("AuditIntentReceipts", new BsonArray())));

            var before = await SnapshotCountsAsync(database, tenantId);
            var context = new TenantContext();
            context.SetTenant(tenantId);
            var repository = new ProductLegalEntityScopeOperationalReadinessRepository(database, context);

            var report = await repository.InspectAsync(200);
            var after = await SnapshotCountsAsync(database, tenantId);

            Assert.Equal(before, after);
            var revision = Assert.Single(report.DescendantIntegrity,
                category => category.Category == "ProductDefinitionRevision");
            Assert.Equal(203, revision.TotalCount);
            Assert.Equal(202, revision.OrphanCount);
            Assert.Equal(200, revision.OrphanIds.Count);
            Assert.True(revision.HasMore);
            foreach (var category in report.DescendantIntegrity.Where(item =>
                         item.Category != "ProductDefinitionRevision"))
            {
                Assert.Equal(2, category.TotalCount);
                Assert.Equal(1, category.OrphanCount);
                Assert.Single(category.OrphanIds);
                Assert.False(category.HasMore);
            }
            Assert.DoesNotContain(report.DescendantIntegrity.SelectMany(item => item.OrphanIds),
                id => id == foreignGlobalProductId);

            var policyAudit = Assert.Single(report.Audit,
                category => category.AggregateType == AuditAggregateType.ProductLegalEntityScopePolicy);
            Assert.Equal(1, policyAudit.PendingCount);
            Assert.Equal(1, policyAudit.DeliveredCount);
            Assert.Equal(0, policyAudit.DeadLetterCount);
            Assert.Equal(1, policyAudit.CompactedReceiptCount);
            Assert.Equal(2, policyAudit.MalformedCount);
            Assert.Contains(malformedIntentId, policyAudit.MalformedIntentIds);
            Assert.Contains(malformedReceiptId, policyAudit.MalformedIntentIds);
            Assert.Equal(1, policyAudit.UnacknowledgedCount);

            var rolloutAudit = Assert.Single(report.Audit,
                category => category.AggregateType == AuditAggregateType.ProductLegalEntityScopeRolloutState);
            Assert.Equal(1, rolloutAudit.ProcessingCount);
            Assert.Equal(1, rolloutAudit.UnacknowledgedCount);
        }
        finally
        {
            await CleanupTenantAsync(database, tenantId);
            await CleanupTenantAsync(database, foreignTenantId);
        }
    }

    [Fact]
    public async Task BootstrapPreparation_RealMongoCreatesOneStateAndReplayDoesNotMutateBusinessData()
    {
        RegisterGuidSerializer();
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.GuidRepresentation = GuidRepresentation.Standard;
        var database = new MongoClient(settings).GetDatabase(DatabaseName);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        await CleanupTenantAsync(database, tenantId);

        try
        {
            var services = new ServiceCollection();
            var tenantContext = new TenantContext();
            services.AddSingleton<ITenantContext>(tenantContext);
            services.AddSingleton(database);
            services.AddScoped<IProductLegalEntityScopeRolloutStateRepository,
                ProductLegalEntityScopeRolloutStateRepository>();
            services.AddScoped<IGlobalProductRepository, GlobalProductRepository>();
            services.AddScoped<IProductLegalEntityScopeOperationalReadinessRepository,
                ProductLegalEntityScopeOperationalReadinessRepository>();
            await using var provider = services.BuildServiceProvider();
            var runner = new ProductLegalEntityScopeOperationalRunner(
                new TestEnvironment(Environments.Development),
                Options.Create(new ProductLegalEntityScopeOperationalOptions
                {
                    Enabled = true,
                    Action = ProductLegalEntityScopeOperationalRunner.BootstrapPreparationAction,
                    TenantId = tenantId,
                    ActorId = actorId,
                    CommandId = commandId
                }),
                provider.GetRequiredService<IServiceScopeFactory>(),
                new FixedTimeProvider(new DateTimeOffset(2026, 8, 27, 13, 0, 0, TimeSpan.Zero)));

            var first = await runner.RunAsync();
            var replay = await runner.RunAsync();

            Assert.True(first.WasCreated);
            Assert.False(replay.WasCreated);
            Assert.Equal(first.Rollout.Id, replay.Rollout.Id);
            var rolloutDocuments = database.GetCollection<BsonDocument>(
                "mdm_product_legal_entity_scope_rollout_states");
            var persisted = await rolloutDocuments.Find(new BsonDocument("TenantId", GuidValue(tenantId)))
                .ToListAsync();
            var state = Assert.Single(persisted);
            Assert.Equal((int)ProductLegalEntityScopeRolloutMode.Preparation, state["Mode"].AsInt32);
            Assert.Equal(commandId, state["CreationCommandId"].AsGuid);
            Assert.Equal(actorId, state["CreatedByActorId"].AsGuid);
            Assert.Empty(state["AuditIntents"].AsBsonArray);
            Assert.Empty(state["AuditIntentReceipts"].AsBsonArray);
            Assert.Equal(0, await database.GetCollection<BsonDocument>(
                    "mdm_product_legal_entity_scope_policies")
                .CountDocumentsAsync(new BsonDocument("TenantId", GuidValue(tenantId))));
        }
        finally
        {
            await CleanupTenantAsync(database, tenantId);
        }
    }

    [Fact]
    public async Task BootstrapPreparation_ConcurrentSameFactsCreatesOnceAndReportsOneReplay()
    {
        RegisterGuidSerializer();
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.GuidRepresentation = GuidRepresentation.Standard;
        var database = new MongoClient(settings).GetDatabase(DatabaseName);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        await CleanupTenantAsync(database, tenantId);

        try
        {
            var tenantContext = new TenantContext();
            tenantContext.SetTenant(tenantId);
            var persistedRepository = new ProductLegalEntityScopeRolloutStateRepository(database, tenantContext);
            var racingRepository = new RacingRolloutRepository(persistedRepository);
            var services = new ServiceCollection();
            services.AddSingleton<ITenantContext>(tenantContext);
            services.AddSingleton<IProductLegalEntityScopeRolloutStateRepository>(racingRepository);
            services.AddSingleton(database);
            services.AddScoped<IGlobalProductRepository, GlobalProductRepository>();
            services.AddScoped<IProductLegalEntityScopeOperationalReadinessRepository,
                ProductLegalEntityScopeOperationalReadinessRepository>();
            await using var provider = services.BuildServiceProvider();
            var options = Options.Create(new ProductLegalEntityScopeOperationalOptions
            {
                Enabled = true,
                Action = ProductLegalEntityScopeOperationalRunner.BootstrapPreparationAction,
                TenantId = tenantId,
                ActorId = actorId,
                CommandId = commandId
            });
            var firstRunner = new ProductLegalEntityScopeOperationalRunner(
                new TestEnvironment(Environments.Development), options,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new FixedTimeProvider(new DateTimeOffset(2026, 8, 27, 13, 30, 0, TimeSpan.Zero)));
            var secondRunner = new ProductLegalEntityScopeOperationalRunner(
                new TestEnvironment(Environments.Development), options,
                provider.GetRequiredService<IServiceScopeFactory>(),
                new FixedTimeProvider(new DateTimeOffset(2026, 8, 27, 13, 30, 0, TimeSpan.Zero)));

            var results = await Task.WhenAll(firstRunner.RunAsync(), secondRunner.RunAsync());

            Assert.Equal(1, results.Count(result => result.WasCreated));
            Assert.Equal(1, results.Count(result => !result.WasCreated));
            Assert.Single(results.Select(result => result.Rollout.Id).Distinct());
            var count = await database.GetCollection<BsonDocument>(
                    "mdm_product_legal_entity_scope_rollout_states")
                .CountDocumentsAsync(new BsonDocument("TenantId", GuidValue(tenantId)));
            Assert.Equal(1, count);
        }
        finally
        {
            await CleanupTenantAsync(database, tenantId);
        }
    }

    [Fact]
    public async Task BootstrapPreparation_MalformedPersistedRolloutFailsClosedWithoutMutation()
    {
        RegisterGuidSerializer();
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.GuidRepresentation = GuidRepresentation.Standard;
        var database = new MongoClient(settings).GetDatabase(DatabaseName);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        await CleanupTenantAsync(database, tenantId);

        try
        {
            await InsertAsync(database, "mdm_product_legal_entity_scope_rollout_states",
                ActiveDocument(Guid.NewGuid(), tenantId,
                    ("Version", -1),
                    ("Mode", (int)ProductLegalEntityScopeRolloutMode.Preparation),
                    ("CreationCommandId", GuidValue(commandId)),
                    ("CreatedByActorId", GuidValue(actorId)),
                    ("AuditIntents", new BsonArray()),
                    ("AuditIntentReceipts", new BsonArray())));
            var before = await SnapshotCountsAsync(database, tenantId);
            var services = new ServiceCollection();
            var tenantContext = new TenantContext();
            services.AddSingleton<ITenantContext>(tenantContext);
            services.AddSingleton(database);
            services.AddScoped<IProductLegalEntityScopeRolloutStateRepository,
                ProductLegalEntityScopeRolloutStateRepository>();
            services.AddScoped<IGlobalProductRepository, GlobalProductRepository>();
            services.AddScoped<IProductLegalEntityScopeOperationalReadinessRepository,
                ProductLegalEntityScopeOperationalReadinessRepository>();
            await using var provider = services.BuildServiceProvider();
            var runner = new ProductLegalEntityScopeOperationalRunner(
                new TestEnvironment(Environments.Development),
                Options.Create(new ProductLegalEntityScopeOperationalOptions
                {
                    Enabled = true,
                    Action = ProductLegalEntityScopeOperationalRunner.BootstrapPreparationAction,
                    TenantId = tenantId,
                    ActorId = actorId,
                    CommandId = commandId
                }),
                provider.GetRequiredService<IServiceScopeFactory>(),
                new FixedTimeProvider(new DateTimeOffset(2026, 8, 27, 14, 0, 0, TimeSpan.Zero)));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync());

            Assert.Equal("PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_STATE_INVALID", error.Message);
            Assert.Equal(before, await SnapshotCountsAsync(database, tenantId));
        }
        finally
        {
            await CleanupTenantAsync(database, tenantId);
        }
    }

    [Fact]
    public async Task Inspect_MalformedRolloutProjectionFailsClosedDeterministically()
    {
        RegisterGuidSerializer();
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.GuidRepresentation = GuidRepresentation.Standard;
        var database = new MongoClient(settings).GetDatabase(DatabaseName);
        var tenantId = Guid.NewGuid();
        await CleanupTenantAsync(database, tenantId);

        try
        {
            await InsertAsync(database, "mdm_product_legal_entity_scope_rollout_states",
                ActiveDocument(Guid.NewGuid(), tenantId,
                    ("Version", -1),
                    ("Mode", (int)ProductLegalEntityScopeRolloutMode.Preparation),
                    ("CreationCommandId", GuidValue(Guid.NewGuid())),
                    ("CreatedByActorId", GuidValue(Guid.NewGuid())),
                    ("AuditIntents", new BsonArray()),
                    ("AuditIntentReceipts", new BsonArray())));
            var context = new TenantContext();
            context.SetTenant(tenantId);
            var repository = new ProductLegalEntityScopeOperationalReadinessRepository(database, context);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InspectAsync(200));

            Assert.Equal("PRODUCT_SCOPE_OPERATIONAL_ROLLOUT_STATE_INVALID", error.Message);
        }
        finally
        {
            await CleanupTenantAsync(database, tenantId);
        }
    }

    private static BsonDocument ActiveDocument(
        Guid id,
        Guid tenantId,
        params (string Name, BsonValue Value)[] fields)
    {
        var document = new BsonDocument
        {
            { "_id", GuidValue(id) },
            { "TenantId", GuidValue(tenantId) },
            { "IsDeleted", false },
            { "Version", 0 }
        };
        foreach (var (name, value) in fields)
        {
            document[name] = value;
        }
        return document;
    }

    private static BsonDocument Intent(
        Guid intentId,
        Guid tenantId,
        Guid aggregateId,
        AuditAggregateType aggregateType,
        AuditIntentDeliveryState deliveryState,
        bool acknowledged = false)
    {
        var timestamp = new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);
        return new BsonDocument
        {
            { "SourceService", "Diten.MDM" },
            { "SchemaVersion", 1 },
            { "ContractVersion", acknowledged ? "v1" : BsonNull.Value },
            { "IntentId", GuidValue(intentId) },
            { "TenantId", GuidValue(tenantId) },
            { "AggregateType", (int)aggregateType },
            { "AggregateId", GuidValue(aggregateId) },
            { "PreVersion", 0 },
            { "PostVersion", 1 },
            { "Operation", (int)(aggregateType == AuditAggregateType.ProductLegalEntityScopePolicy
                ? ProductAuditOperation.ProductLegalEntityScopePolicyCreated
                : ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated) },
            { "ActorId", Guid.NewGuid().ToString("D") },
            { "CorrelationId", "correlation" },
            { "CausationId", "causation" },
            { "CommandId", Guid.NewGuid().ToString("D") },
            { "Sequence", 2L },
            { "TimestampUtc", DateTimeOffsetValue(timestamp) },
            { "EvidenceHash", "evidence" },
            { "SnapshotReference", "snapshot" },
            { "IdempotencyKey", $"intent:{intentId:D}" },
            { "DeliveryState", (int)deliveryState },
            { "AttemptCount", 0 },
            { "CentralAcknowledgement", acknowledged ? "ack" : BsonNull.Value },
            { "CentralIdempotencyKey", acknowledged ? "central-key" : BsonNull.Value },
            { "AcknowledgedContractVersion", acknowledged ? "v1" : BsonNull.Value }
        };
    }

    private static BsonDocument Receipt(Guid intentId, Guid tenantId, bool malformed = false)
    {
        var acknowledgedAt = new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);
        var deliveredAt = acknowledgedAt.AddMinutes(1);
        var compactedAt = deliveredAt.AddMinutes(1);
        return new BsonDocument
        {
            { "SourceService", "Diten.MDM" },
            { "IntentId", GuidValue(intentId) },
            { "TenantId", GuidValue(tenantId) },
            { "IdempotencyKey", $"intent:{intentId:D}" },
            { "CentralAcknowledgement", "ack" },
            { "CentralIdempotencyKey", "central-key" },
            { "ContractVersion", "v1" },
            { "AcknowledgedAt", DateTimeOffsetValue(acknowledgedAt) },
            { "DeliveredAt", DateTimeOffsetValue(deliveredAt) },
            { "CompactedAt", DateTimeOffsetValue(compactedAt) },
            { "CompactReceiptReference", "receipt" },
            { "EvidenceHash", malformed ? "" : "evidence" }
        };
    }

    private static BsonArray DateTimeOffsetValue(DateTimeOffset value)
        => new() { value.Ticks, (int)value.Offset.TotalMinutes };

    private static BsonBinaryData GuidValue(Guid value)
        => new(value, GuidRepresentation.Standard);

    private static string Fact(ProductLegalEntityScopeInventorySnapshot snapshot, string key)
        => snapshot.CanonicalPayload.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith(key + "=", StringComparison.Ordinal))[(key.Length + 1)..];

    private static Task InsertAsync(IMongoDatabase database, string collectionName, params BsonDocument[] documents)
        => database.GetCollection<BsonDocument>(collectionName).InsertManyAsync(documents);

    private static async Task<IReadOnlyList<long>> SnapshotCountsAsync(IMongoDatabase database, Guid tenantId)
    {
        var tenant = GuidValue(tenantId);
        var result = new List<long>();
        foreach (var collectionName in CollectionNames)
        {
            result.Add(await database.GetCollection<BsonDocument>(collectionName)
                .CountDocumentsAsync(new BsonDocument("TenantId", tenant)));
        }
        return result;
    }

    private static async Task CleanupTenantAsync(IMongoDatabase database, Guid tenantId)
    {
        var filter = new BsonDocument("TenantId", GuidValue(tenantId));
        foreach (var collectionName in CollectionNames)
        {
            await database.GetCollection<BsonDocument>(collectionName).DeleteManyAsync(filter);
        }
    }

    private static void RegisterGuidSerializer()
    {
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (BsonSerializationException)
        {
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RacingRolloutRepository(
        IProductLegalEntityScopeRolloutStateRepository inner)
        : IProductLegalEntityScopeRolloutStateRepository
    {
        private readonly TaskCompletionSource _initialReadsReleased =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _initialReadCount;

        public async Task<Diten.MdmService.Domain.Entities.ProductLegalEntityScopeRolloutState?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            var read = Interlocked.Increment(ref _initialReadCount);
            if (read <= 2)
            {
                if (read == 2)
                {
                    _initialReadsReleased.TrySetResult();
                }
                await _initialReadsReleased.Task.WaitAsync(cancellationToken);
                return null;
            }
            return await inner.GetAsync(cancellationToken);
        }

        public Task<Diten.MdmService.Domain.Entities.ProductLegalEntityScopeRolloutState?>
            GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default)
            => inner.GetByCreationCommandIdAsync(creationCommandId, cancellationToken);

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(
            Diten.MdmService.Domain.Entities.ProductLegalEntityScopeRolloutState state,
            CancellationToken cancellationToken = default)
            => inner.CreateAsync(state, cancellationToken);

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(
            Diten.MdmService.Domain.Entities.ProductLegalEntityScopeRolloutState state,
            int expectedVersion,
            CancellationToken cancellationToken = default)
            => inner.UpdateAsync(state, expectedVersion, cancellationToken);
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.MdmService.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
