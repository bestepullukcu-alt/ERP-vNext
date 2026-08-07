using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.S2S;
using Diten.AuthService.Persistence.Settings;
using Diten.AuthService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.S2S;

[Collection(AuthServiceRealMongoTestCollection.Name)]
public sealed class AuthCommonUuidSchemaGateTests
{
    [Fact]
    public async Task Fresh_common_documents_are_uuid_standard_and_typed_filters_match_exactly()
    {
        await using var fixture = await Fixture.CreateAsync();
        var role = new Role("Operator", "Operator", null, fixture.TenantId) { Id = fixture.RoleId };
        var permission = new Permission("management-governance", "decisions", "read", "Read", null, "MOD-0007", PermissionScope.Tenant)
            { Id = fixture.PermissionId };
        var grant = RolePermission.ManualGrant(fixture.RoleId, fixture.PermissionId, fixture.TenantId, fixture.ActorId.ToString("D"));

        await fixture.Database.GetCollection<Role>("roles").InsertOneAsync(role);
        await fixture.Database.GetCollection<Permission>("permissions").InsertOneAsync(permission);
        await fixture.Database.GetCollection<RolePermission>("rolePermissions").InsertOneAsync(grant);
        var versions = new RoleAssignmentVersionRepository(fixture.Database);
        Assert.Equal(1, await versions.IncrementAsync(fixture.TenantId, CancellationToken.None));

        await fixture.AssertStandardAsync("roles", "_id", fixture.RoleId);
        await fixture.AssertStandardAsync("roles", "TenantId", fixture.TenantId);
        await fixture.AssertStandardAsync("permissions", "_id", fixture.PermissionId);
        await fixture.AssertStandardAsync("rolePermissions", "RoleId", fixture.RoleId);
        await fixture.AssertStandardAsync("rolePermissions", "PermissionId", fixture.PermissionId);
        await fixture.AssertStandardAsync("rolePermissions", "TenantId", fixture.TenantId);
        await fixture.AssertStandardAsync("auth_role_assignment_versions", "_id", fixture.TenantId);

        Assert.NotNull(await fixture.Database.GetCollection<Role>("roles")
            .Find(Builders<Role>.Filter.Eq(x => x.Id, fixture.RoleId)).SingleOrDefaultAsync());
        Assert.NotNull(await fixture.Database.GetCollection<Permission>("permissions")
            .Find(Builders<Permission>.Filter.Eq(x => x.Id, fixture.PermissionId)).SingleOrDefaultAsync());
        Assert.NotNull(await fixture.Database.GetCollection<RolePermission>("rolePermissions")
            .Find(Builders<RolePermission>.Filter.Eq(x => x.RoleId, fixture.RoleId)).SingleOrDefaultAsync());
        Assert.Equal(1, await versions.GetAsync(fixture.TenantId, CancellationToken.None));
    }

    [Fact]
    public async Task Legacy_version_is_typed_migration_required_and_is_never_read_as_zero_or_rewritten()
    {
        await using var fixture = await Fixture.CreateAsync();
        var legacy = new BsonDocument
        {
            ["_id"] = Legacy(fixture.TenantId),
            ["Version"] = 7L,
            ["UpdatedAt"] = DateTime.UtcNow
        };
        await fixture.InsertLegacyAsync("auth_role_assignment_versions", legacy);
        var storedLegacy = await fixture.ReadLegacyAsync("auth_role_assignment_versions");
        Assert.Equal(BsonBinarySubType.UuidLegacy, storedLegacy["_id"].AsBsonBinaryData.SubType);
        var repository = new RoleAssignmentVersionRepository(fixture.Database);

        var get = await Assert.ThrowsAsync<AuthUuidMigrationRequiredException>(
            () => repository.GetAsync(fixture.TenantId, CancellationToken.None));
        Assert.Equal("AUTH_UUID_MIGRATION_REQUIRED", get.FailureCode);
        var increment = await Assert.ThrowsAsync<AuthUuidMigrationRequiredException>(
            () => repository.IncrementAsync(fixture.TenantId, CancellationToken.None));
        Assert.Equal("AUTH_UUID_MIGRATION_REQUIRED", increment.FailureCode);

        var unchanged = await fixture.ReadLegacyAsync("auth_role_assignment_versions");
        Assert.Equal(BsonBinarySubType.UuidLegacy, unchanged["_id"].AsBsonBinaryData.SubType);
        Assert.Equal(7L, unchanged["Version"].AsInt64);
    }

    [Theory]
    [InlineData("roles", "_id")]
    [InlineData("roles", "TenantId")]
    [InlineData("permissions", "_id")]
    [InlineData("rolePermissions", "RoleId")]
    [InlineData("rolePermissions", "PermissionId")]
    [InlineData("rolePermissions", "TenantId")]
    public async Task Legacy_or_mixed_common_authorization_identity_fails_closed(string collection, string field)
    {
        await using var fixture = await Fixture.CreateAsync();
        var expected = field switch
        {
            "_id" when collection == "permissions" => fixture.PermissionId,
            "_id" => fixture.RoleId,
            "RoleId" => fixture.RoleId,
            "PermissionId" => fixture.PermissionId,
            _ => fixture.TenantId
        };
        var document = new BsonDocument("_id", ObjectId.GenerateNewId());
        if (field == "_id") document["_id"] = Legacy(expected);
        else document[field] = Legacy(expected);
        await fixture.InsertLegacyAsync(collection, document);
        var stored = await fixture.ReadLegacyAsync(collection);
        Assert.Equal(BsonBinarySubType.UuidLegacy, stored[field].AsBsonBinaryData.SubType);

        var exception = await Assert.ThrowsAsync<AuthUuidMigrationRequiredException>(() =>
            fixture.GuardAsync());
        Assert.Equal("AUTH_UUID_MIGRATION_REQUIRED", exception.FailureCode);
    }

