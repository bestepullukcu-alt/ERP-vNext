using System.Globalization;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Vars = Diten.Platform.Application.Features.TimeEntry.TimeEntryNotificationVariables;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>The two pages a time-entry e-mail links to (implemented where the web origin setting lives).</summary>
public interface ITimeEntryLinks
{
    /// <summary>The person's own week on My Timesheet.</summary>
    string MyWeek(string weekKey);

    /// <summary>The approver's read-only week.</summary>
    string ApprovalWeek(Guid weekId);
}

/// <summary>
/// MOD-0280-FU01 T3 (pack §21.3 N2, N4, N5) — every time-entry e-mail except the midnight one, which keeps its own
/// notifier and segment mark (N6).
///
/// <para><b>Always the same four steps, in this order:</b> resolve the addresses (AuthService omits a deactivated user,
/// so nobody who left gets anything); claim the (event, key, recipient) mark — BEFORE the send, so a crash loses one
/// e-mail and never duplicates one; dispatch by event code; log a refusal. Nothing here throws into the command that
/// called it: the week has already been written.</para>
///
/// <para><b>Minimal content (N7, D11).</b> A week and a link. An approver's e-mail names the person; none says anything
/// about another person, and none carries minutes per day.</para>
/// </summary>
public interface ITimeEntryNotifier
{
    /// <summary>N4 — to the week's STORED approver candidates (D6), one e-mail each, at most once per (week, revision,
    /// recipient, tenant-local day).</summary>
    Task WeekSubmittedAsync(TimesheetWeek week, CancellationToken ct = default);

    /// <summary>N4 — to the same candidates, at most once per (week, revision, recipient, tenant-local day).</summary>
    Task WeekWithdrawnAsync(TimesheetWeek week, CancellationToken ct = default);

    /// <summary>N4 — to the person, once per revision; called only where the finalizer changed the week's state.</summary>
    Task WeekApprovedAsync(TimesheetWeek week, CancellationToken ct = default);

    /// <summary>N4 — to the person with the approver's reason, once per submission.</summary>
    Task WeekRejectedAsync(TimesheetWeek week, CancellationToken ct = default);

    /// <summary>N2 — "last week is still open", once per person-week. True when it was handed to the dispatch.</summary>
    Task<bool> WeekReminderAsync(Guid userId, DateOnly monday, CancellationToken ct = default);

    /// <summary>
    /// BL-488 — N2 for a GROUP of people: their addresses are resolved in ONE question to AuthService (the caller keeps a
    /// group to <see cref="ReminderGroupSize"/>), then each person is claimed and sent to exactly as
    /// <see cref="WeekReminderAsync"/> does it — the same mark key, the same payload, once per person-week. Somebody
    /// AuthService does not hand out (deactivated, no address) gets nothing and is not claimed. Returns how many
    /// reminders were handed to the dispatch.
    /// </summary>
    Task<int> WeekRemindersAsync(IReadOnlyCollection<Guid> userIds, DateOnly monday, CancellationToken ct = default);

    /// <summary>How many people one recipient resolution covers — AuthService's own cap per request.</summary>
    const int ReminderGroupSize = 100;

    /// <summary>N5 — to the person only, once per (meeting, person, attendance).</summary>
    Task MinutesConflictAsync(
        Guid userId, Guid meetingId, string meetingTitle, DateOnly meetingDate, AttendanceStatus attendance,
        CancellationToken ct = default);
}

public sealed class TimeEntryNotifier : ITimeEntryNotifier
{
    private readonly INotificationEventDispatchAdapter _dispatch;
    private readonly ITaskNotificationRecipientResolver _recipients;
    private readonly ITimeEntryNotificationMarkRepository _marks;
    private readonly IUserDisplayNameResolver _names;
    private readonly ITimeEntryLinks _links;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;
    private readonly ILogger<TimeEntryNotifier> _logger;

    public TimeEntryNotifier(
        INotificationEventDispatchAdapter dispatch,
        ITaskNotificationRecipientResolver recipients,
        ITimeEntryNotificationMarkRepository marks,
        IUserDisplayNameResolver names,
        ITimeEntryLinks links,
        ITenantContext tenantContext,
        TimeProvider clock,
        ILogger<TimeEntryNotifier> logger)
    {
        _dispatch = dispatch;
        _recipients = recipients;
        _marks = marks;
        _names = names;
        _links = links;
        _tenantContext = tenantContext;
        _clock = clock;
        _logger = logger;
    }

