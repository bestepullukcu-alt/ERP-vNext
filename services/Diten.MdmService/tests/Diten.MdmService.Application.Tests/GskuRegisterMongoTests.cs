using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
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
        var handler = new GetGskusHandler(
            new GskuRepository(scope.Database, scope.Context(scope.TenantA)),
            new ProductDefinitionRevisionRepository(scope.Database, scope.Context(scope.TenantA)),
            new GlobalProductRepository(scope.Database, scope.Context(scope.TenantA)));

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
        var handler = new GetGskuByIdHandler(
            new GskuRepository(scope.Database, scope.Context(scope.TenantA)),
            new ProductDefinitionRevisionRepository(scope.Database, scope.Context(scope.TenantA)),
            new GlobalProductRepository(scope.Database, scope.Context(scope.TenantA)));

        var cross = await handler.Handle(new GetGskuByIdQuery(crossTenant.Gsku.Id), default);
        var tombstone = await handler.Handle(new GetGskuByIdQuery(deleted.Gsku.Id), default);

        Assert.Equal(404, cross.StatusCode);
        Assert.Equal(404, tombstone.StatusCode);
        Assert.Equal(cross.Errors, tombstone.Errors);
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
