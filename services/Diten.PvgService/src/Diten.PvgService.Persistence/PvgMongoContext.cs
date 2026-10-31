using Diten.PvgService.Infrastructure.RegPvBase;
using MongoDB.Driver;

namespace Diten.PvgService.Persistence;

// The one Mongo client of the PVG build/test store, its database, and the draft collection. Created once per host and
// disposed with it, so a test host that is torn down releases its connections.
internal sealed class PvgMongoContext : IDisposable
{
    // The driver's 30-second default would make the start-up bootstrap and the readiness ping wait far too long.
    internal static readonly TimeSpan ServerSelectionTimeout = TimeSpan.FromSeconds(5);

    internal static readonly TransactionOptions WriteTransactionOptions =
        new(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority);

    private static int _sequence;

    public PvgMongoContext(string connectionString, string databaseName, Action<MongoClientSettings>? configureClientSettings)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        // The driver shares one cluster between clients with equal settings. This context disposes its own cluster, so
        // each context gets its own application name (a test hook may replace it) and with it its own cluster.
        settings.ApplicationName = "diten-pvg-store-" + Interlocked.Increment(ref _sequence);
        settings.ServerSelectionTimeout = ServerSelectionTimeout;
        settings.ReadConcern = ReadConcern.Majority;
        settings.WriteConcern = WriteConcern.WMajority;
        settings.ReadPreference = ReadPreference.Primary;
        configureClientSettings?.Invoke(settings);

        Client = new MongoClient(settings);
        Database = Client.GetDatabase(databaseName);
        Drafts = Database.GetCollection<PvgIntakeDraftDocument>(PvgIntakeDraftIndexCatalog.CollectionName);
    }

    public IMongoClient Client { get; }

    public IMongoDatabase Database { get; }

    public IMongoCollection<PvgIntakeDraftDocument> Drafts { get; }

    public void Dispose() => ClusterRegistry.Instance.UnregisterAndDisposeCluster(Client.Cluster);
}
