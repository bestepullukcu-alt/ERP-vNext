using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class IntegrationEventInboxRepository : IIntegrationEventInboxRepository
{
    private readonly IMongoCollection<ProcessedIntegrationEvent> _collection;

    public IntegrationEventInboxRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ProcessedIntegrationEvent>("integrationEventInbox");
    }

    public async Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
    {
        var entity = new ProcessedIntegrationEvent(eventId, eventName, tenantId);
        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
