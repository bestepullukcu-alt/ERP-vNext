using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuDraftFoundationMongoTests
{
    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.Draft)]
    [InlineData(ProductIdentityLifecycleStatus.IdentityApproved)]
    public async Task Referenceable_parent_create_persists_identity_evidence_binding_and_replays(
        ProductIdentityLifecycleStatus lifecycle)
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, lifecycle);
        var market = new MarketResolver();
        var handler = scope.Handler(scope.TenantA, market: market);
        var command = Command(gsku.Id, "TR", "create-replay");

        var created = await handler.Handle(command, CancellationToken.None);
        var replay = await handler.Handle(command, CancellationToken.None);

        Assert.True(created.IsSuccessful, string.Join(',', created.Errors));
        Assert.Equal(201, created.StatusCode);
        Assert.True(replay.IsSuccessful, string.Join(',', replay.Errors));
        Assert.Equal(created.Data!.LskuId, replay.Data!.LskuId);
        Assert.Equal(created.Data.CanonicalCode, replay.Data.CanonicalCode);
        Assert.Equal(1, market.Calls);
        var stored = await scope.Lskus.Find(x => x.Id == created.Data.LskuId).SingleAsync();
        Assert.Equal(gsku.Id, stored.GskuId);
        Assert.Equal("TR", stored.MarketCode);
        Assert.Equal("market", stored.MarketSelection.SetCode);
        Assert.Equal("TR", stored.MarketSelection.ValueCode);
        Assert.NotEqual(Guid.Empty, stored.MarketSelection.CatalogVersionId);
        Assert.True(stored.MarketSelection.CatalogVersionNumber > 0);
        Assert.Equal(ReferenceCatalogResolutionMode.Latest, stored.MarketSelection.ResolutionMode);
        Assert.NotEqual(default, stored.MarketSelection.ResolvedAtUtc);
        Assert.Equal(ProductAuditOperation.LskuDraftCreated, Assert.Single(stored.AuditIntents).Operation);
        var reservation = await scope.Reservations.Find(x => x.Id == stored.CodeReservationId).SingleAsync();
        Assert.Equal(CodeBearingEntityType.Lsku, reservation.EntityType);
        Assert.Equal(CodeReservationState.Consumed, reservation.ReservationState);
        Assert.Equal(CodeReservationBindingState.Confirmed, reservation.BindingState);
        Assert.Equal(stored.Id, reservation.ConsumedEntityId);
        Assert.Equal(stored.CanonicalCode, reservation.ReservedCode);
        Assert.Equal(1, await scope.Lskus.CountDocumentsAsync(FilterDefinition<Lsku>.Empty));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("cross-tenant")]
    [InlineData("deleted")]
    [InlineData("pending")]
    [InlineData("retired")]
    public async Task Non_referenceable_parent_states_share_one_non_leaking_failure_before_provider_or_reservation(
        string scenario)
    {
        await using var scope = await MongoScope.CreateAsync();
        var tenant = scenario == "cross-tenant" ? scope.TenantB : scope.TenantA;
        var lifecycle = scenario switch
        {
            "pending" => ProductIdentityLifecycleStatus.PendingIdentityApproval,
            "retired" => ProductIdentityLifecycleStatus.Retired,
            _ => ProductIdentityLifecycleStatus.Draft
        };
        var gskuId = Guid.NewGuid();
        if (scenario != "missing")
        {
            var gsku = await scope.InsertGskuAsync(tenant, lifecycle, gskuId);
            if (scenario == "deleted")
            {
                await scope.Gskus.UpdateOneAsync(
                    x => x.Id == gsku.Id,
                    Builders<Gsku>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAt, DateTimeOffset.UtcNow));
            }
        }

        var market = new MarketResolver();
        var response = await scope.Handler(scope.TenantA, market: market)
            .Handle(Command(gskuId, "TR", "non-referenceable-" + scenario), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Contains("GSKU_NOT_REFERENCEABLE", response.Errors);
        Assert.Equal(0, market.Calls);
        Assert.Equal(0, await scope.Reservations.CountDocumentsAsync(FilterDefinition<CodeReservation>.Empty));
        Assert.Equal(0, await scope.Lskus.CountDocumentsAsync(FilterDefinition<Lsku>.Empty));
    }

    [Fact]
    public async Task Payload_drift_conflicts_without_second_code_or_provider_resolution()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var market = new MarketResolver();
        var handler = scope.Handler(scope.TenantA, market: market);
        Assert.True((await handler.Handle(Command(gsku.Id, "TR", "drift"), CancellationToken.None)).IsSuccessful);

        var drift = await handler.Handle(Command(gsku.Id, "US", "drift"), CancellationToken.None);

        Assert.False(drift.IsSuccessful);
        Assert.Equal(409, drift.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_CONFLICT", drift.Errors);
        Assert.Equal(1, market.Calls);
        Assert.Equal(1, await scope.Lskus.CountDocumentsAsync(FilterDefinition<Lsku>.Empty));
        Assert.Equal(1, await scope.Reservations.CountDocumentsAsync(FilterDefinition<CodeReservation>.Empty));
    }

    [Fact]
    public async Task Concurrent_different_commands_have_one_winner_and_replayable_pending_reconciliation_loser()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var first = scope.Handler(scope.TenantA);
        var second = scope.Handler(scope.TenantA);

        var results = await Task.WhenAll(
            first.Handle(Command(gsku.Id, "TR", "race-a"), CancellationToken.None),
            second.Handle(Command(gsku.Id, "TR", "race-b"), CancellationToken.None));

        var winner = Assert.Single(results, result => result.IsSuccessful);
        Assert.Equal(201, winner.StatusCode);
        var loser = Assert.Single(results, result => !result.IsSuccessful);
        Assert.Equal(202, loser.StatusCode);
        Assert.Contains("LSKU_BINDING_RECONCILIATION_REQUIRED", loser.Errors);
        Assert.Equal(1, await scope.Lskus.CountDocumentsAsync(FilterDefinition<Lsku>.Empty));
        var reservations = await scope.Reservations.Find(FilterDefinition<CodeReservation>.Empty).ToListAsync();
        Assert.Equal(2, reservations.Count);
        Assert.Equal(2, reservations.Select(x => x.ReservedCode).Distinct(StringComparer.Ordinal).Count());
        var confirmed = Assert.Single(
            reservations,
            reservation => reservation.BindingState == CodeReservationBindingState.Confirmed);
        var pending = Assert.Single(
            reservations,
            reservation => reservation.BindingState == CodeReservationBindingState.PendingIdentityWrite);
        Assert.Equal(CodeReservationState.Consumed, confirmed.ReservationState);
        Assert.Equal(CodeReservationState.Consumed, pending.ReservationState);
        var stored = await scope.Lskus.Find(FilterDefinition<Lsku>.Empty).SingleAsync();
        Assert.Equal(stored.Id, confirmed.ConsumedEntityId);
        Assert.NotEqual(stored.Id, pending.ConsumedEntityId);
        Assert.Equal(
            ["RACE-A", "RACE-B"],
            reservations.Select(x => x.ReservationCommandId).OrderBy(x => x, StringComparer.Ordinal));

        var losingCommandId = pending.ReservationCommandId;
        var replay = await scope.Handler(scope.TenantA).Handle(
            Command(gsku.Id, "TR", losingCommandId),
            CancellationToken.None);

        Assert.False(replay.IsSuccessful);
        Assert.Equal(202, replay.StatusCode);
        Assert.Contains("LSKU_BINDING_RECONCILIATION_REQUIRED", replay.Errors);
        Assert.Equal(1, await scope.Lskus.CountDocumentsAsync(FilterDefinition<Lsku>.Empty));
        var replayedReservations = await scope.Reservations.Find(FilterDefinition<CodeReservation>.Empty).ToListAsync();
        Assert.Equal(2, replayedReservations.Count);
        Assert.Equal(
            reservations.Select(x => (x.Id, x.ReservedCode)).OrderBy(x => x.Id),
            replayedReservations.Select(x => (x.Id, x.ReservedCode)).OrderBy(x => x.Id));
        var replayedPending = Assert.Single(replayedReservations, x => x.Id == pending.Id);
        Assert.Equal(CodeReservationState.Consumed, replayedPending.ReservationState);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, replayedPending.BindingState);
        var indexes = await (await scope.Lskus.Indexes.ListAsync()).ToListAsync();
        var identityIndex = Assert.Single(indexes, index => index["name"] == "ux_mdm_lskus_tenant_gsku_market");
        Assert.True(identityIndex["unique"].AsBoolean);
        Assert.False(identityIndex.Contains("partialFilterExpression"));
    }

    [Fact]
    public async Task Soft_delete_tombstone_never_releases_gsku_market_or_canonical_code()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var handler = scope.Handler(scope.TenantA);
        var created = await handler.Handle(Command(gsku.Id, "TR", "tombstone-a"), CancellationToken.None);
        Assert.True(created.IsSuccessful);
        var createdData = Assert.IsType<ProductItemSkuMasterModels.LskuDraftDto>(created.Data);
        await scope.Lskus.UpdateOneAsync(
            x => x.Id == createdData.LskuId,
            Builders<Lsku>.Update.Set(x => x.IsDeleted, true).Set(x => x.DeletedAt, DateTimeOffset.UtcNow));

        var retry = await handler.Handle(Command(gsku.Id, "TR", "tombstone-b"), CancellationToken.None);

        Assert.False(retry.IsSuccessful);
        Assert.Equal(409, retry.StatusCode);
        Assert.Equal(1, await scope.Lskus.CountDocumentsAsync(FilterDefinition<Lsku>.Empty));
        var stored = await scope.Lskus.Find(FilterDefinition<Lsku>.Empty).SingleAsync();
        Assert.Equal(createdData.CanonicalCode, stored.CanonicalCode);
        Assert.True(stored.IsDeleted);
    }

    [Fact]
    public async Task Explicit_ambiguous_write_recovers_from_real_mongo_or_remains_pending_for_reconciliation()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var inner = scope.LskuRepository(scope.TenantA);
        var recovered = await scope.Handler(
                scope.TenantA,
                new AmbiguousLskuRepository(inner, persistBeforeAmbiguous: true))
            .Handle(Command(gsku.Id, "TR", "ambiguous-visible"), CancellationToken.None);
        Assert.True(recovered.IsSuccessful, string.Join(',', recovered.Errors));
        Assert.Equal(201, recovered.StatusCode);

        var pending = await scope.Handler(
                scope.TenantA,
                new AmbiguousLskuRepository(inner, persistBeforeAmbiguous: false))
            .Handle(Command(gsku.Id, "US", "ambiguous-invisible"), CancellationToken.None);
        Assert.False(pending.IsSuccessful);
        Assert.Equal(202, pending.StatusCode);
        Assert.Contains("LSKU_BINDING_RECONCILIATION_REQUIRED", pending.Errors);
        var pendingReservation = await scope.Reservations.Find(x => x.ReservationCommandId == "AMBIGUOUS-INVISIBLE")
            .SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, pendingReservation.BindingState);
        Assert.Equal(CodeReservationState.Consumed, pendingReservation.ReservationState);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Provider_failure_mapping_mutates_neither_reservation_nor_identity(int statusCode)
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, ProductIdentityLifecycleStatus.Draft);
        var response = await scope.Handler(scope.TenantA, market: new MarketResolver(statusCode))
            .Handle(Command(gsku.Id, "TR", "provider-" + statusCode), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(0, await scope.Reservations.CountDocumentsAsync(FilterDefinition<CodeReservation>.Empty));
        Assert.Equal(0, await scope.Lskus.CountDocumentsAsync(FilterDefinition<Lsku>.Empty));
    }

    private static CreateLskuDraftCommand Command(Guid gskuId, string marketCode, string idempotencyKey) =>
        new(new ProductItemSkuMasterModels.CreateLskuDraftRequest
        {
            GskuId = gskuId,
            MarketCode = marketCode,
            IdempotencyKey = idempotencyKey
        });

    private sealed class MarketResolver(int? failureStatus = null) : IVerifiedMarketReferenceResolver
    {
        public int Calls { get; private set; }

        public Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(
            string marketCode,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(failureStatus is { } status
                ? VerifiedMarketReferenceResolveResult.Fail(status, status switch
                {
                    404 => "REFERENCE_MARKET_NOT_FOUND",
                    504 => "REFERENCE_PROVIDER_TIMEOUT",
                    _ => "REFERENCE_PROVIDER_UNAVAILABLE"
                })
                : VerifiedMarketReferenceResolveResult.Success(new(
                    "market",
                    marketCode,
                    Guid.Parse("57e1349a-2be7-42c3-a6a0-c8c55329eea8"),
                    7,
                    "LATEST",
                    DateTimeOffset.Parse("2026-08-08T08:00:00+00:00"))));
        }
    }

    private sealed class AmbiguousLskuRepository(ILskuRepository inner, bool persistBeforeAmbiguous) : ILskuRepository
    {
        public Task<Lsku?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default) =>
            inner.GetByCreationCommandIdAsync(creationCommandId, cancellationToken);

        public Task<Lsku?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
            inner.GetByReservationIdAsync(reservationId, cancellationToken);

        public Task<Lsku?> GetByIdentityKeyAsync(Guid gskuId, string marketCode, CancellationToken cancellationToken = default) =>
            inner.GetByIdentityKeyAsync(gskuId, marketCode, cancellationToken);

        public async Task<LskuCreateResult> CreateDraftAsync(Lsku lsku, CancellationToken cancellationToken = default)
        {
            if (persistBeforeAmbiguous)
            {
                var persisted = await inner.CreateDraftAsync(lsku, cancellationToken);
                Assert.True(persisted.Succeeded, persisted.ErrorCode);
            }

            return new(false, null, "LSKU_WRITE_OUTCOME_AMBIGUOUS", WriteOutcomeAmbiguous: true);
        }
    }

    private sealed class ActorContext : IProductIdentityActorContext
    {
        public string ActorId => "lsku-mongo-test";
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
            TenantA = Guid.NewGuid();
            TenantB = Guid.NewGuid();
        }

        public IMongoDatabase Database { get; }
        public Guid TenantA { get; }
        public Guid TenantB { get; }
        public IMongoCollection<Gsku> Gskus => Database.GetCollection<Gsku>("mdm_gskus");
        public IMongoCollection<Lsku> Lskus => Database.GetCollection<Lsku>("mdm_lskus");
        public IMongoCollection<CodeReservation> Reservations =>
            Database.GetCollection<CodeReservation>("mdm_code_reservations");

        public static async Task<MongoScope> CreateAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
                ?? Environment.GetEnvironmentVariable("MONGO_TEST_URI")
                ?? "mongodb://localhost:27017";
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = $"diten_mdm_lsku_tests_{Guid.NewGuid():N}";
            var database = client.GetDatabase(databaseName);
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return new MongoScope(client, database, databaseName);
        }

        public async Task<Gsku> InsertGskuAsync(
            Guid tenantId,
            ProductIdentityLifecycleStatus lifecycle,
            Guid? id = null)
        {
            var gsku = new Gsku
            {
                Id = id ?? Guid.NewGuid(),
                TenantId = tenantId,
                CanonicalCode = "GS-" + Guid.NewGuid().ToString("N"),
                LifecycleStatus = lifecycle,
                Version = 0
            };
            await Gskus.InsertOneAsync(gsku);
            return gsku;
        }

        public LskuRepository LskuRepository(Guid tenantId) => new(Database, Tenant(tenantId));

        public CreateLskuDraftHandler Handler(
            Guid tenantId,
            ILskuRepository? lskus = null,
            IVerifiedMarketReferenceResolver? market = null)
        {
            var tenant = Tenant(tenantId);
            return new CreateLskuDraftHandler(
                new CodeReservationRepository(Database, tenant),
                lskus ?? new LskuRepository(Database, tenant),
                new GskuRepository(Database, tenant),
                market ?? new MarketResolver(),
                tenant,
                new ActorContext());
        }

        public async ValueTask DisposeAsync() => await _client.DropDatabaseAsync(_databaseName);

        private static TenantContext Tenant(Guid tenantId)
        {
            var context = new TenantContext();
            context.SetTenant(tenantId);
            return context;
        }
    }
}
