using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;

/// <summary>
/// MOD-0280-FU01 T3 (pack §21.3 N1–N3) — "last week's timesheet is still open". Hourly; in each tenant whose time admin
/// switched <c>WeeklyReminderEnabled</c> on, and only when it is Monday 09:00 or later in the tenant's own zone (R3), each
/// participant whose previous ISO week is absent, Draft (which is also what a rejected or withdrawn week goes back to)
/// gets ONE reminder. Submitted, Approved and Superseded weeks get none; so does a week whose every day is a weekend or a
/// holiday for the person (an unresolved calendar day counts as a working day — the grid's rule).
///
/// <para><b>Once per person-week</b> whatever the scheduler does: the notifier claims the (person, week) mark before the
/// send, so a second run in the same hour — or on another node — finds it taken.</para>
///
/// <para><b>Participant</b> (N1): anyone with a timesheet week (any status) or a timer segment in the target week or the
/// four ISO weeks before it. Someone who never used the module is never reminded; a deactivated user is never reminded
/// because AuthService does not hand out their address (the resolver omits them, and nothing is sent).</para>
///
/// <para>Same shape as the other time-entry jobs: every active tenant inside <c>TenantScope.Begin</c>, one tenant's or one
/// person's failure logged and never stopping the rest. Off unless <c>BackgroundJobs:RegisterStandardJobs</c> AND
/// <c>EnabledJobs["Diten.Platform.MOD-0280.TimesheetReminderJob"]</c> are both on — and then still off per tenant until
/// the tenant switch says otherwise.</para>
/// </summary>
public sealed class TimesheetReminderJob : IBackgroundJobHandler<TimesheetReminderJobArgs>
{
    private const int DefaultMaxPeoplePerTenant = 2000;

    /// <summary>N1 — how many ISO weeks before the target week still make someone a participant.</summary>
    public const int ParticipationWeeks = 4;

    /// <summary>The job's EnabledJobs configuration key — also what the settings page reports on (M4).</summary>
    public const string JobId = "Diten.Platform.MOD-0280.TimesheetReminderJob";

    /// <summary>M4 — does THIS server run the reminder at all? The scheduler is on, the standard jobs are registered and
    /// the job's own EnabledJobs flag is on — the same three gates <c>PlatformRecurringJobRegistrar</c> and the scheduler
    /// apply. The settings page warns when the tenant switch is on and this is false.</summary>
    public static bool IsScheduled(BackgroundJobSchedulerOptions options)
        => options.Enabled
           && options.RegisterStandardJobs
           && options.EnabledJobs.TryGetValue(JobId, out var enabled)
           && enabled;

    /// <summary>N2 — tenant-local hour on Monday from which last week is reminded.</summary>
    public static readonly TimeSpan ReminderTimeOfDay = TimeSpan.FromHours(9);

    private readonly ITenantRegistryRepository _tenantRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly ITimeEntrySettingsRepository _settings;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimerSegmentRepository _segments;
    private readonly IWorkingHoursProvider _workingHours;
    private readonly ITimeEntryNotifier _notifier;
    private readonly ITimeEntryNotificationMarkRepository _marks;
    private readonly ITimesheetDecisionPuller _puller;
    private readonly TimeProvider _clock;
    private readonly ILogger<TimesheetReminderJob> _logger;

    public TimesheetReminderJob(
        ITenantRegistryRepository tenantRegistry,
        ITenantContext tenantContext,
        ITimeEntrySettingsRepository settings,
        ITimesheetWeekRepository weeks,
        ITimerSegmentRepository segments,
        IWorkingHoursProvider workingHours,
        ITimeEntryNotifier notifier,
        ITimeEntryNotificationMarkRepository marks,
        ITimesheetDecisionPuller puller,
        TimeProvider clock,
        ILogger<TimesheetReminderJob> logger)
    {
        _marks = marks;
        _puller = puller;
        _tenantRegistry = tenantRegistry;
        _tenantContext = tenantContext;
        _settings = settings;
        _weeks = weeks;
        _segments = segments;
        _workingHours = workingHours;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
    }

    public async Task HandleAsync(
        TimesheetReminderJobArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var max = args.MaxPeoplePerTenant <= 0 ? DefaultMaxPeoplePerTenant : args.MaxPeoplePerTenant;
        var correlationId = context.EffectiveCorrelationId.ToString();
        var tenants = await _tenantRegistry.GetActiveTenantsAsync(cancellationToken);
        var sent = 0;
        var failedTenants = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    sent += await RunTenantAsync(max, correlationId, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedTenants++;
                _logger.LogWarning(ex,
                    "time-entry.reminder.tenant_failed TenantId={TenantId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    tenant.Id, ex.GetType().Name, correlationId);
            }
        }

        _logger.LogInformation(
            "time-entry.reminder.completed Tenants={Tenants} Sent={Sent} FailedTenants={Failed} CorrelationId={CorrelationId}",
            tenants.Count, sent, failedTenants, correlationId);
    }

