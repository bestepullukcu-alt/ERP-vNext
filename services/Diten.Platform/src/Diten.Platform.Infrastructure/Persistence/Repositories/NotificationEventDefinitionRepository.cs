using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0027-FU03 — Notification Event Catalog Mongo repository. EventCode is the unique business key (write-path guard
// via GetByEventCode + sync upsert); a unique index on EventCode is created best-effort at construction.
public sealed class NotificationEventDefinitionRepository : INotificationEventDefinitionRepository
{
    private readonly IMongoCollection<NotificationEventDefinition> _collection;

    public NotificationEventDefinitionRepository(IPlatformDbContext dbContext)
    {
        _collection = dbContext.GetCollection<NotificationEventDefinition>(PlatformCollections.NotificationEventDefinitions);
    }

    public async Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition definition, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(definition, cancellationToken: ct);
        return definition;
    }

    public async Task UpdateAsync(NotificationEventDefinition definition, CancellationToken ct = default)
    {
        definition.UpdatedAt = DateTimeOffset.UtcNow;
        await _collection.ReplaceOneAsync(
            Builders<NotificationEventDefinition>.Filter.Eq(x => x.Id, definition.Id),
            definition,
            cancellationToken: ct);
    }

    public async Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _collection.Find(And(ActiveFilter, Eq(x => x.Id, id))).FirstOrDefaultAsync(ct);

    public async Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default)
    {
        var normalized = (eventCode ?? string.Empty).Trim().ToLowerInvariant();
        return await _collection.Find(And(ActiveFilter, Eq(x => x.EventCode, normalized))).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(
        string? ownerModuleId = null,
        NotificationChannelCode? channel = null,
        NotificationEventStatus? status = null,
        bool? canTenantOverride = null,
        NotificationEventUsageType? usageType = null,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<NotificationEventDefinition>> { ActiveFilter };
        if (!string.IsNullOrWhiteSpace(ownerModuleId))
            filters.Add(Eq(x => x.OwnerModuleId, ownerModuleId.Trim()));
        if (channel is not null)
            filters.Add(Eq(x => x.Channel, channel.Value));
        if (status is not null)
            filters.Add(Eq(x => x.Status, status.Value));
        if (canTenantOverride is not null)
            filters.Add(Eq(x => x.CanTenantOverride, canTenantOverride.Value));
        if (usageType is not null)
            filters.Add(Eq(x => x.UsageType, usageType.Value));

        var items = await _collection
            .Find(And(filters.ToArray()))
            .SortBy(x => x.EventCode)
            .Skip(Math.Max(0, skip))
            .Limit(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);
        return items;
    }

    public async Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default)
    {
        var items = await _collection
            .Find(And(ActiveFilter, Eq(x => x.Status, NotificationEventStatus.Active)))
            .SortBy(x => x.EventCode)
            .ToListAsync(ct);
        return items;
    }

    private static FilterDefinition<NotificationEventDefinition> ActiveFilter =>
        Builders<NotificationEventDefinition>.Filter.Eq(x => x.IsDeleted, false);

    private static FilterDefinition<NotificationEventDefinition> Eq<TField>(
        System.Linq.Expressions.Expression<Func<NotificationEventDefinition, TField>> field, TField value) =>
        Builders<NotificationEventDefinition>.Filter.Eq(field, value);

    private static FilterDefinition<NotificationEventDefinition> And(params FilterDefinition<NotificationEventDefinition>[] f) =>
        Builders<NotificationEventDefinition>.Filter.And(f);
}
