using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

internal sealed class DwsFunctionalMongoScope : IAsyncDisposable
{
    private readonly DisposableDwsMongo _mongo;
    public string DatabaseName { get; } = "mod0354_functional_" + Guid.NewGuid().ToString("N");
    public DwsMongoContext Context { get; }
    public DwsFunctionalQueryStore QueryStore { get; }
    public DwsFunctionalCommandPort Commands { get; }
    public DwsFunctionalQueryPort Queries { get; }

    private DwsFunctionalMongoScope(DisposableDwsMongo mongo)
    {
        _mongo = mongo;
        Context = new DwsMongoContext(mongo.Client, DatabaseName);
        QueryStore = new DwsFunctionalQueryStore(Context);
        Commands = new DwsFunctionalCommandPort(QueryStore, new DwsMongoAtomicWriter(Context), TimeProvider.System);
        Queries = new DwsFunctionalQueryPort(QueryStore);
    }

    public static async Task<DwsFunctionalMongoScope> CreateAsync(DisposableDwsMongo mongo)
    {
        var scope = new DwsFunctionalMongoScope(mongo);
        await new DwsMongoIndexInitializer(scope.Context).InitializeAsync();
        return scope;
    }

    public DwsTrustedActorContext CommandActor(Guid tenant, string key, Guid? subject = null) => new(
        tenant,
        subject ?? Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        key);

    public static DwsTrustedActorContext QueryActor(DwsTrustedActorContext command) => command with { IdempotencyKey = null };

    public static ExternalContextReference Reference() => new(
        ExternalContextReference.RequiredContractName,
        ExternalContextReference.RequiredContractVersion,
        ExternalContextKind.Project,
        Guid.NewGuid());

    public async Task<long> CountTenantAsync(Guid tenant)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard));
        long count = 0;
        foreach (var alias in DwsMongoContext.CollectionAliases.Keys)
            count += await Context.Collection(alias).CountDocumentsAsync(filter);
        return count;
    }

    public async Task<IReadOnlyDictionary<string, long>> CountsAsync(Guid tenant)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard));
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var alias in DwsMongoContext.CollectionAliases.Keys)
            result[alias] = await Context.Collection(alias).CountDocumentsAsync(filter);
        return result;
    }

    public async ValueTask DisposeAsync() => await _mongo.Client.DropDatabaseAsync(DatabaseName);
}
