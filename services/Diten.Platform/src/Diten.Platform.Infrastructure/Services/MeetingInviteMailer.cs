using System.Text;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

/// <inheritdoc cref="IMeetingInviteMailer"/>
public sealed class MeetingInviteMailer : IMeetingInviteMailer
{
    /// <summary>Must match the <c>EventCode</c> each entry carries in <c>MeetingManifestProvider</c> and the
    /// <c>TemplateKey</c> each locale is seeded under in <c>NotificationTemplateSeed</c> — the three names are
    /// kept in step by hand, the same way every other <c>platform.*</c> notification event already is.</summary>
    private const string InviteEventCode = "platform.meetings.invite";
    private const string ChangeEventCode = "platform.meetings.change";
    private const string CancelEventCode = "platform.meetings.cancel";

    /// <summary>BL-386 — deliberately its OWN event/template, not a reuse of <see cref="CancelEventCode"/>: the
    /// existing "meeting cancelled" copy links to the meeting (404 for someone no longer on it) and says the
    /// WRONG thing (the meeting itself is not cancelled, only this one person's attendance is).</summary>
    private const string RemovedEventCode = "platform.meetings.removed";

    private readonly INotificationEventDispatchAdapter _notifications;
    private readonly ITaskNotificationRecipientResolver _recipients;
    private readonly ITenantContext _tenantContext;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly ILogger<MeetingInviteMailer> _logger;

    public MeetingInviteMailer(
        INotificationEventDispatchAdapter notifications,
        ITaskNotificationRecipientResolver recipients,
        ITenantContext tenantContext,
        IOptions<AuthServiceOptions> authServiceOptions,
        ILogger<MeetingInviteMailer> logger)
    {
        _notifications = notifications;
        _recipients = recipients;
        _tenantContext = tenantContext;
        _authServiceOptions = authServiceOptions.Value;
        _logger = logger;
    }

    public Task<MeetingInviteDeliveryResult> SendInviteAsync(
        Meeting meeting, string meetingTypeName, IReadOnlyList<MeetingAttendee> recipients, Guid actingUserId, CancellationToken ct = default)
        => SendAsync(InviteEventCode, MeetingIcsEventType.Invite, meeting, meetingTypeName, recipients, actingUserId, ct);

    public Task<MeetingInviteDeliveryResult> SendChangeAsync(
        Meeting meeting, string meetingTypeName, IReadOnlyList<MeetingAttendee> recipients, Guid actingUserId, CancellationToken ct = default)
        => SendAsync(ChangeEventCode, MeetingIcsEventType.Change, meeting, meetingTypeName, recipients, actingUserId, ct);

    public Task<MeetingInviteDeliveryResult> SendCancelAsync(
        Meeting meeting, string meetingTypeName, IReadOnlyList<MeetingAttendee> recipients, Guid actingUserId, CancellationToken ct = default)
        => SendAsync(CancelEventCode, MeetingIcsEventType.Cancel, meeting, meetingTypeName, recipients, actingUserId, ct);

    /// <summary>BL-386 — a single-recipient CANCEL: the removed person's own .ics is withdrawn (same UID, a
    /// higher SEQUENCE — the caller already bumped <see cref="Meeting.Version"/> before calling this), nobody
    /// else's copy is touched. Reusing <see cref="SendAsync"/> unmodified means the self-removal exclusion
    /// (<paramref name="actingUserId"/> filtered out of the audience) applies here exactly as it does to invite/
    /// change/cancel — a person who removes THEMSELVES gets <see cref="MeetingInviteDeliveryResult.NoRecipients"/>,
    /// not a mail about their own action.</summary>
    public Task<MeetingInviteDeliveryResult> SendRemovedAsync(
        Meeting meeting, string meetingTypeName, MeetingAttendee removedAttendee, Guid actingUserId, CancellationToken ct = default)
        => SendAsync(RemovedEventCode, MeetingIcsEventType.Cancel, meeting, meetingTypeName, [removedAttendee], actingUserId, ct);

