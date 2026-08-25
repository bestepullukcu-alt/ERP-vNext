using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuRegisterMongoTests
{
    [Fact]
    public async Task List_and_detail_are_tenant_safe_soft_delete_aware_and_non_disclosing()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gskuA = await scope.InsertGskuAsync(scope.TenantA, "GS-A");
        var gskuB = await scope.InsertGskuAsync(scope.TenantB, "GS-B");
        var visible = await scope.InsertLskuAsync(scope.TenantA, gskuA.Id, "LS-A", "TR");
        var deleted = await scope.InsertLskuAsync(scope.TenantA, gskuA.Id, "LS-DELETED", "US", isDeleted: true);
        var crossTenant = await scope.InsertLskuAsync(scope.TenantB, gskuB.Id, "LS-B", "DE");
        var lskus = new LskuRepository(scope.Database, new TenantContext(scope.TenantA));
        var gskus = new GskuRepository(scope.Database, new TenantContext(scope.TenantA));

        var list = await new GetLskusHandler(lskus, gskus).Handle(
            new GetLskusQuery { PageNumber = 1, PageSize = 20 },
            CancellationToken.None);

        Assert.True(list.IsSuccessful);
        var item = Assert.Single(list.Data!.Items);
        Assert.Equal(visible.Id, item.Id);
        Assert.Equal("GS-A", item.GskuCanonicalCode);
        Assert.DoesNotContain(list.Data.Items, x => x.Id == deleted.Id || x.Id == crossTenant.Id);

        var detailHandler = new GetLskuByIdHandler(lskus, gskus);
        var visibleDetail = await detailHandler.Handle(new GetLskuByIdQuery(visible.Id), CancellationToken.None);
        Assert.True(visibleDetail.IsSuccessful);

        var hiddenResults = await Task.WhenAll(
            detailHandler.Handle(new GetLskuByIdQuery(deleted.Id), CancellationToken.None),
            detailHandler.Handle(new GetLskuByIdQuery(crossTenant.Id), CancellationToken.None),
            detailHandler.Handle(new GetLskuByIdQuery(Guid.NewGuid()), CancellationToken.None));
        Assert.All(hiddenResults, result =>
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(404, result.StatusCode);
            Assert.Equal(["LSKU_NOT_FOUND"], result.Errors);
        });
    }

    [Fact]
    public async Task Paging_search_and_order_are_deterministic_in_mongo()
    {
        await using var scope = await MongoScope.CreateAsync();
        var gsku = await scope.InsertGskuAsync(scope.TenantA, "GS-A");
        await scope.InsertLskuAsync(scope.TenantA, gsku.Id, "LS-003", "US");
        await scope.InsertLskuAsync(scope.TenantA, gsku.Id, "LS-001", "TR");
        await scope.InsertLskuAsync(scope.TenantA, gsku.Id, "LS-002", "DE");
        var lskus = new LskuRepository(scope.Database, new TenantContext(scope.TenantA));
        var gskus = new GskuRepository(scope.Database, new TenantContext(scope.TenantA));
        var handler = new GetLskusHandler(lskus, gskus);

        var first = await handler.Handle(
            new GetLskusQuery { PageNumber = 1, PageSize = 2 },
            CancellationToken.None);
        var second = await handler.Handle(
            new GetLskusQuery { PageNumber = 2, PageSize = 2 },
            CancellationToken.None);
        var marketSearch = await handler.Handle(
            new GetLskusQuery { PageNumber = 1, PageSize = 20, Search = "tr" },
            CancellationToken.None);

        Assert.Equal(["LS-001", "LS-002"], first.Data!.Items.Select(x => x.CanonicalCode));
        Assert.Equal(["LS-003"], second.Data!.Items.Select(x => x.CanonicalCode));
        Assert.Equal(3, first.Data.TotalCount);
        Assert.Equal(3, second.Data.TotalCount);
        Assert.Equal("TR", Assert.Single(marketSearch.Data!.Items).MarketCode);
        Assert.Equal(1, marketSearch.Data.TotalCount);
    }

    [Fact]
    public async Task Create_options_filter_referenceable_gskus_before_paging_and_batch_join_display_facts()
    {
        await using var scope = await MongoScope.CreateAsync();
        var product = await scope.InsertProductAsync(scope.TenantA, "GP-001", "Product");
        var blockedRevision = await scope.InsertRevisionAsync(scope.TenantA, product.Id, "REV-001");
        var draftRevision = await scope.InsertRevisionAsync(scope.TenantA, product.Id, "REV-002");
        var approvedRevision = await scope.InsertRevisionAsync(scope.TenantA, product.Id, "REV-003");
        await scope.InsertGskuAsync(
            scope.TenantA,
            "GS-001",
            blockedRevision.Id,
            ProductIdentityLifecycleStatus.PendingIdentityApproval);
        var firstReferenceable = await scope.InsertGskuAsync(
            scope.TenantA,
            "GS-002",
            draftRevision.Id,
            ProductIdentityLifecycleStatus.Draft);
        await scope.InsertGskuAsync(
            scope.TenantA,
            "GS-003",
            approvedRevision.Id,
            ProductIdentityLifecycleStatus.IdentityApproved);
        await scope.InsertGskuAsync(
            scope.TenantB,
            "GS-000",
            Guid.NewGuid(),
            ProductIdentityLifecycleStatus.Draft);

        var handler = new GetLskuCreateOptionsHandler(
            new GskuRepository(scope.Database, new TenantContext(scope.TenantA)),
            new ProductDefinitionRevisionRepository(scope.Database, new TenantContext(scope.TenantA)),
            new GlobalProductRepository(scope.Database, new TenantContext(scope.TenantA)),
            new MarketResolver());
        var response = await handler.Handle(
            new GetLskuCreateOptionsQuery { PageNumber = 1, PageSize = 1 },
            CancellationToken.None);

        Assert.True(response.IsSuccessful, string.Join(',', response.Errors));
        var option = Assert.Single(response.Data!.Gskus);
        Assert.Equal(firstReferenceable.Id, option.Id);
        Assert.Equal("GS-002", option.CanonicalCode);
        Assert.Equal("GP-001", option.GlobalProductCanonicalCode);
        Assert.Equal("Product", option.GlobalProductName);
        Assert.Equal("REV-002", option.RevisionIdentifier);
        Assert.Equal(["TR", "US"], response.Data.Markets.Select(x => x.Code));
    }

    private sealed class MarketResolver : IVerifiedMarketReferenceResolver
    {
        public Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(
            string code,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(VerifiedMarketReferenceResolveResult.Fail(503, "NOT_USED"));

        public Task<VerifiedMarketEnumerationResult> EnumerateActiveAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(VerifiedMarketEnumerationResult.Success(
            [
                new("US", "United States", 20),
                new("TR", "Türkiye", 10)
            ]));
    }

    private sealed class TenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid value) => TenantId = value;
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
            var connectionString = Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
                ?? Environment.GetEnvironmentVariable("MONGO_TEST_URI")
                ?? "mongodb://localhost:27017";
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            var client = new MongoClient(settings);
            var databaseName = "diten_lsku_register_" + Guid.NewGuid().ToString("N");
            var database = client.GetDatabase(databaseName);
            await database.RunCommandAsync<object>("{ ping: 1 }");
            return new(client, database, databaseName);
        }

        public async Task<GlobalProduct> InsertProductAsync(
            Guid tenantId,
            string canonicalCode,
            string name)
        {
            var product = new GlobalProduct
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CanonicalCode = canonicalCode,
                GlobalProductName = name,
                GlobalProductNameNormalized = name.ToUpperInvariant(),
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
                IsDeleted = false
            };
            await Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(product);
            return product;
        }

        public async Task<ProductDefinitionRevision> InsertRevisionAsync(
            Guid tenantId,
            Guid productId,
            string identifier)
        {
            var revision = new ProductDefinitionRevision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GlobalProductId = productId,
                RevisionIdentifier = identifier,
                CreationCommandId = "REV:" + Guid.NewGuid().ToString("N"),
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
                IsDeleted = false
            };
            await Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
                .InsertOneAsync(revision);
            return revision;
        }

        public Task<Gsku> InsertGskuAsync(
            Guid tenantId,
            string canonicalCode,
            Guid? revisionId = null,
            ProductIdentityLifecycleStatus lifecycle = ProductIdentityLifecycleStatus.Draft) =>
            InsertGskuCoreAsync(tenantId, canonicalCode, revisionId ?? Guid.NewGuid(), lifecycle);

        private async Task<Gsku> InsertGskuCoreAsync(
            Guid tenantId,
            string canonicalCode,
            Guid revisionId,
            ProductIdentityLifecycleStatus lifecycle)
        {
            var gsku = new Gsku
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductDefinitionRevisionId = revisionId,
                CanonicalCode = canonicalCode,
                CodeReservationId = Guid.NewGuid(),
                CreationCommandId = "GSKU:" + Guid.NewGuid().ToString("N"),
                PackQuantity = 1m,
                PackUomCode = "C62",
                LifecycleStatus = lifecycle,
                IsDeleted = false
            };
            await Database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
            return gsku;
        }

        public async Task<Lsku> InsertLskuAsync(
            Guid tenantId,
            Guid gskuId,
            string canonicalCode,
            string marketCode,
            bool isDeleted = false)
        {
            var lsku = new Lsku
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                GskuId = gskuId,
                CanonicalCode = canonicalCode,
                CodeReservationId = Guid.NewGuid(),
                CreationCommandId = "LSKU:" + Guid.NewGuid().ToString("N"),
                MarketCode = marketCode,
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await Database.GetCollection<Lsku>("mdm_lskus").InsertOneAsync(lsku);
            return lsku;
        }

        public async ValueTask DisposeAsync() =>
            await _client.DropDatabaseAsync(_databaseName);
    }
}
