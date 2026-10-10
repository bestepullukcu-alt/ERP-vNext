using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Persistence.Repositories.BusinessReferenceData;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataTenantAssignmentMongoTests : IAsyncLifetime
{
    private string _databaseName = null!;
    private MongoClient _client = null!;
    private IMongoDatabase _database = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _client = new MongoClient(settings);
        _databaseName = await BusinessReferenceDataMongoResidueSweeper.CreateDatabaseAsync(_client, "asn");
        _database = _client.GetDatabase(_databaseName);
        await _database.RunCommandAsync<object>("{ ping: 1 }");
        await PlatformSchemaManifest.ApplyAsync(
            _database,
            new[] { SchemaProfile.BusinessReferenceData });
    }

    public Task DisposeAsync()
    {
        return _client.DropDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Assignment_IndexLifecycleAndExpectedVersion_AreDurable()
    {
        var referenceTenantId = Guid.NewGuid();
        var consumerTenantId = Guid.NewGuid();
        var (repository, _) = CreateRepository(referenceTenantId);
        var assignment = CreateAssignment(referenceTenantId, consumerTenantId, "uom");

        await repository.CreateTenantAssignmentAsync(assignment);
        await Assert.ThrowsAsync<MongoWriteException>(
            () => repository.CreateTenantAssignmentAsync(CreateAssignment(referenceTenantId, consumerTenantId, "uom")));

        Assert.True(await repository.RevokeTenantAssignmentAsync(
            assignment.BusinessReferenceDataTenantAssignmentId,
            consumerTenantId,
            expectedVersion: 1,
            actorId: "steward"));
        Assert.False(await repository.RevokeTenantAssignmentAsync(
            assignment.BusinessReferenceDataTenantAssignmentId,
            consumerTenantId,
            expectedVersion: 1,
            actorId: "stale-writer"));
        Assert.Null(await repository.GetActiveTenantAssignmentAsync(consumerTenantId, "uom"));

        var revoked = await repository.GetTenantAssignmentByIdAsync(
            assignment.BusinessReferenceDataTenantAssignmentId,
            consumerTenantId);
        Assert.NotNull(revoked);
        Assert.Equal(BusinessReferenceDataTenantAssignmentStatus.REVOKED, revoked.AssignmentStatus);
        Assert.Equal(2, revoked.Version);
        Assert.NotNull(revoked.RevokedAt);

        await Assert.ThrowsAsync<MongoWriteException>(
            () => repository.CreateTenantAssignmentAsync(CreateAssignment(referenceTenantId, consumerTenantId, "uom")));
        Assert.True(await repository.ReactivateTenantAssignmentAsync(
            assignment.BusinessReferenceDataTenantAssignmentId,
            consumerTenantId,
            expectedVersion: 2,
            actorId: "steward"));
        Assert.NotNull(await repository.GetActiveTenantAssignmentAsync(consumerTenantId, "uom"));

        Assert.True(await repository.SoftDeleteTenantAssignmentAsync(
            assignment.BusinessReferenceDataTenantAssignmentId,
            consumerTenantId,
            expectedVersion: 3,
            actorId: "steward"));
        Assert.Null(await repository.GetActiveTenantAssignmentAsync(consumerTenantId, "uom"));

        var replacement = CreateAssignment(referenceTenantId, consumerTenantId, "uom");
        await repository.CreateTenantAssignmentAsync(replacement);
        Assert.Equal(
            replacement.BusinessReferenceDataTenantAssignmentId,
            (await repository.GetActiveTenantAssignmentAsync(consumerTenantId, "uom"))?.BusinessReferenceDataTenantAssignmentId);
    }

    [Fact]
    public async Task Assignment_EnforcesReferenceAndConsumerTenantIsolation()
    {
        var referenceTenantA = Guid.NewGuid();
        var referenceTenantB = Guid.NewGuid();
        var consumerTenantA = Guid.NewGuid();
        var consumerTenantB = Guid.NewGuid();
        var (repositoryA, _) = CreateRepository(referenceTenantA);
        var (repositoryB, _) = CreateRepository(referenceTenantB);
        var assignment = CreateAssignment(referenceTenantA, consumerTenantA, "pack-applicability");

        await repositoryA.CreateTenantAssignmentAsync(assignment);

        Assert.Null(await repositoryA.GetActiveTenantAssignmentAsync(consumerTenantB, "pack-applicability"));
        Assert.Null(await repositoryB.GetActiveTenantAssignmentAsync(consumerTenantA, "pack-applicability"));
        Assert.Null(await repositoryB.GetTenantAssignmentByIdAsync(
            assignment.BusinessReferenceDataTenantAssignmentId,
            consumerTenantA));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repositoryA.CreateTenantAssignmentAsync(
                CreateAssignment(referenceTenantB, consumerTenantA, "pack-applicability")));

        var sameConsumerAndSetInOtherReferenceTenant = CreateAssignment(
            referenceTenantB,
            consumerTenantA,
            "pack-applicability");
        await repositoryB.CreateTenantAssignmentAsync(sameConsumerAndSetInOtherReferenceTenant);
        Assert.NotNull(await repositoryB.GetActiveTenantAssignmentAsync(consumerTenantA, "pack-applicability"));
    }

    [Fact]
    public async Task AssignmentIndex_IsPartialUniqueOnReferenceConsumerAndSet()
    {
        var collection = _database.GetCollection<BusinessReferenceDataTenantAssignment>(
            "business_reference_data_tenant_assignments");
        using var cursor = await collection.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();
        var index = Assert.Single(indexes, x => x["name"] == "ux_business_reference_data_tenant_assignments_active");

        Assert.True(index["unique"].AsBoolean);
        Assert.Equal(1, index["key"][nameof(BusinessReferenceDataTenantAssignment.TenantId)].AsInt32);
        Assert.Equal(1, index["key"][nameof(BusinessReferenceDataTenantAssignment.ConsumerTenantId)].AsInt32);
        Assert.Equal(1, index["key"][nameof(BusinessReferenceDataTenantAssignment.SetCode)].AsInt32);
        Assert.False(index["partialFilterExpression"][nameof(BusinessReferenceDataTenantAssignment.IsDeleted)].AsBoolean);
    }

    private (BusinessReferenceDataStewardshipRepository Repository, TenantContext Context) CreateRepository(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        var dbContext = new PlatformDbContext(_client, _database);
        return (new BusinessReferenceDataStewardshipRepository(dbContext, context), context);
    }

    private static BusinessReferenceDataTenantAssignment CreateAssignment(
        Guid referenceTenantId,
        Guid consumerTenantId,
        string setCode)
    {
        return new BusinessReferenceDataTenantAssignment
        {
            TenantId = referenceTenantId,
            ConsumerTenantId = consumerTenantId,
            SetCode = setCode,
            CreatedBy = "test"
        };
    }
}
