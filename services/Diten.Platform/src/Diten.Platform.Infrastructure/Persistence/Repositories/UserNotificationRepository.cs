using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

/// <summary>
/// BL-025 — the in-app inbox against Mongo. Sibling of <see cref="NotificationDispatchRepository"/>, and
/// deliberately a SEPARATE collection: see <see cref="UserNotification"/> for why the dispatch record cannot
/// answer "what have I not read yet?".
/// </summary>
public sealed class UserNotificationRepository : IUserNotificationRepository
{
    private const int MaxPageSize = 100;

    private readonly IMongoCollection<UserNotification> _collection;

    public UserNotificationRepository(IPlatformDbContext dbContext)
    {
        _collection = dbContext.GetCollection<UserNotification>(PlatformCollections.UserNotifications);
    }

    public async Task<UserNotification> CreateAsync(UserNotification notification, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(notification, cancellationToken: ct);
        return notification;
    }

    public async Task<IReadOnlyList<UserNotification>> ListForUserAsync(
        Guid tenantId,
        Guid userId,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        if (take <= 0)
        {
            return [];
        }

        return await _collection
            .Find(OwnedBy(tenantId, userId))
            /*
             * The declared index's sort order, exactly: unread (IsRead false) first, newest first inside each
             * group. Reversing either direction turns this back into a blocking in-memory sort.
             *
             * ⚠ IsRead RATHER THAN ReadAt, BECAUSE MONGO CANNOT SORT ON TWO DateTimeOffset KEYS while BL-030
             * is open — both are stored as BSON arrays and the server answers "cannot sort with keys that are
             * parallel arrays". See UserNotification for the whole reason the boolean exists.
             */
            .SortBy(x => x.IsRead)
            .ThenByDescending(x => x.CreatedAt)
            .Skip(Math.Max(0, skip))
            .Limit(Math.Min(take, MaxPageSize))
            .ToListAsync(ct);
    }

    public async Task<long> CountUnreadForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _collection.CountDocumentsAsync(
            Builders<UserNotification>.Filter.And(
                OwnedBy(tenantId, userId),
                Unread),
            cancellationToken: ct);

    public async Task<bool> MarkReadAsync(
        Guid tenantId,
        Guid userId,
        Guid notificationId,
        DateTimeOffset readAt,
        CancellationToken ct = default)
    {
        /*
         * ⚠ ONE round trip, and the scope is INSIDE it. Read-then-write would leave a window in which the
         * ownership that was checked is not the ownership that is written, and it would need the row back
         * only to throw it away. The filter carries tenant, user AND "still unread", so a second press
         * cannot rewrite the timestamp — the same idempotence UserNotification.TryMarkRead states.
         */
        var result = await _collection.UpdateOneAsync(
            Builders<UserNotification>.Filter.And(
                OwnedBy(tenantId, userId),
                Builders<UserNotification>.Filter.Eq(x => x.Id, notificationId),
                Unread),
            MarkedRead(readAt),
            cancellationToken: ct);

        return result.ModifiedCount > 0;
    }

    public async Task<long> MarkAllReadAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset readAt,
        CancellationToken ct = default)
    {
        var result = await _collection.UpdateManyAsync(
            Builders<UserNotification>.Filter.And(
                OwnedBy(tenantId, userId),
                Unread),
            MarkedRead(readAt),
            cancellationToken: ct);

        return result.ModifiedCount;
    }

    /// <summary>
    /// Unread, expressed on the SORT KEY rather than on the timestamp — so the filter, the sort and the index
    /// all agree on one field. <c>ReadAt</c> stays the fact; see <see cref="UserNotification"/>.
    /// </summary>
    private static FilterDefinition<UserNotification> Unread =>
        Builders<UserNotification>.Filter.Eq(x => x.IsRead, false);

    /// <summary>
    /// The read stamp, written as ONE update so <c>ReadAt</c> and <c>IsRead</c> can never land apart. Every
    /// mark-read path in this repository goes through it — that is the whole guarantee.
    /// </summary>
    private static UpdateDefinition<UserNotification> MarkedRead(DateTimeOffset readAt) =>
        Builders<UserNotification>.Update
            .Set(x => x.ReadAt, readAt)
            .Set(x => x.IsRead, true)
            .Set(x => x.UpdatedAt, readAt)
            .Inc(x => x.Version, 1);

    /// <summary>
    /// The scope EVERY query in this repository starts from. Tenant, person, and not soft-deleted — written
    /// once so no method can be written without it.
    /// </summary>
    private static FilterDefinition<UserNotification> OwnedBy(Guid tenantId, Guid userId) =>
        Builders<UserNotification>.Filter.And(
            Builders<UserNotification>.Filter.Eq(x => x.IsDeleted, false),
            Builders<UserNotification>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<UserNotification>.Filter.Eq(x => x.UserId, userId));
}