    [Fact]
    public async Task Common_and_dedicated_contexts_share_standard_policy_but_are_client_isolated()
    {
        await using var fixture = await Fixture.CreateAsync();
        var dedicated = new S2SMongoContext(new MongoDbSettings
        {
            ConnectionString = fixture.Uri,
            DatabaseName = fixture.DatabaseName
        });

        var commonClient = (MongoClient)fixture.Client;
        var dedicatedClientField = typeof(S2SMongoContext).GetField("_client", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var lazy = (Lazy<MongoClient>)dedicatedClientField.GetValue(dedicated)!;
        Assert.NotSame(commonClient, lazy.Value);
#pragma warning disable CS0618
        Assert.Equal(GuidRepresentation.Standard, commonClient.Settings.GuidRepresentation);
        Assert.Equal(GuidRepresentation.Standard, lazy.Value.Settings.GuidRepresentation);
#pragma warning restore CS0618
    }

    [Fact]
    public async Task Production_di_creates_standard_common_client_against_fresh_disposable_database()
    {
        var uri = Fixture.RequireDisposableUri();
        var databaseName = $"diten_auth_uuid_production_di_{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MongoDbSettings:ConnectionString"] = uri,
            ["MongoDbSettings:DatabaseName"] = databaseName
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(configuration, new FakeHostEnvironment());
        await using var provider = services.BuildServiceProvider();
        var client = (MongoClient)provider.GetRequiredService<IMongoClient>();
#pragma warning disable CS0618
        Assert.Equal(GuidRepresentation.Standard, client.Settings.GuidRepresentation);
#pragma warning restore CS0618
        var database = provider.GetRequiredService<IMongoDatabase>();
        Assert.Equal(databaseName, database.DatabaseNamespace.DatabaseName);
        await client.DropDatabaseAsync(databaseName);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public IMongoClient Client { get; }
        public IMongoDatabase Database { get; }
        public string Uri { get; }
        public string DatabaseName { get; }
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid RoleId { get; } = Guid.NewGuid();
        public Guid PermissionId { get; } = Guid.NewGuid();

        private Fixture(string uri, IMongoClient client, string databaseName)
        {
            Uri = uri;
            Client = client;
            DatabaseName = databaseName;
            Database = client.GetDatabase(databaseName);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var uri = RequireDisposableUri();
            var url = new MongoUrl(uri);
            var policy = typeof(RoleAssignmentVersionRepository).Assembly
                .GetType("Diten.AuthService.Persistence.MongoGuidRepresentationPolicy", throwOnError: true)!;
            var createSettings = policy.GetMethod("CreateClientSettings", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            var settings = (MongoClientSettings)createSettings.Invoke(null, [uri])!;
            var client = new MongoClient(settings);
            var name = $"diten_auth_uuid_gate_{Guid.NewGuid():N}";
            await client.GetDatabase(name).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return new Fixture(uri, client, name);
        }

        public static string RequireDisposableUri()
        {
            var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI")
                      ?? throw new InvalidOperationException("MONGO_TEST_URI must point to disposable MongoDB on port 27021.");
            var url = new MongoUrl(uri);
            if (url.Servers.Any(server => server.Port is 27017 or 27018))
                throw new InvalidOperationException("Protected Mongo ports are forbidden.");
            if (url.Servers.Any(server => server.Port != 27021))
                throw new InvalidOperationException("Schema gate is restricted to disposable MongoDB port 27021.");
            return uri;
        }

        public async Task AssertStandardAsync(string collection, string field, Guid expected)
        {
            var document = await Database.GetCollection<BsonDocument>(collection)
                .Find(FilterDefinition<BsonDocument>.Empty).FirstAsync();
            Assert.Equal(BsonBinarySubType.UuidStandard, document[field].AsBsonBinaryData.SubType);
            Assert.Equal(expected, document[field].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
        }

        public async Task InsertLegacyAsync(string collection, BsonDocument document)
        {
            var settings = MongoClientSettings.FromConnectionString(Uri);
#pragma warning disable CS0618
            settings.GuidRepresentation = GuidRepresentation.CSharpLegacy;
#pragma warning restore CS0618
            var legacyClient = new MongoClient(settings);
            await legacyClient.GetDatabase(DatabaseName).GetCollection<BsonDocument>(collection).InsertOneAsync(document);
        }

        public async Task<BsonDocument> ReadLegacyAsync(string collection)
        {
            var settings = MongoClientSettings.FromConnectionString(Uri);
#pragma warning disable CS0618
            settings.GuidRepresentation = GuidRepresentation.CSharpLegacy;
#pragma warning restore CS0618
            var legacyClient = new MongoClient(settings);
            return await legacyClient.GetDatabase(DatabaseName).GetCollection<BsonDocument>(collection)
                .Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        }

        public async Task GuardAsync()
        {
            var guardType = typeof(RoleAssignmentVersionRepository).Assembly
                .GetType("Diten.AuthService.Persistence.Repositories.AuthCommonUuidCompatibilityGuard", throwOnError: true)!;
            var guard = Activator.CreateInstance(guardType, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null, args: [Database], culture: null)!;
            var method = guardType.GetMethod("EnsureAuthorizationDocumentsCompatibleAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            try
            {
                await (Task)method.Invoke(guard, [TenantId, RoleId, PermissionId, CancellationToken.None])!;
            }
            catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }

        public async ValueTask DisposeAsync() => await Client.DropDatabaseAsync(DatabaseName);
    }

    private static BsonBinaryData Legacy(Guid value) =>
        new(value, GuidRepresentation.CSharpLegacy);

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Diten.AuthService.Application.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
