using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.S2S;
using Diten.AuthService.Persistence.Settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class S2SMongoContextIsolationTests
{
    [Fact]
    public void Architecture_is_allowlisted_and_does_not_replace_common_mongo_dependencies()
    {
        Assert.Equal(typeof(IS2SMongoContext), Assert.Single(typeof(ServicePrincipalRepository).GetConstructors()).GetParameters().Single().ParameterType);
        Assert.Equal(typeof(IS2SMongoContext), Assert.Single(typeof(ServiceCredentialDescriptorRepository).GetConstructors()).GetParameters().Single().ParameterType);
        Assert.Equal(typeof(IS2SMongoContext), Assert.Single(typeof(S2SReplayReceiptStore).GetConstructors()).GetParameters().Single().ParameterType);
        Assert.False(typeof(IMongoClient).IsAssignableFrom(typeof(S2SMongoContext)));

        foreach (var legacy in new[] { typeof(UserRepository), typeof(RoleRepository), typeof(PermissionRepository),
                     typeof(UserRoleRepository), typeof(RolePermissionRepository), typeof(RefreshTokenRepository), typeof(TenantUserMembershipRepository) })
            Assert.DoesNotContain(legacy.GetConstructors().SelectMany(x => x.GetParameters()), x => x.ParameterType == typeof(IS2SMongoContext));
    }

    [Fact]
    public void Effective_policy_and_concrete_member_serializers_are_standard()
    {
        var (uri, databaseName) = RequiredSettings();
        var context = new S2SMongoContext(new MongoDbSettings { ConnectionString = uri, DatabaseName = databaseName });
        Assert.Equal(GuidRepresentation.Standard, EffectivePolicy());
        Assert.Equal(GuidRepresentation.Standard, EffectiveClientRepresentation(context));
        AssertStandardSerializer<ServicePrincipal>(nameof(ServicePrincipal.ServicePrincipalId));
        AssertStandardSerializer<ServiceCredentialDescriptor>(nameof(ServiceCredentialDescriptor.CredentialId));
        AssertStandardSerializer<ServiceCredentialDescriptor>(nameof(ServiceCredentialDescriptor.ServicePrincipalId));
    }

    [Fact]
    public async Task Dedicated_context_writes_standard_bytes_and_allowlist_fails_closed()
    {
        var (uri, databaseName) = RequiredSettings();
        var context = new S2SMongoContext(new MongoDbSettings { ConnectionString = uri, DatabaseName = databaseName });
        Assert.Equal(databaseName, context.DatabaseName);
        Assert.Throws<S2SMongoCollectionNotAllowedException>(() => context.GetAllowlistedRawCollection("users"));
        await S2SMongoIndexInitializer.EnsureAsync(context);

        var now = DateTimeOffset.UtcNow;
        var principal = new ServicePrincipal(Guid.NewGuid(), "uuid-standard", "UUID", ["MOD-0007"], ["diten-fpa-service"],
            [DelegatedActorProofV1.ExactScope], now, now.AddHours(1), "test");
        Assert.True(await new ServicePrincipalRepository(context).TryCreateAsync(principal, CancellationToken.None));
        var raw = await context.GetAllowlistedRawCollection(ServicePrincipalRepository.CollectionName)
            .Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        AssertStandard(raw["_id"], principal.Id);
        AssertStandard(raw[nameof(ServicePrincipal.ServicePrincipalId)], principal.ServicePrincipalId);
        var rendered = Builders<ServicePrincipal>.Filter.Eq(x => x.ServicePrincipalId, principal.ServicePrincipalId)
            .Render(context.ServicePrincipals.DocumentSerializer, context.ServicePrincipals.Settings.SerializerRegistry);
        Assert.Equal(raw[nameof(ServicePrincipal.ServicePrincipalId)].AsBsonBinaryData.Bytes,
            rendered[nameof(ServicePrincipal.ServicePrincipalId)].AsBsonBinaryData.Bytes);

    }

    [Fact]
    public async Task Common_client_behavior_remains_legacy()
    {
        var (uri, databaseName) = RequiredSettings();
        var commonDatabase = new MongoClient(uri).GetDatabase(databaseName);
        var commonId = Guid.NewGuid();
        await commonDatabase.GetCollection<BsonDocument>("commonUuidProbe").InsertOneAsync(new BsonDocument
        {
            ["value"] = new BsonBinaryData(commonId, GuidRepresentation.CSharpLegacy)
        });
        var commonRaw = await commonDatabase.GetCollection<BsonDocument>("commonUuidProbe").Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        Assert.Equal(BsonBinarySubType.UuidLegacy, commonRaw["value"].AsBsonBinaryData.SubType);
    }

    [Fact]
    public async Task Legacy_fu16_document_is_migration_required_without_rewrite()
    {
        var (uri, databaseName) = RequiredSettings();
        var common = new MongoClient(uri).GetDatabase(databaseName);
        var id = Guid.NewGuid();
        await common.GetCollection<BsonDocument>(ServicePrincipalRepository.CollectionName).InsertOneAsync(new BsonDocument
        {
            ["_id"] = new BsonBinaryData(id, GuidRepresentation.CSharpLegacy),
            [nameof(ServicePrincipal.ServicePrincipalId)] = new BsonBinaryData(id, GuidRepresentation.CSharpLegacy)
        });
        var context = new S2SMongoContext(new MongoDbSettings { ConnectionString = uri, DatabaseName = databaseName });
        var error = await Assert.ThrowsAsync<S2SUuidRepresentationIncompatibleException>(() => context.EnsureCompatibleAsync(CancellationToken.None));
        Assert.Equal(S2SMongoContext.IncompatibleFailureCode, error.FailureCode);
        Assert.Equal(1, await common.GetCollection<BsonDocument>(ServicePrincipalRepository.CollectionName).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    private static (string Uri, string DatabaseName) RequiredSettings()
    {
        var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? throw new InvalidOperationException("MONGO_TEST_URI is required.");
        var url = new MongoUrl(uri); if (url.Servers.Any(x => x.Port is 27017 or 27018)) throw new InvalidOperationException("Protected Mongo port.");
        return (uri, $"fu16_context_{Guid.NewGuid():N}");
    }

    private static void AssertStandard(BsonValue value, Guid expected)
    {
        Assert.Equal(BsonBinarySubType.UuidStandard, value.AsBsonBinaryData.SubType);
        Assert.Equal(expected, value.AsBsonBinaryData.ToGuid(GuidRepresentation.Standard));
    }

    private static GuidRepresentation EffectivePolicy()
    {
        var policy = typeof(S2SMongoContext).Assembly.GetType("Diten.AuthService.Persistence.S2S.S2SGuidRepresentationPolicy", throwOnError: true)!;
        return (GuidRepresentation)policy.GetField("Canonical", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.GetRawConstantValue()!;
    }

    private static GuidRepresentation EffectiveClientRepresentation(S2SMongoContext context)
    {
        var field = typeof(S2SMongoContext).GetField("_client", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var client = ((Lazy<MongoClient>)field.GetValue(context)!).Value;
#pragma warning disable CS0618
        return client.Settings.GuidRepresentation;
#pragma warning restore CS0618
    }

    private static void AssertStandardSerializer<T>(string memberName)
    {
        var serializer = Assert.IsType<GuidSerializer>(BsonClassMap.LookupClassMap(typeof(T)).GetMemberMap(memberName).GetSerializer());
        Assert.Equal(GuidRepresentation.Standard, serializer.GuidRepresentation);
    }
}
