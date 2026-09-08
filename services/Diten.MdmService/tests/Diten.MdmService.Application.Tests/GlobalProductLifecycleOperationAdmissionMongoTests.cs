using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GlobalProductLifecycleOperationAdmissionMongoTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private IMongoDatabase _database = null!;
    private IMongoCollection<GlobalProduct> _products = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO") ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _database = new MongoClient(settings).GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        _products = _database.GetCollection<GlobalProduct>("mdm_global_products");
    }

    [Fact]
    public async Task Single_product_cas_admits_only_one_lifecycle_operation_and_exact_apply_replays()
    {
        var product = await SeedAsync("Original");
        var repository = new GlobalProductRepository(_database, new Tenant(_tenantId));
        var operationId = Guid.NewGuid();
        var competingId = Guid.NewGuid();
        var binding = new GlobalProductActiveLifecycleOperationBinding(
            GlobalProductLifecycleOperationKind.Correction, operationId, 0);
        var admitted = await repository.AcquireLifecycleOperationAsync(product.Id, 0, binding,
            Audit(product, 0, operationId, ProductAuditOperation.GlobalProductCorrectionRequested));
        var competing = await repository.AcquireLifecycleOperationAsync(product.Id, 0,
            new(GlobalProductLifecycleOperationKind.Correction, competingId, 0),
            Audit(product, 0, competingId, ProductAuditOperation.GlobalProductCorrectionRequested));
        var current = (await repository.GetByIdAsync(product.Id))!;
        var applyAudit = Audit(current, 1, operationId, ProductAuditOperation.GlobalProductCorrectionApplied);
        var applied = await repository.ApplyCorrectionDecisionAsync(product.Id, 1, binding,
            "Corrected", "CORRECTED", applyAudit);
        var replay = await repository.ApplyCorrectionDecisionAsync(product.Id, 1, binding,
            "Corrected", "CORRECTED", applyAudit);

        Assert.True(admitted.Succeeded);
        Assert.False(competing.Succeeded);
        Assert.Equal("GLOBAL_PRODUCT_LIFECYCLE_OPERATION_ACTIVE", competing.ErrorCode);
        Assert.True(applied.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal("Corrected", applied.GlobalProduct!.GlobalProductName);
        Assert.Null(applied.GlobalProduct.ActiveLifecycleOperation);
        Assert.Equal(2, applied.GlobalProduct.Version);
        Assert.Equal(2, applied.GlobalProduct.AuditIntents.Count);
    }

    public async Task DisposeAsync() => await Task.WhenAll(
        _products.DeleteManyAsync(x => x.TenantId == _tenantId),
        _database.GetCollection<GlobalProductCorrectionOperation>(
            GlobalProductCorrectionOperationRepository.CollectionName)
            .DeleteManyAsync(x => x.TenantId == _tenantId));

    private async Task<GlobalProduct> SeedAsync(string name)
    {
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CanonicalCode = $"GP-{Guid.NewGuid():N}",
            GlobalProductName = name, GlobalProductNameNormalized = GlobalProductNameRules.NormalizeDuplicateKey(name),
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _products.InsertOneAsync(product);
        return product;
    }

    private LocalAuditIntent Audit(GlobalProduct product, int version, Guid operationId,
        ProductAuditOperation operation) => GlobalProductCorrectionAuditIntentFactory.Create(product, version,
        operationId, Guid.NewGuid(), operation, "Corrected", DateTimeOffset.UtcNow);

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
