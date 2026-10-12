using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Domain.Quarantine.Entities;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Quarantine;

/// <summary>
/// MOD-0175 Q2 — the workflow event inbox, the CRM <c>CrmEventInboxRepository</c> shape: the EventId IS the document
/// <c>_id</c> (as a string), so uniqueness is the collection's own primary key. The decision lookup is Tenant-scoped (never
/// cross-tenant) and reads only the decision's company.
/// </summary>
public sealed class QuarantineEventInboxRepository : IQuarantineEventInbox
{
    private readonly IMongoCollection<ProcessedEvent> _inbox;
    private readonly IMongoCollection<QuarantineDecision> _decisions;

    public QuarantineEventInboxRepository(IMongoDatabase database)
    {
        _inbox = database.GetCollection<ProcessedEvent>(QuarantineCollections.EventInbox);
        _decisions = database.GetCollection<QuarantineDecision>(QuarantineCollections.Decisions);
    }

    public async Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _inbox.InsertOneAsync(new ProcessedEvent
            {
                Id = eventId.ToString("D"),
                EventName = eventName,
                TenantId = tenantId.ToString("D"),
                ProcessedAt = DateTime.UtcNow
            }, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<Guid?> DecisionCompanyAsync(Guid tenantId, Guid decisionId, CancellationToken cancellationToken = default)
    {
        var f = Builders<QuarantineDecision>.Filter;
        var decision = await _decisions.Find(f.Eq(d => d.TenantId, tenantId) & f.Eq(d => d.Id, decisionId) & f.Eq(d => d.IsDeleted, false))
            .Project(d => d.LegalEntityId).FirstOrDefaultAsync(cancellationToken);
        return decision;
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
