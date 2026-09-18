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

    /// <summary>
    /// BL-387/BL-373 (owner, 2026-09-14) — the ORGANIZER's own variant of invite/change/cancel, used whenever the
    /// organizer did not perform the action themselves (a series sweep with no actor, a delegate creating/editing
    /// on their behalf, or a reassignment). The .ics is IDENTICAL to what every other recipient gets — same UID,
    /// same METHOD, same SEQUENCE — only the wording differs ("added to your calendar", not "you're invited"):
    /// the .ics is the organizer's only path onto their own calendar (no Google/Outlook integration exists), so
    /// leaving them out entirely (the rejected BL-387 patch, <c>.git/BL-387-organizer-exclusion.patch</c>) would
    /// silently drop their own meeting off it. Three keys, not one key + a conditional variable: confirmed against
    /// <c>EmailTemplateRenderer</c> that the template engine is plain <c>{{token}}</c> substitution with no
    /// conditional syntax, so a single shared key would need the localized sentence chosen in C# — exactly the
    /// hardcoded-text failure mode the seed's own per-locale <c>Create(...)</c> calls exist to avoid.
    /// </summary>
    private const string OrganizerAddedEventCode = "platform.meetings.organizer-added";
    private const string OrganizerChangedEventCode = "platform.meetings.organizer-updated";
    private const string OrganizerCancelledEventCode = "platform.meetings.organizer-cancelled";

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
        => SendAsync(InviteEventCode, OrganizerAddedEventCode, MeetingIcsEventType.Invite, meeting, meetingTypeName, recipients, actingUserId, ct);

    public Task<MeetingInviteDeliveryResult> SendChangeAsync(
        Meeting meeting, string meetingTypeName, IReadOnlyList<MeetingAttendee> recipients, Guid actingUserId, CancellationToken ct = default)
        => SendAsync(ChangeEventCode, OrganizerChangedEventCode, MeetingIcsEventType.Change, meeting, meetingTypeName, recipients, actingUserId, ct);

    public Task<MeetingInviteDeliveryResult> SendCancelAsync(
        Meeting meeting, string meetingTypeName, IReadOnlyList<MeetingAttendee> recipients, Guid actingUserId, CancellationToken ct = default)
        => SendAsync(CancelEventCode, OrganizerCancelledEventCode, MeetingIcsEventType.Cancel, meeting, meetingTypeName, recipients, actingUserId, ct);

    /// <summary>BL-386 — a single-recipient CANCEL: the removed person's own .ics is withdrawn (same UID, a
    /// higher SEQUENCE — the caller already bumped <see cref="Meeting.Version"/> before calling this), nobody
    /// else's copy is touched. Reusing <see cref="SendAsync"/> unmodified means the self-removal exclusion
    /// (<paramref name="actingUserId"/> filtered out of the audience) applies here exactly as it does to invite/
    /// change/cancel — a person who removes THEMSELVES gets <see cref="MeetingInviteDeliveryResult.NoRecipients"/>,
    /// not a mail about their own action. The organizer-variant event code passed here is unreachable in practice
    /// (BL-386's own guard means the organizer's row can never be the one being removed) — <see cref="CancelEventCode"/>
    /// stands in rather than <see cref="RemovedEventCode"/> having no organizer sibling of its own to name.</summary>
    public Task<MeetingInviteDeliveryResult> SendRemovedAsync(
        Meeting meeting, string meetingTypeName, MeetingAttendee removedAttendee, Guid actingUserId, CancellationToken ct = default)
        => SendAsync(RemovedEventCode, CancelEventCode, MeetingIcsEventType.Cancel, meeting, meetingTypeName, [removedAttendee], actingUserId, ct);

    /// <summary>
    /// BL-387 — the ONLY moment a reassignment mails anyone: the new organizer, and only when they did not just
    /// reassign themselves. Deliberately its own entry point rather than a third parameter threaded through
    /// <see cref="SendInviteAsync"/>'s callers: <c>ReassignMeetingOrganizerHandler</c> never touches a persisted
    /// attendee row (YAPMA — attendee rows are this WP's own read-only ground), so the recipient handed to
    /// <see cref="SendAsync"/> is a TRANSIENT <see cref="MeetingAttendee"/> built by the caller, never inserted
    /// anywhere. Reusing the invite pathway with a single-row, organizer-only recipient list means the existing
    /// organizer/other split inside <see cref="SendAsync"/> does the actual work unmodified: the new organizer's
    /// row IS <c>meeting.OrganizerUserId</c> by the time this runs, so it always lands in the organizer group,
    /// never the plain "you're invited" one, with no branch written here to make that true.</summary>
    public Task<MeetingInviteDeliveryResult> SendOrganizerReassignedAsync(
        Meeting meeting, string meetingTypeName, Guid newOrganizerUserId, Guid actingUserId, CancellationToken ct = default)
        => SendAsync(
            InviteEventCode, OrganizerAddedEventCode, MeetingIcsEventType.Invite, meeting, meetingTypeName,
            [new MeetingAttendee { TenantId = meeting.TenantId, MeetingId = meeting.Id, UserId = newOrganizerUserId }],
            actingUserId, ct);

    /// <summary>
    /// K12 — no path through here may throw past this method: setup (recipient/organizer resolution, .ics
    /// build) is guarded by ITS OWN try/catch below, and each of the (up to) two dispatch calls this method can
    /// make is guarded by <see cref="DispatchGroupAsync"/>'s own try/catch — one group's exception must not cost
    /// the OTHER group its mail.
    ///
    /// <para><b>BL-387 — the organizer/others split.</b> <paramref name="recipients"/> is resolved once, then
    /// partitioned: whichever resolved row's <c>UserId</c> equals <c>meeting.OrganizerUserId</c> (if any — most
    /// often there is none, because the organizer IS the actor and was already excluded above) is dispatched
    /// ALONE under <paramref name="organizerEventCode"/>; everyone else goes out together under
    /// <paramref name="eventCode"/>, unchanged from before this WP. Both groups, when both are non-empty, get the
    /// SAME <c>.ics</c> bytes — same UID, METHOD and SEQUENCE — only the template differs.</para>
    /// </summary>
    private async Task<MeetingInviteDeliveryResult> SendAsync(
        string eventCode,
        string organizerEventCode,
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

            // BL-387 — split BEFORE dispatch, never after: the organizer reads a different first sentence than
            // everyone else, and the template engine (EmailTemplateRenderer) has no conditional syntax to pick
            // one sentence over another inside a single rendered body.
            var organizerRow = resolved.FirstOrDefault(r => r.UserId == meeting.OrganizerUserId);
            var otherRows = organizerRow is null
                ? resolved
                : resolved.Where(r => r.UserId != meeting.OrganizerUserId).ToList();

            var variables = BuildVariables(meeting, meetingTypeName, organizerName);

            var othersResult = otherRows.Count > 0
                ? await DispatchGroupAsync(eventCode, meeting.Id, otherRows, variables, icsAttachment, ct)
                : null;
            var organizerResult = organizerRow is not null
                ? await DispatchGroupAsync(organizerEventCode, meeting.Id, [organizerRow], variables, icsAttachment, ct)
                : null;

            return Combine(othersResult, organizerResult);
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

    /// <summary>One recipient GROUP: dispatched as one call PER RECIPIENT (BL-406), not one call carrying the
    /// whole group's <c>To[]</c> — so a permanently-failed send can be attributed back to the single attendee it
    /// was for (see <see cref="NotificationEventDispatchRequest.MeetingAttendeeUserId"/>). Same total mail sent
    /// either way; only the provider call shape changes. Each recipient's own try/catch, so one attendee's
    /// throw/failure never costs another attendee in the same group their mail.</summary>
    private async Task<MeetingInviteDeliveryResult> DispatchGroupAsync(
        string eventCode,
        Guid meetingId,
        IReadOnlyList<TaskNotificationRecipient> group,
        IReadOnlyDictionary<string, object?> variables,
        MessagingProviderAttachment? icsAttachment,
        CancellationToken ct)
    {
        MeetingInviteDeliveryResult? combined = null;
        foreach (var recipient in group)
        {
            var result = await DispatchOneAsync(eventCode, meetingId, recipient, variables, icsAttachment, ct);
            combined = Combine(combined, result);
        }

        return combined ?? MeetingInviteDeliveryResult.NoRecipients;
    }

    /// <summary>The actual single-recipient dispatch call — BL-406's own attribution unit. Its own try/catch,
    /// same posture as <see cref="SendAsync"/>'s own outer try/catch, one level down.</summary>
    private async Task<MeetingInviteDeliveryResult> DispatchOneAsync(
        string eventCode,
        Guid meetingId,
        TaskNotificationRecipient recipient,
        IReadOnlyDictionary<string, object?> variables,
        MessagingProviderAttachment? icsAttachment,
        CancellationToken ct)
    {
        try
        {
            var response = await _notifications.DispatchByEventCodeAsync(
                new NotificationEventDispatchRequest(
                    TenantId: _tenantContext.TenantId,
                    EventCode: eventCode,
                    To: [new EmailRecipientDto(recipient.Email, recipient.DisplayName)],
                    Variables: variables,
                    // Same posture as TaskNotificationService: the reader's own language is not known here, so
                    // the adapter resolves the TENANT's configured language rather than guessing at the actor's.
                    Locale: null,
                    Attachments: icsAttachment is null ? null : [icsAttachment],
                    // BL-406 — the meeting a permanently-failed send about this recipient must be attributed to,
                    // and which attendee that send was for. CausationId had no prior meaning for these dispatches
                    // (never set before this WP); this is the first and only producer that populates it.
                    CausationId: meetingId,
                    MeetingAttendeeUserId: recipient.UserId),
                ct);

            if (!response.IsSuccessful)
            {
                _logger.LogWarning(
                    "meeting.invite.not_dispatched EventCode={EventCode} MeetingId={MeetingId} ReasonCode={ReasonCode} Reason={Reason}",
                    eventCode, meetingId, response.ReasonCode ?? "<none>", string.Join(" | ", response.Errors));
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
            _logger.LogWarning(ex, "meeting.invite.threw EventCode={EventCode} MeetingId={MeetingId}", eventCode, meetingId);
            return new MeetingInviteDeliveryResult(Sent: false, Failed: true, Reason: "THREW");
        }
    }

    private static MeetingInviteDeliveryResult Combine(MeetingInviteDeliveryResult? a, MeetingInviteDeliveryResult? b)
    {
        if (a is null)
        {
            return b ?? MeetingInviteDeliveryResult.NoRecipients;
        }

        if (b is null)
        {
            return a;
        }

        return new MeetingInviteDeliveryResult(Sent: a.Sent || b.Sent, Failed: a.Failed || b.Failed, Reason: a.Reason ?? b.Reason);
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
