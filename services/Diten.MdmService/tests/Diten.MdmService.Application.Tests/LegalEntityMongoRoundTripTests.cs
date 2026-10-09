using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

// Regression guard for the GUID write↔read-back bug (MDM legal-entity reads returned empty even though
// documents existed, because the query-filter Guid was rendered with a different representation than the
// stored Standard/subtype-4 value). This is a REAL-Mongo round-trip through the production AddPersistence
// wiring — an in-memory repo cannot catch it because the defect lives in the MongoDB driver's GUID
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class LegalEntityMongoRoundTripTests
{
    private const string DatabaseName = "diten_mdm_product_scope_itest";

    private static string MongoUri =>
        Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017";

    [Fact]
    public async Task Create_then_read_back_finds_the_entity_for_its_tenant()
    {
        var tenantId = Guid.NewGuid();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = MongoUri,
                ["Mongo:DatabaseName"] = DatabaseName
            })
            .Build();

        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);

        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(tenantContext);
        services.AddPersistence(configuration); // production wiring: GUID serializer + client GuidRepresentation

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IMongoClient>();
        await client.GetDatabase(DatabaseName).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        try
        {
            using var scope = provider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILegalEntityRepository>();

            var incorporation = new DateTimeOffset(2018, 6, 1, 0, 0, 0, TimeSpan.Zero);
            var dissolution = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
            var entity = new LegalEntity
            {
                Code = "RT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                LegalName = "Round Trip Test Entity",
                // MOD-0220 finish — the 4 additive statutory fields must survive the real Mongo write↔read.
                VatNumber = "EU-VAT-987654321",
                PlaceOfIncorporation = "Istanbul, TR",
                IncorporationDate = incorporation,
                DissolutionDate = dissolution
            };

            await repository.CreateAsync(entity);

            // GetAllAsync + GetByIdAsync both filter on the tenant's Standard-encoded GUID. Before the fix
            // these returned empty/null despite the document existing.
            var all = await repository.GetAllAsync();
            Assert.Contains(all, e => e.Id == entity.Id);

            var byId = await repository.GetByIdAsync(entity.Id);
            Assert.NotNull(byId);
            Assert.Equal(tenantId, byId!.TenantId);
            Assert.Equal(entity.Code, byId.Code);
            // New statutory fields round-trip intact.
            Assert.Equal("EU-VAT-987654321", byId.VatNumber);
            Assert.Equal("Istanbul, TR", byId.PlaceOfIncorporation);
            Assert.Equal(incorporation, byId.IncorporationDate);
            Assert.Equal(dissolution, byId.DissolutionDate);
        }
        finally
        {
            var collection = client.GetDatabase(DatabaseName).GetCollection<LegalEntity>("mdm_legal_entities");
            await collection.DeleteManyAsync(item => item.TenantId == tenantId);
        }
    }

    [Fact]
    public async Task Bounded_batch_is_tenant_soft_delete_and_referenceability_safe()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = MongoUri,
            ["Mongo:DatabaseName"] = DatabaseName
        }).Build();
        var tenantContext = new TenantContext(); tenantContext.SetTenant(tenantId);
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(tenantContext);
        services.AddPersistence(configuration);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IMongoClient>();
        var collection = client.GetDatabase(DatabaseName).GetCollection<LegalEntity>("mdm_legal_entities");
        await client.GetDatabase(DatabaseName).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        try
        {
            var active = Entity(tenantId, LegalEntityOperationalStatus.Active);
            var archived = Entity(tenantId, LegalEntityOperationalStatus.Archived);
            var deleted = Entity(tenantId, LegalEntityOperationalStatus.Active); deleted.IsDeleted = true;
            var crossTenant = Entity(otherTenantId, LegalEntityOperationalStatus.Active);
            await collection.InsertManyAsync([active, archived, deleted, crossTenant]);
            using var scope = provider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILegalEntityRepository>();

            var requested = new[] { active.Id, archived.Id, deleted.Id, crossTenant.Id }
                .OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray();
            var result = await repository.GetReferenceableByIdsAsync(requested);

            Assert.Equal([active.Id], result.Select(item => item.Id));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetReferenceableByIdsAsync([active.Id, active.Id]));
            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetReferenceableByIdsAsync([Guid.Empty]));
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                repository.GetReferenceableByIdsAsync(Array.Empty<Guid>(), cancellation.Token));
        }
        finally
        {
            await collection.DeleteManyAsync(item => item.TenantId == tenantId || item.TenantId == otherTenantId);
        }
    }

    private static LegalEntity Entity(Guid tenantId, LegalEntityOperationalStatus status) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Code = "LE-" + Guid.NewGuid().ToString("N")[..8],
        LegalName = "Batch entity", OperationalStatus = status, CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
