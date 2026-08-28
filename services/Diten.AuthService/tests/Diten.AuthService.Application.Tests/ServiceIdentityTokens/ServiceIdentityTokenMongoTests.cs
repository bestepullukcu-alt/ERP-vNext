using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceIdentityTokenMongoTests
{
    private const string DatabaseName = "diten_auth_service_identity_itest";

    [Fact]
    public async Task Fixed_database_enforces_unique_lifecycle_and_exact_tenant_grant_isolation()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        var database = client.GetDatabase(DatabaseName);
        await MongoDbIndexConfigurations.EnsureServiceIdentityIndexesAsync(database);
        var identities = database.GetCollection<ServiceClientIdentity>("serviceClientIdentities");
        var grants = database.GetCollection<ServiceClientTenantGrant>("serviceClientTenantGrants");
        var marker = Guid.NewGuid().ToString("N");
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var identity = new ServiceClientIdentity
        {
            ClientCode = "mdm-" + marker,
            ServiceName = "Diten.MDM",
            ActiveCredentialHash = "test-only",
            ActiveCredentialVersion = "v1"
        };

        try
        {
            await identities.InsertOneAsync(identity);
            await Assert.ThrowsAsync<MongoWriteException>(() => identities.InsertOneAsync(new ServiceClientIdentity
            {
                ClientCode = identity.ClientCode,
                ServiceName = "Diten.MDM",
                ActiveCredentialHash = "test-only",
                ActiveCredentialVersion = "v1"
            }));

            await grants.InsertManyAsync([
                new ServiceClientTenantGrant { TenantId = tenantA, ServiceClientIdentityId = identity.Id, Audience = "TRUSTED_AUDIT_SOURCE_INGEST", IsEnabled = true },
                new ServiceClientTenantGrant { TenantId = tenantB, ServiceClientIdentityId = identity.Id, Audience = "TRUSTED_AUDIT_SOURCE_INGEST", IsEnabled = false }
            ]);

            var identityRepository = new ServiceClientIdentityRepository(database);
            var grantRepository = new ServiceClientTenantGrantRepository(database);
            Assert.Equal(identity.Id, (await identityRepository.GetByClientCodeAsync(identity.ClientCode, CancellationToken.None))?.Id);
            Assert.True(await grantRepository.HasEnabledGrantAsync(tenantA, identity.Id, "TRUSTED_AUDIT_SOURCE_INGEST", CancellationToken.None));
            Assert.False(await grantRepository.HasEnabledGrantAsync(tenantB, identity.Id, "TRUSTED_AUDIT_SOURCE_INGEST", CancellationToken.None));
            Assert.False(await grantRepository.HasEnabledGrantAsync(tenantA, identity.Id, "OTHER", CancellationToken.None));

            await Assert.ThrowsAsync<MongoBulkWriteException<ServiceClientTenantGrant>>(() => grants.InsertManyAsync([
                new ServiceClientTenantGrant
                {
                    TenantId = tenantA,
                    ServiceClientIdentityId = identity.Id,
                    Audience = "TRUSTED_AUDIT_SOURCE_INGEST",
                    IsEnabled = true
                }
            ]));

            await grants.UpdateOneAsync(
                x => x.TenantId == tenantA && x.ServiceClientIdentityId == identity.Id,
                Builders<ServiceClientTenantGrant>.Update.Set(x => x.IsDeleted, true));
            await grants.InsertOneAsync(new ServiceClientTenantGrant
            {
                TenantId = tenantA,
                ServiceClientIdentityId = identity.Id,
                Audience = "TRUSTED_AUDIT_SOURCE_INGEST",
                IsEnabled = true
            });
            Assert.True(await grantRepository.HasEnabledGrantAsync(
                tenantA, identity.Id, "TRUSTED_AUDIT_SOURCE_INGEST", CancellationToken.None));

            var grantIndexes = await (await grants.Indexes.ListAsync()).ToListAsync();
            var uniqueGrantIndex = Assert.Single(grantIndexes, x =>
                x["name"] == "ux_service_client_grant_tenant_client_audience_active");
            Assert.True(uniqueGrantIndex["unique"].AsBoolean);
            Assert.Equal(
                new BsonDocument { { "TenantId", 1 }, { "ServiceClientIdentityId", 1 }, { "Audience", 1 } },
                uniqueGrantIndex["key"].AsBsonDocument);
            Assert.Equal(
                new BsonDocument("IsDeleted", false),
                uniqueGrantIndex["partialFilterExpression"].AsBsonDocument);

            var malformedCode = "malformed-" + marker;
            var rawIdentities = database.GetCollection<BsonDocument>("serviceClientIdentities");
            await rawIdentities.InsertOneAsync(new BsonDocument
            {
                { "_id", new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard) },
                { "ClientCode", malformedCode },
                { "ServiceName", 42 },
                { "ActiveCredentialHash", "test-only" },
                { "ActiveCredentialVersion", "v1" },
                { "IsRevoked", false },
                { "IsDeleted", false },
                { "CreatedAt", DateTime.UtcNow },
                { "CreatedBy", "test" }
            });
            await Assert.ThrowsAsync<ServiceIdentityPersistenceUnavailableException>(() =>
                identityRepository.GetByClientCodeAsync(malformedCode, CancellationToken.None));
            await rawIdentities.DeleteManyAsync(new BsonDocument("ClientCode", malformedCode));

            await identities.UpdateOneAsync(x => x.Id == identity.Id, Builders<ServiceClientIdentity>.Update.Set(x => x.IsDeleted, true));
            var replacement = new ServiceClientIdentity
            {
                ClientCode = identity.ClientCode,
                ServiceName = "Diten.MDM",
                ActiveCredentialHash = "replacement",
                ActiveCredentialVersion = "v2"
            };
            await identities.InsertOneAsync(replacement);
            Assert.Equal(replacement.Id, (await identityRepository.GetByClientCodeAsync(identity.ClientCode, CancellationToken.None))?.Id);
        }
        finally
        {
            await identities.DeleteManyAsync(x => x.ClientCode == identity.ClientCode);
            await grants.DeleteManyAsync(x => x.ServiceClientIdentityId == identity.Id);
        }
    }
}