    private async Task<int> RunTenantAsync(int max, string correlationId, CancellationToken ct)
    {
        // N3 — off by default: no row, or the switch not turned on, and nothing reaches anyone.
        if ((await _settings.GetAsync(ct))?.WeeklyReminderEnabled != true)
        {
            return 0;
        }

        var now = _clock.GetUtcNow();
        if (TargetMonday(now, await TenantZoneAsync(now, ct)) is not { } monday)
        {
            return 0;
        }

        var weekKey = WeekCalendar.KeyOf(monday);
        var window = Enumerable.Range(0, ParticipationWeeks + 1).Select(i => WeekCalendar.KeyOf(monday.AddDays(-7 * i))).ToList();
        var participants = (await _weeks.ListUserIdsWithWeeksAsync(window, ct))
            .Concat(await _segments.ListUserIdsWithSegmentsAsync(window, ct))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        // CT acceptance round 1 (M1): the limit is on SENDS, never on the participant list. Cutting the list first took
        // the same first N people every hour and left the rest un-reminded for ever. People already reminded for this
        // week are skipped by a read of their mark (the key the send claimed), so they never use up a run's budget.
        //
        // BL-488 — the people who are due are sent to in GROUPS: one recipient resolution per group, not one per person
        // (each resolution makes AuthService walk the tenant's users). A group is never larger than what is left of the
        // run's budget, so a run still hands over at most `max` reminders.
        var sent = 0;
        var alreadyReminded = 0;
        var index = 0;
        var due = new List<Guid>();
        for (; index < participants.Count && sent < max; index++)
        {
            var userId = participants[index];
            try
            {
                if (await _marks.ExistsAsync(TimeEntryNotificationEvents.WeekReminder,
                        TimeEntryNotifier.MarkKey(TimeEntryNotifier.ReminderKey(userId, weekKey), userId), ct))
                {
                    alreadyReminded++;
                    continue;
                }

                if (await IsDueAsync(userId, monday, weekKey, correlationId, ct))
                {
                    due.Add(userId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "time-entry.reminder.person_failed UserId={UserId} WeekKey={WeekKey} CorrelationId={CorrelationId}",
                    userId, weekKey, correlationId);
            }

            if (due.Count >= Math.Min(ITimeEntryNotifier.ReminderGroupSize, max - sent))
            {
                sent += await SendGroupAsync(due, monday, weekKey, correlationId, ct);
            }
        }

        // The people still waiting when the list ran out. (When the budget ran out, the last group was sent in the loop.)
        if (due.Count > 0)
        {
            sent += await SendGroupAsync(due, monday, weekKey, correlationId, ct);
        }

        if (index < participants.Count)
        {
            _logger.LogInformation(
                "time-entry.reminder.limit_reached WeekKey={WeekKey} Limit={Limit} Participants={Participants} Checked={Checked} "
                + "AlreadyReminded={AlreadyReminded} Left={Left} CorrelationId={CorrelationId}; the next run continues.",
                weekKey, max, participants.Count, index, alreadyReminded, participants.Count - index, correlationId);
        }

        return sent;
    }

    /// <summary>Sends to one group and empties it. A group's failure is logged and never stops the rest of the run.</summary>
    private async Task<int> SendGroupAsync(List<Guid> due, DateOnly monday, string weekKey, string correlationId, CancellationToken ct)
    {
        var group = due.ToList();
        due.Clear();
        try
        {
            return await _notifier.WeekRemindersAsync(group, monday, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                "time-entry.reminder.group_failed People={People} WeekKey={WeekKey} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                group.Count, weekKey, ex.GetType().Name, correlationId);
            return 0;
        }
    }

    /// <summary>N2 — the Monday of the week to remind about: the PREVIOUS ISO week, from Monday 09:00 in the tenant's zone
    /// until the week ends; null only on Monday before 09:00. CT acceptance (T3): not Monday-only — a run missed on
    /// Monday (deploy, outage) or a switch turned on mid-week still reminds once for last week; the per-person-week mark
    /// keeps it to one. A DST change is the zone's business: 09:00 is 09:00 local on either side of it.</summary>
    public static DateOnly? TargetMonday(DateTimeOffset nowUtc, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var daysSinceMonday = ((int)local.DayOfWeek + 6) % 7;
        if (daysSinceMonday == 0 && local.TimeOfDay < ReminderTimeOfDay)
        {
            return null;
        }

        return DateOnly.FromDateTime(local.DateTime).AddDays(-daysSinceMonday - 7);
    }

    /// <summary>N2 — absent or Draft (a rejected or withdrawn revision is a Draft again), and at least one working day.
    /// CT acceptance round 1 (L6): a revision that still reads Submitted may carry a MOD-0023 decision nobody has pulled
    /// yet — the pull finalizer runs FIRST, so an approved week is not reminded and a rejected one is.</summary>
    private async Task<bool> IsDueAsync(Guid userId, DateOnly monday, string weekKey, string correlationId, CancellationToken ct)
    {
        var revisions = await _weeks.ListRevisionsAsync(userId, weekKey, ct);
        if (revisions.Any(r => r.Status == TimesheetWeekStatus.Submitted)
            && (await _puller.PullAsync(revisions, correlationId, ct)).ShouldReread)
        {
            revisions = await _weeks.ListRevisionsAsync(userId, weekKey, ct);
        }

        if (revisions.Any(r => r.Status is TimesheetWeekStatus.Submitted or TimesheetWeekStatus.Approved or TimesheetWeekStatus.Superseded))
        {
            return false;
        }

        var days = (await _workingHours.GetWorkingWindowsAsync(userId, monday, monday.AddDays(6), ct)).Days;
        // An empty answer is not "all days off": the provider could not say, and a guessed day off would hide work.
        return days.Count == 0 || days.Any(d => d.DayKind == WorkingDayKinds.WorkingDay);
    }

    /// <summary>R3 — the tenant's zone (v1: one zone per tenant), read the way every other time-entry clock reads it.</summary>
    private async Task<TimeZoneInfo> TenantZoneAsync(DateTimeOffset nowUtc, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(nowUtc.UtcDateTime);
        return (await _workingHours.GetWorkingWindowsAsync(Guid.Empty, today, today, ct)).TimeZone;
    }
}
