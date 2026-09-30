using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Infrastructure.Audit;
using Microsoft.Extensions.Options;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;
using Xunit.Abstractions;

namespace Diten.MdmService.Application.Tests.Audit;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class SelectedAuditIntentDeliveryMongoTests(AuditIntentTemporalMongoFixture mongo, ITestOutputHelper output)
    : IClassFixture<AuditIntentTemporalMongoFixture>, IAsyncLifetime
{
    private const string DatabaseName = "diten_mdm_product_scope_itest";
    private const string Products = "mdm_global_products";
    private const string Reservations = "mdm_code_reservations";
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _foreign = Guid.NewGuid();
    private readonly ConcurrentQueue<string> _commands = new();
    private IMongoDatabase _database = null!;
    private static readonly string[] Collections = [Products, Reservations, "mdm_finished_goods", "mdm_product_legal_entity_scope_rollout_states",
        "mdm_product_definition_revisions", "mdm_gskus", "mdm_lskus", "mdm_product_abbreviation_register", "mdm_product_legal_entity_scope_policies"];

    public async Task InitializeAsync()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        var settings = MongoClientSettings.FromConnectionString(mongo.ReplicaConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        Assert.Single(settings.Servers);
        Assert.Equal("127.0.0.1", settings.Server.Host);
        Assert.NotEqual(27017, settings.Server.Port);
        Assert.False(string.IsNullOrWhiteSpace(settings.ReplicaSetName));
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(e => _commands.Enqueue(e.CommandName));
        _database = new MongoClient(settings).GetDatabase(DatabaseName);
        var hello = await _database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
        Assert.True(hello["isWritablePrimary"].AsBoolean);
        Assert.Equal(settings.ReplicaSetName, hello["setName"].AsString);
        foreach (var connection in new[] { mongo.ReplicaConnectionString, mongo.StandaloneConnectionString })
        {
            var server = await new MongoClient(connection).GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("serverStatus", 1));
            output.WriteLine("OWNED_MONGO_PID={0}", server["pid"].ToInt64());
        }
        foreach (var name in Collections)
        {
            var existing = await (await _database.ListCollectionNamesAsync()).ToListAsync();
            if (!existing.Contains(name)) await _database.CreateCollectionAsync(name);
        }
        await CreateExactIndex(Products);
        await CreateExactIndex(Reservations);
        foreach (var name in new[] { "mdm_product_definition_revisions", "mdm_gskus", "mdm_lskus", "mdm_product_legal_entity_scope_policies" })
            await CreateExactIndex(name);
        await _database.GetCollection<BsonDocument>("mdm_product_abbreviation_register").Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(new BsonDocument { { "TenantId", 1 }, { "NormalizedAbbreviation", 1 }, { "LifecycleStatus", 1 } },
                new CreateIndexOptions { Name = "ix_mdm_product_abbreviation_register_tenant_resolution" }));
        var now = DateTimeOffset.UtcNow;
        var state = new AuditIntentTemporalMigrationState
        {
            Phase = AuditIntentTemporalMigrationState.Phases.CutoverActive, CollectionOrdinal = 8,
            CollectionBoundaries = Enumerable.Range(0, 8).Select(i => new AuditIntentTemporalCollectionBoundary
                { CollectionOrdinal = i, UpperAggregateId = Guid.Empty }).ToList(),
            UpdatedAtUtcTicks = now.UtcTicks, CompletedAtUtcTicks = now.UtcTicks,
            ActivationVersion = AuditIntentTemporalStorage.CurrentVersion,
            IndexEvidenceFingerprint = AuditIntentTemporalMigrationRepository.SelectedIndexEvidenceFingerprint
        };
        await _database.GetCollection<AuditIntentTemporalMigrationState>(AuditIntentTemporalMigrationRepository.StateCollectionName)
            .ReplaceOneAsync(x => x.Id == AuditIntentTemporalMigrationState.ExactId, state, new ReplaceOptions { IsUpsert = true });
        _commands.Clear();
    }

    public async Task DisposeAsync()
    {
        foreach (var name in Collections)
        {
            var filter = new BsonDocument("TenantId", new BsonDocument("$in", new BsonArray { Binary(_tenant), Binary(_foreign) }));
            var collection = _database.GetCollection<BsonDocument>(name);
            Assert.True((await collection.DeleteManyAsync(filter)).IsAcknowledged);
            Assert.Equal(0, await collection.CountDocumentsAsync(filter));
            output.WriteLine("TENANT_CLEANUP={0};remaining=0", name);
        }
        // This exact global state is owned by the dedicated disposable mongod fixture, not Local Development.
        var states = _database.GetCollection<BsonDocument>(AuditIntentTemporalMigrationRepository.StateCollectionName);
        var stateFilter = new BsonDocument("_id", AuditIntentTemporalMigrationState.ExactId);
        await states.DeleteOneAsync(stateFilter);
        Assert.Equal(0, await states.CountDocumentsAsync(stateFilter));
        output.WriteLine("FIXTURE_GLOBAL_STATE_CLEANUP=0;DATABASE_NOT_DROPPED");
    }

    [Fact]
    public async Task Selected_soft_deleted_source_claim_compact_replay_preserves_all_nonselected_business_data()
    {
        var intent = Intent(ProductAuditOperation.GlobalProductDraftCreated);
        await Insert(Products, intent, deleted: true);
        var unrelated = Intent(ProductAuditOperation.GlobalProductDraftCreated);
        await Insert(Products, unrelated);
        var foreign = Intent(ProductAuditOperation.GlobalProductDraftCreated); foreign.TenantId = _foreign;
        await Insert(Products, foreign);
        var beforeUnrelated = await Read(Products, unrelated);
        var beforeForeign = await Read(Products, foreign);
        var finishedGood = Intent(ProductAuditOperation.FinishedGoodDraftCreated); finishedGood.AggregateType = AuditAggregateType.FinishedGood;
        var rollout = Intent(ProductAuditOperation.ProductLegalEntityScopeEnforcementActivated); rollout.AggregateType = AuditAggregateType.ProductLegalEntityScopeRolloutState;
        await Insert("mdm_finished_goods", finishedGood);
        await Insert("mdm_product_legal_entity_scope_rollout_states", rollout);
        var beforeFinishedGood = await Read("mdm_finished_goods", finishedGood);
        var beforeRollout = await Read("mdm_product_legal_entity_scope_rollout_states", rollout);
        var request = Request(intent);
        _commands.Clear();
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await repository.PrepareSelectedAsync(request);
        var claims = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => repository.TryClaimAsync(
            request.Items[0].Locator, 0, "owned-test", TimeSpan.FromMinutes(1))));
        var claim = Assert.Single(claims, x => x is not null)!;
        Assert.Equal(1, claim.ClaimGeneration);
        var payload = Assert.IsType<AuditIntentClaimedPayload>(await repository.ReadClaimedPayloadAsync(claim));
        Assert.Equal(intent.ActorId, payload.ActorId);
        Assert.True(await repository.AcknowledgeAndCompactAsync(claim, Acknowledgement(intent), "receipt-test"));
        Assert.NotNull(await repository.ReadSelectedReceiptAsync(request.Items[0].Locator));
        var replay = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await replay.PrepareSelectedAsync(request);
        Assert.NotNull(await replay.ReadSelectedReceiptAsync(request.Items[0].Locator));
        Assert.Null(await replay.TryClaimAsync(request.Items[0].Locator, 0, "replay", TimeSpan.FromMinutes(1)));
        var after = await Read(Products, intent);
        Assert.Equal(7, after["Version"].AsInt32);
        Assert.True(after["IsDeleted"].AsBoolean);
        Assert.Empty(after["AuditIntents"].AsBsonArray);
        Assert.Single(after["AuditIntentReceipts"].AsBsonArray);
        Assert.Equal(beforeUnrelated, await Read(Products, unrelated));
        Assert.Equal(beforeForeign, await Read(Products, foreign));
        Assert.Equal(beforeFinishedGood, await Read("mdm_finished_goods", finishedGood));
        Assert.Equal(beforeRollout, await Read("mdm_product_legal_entity_scope_rollout_states", rollout));
        Assert.DoesNotContain("createIndexes", _commands);
        Assert.DoesNotContain("create", _commands);
    }

    [Theory]
    [InlineData(ProductAuditOperation.GlobalProductDraftUpdated)]
    [InlineData(ProductAuditOperation.GlobalProductIdentitySubmitted)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityApproved)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityRejected)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn)]
    [InlineData(ProductAuditOperation.GlobalProductIdentityRetired)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionRequested)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionApplied)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionRejected)]
    [InlineData(ProductAuditOperation.GlobalProductCorrectionManualReconciliationRequired)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementRequested)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementRejected)]
    [InlineData(ProductAuditOperation.GlobalProductRetirementManualReconciliationRequired)]
    public async Task Each_real_GP_producer_operation_can_be_prepared_and_claimed(ProductAuditOperation operation)
    {
        var intent = Intent(operation); await Insert(Products, intent);
        var request = Request(intent);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await repository.PrepareSelectedAsync(request);
        Assert.NotNull(await repository.TryClaimAsync(request.Items[0].Locator, 0, "owned-test", TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("evidence")]
    [InlineData("cancel43")]
    [InlineData("cancel47")]
    [InlineData("wrong-pair")]
    [InlineData("cutover")]
    [InlineData("fingerprint")]
    public async Task Preflight_drift_rejects_with_zero_source_mutation(string fault)
    {
        var intent = Intent(fault switch { "cancel43" => ProductAuditOperation.GlobalProductCorrectionCancelled,
            "cancel47" => ProductAuditOperation.GlobalProductRetirementCancelled,
            "wrong-pair" => ProductAuditOperation.GskuDraftCreated, _ => ProductAuditOperation.GlobalProductDraftCreated });
        await Insert(Products, intent);
        var request = Request(intent, fault == "generation" ? 1 : 0, fault == "evidence" ? new string('B', 64) : null);
        if (fault is "cutover" or "fingerprint")
            await _database.GetCollection<BsonDocument>(AuditIntentTemporalMigrationRepository.StateCollectionName).UpdateOneAsync(
                new BsonDocument("_id", AuditIntentTemporalMigrationState.ExactId), new BsonDocument("$set",
                    fault == "cutover" ? new BsonDocument("Phase", "CompletionVerified") : new BsonDocument("IndexEvidenceFingerprint", "invalid")));
        var before = await Read(Products, intent);
        _commands.Clear();
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.PrepareSelectedAsync(request));
        Assert.Equal(before, await Read(Products, intent));
        Assert.DoesNotContain(_commands, name => name is "insert" or "update" or "findAndModify" or "createIndexes");
    }

    [Theory]
    [InlineData(CodeBearingEntityType.GlobalProduct)]
    [InlineData(CodeBearingEntityType.Gsku)]
    public async Task Consumed_reservation_reserved_evidence_remains_replayable_without_downstream_insert(CodeBearingEntityType type)
    {
        var intent = Intent(ProductAuditOperation.CodeReserved); intent.AggregateType = AuditAggregateType.CodeReservation;
        // Match the actual CodeReservationRepository.CreateIntent producer, not an arbitrary receipt key.
        intent.PreVersion = -1;
        intent.PostVersion = 0;
        intent.CausationId = intent.CommandId;
        intent.SnapshotReference = $"CodeReservation/{intent.AggregateId:N}/0";
        intent.IdempotencyKey = $"{_tenant:N}:CodeReservation:{intent.AggregateId:N}:{intent.CommandId}";
        var code = (type == CodeBearingEntityType.GlobalProduct ? "GP-" : "GS-") + "000000000001";
        intent.EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{_tenant:N}|{intent.AggregateId:N}|{code}|{type}|RESERVED")));
        var reservation = new CodeReservation { Id = intent.AggregateId, TenantId = _tenant, EntityType = type,
            ReservedCode = code, ReservedByActorId = intent.ActorId, ReservationCommandId = intent.CommandId,
            ConsumedEntityId = Guid.NewGuid(), AuditIntents = [intent] };
        await _database.GetCollection<CodeReservation>(Reservations).InsertOneAsync(reservation);
        var request = Request(intent);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await repository.PrepareSelectedAsync(request);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(request.Items[0].Locator, 0, "owned-test", TimeSpan.FromMinutes(1)));
        Assert.True(await repository.AcknowledgeAndCompactAsync(claim, Acknowledgement(intent), "receipt-test"));
        Assert.NotNull(await repository.ReadSelectedReceiptAsync(request.Items[0].Locator));
        var replay = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await replay.PrepareSelectedAsync(request);
        Assert.NotNull(await replay.ReadSelectedReceiptAsync(request.Items[0].Locator));
        await _database.GetCollection<BsonDocument>(Reservations).UpdateOneAsync(
            new BsonDocument("_id", Binary(intent.AggregateId)),
            new BsonDocument("$set", new BsonDocument("AuditIntentReceipts.0.IdempotencyKey", "not-the-producer-command")));
        var drift = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => drift.PrepareSelectedAsync(request));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("order")]
    [InlineData("unique")]
    [InlineData("sparse")]
    [InlineData("partial")]
    [InlineData("ttl")]
    public async Task Index_spec_drift_is_rejected_without_repair_or_claim(string fault)
    {
        var intent = Intent(ProductAuditOperation.GlobalProductDraftCreated); await Insert(Products, intent);
        var request = Request(intent);
        var collection = _database.GetCollection<BsonDocument>(Products);
        var name = $"ix_{Products}_{AuditIntentTemporalMigrationState.ExactIndexSuffix}";
        await collection.Indexes.DropOneAsync(name);
        try
        {
            if (fault != "missing")
            {
                var keys = ExactKeys();
                if (fault == "order") keys = new BsonDocument(keys.Elements.Reverse());
                if (fault == "ttl") keys = new BsonDocument("AuditIntents.TimestampUtc", 1);
                var options = new CreateIndexOptions<BsonDocument> { Name = name, Unique = fault == "unique", Sparse = fault == "sparse" };
                if (fault == "partial") options.PartialFilterExpression = new BsonDocument("IsDeleted", false);
                if (fault == "ttl") options.ExpireAfter = TimeSpan.FromDays(365);
                await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(keys, options));
            }
            var before = await Read(Products, intent);
            _commands.Clear();
            var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.PrepareSelectedAsync(request));
            Assert.Equal(before, await Read(Products, intent));
            Assert.DoesNotContain(_commands, command => command is "createIndexes" or "update" or "findAndModify" or "insert");
        }
        finally
        {
            var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();
            if (indexes.Any(index => index["name"] == name)) await collection.Indexes.DropOneAsync(name);
            await CreateExactIndex(Products);
        }
    }

    [Fact]
    public async Task Every_mutation_rejects_forged_claim_and_outside_selection_independently_of_runner()
    {
        var intent = Intent(ProductAuditOperation.GlobalProductDraftCreated); await Insert(Products, intent);
        var request = Request(intent);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await repository.PrepareSelectedAsync(request);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(request.Items[0].Locator, 0, "owned-test", TimeSpan.FromMinutes(1)));
        var forged = claim with { ClaimGeneration = claim.ClaimGeneration + 1 };
        var before = await Read(Products, intent);
        Assert.Null(await repository.ReadClaimedPayloadAsync(forged));
        Assert.False(await repository.MarkRetryableFailureAsync(forged, TimeSpan.FromSeconds(1), "test"));
        Assert.False(await repository.MarkDeadLetterAsync(forged, "test"));
        Assert.False(await repository.MarkDeliveredAsync(forged, Acknowledgement(intent)));
        Assert.False(await repository.AcknowledgeAndCompactAsync(forged, Acknowledgement(intent), "receipt-test"));
        Assert.False(await repository.CompactDeliveredAsync(forged, "receipt-test"));
        var outside = claim with { Locator = claim.Locator with { AggregateId = Guid.NewGuid() } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.TryClaimAsync(outside.Locator, 0, "test", TimeSpan.FromMinutes(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReadClaimedPayloadAsync(outside));
        await Denied(() => repository.MarkRetryableFailureAsync(outside, TimeSpan.FromSeconds(1), "test"));
        await Denied(() => repository.MarkDeadLetterAsync(outside, "test"));
        await Denied(() => repository.MarkDeliveredAsync(outside, Acknowledgement(intent)));
        await Denied(() => repository.AcknowledgeAndCompactAsync(outside, Acknowledgement(intent), "receipt-test"));
        await Denied(() => repository.CompactDeliveredAsync(outside, "receipt-test"));
        Assert.Equal(before, await Read(Products, intent));
    }

    [Fact]
    public async Task Source_actor_drift_after_preflight_is_rejected_and_never_rewritten()
    {
        var intent = Intent(ProductAuditOperation.GlobalProductDraftCreated); await Insert(Products, intent);
        var request = Request(intent);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await repository.PrepareSelectedAsync(request);
        await _database.GetCollection<BsonDocument>(Products).UpdateOneAsync(new BsonDocument("_id", Binary(intent.AggregateId)),
            new BsonDocument("$set", new BsonDocument("AuditIntents.0.ActorId", "changed-test-actor")));
        var before = await Read(Products, intent);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.TryClaimAsync(request.Items[0].Locator, 0, "test", TimeSpan.FromMinutes(1)));
        Assert.Equal(before, await Read(Products, intent));
    }

    [Fact]
    public async Task Standalone_and_missing_collection_are_rejected_without_implicit_creation()
    {
        var intent = Intent(ProductAuditOperation.GlobalProductDraftCreated);
        var request = Request(intent);
        var database = new MongoClient(mongo.StandaloneConnectionString).GetDatabase(DatabaseName);
        var before = await (await database.ListCollectionNamesAsync()).ToListAsync();
        var standalone = AuditIntentDeliveryRepository.CreateSelected(database, request, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => standalone.PrepareSelectedAsync(request));
        Assert.Equal(before, await (await database.ListCollectionNamesAsync()).ToListAsync());
        // Standalone cannot reach metadata preflight. On replica remove only this fixture-owned empty collection.
        await _database.DropCollectionAsync("mdm_lskus");
        var missing = request.Items[0] with { Locator = request.Items[0].Locator with { AggregateType = AuditAggregateType.Lsku } };
        var missingRequest = new SelectedAuditIntentDeliveryRequest(Guid.NewGuid(), _tenant, [missing]);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, missingRequest, TimeProvider.System);
        _commands.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.PrepareSelectedAsync(missingRequest));
        Assert.DoesNotContain(_commands, command => command is "create" or "createIndexes" or "insert");
    }

    [Theory]
    [InlineData(AuditAggregateType.ProductDefinitionRevision, ProductAuditOperation.ProductDefinitionRevisionDraftCreated, "mdm_product_definition_revisions")]
    [InlineData(AuditAggregateType.Gsku, ProductAuditOperation.GskuDraftCreated, "mdm_gskus")]
    [InlineData(AuditAggregateType.Lsku, ProductAuditOperation.LskuDraftCreated, "mdm_lskus")]
    [InlineData(AuditAggregateType.ProductAbbreviation, ProductAuditOperation.ProductAbbreviationAllocationRequested, "mdm_product_abbreviation_register")]
    [InlineData(AuditAggregateType.ProductLegalEntityScopePolicy, ProductAuditOperation.ProductLegalEntityScopePolicyCreated, "mdm_product_legal_entity_scope_policies")]
    public async Task Every_first_five_aggregate_family_supports_exact_claim_and_receipt(AuditAggregateType type, ProductAuditOperation operation, string collection)
    {
        var intent = Intent(operation); intent.AggregateType = type;
        await Insert(collection, intent);
        var request = Request(intent);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await repository.PrepareSelectedAsync(request);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(request.Items[0].Locator, 0, "owned-test", TimeSpan.FromMinutes(1)));
        Assert.NotNull(await repository.ReadClaimedPayloadAsync(claim));
        Assert.True(await repository.AcknowledgeAndCompactAsync(claim, Acknowledgement(intent), "receipt-test"));
        Assert.NotNull(await repository.ReadSelectedReceiptAsync(request.Items[0].Locator));
        Assert.Equal(7, (await Read(collection, intent))["Version"].AsInt32);
    }

    [Fact]
    public async Task Expired_owner_cannot_advance_after_selected_reclaim()
    {
        var intent = Intent(ProductAuditOperation.GlobalProductDraftCreated); await Insert(Products, intent);
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var request = Request(intent);
        var oldRepository = AuditIntentDeliveryRepository.CreateSelected(_database, request, clock);
        await oldRepository.PrepareSelectedAsync(request);
        var oldClaim = Assert.IsType<AuditIntentClaim>(await oldRepository.TryClaimAsync(request.Items[0].Locator, 0, "old", TimeSpan.FromSeconds(10)));
        clock.Now += TimeSpan.FromSeconds(11);
        var next = Request(intent, 1);
        var newRepository = AuditIntentDeliveryRepository.CreateSelected(_database, next, clock);
        await newRepository.PrepareSelectedAsync(next);
        var newClaim = Assert.IsType<AuditIntentClaim>(await newRepository.TryClaimAsync(next.Items[0].Locator, 1, "new", TimeSpan.FromMinutes(1)));
        Assert.Equal(2, newClaim.ClaimGeneration);
        Assert.Null(await oldRepository.ReadClaimedPayloadAsync(oldClaim));
        Assert.False(await oldRepository.MarkRetryableFailureAsync(oldClaim, TimeSpan.FromSeconds(1), "stale-owner"));
        Assert.False(await oldRepository.AcknowledgeAndCompactAsync(oldClaim, Acknowledgement(intent), "receipt-test"));
        Assert.False(await oldRepository.MarkDeadLetterAsync(oldClaim, "stale-owner"));
        Assert.False(await oldRepository.MarkDeliveredAsync(oldClaim, Acknowledgement(intent)));
        Assert.False(await oldRepository.CompactDeliveredAsync(oldClaim, "receipt-test"));
        Assert.True(await newRepository.AcknowledgeAndCompactAsync(newClaim, Acknowledgement(intent), "receipt-test"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Postclaim_actor_drift_and_foreign_tenant_deny_all_repository_mutations(bool foreignTenant)
    {
        var intent = Intent(ProductAuditOperation.GlobalProductDraftCreated); await Insert(Products, intent);
        var request = Request(intent);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await repository.PrepareSelectedAsync(request);
        var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(request.Items[0].Locator, 0, "test", TimeSpan.FromMinutes(1)));
        if (foreignTenant) claim = claim with { Locator = claim.Locator with { TenantId = _foreign } };
        else await _database.GetCollection<BsonDocument>(Products).UpdateOneAsync(new BsonDocument("_id", Binary(intent.AggregateId)),
            new BsonDocument("$set", new BsonDocument("AuditIntents.0.ActorId", "postclaim-changed-actor")));
        var before = await Read(Products, intent);
        _commands.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ReadClaimedPayloadAsync(claim));
        await Denied(() => repository.MarkRetryableFailureAsync(claim, TimeSpan.FromSeconds(1), "test"));
        await Denied(() => repository.MarkDeadLetterAsync(claim, "test"));
        await Denied(() => repository.MarkDeliveredAsync(claim, Acknowledgement(intent)));
        await Denied(() => repository.AcknowledgeAndCompactAsync(claim, Acknowledgement(intent), "receipt-test"));
        await Denied(() => repository.CompactDeliveredAsync(claim, "receipt-test"));
        Assert.Equal(before, await Read(Products, intent));
        Assert.DoesNotContain(_commands, name => name is "insert" or "update" or "findAndModify" or "createIndexes");
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("receipt-evidence")]
    [InlineData("receipt-tenant")]
    [InlineData("receipt-key")]
    public async Task Legacy_intent_and_compacted_receipt_drift_fail_without_mutation(string fault)
    {
        var intent = Intent(ProductAuditOperation.GlobalProductDraftCreated);
        if (fault == "legacy") { intent.TemporalStorageVersion = null; intent.TimestampUtcTicksV1 = null; }
        await Insert(Products, intent);
        var request = Request(intent);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        if (fault != "legacy")
        {
            await repository.PrepareSelectedAsync(request);
            var claim = Assert.IsType<AuditIntentClaim>(await repository.TryClaimAsync(request.Items[0].Locator, 0, "test", TimeSpan.FromMinutes(1)));
            Assert.True(await repository.AcknowledgeAndCompactAsync(claim, Acknowledgement(intent), "receipt-test"));
            var patch = fault switch { "receipt-evidence" => new BsonDocument("AuditIntentReceipts.0.EvidenceHash", new string('B', 64)),
                "receipt-tenant" => new BsonDocument("AuditIntentReceipts.0.TenantId", Binary(_foreign)),
                _ => new BsonDocument("AuditIntentReceipts.0.CentralIdempotencyKey", "foreign-key") };
            await _database.GetCollection<BsonDocument>(Products).UpdateOneAsync(new BsonDocument("_id", Binary(intent.AggregateId)), new BsonDocument("$set", patch));
        }
        var before = await Read(Products, intent);
        var replay = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => replay.PrepareSelectedAsync(request));
        Assert.Equal(before, await Read(Products, intent));
    }

    [Theory]
    [InlineData("fg")]
    [InlineData("unproven-consumed")]
    [InlineData("wrong-reserved-key")]
    public async Task Reservation_scope_never_promotes_FG_unproven_or_wrong_command_evidence(string fault)
    {
        var intent = Intent(fault == "unproven-consumed" ? ProductAuditOperation.CodeConsumed : ProductAuditOperation.CodeReserved);
        intent.AggregateType = AuditAggregateType.CodeReservation;
        var type = fault == "fg" ? CodeBearingEntityType.FinishedGood : CodeBearingEntityType.GlobalProduct;
        var code = fault == "fg" ? "FG-000000000001" : "GP-000000000001";
        intent.EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{_tenant:N}|{intent.AggregateId:N}|{code}|{type}|RESERVED")));
        var reservation = new CodeReservation { Id = intent.AggregateId, TenantId = _tenant, EntityType = type,
            ReservedCode = code, ReservedByActorId = intent.ActorId, ReservationCommandId = intent.CommandId,
            ConsumedEntityId = Guid.NewGuid(), AuditIntents = [intent] };
        await _database.GetCollection<CodeReservation>(Reservations).InsertOneAsync(reservation);
        var before = await Read(Reservations, intent);
        var request = Request(intent);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.PrepareSelectedAsync(request));
        Assert.Equal(before, await Read(Reservations, intent));
    }

    [Fact]
    public async Task Real_loopback_http_response_loss_then_duplicate_receipt_compacts_once_and_replay_sends_nothing()
    {
        // Actual HTTP transport and Mongo bookkeeping; the central durable store is deliberately a simulation.
        // This test is not proof of Local Development Platform ingestion or audit_events delivery.
        var intent = Intent(ProductAuditOperation.GlobalProductDraftCreated); await Insert(Products, intent);
        var clock = new TestClock(DateTimeOffset.UtcNow);
        using var listener = new HttpListener();
        var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var committed = new HashSet<string>(StringComparer.Ordinal);
        var received = 0;
        var central = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal("Bearer test-only-token", context.Request.Headers["Authorization"]);
                Assert.Null(context.Request.Headers["X-Tenant-Id"]);
                using var body = await JsonDocument.ParseAsync(context.Request.InputStream);
                Assert.Equal(intent.IntentId, body.RootElement.GetProperty("intentId").GetGuid());
                Assert.Equal(intent.ActorId, body.RootElement.GetProperty("actorId").GetString());
                var key = AuditIntentContract.BuildCentralIdempotencyKey(intent.TenantId, intent.IntentId, intent.ContractVersion!);
                var duplicate = !committed.Add(key);
                Interlocked.Increment(ref received);
                if (attempt == 0)
                {
                    // Central simulated commit happened, but receipt cannot be returned to the client.
                    context.Response.StatusCode = 503;
                    context.Response.Close();
                    continue;
                }
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new { data = new { centralAcknowledgement = "receipt-test",
                    centralIdempotencyKey = key, contractVersion = intent.ContractVersion, acceptedAt = DateTimeOffset.UtcNow,
                    duplicate }, statusCode = 200, isSuccessful = true, errors = Array.Empty<string>(), reason_code = (string?)null,
                    correlation_id = "test-correlation" });
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        });
        var client = new PlatformTrustedSourceAuditIntentClient(new LoopbackClientFactory(),
            Options.Create(new TrustedSourceAuditIntentClientOptions { PlatformBaseUrl = $"http://127.0.0.1:{port}/" }), clock);
        var identity = new TestIdentity();
        var request = Request(intent);
        var repository = AuditIntentDeliveryRepository.CreateSelected(_database, request, clock);
        var first = await new AuditIntentDeliveryProcessor(repository, identity, client).ProcessSelectedAsync(
            request, "test", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), 3);
        Assert.Equal(1, first.Batch.RetryScheduled);
        Assert.Empty(first.Receipts);
        clock.Now += TimeSpan.FromSeconds(2);
        var retryRequest = Request(intent, 1);
        var retryRepository = AuditIntentDeliveryRepository.CreateSelected(_database, retryRequest, clock);
        var processor = new AuditIntentDeliveryProcessor(retryRepository, identity, client);
        var second = await processor.ProcessSelectedAsync(retryRequest, "test", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), 3);
        Assert.Equal(1, second.Batch.Accepted);
        Assert.Single(second.Receipts);
        await central;
        Assert.Equal(2, received);
        Assert.Single(committed);
        var replayRepository = AuditIntentDeliveryRepository.CreateSelected(_database, retryRequest, clock);
        var replay = await new AuditIntentDeliveryProcessor(replayRepository, identity, client).ProcessSelectedAsync(
            retryRequest, "test", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1), 3);
        Assert.Single(replay.Receipts);
        Assert.Equal(0, replay.Batch.Claimed);
        Assert.Equal(2, received);
        Assert.Equal(2, identity.Calls);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class TestIdentity : ITrustedSourceAuditServiceIdentityProvider
    {
        public int Calls { get; private set; }
        public Task<TrustedSourceAuditServiceIdentity> GetAsync(Guid tenantId, string audience, bool forceRefresh, CancellationToken cancellationToken = default)
        {
            Assert.Equal(AuditIntentDeliveryProcessor.RequiredAudience, audience);
            Calls++;
            return Task.FromResult(new TrustedSourceAuditServiceIdentity("test-only-token", DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
    private sealed class LoopbackClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            { Timeout = Timeout.InfiniteTimeSpan };
    }

    private LocalAuditIntent Intent(ProductAuditOperation operation)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        return new() { IntentId = Guid.NewGuid(), TenantId = _tenant, AggregateId = Guid.NewGuid(),
            AggregateType = AuditAggregateType.GlobalProduct, Operation = operation, ActorId = "original-human-actor",
            CommandId = "test-owned-command", CausationId = "cause", CorrelationId = Guid.NewGuid().ToString("D"),
            ContractVersion = "mod-0290.audit-intent.v1", PreVersion = 6, PostVersion = 7, Sequence = 1,
            TimestampUtc = now, TimestampUtcTicksV1 = now.UtcTicks, TemporalStorageVersion = 1,
            EvidenceHash = new string('A', 64), IdempotencyKey = Guid.NewGuid().ToString("D") };
    }
    private SelectedAuditIntentDeliveryRequest Request(LocalAuditIntent intent, long generation = 0, string? evidence = null)
        => new(Guid.NewGuid(), _tenant, [new(new(_tenant, intent.AggregateType, intent.AggregateId, intent.IntentId), generation, evidence ?? intent.EvidenceHash)]);
    private Task Insert(string collection, LocalAuditIntent intent, bool deleted = false)
        => _database.GetCollection<BsonDocument>(collection).InsertOneAsync(new BsonDocument
        { { "_id", Binary(intent.AggregateId) }, { "TenantId", Binary(intent.TenantId) }, { "Version", 7 }, { "IsDeleted", deleted },
            { "AuditIntents", new BsonArray { intent.ToBsonDocument() } }, { "AuditIntentReceipts", new BsonArray() } });
    private Task<BsonDocument> Read(string collection, LocalAuditIntent intent)
        => _database.GetCollection<BsonDocument>(collection).Find(new BsonDocument("_id", Binary(intent.AggregateId))).SingleAsync();
    private static BsonBinaryData Binary(Guid id) => new(id, GuidRepresentation.Standard);
    private static async Task Denied(Func<Task<bool>> action)
    {
        try { Assert.False(await action()); }
        catch (InvalidOperationException) { } // Both supported rejection forms must leave the persisted row unchanged.
    }
    private static AuditIntentAcknowledgement Acknowledgement(LocalAuditIntent intent) => new("receipt-test",
        AuditIntentContract.BuildCentralIdempotencyKey(intent.TenantId, intent.IntentId, intent.ContractVersion!), intent.ContractVersion!, DateTimeOffset.UtcNow);
    private Task<string> CreateExactIndex(string collection) => _database.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(ExactKeys(), new CreateIndexOptions
            { Name = $"ix_{collection}_{AuditIntentTemporalMigrationState.ExactIndexSuffix}" }));
    private static BsonDocument ExactKeys() => new() { { "TenantId", 1 }, { "AuditIntents.TemporalStorageVersion", 1 },
            { "AuditIntents.DeliveryState", 1 }, { "AuditIntents.NextRetryAtUtcTicksV1", 1 }, { "AuditIntents.LeaseUntilUtcTicksV1", 1 },
            { "AuditIntents.TimestampUtcTicksV1", 1 }, { "AuditIntents.IntentId", 1 } };
}
