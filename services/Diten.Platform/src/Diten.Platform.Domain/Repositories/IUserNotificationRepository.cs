using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Domain.Repositories;

/// <summary>
/// BL-025 — the in-app inbox, read and written per PERSON.
///
/// <para><b>Every method takes tenant AND user, and neither is optional.</b> There is deliberately no
/// "get by id" that trusts the id alone: an id is guessable, and a notification is the one record where
/// leaking somebody else's row leaks the thing itself rather than a pointer to it. The scope is part of
/// every filter, so a caller cannot forget it — the compiler asks for it.</para>
/// </summary>
public interface IUserNotificationRepository
{
    Task<UserNotification> CreateAsync(UserNotification notification, CancellationToken ct = default);

    /// <summary>
    /// One page of this person's notifications, UNREAD FIRST and newest first within each group.
    ///
    /// <para>The ordering is <c>{ IsRead asc, CreatedAt desc }</c> — false sorts before true, so "unread
    /// first" needs no second query. It is exactly the sort the declared index serves after the tenant/user
    /// equality prefix, so the page comes back as an index walk rather than a blocking sort. The read state
    /// is keyed on <c>IsRead</c> rather than on <c>ReadAt</c> because Mongo cannot sort on two
    /// <c>DateTimeOffset</c> fields while BL-030 is open — see <c>UserNotification</c>.</para>
    /// </summary>
    Task<IReadOnlyList<UserNotification>> ListForUserAsync(
        Guid tenantId,
        Guid userId,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// How many of this person's notifications are unread.
    ///
    /// <para>Counted server-side ON PURPOSE. The alternative — letting the caller count the unread rows in
    /// the page it just fetched — produces a number that means "unread in the first 20", and a badge showing
    /// that is the invented-count defect the bell was stripped of in the first place.</para>
    /// </summary>
    Task<long> CountUnreadForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Mark ONE of this person's notifications read. False when no such row belongs to them — a missing row
    /// and somebody else's row are the same answer, so the endpoint cannot be used to probe for existence.
    /// </summary>
    Task<bool> MarkReadAsync(
        Guid tenantId,
        Guid userId,
        Guid notificationId,
        DateTimeOffset readAt,
        CancellationToken ct = default);

    /// <summary>Mark every unread notification of this person read. Returns how many actually changed.</summary>
    Task<long> MarkAllReadAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset readAt,
        CancellationToken ct = default);
}
