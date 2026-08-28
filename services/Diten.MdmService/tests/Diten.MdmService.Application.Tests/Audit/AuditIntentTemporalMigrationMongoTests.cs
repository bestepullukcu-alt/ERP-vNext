using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class AuditIntentTemporalMigrationMongoTests(
    AuditIntentTemporalMongoFixture mongo) :
    IAsyncLifetime,
    IClassFixture<AuditIntentTemporalMongoFixture>
{
    private static readonly (string Collection, AuditAggregateType Type)[] AggregateCollections =
    [
        ("mdm_code_reservations", AuditAggregateType.CodeReservation),
        ("mdm_global_products", AuditAggregateType.GlobalProduct),
        ("mdm_product_definition_revisions", AuditAggregateType.ProductDefinitionRevision),
        ("mdm_gskus", AuditAggregateType.Gsku),
        ("mdm_finished_goods", AuditAggregateType.FinishedGood),
        ("mdm_lskus", AuditAggregateType.Lsku),
        ("mdm_product_legal_entity_scope_policies", AuditAggregateType.ProductLegalEntityScopePolicy),
        ("mdm_product_legal_entity_scope_rollout_states", AuditAggregateType.ProductLegalEntityScopeRolloutState)
    ];

    private IMongoClient _client = null!;
    private IMongoDatabase _database = null!;
    private string _databaseName = null!;
    private readonly Guid _tenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(mongo.ReplicaConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
        _client = new MongoClient(settings);
        _databaseName = $"mdm_fu02_temporal_{Guid.NewGuid():N}";
        _database = _client.GetDatabase(_databaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
    }

    public Task DisposeAsync() => _client.DropDatabaseAsync(_databaseName);

    [Fact]
    public async Task StandaloneTopology_RunRejectsBeforeStateOrAggregateMutation()
    {
        var settings = MongoClientSettings.FromConnectionString(mongo.StandaloneConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        var databaseName = $"mdm_fu02_standalone_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        var aggregate = LegacyAggregate(
            Guid.NewGuid(),
            AuditAggregateType.ProductLegalEntityScopePolicy,
            [LegacyIntent(Guid.NewGuid(), OffsetTime(14), OffsetTime(-12), null)]);
        var collection = database.GetCollection<BsonDocument>("mdm_global_products");
        await collection.InsertOneAsync(aggregate);
        var before = await collection.Find(new BsonDocument("_id", aggregate["_id"])).SingleAsync();

        try
        {
            var repository = Repository(database);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.RunAsync(Request()));

            Assert.Equal("AUDIT_INTENT_TEMPORAL_REPLICA_SET_REQUIRED", error.Message);
            Assert.Equal(0, await database.GetCollection<BsonDocument>(
                AuditIntentTemporalMigrationRepository.StateCollectionName)
                .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(before, await collection.Find(new BsonDocument("_id", aggregate["_id"])).SingleAsync());
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [Theory]
    [InlineData("object")]
    [InlineData("scalar")]
    [InlineData("bad-element")]
    public async Task Preflight_MalformedAuditIntentContainer_FailsBeforeStateOrAggregateMutation(string shape)
    {
        var aggregate = LegacyAggregate(Guid.NewGuid(), AuditAggregateType.CodeReservation, []);
        aggregate["AuditIntents"] = shape switch
        {
            "object" => new BsonDocument("IntentId", "not-an-array"),
            "scalar" => 17,
            _ => new BsonArray { "not-a-document" }
        };
        var collection = _database.GetCollection<BsonDocument>("mdm_code_reservations");
        await collection.InsertOneAsync(aggregate);
        var before = await collection.Find(new BsonDocument("_id", aggregate["_id"])).SingleAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Repository(_database).RunAsync(Request()));

        Assert.Equal("AUDIT_INTENT_TEMPORAL_CONTAINER_INVALID", error.Message);
        Assert.Null(await Repository(_database).GetValidatedStateAsync());
        Assert.Equal(before, await collection.Find(new BsonDocument("_id", aggregate["_id"])).SingleAsync());
    }

    [Fact]
    public async Task EmptyAuditIntentArray_IsValidAndDoesNotCreatePhantomCounters()
    {
        var aggregate = LegacyAggregate(Guid.NewGuid(), AuditAggregateType.CodeReservation, []);
        await _database.GetCollection<BsonDocument>("mdm_code_reservations").InsertOneAsync(aggregate);

        var result = await Repository(_database).RunAsync(Request(verify: true));

        Assert.Equal(AuditIntentTemporalMigrationState.Phases.CompletionVerified, result.Phase);
        Assert.Equal(0, result.ScannedCount);
        Assert.Equal(0, result.MigratedCount);
        Assert.Equal(0, result.AlreadyCurrentCount);
    }

    [Fact]
    public async Task Verification_MalformedContainerAfterCompletion_BlocksActivationWithoutMutation()
    {
        var valid = LegacyAggregate(
            Guid.NewGuid(),
            AuditAggregateType.CodeReservation,
            [CurrentIntent(Guid.NewGuid(), OffsetTime(0), null, null)]);
        var collection = _database.GetCollection<BsonDocument>("mdm_code_reservations");
        await collection.InsertOneAsync(valid);
        var completed = await Repository(_database).RunAsync(Request());
        Assert.Equal(AuditIntentTemporalMigrationState.Phases.Completed, completed.Phase);
        var malformed = LegacyAggregate(Guid.NewGuid(), AuditAggregateType.CodeReservation, []);
        malformed["AuditIntents"] = new BsonArray { 42 };
        await collection.InsertOneAsync(malformed);
        var before = await collection.Find(new BsonDocument("_id", malformed["_id"])).SingleAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Repository(_database).RunAsync(Request(verify: true, activate: true)));

        Assert.Equal("AUDIT_INTENT_TEMPORAL_CONTAINER_INVALID", error.Message);
        var state = Assert.IsType<AuditIntentTemporalMigrationState>(await Repository(_database).GetValidatedStateAsync());
        Assert.Equal(AuditIntentTemporalMigrationState.Phases.Completed, state.Phase);
        Assert.Null(state.ActivationVersion);
        Assert.Equal(before, await collection.Find(new BsonDocument("_id", malformed["_id"])).SingleAsync());
    }

    [Fact]
    public async Task Migration_MixedOffsetsAndCurrentRows_CompletesWithZeroLossAndActivates()
    {
        var ordinal = 1;
        foreach (var (collectionName, type) in AggregateCollections)
        {
            var legacy = LegacyIntent(
                GuidFromOrdinal(ordinal++),
                OffsetTime(ordinal % 2 == 0 ? 14 : -12),
                OffsetTime(ordinal % 3 == 0 ? -12 : 14),
                OffsetTime(0));
            var current = CurrentIntent(
                GuidFromOrdinal(ordinal++),
                OffsetTime(0),
                null,
                null);
            await _database.GetCollection<BsonDocument>(collectionName).InsertOneAsync(
                LegacyAggregate(GuidFromOrdinal(100 + ordinal), type, [legacy, current]));
        }

        var result = await Repository(_database).RunAsync(Request(verify: true, activate: true));

        Assert.Equal(AuditIntentTemporalMigrationState.Phases.CutoverActive, result.Phase);
        Assert.Equal(16, result.ScannedCount);
        Assert.Equal(8, result.MigratedCount);
        Assert.Equal(8, result.AlreadyCurrentCount);
        foreach (var (collectionName, _) in AggregateCollections)
        {
            var row = await _database.GetCollection<BsonDocument>(collectionName)
                .Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
            Assert.All(row["AuditIntents"].AsBsonArray, value => AssertCurrentRawIntent(value.AsBsonDocument));
        }
    }

    [Fact]
    public async Task Migration_IntentCursorCrossesBatchBoundariesWithinOneAggregate_AndPreservesSiblingsAndBusinessFields()
    {
        var aggregateId = Guid.NewGuid();
        var intents = Enumerable.Range(1, 5)
            .Select(ordinal => LegacyIntent(
                GuidFromOrdinal(ordinal),
                OffsetTime(ordinal % 2 == 0 ? 14 : -12),
                null,
                null))
            .ToArray();
        var aggregate = LegacyAggregate(aggregateId, AuditAggregateType.Gsku, intents);
        aggregate["BusinessSentinel"] = "preserve-exactly";
        var collection = _database.GetCollection<BsonDocument>("mdm_gskus");
        await collection.InsertOneAsync(aggregate);

        var result = await Repository(_database).RunAsync(Request(verify: true));

        Assert.Equal(AuditIntentTemporalMigrationState.Phases.CompletionVerified, result.Phase);
        Assert.Equal(5, result.ScannedCount);
        Assert.Equal(5, result.MigratedCount);
        var stored = await collection.Find(new BsonDocument("_id", aggregate["_id"])).SingleAsync();
        Assert.Equal("preserve-exactly", stored["BusinessSentinel"].AsString);
        Assert.Equal(7, stored["Version"].AsInt32);
        var storedIntents = stored["AuditIntents"].AsBsonArray.Select(value => value.AsBsonDocument).ToArray();
        Assert.Equal(5, storedIntents.Length);
        Assert.Equal(
            intents.Select(intent => intent["IntentId"]).OrderBy(value => value.ToString(), StringComparer.Ordinal),
            storedIntents.Select(intent => intent["IntentId"]).OrderBy(value => value.ToString(), StringComparer.Ordinal));
        Assert.All(storedIntents, AssertCurrentRawIntent);
    }

    [Theory]
    [InlineData(AuditIntentTemporalMigrationFailurePoint.AfterPreflightBeforeState)]
    [InlineData(AuditIntentTemporalMigrationFailurePoint.AfterReadyState)]
    [InlineData(AuditIntentTemporalMigrationFailurePoint.AfterAggregateBeforeCheckpoint)]
    [InlineData(AuditIntentTemporalMigrationFailurePoint.AfterCheckpointCommit)]
    [InlineData(AuditIntentTemporalMigrationFailurePoint.AfterCompleted)]
    public async Task Migration_CheckpointFailure_ReplayCompletesWithoutLoss(
        AuditIntentTemporalMigrationFailurePoint failurePoint)
    {
        var aggregate = LegacyAggregate(
            Guid.NewGuid(),
            AuditAggregateType.GlobalProduct,
            [LegacyIntent(Guid.NewGuid(), OffsetTime(14), OffsetTime(-12), null)]);
        var collection = _database.GetCollection<BsonDocument>("mdm_global_products");
        await collection.InsertOneAsync(aggregate);
        var injector = new OneShotFailureInjector(failurePoint);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Repository(_database, injector).RunAsync(Request()));

        var replay = await Repository(_database).RunAsync(Request(verify: true));
        Assert.Equal(AuditIntentTemporalMigrationState.Phases.CompletionVerified, replay.Phase);
        Assert.Equal(1, replay.ScannedCount);
        Assert.Equal(1, replay.MigratedCount + replay.AlreadyCurrentCount);
        var stored = await collection.Find(new BsonDocument("_id", aggregate["_id"])).SingleAsync();
        AssertCurrentRawIntent(Assert.Single(stored["AuditIntents"].AsBsonArray).AsBsonDocument);
    }

    [Fact]
    public async Task Migration_OverOneMegabyteCandidate_FailsWithoutAggregateMutation()
    {
        var aggregate = LegacyAggregate(
            Guid.NewGuid(),
            AuditAggregateType.ProductLegalEntityScopePolicy,
            [LegacyIntent(Guid.NewGuid(), OffsetTime(14), OffsetTime(-12), null)]);
        aggregate["OversizedBusinessPayload"] = new string('x',
            AuditIntentTemporalMigrationRepository.MaximumAggregateDocumentBytes);
        var collection = _database.GetCollection<BsonDocument>("mdm_product_legal_entity_scope_policies");
        await collection.InsertOneAsync(aggregate);
        var before = await collection.Find(new BsonDocument("_id", aggregate["_id"])).SingleAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Repository(_database).RunAsync(Request()));

        Assert.Equal("AUDIT_INTENT_TEMPORAL_DOCUMENT_BUDGET_EXCEEDED", error.Message);
        Assert.Equal(before, await collection.Find(new BsonDocument("_id", aggregate["_id"])).SingleAsync());
        var state = Assert.IsType<AuditIntentTemporalMigrationState>(await Repository(_database).GetValidatedStateAsync());
        Assert.Equal(AuditIntentTemporalMigrationState.Phases.RecoveryRequired, state.Phase);
        Assert.Equal(0, state.ScannedCount);
    }

    [Fact]
    public async Task Completion_WrongSameNameIndex_FailsClosedWithoutActivation()
    {
        var aggregate = LegacyAggregate(
            Guid.NewGuid(),
            AuditAggregateType.CodeReservation,
            [CurrentIntent(Guid.NewGuid(), OffsetTime(0), null, null)]);
        var collection = _database.GetCollection<BsonDocument>("mdm_code_reservations");
        await collection.InsertOneAsync(aggregate);
        var completed = await Repository(_database).RunAsync(Request());
        Assert.Equal(AuditIntentTemporalMigrationState.Phases.Completed, completed.Phase);
        var expectedName = $"ix_mdm_code_reservations_{AuditIntentTemporalMigrationState.ExactIndexSuffix}";
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocument("TenantId", 1),
            new CreateIndexOptions { Name = expectedName }));

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            Repository(_database).RunAsync(Request(verify: true, activate: true)));

        Assert.Contains("index", error.Message, StringComparison.OrdinalIgnoreCase);
        var state = Assert.IsType<AuditIntentTemporalMigrationState>(await Repository(_database).GetValidatedStateAsync());
        Assert.Equal(AuditIntentTemporalMigrationState.Phases.Completed, state.Phase);
        Assert.Null(state.ActivationVersion);
    }

    [Fact]
    public async Task ActiveCutover_DiscoveryClaimAndReclaim_AreBoundedAndDeterministic()
    {
        var now = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(now);
        await InsertActiveStateAsync(now);
        var tenant = new TenantContext();
        tenant.SetTenant(_tenantId);
        var ids = new[] { GuidFromOrdinal(3), GuidFromOrdinal(1), GuidFromOrdinal(2) };
        foreach (var intentId in ids)
        {
            var aggregateId = Guid.NewGuid();
            var reservation = new CodeReservation
            {
                Id = aggregateId,
                TenantId = _tenantId,
                EntityType = CodeBearingEntityType.GlobalProduct,
                ReservedCode = ($"GP-{intentId:N}")[..15],
                ReservationCommandId = Guid.NewGuid().ToString("N"),
                ReservedAt = now,
                ReservedByActorId = "test",
                AuditIntents = [TypedCurrentIntent(intentId, aggregateId, now.AddMinutes(-1))]
            };
            await _database.GetCollection<CodeReservation>("mdm_code_reservations").InsertOneAsync(reservation);
        }

        var delivery = new AuditIntentDeliveryRepository(_database, tenant, clock);
        var discovered = await delivery.DiscoverEligibleAsync(2);

        Assert.Equal(2, discovered.Count);
        Assert.Equal(ids.OrderBy(id => id.ToString("N"), StringComparer.Ordinal).Take(2),
            discovered.Select(item => item.Locator.IntentId));
        var first = discovered[0];
        var claim = Assert.IsType<AuditIntentClaim>(await delivery.TryClaimAsync(
            first.Locator, 0, "worker-a", TimeSpan.FromMinutes(2)));
        Assert.Null(await delivery.TryClaimAsync(first.Locator, 1, "worker-b", TimeSpan.FromMinutes(2)));
        clock.Advance(TimeSpan.FromMinutes(3));
        var reclaimed = Assert.IsType<AuditIntentClaim>(await delivery.TryClaimAsync(
            first.Locator, 1, "worker-b", TimeSpan.FromMinutes(2)));
        Assert.Equal(2, reclaimed.ClaimGeneration);
        Assert.NotEqual(claim.ClaimToken, reclaimed.ClaimToken);
    }

    [Fact]
    public async Task ActiveCutover_MismatchedLegacyAndShadow_IsNeverClaimed()
    {
        var now = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        await InsertActiveStateAsync(now);
        var aggregate = LegacyAggregate(
            Guid.NewGuid(),
            AuditAggregateType.CodeReservation,
            [CurrentIntent(Guid.NewGuid(), now, null, null)]);
        aggregate["AuditIntents"].AsBsonArray[0].AsBsonDocument["TimestampUtcTicksV1"] = now.UtcTicks + 1;
        await _database.GetCollection<BsonDocument>("mdm_code_reservations").InsertOneAsync(aggregate);
        var tenant = new TenantContext();
        tenant.SetTenant(_tenantId);
        var repository = new AuditIntentDeliveryRepository(_database, tenant, new ManualTimeProvider(now));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DiscoverEligibleAsync(10));

        Assert.Equal("AUDIT_INTENT_TEMPORAL_SHAPE_INVALID", error.Message);
    }

    [Fact]
    public async Task SelectedScalarIndex_HintedExplainUsesIxscanAndReturnsBoundedExactWindow()
    {
        var now = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        var collection = _database.GetCollection<BsonDocument>("mdm_code_reservations");
        for (var ordinal = 1; ordinal <= 120; ordinal++)
        {
            var offset = ordinal % 3 == 0 ? TimeSpan.FromHours(14)
                : ordinal % 3 == 1 ? TimeSpan.Zero
                : TimeSpan.FromHours(-12);
            var timestamp = now.AddMinutes(-ordinal).ToOffset(offset);
            DateTimeOffset? nextRetry = ordinal % 3 == 1
                ? now.AddMinutes(10).ToOffset(TimeSpan.FromHours(14))
                : null;
            DateTimeOffset? leaseUntil = ordinal % 3 == 2
                ? now.AddMinutes(-10).ToOffset(TimeSpan.FromHours(-12))
                : null;
            var intent = CurrentIntent(GuidFromOrdinal(ordinal), timestamp, nextRetry, leaseUntil);
            if (ordinal % 3 == 2) intent["DeliveryState"] = (int)AuditIntentDeliveryState.Processing;
            await collection.InsertOneAsync(LegacyAggregate(
                Guid.NewGuid(),
                AuditAggregateType.CodeReservation,
                [intent]));
        }
        var activated = await Repository(_database).RunAsync(Request(verify: true, activate: true));
        Assert.Equal(AuditIntentTemporalMigrationState.Phases.CutoverActive, activated.Phase);
        const int limit = 7;
        var tenant = new BsonBinaryData(_tenantId, GuidRepresentation.Standard);
        var indexName = $"ix_mdm_code_reservations_{AuditIntentTemporalMigrationState.ExactIndexSuffix}";
        var pipeline = new BsonArray
        {
            new BsonDocument("$match", new BsonDocument
            {
                ["TenantId"] = tenant,
                ["AuditIntents"] = new BsonDocument("$elemMatch", new BsonDocument
                {
                    ["TenantId"] = tenant,
                    ["AggregateType"] = (int)AuditAggregateType.CodeReservation,
                    ["SourceService"] = AuditIntentContract.SourceService,
                    ["TemporalStorageVersion"] = AuditIntentTemporalStorage.CurrentVersion,
                    ["$or"] = new BsonArray
                    {
                        new BsonDocument
                        {
                            ["DeliveryState"] = (int)AuditIntentDeliveryState.Pending,
                            ["$or"] = new BsonArray
                            {
                                new BsonDocument("NextRetryAtUtcTicksV1", BsonNull.Value),
                                new BsonDocument("NextRetryAtUtcTicksV1", new BsonDocument("$lte", now.UtcTicks))
                            }
                        },
                        new BsonDocument
                        {
                            ["DeliveryState"] = (int)AuditIntentDeliveryState.Processing,
                            ["LeaseUntilUtcTicksV1"] = new BsonDocument("$lte", now.UtcTicks)
                        }
                    }
                })
            }),
            new BsonDocument("$unwind", "$AuditIntents"),
            new BsonDocument("$match", new BsonDocument
            {
                ["TenantId"] = tenant,
                ["AuditIntents.TenantId"] = tenant,
                ["AuditIntents.AggregateType"] = (int)AuditAggregateType.CodeReservation,
                ["AuditIntents.SourceService"] = AuditIntentContract.SourceService,
                ["AuditIntents.TemporalStorageVersion"] = AuditIntentTemporalStorage.CurrentVersion,
                ["$or"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["AuditIntents.DeliveryState"] = (int)AuditIntentDeliveryState.Pending,
                        ["$or"] = new BsonArray
                        {
                            new BsonDocument("AuditIntents.NextRetryAtUtcTicksV1", BsonNull.Value),
                            new BsonDocument("AuditIntents.NextRetryAtUtcTicksV1", new BsonDocument("$lte", now.UtcTicks))
                        }
                    },
                    new BsonDocument
                    {
                        ["AuditIntents.DeliveryState"] = (int)AuditIntentDeliveryState.Processing,
                        ["AuditIntents.LeaseUntilUtcTicksV1"] = new BsonDocument("$lte", now.UtcTicks)
                    }
                }
            }),
            new BsonDocument("$sort", new BsonDocument
            {
                ["AuditIntents.TimestampUtcTicksV1"] = 1,
                ["AuditIntents.IntentId"] = 1
            }),
            new BsonDocument("$limit", limit)
        };
        var explanation = await _database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["explain"] = new BsonDocument
            {
                ["aggregate"] = "mdm_code_reservations",
                ["pipeline"] = pipeline,
                ["cursor"] = new BsonDocument(),
                ["hint"] = indexName
            },
            ["verbosity"] = "executionStats"
        });
        var rendered = explanation.ToJson();
        Assert.Contains("IXSCAN", rendered, StringComparison.Ordinal);
        Assert.Contains(indexName, rendered, StringComparison.Ordinal);
        Assert.Contains("$sort", rendered, StringComparison.Ordinal);
        var multikeyStages = FindDocuments(explanation, document =>
            document.TryGetValue("isMultiKey", out var value) && value.IsBoolean && value.AsBoolean);
        Assert.NotEmpty(multikeyStages);
        Assert.All(multikeyStages, stage =>
        {
            var paths = stage["multiKeyPaths"].AsBsonDocument;
            Assert.Empty(paths["TenantId"].AsBsonArray);
            Assert.All(paths.Elements.Where(element => element.Name.StartsWith("AuditIntents.", StringComparison.Ordinal)),
                element => Assert.NotEmpty(element.Value.AsBsonArray));
        });
        var aggregateResult = await _database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["aggregate"] = "mdm_code_reservations",
            ["pipeline"] = pipeline,
            ["cursor"] = new BsonDocument()
        });
        var returnedRows = aggregateResult["cursor"].AsBsonDocument["firstBatch"].AsBsonArray;
        var keys = ReadNumericField(explanation, "totalKeysExamined").Max();
        var documents = ReadNumericField(explanation, "totalDocsExamined").Max();
        Assert.Equal(limit, returnedRows.Count);
        Assert.All(returnedRows, row =>
        {
            var intent = row["AuditIntents"].AsBsonDocument;
            var pendingReady = intent["DeliveryState"].AsInt32 == (int)AuditIntentDeliveryState.Pending
                               && (intent["NextRetryAtUtcTicksV1"].IsBsonNull
                                   || intent["NextRetryAtUtcTicksV1"].AsInt64 <= now.UtcTicks);
            var staleProcessing = intent["DeliveryState"].AsInt32 == (int)AuditIntentDeliveryState.Processing
                                  && intent["LeaseUntilUtcTicksV1"].AsInt64 <= now.UtcTicks;
            Assert.True(pendingReady || staleProcessing);
        });
        Assert.InRange(keys, limit, 120);
        Assert.InRange(documents, limit, 120);
    }

    private AuditIntentTemporalMigrationRepository Repository(
        IMongoDatabase database,
        IAuditIntentTemporalMigrationFailureInjector? injector = null) =>
        new(database, new ManualTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero)), injector);

    private static AuditIntentTemporalMigrationRequest Request(bool verify = false, bool activate = false) => new(
        "mdm-fu02-test-runner",
        2,
        TimeSpan.FromMinutes(2),
        verify,
        activate,
        AuditIntentTemporalMigrationRepository.SelectedIndexEvidenceFingerprint);

    private async Task InsertActiveStateAsync(DateTimeOffset now)
    {
        await _database.GetCollection<AuditIntentTemporalMigrationState>(
            AuditIntentTemporalMigrationRepository.StateCollectionName).InsertOneAsync(new AuditIntentTemporalMigrationState
        {
            Phase = AuditIntentTemporalMigrationState.Phases.CutoverActive,
            CollectionOrdinal = 8,
            CollectionBoundaries = Enumerable.Range(0, 8).Select(index =>
                new AuditIntentTemporalCollectionBoundary
                {
                    CollectionOrdinal = index,
                    UpperAggregateId = Guid.Empty
                }).ToList(),
            UpdatedAtUtcTicks = now.UtcTicks,
            CompletedAtUtcTicks = now.UtcTicks,
            ActivationVersion = AuditIntentTemporalStorage.CurrentVersion,
            IndexEvidenceFingerprint = AuditIntentTemporalMigrationRepository.SelectedIndexEvidenceFingerprint
        });
    }

    private BsonDocument LegacyAggregate(
        Guid id,
        AuditAggregateType type,
        IEnumerable<BsonDocument> intents)
    {
        var boundIntents = intents.Select(intent =>
        {
            intent["AggregateType"] = (int)type;
            intent["AggregateId"] = new BsonBinaryData(id, GuidRepresentation.Standard);
            return intent;
        });
        return new BsonDocument
        {
            ["_id"] = new BsonBinaryData(id, GuidRepresentation.Standard),
            ["TenantId"] = new BsonBinaryData(_tenantId, GuidRepresentation.Standard),
            ["AggregateType"] = (int)type,
            ["Version"] = 7,
            ["IsDeleted"] = false,
            ["AuditIntents"] = new BsonArray(boundIntents)
        };
    }

    private BsonDocument LegacyIntent(
        Guid id,
        DateTimeOffset timestamp,
        DateTimeOffset? nextRetry,
        DateTimeOffset? lease) => new()
    {
        ["IntentId"] = new BsonBinaryData(id, GuidRepresentation.Standard),
        ["TenantId"] = new BsonBinaryData(_tenantId, GuidRepresentation.Standard),
        ["TimestampUtc"] = Legacy(timestamp),
        ["NextRetryAt"] = nextRetry.HasValue ? Legacy(nextRetry.Value) : BsonNull.Value,
        ["LeaseUntil"] = lease.HasValue ? Legacy(lease.Value) : BsonNull.Value,
        ["DeliveryState"] = (int)AuditIntentDeliveryState.Pending,
        ["ClaimGeneration"] = 0L,
        ["SourceService"] = AuditIntentContract.SourceService
    };

    private BsonDocument CurrentIntent(
        Guid id,
        DateTimeOffset timestamp,
        DateTimeOffset? nextRetry,
        DateTimeOffset? lease)
    {
        var intent = LegacyIntent(id, timestamp, nextRetry, lease);
        intent["TimestampUtcTicksV1"] = timestamp.UtcTicks;
        intent["NextRetryAtUtcTicksV1"] = nextRetry.HasValue ? nextRetry.Value.UtcTicks : BsonNull.Value;
        intent["LeaseUntilUtcTicksV1"] = lease.HasValue ? lease.Value.UtcTicks : BsonNull.Value;
        intent["TemporalStorageVersion"] = AuditIntentTemporalStorage.CurrentVersion;
        return intent;
    }

    private LocalAuditIntent TypedCurrentIntent(Guid id, Guid aggregateId, DateTimeOffset timestamp)
    {
        var intent = new LocalAuditIntent
        {
            IntentId = id,
            TenantId = _tenantId,
            AggregateType = AuditAggregateType.CodeReservation,
            AggregateId = aggregateId,
            Operation = ProductAuditOperation.CodeReserved,
            ActorId = "actor",
            CorrelationId = Guid.NewGuid().ToString("N"),
            CausationId = Guid.NewGuid().ToString("N"),
            CommandId = Guid.NewGuid().ToString("N"),
            Sequence = 1,
            TimestampUtc = timestamp,
            EvidenceHash = "evidence",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            DeliveryState = AuditIntentDeliveryState.Pending
        };
        AuditIntentTemporalStorage.ApplyCurrentVersion(intent);
        return intent;
    }

    private static BsonArray Legacy(DateTimeOffset value) => [value.Ticks, (int)value.Offset.TotalMinutes];

    private static void AssertCurrentRawIntent(BsonDocument intent)
    {
        Assert.Equal(AuditIntentTemporalStorage.CurrentVersion, intent["TemporalStorageVersion"].AsInt32);
        Assert.True(intent["TimestampUtcTicksV1"].IsInt64);
        Assert.True(intent["NextRetryAtUtcTicksV1"].IsInt64 || intent["NextRetryAtUtcTicksV1"].IsBsonNull);
        Assert.True(intent["LeaseUntilUtcTicksV1"].IsInt64 || intent["LeaseUntilUtcTicksV1"].IsBsonNull);
    }

    private static IReadOnlyList<long> ReadNumericField(BsonValue value, string name)
    {
        var values = new List<long>();
        Visit(value);
        return values;

        void Visit(BsonValue current)
        {
            if (current.IsBsonDocument)
            {
                foreach (var element in current.AsBsonDocument)
                {
                    if (string.Equals(element.Name, name, StringComparison.Ordinal) && element.Value.IsNumeric)
                        values.Add(element.Value.ToInt64());
                    Visit(element.Value);
                }
            }
            else if (current.IsBsonArray)
            {
                foreach (var item in current.AsBsonArray) Visit(item);
            }
        }
    }

    private static IReadOnlyList<BsonDocument> FindDocuments(
        BsonValue value,
        Func<BsonDocument, bool> predicate)
    {
        var documents = new List<BsonDocument>();
        Visit(value);
        return documents;

        void Visit(BsonValue current)
        {
            if (current.IsBsonDocument)
            {
                var document = current.AsBsonDocument;
                if (predicate(document)) documents.Add(document);
                foreach (var element in document) Visit(element.Value);
            }
            else if (current.IsBsonArray)
            {
                foreach (var item in current.AsBsonArray) Visit(item);
            }
        }
    }

    private static DateTimeOffset OffsetTime(int offsetHours) =>
        new(2026, 8, 28, 10, 0, 0, TimeSpan.FromHours(offsetHours));

    private static Guid GuidFromOrdinal(int ordinal)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes[14] = checked((byte)(ordinal / 256));
        bytes[15] = checked((byte)(ordinal % 256));
        return new Guid(bytes, bigEndian: true);
    }

    private sealed class OneShotFailureInjector(AuditIntentTemporalMigrationFailurePoint point) :
        IAuditIntentTemporalMigrationFailureInjector
    {
        private int _used;

        public void ThrowIfRequested(AuditIntentTemporalMigrationFailurePoint candidate)
        {
            if (candidate == point && Interlocked.Exchange(ref _used, 1) == 0)
            {
                throw new InvalidOperationException("INJECTED_TEMPORAL_FAILURE");
            }
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}

public sealed class AuditIntentTemporalMongoFixture : IAsyncLifetime
{
    private const string MongodPathVariable = "DITEN_FU02_MONGOD_PATH";
    private OwnedMongoProcess? _replica;
    private OwnedMongoProcess? _standalone;

    public string ReplicaConnectionString { get; private set; } = string.Empty;
    public string StandaloneConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            var mongod = ResolveMongodPath();
            _standalone = await OwnedMongoProcess.StartAsync(mongod, null);
            await WaitForHelloAsync(_standalone.DirectConnectionString, _standalone, hello => !hello.Contains("setName"));
            StandaloneConnectionString = _standalone.DirectConnectionString;

            var setName = $"mdmfu02{Guid.NewGuid():N}";
            _replica = await OwnedMongoProcess.StartAsync(mongod, setName);
            await InitiateReplicaSetAsync(_replica, setName);
            ReplicaConnectionString = _replica.ReplicaConnectionString(setName);
            await WaitForHelloAsync(
                ReplicaConnectionString,
                _replica,
                hello => hello.TryGetValue("setName", out var name) && name.IsString &&
                         hello.TryGetValue("isWritablePrimary", out var primary) && primary.ToBoolean());
        }
        catch (Exception original)
        {
            try
            {
                await DisposeAsync();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(original, cleanup);
            }
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(original).Throw();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        var failures = new List<Exception>();
        foreach (var process in new[] { _replica, _standalone })
        {
            if (process is null) continue;
            try { await process.DisposeAsync(); }
            catch (Exception exception) { failures.Add(exception); }
        }
        _replica = null;
        _standalone = null;
        if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException(failures);
    }

    private static async Task InitiateReplicaSetAsync(OwnedMongoProcess process, string setName)
    {
        var settings = MongoClientSettings.FromConnectionString(process.DirectConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(500);
        var admin = new MongoClient(settings).GetDatabase("admin");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            process.ThrowIfExited();
            try
            {
                await admin.RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument
                {
                    ["_id"] = setName,
                    ["members"] = new BsonArray
                    {
                        new BsonDocument { ["_id"] = 0, ["host"] = $"127.0.0.1:{process.Port}" }
                    }
                }));
                return;
            }
            catch (MongoException) { await Task.Delay(200); }
            catch (TimeoutException) { await Task.Delay(200); }
        }
        throw new TimeoutException($"MDM_TEMPORAL_REPLICA_INIT_TIMEOUT{Environment.NewLine}{process.RecentLog()}");
    }

    private static async Task WaitForHelloAsync(
        string connection,
        OwnedMongoProcess process,
        Func<BsonDocument, bool> expected)
    {
        var settings = MongoClientSettings.FromConnectionString(connection);
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(750);
        var admin = new MongoClient(settings).GetDatabase("admin");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            process.ThrowIfExited();
            try
            {
                var hello = await admin.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
                if (expected(hello)) return;
            }
            catch (MongoException) { }
            catch (TimeoutException) { }
            await Task.Delay(200);
        }
        throw new TimeoutException($"MDM_TEMPORAL_MONGO_READY_TIMEOUT{Environment.NewLine}{process.RecentLog()}");
    }

    private static string ResolveMongodPath()
    {
        var configured = Environment.GetEnvironmentVariable(MongodPathVariable);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return Path.GetFullPath(configured);
        var executable = OperatingSystem.IsWindows() ? "mongod.exe" : "mongod";
        var pathMatch = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => Path.Combine(path, executable)).FirstOrDefault(File.Exists);
        if (pathMatch is not null) return Path.GetFullPath(pathMatch);
        if (OperatingSystem.IsWindows())
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MongoDB", "Server");
            var installed = Directory.Exists(root)
                ? Directory.EnumerateFiles(root, "mongod.exe", SearchOption.AllDirectories)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
                : null;
            if (installed is not null) return installed;
        }
        else
        {
            var installed = new[] { "/opt/homebrew/bin/mongod", "/usr/local/bin/mongod", "/usr/bin/mongod" }
                .FirstOrDefault(File.Exists);
            if (installed is not null) return installed;
        }
        throw new FileNotFoundException($"MDM_TEMPORAL_MONGOD_REQUIRED: set {MongodPathVariable}");
    }

    private sealed class OwnedMongoProcess : IAsyncDisposable
    {
        private readonly ConcurrentQueue<string> _log = new();
        private Process? _process;
        private string? _directory;
        private OwnedMongoProcess(int port, string directory) { Port = port; _directory = directory; }
        public int Port { get; }
        public string DirectConnectionString => $"mongodb://127.0.0.1:{Port}/?directConnection=true";
        public string ReplicaConnectionString(string setName) =>
            $"mongodb://127.0.0.1:{Port}/?replicaSet={setName}&directConnection=true";

        public static async Task<OwnedMongoProcess> StartAsync(string mongod, string? setName)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var directory = Path.Combine(Path.GetTempPath(), $"diten_mdm_temporal_{Guid.NewGuid():N}");
            var owned = new OwnedMongoProcess(port, directory);
            try
            {
                Directory.CreateDirectory(directory);
                var start = new ProcessStartInfo
                {
                    FileName = mongod,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var argument in new[] { "--dbpath", directory, "--port", port.ToString(), "--bind_ip", "127.0.0.1" })
                    start.ArgumentList.Add(argument);
                if (setName is not null) { start.ArgumentList.Add("--replSet"); start.ArgumentList.Add(setName); }
                start.ArgumentList.Add("--quiet");
                owned._process = new Process { StartInfo = start, EnableRaisingEvents = true };
                owned._process.OutputDataReceived += (_, args) => owned.Capture(args.Data);
                owned._process.ErrorDataReceived += (_, args) => owned.Capture(args.Data);
                if (!owned._process.Start()) throw new InvalidOperationException("MDM_TEMPORAL_MONGO_START_FAILED");
                owned._process.BeginOutputReadLine();
                owned._process.BeginErrorReadLine();
                return owned;
            }
            catch (Exception original)
            {
                try { await owned.DisposeAsync(); }
                catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(original).Throw();
                throw;
            }
        }

        public void ThrowIfExited()
        {
            if (_process is not null && _process.HasExited)
                throw new InvalidOperationException($"MDM_TEMPORAL_MONGO_EXITED:{_process.ExitCode}{Environment.NewLine}{RecentLog()}");
        }
        public string RecentLog() => string.Join(Environment.NewLine, _log);
        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            if (_process is not null)
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                        await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                    }
                }
                catch (Exception exception) { failures.Add(exception); }
                finally { try { _process.Dispose(); } catch (Exception exception) { failures.Add(exception); } _process = null; }
            }
            if (_directory is not null)
            {
                var target = Path.GetFullPath(_directory);
                var root = Path.GetFullPath(Path.GetTempPath());
                var scoped = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
                if (!target.StartsWith(scoped, StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(target).StartsWith("diten_mdm_temporal_", StringComparison.Ordinal))
                    failures.Add(new InvalidOperationException("MDM_TEMPORAL_CLEANUP_SCOPE_INVALID"));
                else
                {
                    for (var attempt = 0; attempt < 30 && Directory.Exists(target); attempt++)
                    {
                        try { Directory.Delete(target, recursive: true); }
                        catch (IOException) when (attempt < 29) { await Task.Delay(100); }
                        catch (UnauthorizedAccessException) when (attempt < 29) { await Task.Delay(100); }
                        catch (Exception exception) { failures.Add(exception); break; }
                    }
                    if (Directory.Exists(target)) failures.Add(new InvalidOperationException("MDM_TEMPORAL_CLEANUP_FAILED"));
                }
                _directory = null;
            }
            if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }
        private void Capture(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _log.Enqueue(line);
            while (_log.Count > 100) _log.TryDequeue(out _);
        }
    }
}
