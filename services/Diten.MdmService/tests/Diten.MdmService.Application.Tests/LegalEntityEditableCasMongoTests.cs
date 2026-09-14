using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.LegalEntity;
using Diten.MdmService.Application.Features.LegalEntity.Commands;
using Diten.MdmService.Application.Features.LegalEntity.Handlers.CommandHandlers;
using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.MdmService.Application.Tests;

[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class LegalEntityEditableCasMongoTests(
    AuditIntentTemporalMongoFixture mongo,
    ITestOutputHelper output) : IClassFixture<AuditIntentTemporalMongoFixture>
{
    [Fact]
    public async Task Update_NormalEditUpdatesExactEditableSetAndPreservesAllProtectedStateAndUnknownBson()
    {
        var tenantId = Guid.NewGuid();
        await using var provider = CreateProvider(tenantId);
        var repository = provider.GetRequiredService<ILegalEntityRepository>();
        var collection = Collection(provider);
        var rawCollection = RawCollection(provider);
        var reviewDueUtc = new DateTimeOffset(2030, 4, 5, 0, 0, 0, TimeSpan.Zero);
        var entity = Entity(tenantId, $"LE-NORMAL-{Guid.NewGuid():N}");
        entity.ApprovalStatus = LegalEntityApprovalStatus.Approved;
        entity.ReviewDueUtc = reviewDueUtc;
        entity.SourceSystem = "protected-source";
        entity.LegacyCode = "protected-legacy";
        entity.EvidenceStatus = LegalEntityEvidenceStatus.Verified;
        await repository.CreateAsync(entity);

        try
        {
            var proposed = Assert.IsType<LegalEntity>(await repository.GetByIdAsync(entity.Id));
            var expectedVersion = proposed.Version;
            var identity = (entity.Id, entity.TenantId, entity.CreatedAt, entity.IsDeleted, entity.DeletedAt);
            var incorporated = new DateTimeOffset(2020, 1, 2, 0, 0, 0, TimeSpan.Zero);
            var dissolved = new DateTimeOffset(2040, 3, 4, 0, 0, 0, TimeSpan.Zero);
            var request = LegalEntityTestData.ValidRequest(
                    code: $"LE-EDIT-{Guid.NewGuid():N}",
                    legalName: "Edited legal name",
                    organizationRoleCode: "LEGALENTITY",
                    registeredAddressJson: "{\"line1\":\"Edited\",\"city\":\"Izmir\",\"country\":\"TR\"}",
                    vatNumber: "VAT-EDIT",
                    placeOfIncorporation: "Izmir, TR",
                    incorporationDate: incorporated,
                    dissolutionDate: dissolved)
                with
                {
                    DisplayName = "Edited display",
                    LegalFormCode = "LLC",
                    RegistrationNumber = "REG-EDIT",
                    TaxId = "TAX-EDIT",
                    CountryCode = "DE",
                    StatutoryStatus = "Dissolved",
                    ParentLegalEntityId = null,
                    OwnershipPercent = 62.5m,
                    ControlTypeCode = "CONTROL-EDIT",
                    FiscalYearVariant = "K4",
                    AccountingStandardCode = "LOCAL-GAAP",
                    TaxRegimeCode = "SPECIAL",
                    BaseCurrencyCode = "EUR",
                    CorrespondenceAddressJson = "{\"line1\":\"Mail\",\"city\":\"Berlin\",\"country\":\"DE\"}",
                    OfficialEmail = "edited@example.test",
                    OfficialPhone = "+49 30 123",
                    Website = "https://edited.example.test",
                    ApprovalStatus = "Rejected",
                    ReviewDueUtc = reviewDueUtc.AddYears(10),
                    SourceSystem = "attacker-source",
                    LegacyCode = "attacker-legacy",
                    EvidenceStatus = "NotStarted",
                    ExpectedVersion = entity.Version
                };
            var beforeUpdatedAt = entity.UpdatedAt;
            LegalEntityMappings.ApplyEditableFields(proposed, request);
            await rawCollection.UpdateOneAsync(
                RawTenantEntityFilter(tenantId, entity.Id),
                new BsonDocument("$set", new BsonDocument("A1aUnknown", new BsonDocument("marker", "preserve"))));

            var updated = await repository.UpdateEditableFieldsAsync(
                proposed, expectedVersion, CancellationToken.None);

            Assert.True(updated);
            var raw = await rawCollection.Find(RawTenantEntityFilter(tenantId, entity.Id)).SingleAsync();
            Assert.Equal("preserve", raw["A1aUnknown"]["marker"].AsString);
            var persisted = await collection
                .Find(item => item.Id == entity.Id && item.TenantId == tenantId)
                .Project<LegalEntity>(Builders<LegalEntity>.Projection.Exclude("A1aUnknown"))
                .SingleAsync();
            Assert.Equal(request.Code.Trim(), persisted.Code);
            Assert.Equal(request.LegalName.Trim(), persisted.LegalName);
            Assert.Equal(request.DisplayName, persisted.DisplayName);
            Assert.Equal(request.LegalFormCode, persisted.LegalFormCode);
            Assert.Equal(request.OrganizationRoleCode, persisted.OrganizationRoleCode);
            Assert.Equal(request.RegistrationNumber, persisted.RegistrationNumber);
            Assert.Equal(request.TaxId, persisted.TaxId);
            Assert.Equal(request.VatNumber, persisted.VatNumber);
            Assert.Equal(request.PlaceOfIncorporation, persisted.PlaceOfIncorporation);
            Assert.Equal(incorporated, persisted.IncorporationDate);
            Assert.Equal(dissolved, persisted.DissolutionDate);
            Assert.Equal(request.CountryCode, persisted.CountryCode);
            Assert.Equal(LegalEntityStatutoryStatus.Dissolved, persisted.StatutoryStatus);
            Assert.Null(persisted.ParentLegalEntityId);
            Assert.Equal(request.OwnershipPercent, persisted.OwnershipPercent);
            Assert.Equal(request.ControlTypeCode, persisted.ControlTypeCode);
            Assert.Equal(request.FiscalYearVariant, persisted.FiscalYearVariant);
            Assert.Equal(request.AccountingStandardCode, persisted.AccountingStandardCode);
            Assert.Equal(request.TaxRegimeCode, persisted.TaxRegimeCode);
            Assert.Equal(request.BaseCurrencyCode, persisted.BaseCurrencyCode);
            Assert.Equal(request.RegisteredAddressJson, persisted.RegisteredAddressJson);
            Assert.Equal(request.CorrespondenceAddressJson, persisted.CorrespondenceAddressJson);
            Assert.Equal(request.OfficialEmail, persisted.OfficialEmail);
            Assert.Equal(request.OfficialPhone, persisted.OfficialPhone);
            Assert.Equal(request.Website, persisted.Website);
            Assert.Equal(100, persisted.CompletenessScore);
            Assert.True(persisted.UpdatedAt > beforeUpdatedAt);
            Assert.Equal(1, persisted.Version);

            Assert.Equal(LegalEntityOperationalStatus.Active, persisted.OperationalStatus);
            Assert.Equal(LegalEntityApprovalStatus.Approved, persisted.ApprovalStatus);
            Assert.Equal(reviewDueUtc, persisted.ReviewDueUtc);
            Assert.Equal("protected-source", persisted.SourceSystem);
            Assert.Equal("protected-legacy", persisted.LegacyCode);
            Assert.Equal(LegalEntityEvidenceStatus.Verified, persisted.EvidenceStatus);
            Assert.Equal(identity.Id, persisted.Id);
            Assert.Equal(identity.TenantId, persisted.TenantId);
            Assert.Equal(identity.CreatedAt, persisted.CreatedAt);
            Assert.Equal(identity.IsDeleted, persisted.IsDeleted);
            Assert.Equal(identity.DeletedAt, persisted.DeletedAt);
        }
        finally
        {
            await collection.DeleteManyAsync(item => item.TenantId == tenantId && item.Id == entity.Id);
        }
    }

    [Theory]
    [InlineData(LegalEntityOperationalStatus.Suspended)]
    [InlineData(LegalEntityOperationalStatus.Archived)]
    public async Task UpdateEditableFieldsAsync_StaleCapturedEditCannotResurrectLifecycleInvalidator(
        LegalEntityOperationalStatus invalidatedStatus)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var provider = CreateProvider(tenantId);
        var repository = provider.GetRequiredService<ILegalEntityRepository>();
        var collection = provider.GetRequiredService<IMongoClient>()
            .GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName)
            .GetCollection<LegalEntity>("mdm_legal_entities");
        var entity = Entity(tenantId, "LE-CAS-LIFE");
        await repository.CreateAsync(entity);

        try
        {
            var staleEdit = Assert.IsType<LegalEntity>(await repository.GetByIdAsync(entity.Id));
            var expectedVersion = staleEdit.Version;
            LegalEntityMappings.Apply(
                staleEdit,
                LegalEntityTestData.ValidRequest(code: entity.Code, legalName: "Stale captured edit"));

            var invalidator = Builders<LegalEntity>.Update
                .Set(item => item.OperationalStatus, invalidatedStatus)
                .Set(item => item.UpdatedAt, DateTimeOffset.UtcNow)
                .Inc(item => item.Version, 1);
            var invalidatorResult = await collection.UpdateOneAsync(
                item => item.TenantId == tenantId && item.Id == entity.Id && !item.IsDeleted,
                invalidator);
            Assert.Equal(1, invalidatorResult.ModifiedCount);
            var beforeStaleAttempt = await RawByIdAsync(provider, tenantId, entity.Id);

            // Act
            var updated = await InvokeEditableCasAsync(repository, staleEdit, expectedVersion);

            // Assert
            var persisted = await collection.Find(item => item.TenantId == tenantId && item.Id == entity.Id).SingleAsync();
            Assert.False(updated);
            Assert.Equal(invalidatedStatus, persisted.OperationalStatus);
            Assert.Equal(expectedVersion + 1, persisted.Version);
            Assert.NotEqual("Stale captured edit", persisted.LegalName);
            Assert.Equal(beforeStaleAttempt, await RawByIdAsync(provider, tenantId, entity.Id));
        }
        finally
        {
            await collection.DeleteManyAsync(item => item.TenantId == tenantId && item.Id == entity.Id);
        }
    }

    [Fact]
    public async Task UpdateEditableFieldsAsync_StaleCapturedEditCannotResurrectDeleteAndMakesZeroMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var provider = CreateProvider(tenantId);
        var repository = provider.GetRequiredService<ILegalEntityRepository>();
        var collection = Collection(provider);
        var entity = Entity(tenantId, $"LE-CAS-DELETE-{Guid.NewGuid():N}");
        await repository.CreateAsync(entity);

        try
        {
            var staleEdit = Assert.IsType<LegalEntity>(await repository.GetByIdAsync(entity.Id));
            LegalEntityMappings.ApplyEditableFields(
                staleEdit,
                LegalEntityTestData.ValidRequest(code: entity.Code, legalName: "Stale after delete"));
            Assert.True(await repository.DeleteAsync(entity.Id));
            var afterDelete = await RawByIdAsync(provider, tenantId, entity.Id);

            Assert.False(await repository.UpdateEditableFieldsAsync(staleEdit, 0, CancellationToken.None));
            Assert.Equal(afterDelete, await RawByIdAsync(provider, tenantId, entity.Id));
            Assert.True((await collection.Find(
                item => item.TenantId == tenantId && item.Id == entity.Id).SingleAsync()).IsDeleted);
        }
        finally
        {
            await collection.DeleteManyAsync(item => item.TenantId == tenantId && item.Id == entity.Id);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("deleted")]
    public async Task Update_MissingForeignAndDeletedAreSameNonDisclosing404WithZeroMutation(string visibility)
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var provider = CreateProvider(tenantId);
        await using var otherProvider = CreateProvider(otherTenantId);
        var repository = provider.GetRequiredService<ILegalEntityRepository>();
        var collection = Collection(provider);
        var target = Entity(visibility == "foreign" ? otherTenantId : tenantId, $"LE-{visibility}-{Guid.NewGuid():N}");

        try
        {
            if (visibility == "foreign")
            {
                await otherProvider.GetRequiredService<ILegalEntityRepository>().CreateAsync(target);
            }
            else if (visibility == "deleted")
            {
                await repository.CreateAsync(target);
                Assert.True(await repository.DeleteAsync(target.Id));
            }

            var before = visibility == "missing"
                ? null
                : await RawByIdAsync(provider, target.TenantId, target.Id);
            var request = LegalEntityTestData.ValidRequest(code: target.Code, legalName: "Must not write")
                with { ExpectedVersion = 0 };
            var response = await new UpdateLegalEntityHandler(repository).Handle(
                new UpdateLegalEntityCommand(target.Id, request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(404, response.StatusCode);
            Assert.Equal(["Legal Entity not found."], response.Errors);
            if (before is not null)
            {
                Assert.Equal(before, await RawByIdAsync(provider, target.TenantId, target.Id));
            }
        }
        finally
        {
            await collection.DeleteManyAsync(
                item => item.TenantId == target.TenantId && item.Id == target.Id);
        }
    }

    [Fact]
    public async Task UpdateEditableFieldsAsync_TwoConcurrentEditsFromOneVersionYieldOneSuccessAndOneConflict()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await using var provider = CreateProvider(tenantId);
        var repository = provider.GetRequiredService<ILegalEntityRepository>();
        var collection = provider.GetRequiredService<IMongoClient>()
            .GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName)
            .GetCollection<LegalEntity>("mdm_legal_entities");
        var entity = Entity(tenantId, "LE-CAS-RACE");
        await repository.CreateAsync(entity);

        try
        {
            var first = Assert.IsType<LegalEntity>(await repository.GetByIdAsync(entity.Id));
            var second = Assert.IsType<LegalEntity>(await repository.GetByIdAsync(entity.Id));
            var expectedVersion = first.Version;
            LegalEntityMappings.Apply(first, LegalEntityTestData.ValidRequest(code: entity.Code, legalName: "First edit"));
            LegalEntityMappings.Apply(second, LegalEntityTestData.ValidRequest(code: entity.Code, legalName: "Second edit"));

            // Act
            var results = await Task.WhenAll(
                InvokeEditableCasAsync(repository, first, expectedVersion),
                InvokeEditableCasAsync(repository, second, expectedVersion));

            // Assert
            var persisted = await collection.Find(item => item.TenantId == tenantId && item.Id == entity.Id).SingleAsync();
            Assert.Equal(1, results.Count(result => result));
            Assert.Equal(1, results.Count(result => !result));
            Assert.Equal(expectedVersion + 1, persisted.Version);
            Assert.Contains(persisted.LegalName, new[] { "First edit", "Second edit" });
        }
        finally
        {
            await collection.DeleteManyAsync(item => item.TenantId == tenantId && item.Id == entity.Id);
        }
    }

    private ServiceProvider CreateProvider(Guid tenantId)
    {
        output.WriteLine(
            $"Mongo topology: test-owned dynamic loopback replica set; database={ProductLegalEntityScopeMongoCollection.DatabaseName}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = mongo.ReplicaConnectionString,
                ["Mongo:DatabaseName"] = ProductLegalEntityScopeMongoCollection.DatabaseName
            })
            .Build();
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(tenantContext);
        services.AddPersistence(configuration);
        return services.BuildServiceProvider();
    }

    private static IMongoCollection<LegalEntity> Collection(ServiceProvider provider)
        => provider.GetRequiredService<IMongoClient>()
            .GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName)
            .GetCollection<LegalEntity>("mdm_legal_entities");

    private static IMongoCollection<BsonDocument> RawCollection(ServiceProvider provider)
        => provider.GetRequiredService<IMongoClient>()
            .GetDatabase(ProductLegalEntityScopeMongoCollection.DatabaseName)
            .GetCollection<BsonDocument>("mdm_legal_entities");

    private static Task<BsonDocument> RawByIdAsync(ServiceProvider provider, Guid tenantId, Guid id)
        => RawCollection(provider)
            .Find(RawTenantEntityFilter(tenantId, id))
            .SingleAsync();

    private static FilterDefinition<BsonDocument> RawTenantEntityFilter(Guid tenantId, Guid id)
        => Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq(
                "TenantId",
                new BsonBinaryData(tenantId, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Filter.Eq(
                "_id",
                new BsonBinaryData(id, GuidRepresentation.Standard)));

    private static async Task<bool> InvokeEditableCasAsync(
        ILegalEntityRepository repository,
        LegalEntity proposed,
        int expectedVersion)
    {
        var method = repository.GetType().GetMethod(
            "UpdateEditableFieldsAsync",
            [typeof(LegalEntity), typeof(int), typeof(CancellationToken)]);
        if (method is null)
        {
            // RED compatibility: current production has only the stale whole-document path.
            return await repository.UpdateAsync(proposed, CancellationToken.None);
        }

        var task = Assert.IsAssignableFrom<Task<bool>>(
            method.Invoke(repository, [proposed, expectedVersion, CancellationToken.None]));
        return await task;
    }

    private static LegalEntity Entity(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Code = code,
        LegalName = "Legal Entity before edit",
        DisplayName = "Before",
        LegalFormCode = "CORPORATION",
        OrganizationRoleCode = "LEGALENTITY",
        CountryCode = "TR",
        BaseCurrencyCode = "TRY",
        RegisteredAddressJson = LegalEntityTestData.ValidAddressJson,
        OperationalStatus = LegalEntityOperationalStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        Version = 0
    };
}
