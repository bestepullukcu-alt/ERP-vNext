using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

/// <summary>
/// WP-VW-W4 (W4-BE-b1) — the event inbox (the CrmService <c>CrmEventInboxRepository</c> copy). The EventId IS the document
/// <c>_id</c> (as a string), so uniqueness is the collection's own primary key — no extra index, no Guid representation trap.
/// Global (not tenant-scoped through the HTTP context): the consumer runs without a request.
/// </summary>
public sealed class MdmEventInboxRepository : IMdmEventInboxRepository
{
    public const string CollectionName = "mdm_event_inbox";

    private readonly IMongoCollection<ProcessedEvent> _collection;

    public MdmEventInboxRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ProcessedEvent>(CollectionName);

    public async Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            await _collection.InsertOneAsync(new ProcessedEvent
            {
                Id = eventId.ToString("D"),
                EventName = eventName,
                TenantId = tenantId.ToString("D"),
                ProcessedAt = DateTime.UtcNow
            }, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public sealed class ProcessedEvent
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        public string EventName { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
    }
}
