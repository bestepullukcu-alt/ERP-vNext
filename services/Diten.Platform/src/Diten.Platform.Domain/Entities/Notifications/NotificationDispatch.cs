using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Notifications;

public sealed class NotificationDispatch : BaseEntity
{
    public Guid TenantId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public string Locale { get; set; } = "en";
    public NotificationChannelCode Channel { get; set; } = NotificationChannelCode.Email;
    public MessagingProviderCode ProviderCode { get; set; } = MessagingProviderCode.Fake;
    public string? ProviderMessageId { get; set; }
    public NotificationDispatchStatus Status { get; set; } = NotificationDispatchStatus.Queued;
    public List<EmailRecipient> To { get; set; } = [];
    public List<EmailRecipient> Cc { get; set; } = [];
    public List<EmailRecipient> Bcc { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string? BodyHtmlPreview { get; set; }
    public string? BodyTextPreview { get; set; }
    public string VariablesJson { get; set; } = "{}";

    // BL-374 — the SemanticVersion the template carried AT QUEUE TIME. A retry only re-renders the full body
    // from TemplateId + VariablesJson when this still matches the template's CURRENT SemanticVersion; a
    // changed template means the queue-time render is no longer reproducible and the retry must fall back to
    // the persisted preview rather than send content the sender never approved. Absent on documents written
    // before this field existed — a null on both sides compares equal, which is intentionally permissive for
    // templates unversioned at write time, exactly as narrow as that comparison already is.
    public string? TemplateSemanticVersion { get; set; }

    // BL-374 — a calendar attachment has no secret to protect (unlike the body/preview above, which are
    // masked), so it is safe to persist outright for a retry to reuse. Bounded by
    // QueueEmailNotificationHandler's own size gate; absent on documents written before this field existed.
    public List<NotificationDispatchAttachment> Attachments { get; set; } = [];

    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }

    // BL-406 — ADDITIVE ONLY, both null on every row written before this WP and on every non-meeting dispatch
    // afterward. Meeting mail is now dispatched 1:1 (one NotificationDispatch per attendee — see
    // MeetingInviteMailer.DispatchGroupAsync); CausationId carries the MeetingId (its own pre-existing,
    // previously-unused meaning: "the id of the thing that caused this dispatch"), and this field carries WHICH
    // attendee this particular dispatch was for, since a dispatch's own `To` list on its own cannot be resolved
    // back to a MeetingAttendee row (no email is stored on MeetingAttendee). Never set for non-meeting mail.
    public Guid? MeetingAttendeeUserId { get; set; }

    // BL-406 — set exactly once, at the moment this dispatch's failure is first recognised as PERMANENT (no
    // further retry will occur — see EmailDispatchJob/MarkNotificationDispatchFailedHandler). Idempotency guard:
    // a later re-entry over an already-terminal dispatch (e.g. a duplicate Hangfire execution of the same retry
    // job) must not fire the organizer notification / ops counter a second time. Null on every row that has
    // never permanently failed, and on every row written before this WP.
    //
    // BL-454 — two values once permanent: PermanentFailurePending while the permanent-failure effects (counter,
    // organizer notification, attendee badge) have not run yet — written in the SAME write as the transition — then
    // the time they ran. A row left pending (the event publish threw before the effects) is re-driven by the retry
    // sweep. Every query that keeps permanent rows out reads "not null", so pending is already permanent there.
    public DateTimeOffset? PermanentlyFailedNotifiedAt { get; set; }

    /// <summary>BL-454 — <see cref="PermanentlyFailedNotifiedAt"/> of a permanent row whose effects have not run yet.</summary>
    public static readonly DateTimeOffset PermanentFailurePending = DateTimeOffset.MinValue;

    /// <summary>BL-454 — the row is permanent and its permanent-failure effects have not been applied.</summary>
    public static bool IsPermanentFailurePending(NotificationDispatch dispatch) =>
        dispatch.PermanentlyFailedNotifiedAt == PermanentFailurePending;

    public bool TryMarkSent(string? providerMessageId, DateTimeOffset now)
    {
        // A retry sends a row that is already Failed; refusing that transition left an accepted mail Failed and due,
        // so the sweep sent it again every minute.
        if (Status is not (NotificationDispatchStatus.Queued or NotificationDispatchStatus.Failed))
        {
            return false;
        }

        Status = NotificationDispatchStatus.Sent;
        ReleaseVariables();
        ProviderMessageId = providerMessageId;
        SentAt = now;
        UpdatedAt = now;
        Version++;
        return true;
    }

    public bool TryMarkFailed(string errorCode, string errorMessage, DateTimeOffset now, bool isPermanent = false)
    {
        if (Status is NotificationDispatchStatus.Sent or NotificationDispatchStatus.Cancelled)
        {
            return false;
        }

        Status = NotificationDispatchStatus.Failed;
        if (isPermanent)
        {
            ReleaseVariables();
        }

        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        FailedAt = now;
        UpdatedAt = now;
        Version++;
        return true;
    }

    public bool TryCancel(DateTimeOffset now)
    {
        if (Status != NotificationDispatchStatus.Queued)
        {
            return false;
        }

        Status = NotificationDispatchStatus.Cancelled;
        ReleaseVariables();
        UpdatedAt = now;
        Version++;
        return true;
    }

    /// <summary>
    /// BL-454 — the variables are kept for one purpose: a retry that sends exactly what the first attempt sent. Once
    /// no retry can follow (sent, permanently failed, cancelled) they are released, in the same transition; the
    /// subject and the preview stay as the record of what was sent. A row still waiting for a retry keeps them.
    /// </summary>
    public const string ReleasedVariablesJson = "{}";

    /// <summary>
    /// BL-454 — the error code a SENT dispatch carries when a retry could only send the stored, masked preview (a subject
    /// or a link may read <c>[REDACTED]</c>). The monitoring screen shows it next to the status; no field is added.
    /// </summary>
    public const string RetryDegradedErrorCode = "RetryDegraded";

    private void ReleaseVariables() => VariablesJson = ReleasedVariablesJson;
}