    public async Task WeekSubmittedAsync(TimesheetWeek week, CancellationToken ct = default)
    {
        var person = await PersonNameAsync(week.UserId, ct);
        await SendAsync(
            TimeEntryNotificationEvents.WeekSubmitted, ApproverDayKey(week), week.ApproverCandidateUserIds,
            new Dictionary<string, object?>
            {
                [Vars.PersonName] = person,
                [Vars.WeekLabel] = WeekLabel(week.WeekKey, week.WeekStartDate),
                [Vars.TimesheetUrl] = _links.ApprovalWeek(week.Id)
            },
            week.Id, ct);
    }

    public async Task WeekWithdrawnAsync(TimesheetWeek week, CancellationToken ct = default)
    {
        var person = await PersonNameAsync(week.UserId, ct);
        await SendAsync(
            TimeEntryNotificationEvents.WeekWithdrawn, ApproverDayKey(week), week.ApproverCandidateUserIds,
            new Dictionary<string, object?>
            {
                [Vars.PersonName] = person,
                [Vars.WeekLabel] = WeekLabel(week.WeekKey, week.WeekStartDate)
            },
            week.Id, ct);
    }

    public Task WeekApprovedAsync(TimesheetWeek week, CancellationToken ct = default)
        => SendAsync(
            TimeEntryNotificationEvents.WeekApproved, $"{week.Id:N}:r{week.RevisionNumber}", [week.UserId],
            new Dictionary<string, object?>
            {
                [Vars.WeekLabel] = WeekLabel(week.WeekKey, week.WeekStartDate),
                [Vars.TimesheetUrl] = _links.MyWeek(week.WeekKey)
            },
            week.Id, ct);

    public Task WeekRejectedAsync(TimesheetWeek week, CancellationToken ct = default)
        => SendAsync(
            TimeEntryNotificationEvents.WeekRejected, SubmissionKey(week), [week.UserId],
            new Dictionary<string, object?>
            {
                [Vars.WeekLabel] = WeekLabel(week.WeekKey, week.WeekStartDate),
                // MOD-0023 refuses a reject without a comment on this definition (R5); the dash only covers a decision
                // recorded before that option existed, so the required variable is never blank.
                [Vars.Reason] = string.IsNullOrWhiteSpace(week.LastRejectionReason) ? "—" : week.LastRejectionReason.Trim(),
                [Vars.TimesheetUrl] = _links.MyWeek(week.WeekKey)
            },
            week.Id, ct);

    public async Task<bool> WeekReminderAsync(Guid userId, DateOnly monday, CancellationToken ct = default)
        => await WeekRemindersAsync([userId], monday, ct) > 0;

    public Task<int> WeekRemindersAsync(IReadOnlyCollection<Guid> userIds, DateOnly monday, CancellationToken ct = default)
    {
        var weekKey = WeekCalendar.KeyOf(monday);
        return SendAsync(
            TimeEntryNotificationEvents.WeekReminder, recipient => ReminderKey(recipient, weekKey), weekKey, userIds,
            new Dictionary<string, object?>
            {
                [Vars.WeekLabel] = WeekLabel(weekKey, monday),
                [Vars.TimesheetUrl] = _links.MyWeek(weekKey)
            },
            null, ct, eachRecipientOnItsOwn: true);
    }

    public Task MinutesConflictAsync(
        Guid userId, Guid meetingId, string meetingTitle, DateOnly meetingDate, AttendanceStatus attendance,
        CancellationToken ct = default)
        => SendAsync(
            TimeEntryNotificationEvents.MinutesConflict, $"{meetingId:N}:{userId:N}:{attendance}", [userId],
            new Dictionary<string, object?>
            {
                [Vars.MeetingTitle] = meetingTitle,
                [Vars.MeetingDate] = meetingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                [Vars.TimesheetUrl] = _links.MyWeek(WeekCalendar.KeyOf(meetingDate))
            },
            meetingId, ct);

    /// <summary><c>2026-W40 (2026-09-28 – 2026-10-04)</c> — the same in every language, so no locale is guessed here.</summary>
    public static string WeekLabel(string weekKey, DateOnly monday)
        => string.Create(CultureInfo.InvariantCulture, $"{weekKey} ({monday:yyyy-MM-dd} – {monday.AddDays(6):yyyy-MM-dd})");