    /// <summary>
    /// K12 — this method's own try/catch is the whole rule: whatever happens below, the caller (a meeting
    /// create/update/cancel handler already past its own write) gets a RESULT, never an exception. S5b — the
    /// same posture extends to .ics generation: a malformed attendee address or any other failure building the
    /// attachment is caught by this SAME try/catch, never a reason the meeting write itself is threatened.
    /// </summary>
    private async Task<MeetingInviteDeliveryResult> SendAsync(
        string eventCode,
        MeetingIcsEventType icsEventType,
        Meeting meeting,
        string meetingTypeName,
        IReadOnlyList<MeetingAttendee> recipients,
        Guid actingUserId,
        CancellationToken ct)
    {
        try
        {
            var audience = recipients
                .Select(a => a.UserId)
                .Where(id => id != Guid.Empty && id != actingUserId)
                .Distinct()
                .ToList();

            if (audience.Count == 0)
            {
                // Everyone who would be told IS the person who just did it (or there is nobody invited yet).
                return MeetingInviteDeliveryResult.NoRecipients;
            }

            var resolved = await _recipients.ResolveAsync(audience, ct);
            if (resolved.Count == 0)
            {
                _logger.LogWarning(
                    "meeting.invite.no_recipients EventCode={EventCode} MeetingId={MeetingId} Candidates={Candidates}.",
                    eventCode, meeting.Id, audience.Count);
                return new MeetingInviteDeliveryResult(Sent: false, Failed: true, Reason: "NO_RECIPIENTS");
            }

            // The organizer's identity is content, not a recipient decision — resolved on its own so it still
            // appears even when the organizer is excluded from `audience` above (the common case: they are
            // the actor themselves). S5b needs the organizer's own EMAIL too (the .ics ORGANIZER line), not
            // just the display name the mail body already used.
            var organizer = (await _recipients.ResolveAsync([meeting.OrganizerUserId], ct)).FirstOrDefault()
                ?? new TaskNotificationRecipient(meeting.OrganizerUserId, string.Empty, meeting.OrganizerUserId.ToString());
            var organizerName = organizer.DisplayName ?? meeting.OrganizerUserId.ToString();

            // BL-374 (2) — an ORGANIZER-less .ics is worse than none: RFC 5546 requires METHOD:REQUEST to carry
            // an ORGANIZER address, and a blank "mailto:" (the fallback just above) can make Outlook show the
            // whole invite as an unsupported calendar message. The invite BODY still goes either way (K12) —
            // only the calendar attachment is withheld when the organizer's e-mail could not be resolved.
            var icsAttachment = string.IsNullOrWhiteSpace(organizer.Email)
                ? null
                : BuildIcsAttachment(icsEventType, meeting, organizer, resolved);

            if (icsAttachment is null)
            {
                _logger.LogWarning(
                    "meeting.invite.ics_omitted_organizer_email_unresolved EventCode={EventCode} MeetingId={MeetingId} OrganizerUserId={OrganizerUserId}",
                    eventCode, meeting.Id, meeting.OrganizerUserId);
            }

            var response = await _notifications.DispatchByEventCodeAsync(
                new NotificationEventDispatchRequest(
                    TenantId: _tenantContext.TenantId,
                    EventCode: eventCode,
                    To: resolved.Select(r => new EmailRecipientDto(r.Email, r.DisplayName)).ToList(),
                    Variables: BuildVariables(meeting, meetingTypeName, organizerName),
                    // Same posture as TaskNotificationService: the reader's own language is not known here, so
                    // the adapter resolves the TENANT's configured language rather than guessing at the actor's.
                    Locale: null,
                    Attachments: icsAttachment is null ? null : [icsAttachment]),
                ct);

            if (!response.IsSuccessful)
            {
                _logger.LogWarning(
                    "meeting.invite.not_dispatched EventCode={EventCode} MeetingId={MeetingId} ReasonCode={ReasonCode} Reason={Reason}",
                    eventCode, meeting.Id, response.ReasonCode ?? "<none>", string.Join(" | ", response.Errors));
                return new MeetingInviteDeliveryResult(Sent: false, Failed: true, Reason: response.ReasonCode ?? "DISPATCH_FAILED");
            }

            return new MeetingInviteDeliveryResult(Sent: true, Failed: false, Reason: null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The rule the whole feature hangs on: an invite e-mail never fails the write that triggered it.
            _logger.LogWarning(ex, "meeting.invite.threw EventCode={EventCode} MeetingId={MeetingId}", eventCode, meeting.Id);
            return new MeetingInviteDeliveryResult(Sent: false, Failed: true, Reason: "THREW");
        }
    }

    /// <summary>"Toplantıyı aç" — an ABSOLUTE url, deliberately: this reaches a reader OUTSIDE the app (their
    /// mail client), unlike <c>TaskNotificationService.TaskDeepLink</c>'s relative path, which is only ever read
    /// from inside the app that already knows its own origin.</summary>
    private string MeetingDeepLink(Guid meetingId)
        => $"{_authServiceOptions.FrontendBaseUrl.TrimEnd('/')}/Meetings/{meetingId}";

    private Dictionary<string, object?> BuildVariables(Meeting meeting, string meetingTypeName, string organizerName) => new()
    {
        ["MeetingTitle"] = meeting.Title,
        ["MeetingType"] = meetingTypeName,
        ["StartAt"] = meeting.StartAt.ToString("yyyy-MM-dd HH:mm"),
        ["EndAt"] = meeting.EndAt.ToString("yyyy-MM-dd HH:mm"),
        ["Organizer"] = organizerName,
        ["Location"] = meeting.Location ?? string.Empty,
        ["MeetingUrl"] = MeetingDeepLink(meeting.Id)
    };

    /// <summary>S5b — pack's own .ics sözleşme: filename "invite.ics" for all three moments (the METHOD
    /// parameter inside <see cref="MeetingIcsBuilder.ContentType"/> is what distinguishes REQUEST from CANCEL,
    /// not the filename).</summary>
    private static MessagingProviderAttachment BuildIcsAttachment(
        MeetingIcsEventType eventType, Meeting meeting, TaskNotificationRecipient organizer, IReadOnlyList<TaskNotificationRecipient> attendees)
    {
        var ics = MeetingIcsBuilder.Build(meeting, organizer, attendees, eventType);
        return new MessagingProviderAttachment(
            MeetingIcsBuilder.FileName, MeetingIcsBuilder.ContentType(eventType), Encoding.UTF8.GetBytes(ics));
    }
}
