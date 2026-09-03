using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class ProductLegalEntityScopeWriteAdmissionMongoTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Same_command_replays_exact_lease_and_stale_generation_cannot_release_no_ABA()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True((await repository.CreateAsync(state)).Succeeded);
        var requested = Lease(Guid.NewGuid(), Guid.NewGuid(), "CreateLskuDraft", "A");

        var first = await repository.AcquireWriterLeaseAsync(requested);
        var replay = await repository.AcquireWriterLeaseAsync(Lease(requested.CommandId, requested.ActorId, requested.MutationKind, "A"));
        Assert.True(first.Acquired);
        Assert.Equal(first.Lease!.Token, replay.Lease!.Token);
        Assert.Equal(first.Lease.Generation, replay.Lease.Generation);
        Assert.False(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation));
        Assert.True(await repository.BindWriterLeaseBaselineAsync(first.Lease.Token, first.Lease.Generation, new string('B', 64)));
        Assert.False(await repository.ReleaseWriterLeaseAsync(Guid.NewGuid(), first.Lease.Generation));
        Assert.False(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation + 1));
        Assert.True(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation));

        var second = await repository.AcquireWriterLeaseAsync(Lease(Guid.NewGuid(), Guid.NewGuid(), "CreateLskuDraft", "C"));
        Assert.True(second.Acquired);
        Assert.True(second.Lease!.Generation > first.Lease.Generation);
        Assert.False(await repository.ReleaseWriterLeaseAsync(first.Lease.Token, first.Lease.Generation));
    }

    [Fact]
    public async Task Pre_H1b_rollout_without_writer_fields_acquires_generation_one_without_migration()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var repository = scope.RolloutRepository();
        var state = ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True((await repository.CreateAsync(state)).Succeeded);
        await scope.RolloutCollection.UpdateOneAsync(
            item => item.TenantId == scope.TenantId && item.Id == state.Id,
            Builders<ProductLegalEntityScopeRolloutState>.Update
                .Unset(nameof(ProductLegalEntityScopeRolloutState.WriterLeaseGeneration))
                .Unset(nameof(ProductLegalEntityScopeRolloutState.ActiveWriterLease))
                .Unset(nameof(ProductLegalEntityScopeRolloutState.ActiveFence)));

        var acquired = await repository.AcquireWriterLeaseAsync(
            Lease(Guid.NewGuid(), Guid.NewGuid(), "RequestProductAbbreviationAllocation", "L"));

        Assert.True(acquired.Acquired);
        Assert.NotNull(acquired.Lease);
        Assert.Equal(1, acquired.Lease.Generation);
        var persisted = await scope.RolloutCollection
            .Find(item => item.TenantId == scope.TenantId && item.Id == state.Id)
            .SingleAsync();
        Assert.Equal(1, persisted.WriterLeaseGeneration);
        Assert.Equal(acquired.Lease.Token, persisted.ActiveWriterLease!.Token);
    }

    [Fact]
    public async Task Tenant_B_cannot_observe_or_release_tenant_A_lease_and_payload_drift_is_denied()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenantA = scope.RolloutRepository();
        Assert.True((await tenantA.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
        var command = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var acquired = await tenantA.AcquireWriterLeaseAsync(Lease(command, actor, "CreateFinishedGoodDraft", "A"));
        var drift = await tenantA.AcquireWriterLeaseAsync(Lease(command, actor, "CreateFinishedGoodDraft", "B"));
        var tenantB = scope.RolloutRepository(Guid.NewGuid());

        Assert.False(drift.Acquired);
        Assert.Equal("PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE", drift.FailureCode);
        Assert.Null(await tenantB.GetAsync());
        Assert.False(await tenantB.ReleaseWriterLeaseAsync(acquired.Lease!.Token, acquired.Lease.Generation));
    }

    [Fact]
    public async Task Coordinator_zero_delta_releases_while_202_exception_and_cancellation_retain()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenant = new TenantContext(); tenant.SetTenant(scope.TenantId);
        var repository = scope.RolloutRepository();
        Assert.True((await repository.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(repository,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(Guid.NewGuid().ToString("D")), tenant, new FixedClock());

        var first = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
        await coordinator.CompleteAsync(first, false, 409, false, CancellationToken.None);
        Assert.Null((await repository.GetAsync())!.ActiveWriterLease);

        foreach (var (status, exceptional) in new[] { (202, false), (500, true), (499, true) })
        {
            var admission = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
            await coordinator.CompleteAsync(admission, false, status, exceptional, CancellationToken.None);
            Assert.NotNull((await repository.GetAsync())!.ActiveWriterLease);
            var lease = (await repository.GetAsync())!.ActiveWriterLease!;
            Assert.True(await repository.ReleaseWriterLeaseAsync(lease.Token, lease.Generation));
        }
    }

    [Fact]
    public async Task Coordinator_ignores_delivery_owned_progress_but_retains_for_partial_immutable_intent()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenant = new TenantContext(); tenant.SetTenant(scope.TenantId);
        var repository = scope.RolloutRepository();
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.True((await repository.CreateAsync(rollout)).Succeeded);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(repository,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(Guid.NewGuid().ToString("D")), tenant, new FixedClock());
        var collection = scope.Database.GetCollection<BsonDocument>(
            "mdm_product_legal_entity_scope_rollout_states");

        var deliveryOnly = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", rollout.Id),
            Builders<BsonDocument>.Update.Push("AuditIntentReceipts", new BsonDocument
            {
                { "IntentId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "CentralAcknowledgement", "receipt-progress" }
            }));
        await coordinator.CompleteAsync(deliveryOnly, false, 409, false, CancellationToken.None);
        Assert.Null((await repository.GetAsync())!.ActiveWriterLease);

        var partialMutation = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
        await collection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", rollout.Id),
            Builders<BsonDocument>.Update.Push("AuditIntents", new BsonDocument
            {
                { "IntentId", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "TenantId", new BsonBinaryData(scope.TenantId, GuidRepresentation.Standard) },
                { "AggregateType", (int)AuditAggregateType.ProductLegalEntityScopeRolloutState },
                { "AggregateId", new BsonBinaryData(rollout.Id, GuidRepresentation.Standard) },
                { "SourceService", AuditIntentContract.SourceService },
                { "CommandId", Guid.NewGuid().ToString("D") }, { "EvidenceHash", new string('A', 64) }
            }));
        await coordinator.CompleteAsync(partialMutation, false, 409, false, CancellationToken.None);
        Assert.NotNull((await repository.GetAsync())!.ActiveWriterLease);
    }

    [Fact]
    public async Task Coordinator_allows_only_exact_GSKU_facade_inner_nesting()
    {
        await using var scope = await ProductScopeMongoScope.CreateAsync();
        var tenant = new TenantContext(); tenant.SetTenant(scope.TenantId);
        var repository = scope.RolloutRepository();
        Assert.True((await repository.CreateAsync(ProductLegalEntityScopeRolloutState.CreatePreparation(
            scope.TenantId, Guid.NewGuid(), Guid.NewGuid(), Now))).Succeeded);
        var coordinator = new ProductLegalEntityScopeWriteFenceCoordinator(repository,
            new ProductLegalEntityScopeOperationalReadinessRepository(scope.Database, tenant),
            new Actor(Guid.NewGuid().ToString("D")), tenant, new FixedClock());
        var productId = Guid.NewGuid();
        const string operation = "exact-operation";
        var facade = new CreateFirstGskuDraftFacadeCommand(
            new ProductItemSkuMasterModels.CreateFirstGskuDraftFacadeRequest
            {
                GlobalProductId = productId, PackQuantity = 10, PackUomCode = "EA"
            }, operation);
        var inner = new CreateFirstGskuDraftCommand(new ProductItemSkuMasterModels.CreateFirstGskuDraftRequest
        {
            GlobalProductId = productId, PackQuantity = 10, PackUomCode = "EA",
            CreationCommandId = $"GSKU:{operation.ToUpperInvariant()}"
        });

        var parent = await coordinator.EnterAsync(facade, CancellationToken.None);
        Assert.True(parent.OwnsLease);
        using (coordinator.Activate(parent))
        {
            var exactChild = await coordinator.EnterAsync(inner, CancellationToken.None);
            var unrelated = await coordinator.EnterAsync(Command(Guid.NewGuid()), CancellationToken.None);
            Assert.True(exactChild.IsAllowed); Assert.False(exactChild.OwnsLease);
            Assert.False(unrelated.IsAllowed);
            Assert.Equal("PRODUCT_SCOPE_NESTED_MUTATION_NOT_AUTHORIZED", unrelated.FailureCode);
            await coordinator.CompleteAsync(exactChild, true, 201, false, CancellationToken.None);
        }
        await coordinator.CompleteAsync(parent, true, 201, false, CancellationToken.None);
        Assert.Null((await repository.GetAsync())!.ActiveWriterLease);

        var directInner = await coordinator.EnterAsync(inner, CancellationToken.None);
        Assert.True(directInner.OwnsLease);
        await coordinator.CompleteAsync(directInner, true, 201, false, CancellationToken.None);
    }

    private static CreateLskuDraftCommand Command(Guid identity) => new(new ProductItemSkuMasterModels.CreateLskuDraftRequest
    {
        GskuId = Guid.NewGuid(), MarketCode = "TR", IdempotencyKey = identity.ToString("D")
    });
    private sealed class Actor(string actorId) : IProductIdentityActorContext { public string ActorId => actorId; }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }


    private static ProductLegalEntityScopeWriterLease Lease(Guid command, Guid actor, string kind, string fingerprintSeed) => new()
    {
        Token = Guid.NewGuid(), Generation = 1, CommandId = command, ActorId = actor,
        MutationKind = kind, PayloadFingerprint = new string(fingerprintSeed[0], 64),
        Owner = $"Diten.MDM:{kind}", AcquiredAtUtc = Now, ExpiresAtUtc = Now.AddSeconds(120)
    };
}