    /// <summary>One submission of one revision: a rejected week submitted again is a new submission (the rejection key).</summary>
    private static string SubmissionKey(TimesheetWeek week) => $"{week.Id:N}:r{week.RevisionNumber}:s{week.SubmissionCount}";

    /// <summary>
    /// CT acceptance round 1 (M3): submitted and withdrawn go to an approver at most once per (week, revision, recipient,
    /// tenant-local day). A submit/withdraw loop must not fill the approvers' inbox; the approval item stays visible in
    /// the Task Center whatever the e-mails say. The day is the week's own zone (R3) — the one it was written in.
    /// </summary>
    private string ApproverDayKey(TimesheetWeek week)
        => string.Create(CultureInfo.InvariantCulture,
            $"{week.Id:N}:r{week.RevisionNumber}:d{WeekCalendar.LocalDateOf(_clock.GetUtcNow(), Zone(week.TimeZoneId)):yyyyMMdd}");

    private static TimeZoneInfo Zone(string? id)
        => !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : TimeZoneInfo.Utc;

    /// <summary>The (person, week) part of a reminder's mark key.</summary>
    public static string ReminderKey(Guid userId, string weekKey) => $"{userId:N}:{weekKey}";

    /// <summary>The mark key a send claims for one recipient — the ONLY place its shape is written, so a reader that asks
    /// "was this already sent?" (the reminder job, M1) asks with exactly the key the send claimed.</summary>
    public static string MarkKey(string key, Guid recipientUserId) => $"{key}:{recipientUserId:N}";

    private async Task<string> PersonNameAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var names = await _names.ResolveAsync([userId], ct);
            if (names.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogInformation(ex, "time-entry.notify: the person's name could not be read; a neutral one is used.");
        }

        // Never the user id in the name's place.
        return "—";
    }

    /// <summary>The four steps, for one key shared by every recipient.</summary>
    private Task<int> SendAsync(
        string eventCode, string key, IReadOnlyCollection<Guid> userIds,
        IReadOnlyDictionary<string, object?> variables, Guid? causationId, CancellationToken ct)
        => SendAsync(eventCode, _ => key, key, userIds, variables, causationId, ct);

    /// <summary>The four steps. <paramref name="keyOf"/> gives each recipient the key their mark is claimed under;
    /// <paramref name="subject"/> is what the send is ABOUT (the shared key, or the week of a group of reminders) — it
    /// names the log line of a failure that happens before any recipient is reached.
    /// <paramref name="eachRecipientOnItsOwn"/>: the recipients are unrelated people (a group of reminders), so one
    /// person's failed send must not cost the others theirs — exactly as when each was sent on their own.
    /// Returns how many e-mails were handed to the dispatch.</summary>
    private async Task<int> SendAsync(
        string eventCode, Func<Guid, string> keyOf, string subject, IReadOnlyCollection<Guid> userIds,
        IReadOnlyDictionary<string, object?> variables, Guid? causationId, CancellationToken ct,
        bool eachRecipientOnItsOwn = false)
    {
        var sent = 0;
        var key = subject;
        try
        {
            var wanted = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (wanted.Count == 0)
            {
                _logger.LogInformation("{EventCode} {Key}: nobody to notify.", eventCode, key);
                return 0;
            }

            // ONLY the people AuthService hands out are ever claimed or sent to: a deactivated user is not in its answer.
            var recipients = await _recipients.ResolveAsync(wanted, ct);
            foreach (var recipient in recipients)
            {
                key = keyOf(recipient.UserId);
                try
                {
                    if (!await _marks.TryClaimAsync(eventCode, MarkKey(key, recipient.UserId), _clock.GetUtcNow(), ct))
                    {
                        continue;
                    }

                    var result = await _dispatch.DispatchByEventCodeAsync(new NotificationEventDispatchRequest(
                        _tenantContext.TenantId,
                        eventCode,
                        [new EmailRecipientDto(recipient.Email, recipient.DisplayName)],
                        variables,
                        CausationId: causationId), ct);
                    sent++;
                    if (!result.IsSuccessful)
                    {
                        _logger.LogInformation("{EventCode} {Key} not sent to {UserId}: {Reason}.",
                            eventCode, key, recipient.UserId, result.ReasonCode);
                    }
                }
                catch (Exception ex) when (eachRecipientOnItsOwn && ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "{EventCode} {Key} failed; the command it follows stands.", eventCode, key);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "{EventCode} {Key} failed; the command it follows stands.", eventCode, key);
        }

        return sent;
    }
}
