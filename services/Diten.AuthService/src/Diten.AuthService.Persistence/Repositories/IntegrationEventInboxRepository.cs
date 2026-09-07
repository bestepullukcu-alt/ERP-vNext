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

    public async Task<IntegrationEventClaim> TryClaimAsync(
        Guid eventId,
        string eventName,
        Guid tenantId,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        var now = DateTimeOffset.UtcNow;
        var claimId = Guid.NewGuid();
        try
        {
            await _collection.InsertOneAsync(
                new ProcessedIntegrationEvent(eventId, eventName, tenantId, claimId, now.Add(leaseDuration)),
                cancellationToken: ct);
            return new IntegrationEventClaim(IntegrationEventClaimResult.Claimed, claimId);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await _collection.Find(item => item.EventId == eventId).FirstOrDefaultAsync(ct);
            if (existing is null)
            {
                return new IntegrationEventClaim(IntegrationEventClaimResult.Busy);
            }

            if (existing.TenantId != tenantId)
            {
                return new IntegrationEventClaim(IntegrationEventClaimResult.TenantMismatch);
            }

            if (!string.Equals(existing.EventName, eventName, StringComparison.Ordinal))
            {
                return new IntegrationEventClaim(IntegrationEventClaimResult.IdentityMismatch);
            }

            if (existing.State == IntegrationEventInboxState.Completed)
            {
                return new IntegrationEventClaim(IntegrationEventClaimResult.Completed);
            }

            if (!existing.LeaseExpiresAtUtc.HasValue || existing.LeaseExpiresAtUtc.Value > now)
            {
                return new IntegrationEventClaim(IntegrationEventClaimResult.Busy);
            }

            var filter = Builders<ProcessedIntegrationEvent>.Filter.And(
                Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.EventId, eventId),
                Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.TenantId, tenantId),
                Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.State, IntegrationEventInboxState.Processing),
                Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.LeaseExpiresAtUtc, existing.LeaseExpiresAtUtc));
            var update = Builders<ProcessedIntegrationEvent>.Update
                .Set(item => item.ClaimId, claimId)
                .Set(item => item.LeaseExpiresAtUtc, now.Add(leaseDuration))
                .Set(item => item.UpdatedAt, now)
                .Set(item => item.UpdatedBy, "internal-consumer");
            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
            return result.ModifiedCount == 1
                ? new IntegrationEventClaim(IntegrationEventClaimResult.Claimed, claimId)
                : new IntegrationEventClaim(IntegrationEventClaimResult.Busy);
        }
    }

    public async Task CompleteClaimAsync(Guid eventId, Guid tenantId, Guid claimId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = Builders<ProcessedIntegrationEvent>.Filter.And(
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.EventId, eventId),
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.TenantId, tenantId),
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.ClaimId, claimId),
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.State, IntegrationEventInboxState.Processing));
        var update = Builders<ProcessedIntegrationEvent>.Update
            .Set(item => item.State, IntegrationEventInboxState.Completed)
            .Set(item => item.ProcessedAt, now)
            .Set(item => item.ClaimId, null)
            .Set(item => item.LeaseExpiresAtUtc, null)
            .Set(item => item.UpdatedAt, now)
            .Set(item => item.UpdatedBy, "internal-consumer");
        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        if (result.ModifiedCount == 1)
        {
            return;
        }

        var existing = await _collection.Find(item => item.EventId == eventId).FirstOrDefaultAsync(ct);
        if (existing?.TenantId == tenantId && existing.State == IntegrationEventInboxState.Completed)
        {
            return;
        }

        throw new InvalidOperationException("The entitlement inbox claim cannot be completed by this tenant.");
    }

    public async Task ReleaseClaimAsync(Guid eventId, Guid tenantId, Guid claimId, CancellationToken ct = default)
    {
        var filter = Builders<ProcessedIntegrationEvent>.Filter.And(
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.EventId, eventId),
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.TenantId, tenantId),
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.ClaimId, claimId),
            Builders<ProcessedIntegrationEvent>.Filter.Eq(item => item.State, IntegrationEventInboxState.Processing));
        await _collection.DeleteOneAsync(filter, ct);
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
