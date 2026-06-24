using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
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
// serialization config. Skips automatically when no MongoDB is reachable.
public sealed class LegalEntityMongoRoundTripTests
{
    private static string MongoUri =>
        Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017";

    [SkippableFact]
    public async Task Create_then_read_back_finds_the_entity_for_its_tenant()
    {
        Skip.IfNot(IsMongoReachable(), $"MongoDB not reachable at {MongoUri}; round-trip test skipped.");

        var tenantId = Guid.NewGuid();
        var dbName = "DitenERP_RoundTripTest_" + Guid.NewGuid().ToString("N")[..8];

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = MongoUri,
                ["Mongo:DatabaseName"] = dbName
            })
            .Build();

        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);

        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(tenantContext);
        services.AddPersistence(configuration); // production wiring: GUID serializer + client GuidRepresentation

        await using var provider = services.BuildServiceProvider();

        try
        {
            using var scope = provider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ILegalEntityRepository>();

            var entity = new LegalEntity
            {
                Code = "RT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                LegalName = "Round Trip Test Entity"
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
        }
        finally
        {
            // Keep the test server clean: the database name is unique per run.
            var client = provider.GetRequiredService<IMongoClient>();
            await client.DropDatabaseAsync(dbName);
        }
    }

    private static bool IsMongoReachable()
    {
        try
        {
            var settings = MongoClientSettings.FromConnectionString(MongoUri);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            settings.ConnectTimeout = TimeSpan.FromSeconds(2);
            var client = new MongoClient(settings);
            client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
