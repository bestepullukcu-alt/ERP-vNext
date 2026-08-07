using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.Settings;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.S2S;

public sealed class S2SMongoContext : IS2SMongoContext
{
    public const string IncompatibleFailureCode = "FU16_S2S_UUID_REPRESENTATION_INCOMPATIBLE";
    private static readonly HashSet<string> Allowlist = new(StringComparer.Ordinal)
    {
        ServicePrincipalRepository.CollectionName,
        ServiceCredentialDescriptorRepository.CollectionName,
        S2SReplayReceiptStore.CollectionName
    };

    private readonly Lazy<MongoClient> _client;
    private readonly Lazy<IMongoDatabase> _database;
    private readonly SemaphoreSlim _compatibilityGate = new(1, 1);
    private bool _compatible;

    public S2SMongoContext(MongoDbSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString)) throw new InvalidOperationException("Mongo connection string is required.");
        if (string.IsNullOrWhiteSpace(settings.DatabaseName)) throw new InvalidOperationException("Mongo database name is required.");
        S2SGuidRepresentationPolicy.EnsureConfigured();
        DatabaseName = settings.DatabaseName;
        _client = new Lazy<MongoClient>(() =>
        {
            var clientSettings = MongoGuidRepresentationPolicy.CreateClientSettings(settings.ConnectionString);
            return new MongoClient(clientSettings);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        _database = new Lazy<IMongoDatabase>(() => _client.Value.GetDatabase(DatabaseName), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string DatabaseName { get; }
    public IMongoCollection<ServicePrincipal> ServicePrincipals => GetTyped<ServicePrincipal>(ServicePrincipalRepository.CollectionName);
    public IMongoCollection<ServiceCredentialDescriptor> ServiceCredentialDescriptors => GetTyped<ServiceCredentialDescriptor>(ServiceCredentialDescriptorRepository.CollectionName);
    public IMongoCollection<S2SReplayReceipt> ReplayReceipts => GetTyped<S2SReplayReceipt>(S2SReplayReceiptStore.CollectionName);

    public IMongoCollection<BsonDocument> GetAllowlistedRawCollection(string collectionName)
    {
        RequireAllowlisted(collectionName);
        return _database.Value.GetCollection<BsonDocument>(collectionName);
    }

    public Task<IClientSessionHandle> StartSessionAsync(CancellationToken cancellationToken) =>
        _client.Value.StartSessionAsync(cancellationToken: cancellationToken);

    public async Task EnsureCompatibleAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _compatible)) return;
        await _compatibilityGate.WaitAsync(cancellationToken);
        try
        {
            if (_compatible) return;
            await RequireStandardAsync(ServicePrincipalRepository.CollectionName, ["_id", nameof(ServicePrincipal.ServicePrincipalId)], cancellationToken);
            await RequireStandardAsync(ServiceCredentialDescriptorRepository.CollectionName,
                ["_id", nameof(ServiceCredentialDescriptor.CredentialId), nameof(ServiceCredentialDescriptor.ServicePrincipalId)], cancellationToken);
            Volatile.Write(ref _compatible, true);
        }
        finally { _compatibilityGate.Release(); }
    }

    private IMongoCollection<T> GetTyped<T>(string name)
    {
        RequireAllowlisted(name);
        return _database.Value.GetCollection<T>(name);
    }

    private static void RequireAllowlisted(string name)
    {
        if (!Allowlist.Contains(name)) throw new S2SMongoCollectionNotAllowedException(name);
    }

    private async Task RequireStandardAsync(string collectionName, IReadOnlyList<string> fields, CancellationToken cancellationToken)
    {
        IReadOnlyList<BsonDocument> documents;
        try
        {
            documents = await GetAllowlistedRawCollection(collectionName).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            throw new S2SUuidRepresentationIncompatibleException(collectionName, "uuid-field", ex);
        }
        foreach (var document in documents)
        foreach (var field in fields)
        {
            if (!document.TryGetValue(field, out var value) || value.BsonType != BsonType.Binary ||
                value.AsBsonBinaryData.SubType != BsonBinarySubType.UuidStandard)
                throw new S2SUuidRepresentationIncompatibleException(collectionName, field);
        }
    }
}

public sealed class S2SMongoCollectionNotAllowedException(string collectionName)
    : InvalidOperationException($"FU16 S2S Mongo collection is not allowlisted: {collectionName}");

public sealed class S2SUuidRepresentationIncompatibleException(string collectionName, string fieldName, Exception? innerException = null)
    : InvalidOperationException($"{S2SMongoContext.IncompatibleFailureCode}: {collectionName}.{fieldName} requires migration to BSON UuidStandard subtype 4", innerException)
{
    public string FailureCode => S2SMongoContext.IncompatibleFailureCode;
}
