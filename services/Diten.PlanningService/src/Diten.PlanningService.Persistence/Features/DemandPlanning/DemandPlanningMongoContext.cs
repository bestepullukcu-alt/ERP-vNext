using Diten.PlanningService.Domain.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

public sealed class DemandPlanningMongoContext
{
    private static readonly object SerializerLock = new();
    private static bool _serializerReady;
    private readonly IMongoDatabase _database;
    private readonly IMongoClient _client;

    public DemandPlanningMongoContext(IConfiguration configuration)
    {
        EnsureStandardGuidSerialization();

        var connectionString = configuration["Mongo:ConnectionString"];
        var databaseName = configuration["Mongo:SupplyChainDatabaseName"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException(
                "Mongo:ConnectionString and Mongo:SupplyChainDatabaseName must identify the shared supply-chain database.");

        var settings = MongoClientSettings.FromConnectionString(connectionString);
        // In V3 the registered GuidSerializer controls subtype 4. The obsolete
        // client setting is V2-only and prevents a real connection from opening.
        _client = new MongoClient(settings);
        _database = _client.GetDatabase(databaseName);
    }

    public IMongoCollection<DemandHistoryImportBatch> HistoryImportBatches =>
        _database.GetCollection<DemandHistoryImportBatch>("mod0188_history_import_batches");

    public IMongoCollection<DemandHistoryReviewAttempt> HistoryReviewAttempts =>
        _database.GetCollection<DemandHistoryReviewAttempt>("mod0188_history_review_attempts");

    public IMongoCollection<PlanningCycle> PlanningCycles =>
        _database.GetCollection<PlanningCycle>("mod0188_planning_cycles");

    public IMongoCollection<DemandAuditEntry> AuditEntries =>
        _database.GetCollection<DemandAuditEntry>("mod0188_demand_audit");

    public IMongoCollection<DemandOutboxMessage> Outbox =>
        _database.GetCollection<DemandOutboxMessage>("mod0188_demand_outbox");

    public IMongoCollection<ManualDraftManifest> ManualDraftManifests =>
        _database.GetCollection<ManualDraftManifest>("mod0188_manual_draft_manifests");

    public IMongoCollection<ManualDraftSeriesPart> ManualDraftSeriesParts =>
        _database.GetCollection<ManualDraftSeriesPart>("mod0188_manual_draft_series");

    public IMongoCollection<ManualDraftAuditRecord> ManualDraftAudit =>
        _database.GetCollection<ManualDraftAuditRecord>("mod0188_manual_draft_audit");

    public IMongoCollection<ManualDraftReviewAuditRecord> ManualDraftReviewAudit =>
        _database.GetCollection<ManualDraftReviewAuditRecord>("mod0188_manual_draft_review_audit");

    public IMongoCollection<ManualDraftReviewSlot> ManualDraftReviewSlots =>
        _database.GetCollection<ManualDraftReviewSlot>("mod0188_manual_draft_review_slots");

    public IMongoCollection<ManualDraftPublicationAuditRecord> ManualDraftPublicationAudit =>
        _database.GetCollection<ManualDraftPublicationAuditRecord>("mod0188_manual_draft_publication_audit");

    public IMongoCollection<PublishedRevisionManifest> PublishedRevisionManifests =>
        _database.GetCollection<PublishedRevisionManifest>("mod0188_published_revision_manifests");

    public IMongoCollection<PublishedRevisionPart> PublishedRevisionParts =>
        _database.GetCollection<PublishedRevisionPart>("mod0188_published_revision_parts");

    public IMongoCollection<PublishedBaselineSlot> PublishedBaselineSlots =>
        _database.GetCollection<PublishedBaselineSlot>("mod0188_published_baseline_slots");

    public Task<IClientSessionHandle> StartSessionAsync(CancellationToken cancellationToken) =>
        _client.StartSessionAsync(cancellationToken: cancellationToken);

    private static void EnsureStandardGuidSerialization()
    {
        lock (SerializerLock)
        {
            if (_serializerReady) return;
#pragma warning disable CS0618
            BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            }
            catch (BsonSerializationException)
            {
                if (BsonSerializer.LookupSerializer<Guid>() is not GuidSerializer existing ||
                    existing.GuidRepresentation != GuidRepresentation.Standard)
                    throw new InvalidOperationException(
                        "MOD-0188 requires Mongo V3 standard UUID subtype-4 serialization.");
            }
            _serializerReady = true;
        }
    }
}
