using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using Diten.HcmService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.HcmService.Application.Tests;

public sealed class EmployeeDraftSessionRepositoryMongoTests
{
    [Fact]
    public async Task Repository_RoundTripsDraft_ByMongoIdAndTenant()
    {
        var databaseName = $"DitenERP_Mod0251_RepoTests_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://localhost:27017",
                ["Mongo:DatabaseName"] = databaseName
            })
            .Build();

        services.AddPersistence(configuration);
        await using var provider = services.BuildServiceProvider();
        var repository = provider.GetRequiredService<IEmployeeDraftSessionRepository>();
        var mongoClient = provider.GetRequiredService<IMongoClient>();
        var database = provider.GetRequiredService<IMongoDatabase>();

        try
        {
            var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var draftSession = new EmployeeDraftSession
            {
                TenantId = tenantId,
                SourceContext = "repository-test",
                ClientReference = "client-1",
                CreateIdempotencyKeyHash = "create-key"
            };

            await repository.AddAsync(draftSession, CancellationToken.None);
            var rawCollection = database.GetCollection<BsonDocument>("hcm_employee_draft_sessions");
            var rawDocument = await rawCollection.Find(Builders<BsonDocument>.Filter.Empty).FirstOrDefaultAsync();
            Assert.NotNull(rawDocument);
            Assert.True(rawDocument.Contains("_id"), rawDocument.ToJson());
            Assert.True(rawDocument.Contains("TenantId"), rawDocument.ToJson());
            Assert.True(rawDocument["_id"].IsString, rawDocument.ToJson());
            Assert.True(rawDocument["TenantId"].IsString, rawDocument.ToJson());
            Assert.Equal(draftSession.Id.ToString("D"), rawDocument["_id"].AsString);
            Assert.Equal(tenantId.ToString("D"), rawDocument["TenantId"].AsString);

            var loaded = await repository.GetByIdAsync(tenantId, draftSession.Id, CancellationToken.None);
            Assert.NotNull(loaded);
            Assert.Equal(draftSession.Id, loaded.Id);
            Assert.Equal(tenantId, loaded.TenantId);

            var crossTenant = await repository.GetByIdAsync(Guid.NewGuid(), draftSession.Id, CancellationToken.None);
            Assert.Null(crossTenant);

            var idempotencyReplay = await repository.GetByCreateIdempotencyKeyAsync(tenantId, "create-key", CancellationToken.None);
            Assert.NotNull(idempotencyReplay);
            Assert.Equal(draftSession.Id, idempotencyReplay.Id);

            loaded.Touch("save-key");
            var replaced = await repository.ReplaceAsync(loaded, expectedVersion: 1, CancellationToken.None);
            Assert.True(replaced);

            var saved = await repository.GetByIdAsync(tenantId, draftSession.Id, CancellationToken.None);
            Assert.NotNull(saved);
            Assert.Equal(2, saved.Version);
            Assert.Equal("\"2\"", saved.ETag);
        }
        finally
        {
            await mongoClient.DropDatabaseAsync(databaseName);
        }
    }
}
