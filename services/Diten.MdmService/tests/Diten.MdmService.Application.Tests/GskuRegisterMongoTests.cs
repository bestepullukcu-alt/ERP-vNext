using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GskuRegisterMongoTests
{
    [Fact]
    public async Task List_projection_is_batched_ordered_tenant_scoped_and_soft_delete_aware()
    {
        await using var scope = await MongoScope.CreateAsync();
        var visible = await scope.SeedPairAsync(scope.TenantA, "GS-0002", "Visible", false);
        await scope.SeedPairAsync(scope.TenantA, "GS-0001", "Deleted", true);
        await scope.SeedPairAsync(scope.TenantB, "GS-0000", "Other Tenant", false);
        var tenantContext = scope.Context(scope.TenantA);
        var access = ProductLegalEntityScopeTestFixture.Preparation(tenantContext);
        var handler = new GetGskusHandler(
            new GskuRepository(scope.Database, tenantContext),
            new ProductDefinitionRevisionRepository(scope.Database, tenantContext),
            new GlobalProductRepository(scope.Database, tenantContext),
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);

        var response = await handler.Handle(new GetGskusQuery { PageNumber = 1, PageSize = 20 }, default);

        Assert.True(response.IsSuccessful);
        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(visible.Gsku.Id, item.Id);
        Assert.Equal(visible.Product.Id, item.GlobalProductId);
        Assert.Equal("Visible", item.GlobalProductName);
        Assert.Equal(1, response.Data.TotalCount);
    }

    [Fact]
    public async Task Detail_returns_the_same_non_disclosing_404_for_cross_tenant_and_soft_deleted_ids()
    {
        await using var scope = await MongoScope.CreateAsync();
        var crossTenant = await scope.SeedPairAsync(scope.TenantB, "GS-1000", "Other Tenant", false);
        var deleted = await scope.SeedPairAsync(scope.TenantA, "GS-1001", "Deleted", true);
        var tenantContext = scope.Context(scope.TenantA);
        var access = ProductLegalEntityScopeTestFixture.Preparation(tenantContext);
        var handler = new GetGskuByIdHandler(
            new GskuRepository(scope.Database, tenantContext),
            new ProductDefinitionRevisionRepository(scope.Database, tenantContext),
            new GlobalProductRepository(scope.Database, tenantContext),
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);

        var cross = await handler.Handle(new GetGskuByIdQuery(crossTenant.Gsku.Id), default);
        var tombstone = await handler.Handle(new GetGskuByIdQuery(deleted.Gsku.Id), default);

        Assert.Equal(404, cross.StatusCode);
        Assert.Equal(404, tombstone.StatusCode);
        Assert.Equal(cross.Errors, tombstone.Errors);
    }

    [Fact]
    public async Task Enforced_scope_pages_target_inventory_without_tenant_wide_parent_id_fan_out()
    {
        await using var scope = await MongoScope.CreateAsync();
        var legalEntityId = Guid.NewGuid();
        var policies = new List<ProductLegalEntityScopePolicy>();
        for (var index = 0; index < 25; index++)
        {
            var pair = await scope.SeedPairAsync(
                scope.TenantA,
                $"GS-{index:D4}",
                $"Product {index:D4}",
                deleted: false);
            policies.Add(ProductLegalEntityScopePolicy.Create(
                scope.TenantA,
                pair.Product.Id,
                Guid.NewGuid(),
                ProductLegalEntityScopeMode.Scoped,
                [legalEntityId],
                Guid.NewGuid(),
                DateTimeOffset.UtcNow));
        }

        await scope.Database
            .GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertManyAsync(policies);
        var tenantContext = scope.Context(scope.TenantA);
        var policyRepository = new ProductLegalEntityScopePolicyRepository(scope.Database, tenantContext);
        var access = ProductLegalEntityScopeTestFixture.Enforced(
            tenantContext,
            [legalEntityId],
            policyRepository);
        var handler = new GetGskusHandler(
            new GskuRepository(scope.Database, tenantContext),
            new ProductDefinitionRevisionRepository(scope.Database, tenantContext),
            new GlobalProductRepository(scope.Database, tenantContext),
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);

        var response = await handler.Handle(
            new GetGskusQuery { PageNumber = 2, PageSize = 10 },
            default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(25, response.Data!.TotalCount);
        Assert.Equal(10, response.Data.Items.Count);
        Assert.Equal("GS-0010", response.Data.Items[0].CanonicalCode);
        Assert.Equal("GS-0019", response.Data.Items[^1].CanonicalCode);
    }

    [Fact]
    public async Task Enforced_scope_denies_policy_with_valid_current_period_but_corrupt_history()
    {
        await using var scope = await MongoScope.CreateAsync();
        var pair = await scope.SeedPairAsync(scope.TenantA, "GS-2000", "Corrupt History", false);
        var legalEntityId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var policy = ProductLegalEntityScopePolicy.Create(
            scope.TenantA,
            pair.Product.Id,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped,
            [legalEntityId],
            Guid.NewGuid(),
            now.AddMinutes(-2));
        policy.ReplaceCurrent(
            0,
            Guid.NewGuid(),
            ProductLegalEntityScopeMode.Scoped,
            [legalEntityId],
            Guid.NewGuid(),
            now.AddMinutes(-1));
        policy.ScopePeriods[0].ActorId = Guid.Empty;
        await scope.Database
            .GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(policy);

        var tenantContext = scope.Context(scope.TenantA);
        var policyRepository = new ProductLegalEntityScopePolicyRepository(scope.Database, tenantContext);
        var access = ProductLegalEntityScopeTestFixture.Enforced(
            tenantContext,
            [legalEntityId],
            policyRepository);
        var list = new GetGskusHandler(
            new GskuRepository(scope.Database, tenantContext),
            new ProductDefinitionRevisionRepository(scope.Database, tenantContext),
            new GlobalProductRepository(scope.Database, tenantContext),
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);
        var detail = new GetGskuByIdHandler(
            new GskuRepository(scope.Database, tenantContext),
            new ProductDefinitionRevisionRepository(scope.Database, tenantContext),
            new GlobalProductRepository(scope.Database, tenantContext),
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);

        var page = await list.Handle(new GetGskusQuery { PageNumber = 1, PageSize = 20 }, default);
        var item = await detail.Handle(new GetGskuByIdQuery(pair.Gsku.Id), default);

        Assert.True(page.IsSuccessful);
        Assert.Empty(page.Data!.Items);
        Assert.Equal(0, page.Data.TotalCount);
        Assert.Equal(404, item.StatusCode);
        Assert.Contains("GSKU_NOT_FOUND", item.Errors);
    }

    [Fact]
    public async Task Enforced_scope_hides_missing_deleted_and_cross_tenant_parent_chains_identically()
    {
        await using var scope = await MongoScope.CreateAsync();
        var missing = await scope.SeedPairAsync(scope.TenantA, "GS-3000", "Missing Parent", false);
        var deleted = await scope.SeedPairAsync(scope.TenantA, "GS-3001", "Deleted Parent", false);
        var crossTenant = await scope.SeedPairAsync(scope.TenantA, "GS-3002", "Cross Tenant Parent", false);
        var revisions = scope.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions");
        await revisions.DeleteOneAsync(item => item.Id == missing.Revision.Id);
        await revisions.UpdateOneAsync(
            item => item.Id == deleted.Revision.Id,
            Builders<ProductDefinitionRevision>.Update
                .Set(item => item.IsDeleted, true)
                .Set(item => item.DeletedAt, DateTimeOffset.UtcNow));
        await revisions.UpdateOneAsync(
            item => item.Id == crossTenant.Revision.Id,
            Builders<ProductDefinitionRevision>.Update.Set(item => item.TenantId, scope.TenantB));

        var legalEntityId = Guid.NewGuid();
        var policies = new[] { missing, deleted, crossTenant }
            .Select(pair => ProductLegalEntityScopePolicy.Create(
                scope.TenantA,
                pair.Product.Id,
                Guid.NewGuid(),
                ProductLegalEntityScopeMode.Scoped,
                [legalEntityId],
                Guid.NewGuid(),
                DateTimeOffset.UtcNow))
            .ToArray();
        await scope.Database
            .GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertManyAsync(policies);

        var tenantContext = scope.Context(scope.TenantA);
        var policyRepository = new ProductLegalEntityScopePolicyRepository(scope.Database, tenantContext);
        var access = ProductLegalEntityScopeTestFixture.Enforced(
            tenantContext,
            [legalEntityId],
            policyRepository);
        var list = new GetGskusHandler(
            new GskuRepository(scope.Database, tenantContext),
            new ProductDefinitionRevisionRepository(scope.Database, tenantContext),
            new GlobalProductRepository(scope.Database, tenantContext),
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);
        var detail = new GetGskuByIdHandler(
            new GskuRepository(scope.Database, tenantContext),
            new ProductDefinitionRevisionRepository(scope.Database, tenantContext),
            new GlobalProductRepository(scope.Database, tenantContext),
            access.Rollouts,
            access.Policies,
            access.Candidates,
            tenantContext);

        var page = await list.Handle(new GetGskusQuery { PageNumber = 1, PageSize = 20 }, default);
        var responses = await Task.WhenAll(new[] { missing, deleted, crossTenant }
            .Select(pair => detail.Handle(new GetGskuByIdQuery(pair.Gsku.Id), default)));

        Assert.True(page.IsSuccessful);
        Assert.Empty(page.Data!.Items);
        Assert.Equal(0, page.Data.TotalCount);
        Assert.All(responses, response =>
        {
            Assert.Equal(404, response.StatusCode);
            Assert.Contains("GSKU_NOT_FOUND", response.Errors);
        });
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

        public static async Task<MongoScope> CreateAsync()
        {
            var settings = MongoClientSettings.FromConnectionString(
                Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017");
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var databaseName = "MOD0290_GSKU_" + Guid.NewGuid().ToString("N");
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

        public async Task<(GlobalProduct Product, ProductDefinitionRevision Revision, Gsku Gsku)> SeedPairAsync(
            Guid tenantId,
            string gskuCode,
            string productName,
            bool deleted)
        {
            var now = DateTimeOffset.UtcNow;
            var product = new GlobalProduct
            {
                Id = Guid.NewGuid(), TenantId = tenantId, CanonicalCode = "GP-" + Guid.NewGuid().ToString("N")[..8],
                GlobalProductName = productName, GlobalProductNameNormalized = productName.ToUpperInvariant(),
                CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
                CreatedAt = now, UpdatedAt = now
            };
            var revision = new ProductDefinitionRevision
            {
                Id = Guid.NewGuid(), TenantId = tenantId, GlobalProductId = product.Id,
                RevisionIdentifier = "REV-001", CreationCommandId = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft, CreatedAt = now, UpdatedAt = now
            };
            var gsku = new Gsku
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProductDefinitionRevisionId = revision.Id,
                CanonicalCode = gskuCode, CodeReservationId = Guid.NewGuid(), CreationCommandId = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES", PackQuantity = 1, PackUomCode = "C62",
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft, CreatedAt = now, UpdatedAt = now,
                IsDeleted = deleted, DeletedAt = deleted ? now : null
            };
            await Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(product);
            await Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
            await Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
            return (product, revision, gsku);
        }

        public async ValueTask DisposeAsync() => await _client.DropDatabaseAsync(_databaseName);
    }
}

internal sealed record ProductLegalEntityScopeTestDependencies(
    IProductLegalEntityScopeRolloutStateRepository Rollouts,
    IProductLegalEntityScopePolicyRepository Policies,
    ProductLegalEntityScopeCandidateFacade Candidates);

internal static class ProductLegalEntityScopeTestFixture
{
    internal static ProductLegalEntityScopeTestDependencies Preparation(ITenantContext tenantContext)
    {
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            tenantContext.TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        return new(
            new RolloutRepository(rollout),
            new PolicyRepository(),
            new ProductLegalEntityScopeCandidateFacade(
                new ForbiddenProvider(),
                new InMemoryLegalEntityRepository(tenantContext.TenantId, []),
                tenantContext,
                new ActorContext()));
    }

    internal static ProductLegalEntityScopeTestDependencies Enforced(
        ITenantContext tenantContext,
        IReadOnlyCollection<Guid> candidateLegalEntityIds,
        IProductLegalEntityScopePolicyRepository policies)
    {
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            tenantContext.TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        var ordered = candidateLegalEntityIds
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var entities = ordered.Select(id => new LegalEntity
        {
            Id = id,
            TenantId = tenantContext.TenantId,
            Code = "LE-" + id.ToString("N")[..8],
            LegalName = "Scope Candidate",
            OperationalStatus = LegalEntityOperationalStatus.Active,
            IsDeleted = false
        });
        return new(
            new RolloutRepository(rollout),
            policies,
            new ProductLegalEntityScopeCandidateFacade(
                new ScopeProvider(ordered),
                new InMemoryLegalEntityRepository(tenantContext.TenantId, entities),
                tenantContext,
                new ActorContext()));
    }

    internal static ProductLegalEntityScopeTestDependencies EnforcedProviderFailure(
        ITenantContext tenantContext,
        int statusCode,
        string failureCode)
    {
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            tenantContext.TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        return new(
            new RolloutRepository(rollout),
            new PolicyRepository(),
            new ProductLegalEntityScopeCandidateFacade(
                new FailureProvider(statusCode, failureCode),
                new InMemoryLegalEntityRepository(tenantContext.TenantId, []),
                tenantContext,
                new ActorContext()));
    }

    private sealed class RolloutRepository(ProductLegalEntityScopeRolloutState rollout)
        : IProductLegalEntityScopeRolloutStateRepository
    {
        public Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductLegalEntityScopeRolloutState?>(rollout);

        public Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(
            Guid creationCommandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductLegalEntityScopeRolloutState?>(null);

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(
            ProductLegalEntityScopeRolloutState state,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(
            ProductLegalEntityScopeRolloutState state,
            int expectedVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PolicyRepository : IProductLegalEntityScopePolicyRepository
    {
        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(
            Guid globalProductId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductLegalEntityScopePolicy?>(null);

        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(
            Guid creationCommandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductLegalEntityScopePolicy?>(null);

        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(
            ProductLegalEntityScopePolicy policy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(
            ProductLegalEntityScopePolicy policy,
            int expectedVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(
            IReadOnlyCollection<Guid> globalProductIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class ForbiddenProvider : ITrustedLegalEntityScopeProvider
    {
        public Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
            Guid expectedTenantId,
            Guid expectedSubjectId,
            string moduleCode,
            string permissionKey,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("PREPARATION_MUST_NOT_RESOLVE_SCOPE_PROVIDER");
    }

    private sealed class ScopeProvider(IReadOnlyList<Guid> legalEntityIds)
        : ITrustedLegalEntityScopeProvider
    {
        public Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
            Guid expectedTenantId,
            Guid expectedSubjectId,
            string moduleCode,
            string permissionKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TrustedLegalEntityScopeProviderResult.Success(
                expectedTenantId,
                expectedSubjectId,
                moduleCode,
                permissionKey,
                DateTimeOffset.UtcNow,
                legalEntityIds));
    }

    private sealed class FailureProvider(int statusCode, string failureCode)
        : ITrustedLegalEntityScopeProvider
    {
        public Task<TrustedLegalEntityScopeProviderResult> ResolveAsync(
            Guid expectedTenantId,
            Guid expectedSubjectId,
            string moduleCode,
            string permissionKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TrustedLegalEntityScopeProviderResult.Fail(statusCode, failureCode));
    }

    private sealed class ActorContext : IProductIdentityActorContext
    {
        public string ActorId { get; } = Guid.NewGuid().ToString("D");
    }
}
