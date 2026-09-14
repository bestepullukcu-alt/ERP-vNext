using Diten.Platform.Domain.Entities.Meetings;

namespace Diten.Platform.Application.Features.Meetings.Services;

/// <summary>
/// MOD-0357 S5 (pack §3 Providers, K12) — wraps <c>INotificationEventDispatchAdapter</c> for the three
/// meeting-lifecycle e-mails. Declared in Application, implemented in Infrastructure (mirrors
/// <c>IMeetingRepository</c>/<c>MeetingRepository</c>): the implementation needs the tenant's PUBLIC web
/// origin to build the "Toplantıyı aç" deep link, and that setting (<c>AuthServiceOptions.FrontendBaseUrl</c>)
/// is an Infrastructure concern <c>ITaskNotificationService</c> never had to reach for — its own deep link stays
/// relative because it is read only from inside the app, never from an e-mail client outside it.
///
/// <para><b>K12 — never throws, never rolls back the write that triggered it.</b> Every method returns a
/// <see cref="MeetingInviteDeliveryResult"/> describing what happened; the caller decides whether that result is
/// worth surfacing (today, only the create path echoes it on <see cref="MeetingDto.InviteDelivery"/> — change and
/// cancel log the same outcome but have no response field of their own to carry it on).</para>
///
/// <para><b>.ics is NOT built here.</b> S5b's own job (pack K5), gated on an additive
/// <c>MessagingProviderEmailRequest.Attachments</c> field this slice does not have.</para>
/// </summary>
public interface IMeetingInviteMailer
{
    /// <summary>Sent to every <paramref name="recipients"/> row except <paramref name="actingUserId"/> — the
    /// actor already knows what they just did (same actor-exclusion rule <c>TaskNotificationService</c> takes).</summary>
    Task<MeetingInviteDeliveryResult> SendInviteAsync(
        Meeting meeting,
        string meetingTypeName,
        IReadOnlyList<MeetingAttendee> recipients,
        Guid actingUserId,
        CancellationToken ct = default);

    /// <summary>StartAt/EndAt/Location changed on an already-scheduled meeting.</summary>
    Task<MeetingInviteDeliveryResult> SendChangeAsync(
        Meeting meeting,
        string meetingTypeName,
        IReadOnlyList<MeetingAttendee> recipients,
        Guid actingUserId,
        CancellationToken ct = default);

    /// <summary>The meeting was cancelled.</summary>
    Task<MeetingInviteDeliveryResult> SendCancelAsync(
        Meeting meeting,
        string meetingTypeName,
        IReadOnlyList<MeetingAttendee> recipients,
        Guid actingUserId,
        CancellationToken ct = default);

    /// <summary>BL-386 — <paramref name="removedAttendee"/> was taken off an otherwise-still-scheduled meeting;
    /// the same actor-exclusion rule every other Send*Async takes means a self-removal (the removed person is
    /// also the actor) sends nothing, for free, with no extra branch here.</summary>
    Task<MeetingInviteDeliveryResult> SendRemovedAsync(
        Meeting meeting,
        string meetingTypeName,
        MeetingAttendee removedAttendee,
        Guid actingUserId,
        CancellationToken ct = default);

    /// <summary>BL-387 — <paramref name="newOrganizerUserId"/> just became this meeting's organizer
    /// (<c>meeting.OrganizerUserId</c> is already the new value by the time this is called) and gets told the
    /// SAME "added to your calendar" mail a freshly-created meeting's own organizer gets, unless they reassigned
    /// the meeting to themselves. Nobody else on the meeting is mailed by a reassignment on its own — the same
    /// posture <c>ReassignMeetingOrganizerHandler</c> already takes toward every OTHER attendee row.</summary>
    Task<MeetingInviteDeliveryResult> SendOrganizerReassignedAsync(
        Meeting meeting,
        string meetingTypeName,
        Guid newOrganizerUserId,
        Guid actingUserId,
        CancellationToken ct = default);
}

/// <summary>K12's own shape — <see cref="Sent"/> and <see cref="Failed"/> are deliberately not each other's
/// negation: an organizer-only meeting has nobody to tell, which is neither a send nor a failure.</summary>
public sealed record MeetingInviteDeliveryResult(bool Sent, bool Failed, string? Reason)
{
    public static readonly MeetingInviteDeliveryResult NoRecipients = new(Sent: false, Failed: false, Reason: null);
}
