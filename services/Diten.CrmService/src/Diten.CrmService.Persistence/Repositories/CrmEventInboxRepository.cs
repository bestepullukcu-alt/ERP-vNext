using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-CL-BE-4 — the event inbox, copied from AuthService's IntegrationEventInboxRepository. The EventId IS the
/// document <c>_id</c> (as a string), so uniqueness is the collection's own primary key — no extra index, no Guid
/// representation trap.
/// </summary>
public sealed class CrmEventInboxRepository : ICrmEventInboxRepository
{
    public const string CollectionName = "crm_event_inbox";

    private readonly IMongoCollection<ProcessedEvent> _collection;

    public CrmEventInboxRepository(IMongoDatabase database)
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
