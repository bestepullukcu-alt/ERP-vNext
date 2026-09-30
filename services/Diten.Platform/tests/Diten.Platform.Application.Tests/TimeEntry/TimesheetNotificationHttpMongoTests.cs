using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;
using Diten.Platform.Application.Features.TimeEntry.SelfRegistration;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T3 — the week events (N4), the minutes conflict (N5) and the manifest ⇔ dispatch contract (N7), on the
/// REAL wire: the time-entry and meetings routes, MOD-0023's own decision route, the real finalizer, the real marks in a
/// disposable mongod. The dispatch is captured with the payload it was really given; AuthService's address book is a
/// double (<see cref="TestRecipients"/>).
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetNotificationHttpMongoTests : TimerScenario
{
    private const string MeetingCategory = "INTERNAL_MEETING";
    private readonly Guid _secondManager = Guid.NewGuid();

    public TimesheetNotificationHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await SeedCategoryAsync(Tenant, MeetingCategory);
        // Two holders of the line manager's seat: two stored candidates (D6), two e-mails.
        await SeatAsync(_secondManager, ManagerSeat);
    }

    // ── T3-04 — the four week events ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T3_04_submit_tells_each_stored_candidate_once_with_the_persons_name_and_nothing_per_day()
    {
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA), Row(Monday.AddDays(1), 90, TaskB));

        var week = await StoredWeekAsync(weekId);
        var sent = Sent(TimeEntryNotificationEvents.WeekSubmitted);
        Assert.Equal(2, week.ApproverCandidateUserIds.Count);
        Assert.Equal(
            week.ApproverCandidateUserIds.Select(TestRecipients.Address).OrderBy(a => a),
            sent.Select(r => r.To.Single().Email).OrderBy(a => a));
        Assert.All(sent, r =>
        {
            Assert.Equal("Ayşe Yılmaz", r.Variables[TimeEntryNotificationVariables.PersonName]);
            Assert.Equal($"{TimeEntryHost.WebOrigin}/TimeEntry/Approvals/{weekId:D}", r.Variables[TimeEntryNotificationVariables.TimesheetUrl]);
            // Minimal content (N7, D11): no minutes, no days, no task.
            Assert.DoesNotContain(r.Variables.Values, v => v is int || Convert.ToString(v)!.Contains("120"));
        });
        Assert.DoesNotContain(sent, r => r.To.Single().Email == TestRecipients.Address(Person));
    }

    [Fact]
    public async Task T3_04_approval_is_told_once_however_often_the_finalizer_reads_it()
    {
        var weekId = await SubmittedWeekAsync();
        var approver = (await StoredWeekAsync(weekId)).AssignedApproverUserId!.Value;
        Ok(await DecideAsync(approver, weekId, approve: true));

        Ok(await GetWeekAsync()); // the pull finalizer applies the decision here
        Ok(await GetWeekAsync()); // and finds nothing to do here
        await RunSweepAsync();    // nor here

        Assert.Equal(TimesheetWeekStatus.Approved, (await StoredWeekAsync(weekId)).Status);
        var approved = Assert.Single(Sent(TimeEntryNotificationEvents.WeekApproved));
        Assert.Equal(TestRecipients.Address(Person), approved.To.Single().Email);
        Assert.Equal($"{TimeEntryHost.WebOrigin}/TimeEntry?week={CurrentWeek}", approved.Variables[TimeEntryNotificationVariables.TimesheetUrl]);
        Assert.Empty(Sent(TimeEntryNotificationEvents.WeekRejected));
    }

    [Fact]
    public async Task T3_04_rejection_carries_the_approvers_reason_and_a_resubmission_is_a_new_submission()
    {
        var weekId = await SubmittedWeekAsync();
        var approver = (await StoredWeekAsync(weekId)).AssignedApproverUserId!.Value;
        Ok(await DecideAsync(approver, weekId, approve: false, comment: "Tuesday is missing the client call."));
        Ok(await GetWeekAsync());
        Ok(await GetWeekAsync());

        var rejected = Assert.Single(Sent(TimeEntryNotificationEvents.WeekRejected));
        Assert.Equal(TestRecipients.Address(Person), rejected.To.Single().Email);
        Assert.Equal("Tuesday is missing the client call.", rejected.Variables[TimeEntryNotificationVariables.Reason]);

        // Submitted again after the fix: the approvers hear of THIS submission too.
        var again = await SubmitAsync();
        Ok(again);
        Assert.Equal(4, Sent(TimeEntryNotificationEvents.WeekSubmitted).Count);
    }

    [Fact]
    public async Task T3_04_withdraw_tells_the_same_candidates_without_a_link_to_a_week_they_can_no_longer_open()
    {
        var weekId = await SubmittedWeekAsync();
        Ok(await WithdrawAsync());

        var candidates = (await StoredWeekAsync(weekId)).ApproverCandidateUserIds.Select(TestRecipients.Address).OrderBy(a => a);
        var withdrawn = Sent(TimeEntryNotificationEvents.WeekWithdrawn);
        Assert.Equal(candidates, withdrawn.Select(r => r.To.Single().Email).OrderBy(a => a));
        Assert.All(withdrawn, r => Assert.False(r.Variables.ContainsKey(TimeEntryNotificationVariables.TimesheetUrl)));
    }

    [Theory]
    [InlineData(false)] // the pipeline refuses (provider down)
    [InlineData(true)]  // the pipeline throws
    public async Task T3_04_a_failed_send_never_fails_the_command(bool throws)
    {
        Host.Dispatch.Refuse = !throws;
        Host.Dispatch.Throw = throws;

        var weekId = await SubmittedWeekAsync(); // asserts 200 inside
        Assert.Equal(TimesheetWeekStatus.Submitted, (await StoredWeekAsync(weekId)).Status);
        Ok(await WithdrawAsync());
        Assert.Equal(TimesheetWeekStatus.Draft, (await StoredWeekAsync(weekId)).Status);
        Assert.NotEmpty(Sent(TimeEntryNotificationEvents.WeekSubmitted));
    }

    // ── T3-05 — the minutes conflict ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T3_05_an_accepted_row_marked_absent_tells_the_person_once_and_a_correction_to_excused_once_more()
    {
        var meeting = await SeedMeetingAsync(InvitationResponse.Accepted);
        await AcceptSuggestionAsync(meeting);

        await PublishMinutesAsync(meeting, AttendanceStatus.Absent);
        var first = Assert.Single(Sent(TimeEntryNotificationEvents.MinutesConflict));
        Assert.Equal(TestRecipients.Address(Person), first.To.Single().Email);
        Assert.Equal("Weekly sync", first.Variables[TimeEntryNotificationVariables.MeetingTitle]);
        Assert.Equal("2026-10-06", first.Variables[TimeEntryNotificationVariables.MeetingDate]);
        Assert.Equal($"{TimeEntryHost.WebOrigin}/TimeEntry?week={CurrentWeek}", first.Variables[TimeEntryNotificationVariables.TimesheetUrl]);

        await CorrectMinutesAsync(meeting, AttendanceStatus.Excused);
        Assert.Equal(2, Sent(TimeEntryNotificationEvents.MinutesConflict).Count);

        await CorrectMinutesAsync(meeting, AttendanceStatus.Absent); // Absent was told already
        Assert.Equal(2, Sent(TimeEntryNotificationEvents.MinutesConflict).Count);

        // The person only: never the approver, never the organizer.
        Assert.All(Host.Dispatch.Requests.Where(r => r.TenantId == Tenant), r =>
            Assert.Equal(TestRecipients.Address(Person), r.To.Single().Email));
    }

    [Fact]
    public async Task T3_05_present_and_an_unaccepted_suggestion_send_nothing()
    {
        var attended = await SeedMeetingAsync(InvitationResponse.Accepted);
        await AcceptSuggestionAsync(attended);
        await PublishMinutesAsync(attended, AttendanceStatus.Present);

        var neverAccepted = await SeedMeetingAsync(InvitationResponse.Accepted, startHour: 14);
        await PublishMinutesAsync(neverAccepted, AttendanceStatus.Absent);

        Assert.Empty(Sent(TimeEntryNotificationEvents.MinutesConflict));
    }

    [Fact]
    public async Task T3_05_an_observer_that_throws_never_fails_the_minutes_publish_or_correction()
    {
        using var failing = new TimeEntryHost(Fixture.DbContext,
            services => services.AddScoped<IMeetingAttendanceObserver, ThrowingObserver>());
        var meeting = await SeedMeetingAsync(InvitationResponse.Accepted);

        var draft = await failing.PutAsync($"/api/v1/meetings/{meeting}/minutes/draft", MinutesToken(failing),
            new { attendance = new[] { new { attendeeUserId = Person, status = (int)AttendanceStatus.Absent } }, decisions = Array.Empty<object>(), expectedVersion = (int?)null });
        Assert.True(draft.Status is HttpStatusCode.OK or HttpStatusCode.Created, draft.ToString());
        var publish = await failing.PostAsync($"/api/v1/meetings/{meeting}/minutes/publish", MinutesToken(failing),
            new { expectedVersion = draft.Data.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.OK, publish.Status);
        var correct = await failing.PostAsync($"/api/v1/meetings/{meeting}/minutes/correct", MinutesToken(failing),
            new { attendance = new[] { new { attendeeUserId = Person, status = (int)AttendanceStatus.Excused } }, decisions = Array.Empty<object>(), correctionReason = "late arrival noted" });
        Assert.Equal(HttpStatusCode.Created, correct.Status);

        Assert.Equal(2, ThrowingObserver.Calls);
        var stored = await Collection<MeetingAttendee>(PlatformCollections.MeetingAttendees)
            .Find(a => a.MeetingId == meeting && a.UserId == Person).SingleAsync();
        Assert.Equal(AttendanceStatus.Excused, stored.AttendanceStatus); // the sync before the observer stands
    }

    [Fact]
    public void T3_05_production_wires_the_time_entry_observer_into_the_minutes_handlers()
    {
        var services = new ServiceCollection();
        services.AddTimeEntryModule();
        Assert.Contains(services, d => d.ServiceType == typeof(IMeetingAttendanceObserver)
                                       && d.ImplementationType == typeof(MinutesConflictObserver));
    }

    // ── T3-06 — manifest ⇔ dispatch ⇔ template ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T3_06_every_events_required_variables_are_exactly_the_keys_its_dispatch_really_sent()
    {
        // Drive all seven through their production paths and record what was actually handed to the dispatch.
        var weekId = await SubmittedWeekAsync();                                        // submitted
        Ok(await WithdrawAsync());                                                      // withdrawn
        Ok(await SubmitAsync());
        var approver = (await StoredWeekAsync(weekId)).AssignedApproverUserId!.Value;
        Ok(await DecideAsync(approver, weekId, approve: false, comment: "please split Monday"));
        Ok(await GetWeekAsync());                                                       // rejected
        Ok(await SubmitAsync());
        approver = (await StoredWeekAsync(weekId)).AssignedApproverUserId!.Value;
        Ok(await DecideAsync(approver, weekId, approve: true));
        Ok(await GetWeekAsync());                                                       // approved

        var meeting = await SeedMeetingAsync(InvitationResponse.Accepted);
        await AcceptCurrentWeekSuggestionAsync(meeting, useSecondWeek: true);
        await PublishMinutesAsync(meeting, AttendanceStatus.Absent);                    // minutes conflict

        await RemindAsync();                                                            // reminder

        using (var scope = Host.Services.CreateScope())                                 // auto-closed: the real notifier
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<Diten.Platform.Common.Tenancy.ITenantContext>(), Tenant))
        {
            await new TimerAutoCloseNotifier(Host.Dispatch, Host.Recipients, scope.ServiceProvider.GetRequiredService<ITimeEntryLinks>(),
                    NullLogger<TimerAutoCloseNotifier>.Instance)
                .NotifyAsync(new TimerSegment
                {
                    TenantId = Tenant, UserId = Person, CategoryCode = Category, LocalDate = Monday, WeekKey = CurrentWeek,
                    TimeZoneId = Zone, DurationSeconds = 5400, StartSource = TimerStartSource.TimerControl
                });
        }

        var manifest = new TimeEntryManifestProvider().GetManifest().NotificationEvents!;
        Assert.Equal(7, manifest.Count);
        foreach (var declared in manifest)
        {
            var sent = Host.Dispatch.Requests.Where(r => r.TenantId == Tenant && r.EventCode == declared.EventCode).ToList();
            Assert.True(sent.Count > 0, $"{declared.EventCode} was never dispatched by this test");
            var required = declared.RequiredVariables!.Select(v => v.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.All(sent, r => Assert.Equal(required, r.Variables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList()));
            Assert.Equal("Active", declared.Status);
            Assert.True(Diten.Platform.Application.Features.Notifications.NotificationParsing.IsValidTemplateKey(declared.EventCode),
                $"{declared.EventCode} breaks the platform's event-code rule; the sync keeps it Draft and the dispatch refuses it");
        }
    }

    [Fact]
    public void T3_06_seven_events_have_seven_languages_and_render_exactly_their_manifest_variables()
    {
        var seeded = SeededTemplates();
        var manifest = new TimeEntryManifestProvider().GetManifest().NotificationEvents!;
        var placeholder = new Regex(@"\{\{\s*([A-Za-z][A-Za-z0-9_.]*)\s*\}\}");

        foreach (var declared in manifest)
        {
            var required = declared.RequiredVariables!.Select(v => v.Name).ToHashSet();
            var templates = seeded.Where(t => t.TemplateKey == declared.DefaultTemplateKey).ToList();
            Assert.Equal(new[] { "ar", "en", "es", "fr", "ru", "tr", "zh" }, templates.Select(t => t.Locale).OrderBy(l => l));
            foreach (var template in templates)
            {
                foreach (var part in new[] { template.SubjectTemplate, template.BodyHtmlTemplate!, template.BodyTextTemplate! })
                {
                    var used = placeholder.Matches(part).Select(m => m.Groups[1].Value).ToHashSet();
                    Assert.True(used.IsSubsetOf(required), $"{template.TemplateKey}/{template.Locale} renders {string.Join(",", used.Except(required))} the event never sends");
                }

                var everywhere = placeholder.Matches(template.BodyHtmlTemplate + template.BodyTextTemplate)
                    .Select(m => m.Groups[1].Value).ToHashSet();
                Assert.True(required.SetEquals(everywhere), $"{template.TemplateKey}/{template.Locale} leaves a required variable out");
                Assert.Equal(required.OrderBy(n => n), template.Variables.Select(v => v.Name).OrderBy(n => n));
            }

            // A real translation, not an English copy: every non-English subject differs from the English one.
            var english = templates.Single(t => t.Locale == "en").SubjectTemplate;
            Assert.All(templates.Where(t => t.Locale != "en"), t => Assert.NotEqual(english, t.SubjectTemplate));
        }
    }

    /// <summary>The C# twin of <c>time-entry-copy-no-internal-codes.test.js</c>: no e-mail a person reads carries a
    /// decision number, a pack section, a module-pack, backlog or other internal code.</summary>
    public static readonly Regex InternalCode = new(
        @"\(D\d{1,2}\)|（D\d{1,2}）|\bD\d{1,2}\b|§\s?\d|MOD-\d{4}|BL-\d{3}|DCP-\d|ADR-\d|WC-\d|CAND-CAP|\bN[1-7]\b|\bT[1-4][ab]?\b");

    [Fact]
    public void T3_06_no_time_entry_email_names_an_internal_code()
    {
        var leaks = SeededTemplates()
            .Where(t => t.TemplateKey.StartsWith("timeentry.", StringComparison.Ordinal))
            .SelectMany(t => new[] { t.SubjectTemplate, t.BodyHtmlTemplate, t.BodyTextTemplate }
                .Where(text => text is not null && InternalCode.IsMatch(text))
                .Select(text => $"{t.TemplateKey}/{t.Locale}: {text}"))
            .ToList();
        Assert.Empty(leaks);
        Assert.Equal(49, SeededTemplates().Count(t => t.TemplateKey.StartsWith("timeentry.", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("see (D6)")]
    [InlineData("pack §22")]
    [InlineData("MOD-0280 approval")]
    [InlineData("BL-482")]
    [InlineData("decided in N4")]
    public void T3_06_the_internal_code_pattern_catches_what_it_claims_to(string text) => Assert.Matches(InternalCode, text);

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────────────

    private IReadOnlyList<NotificationEventDispatchRequest> Sent(string eventCode)
        => Host.Dispatch.For(eventCode).Where(r => r.TenantId == Tenant).ToList();

    private async Task RunSweepAsync()
    {
        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TimesheetDecisionSweepJob>().HandleAsync(
            new TimesheetDecisionSweepJobArgs(200), new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Recurring));
    }

    /// <summary>A Draft in W42 and the next Monday's run: the reminder event, on its production path.</summary>
    private async Task RemindAsync()
    {
        await Collection<Tenant>(PlatformCollections.Tenants).UpdateOneAsync(
            t => t.Id == Tenant, Builders<Tenant>.Update.Set(t => t.Status, TenantStatus.Active));
        var version = (await Host.GetAsync("/api/v1/time-entry/settings", AdminToken())).Data.GetProperty("version").GetInt32();
        Ok(await Host.PutAsync("/api/v1/time-entry/settings", AdminToken(),
            new { expectedVersion = version, timeAdminPoolPositionId = (Guid?)null, weeklyReminderEnabled = true }));
        Host.Clock.UtcNow = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero); // Monday W43, 09:00 Istanbul
        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TimesheetReminderJob>().HandleAsync(
            new TimesheetReminderJobArgs(2000), new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Recurring));
        Host.Clock.UtcNow = Wednesday;
    }

    /// <summary>A meeting on Tuesday 2026-10-06 in Istanbul, organised by the manager, the person invited.</summary>
    private async Task<Guid> SeedMeetingAsync(InvitationResponse response, int startHour = 10, int day = 6)
    {
        var id = Guid.NewGuid();
        var start = IstanbulLocal(2026, 10, day, startHour, 0);
        await Collection<Meeting>(PlatformCollections.MeetingMeetings).InsertOneAsync(new Meeting
        {
            Id = id, TenantId = Tenant, Title = "Weekly sync", MeetingTypeId = Guid.NewGuid(), StartAt = start,
            EndAt = start.AddMinutes(45), OrganizerUserId = Manager, IdempotencyKey = "k-" + id.ToString("N"),
            Lifecycle = MeetingLifecycle.Scheduled
        });
        await Collection<MeetingAttendee>(PlatformCollections.MeetingAttendees).InsertOneAsync(new MeetingAttendee
        {
            TenantId = Tenant, MeetingId = id, UserId = Person, InvitationResponse = response
        });
        return id;
    }

    private Task AcceptSuggestionAsync(Guid meeting) => AcceptCurrentWeekSuggestionAsync(meeting, useSecondWeek: false);

    /// <summary>"I attended" on the meeting's suggestion, through the week route. <paramref name="useSecondWeek"/>: the
    /// current week is already approved in the contract test, so the meeting's day moves into W42 there.</summary>
    private async Task AcceptCurrentWeekSuggestionAsync(Guid meeting, bool useSecondWeek)
    {
        var weekKey = CurrentWeek;
        if (useSecondWeek)
        {
            weekKey = "2026-W42";
            var start = IstanbulLocal(2026, 10, 13, 10, 0);
            await Collection<Meeting>(PlatformCollections.MeetingMeetings).UpdateOneAsync(m => m.Id == meeting,
                Builders<Meeting>.Update.Set(m => m.StartAt, start).Set(m => m.EndAt, start.AddMinutes(45)));
            Host.Clock.UtcNow = IstanbulLocal(2026, 10, 14, 12, 0);
        }

        var week = await Host.GetAsync($"/api/v1/time-entry/weeks/{weekKey}", PersonToken());
        Ok(week);
        var suggestion = week.Data.GetProperty("suggestions").EnumerateArray()
            .Single(s => s.GetProperty("meetingId").GetGuid() == meeting);
        Ok(await Host.PostAsync(
            $"/api/v1/time-entry/weeks/{weekKey}/suggestions/{suggestion.GetProperty("id").GetGuid()}/accept", PersonToken(),
            new { expectedVersion = week.Data.GetProperty("version").GetInt32() }));
        Host.Clock.UtcNow = Wednesday;
    }

    private string MinutesToken(TimeEntryHost? host = null)
        => (host ?? Host).Token(Manager, Tenant, MeetingPermissions.Read, MeetingPermissions.MinutesWrite, MeetingPermissions.MinutesPublish);

    private async Task PublishMinutesAsync(Guid meeting, AttendanceStatus status)
    {
        var draft = await Host.PutAsync($"/api/v1/meetings/{meeting}/minutes/draft", MinutesToken(),
            new { attendance = new[] { new { attendeeUserId = Person, status = (int)status } }, decisions = Array.Empty<object>(), expectedVersion = (int?)null });
        Assert.True(draft.Status is HttpStatusCode.OK or HttpStatusCode.Created, draft.ToString());
        var published = await Host.PostAsync($"/api/v1/meetings/{meeting}/minutes/publish", MinutesToken(),
            new { expectedVersion = draft.Data.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.OK, published.Status);
    }

    private async Task CorrectMinutesAsync(Guid meeting, AttendanceStatus status)
    {
        var corrected = await Host.PostAsync($"/api/v1/meetings/{meeting}/minutes/correct", MinutesToken(),
            new { attendance = new[] { new { attendeeUserId = Person, status = (int)status } }, decisions = Array.Empty<object>(), correctionReason = "attendance corrected" });
        Assert.Equal(HttpStatusCode.Created, corrected.Status);
    }

    private static IReadOnlyList<Diten.Platform.Domain.Entities.Notifications.NotificationTemplate> SeededTemplates()
    {
        var factory = typeof(Diten.Platform.Infrastructure.Persistence.Configurations.NotificationTemplateSeed)
            .GetMethod("CreatePlatformDefaults", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (IReadOnlyList<Diten.Platform.Domain.Entities.Notifications.NotificationTemplate>)factory.Invoke(null, null)!;
    }

    private sealed class ThrowingObserver : IMeetingAttendanceObserver
    {
        public static int Calls;

        public Task OnMinutesAttendanceRecordedAsync(MeetingAttendanceObservation observation, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            throw new InvalidOperationException("test: the observer fails");
        }
    }
}
