using System.Net;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.TimeEntry;

using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T3 — the Monday reminder (pack §21.3 N1–N3, N6), on the REAL job, the REAL notifier, the real marks in a
/// disposable mongod, and the tenant switch turned on through the real settings PUT. Only the dispatch (captured with its
/// payload) and AuthService's address book are doubles.
///
/// <para>The week to remind about is W41 (Monday 2026-10-05, the scenario's week); the reminder is due from Monday
/// 2026-10-12 09:00 in the tenant's zone. Week states are staged in the store: the job reads nothing else, and the paths
/// that produce each state have their own suites.</para>
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetReminderHttpMongoTests : TimerScenario
{
    /// <summary>Monday 2026-10-12 09:00 in Istanbul (UTC+3, no DST).</summary>
    private static readonly DateTimeOffset MondayNine = new(2026, 10, 12, 6, 0, 0, TimeSpan.Zero);

    public TimesheetReminderHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    /// <summary>The job walks ACTIVE tenants only (the registry's own filter), like every other time-entry job.</summary>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Collection<Tenant>(PlatformCollections.Tenants).UpdateOneAsync(
            t => t.Id == Tenant, Builders<Tenant>.Update.Set(t => t.Status, TenantStatus.Active));
    }

    // ── T3-01 — who is reminded ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T3_01_absent_draft_rejected_and_withdrawn_weeks_get_one_reminder_the_rest_none()
    {
        var absent = Guid.NewGuid();      // no W41 week, but a timer segment in W40 → a participant
        var draft = Person;               // a W41 draft
        var rejected = Guid.NewGuid();    // back to Draft after a rejection
        var withdrawn = Guid.NewGuid();   // back to Draft after a withdrawal
        var submitted = Guid.NewGuid();
        var approved = Guid.NewGuid();
        var superseded = Guid.NewGuid();  // revision 1 superseded by an approved correction
        var old = Guid.NewGuid();         // last active in W36 — no longer a participant
        var deactivated = Guid.NewGuid(); // a W41 draft, but AuthService no longer hands out their address

        await SeedSegmentAsync(absent, new DateOnly(2026, 9, 29));
        await SeedWeekAsync(draft, TimesheetWeekStatus.Draft);
        await SeedWeekAsync(rejected, TimesheetWeekStatus.Draft, w => { w.LastRejectedAtUtc = Wednesday; w.LastRejectionReason = "fix Monday"; });
        await SeedWeekAsync(withdrawn, TimesheetWeekStatus.Draft, w => w.WithdrawnAtUtc = Wednesday);
        await SeedWeekAsync(submitted, TimesheetWeekStatus.Submitted);
        await SeedWeekAsync(approved, TimesheetWeekStatus.Approved);
        await SeedWeekAsync(superseded, TimesheetWeekStatus.Superseded);
        await SeedWeekAsync(superseded, TimesheetWeekStatus.Approved, revision: 2);
        await SeedWeekAsync(old, TimesheetWeekStatus.Draft, weekKey: "2026-W36", monday: new DateOnly(2026, 8, 31));
        await SeedWeekAsync(deactivated, TimesheetWeekStatus.Draft);
        Host.Recipients.Omitted.Add(deactivated);
        await SwitchReminderAsync(true);

        await RunAtAsync(MondayNine);

        var reminded = Reminders().Select(r => r.To.Single().Email).OrderBy(e => e).ToList();
        Assert.Equal(
            new[] { absent, draft, rejected, withdrawn }.Select(TestRecipients.Address).OrderBy(e => e),
            reminded);
        Assert.All(Reminders(), r =>
        {
            Assert.Equal(Tenant, r.TenantId);
            Assert.Equal(TimesheetWeekLabel, r.Variables[TimeEntryNotificationVariables.WeekLabel]);
            Assert.Equal($"{TimeEntryHost.WebOrigin}/TimeEntry?week={CurrentWeek}", r.Variables[TimeEntryNotificationVariables.TimesheetUrl]);
        });

        // The deactivated person was never claimed either: resolution comes before the claim.
        Assert.Equal(4, await MarkCountAsync(TimeEntryNotificationEvents.WeekReminder));
        Assert.DoesNotContain(await MarkKeysAsync(TimeEntryNotificationEvents.WeekReminder), k => k.Contains(deactivated.ToString("N")));
    }

    [Fact]
    public async Task T3_01_the_tenant_switch_off_sends_nothing_and_it_is_off_by_default()
    {
        await SeedWeekAsync(Person, TimesheetWeekStatus.Draft);

        await RunAtAsync(MondayNine); // no settings row at all
        Assert.Empty(Reminders());

        await SwitchReminderAsync(false); // a row, switch off
        await RunAtAsync(MondayNine);
        Assert.Empty(Reminders());
        Assert.Equal(0, await MarkCountAsync(TimeEntryNotificationEvents.WeekReminder));
    }

    [Fact]
    public async Task T3_01_a_week_of_weekend_and_holidays_only_is_skipped_but_an_unresolved_calendar_is_a_working_week()
    {
        await SeedWeekAsync(Person, TimesheetWeekStatus.Draft);
        await SwitchReminderAsync(true);
        foreach (var day in WeekCalendar.DaysOf(Monday).Take(5))
        {
            Host.Calendar.Holidays.Add(day);
        }

        await RunAtAsync(MondayNine);
        Assert.Empty(Reminders());

        Host.Calendar.Holidays.Clear();
        foreach (var day in WeekCalendar.DaysOf(Monday))
        {
            Host.Calendar.Unresolved.Add(day);
        }

        await RunAtAsync(MondayNine.AddMinutes(30));
        Assert.Single(Reminders());
    }

    [Fact]
    public async Task T3_01_a_person_AuthService_does_not_return_is_never_sent_anything()
    {
        await SeedWeekAsync(Person, TimesheetWeekStatus.Draft);
        await SwitchReminderAsync(true);
        Host.Recipients.Omitted.Add(Person);

        await RunAtAsync(MondayNine);

        Assert.Empty(Reminders());
        Assert.Equal(0, await MarkCountAsync(TimeEntryNotificationEvents.WeekReminder));
    }

    // ── T3-02 — the tenant-local Monday 09:00, across the DST change ─────────────────────────────────────────────

    [Fact]
    public async Task T3_02_Zurich_Monday_after_the_DST_change_0859_none_0900_one_second_run_still_one()
    {
        // 2026-10-25 is the last Sunday of October: Zurich goes from UTC+2 to UTC+1. Monday 2026-10-26 09:00 local is
        // 08:00Z — a fixed summer offset would say 07:00Z. The target week is W43, the week that holds the change.
        await SetTenantZoneAsync("Europe/Zurich");
        await SeedWeekAsync(Person, TimesheetWeekStatus.Draft, weekKey: "2026-W43", monday: new DateOnly(2026, 10, 19));
        await SwitchReminderAsync(true);

        await RunAtAsync(new DateTimeOffset(2026, 10, 26, 7, 0, 0, TimeSpan.Zero)); // 08:00 local — what +2 would call 09:00
        await RunAtAsync(new DateTimeOffset(2026, 10, 26, 7, 59, 0, TimeSpan.Zero)); // 08:59 local
        Assert.Empty(Reminders());

        await RunAtAsync(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero)); // 09:00 local
        var reminder = Assert.Single(Reminders());
        Assert.Equal("2026-W43 (2026-10-19 – 2026-10-25)", reminder.Variables[TimeEntryNotificationVariables.WeekLabel]);

        await RunAtAsync(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero)); // the same hour again
        await RunAtAsync(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero)); // and the next one
        Assert.Single(Reminders());

        await RunAtAsync(new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero)); // Tuesday: the catch-up finds the mark
        Assert.Single(Reminders());
    }

    /// <summary>CT acceptance (T3): the job did not run on Monday (deploy, outage) — the first run of the week still
    /// reminds once for last week; the next runs find the mark.</summary>
    [Fact]
    public async Task T3_02_a_Monday_the_job_missed_is_caught_up_once_on_the_first_run_later_that_week()
    {
        await SetTenantZoneAsync("Europe/Zurich");
        await SeedWeekAsync(Person, TimesheetWeekStatus.Draft, weekKey: "2026-W43", monday: new DateOnly(2026, 10, 19));
        await SwitchReminderAsync(true);

        await RunAtAsync(new DateTimeOffset(2026, 10, 28, 13, 0, 0, TimeSpan.Zero)); // Wednesday 14:00 local, first run
        Assert.Equal("2026-W43 (2026-10-19 – 2026-10-25)", Assert.Single(Reminders()).Variables[TimeEntryNotificationVariables.WeekLabel]);

        await RunAtAsync(new DateTimeOffset(2026, 11, 1, 22, 0, 0, TimeSpan.Zero)); // Sunday 23:00 local
        Assert.Single(Reminders());
    }

    [Theory]
    [InlineData("2026-10-26T07:59:00Z", null)]
    [InlineData("2026-10-26T08:00:00Z", "2026-10-19")]
    [InlineData("2026-10-19T06:59:00Z", null)]           // the Monday BEFORE the change: 08:59 at UTC+2
    [InlineData("2026-10-19T07:00:00Z", "2026-10-12")]   // 09:00 at UTC+2
    [InlineData("2026-10-25T23:30:00Z", null)]           // 00:30 Monday local — before 09:00
    [InlineData("2026-10-27T08:00:00Z", "2026-10-19")]   // Tuesday 09:00 local — catch-up, still last week
    [InlineData("2026-11-01T22:59:00Z", "2026-10-19")]   // Sunday 23:59 local — the week's last minute
    [InlineData("2026-10-25T22:00:00Z", "2026-10-12")]   // Sunday 23:00 of W43 (after the change) — last week is W42
    public void T3_02_the_target_week_is_decided_in_the_zone_not_in_UTC(string nowUtc, string? expectedMonday)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");
        var monday = TimesheetReminderJob.TargetMonday(DateTimeOffset.Parse(nowUtc), zone);
        Assert.Equal(expectedMonday, monday?.ToString("yyyy-MM-dd"));
    }

    // ── T3-03 — at most once under a race ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T3_03_twenty_parallel_reminders_for_one_person_week_send_exactly_one()
    {
        var starts = new TaskCompletionSource();
        var runs = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await starts.Task;
            using var scope = Host.Services.CreateScope();
            using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
            {
                return await scope.ServiceProvider.GetRequiredService<ITimeEntryNotifier>().WeekReminderAsync(Person, Monday);
            }
        })).ToList();
        starts.SetResult();
        var results = await Task.WhenAll(runs);

        Assert.Equal(1, results.Count(sent => sent));
        Assert.Single(Reminders());
        Assert.Equal(1, await MarkCountAsync(TimeEntryNotificationEvents.WeekReminder));
    }

    [Fact]
    public async Task T3_03_the_mark_store_itself_lets_exactly_one_of_twenty_parallel_claims_win()
    {
        var starts = new TaskCompletionSource();
        var claims = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await starts.Task;
            using var scope = Host.Services.CreateScope();
            using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
            {
                return await scope.ServiceProvider.GetRequiredService<ITimeEntryNotificationMarkRepository>()
                    .TryClaimAsync("race.kind", "one-key", Wednesday);
            }
        })).ToList();
        starts.SetResult();

        Assert.Equal(1, (await Task.WhenAll(claims)).Count(won => won));

        // Tenant-first: the same (kind, key) in another tenant is its own claim.
        using var other = Host.Services.CreateScope();
        using (TenantScope.Begin(other.ServiceProvider.GetRequiredService<ITenantContext>(), OtherTenant))
        {
            Assert.True(await other.ServiceProvider.GetRequiredService<ITimeEntryNotificationMarkRepository>()
                .TryClaimAsync("race.kind", "one-key", Wednesday));
        }
    }

    // ── T3-08 — the tenant switch, on the settings command ───────────────────────────────────────────────────────

    [Fact]
    public async Task T3_08_the_reminder_switch_defaults_off_is_versioned_audited_and_leaves_the_pool_alone()
    {
        var read = await Host.GetAsync("/api/v1/time-entry/settings", AdminToken());
        Assert.False(read.Data.GetProperty("weeklyReminderEnabled").GetBoolean());

        await SetPoolAsync(PoolSeat);
        var version = (await Host.GetAsync("/api/v1/time-entry/settings", AdminToken())).Data.GetProperty("version").GetInt32();

        var on = await Host.PutAsync("/api/v1/time-entry/settings", AdminToken(),
            new { expectedVersion = version, timeAdminPoolPositionId = PoolSeat, weeklyReminderEnabled = true });
        Assert.Equal(HttpStatusCode.OK, on.Status);
        Assert.True(on.Data.GetProperty("weeklyReminderEnabled").GetBoolean());
        Assert.Equal(version + 1, on.Data.GetProperty("version").GetInt32());

        // A stale version is refused and changes nothing.
        var stale = await Host.PutAsync("/api/v1/time-entry/settings", AdminToken(),
            new { expectedVersion = version, timeAdminPoolPositionId = PoolSeat, weeklyReminderEnabled = false });
        Assert.Equal(HttpStatusCode.Conflict, stale.Status);
        Assert.Equal(TimeEntryReasonCodes.SettingsConcurrencyConflict, stale.ReasonCode);

        // A pool-only save (the pool button) leaves the reminder as it is.
        var poolOnly = await Host.PutAsync("/api/v1/time-entry/settings", AdminToken(),
            new { expectedVersion = version + 1, timeAdminPoolPositionId = PoolSeat });
        Assert.Equal(HttpStatusCode.OK, poolOnly.Status);
        Assert.True(poolOnly.Data.GetProperty("weeklyReminderEnabled").GetBoolean());

        var stored = await Collection<TimeEntrySettings>(PlatformCollections.TimeEntrySettings)
            .Find(s => s.TenantId == Tenant).SingleAsync();
        Assert.True(stored.WeeklyReminderEnabled);
        Assert.Equal(PoolSeat, stored.TimeAdminPoolPositionId);

        var audited = Host.Audit.Requests.Where(r => r.EntityType == "TimeEntrySettings").ToList();
        Assert.Contains(audited, r => r.Metadata is not null
                                      && r.Metadata.TryGetValue("weeklyReminderEnabled", out var value) && Equals(value, true));

        // Without the settings key: 403, nothing written.
        var person = await Host.PutAsync("/api/v1/time-entry/settings", PersonToken(),
            new { expectedVersion = version + 2, timeAdminPoolPositionId = PoolSeat, weeklyReminderEnabled = false });
        Assert.Equal(HttpStatusCode.Forbidden, person.Status);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────────────

    private const string TimesheetWeekLabel = "2026-W41 (2026-10-05 – 2026-10-11)";

    private IReadOnlyList<Diten.Platform.Application.Features.Notifications.Services.NotificationEventDispatchRequest> Reminders()
        => Host.Dispatch.For(TimeEntryNotificationEvents.WeekReminder).Where(r => r.TenantId == Tenant).ToList();

    private async Task SwitchReminderAsync(bool enabled)
    {
        var version = (await Host.GetAsync("/api/v1/time-entry/settings", AdminToken())).Data.GetProperty("version").GetInt32();
        var result = await Host.PutAsync("/api/v1/time-entry/settings", AdminToken(),
            new { expectedVersion = version, timeAdminPoolPositionId = (Guid?)null, weeklyReminderEnabled = enabled });
        Assert.Equal(HttpStatusCode.OK, result.Status);
    }

    private async Task RunAtAsync(DateTimeOffset nowUtc)
    {
        Host.Clock.UtcNow = nowUtc;
        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TimesheetReminderJob>().HandleAsync(
            new TimesheetReminderJobArgs(2000), new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Recurring));
    }

    private Task SeedWeekAsync(Guid user, TimesheetWeekStatus status, Action<TimesheetWeek>? tweak = null,
        string weekKey = CurrentWeek, DateOnly? monday = null, int revision = 1)
    {
        var week = new TimesheetWeek
        {
            TenantId = Tenant,
            UserId = user,
            WeekKey = weekKey,
            WeekStartDate = monday ?? Monday,
            TimeZoneId = Zone,
            RevisionNumber = revision,
            Status = status,
            IsOpen = status is TimesheetWeekStatus.Draft or TimesheetWeekStatus.Submitted,
            InForce = status == TimesheetWeekStatus.Approved
        };
        tweak?.Invoke(week);
        return Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).InsertOneAsync(week);
    }

    private Task SeedSegmentAsync(Guid user, DateOnly localDate)
        => Collection<TimerSegment>(PlatformCollections.TimeEntryTimerSegments).InsertOneAsync(new TimerSegment
        {
            TenantId = Tenant,
            UserId = user,
            CategoryCode = Category,
            IsRunning = false,
            DurationSeconds = 1800,
            LocalDate = localDate,
            TimeZoneId = Zone,
            WeekKey = WeekCalendar.KeyOf(localDate),
            StartSource = TimerStartSource.TimerControl,
            StopReason = TimerStopReason.TimerControl
        });

    private Task<long> MarkCountAsync(string kind)
        => Collection<TimeEntryNotificationMark>(PlatformCollections.TimeEntryNotificationMarks)
            .CountDocumentsAsync(m => m.TenantId == Tenant && m.Kind == kind);

    private async Task<List<string>> MarkKeysAsync(string kind)
        => (await Collection<TimeEntryNotificationMark>(PlatformCollections.TimeEntryNotificationMarks)
            .Find(m => m.TenantId == Tenant && m.Kind == kind).ToListAsync()).Select(m => m.Key).ToList();
}
