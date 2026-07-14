using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantMessagingSettingsRepository : ITenantMessagingSettingsRepository
{
    private readonly IMongoCollection<TenantMessagingSettings> _collection;

    public TenantMessagingSettingsRepository(IPlatformDbContext dbContext)
    {
        _collection = dbContext.GetCollection<TenantMessagingSettings>("notification_tenant_messaging_settings");
    }

    public async Task<TenantMessagingSettings> CreateAsync(TenantMessagingSettings settings, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(settings, cancellationToken: ct);
        return settings;
    }

    public async Task<TenantMessagingSettings?> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<TenantMessagingSettings>.Filter.And(
            ActiveFilter,
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.IsPlatformDefault, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<TenantMessagingSettings?> GetPlatformDefaultAsync(CancellationToken ct = default)
    {
        var filter = Builders<TenantMessagingSettings>.Filter.And(
            ActiveFilter,
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.TenantId, null),
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.IsPlatformDefault, true));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<TenantMessagingSettings?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var filter = Builders<TenantMessagingSettings>.Filter.And(
            ActiveFilter,
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.Id, id),
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.IsPlatformDefault, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<TenantMessagingSettings?> GetPlatformDefaultByIdAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<TenantMessagingSettings>.Filter.And(
            ActiveFilter,
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.Id, id),
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.TenantId, null),
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.IsPlatformDefault, true));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TenantMessagingSettings>> ListTenantSettingsAsync(int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var filter = Builders<TenantMessagingSettings>.Filter.And(
            ActiveFilter,
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.IsPlatformDefault, false));

        return await _collection.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(TenantMessagingSettings settings, CancellationToken ct = default)
    {
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        settings.Version++;

        var filter = Builders<TenantMessagingSettings>.Filter.And(
            ActiveFilter,
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.Id, settings.Id));

        await _collection.ReplaceOneAsync(filter, settings, cancellationToken: ct);
    }

    public async Task SoftDeleteTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<TenantMessagingSettings>.Filter.And(
            ActiveFilter,
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.IsPlatformDefault, false));

        await SoftDeleteAsync(filter, ct);
    }

    public async Task SoftDeletePlatformDefaultAsync(CancellationToken ct = default)
    {
        var filter = Builders<TenantMessagingSettings>.Filter.And(
            ActiveFilter,
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.TenantId, null),
            Builders<TenantMessagingSettings>.Filter.Eq(x => x.IsPlatformDefault, true));

        await SoftDeleteAsync(filter, ct);
    }

    private async Task SoftDeleteAsync(FilterDefinition<TenantMessagingSettings> filter, CancellationToken ct)
    {
        var update = Builders<TenantMessagingSettings>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    private static FilterDefinition<TenantMessagingSettings> ActiveFilter =>
        Builders<TenantMessagingSettings>.Filter.Eq(x => x.IsDeleted, false);
}
