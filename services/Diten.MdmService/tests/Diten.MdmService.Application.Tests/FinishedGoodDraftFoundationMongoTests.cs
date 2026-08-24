using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FinishedGoodDraftFoundationMongoTests
{
    [Fact]
    public async Task Draft_and_identity_approved_gskus_allow_many_finished_goods_with_idempotent_replay()
    {
        await using var scope = await MongoScope.CreateAsync();
        var draft = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var approved = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved);

        var first = await scope.Create(scope.TenantA, draft.Id, " first-command ");
        var replay = await scope.Create(scope.TenantA, draft.Id, "FIRST-COMMAND");
        var second = await scope.Create(scope.TenantA, draft.Id, "second-command");
        var third = await scope.Create(scope.TenantA, approved.Id, "approved-command");

        Assert.True(first.IsSuccessful, string.Join(',', first.Errors));
        Assert.True(replay.IsSuccessful, string.Join(',', replay.Errors));
        Assert.True(second.IsSuccessful, string.Join(',', second.Errors));
        Assert.True(third.IsSuccessful, string.Join(',', third.Errors));
        Assert.Equal(first.Data!.FinishedGoodId, replay.Data!.FinishedGoodId);
        Assert.Equal(draft.Id, first.Data.GskuId);
        Assert.Equal(draft.CanonicalCode, first.Data.GskuDisplay());
        Assert.NotEqual(first.Data.CanonicalCode, second.Data!.CanonicalCode);
        Assert.Equal(3, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Empty));
        Assert.Equal(3, await scope.Reservations.CountDocumentsAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)));
    }

    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.PendingIdentityApproval)]
    [InlineData(ProductIdentityLifecycleStatus.Retired)]
    public async Task Non_referenceable_gsku_is_rejected_before_reservation(ProductIdentityLifecycleStatus status)
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, status);

        var result = await scope.Create(scope.TenantA, gsku.Id, "blocked-status");

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("GSKU_NOT_REFERENCEABLE", result.Errors);
        Assert.Equal(0, await scope.Reservations.CountDocumentsAsync(Builders<CodeReservation>.Filter.Empty));
        Assert.Equal(0, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Empty));
    }

    [Fact]
    public async Task Missing_cross_tenant_and_soft_deleted_gskus_are_indistinguishably_rejected_before_reservation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var foreign = await scope.InsertGskuAsync(scope.TenantB, ProductIdentityLifecycleStatus.Draft);
        var deleted = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft, isDeleted: true);

        var results = new[]
        {
            await scope.Create(scope.TenantA, Guid.NewGuid(), "missing"),
            await scope.Create(scope.TenantA, foreign.Id, "foreign"),
            await scope.Create(scope.TenantA, deleted.Id, "deleted")
        };

        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(404, result.StatusCode);
            Assert.Contains("GSKU_NOT_REFERENCEABLE", result.Errors);
        });
        Assert.Equal(0, await scope.Reservations.CountDocumentsAsync(Builders<CodeReservation>.Filter.Empty));
    }

    [Fact]
    public async Task Conflicting_replay_and_tombstoned_command_never_allocate_a_second_code()
    {
        await using var scope = await MongoScope.CreateAsync();
        var firstGsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var otherGsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var created = await scope.Create(scope.TenantA, firstGsku.Id, "stable-command");
        var drift = await scope.Create(scope.TenantA, otherGsku.Id, "stable-command");
        await scope.FinishedGoods.UpdateOneAsync(
            item => item.Id == created.Data!.FinishedGoodId,
            Builders<FinishedGood>.Update.Set(item => item.IsDeleted, true).Set(item => item.DeletedAt, DateTimeOffset.UtcNow));
        var tombstoneReplay = await scope.Create(scope.TenantA, firstGsku.Id, "stable-command");

        Assert.False(drift.IsSuccessful);
        Assert.Equal(409, drift.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_CONFLICT", drift.Errors);
        Assert.False(tombstoneReplay.IsSuccessful);
        Assert.Equal(409, tombstoneReplay.StatusCode);
        Assert.Contains("CREATION_COMMAND_TOMBSTONED", tombstoneReplay.Errors);
        Assert.Equal(1, await scope.Reservations.CountDocumentsAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)));
        Assert.Equal(1, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Empty));

        var replacement = await scope.Create(scope.TenantA, firstGsku.Id, "replacement-command");
        Assert.True(replacement.IsSuccessful);
        Assert.NotEqual(created.Data!.CanonicalCode, replacement.Data!.CanonicalCode);
        Assert.Equal(2, await scope.Reservations.CountDocumentsAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)));
    }

    [Fact]
    public async Task Concurrent_same_command_has_one_identity_and_one_consumed_confirmed_reservation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => scope.Create(scope.TenantA, gsku.Id, "concurrent-command")));

        Assert.All(results, result => Assert.True(result.IsSuccessful, string.Join(',', result.Errors)));
        Assert.Single(results.Select(result => result.Data!.FinishedGoodId).Distinct());
        var reservation = Assert.Single(await scope.Reservations.Find(
            Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)).ToListAsync());
        Assert.Equal(CodeReservationState.Consumed, reservation.ReservationState);
        Assert.Equal(CodeReservationBindingState.Confirmed, reservation.BindingState);
        Assert.Equal(1, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Empty));
    }

    [Fact]
    public async Task Pre_cancelled_real_mongo_create_propagates_cancellation_without_reservation_or_identity_write()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var context = scope.Context(scope.TenantA);
        var handler = new CreateFinishedGoodDraftHandler(
            new CodeReservationRepository(scope.Database, context),
            new FinishedGoodRepository(scope.Database, context),
            new GskuRepository(scope.Database, context),
            context,
            new ActorContext());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(
            new CreateFinishedGoodDraftCommand(new()
            {
                GskuId = gsku.Id,
                IdempotencyKey = "cancelled-real-mongo-command"
            }),
            cancellation.Token));

        Assert.Equal(0, await scope.Reservations.CountDocumentsAsync(
            Builders<CodeReservation>.Filter.Eq(x => x.EntityType, CodeBearingEntityType.FinishedGood)));
        Assert.Equal(0, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Empty));
    }

    [Fact]
    public async Task Concurrent_distinct_commands_for_one_gsku_create_distinct_codes_without_a_cardinality_cap()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(index => scope.Create(scope.TenantA, gsku.Id, $"distinct-{index}")));

        Assert.All(results, result => Assert.True(result.IsSuccessful, string.Join(',', result.Errors)));
        Assert.Equal(8, results.Select(result => result.Data!.FinishedGoodId).Distinct().Count());
        Assert.Equal(8, results.Select(result => result.Data!.CanonicalCode).Distinct().Count());
        Assert.All(results, result => Assert.Equal(gsku.Id, result.Data!.GskuId));
        Assert.Equal(8, await scope.FinishedGoods.CountDocumentsAsync(
            Builders<FinishedGood>.Filter.Eq(item => item.GskuId, gsku.Id)));
    }

    [Fact]
    public async Task Stale_finished_good_binding_confirmation_is_rejected_without_aggregate_mutation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var repository = new CodeReservationRepository(scope.Database, scope.Context(scope.TenantA));
        var reservation = await repository.ReserveAsync(
            CodeBearingEntityType.FinishedGood,
            "stale-binding-reserve",
            "actor",
            "correlation");
        var identityId = Guid.NewGuid();
        var consumed = await repository.ConsumeForIdentityAsync(
            reservation.Id,
            CodeBearingEntityType.FinishedGood,
            identityId,
            reservation.Version,
            "stale-binding-consume",
            "actor",
            "correlation");
        Assert.True(consumed.Succeeded);
        var stale = await repository.ConfirmIdentityBindingAsync(
                reservation.Id,
                identityId,
                0,
                "stale-confirm",
                "actor",
                "correlation");

        Assert.False(stale.Succeeded);
        Assert.Equal("CONCURRENCY_CONFLICT", stale.ErrorCode);
        Assert.Equal(0, await scope.FinishedGoods.CountDocumentsAsync(Builders<FinishedGood>.Filter.Empty));
        var storedReservation = await scope.Reservations.Find(item => item.Id == reservation.Id).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, storedReservation.BindingState);
        Assert.Equal(identityId, storedReservation.ConsumedEntityId);
    }

    [Fact]
    public async Task List_detail_and_selector_are_tenant_scoped_bounded_and_code_only()
    {
        await using var scope = await MongoScope.CreateAsync();
        var draft = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft, "GS-000000000010");
        var approved = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.IdentityApproved, "GS-000000000020");
        _ = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.PendingIdentityApproval, "GS-000000000030");
        _ = await scope.InsertGskuAsync(scope.TenantB, ProductIdentityLifecycleStatus.Draft, "GS-000000000040");
        var first = await scope.Create(scope.TenantA, approved.Id, "list-b");
        var second = await scope.Create(scope.TenantA, draft.Id, "list-a");

        var repositories = scope.Repositories(scope.TenantA);
        var list = await new GetFinishedGoodsHandler(repositories.FinishedGoods, repositories.Gskus).Handle(
            new GetFinishedGoodsQuery { Search = approved.CanonicalCode, PageSize = 20 }, CancellationToken.None);
        var detail = await new GetFinishedGoodByIdHandler(repositories.FinishedGoods, repositories.Gskus).Handle(
            new GetFinishedGoodByIdQuery(first.Data!.FinishedGoodId), CancellationToken.None);
        var selector = await new GetFinishedGoodGskuSelectorHandler(repositories.Gskus).Handle(
            new GetFinishedGoodGskuSelectorQuery { PageSize = 20 }, CancellationToken.None);

        Assert.Single(list.Data!.Items);
        Assert.Equal(first.Data.FinishedGoodId, list.Data.Items[0].Id);
        Assert.Equal(approved.CanonicalCode, list.Data.Items[0].GskuDisplay);
        Assert.Equal(approved.CanonicalCode, detail.Data!.GskuDisplay);
        Assert.Equal([draft.CanonicalCode, approved.CanonicalCode], selector.Data!.Items.Select(item => item.Display));
        Assert.All(selector.Data.Items, item => Assert.Equal(item.CanonicalCode, item.Display));
        Assert.NotEqual(first.Data.FinishedGoodId, second.Data!.FinishedGoodId);
    }

    [Fact]
    public async Task Finished_good_audit_delivery_is_fenced_acknowledged_compacted_and_version_neutral()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var created = await scope.Create(scope.TenantA, gsku.Id, "audit-command");
        var delivery = new AuditIntentDeliveryRepository(scope.Database, scope.Context(scope.TenantA), TimeProvider.System);
        var item = Assert.Single(
            await delivery.DiscoverEligibleAsync(100),
            work => work.Locator.AggregateType == AuditAggregateType.FinishedGood);
        var claim = Assert.IsType<AuditIntentClaim>(await delivery.TryClaimAsync(
            item.Locator, item.ClaimGeneration, "worker-a", TimeSpan.FromMinutes(5)));
        Assert.Null(await delivery.TryClaimAsync(item.Locator, item.ClaimGeneration, "worker-b", TimeSpan.FromMinutes(5)));
        var staleClaim = claim with { ClaimToken = "stale-token" };
        Assert.False(await delivery.MarkRetryableFailureAsync(staleClaim, TimeSpan.FromMinutes(1), "stale"));
        const string contractVersion = "finished-good-v1";
        var acknowledgement = new AuditIntentAcknowledgement(
            "central-ack",
            AuditIntentContract.BuildCentralIdempotencyKey(scope.TenantA, item.Locator.IntentId, contractVersion),
            contractVersion,
            DateTimeOffset.UtcNow);
        Assert.True(await delivery.MarkDeliveredAsync(claim, acknowledgement));
        Assert.True(await delivery.CompactDeliveredAsync(claim, "fg-receipt"));
        Assert.True(await delivery.CompactDeliveredAsync(claim, "fg-receipt"));

        var stored = await scope.FinishedGoods.Find(item => item.Id == created.Data!.FinishedGoodId).SingleAsync();
        Assert.Equal(0, stored.Version);
        Assert.Empty(stored.AuditIntents);
        Assert.Single(stored.AuditIntentReceipts);
    }

    private sealed class MongoScope : IAsyncDisposable
    {
        private readonly IMongoClient _client;
        private readonly string _databaseName;

        private MongoScope(IMongoClient client, IMongoDatabase database, string databaseName)
        {
            _client = client;
            Database = database;
            _databaseName = databaseName;
        }

        public Guid TenantA { get; } = Guid.NewGuid();
        public Guid TenantB { get; } = Guid.NewGuid();
        public IMongoDatabase Database { get; }
        public IMongoCollection<FinishedGood> FinishedGoods => Database.GetCollection<FinishedGood>("mdm_finished_goods");
        public IMongoCollection<CodeReservation> Reservations => Database.GetCollection<CodeReservation>("mdm_code_reservations");

        public static async Task<MongoScope> CreateAsync()
        {
            var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017";
            var settings = MongoClientSettings.FromConnectionString(uri);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = "mdm_fg_" + Guid.NewGuid().ToString("N");
            var database = client.GetDatabase(databaseName);
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));
            return new(client, database, databaseName);
        }

        public TenantContext Context(Guid tenantId)
        {
            var context = new TenantContext();
            context.SetTenant(tenantId);
            return context;
        }

        public (FinishedGoodRepository FinishedGoods, GskuRepository Gskus) Repositories(Guid tenantId)
            => (new(Database, Context(tenantId)), new(Database, Context(tenantId)));

        public async Task<Gsku> InsertGskuAsync(
            Guid tenantId,
            ProductIdentityLifecycleStatus status,
            string? code = null,
            bool isDeleted = false)
        {
            _ = new GskuRepository(Database, Context(tenantId));
            var gsku = new Gsku
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductDefinitionRevisionId = Guid.NewGuid(),
                CanonicalCode = code ?? "GS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                CodeReservationId = Guid.NewGuid(),
                CreationCommandId = Guid.NewGuid().ToString("N"),
                PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES",
                PackQuantity = 1m,
                PackUomCode = "C62",
                LifecycleStatus = status,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null
            };
            await Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
            return gsku;
        }

        public Task<Diten.Shared.Core.Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>> Create(
            Guid tenantId,
            Guid gskuId,
            string idempotencyKey)
        {
            var context = Context(tenantId);
            var handler = new CreateFinishedGoodDraftHandler(
                new CodeReservationRepository(Database, context),
                new FinishedGoodRepository(Database, context),
                new GskuRepository(Database, context),
                context,
                new ActorContext());
            return handler.Handle(new CreateFinishedGoodDraftCommand(new()
            {
                GskuId = gskuId,
                IdempotencyKey = idempotencyKey
            }), CancellationToken.None);
        }

        public async ValueTask DisposeAsync() => await _client.DropDatabaseAsync(_databaseName);
    }

    private sealed class ActorContext : IProductIdentityActorContext
    {
        public string ActorId => "finished-good-test-actor";
    }
}

internal static class FinishedGoodDraftDtoTestExtensions
{
    public static string GskuDisplay(this ProductItemSkuMasterModels.FinishedGoodDraftDto dto)
        => dto.GskuCanonicalCode;
}
