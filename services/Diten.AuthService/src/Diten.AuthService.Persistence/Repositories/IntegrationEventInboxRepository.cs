using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class IntegrationEventInboxRepository : IIntegrationEventInboxRepository
{
    private const string CompletionActor = "entitlement-sync";
    private readonly IMongoCollection<ProcessedIntegrationEvent> _collection;

    public IntegrationEventInboxRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ProcessedIntegrationEvent>("integrationEventInbox");
    }

    public async Task<IntegrationEventInboxEntry?> GetAsync(
        Guid eventId,
        string eventName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var entity = await _collection
            .Find(item => item.EventId == eventId
                          && item.EventName == eventName
                          && item.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        return entity is null
            ? null
            : new IntegrationEventInboxEntry(
                entity.EventId,
                entity.EventName,
                entity.TenantId,
                entity.CompletionProtocolVersion);
    }

    public async Task MarkCompletedAsync(
        Guid eventId,
        string eventName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = Builders<ProcessedIntegrationEvent>.Filter.And(
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.EventId, eventId),
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.EventName, eventName),
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.TenantId, tenantId),
            Builders<ProcessedIntegrationEvent>.Filter.Ne(
                item => item.CompletionProtocolVersion,
                ProcessedIntegrationEvent.CurrentCompletionProtocolVersion));
        var update = Builders<ProcessedIntegrationEvent>.Update
            .SetOnInsert(item => item.Id, Guid.NewGuid())
            .SetOnInsert(item => item.CreatedAt, now)
            .SetOnInsert(item => item.CreatedBy, CompletionActor)
            .SetOnInsert(item => item.EventId, eventId)
            .SetOnInsert(item => item.EventName, eventName)
            .SetOnInsert(item => item.TenantId, tenantId)
            .Set(item => item.ProcessedAt, now)
            .Set(item => item.CompletionProtocolVersion, ProcessedIntegrationEvent.CurrentCompletionProtocolVersion)
            .Set(item => item.UpdatedAt, now)
            .Set(item => item.UpdatedBy, CompletionActor);

        try
        {
            await _collection.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true },
                ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await GetAsync(eventId, eventName, tenantId, ct);
            if (existing is not null
                && HasExactFacts(existing, eventName, tenantId)
                && existing.CompletionProtocolVersion == ProcessedIntegrationEvent.CurrentCompletionProtocolVersion)
            {
                return; // A concurrent delivery completed the exact same event facts.
            }

            throw new InvalidOperationException(
                $"Integration event '{eventId}' conflicts with previously recorded event facts.",
                ex);
        }
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

    private static bool HasExactFacts(
        IntegrationEventInboxEntry existing,
        string eventName,
        Guid tenantId)
        => existing.TenantId == tenantId
           && string.Equals(existing.EventName, eventName, StringComparison.Ordinal);
}
