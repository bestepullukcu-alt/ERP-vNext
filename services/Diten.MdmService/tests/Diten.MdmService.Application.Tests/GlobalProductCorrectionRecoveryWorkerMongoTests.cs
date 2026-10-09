using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GlobalProductCorrectionRecoveryWorkerMongoTests : IAsyncLifetime
{
    private static readonly Guid[] Tenants =
    [
        Guid.Parse("71000000-0000-0000-0000-000000000001"),
        Guid.Parse("72000000-0000-0000-0000-000000000001"),
        Guid.Parse("73000000-0000-0000-0000-000000000001")
    ];
    private IMongoDatabase _database = null!;
    private IMongoCollection<GlobalProductCorrectionOperation> _operations = null!;

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
        _operations = _database.GetCollection<GlobalProductCorrectionOperation>(
            GlobalProductCorrectionOperationRepository.CollectionName);
        await _operations.DeleteManyAsync(x => Tenants.Contains(x.TenantId));
    }

    [Fact]
    public async Task Tenant_discovery_is_bounded_sorted_and_cursor_stable()
    {
        foreach (var tenantId in Tenants)
            Assert.True((await new GlobalProductCorrectionOperationRepository(_database, new Tenant(tenantId))
                .ReserveAsync(Candidate(tenantId))).Succeeded);
        var discovery = new GlobalProductCorrectionOperationRepository(_database, new UnresolvedTenant());

        var first = await discovery.DiscoverTenantPartitionsAsync(null, 2);
        var second = await discovery.DiscoverTenantPartitionsAsync(first.NextAfterTenantId, 2);

        Assert.Equal(Tenants.Take(2), first.TenantIds);
        Assert.Equal(Tenants.Skip(2), second.TenantIds);
        Assert.Null(second.NextAfterTenantId);
    }

    public Task DisposeAsync() => _operations.DeleteManyAsync(x => Tenants.Contains(x.TenantId));

    private static GlobalProductCorrectionOperation Candidate(Guid tenantId)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.UtcTicks;
        return new()
        {
            Id = id, TenantId = tenantId, OperationId = id, GlobalProductId = Guid.NewGuid(),
            BaseProductVersion = 1, MakerSubjectId = Guid.NewGuid(), ProposedGlobalProductName = "Corrected",
            ProposedGlobalProductNameNormalized = "CORRECTED", WorkflowTemplateId = Guid.NewGuid(),
            CandidatePrincipalIds = [Guid.NewGuid()], ReasonCode = "CORRECTION",
            ObjectType = "GlobalProductCorrection", ObjectId = id.ToString("D"), ObjectRef = "GP-TEST",
            StartIdempotencyKey = $"global-product-correction:{tenantId:D}:{id:D}",
            OperationFingerprint = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant(),
            CreatedAtUtcTicksV1 = now, UpdatedAtUtcTicksV1 = now
        };
    }

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class UnresolvedTenant : ITenantContext
    {
        public Guid TenantId => throw new InvalidOperationException("TenantId has not been resolved.");
        public bool IsResolved => false;
        public void SetTenant(Guid tenantId) => throw new NotSupportedException();
    }
}
