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
    /// <summary>N4 — to the week's STORED approver candidates (D6), one e-mail each, once per submission.</summary>
    Task WeekSubmittedAsync(TimesheetWeek week, CancellationToken ct = default);

    /// <summary>N4 — to the same candidates, once per submission the person took back.</summary>
    Task WeekWithdrawnAsync(TimesheetWeek week, CancellationToken ct = default);

    /// <summary>N4 — to the person, once per revision; called only where the finalizer changed the week's state.</summary>
    Task WeekApprovedAsync(TimesheetWeek week, CancellationToken ct = default);

    /// <summary>N4 — to the person with the approver's reason, once per submission.</summary>
    Task WeekRejectedAsync(TimesheetWeek week, CancellationToken ct = default);

    /// <summary>N2 — "last week is still open", once per person-week. True when it was handed to the dispatch.</summary>
    Task<bool> WeekReminderAsync(Guid userId, DateOnly monday, CancellationToken ct = default);

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
            TimeEntryNotificationEvents.WeekSubmitted, SubmissionKey(week), week.ApproverCandidateUserIds,
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
            TimeEntryNotificationEvents.WeekWithdrawn, SubmissionKey(week), week.ApproverCandidateUserIds,
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
    {
        var weekKey = WeekCalendar.KeyOf(monday);
        return await SendAsync(
            TimeEntryNotificationEvents.WeekReminder, $"{userId:N}:{weekKey}", [userId],
            new Dictionary<string, object?>
            {
                [Vars.WeekLabel] = WeekLabel(weekKey, monday),
                [Vars.TimesheetUrl] = _links.MyWeek(weekKey)
            },
            null, ct) > 0;
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

    /// <summary>One submission of one revision: a withdrawn or rejected week submitted again is a new submission.</summary>
    private static string SubmissionKey(TimesheetWeek week) => $"{week.Id:N}:r{week.RevisionNumber}:s{week.SubmissionCount}";

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

    /// <summary>The four steps. Returns how many e-mails were handed to the dispatch.</summary>
    private async Task<int> SendAsync(
        string eventCode, string key, IReadOnlyCollection<Guid> userIds,
        IReadOnlyDictionary<string, object?> variables, Guid? causationId, CancellationToken ct)
    {
        var sent = 0;
        try
        {
            var wanted = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (wanted.Count == 0)
            {
                _logger.LogInformation("{EventCode} {Key}: nobody to notify.", eventCode, key);
                return 0;
            }

            var recipients = await _recipients.ResolveAsync(wanted, ct);
            foreach (var recipient in recipients)
            {
                if (!await _marks.TryClaimAsync(eventCode, $"{key}:{recipient.UserId:N}", _clock.GetUtcNow(), ct))
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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "{EventCode} {Key} failed; the command it follows stands.", eventCode, key);
        }

        return sent;
    }
}
