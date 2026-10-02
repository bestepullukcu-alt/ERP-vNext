using Diten.AuthService.Application.Features.ServiceIdentityTokens.Registry;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.ServiceClientRegistry.Tests.Support;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// MOD-0033-FU02 §F.1 against a real (test-owned) mongod: the production wiring, repositories, indexes and audit
/// writer. "One startup" below is <see cref="RegistryStartup.RunAsync"/> — what Program.cs registers, nothing less.
/// </summary>
[Collection(OwnedMongoCollection.Name)]
public sealed class ServiceClientRegistryMongoTests(OwnedMongo mongo)
{
    private IMongoCollection<ServiceClientIdentity> Identities =>
        mongo.Database.GetCollection<ServiceClientIdentity>("serviceClientIdentities");

    private IMongoCollection<ServiceClientTenantGrant> Grants =>
        mongo.Database.GetCollection<ServiceClientTenantGrant>("serviceClientTenantGrants");

    [Fact]
    public async Task Startup_WhenClientIsDeclared_CreatesIdentityGrantsAndAuditRows()
    {
        var code = RegistryStartup.NewClientCode();
        var hash = RegistryStartup.NewHash();
        var tenants = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await RegistryStartup.RunAsync(mongo.Database, RegistryStartup.Configuration(new RegistryStartup.Declared(code, hash, tenants)));

        var identity = await Identities.Find(x => x.ClientCode == code).SingleAsync();
        Assert.Equal(ServiceClientIdentityIds.FromClientCode(code), identity.Id);
        Assert.Equal(("Diten.MDM", "TRUSTED_WORKFLOW_CONSUMER", hash, "v1"),
            (identity.ServiceName, identity.AllowedAudience, identity.ActiveCredentialHash, identity.ActiveCredentialVersion));
        Assert.False(identity.IsRevoked);
        Assert.False(identity.IsDeleted);
        Assert.Equal(ServiceClientRegistryReconciler.SystemActor, identity.CreatedBy);
        var grants = await Grants.Find(x => x.ServiceClientIdentityId == identity.Id).ToListAsync();
        Assert.Equal(tenants.Order(), grants.Select(grant => grant.TenantId).Order());
        Assert.All(grants, grant => Assert.True(grant.IsEnabled));
        // The production read path the token endpoint uses sees them.
        Assert.True(await new ServiceClientTenantGrantRepository(mongo.Database)
            .HasEnabledGrantAsync(tenants[0], identity.Id, "TRUSTED_WORKFLOW_CONSUMER", CancellationToken.None));

        var snapshot = await RegistryStartup.SnapshotAsync(mongo.Database, [code], tenants);
        var audits = snapshot.Where(line => line.StartsWith("audit ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, audits.Length); // one identity + two grants
        Assert.Single(audits, line => line.Contains(ServiceClientRegistryReconciler.IdentityRegisteredEvent, StringComparison.Ordinal));
        Assert.Equal(2, audits.Count(line => line.Contains(ServiceClientRegistryReconciler.TenantGrantedEvent, StringComparison.Ordinal)));
        Assert.All(audits, line => Assert.DoesNotContain(hash, line, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Startup_WhenRunASecondTime_WritesNothing()
    {
        var code = RegistryStartup.NewClientCode();
        var tenants = new[] { Guid.NewGuid() };
        var configuration = RegistryStartup.Configuration(new RegistryStartup.Declared(code, RegistryStartup.NewHash(), tenants));
        await RegistryStartup.RunAsync(mongo.Database, configuration);
        var before = await RegistryStartup.SnapshotAsync(mongo.Database, [code], tenants);

        var log = await RegistryStartup.RunAsync(mongo.Database, configuration);

        Assert.Equal(before, await RegistryStartup.SnapshotAsync(mongo.Database, [code], tenants));
        Assert.DoesNotContain(log, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Startup_WhenIdentityExistsWithDifferentFacts_LeavesItByteIdenticalAndReportsFieldNamesOnly()
    {
        var code = RegistryStartup.NewClientCode();
        var tenant = Guid.NewGuid();
        var persistedHash = RegistryStartup.NewHash();
        await RegistryStartup.RunAsync(mongo.Database,
            RegistryStartup.Configuration(new RegistryStartup.Declared(code, persistedHash, [tenant], "v1")));
        var before = await RegistryStartup.SnapshotAsync(mongo.Database, [code], [tenant]);
        var declaredHash = RegistryStartup.NewHash();

        var log = await RegistryStartup.RunAsync(mongo.Database,
            RegistryStartup.Configuration(new RegistryStartup.Declared(code, declaredHash, [tenant], "v2")));

        Assert.Equal(before, await RegistryStartup.SnapshotAsync(mongo.Database, [code], [tenant]));
        var warning = Assert.Single(log, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(code, warning.Text, StringComparison.Ordinal);
        Assert.Contains("CredentialHash,CredentialVersion", warning.Text, StringComparison.Ordinal);
        Assert.All(log, entry =>
        {
            Assert.DoesNotContain(persistedHash, entry.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(declaredHash, entry.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task CreateIfAbsent_WhenIdentityAlreadyExists_ReturnsFalseAndLeavesTheDocumentByteIdentical()
    {
        var code = RegistryStartup.NewClientCode();
        await RegistryStartup.RunAsync(mongo.Database,
            RegistryStartup.Configuration(new RegistryStartup.Declared(code, RegistryStartup.NewHash(), [])));
        var before = await RegistryStartup.SnapshotAsync(mongo.Database, [code], []);

        var created = await new ServiceClientIdentityRepository(mongo.Database).CreateIfAbsentAsync(new ServiceClientIdentity
        {
            Id = ServiceClientIdentityIds.FromClientCode(code),
            ClientCode = code,
            ServiceName = "Diten.MDM",
            AllowedAudience = "TRUSTED_WORKFLOW_CONSUMER",
            ActiveCredentialHash = RegistryStartup.NewHash(),
            ActiveCredentialVersion = "attacker-version"
        }, CancellationToken.None);

        Assert.False(created);
        Assert.Equal(before, await RegistryStartup.SnapshotAsync(mongo.Database, [code], []));
    }

    [Fact]
    public async Task Startup_WhenGrantWasDisabled_LeavesItDisabledAndReportsIt()
    {
        var code = RegistryStartup.NewClientCode();
        var tenant = Guid.NewGuid();
        var configuration = RegistryStartup.Configuration(new RegistryStartup.Declared(code, RegistryStartup.NewHash(), [tenant]));
        await RegistryStartup.RunAsync(mongo.Database, configuration);
        var identityId = ServiceClientIdentityIds.FromClientCode(code);
        // The operator's act, done directly: the registry offers no way to disable a grant.
        await Grants.UpdateOneAsync(x => x.ServiceClientIdentityId == identityId && x.TenantId == tenant,
            Builders<ServiceClientTenantGrant>.Update.Set(x => x.IsEnabled, false));
        var before = await RegistryStartup.SnapshotAsync(mongo.Database, [code], [tenant]);

        var log = await RegistryStartup.RunAsync(mongo.Database, configuration);

        Assert.Equal(before, await RegistryStartup.SnapshotAsync(mongo.Database, [code], [tenant]));
        Assert.False((await Grants.Find(x => x.ServiceClientIdentityId == identityId && x.TenantId == tenant).SingleAsync()).IsEnabled);
        Assert.False(await new ServiceClientTenantGrantRepository(mongo.Database)
            .HasEnabledGrantAsync(tenant, identityId, "TRUSTED_WORKFLOW_CONSUMER", CancellationToken.None));
        var warning = Assert.Single(log, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("is disabled; it was left disabled", warning.Text, StringComparison.Ordinal);
        Assert.Contains(tenant.ToString("D"), warning.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateIfAbsent_WhenGrantAlreadyExistsDisabled_ReturnsFalseAndLeavesItDisabled()
    {
        var code = RegistryStartup.NewClientCode();
        var tenant = Guid.NewGuid();
        await RegistryStartup.RunAsync(mongo.Database,
            RegistryStartup.Configuration(new RegistryStartup.Declared(code, RegistryStartup.NewHash(), [tenant])));
        var identityId = ServiceClientIdentityIds.FromClientCode(code);
        await Grants.UpdateOneAsync(x => x.ServiceClientIdentityId == identityId && x.TenantId == tenant,
            Builders<ServiceClientTenantGrant>.Update.Set(x => x.IsEnabled, false));
        var before = await RegistryStartup.SnapshotAsync(mongo.Database, [code], [tenant]);

        var created = await new ServiceClientTenantGrantRepository(mongo.Database).CreateIfAbsentAsync(
            new ServiceClientTenantGrant
            {
                TenantId = tenant, ServiceClientIdentityId = identityId,
                Audience = "TRUSTED_WORKFLOW_CONSUMER", IsEnabled = true
            }, CancellationToken.None);

        Assert.False(created);
        Assert.Equal(before, await RegistryStartup.SnapshotAsync(mongo.Database, [code], [tenant]));
    }

    [Fact]
    public async Task Startup_WhenEntryOrTenantIsRemovedFromConfiguration_ChangesNothing()
    {
        var kept = RegistryStartup.NewClientCode();
        var removed = RegistryStartup.NewClientCode();
        var tenants = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var hash = RegistryStartup.NewHash();
        await RegistryStartup.RunAsync(mongo.Database, RegistryStartup.Configuration(
            new RegistryStartup.Declared(kept, hash, tenants),
            new RegistryStartup.Declared(removed, RegistryStartup.NewHash(), tenants)));
        var before = await RegistryStartup.SnapshotAsync(mongo.Database, [kept, removed], tenants);

        // The second client is gone from configuration, and the first one lost a tenant.
        await RegistryStartup.RunAsync(mongo.Database,
            RegistryStartup.Configuration(new RegistryStartup.Declared(kept, hash, [tenants[0]])));

        Assert.Equal(before, await RegistryStartup.SnapshotAsync(mongo.Database, [kept, removed], tenants));
    }

    [Fact]
    public async Task Startup_WhenIdentityWasRevokedOrSoftDeleted_NeitherRestoresNorGrantsIt()
    {
        var revoked = RegistryStartup.NewClientCode();
        var deleted = RegistryStartup.NewClientCode();
        var revokedHash = RegistryStartup.NewHash();
        var deletedHash = RegistryStartup.NewHash();
        await RegistryStartup.RunAsync(mongo.Database, RegistryStartup.Configuration(
            new RegistryStartup.Declared(revoked, revokedHash, []),
            new RegistryStartup.Declared(deleted, deletedHash, [])));
        await Identities.UpdateOneAsync(x => x.ClientCode == revoked, Builders<ServiceClientIdentity>.Update.Set(x => x.IsRevoked, true));
        await Identities.UpdateOneAsync(x => x.ClientCode == deleted, Builders<ServiceClientIdentity>.Update.Set(x => x.IsDeleted, true));
        var tenant = Guid.NewGuid();
        var before = await RegistryStartup.SnapshotAsync(mongo.Database, [revoked, deleted], [tenant]);

        var log = await RegistryStartup.RunAsync(mongo.Database, RegistryStartup.Configuration(
            new RegistryStartup.Declared(revoked, revokedHash, [tenant]),
            new RegistryStartup.Declared(deleted, deletedHash, [tenant])));

        Assert.Equal(before, await RegistryStartup.SnapshotAsync(mongo.Database, [revoked, deleted], [tenant]));
        Assert.Equal(1, await Identities.CountDocumentsAsync(x => x.ClientCode == deleted));
        Assert.Contains(log, entry => entry.Text.Contains("is revoked; it was left revoked", StringComparison.Ordinal));
        Assert.Contains(log, entry => entry.Text.Contains("was deleted; it was not created again", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Startup_WhenManyAuthInstancesStartTogether_ProducesOneIdentityOneGrantAndOneAuditRowEach()
    {
        await MongoDbIndexConfigurations.EnsureServiceIdentityIndexesAsync(mongo.Database);
        var code = RegistryStartup.NewClientCode();
        var tenant = Guid.NewGuid();
        var configuration = RegistryStartup.Configuration(new RegistryStartup.Declared(code, RegistryStartup.NewHash(), [tenant]));
        using var gate = new ManualResetEventSlim(false);

        var startups = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            gate.Wait();
            await RegistryStartup.RunAsync(mongo.Database, configuration);
        })).ToArray();
        gate.Set();
        await Task.WhenAll(startups);

        Assert.Equal(1, await Identities.CountDocumentsAsync(x => x.ClientCode == code));
        var identityId = ServiceClientIdentityIds.FromClientCode(code);
        Assert.Equal(1, await Grants.CountDocumentsAsync(x => x.ServiceClientIdentityId == identityId));
        var audits = (await RegistryStartup.SnapshotAsync(mongo.Database, [code], [tenant]))
            .Where(line => line.StartsWith("audit ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, audits.Length); // exactly one winner wrote each create
    }

    [Fact]
    public async Task Grants_WhenTwoWritersInsertTheSameTriple_TheUniqueIndexKeepsOne()
    {
        await MongoDbIndexConfigurations.EnsureServiceIdentityIndexesAsync(mongo.Database);
        var identityId = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var repository = new ServiceClientTenantGrantRepository(mongo.Database);
        ServiceClientTenantGrant Grant() => new()
        {
            TenantId = tenant, ServiceClientIdentityId = identityId, Audience = "TRUSTED_WORKFLOW_CONSUMER", IsEnabled = true
        };

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => repository.CreateIfAbsentAsync(Grant(), CancellationToken.None))));

        Assert.Single(results, created => created);
        Assert.Equal(1, await Grants.CountDocumentsAsync(x => x.ServiceClientIdentityId == identityId));
        var indexes = await (await mongo.Database.GetCollection<BsonDocument>("serviceClientTenantGrants").Indexes.ListAsync()).ToListAsync();
        var unique = Assert.Single(indexes, index => index["name"] == "ux_service_client_grant_tenant_client_audience_active");
        Assert.True(unique["unique"].AsBoolean);
    }

    [Fact]
    public async Task Startup_WhenOneEntryIsInvalid_AppliesNoEntryAtAll()
    {
        var valid = RegistryStartup.NewClientCode();
        var invalid = RegistryStartup.NewClientCode();
        var tenant = Guid.NewGuid();
        var configuration = RegistryStartup.Configuration(
            new RegistryStartup.Declared(valid, RegistryStartup.NewHash(), [tenant]),
            new RegistryStartup.Declared(invalid, "not-a-hash", [tenant]));

        var exception = await Assert.ThrowsAsync<ServiceClientRegistryConfigurationException>(
            () => RegistryStartup.RunAsync(mongo.Database, configuration));

        Assert.Equal("Configuration error: ServiceClientRegistry:Clients[1] CredentialHash is malformed.", exception.Message);
        // The VALID first entry was not applied either: validation is over the whole section, before any write.
        Assert.Empty(await RegistryStartup.SnapshotAsync(mongo.Database, [valid, invalid], [tenant]));
    }
}
