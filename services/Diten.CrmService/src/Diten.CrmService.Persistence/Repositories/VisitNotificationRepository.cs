using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-VW-W5 (K-N6) — <c>crm_visit_notifications</c>: tenant scoped; the unique index (TenantId, EventCode, SubjectId)
/// keeps one e-mail per subject (a concurrent second insert loses with <see cref="TryInsertAsync"/> = false). The claim is
/// one find-and-update on scalar tick fields (a DateTimeOffset is a BSON array — never a filter key).
/// </summary>
public sealed class VisitNotificationRepository : IVisitNotificationRepository
{
    public const string CollectionName = "crm_visit_notifications";

    private readonly IMongoCollection<VisitNotification> _collection;

    public VisitNotificationRepository(IMongoDatabase database)
        => _collection = database.GetCollection<VisitNotification>(CollectionName);

    private static FilterDefinition<VisitNotification> Tenant(Guid tenantId)
        => Builders<VisitNotification>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<bool> TryInsertAsync(VisitNotification row, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(row, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<bool> ReplaceAsync(VisitNotification row, int expectedVersion, CancellationToken cancellationToken)
    {
        row.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<VisitNotification>.Filter.Where(x => x.Id == row.Id && x.TenantId == row.TenantId && x.Version == expectedVersion),
            row, cancellationToken: cancellationToken);
        if (result.IsAcknowledged && result.MatchedCount == 1)
        {
            return true;
        }

        row.Version = expectedVersion;
        return false;
    }

    public async Task<VisitNotification?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<VisitNotification>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<VisitNotification> Items, long Total)> ListAsync(
        Guid tenantId, VisitNotificationFilter filter, int skip, int take, CancellationToken cancellationToken)
    {
        var b = Builders<VisitNotification>.Filter;
        var f = Tenant(tenantId);
        if (filter.From is { } from) f &= b.Gte(x => x.VisitDate, from);
        if (filter.To is { } to) f &= b.Lte(x => x.VisitDate, to);
        if (filter.EventCode is { } ev) f &= b.Eq(x => x.EventCode, ev);
        if (filter.Status is { } st) f &= b.Eq(x => x.Status, st);
        if (filter.ResourceId is { } rep) f &= b.Eq(x => x.ResourceId, rep);
        if (filter.Reps is { } reps) f &= b.In(x => x.ResourceId, reps);
        var total = await _collection.CountDocumentsAsync(f, cancellationToken: cancellationToken);
        var items = await _collection.Find(f).SortByDescending(x => x.CreatedTicks).Skip(skip).Limit(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<VisitNotification?> ClaimNextAsync(
        long nowTicks, long staleQueuedBeforeTicks, long claimUntilTicks, string claimer, int maxAttempts, CancellationToken cancellationToken)
    {
        var b = Builders<VisitNotification>.Filter;
        var due = b.Eq(x => x.IsDeleted, false)
                  & b.Lte(x => x.ClaimedUntilTicks, nowTicks)
                  & ((b.Eq(x => x.Status, VisitNotificationStatuses.Failed) & b.Lte(x => x.NextAttemptTicks, nowTicks) & b.Lt(x => x.Attempts, maxAttempts))
                     | (b.Eq(x => x.Status, VisitNotificationStatuses.Queued) & b.Lte(x => x.CreatedTicks, staleQueuedBeforeTicks)));
        return await _collection.FindOneAndUpdateAsync(due,
            Builders<VisitNotification>.Update.Set(x => x.ClaimedUntilTicks, claimUntilTicks).Set(x => x.ClaimedBy, claimer),
            new FindOneAndUpdateOptions<VisitNotification> { ReturnDocument = ReturnDocument.After, Sort = Builders<VisitNotification>.Sort.Ascending(x => x.CreatedTicks) },
            cancellationToken);
    }
}

/// <summary>WP-VW-W5 (K-N5) — <c>crm_visit_notification_settings</c>: one document per tenant (unique TenantId).</summary>
public sealed class VisitNotificationSettingsRepository : IVisitNotificationSettingsRepository
{
    public const string CollectionName = "crm_visit_notification_settings";

    private readonly IMongoCollection<VisitNotificationSettings> _collection;

    public VisitNotificationSettingsRepository(IMongoDatabase database)
        => _collection = database.GetCollection<VisitNotificationSettings>(CollectionName);

    public async Task<VisitNotificationSettings?> GetAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _collection.Find(Builders<VisitNotificationSettings>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> InsertAsync(VisitNotificationSettings entity, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<bool> ReplaceAsync(VisitNotificationSettings entity, int expectedVersion, CancellationToken cancellationToken)
    {
        entity.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(
            Builders<VisitNotificationSettings>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId && x.Version == expectedVersion),
            entity, cancellationToken: cancellationToken);
        if (result.IsAcknowledged && result.MatchedCount == 1)
        {
            return true;
        }

        entity.Version = expectedVersion;
        return false;
    }

    public async Task<IReadOnlyList<Guid>> ListTenantsWithOverdueRemindersAsync(CancellationToken cancellationToken)
        => await _collection.Find(Builders<VisitNotificationSettings>.Filter.Where(x => !x.IsDeleted && x.OverdueReminders))
            .Project(x => x.TenantId).ToListAsync(cancellationToken);
}
