using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Notifications;

/// <summary>
/// BL-025 — ONE notification, for ONE person, with its own read state.
///
/// <para><b>Why this is a new record and not a column on <see cref="NotificationDispatch"/>.</b> A dispatch is
/// MESSAGE-shaped: one row per message, carrying <c>List&lt;EmailRecipient&gt;</c>, and an
/// <see cref="EmailRecipient"/> is <c>{ Email, DisplayName }</c> — no user id anywhere on the record. That shape
/// is right for the question the platform dispatch-monitoring screen asks ("what did we try to send, to whom,
/// did it go out?"). It cannot answer "what have I not read yet?": there is no id to filter on, no per-person
/// row to mark, and adding a <c>UserId</c> to a row that already fans out to many recipients would make the
/// monitoring screen's counts wrong. So the in-app channel gets its own person-shaped record, and the dispatch
/// record is left exactly as it is.</para>
///
/// <para><b>The user id was already there and was being dropped.</b> The task notification path resolves
/// <c>TaskNotificationRecipient(UserId, Email, DisplayName)</c> and then projects it to
/// <c>EmailRecipientDto(Email, DisplayName)</c> — the id falls on the floor one line before the send. This
/// record is where it lands instead.</para>
///
/// <para><b>Read state is a timestamp PLUS a boolean, and the boolean is not redundant.</b> The design that
/// wants to exist here is <c>ReadAt == null means unread</c> — one field, nothing to disagree. It cannot be
/// sorted on. BL-030 is open, so no <c>DateTimeOffsetSerializer</c> is registered and the driver stores every
/// <c>DateTimeOffset</c> as a BSON ARRAY <c>[ticks, offsetMinutes]</c>. Two array fields in one sort make
/// MongoDB answer "cannot sort with keys that are parallel arrays", and two array fields in one compound
/// index make it answer "cannot index parallel arrays". So a query ordered by
/// <c>{ReadAt, CreatedAt}</c> — the natural "unread first, newest first" — throws at runtime and its index
/// never builds, while every fake-repository test stays green.
///
/// <para><see cref="IsRead"/> therefore exists as a SORT KEY, not as a second opinion:
/// <see cref="ReadAt"/> remains the fact ("when did they read it"), and the boolean is set in the same
/// operation, never independently. <c>UserNotificationTests</c> pins that the two can never disagree. Both
/// collapse back into the single nullable timestamp the day BL-030 lands.</para>
///
/// <para>Marking read is idempotent — see <see cref="TryMarkRead"/>.</para>
/// </summary>
public sealed class UserNotification : BaseEntity
{
    /// <summary>Tenant scope. First key of the read index — no query ever crosses a tenant.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// WHOSE notification this is. Never accepted from a request: the write path takes it from the resolved
    /// recipient, and the read path takes it from the caller's token.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The stable event code that produced this row (e.g. <c>platform.tasks.assigned</c>) — the same code the
    /// e-mail channel dispatches on.
    ///
    /// <para>It is carried rather than a rendered sentence because the reader's language is not known at write
    /// time, and a row written in one language cannot be re-read in another. A surface that wants a localized
    /// label resolves it from this code, the way the tenant navigation resolves names from module codes.</para>
    /// </summary>
    public string EventCode { get; set; } = string.Empty;

    /// <summary>
    /// The subject of the notification in the words of the record it is about — a task's own title, not a
    /// composed sentence. Data the tenant typed, so it needs no translation and invents no copy.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional longer text. Null when the event code and the title already say everything.</summary>
    public string? Body { get; set; }

    /// <summary>Where the notification points. Null when the source has no addressable surface.</summary>
    public string? TargetUrl { get; set; }

    /// <summary>How loudly to present it. Defaults to <see cref="UserNotificationSeverity.Info"/>.</summary>
    public UserNotificationSeverity Severity { get; set; } = UserNotificationSeverity.Info;

    /// <summary>
    /// When the reader read it; null means unread. <see cref="BaseEntity.CreatedAt"/> is when it arrived.
    /// THE FACT — <see cref="IsRead"/> mirrors it so that Mongo can sort and index on the read state.
    /// </summary>
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>
    /// <c>ReadAt is not null</c>, materialised so it can be a sort and index key. Never set on its own — see
    /// the class summary for why BL-030 forces this field to exist at all.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Mark this row read, once. Returns false when it was ALREADY read, so a repeat call cannot rewrite the
    /// timestamp — "read at 09:00" must not become "read at 14:00" because a list was refreshed.
    /// </summary>
    public bool TryMarkRead(DateTimeOffset now)
    {
        if (ReadAt is not null)
        {
            return false;
        }

        ReadAt = now;
        IsRead = true;      // Together with ReadAt, always. The two are one fact in two shapes.
        UpdatedAt = now;
        Version++;
        return true;
    }
}
