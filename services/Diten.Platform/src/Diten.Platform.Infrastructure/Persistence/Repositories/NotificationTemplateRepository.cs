using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly IMongoCollection<NotificationTemplate> _collection;

    public NotificationTemplateRepository(IPlatformDbContext dbContext)
    {
        _collection = dbContext.GetCollection<NotificationTemplate>("notification_templates");
    }

    public async Task<NotificationTemplate> CreateAsync(NotificationTemplate template, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(template, cancellationToken: ct);
        return template;
    }

    public async Task<NotificationTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<NotificationTemplate>.Filter.And(
            ActiveFilter,
            Builders<NotificationTemplate>.Filter.Eq(x => x.Id, id));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<NotificationTemplate?> GetActiveByKeyAsync(
        Guid? tenantId,
        bool isPlatformDefault,
        string templateKey,
        string locale,
        NotificationChannelCode channel,
        CancellationToken ct = default)
    {
        var filter = ScopeFilter(tenantId, isPlatformDefault, templateKey, locale, channel);
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<NotificationTemplate?> GetBestActiveByKeyAsync(
        Guid tenantId,
        string templateKey,
        string locale,
        NotificationChannelCode channel,
        CancellationToken ct = default)
    {
        var tenantTemplate = await GetActiveByKeyAsync(tenantId, false, templateKey, locale, channel, ct);
        if (tenantTemplate is not null)
        {
            return tenantTemplate;
        }

        var platformTemplate = await GetActiveByKeyAsync(null, true, templateKey, locale, channel, ct);
        if (platformTemplate is not null)
        {
            return platformTemplate;
        }

        var neutralLocale = NeutralLocale(locale);
        if (neutralLocale is null)
        {
            return null;
        }

        tenantTemplate = await GetActiveByKeyAsync(tenantId, false, templateKey, neutralLocale, channel, ct);
        if (tenantTemplate is not null)
        {
            return tenantTemplate;
        }

        return await GetActiveByKeyAsync(null, true, templateKey, neutralLocale, channel, ct);
    }

    public async Task<bool> ActiveTemplateExistsAsync(
        Guid? tenantId,
        bool isPlatformDefault,
        string templateKey,
        string locale,
        NotificationChannelCode channel,
        Guid? excludeId = null,
        CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<NotificationTemplate>>
        {
            ScopeFilter(tenantId, isPlatformDefault, templateKey, locale, channel)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<NotificationTemplate>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return await _collection.Find(Builders<NotificationTemplate>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default)
    {
        template.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<NotificationTemplate>.Filter.And(
            ActiveFilter,
            Builders<NotificationTemplate>.Filter.Eq(x => x.Id, template.Id));

        await _collection.ReplaceOneAsync(filter, template, cancellationToken: ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<NotificationTemplate>.Filter.And(
            ActiveFilter,
            Builders<NotificationTemplate>.Filter.Eq(x => x.Id, id));

        var update = Builders<NotificationTemplate>.Update
            .Set(x => x.Status, NotificationTemplateStatus.Archived)
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    private static FilterDefinition<NotificationTemplate> ScopeFilter(
        Guid? tenantId,
        bool isPlatformDefault,
        string templateKey,
        string locale,
        NotificationChannelCode channel)
    {
        return Builders<NotificationTemplate>.Filter.And(
            ActiveFilter,
            Builders<NotificationTemplate>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<NotificationTemplate>.Filter.Eq(x => x.IsPlatformDefault, isPlatformDefault),
            Builders<NotificationTemplate>.Filter.Eq(x => x.TemplateKey, templateKey),
            Builders<NotificationTemplate>.Filter.Eq(x => x.Locale, locale),
            Builders<NotificationTemplate>.Filter.Eq(x => x.Channel, channel),
            Builders<NotificationTemplate>.Filter.Eq(x => x.Status, NotificationTemplateStatus.Active));
    }

    private static FilterDefinition<NotificationTemplate> ActiveFilter =>
        Builders<NotificationTemplate>.Filter.Eq(x => x.IsDeleted, false);

    private static string? NeutralLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var normalized = locale.Trim().ToLowerInvariant();
        var separatorIndex = normalized.IndexOf('-');
        return separatorIndex <= 0 ? null : normalized[..separatorIndex];
    }
}
