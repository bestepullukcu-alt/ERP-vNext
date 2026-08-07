using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.S2S;
using Diten.AuthService.Persistence.Settings;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.S2S;

[Collection(AuthServiceRealMongoTestCollection.Name)]
public sealed class S2SMongoFoundationTests
{
    private static string RequiredMongoUri
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("MONGO_TEST_URI");
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("MONGO_TEST_URI must identify the disposable FU16 Mongo instance.");
            var url = new MongoUrl(value);
            if (url.Servers.Any(x => x.Port is 27017 or 27018)) throw new InvalidOperationException("Protected Mongo ports are forbidden for FU16 tests.");
            return value;
        }
    }

    [Fact]
    public async Task Production_indexes_enforce_exact_concurrency_and_cas()
    {
        var (client, database, databaseName) = await CreateDatabaseAsync();
        try
        {
            await MongoDbIndexConfigurations.EnsureIndexesAsync(database);
            var context = Context(databaseName);
            await S2SMongoIndexInitializer.EnsureAsync(context);
            var principals = new ServicePrincipalRepository(context);
            var credentials = new ServiceCredentialDescriptorRepository(context);
            var replay = new S2SReplayReceiptStore(context);
            var principalId = Guid.NewGuid();
            var firstPrincipal = CreatePrincipal(principalId, "gate-i-producer");
            var secondPrincipal = CreatePrincipal(Guid.NewGuid(), "gate-i-producer");

            var principalStart = NewGate();
            var principalResults = await RaceAsync(principalStart,
                () => principals.TryCreateAsync(firstPrincipal, CancellationToken.None),
                () => principals.TryCreateAsync(secondPrincipal, CancellationToken.None));
            Assert.Equal(1, principalResults.Count(x => x));

            var firstCredential = CreateCredential(Guid.NewGuid(), principalId, "gate-i-kid");
            var duplicateIdentity = CreateCredential(firstCredential.Id, Guid.NewGuid(), "other-kid");
            Assert.True(await credentials.TryCreateAsync(firstCredential, CancellationToken.None));
            Assert.False(await credentials.TryCreateAsync(duplicateIdentity, CancellationToken.None));
            Assert.False(await credentials.TryCreateAsync(CreateCredential(Guid.NewGuid(), Guid.NewGuid(), "gate-i-kid"), CancellationToken.None));

            var now = DateTimeOffset.UtcNow;
            var jti = Guid.NewGuid().ToString("D");
            var nonce = Guid.NewGuid().ToString("D");
            var receiptStart = NewGate();
            var receiptResults = await RaceAsync(receiptStart,
                () => replay.TryAcceptAsync(new S2SReplayReceipt("diten-auth-service", jti, nonce, "hash-a", now.AddMinutes(5), now), CancellationToken.None),
                () => replay.TryAcceptAsync(new S2SReplayReceipt("diten-auth-service", jti, Guid.NewGuid().ToString("D"), "hash-b", now.AddMinutes(5), now), CancellationToken.None));
            Assert.Equal(1, receiptResults.Count(x => x.Kind == ReplayReceiptAcceptanceKind.Accepted));
            Assert.Equal(1, receiptResults.Count(x => x.Kind == ReplayReceiptAcceptanceKind.Replay));

            var exactNonce = Guid.NewGuid().ToString("D");
            Assert.Equal(ReplayReceiptAcceptanceKind.Accepted, (await replay.TryAcceptAsync(
                new S2SReplayReceipt("diten-auth-service", Guid.NewGuid().ToString("D"), exactNonce, "hash-c", now.AddMinutes(5), now),
                CancellationToken.None)).Kind);
            var nonceResult = await replay.TryAcceptAsync(new S2SReplayReceipt("diten-auth-service", Guid.NewGuid().ToString("D"), exactNonce, "hash-d", now.AddMinutes(5), now), CancellationToken.None);
            Assert.Equal(ReplayReceiptAcceptanceKind.Replay, nonceResult.Kind);

            var stored = await principals.GetByClientIdAsync("gate-i-producer", CancellationToken.None);
            Assert.NotNull(stored);
            Assert.NotEqual(Guid.Empty, stored.Id);
            var expectedVersion = stored.PrincipalVersion;
            Assert.Equal(1, expectedVersion);
            var rawPrincipal = await database.GetCollection<BsonDocument>(ServicePrincipalRepository.CollectionName)
                .Find(new BsonDocument("ClientId", "gate-i-producer")).SingleAsync();
            Assert.Equal(stored.Id, rawPrincipal["_id"].AsGuid);
            Assert.Equal(expectedVersion, rawPrincipal["PrincipalVersion"].ToInt64());
            Assert.False(rawPrincipal["IsDeleted"].ToBoolean());
            var firstCopy = Clone(stored);
            var secondCopy = Clone(stored);
            firstCopy.TransitionTo(ServicePrincipalStatus.Active, "operator-a", now);
            secondCopy.TransitionTo(ServicePrincipalStatus.Active, "operator-b", now);
            Assert.Equal(stored.Id, firstCopy.Id);
            Assert.Equal(stored.Id, secondCopy.Id);
            Assert.Equal(expectedVersion + 1, firstCopy.PrincipalVersion);
            Assert.Equal(ServicePrincipalStatus.Active, firstCopy.Status);
            var casStart = NewGate();
            var casResults = await RaceAsync(casStart,
                () => principals.TryReplaceAsync(firstCopy, expectedVersion, CancellationToken.None),
                () => principals.TryReplaceAsync(secondCopy, expectedVersion, CancellationToken.None));
            Assert.Equal(1, casResults.Count(x => x));

            await AssertIndexesAsync(database);
            await AssertNoSecretMaterialAsync(database);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task Cancellation_leaves_no_receipt_and_unavailable_authority_is_503_class()
    {
        var (client, database, databaseName) = await CreateDatabaseAsync();
        try
        {
            await MongoDbIndexConfigurations.EnsureIndexesAsync(database);
            var collection = database.GetCollection<S2SReplayReceipt>(S2SReplayReceiptStore.CollectionName);
            var store = new S2SReplayReceiptStore(Context(databaseName));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var now = DateTimeOffset.UtcNow;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TryAcceptAsync(
                new S2SReplayReceipt("diten-auth-service", Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), "hash", now.AddMinutes(5), now),
                cancelled.Token));
            Assert.Equal(0, await collection.CountDocumentsAsync(FilterDefinition<S2SReplayReceipt>.Empty));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }

        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:1");
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(100);
        settings.ConnectTimeout = TimeSpan.FromMilliseconds(100);
        var unavailable = new S2SReplayReceiptStore(new S2SMongoContext(new MongoDbSettings
        {
            ConnectionString = "mongodb://127.0.0.1:1", DatabaseName = "fu16_unavailable"
        }));
        var acceptedAt = DateTimeOffset.UtcNow;
        var result = await unavailable.TryAcceptAsync(new S2SReplayReceipt("diten-auth-service", Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"), "hash", acceptedAt.AddMinutes(5), acceptedAt), CancellationToken.None);
        Assert.Equal(ReplayReceiptAcceptanceKind.AuthorityUnavailable, result.Kind);
        Assert.Equal(503, result.SuggestedHttpStatusCode);
    }

    private static async Task<(MongoClient Client, IMongoDatabase Database, string DatabaseName)> CreateDatabaseAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(RequiredMongoUri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        var databaseName = "diten_auth_fu16_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(databaseName);
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        return (client, database, databaseName);
    }

    private static S2SMongoContext Context(string databaseName) => new(new MongoDbSettings
    {
        ConnectionString = RequiredMongoUri,
        DatabaseName = databaseName
    });

    private static async Task<T[]> RaceAsync<T>(TaskCompletionSource gate, Func<Task<T>> first, Func<Task<T>> second)
    {
        async Task<T> Run(Func<Task<T>> action) { await gate.Task; return await action(); }
        var tasks = new[] { Run(first), Run(second) };
        gate.SetResult();
        return await Task.WhenAll(tasks);
    }

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ServicePrincipal CreatePrincipal(Guid id, string clientId) => new(id, clientId, "Gate I producer", ["MOD-0007"],
        ["diten-management-governance-service"], [DelegatedActorProofV1.ExactScope], DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1), "test");

    private static ServicePrincipal Clone(ServicePrincipal source) => new(source.Id, source.ClientId, source.DisplayName,
        source.OwnerModuleIds, source.AllowedAudiences, source.AllowedProtocolScopes, source.NotBeforeUtc, source.ExpiresAtUtc, source.CreatedBy);

    private static ServiceCredentialDescriptor CreateCredential(Guid id, Guid principalId, string kid)
    {
        var now = DateTimeOffset.UtcNow;
        return new ServiceCredentialDescriptor(id, principalId, kid, "RS256", 3072, "public-reference", "thumbprint",
            now.AddMinutes(-1), now.AddDays(1), 1, now.AddHours(1), "test");
    }

    private static async Task AssertIndexesAsync(IMongoDatabase database)
    {
        static async Task<BsonDocument[]> ReadAsync<T>(IMongoCollection<T> collection) =>
            (await (await collection.Indexes.ListAsync()).ToListAsync()).ToArray();

        var principalIndexes = await ReadAsync(database.GetCollection<ServicePrincipal>(ServicePrincipalRepository.CollectionName));
        var credentialIndexes = await ReadAsync(database.GetCollection<ServiceCredentialDescriptor>(ServiceCredentialDescriptorRepository.CollectionName));
        var replayIndexes = await ReadAsync(database.GetCollection<S2SReplayReceipt>(S2SReplayReceiptStore.CollectionName));
        AssertUnique(principalIndexes, ServicePrincipalRepository.ClientIdUniqueIndexName, new BsonDocument("ClientId", 1));
        AssertUnique(credentialIndexes, ServiceCredentialDescriptorRepository.CredentialIdUniqueIndexName, new BsonDocument("CredentialId", 1));
        AssertUnique(credentialIndexes, ServiceCredentialDescriptorRepository.KidUniqueIndexName, new BsonDocument("Kid", 1));
        AssertUnique(replayIndexes, S2SReplayReceiptStore.IssuerJtiUniqueIndexName, new BsonDocument { { "Issuer", 1 }, { "Jti", 1 } });
        AssertUnique(replayIndexes, S2SReplayReceiptStore.IssuerNonceUniqueIndexName, new BsonDocument { { "Issuer", 1 }, { "Nonce", 1 } });
        Assert.DoesNotContain(replayIndexes, x => x.GetValue("expireAfterSeconds", BsonNull.Value) != BsonNull.Value);
    }

    private static void AssertUnique(IEnumerable<BsonDocument> indexes, string name, BsonDocument key)
    {
        var index = Assert.Single(indexes, x => x["name"].AsString == name);
        Assert.True(index.GetValue("unique", false).ToBoolean());
        Assert.Equal(key, index["key"].AsBsonDocument);
    }

    private static async Task AssertNoSecretMaterialAsync(IMongoDatabase database)
    {
        foreach (var collectionName in new[] { ServicePrincipalRepository.CollectionName, ServiceCredentialDescriptorRepository.CollectionName, S2SReplayReceiptStore.CollectionName })
        {
            var documents = await database.GetCollection<BsonDocument>(collectionName).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
            foreach (var document in documents)
            {
                var text = document.ToJson();
                Assert.DoesNotContain("privateKey", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("pem", text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
