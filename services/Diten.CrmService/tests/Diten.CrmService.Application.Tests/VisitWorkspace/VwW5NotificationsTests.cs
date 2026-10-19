using System.Net;
using System.Text;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.DoctorInsights;
using Diten.CrmService.Application.Features.VisitEvidenceFlags;
using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Application.Tests.VisitReport;
using Diten.CrmService.Application.Tests.VisitExecution;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Auth;
using Diten.CrmService.Infrastructure.VisitNotifications;
using Diten.CrmService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;
using ReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W5 · W5-BE on the PRODUCTION worker, sender, handlers, Platform client and Mongo store: the overdue reminder once
/// per visit (only an expired visit, only a tenant whose settings turn it on, never the whole history), the retry with a
/// growing wait and at most five attempts, the list's visibility (own / team / settings key / another tenant 404), the
/// settings (409, address checks, audited without the address), the Platform request (the internal key, no user token, the
/// contract's shape), and on a real mongod: one row per subject, ONE winner of two concurrent claims, the list's tenant
/// filter.
/// </summary>
public sealed class VwW5NotificationsTests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000e5");
    private static readonly Guid OtherTenant = Guid.Parse("11110000-0000-0000-0000-0000000000e5");
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);
    private const string Rep = "rep-1";
    private const string Rep2 = "rep-2";
    private const string Stranger = "rep-x";
    private const string Manager = "mgr-1";

    private readonly W5Notifications _n = new(Now);
    private readonly FakePlannedVisitRepository _plans = new();
    private readonly FakeVisitReportRepository _reports = new();
    private readonly FakeContactRepository _contacts = new();
    private readonly FakeAccountRepository _accounts = new();

    // ═══ 1 · the overdue reminder (K-N7 a) ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task N1_an_expired_visit_reminds_the_line_manager_once_and_only_in_a_tenant_whose_settings_turn_it_on()
    {
        var expired = Plan(Today.AddDays(-3));                       // 17 Oct: locked since 20 Oct 00:00Z
        var reported = Plan(Today.AddDays(-3));
        _reports.Items.Add(new ReportEntity { TenantId = Tenant, PlannedVisitId = reported.Id, ReportStatus = VisitReportStatus.Submitted, ExecutionOutcome = VisitExecutionOutcome.Completed });
        var cancelled = Plan(Today.AddDays(-3), PlannedVisitStatus.Cancelled);
        var stillOpen = Plan(Today.AddDays(-1));                      // the 48 h window is still open
        var ancient = Plan(Today.AddDays(-30));                       // outside the look-back: never mailed
        var draftPlan = Plan(Today.AddDays(-3), PlannedVisitStatus.Draft);
        var worker = Worker();

        // no settings document: nothing is swept (no backlog mailed the day this ships)
        Assert.Equal(new VisitNotificationWorker.TickResult(0, 0), await worker.RunOnceAsync(Now, "w1", default));
        Assert.Empty(_n.Platform.Requests);

        _n.Settings.Items.Add(new VisitNotificationSettings { TenantId = Tenant, OverdueReminders = true });
        var first = await worker.RunOnceAsync(Now, "w1", default);
        Assert.Equal(1, first.Overdue);
        var row = Assert.Single(_n.Rows.Items);
        Assert.Equal((VisitNotificationEvents.ReportOverdue, expired.Id, VisitNotificationStatuses.Sent), (row.EventCode, row.SubjectId, row.Status));
        var request = Assert.Single(_n.Platform.Requests);
        Assert.Equal(Rep, Assert.Single(request.To).LineManagerOf);
        Assert.Equal("2026-10-19T23:59:59Z", request.Variables["deadline"]);

        // the next ticks: the same visit never reminds again
        await worker.RunOnceAsync(Now.AddMinutes(15), "w1", default);
        await worker.RunOnceAsync(Now.AddHours(5), "w2", default);
        Assert.Single(_n.Platform.Requests);
        Assert.DoesNotContain(_n.Rows.Items, r => r.SubjectId == reported.Id || r.SubjectId == cancelled.Id || r.SubjectId == stillOpen.Id
                                                  || r.SubjectId == ancient.Id || r.SubjectId == draftPlan.Id);

        // the switch off: nothing new
        _n.Settings.Items[0].OverdueReminders = false;
        Plan(Today.AddDays(-4));
        await worker.RunOnceAsync(Now.AddHours(6), "w1", default);
        Assert.Single(_n.Platform.Requests);
    }

    // ═══ 2 · the retries (K-N7 b, K-N8) ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task N2_a_failed_row_is_retried_when_due_with_a_growing_wait_and_gives_up_after_five_attempts()
    {
        _n.Platform.Down = true;
        var sender = _n.Sender();
        var row = NewRow(Rep, VisitNotificationEvents.ReportSubmitted);
        Assert.True(await sender.QueueAndSendAsync(row, default));
        Assert.Equal((VisitNotificationStatuses.Failed, 1), (row.Status, row.Attempts));

        var worker = Worker();
        Task<VisitNotificationWorker.TickResult> Tick(DateTimeOffset at) { _n.Clock.Value = at; return worker.RunOnceAsync(at, "w1", default); }
        Assert.Equal(0, (await Tick(Now.AddMinutes(4))).Retried);  // not due yet (5 min)
        var expectedWaits = new[] { 10, 20, 40 };
        var at = Now.AddMinutes(5);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(1, (await Tick(at)).Retried);
            Assert.Equal((i + 2, (at + TimeSpan.FromMinutes(expectedWaits[i])).UtcTicks), (row.Attempts, row.NextAttemptTicks!.Value));
            at += TimeSpan.FromMinutes(expectedWaits[i]);
        }

        Assert.Equal(1, (await Tick(at)).Retried);                                       // the 5th attempt fails too
        Assert.Equal((5, (long?)null), (row.Attempts, row.NextAttemptTicks));
        Assert.Contains(VisitNotificationNotes.AttemptsExhausted, row.Notes);
        Assert.Equal(0, (await Tick(at.AddDays(1))).Retried);                            // never again
        Assert.Equal(5, _n.Platform.Requests.Count);

        // a second row: the Platform back before its next attempt → sent on the retry
        _n.Clock.Value = Now;
        _n.Platform.Down = true;
        var row2 = NewRow(Rep2, VisitNotificationEvents.ReportOverdue);
        await sender.QueueAndSendAsync(row2, default);
        _n.Platform.Down = false;
        await Tick(Now.AddMinutes(6));
        Assert.Equal((VisitNotificationStatuses.Sent, 2, (long?)null), (row2.Status, row2.Attempts, row2.NextAttemptTicks));

        // a queued row whose sender died is picked up after 10 minutes
        var orphan = NewRow(Rep2, VisitNotificationEvents.AdverseEvent);
        orphan.Status = VisitNotificationStatuses.Queued;
        orphan.CreatedTicks = Now.UtcTicks;
        await _n.Rows.TryInsertAsync(orphan, default);
        Assert.Equal(0, (await Tick(Now.AddMinutes(9))).Retried);
        Assert.Equal(1, (await Tick(Now.AddMinutes(11))).Retried);
        Assert.Equal(VisitNotificationStatuses.Sent, orphan.Status);
    }

    [Fact]
    public async Task N3_no_recipient_resolved_is_its_own_final_status_and_a_claimed_row_is_skipped_by_another_worker()
    {
        _n.Platform.Status = VisitNotificationStatuses.NoRecipient;
        var row = NewRow(Rep, VisitNotificationEvents.ReportSubmitted);
        await _n.Sender().QueueAndSendAsync(row, default);
        Assert.Equal((VisitNotificationStatuses.NoRecipient, (long?)null), (row.Status, row.NextAttemptTicks));

        // a failed row claimed by worker A (claim valid 2 minutes) is not handed to worker B
        _n.Platform.Down = true;
        var failed = NewRow(Rep2, VisitNotificationEvents.ReportSubmitted);
        await _n.Sender().QueueAndSendAsync(failed, default);
        var due = Now.AddMinutes(6);
        var a = await _n.Rows.ClaimNextAsync(due.UtcTicks, due.UtcTicks, due.AddMinutes(2).UtcTicks, "A", 5, default);
        var b = await _n.Rows.ClaimNextAsync(due.UtcTicks, due.UtcTicks, due.AddMinutes(2).UtcTicks, "B", 5, default);
        Assert.Equal((failed.Id, (VisitNotification?)null), (a!.Id, b));
    }

    // ═══ 3 · the list (K-N9) ════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task N4_a_rep_sees_their_own_a_manager_the_team_the_settings_key_the_tenant_and_another_tenant_nothing()
    {
        var team = new TeamWorld();
        var mine = Stored(Rep, VisitNotificationEvents.ReportSubmitted);
        var teammate = Stored(Rep2, VisitNotificationEvents.AdverseEvent);
        var stranger = Stored(Stranger, VisitNotificationEvents.ReportOverdue);
        var foreign = Stored(Rep, VisitNotificationEvents.ReportSubmitted, OtherTenant);

        async Task<string[]> Seen(string caller, params string[] perms)
            => (await team.Handlers(_n.Rows, caller, perms).Handle(new ListVisitNotificationsQuery(), default)).Data!.Items
                .Select(i => i.ResourceId).OrderBy(x => x).ToArray();

        Assert.Equal(new[] { Rep }, await Seen(Rep, VisitNotificationPermissions.Read));
        Assert.Equal(new[] { Rep, Rep2 }, await Seen(Manager, VisitNotificationPermissions.Read));   // the team (+ none of their own)
        Assert.Equal(new[] { Rep, Rep2, Stranger }, await Seen("admin", VisitNotificationPermissions.SettingsManage));
        Assert.DoesNotContain(foreign.Id, (await team.Handlers(_n.Rows, "admin", VisitNotificationPermissions.SettingsManage)
            .Handle(new ListVisitNotificationsQuery(), default)).Data!.Items.Select(i => i.Id));

        // the preview: own → 200, outside the scope → 404, another tenant → 404 (no leak); never an address
        var detail = await team.Handlers(_n.Rows, Rep, VisitNotificationPermissions.Read).Handle(new GetVisitNotificationQuery(mine.Id), default);
        Assert.Equal((200, "Okan Tekin"), (detail.StatusCode, detail.Data!.Variables["repName"]));
        Assert.Equal(404, (await team.Handlers(_n.Rows, Rep, VisitNotificationPermissions.Read).Handle(new GetVisitNotificationQuery(stranger.Id), default)).StatusCode);
        Assert.Equal(404, (await team.Handlers(_n.Rows, "admin", VisitNotificationPermissions.SettingsManage).Handle(new GetVisitNotificationQuery(foreign.Id), default)).StatusCode);
        Assert.Equal(200, (await team.Handlers(_n.Rows, Manager, VisitNotificationPermissions.Read).Handle(new GetVisitNotificationQuery(teammate.Id), default)).StatusCode);

        // the filters: event, status, the rep (outside the scope = empty, not an error), bad input = 400
        var byEvent = (await team.Handlers(_n.Rows, "admin", VisitNotificationPermissions.SettingsManage)
            .Handle(new ListVisitNotificationsQuery(EventCode: VisitNotificationEvents.AdverseEvent), default)).Data!;
        Assert.Equal(new[] { teammate.Id }, byEvent.Items.Select(i => i.Id));
        Assert.Equal(0, (await team.Handlers(_n.Rows, Rep, VisitNotificationPermissions.Read)
            .Handle(new ListVisitNotificationsQuery(ResourceId: Stranger), default)).Data!.Total);
        foreach (var bad in new[] { new ListVisitNotificationsQuery(PageSize: 101), new ListVisitNotificationsQuery(EventCode: "crm.other"),
                     new ListVisitNotificationsQuery(Status: "lost"), new ListVisitNotificationsQuery(From: Today, To: Today.AddDays(-1)) })
        {
            Assert.Equal(400, (await team.Handlers(_n.Rows, "admin", VisitNotificationPermissions.SettingsManage).Handle(bad, default)).StatusCode);
        }
    }

    // ═══ 4 · the settings (K-N5, K-N9) ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task N5_the_settings_are_versioned_check_the_addresses_and_are_audited_without_the_address()
    {
        var audit = new VisitWorkspace.RecordingAudit();
        var h = new VisitNotificationSettingsHandlers(TenantFactory.Tenant(Tenant), new NullActorContext(), _n.Settings, audit, _n.Clock);
        var defaults = (await h.Handle(new GetVisitNotificationSettingsQuery(), default)).Data!;
        Assert.Equal((null, null, true, true, 0, true), (defaults.PvEmail, defaults.MslEmail, defaults.OverdueReminders, defaults.ReportSummary, defaults.Version, defaults.IsDefault));

        foreach (var bad in new[] { "pv", "pv@", "pv@test", "a b@test.com", "pv@test.com, msl@test.com", "Display <pv@test.com>", new string('a', 250) + "@x.com" })
        {
            var res = await h.Handle(new UpdateVisitNotificationSettingsCommand(new(bad, null, null, null, null)), default);
            Assert.True(res.StatusCode == 400 && res.Errors.Contains(VisitNotificationErrors.EmailInvalid), bad);
        }

        var created = await h.Handle(new UpdateVisitNotificationSettingsCommand(new(" pv@grandmedical.test ", null, false, null, null)), default);
        Assert.Equal((200, "pv@grandmedical.test", (string?)null, false, true), (created.StatusCode, created.Data!.PvEmail, created.Data.MslEmail, created.Data.OverdueReminders, created.Data.ReportSummary));
        var ev = Assert.Single(audit.Events);
        Assert.Equal(VisitNotificationSettingsHandlers.AuditEvent, ev.Event);
        Assert.Equal("pvEmail=set;mslEmail=unset;overdueReminders=false;reportSummary=true", ev.Detail);
        Assert.DoesNotContain("@", ev.Detail);

        // a create without the version while one exists / a stale version → 409; the right version → replaced; "" clears
        Assert.Equal(409, (await h.Handle(new UpdateVisitNotificationSettingsCommand(new(null, "msl@grandmedical.test", null, null, null)), default)).StatusCode);
        Assert.Equal(409, (await h.Handle(new UpdateVisitNotificationSettingsCommand(new(null, "msl@grandmedical.test", null, null, 7)), default)).StatusCode);
        var replaced = await h.Handle(new UpdateVisitNotificationSettingsCommand(new("", "msl@grandmedical.test", null, null, 0)), default);
        Assert.Equal(((string?)null, "msl@grandmedical.test", 1, false), (replaced.Data!.PvEmail, replaced.Data.MslEmail, replaced.Data.Version, replaced.Data.OverdueReminders));
        _n.Settings.StaleVersion = 1;
        Assert.Equal(409, (await h.Handle(new UpdateVisitNotificationSettingsCommand(new(null, null, true, null, 1)), default)).StatusCode);

        // another tenant reads its own defaults
        var other = new VisitNotificationSettingsHandlers(TenantFactory.Tenant(OtherTenant), new NullActorContext(), _n.Settings, audit, _n.Clock);
        Assert.True((await other.Handle(new GetVisitNotificationSettingsQuery(), default)).Data!.IsDefault);
    }

    // ═══ 5 · the Platform request (K-N1 / K-N8) ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task N6_the_platform_request_goes_direct_with_the_internal_key_and_no_user_token_in_the_contracts_shape()
    {
        var handler = new PlatformHandler();
        handler.Answer = _ => Json("{\"data\":{\"dispatches\":[{\"dispatchId\":\"d-1\",\"locale\":\"tr\",\"status\":\"queued\"},{\"dispatchId\":\"d-2\",\"locale\":\"en\",\"status\":\"queued\"}]," +
                                   "\"resolved\":{\"to\":[{\"userId\":\"mgr-1\",\"via\":\"line-manager\"}],\"cc\":[{\"via\":\"email\"}]},\"status\":\"queued\"},\"statusCode\":200,\"isSuccessful\":true}");
        var client = Client(handler, "internal-key-1");
        var result = await client.DispatchAsync(new PlatformNotificationRequest(Tenant, VisitNotificationEvents.AdverseEvent, "MOD-0155",
            [new(Email: "pv@grandmedical.test")], [new(LineManagerOf: Rep)], new Dictionary<string, string> { ["repName"] = "Okan" }, "crm:x"), default);

        Assert.True(result.Available);
        Assert.Equal(new[] { "d-1", "d-2" }, result.DispatchIds);
        Assert.Equal(("mgr-1", "line-manager"), (result.To.Single().UserId, result.To.Single().Via));
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("http://platform.test/api/internal/notifications/events/dispatch", sent.RequestUri!.ToString());
        Assert.Equal("internal-key-1", sent.Headers.GetValues("X-Internal-Api-Key").Single());
        Assert.Null(sent.Headers.Authorization);                                         // the user token is never forwarded
        var body = JsonDocument.Parse(handler.Bodies.Single()).RootElement;
        Assert.Equal(Tenant.ToString("D"), body.GetProperty("tenantId").GetString());
        Assert.Equal(("MOD-0155", "crm:x"), (body.GetProperty("sourceModule").GetString(), body.GetProperty("correlationId").GetString()));
        Assert.Equal("pv@grandmedical.test", body.GetProperty("recipients").GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal(Rep, body.GetProperty("recipients").GetProperty("cc")[0].GetProperty("lineManagerOf").GetString());
        Assert.False(body.GetProperty("recipients").GetProperty("cc")[0].TryGetProperty("email", out _));
        Assert.Equal("Okan", body.GetProperty("variables").GetProperty("repName").GetString());

        // the no-recipient answer, the failures — never thrown
        handler.Answer = _ => Json("{\"data\":{\"dispatches\":[],\"resolved\":{\"to\":[],\"cc\":[]},\"status\":\"no-recipient\"},\"isSuccessful\":true}");
        Assert.Equal("no-recipient", (await client.DispatchAsync(Request(), default)).Status);
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable })
        {
            handler.Answer = _ => new HttpResponseMessage(status);
            Assert.False((await client.DispatchAsync(Request(), default)).Available, status.ToString());
        }

        handler.Answer = _ => Json("not json");
        Assert.False((await client.DispatchAsync(Request(), default)).Available);
        handler.Answer = _ => throw new HttpRequestException("down");
        Assert.False((await client.DispatchAsync(Request(), default)).Available);

        // without the internal key the Platform is not even asked
        var calls = handler.Requests.Count;
        Assert.Equal("internal-api-key-missing", (await Client(handler, "").DispatchAsync(Request(), default)).Error);
        Assert.Equal(calls, handler.Requests.Count);
    }

    [Fact]
    public async Task N7_the_hosted_scheduler_is_off_by_configuration_and_a_failing_tick_never_stops_it()
    {
        var off = new VisitNotificationHostedService(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Config(new() { ["Crm:Notifications:Enabled"] = "false" }), NullLogger<VisitNotificationHostedService>.Instance);
        Assert.False(off.Enabled);
        await off.StartAsync(default);
        await off.StopAsync(default);

        var on = new VisitNotificationHostedService(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Config(new() { ["Crm:Notifications:IntervalMinutes"] = "7" }), NullLogger<VisitNotificationHostedService>.Instance);
        Assert.True(on.Enabled);
        Assert.Equal(TimeSpan.FromMinutes(7), on.Interval);
        await on.TickAsync(default);                                                     // no worker registered: logged, not thrown
    }

    // ═══ 6 · the real mongod (the lane's own port; skipped without CRM_TEST_MONGO_URI) ═════════════════════════

    [Fact]
    public async Task N8_on_a_real_mongod_one_row_per_subject_one_winner_of_two_concurrent_claims_and_the_list_filters_the_tenant()
    {
        var uri = Environment.GetEnvironmentVariable("CRM_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri) || uri.Contains(":27017", StringComparison.Ordinal)) return; // never the dev database
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var client = new MongoClient(uri);
        var name = "W5_BE_" + Guid.NewGuid().ToString("N");
        try
        {
            var db = client.GetDatabase(name);
            typeof(Diten.CrmService.Persistence.DependencyInjection)
                .GetMethod("TryEnsureIndexes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, [db]);
            var repo = new VisitNotificationRepository(db);

            // one row per (tenant, event, subject): the database refuses the second one (another event / tenant is fine)
            var subject = Guid.NewGuid();
            Assert.True(await repo.TryInsertAsync(Row(Tenant, VisitNotificationEvents.ReportSubmitted, subject, Rep), default));
            Assert.False(await repo.TryInsertAsync(Row(Tenant, VisitNotificationEvents.ReportSubmitted, subject, Rep), default));
            Assert.True(await repo.TryInsertAsync(Row(Tenant, VisitNotificationEvents.AdverseEvent, subject, Rep), default));
            Assert.True(await repo.TryInsertAsync(Row(OtherTenant, VisitNotificationEvents.ReportSubmitted, subject, Rep), default));

            // two workers claiming at once: ONE gets the due row
            var due = Row(Tenant, VisitNotificationEvents.ReportOverdue, Guid.NewGuid(), Rep2);
            due.Status = VisitNotificationStatuses.Failed;
            due.NextAttemptTicks = Now.UtcTicks;
            due.Attempts = 1;
            await repo.TryInsertAsync(due, default);
            var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
                repo.ClaimNextAsync(Now.UtcTicks, 0, Now.AddMinutes(2).UtcTicks, "w" + i, 5, default)));
            Assert.Single(claims, c => c is not null);
            Assert.Equal(due.Id, claims.Single(c => c is not null)!.Id);
            Assert.Null(await repo.ClaimNextAsync(Now.AddMinutes(1).UtcTicks, 0, Now.AddMinutes(3).UtcTicks, "late", 5, default)); // still claimed

            // the list: this tenant only (rows and total), newest first, the reps filter
            var page = await repo.ListAsync(Tenant, new VisitNotificationFilter(), 0, 10, default);
            Assert.Equal(3, page.Total);
            Assert.All(page.Items, r => Assert.Equal(Tenant, r.TenantId));
            var narrowed = await repo.ListAsync(Tenant, new VisitNotificationFilter(Reps: [Rep2], From: Today.AddDays(-1), To: Today), 0, 10, default);
            Assert.Equal(due.Id, Assert.Single(narrowed.Items).Id);

            // the settings: the tenant source of the overdue sweep
            var settings = new VisitNotificationSettingsRepository(db);
            Assert.True(await settings.InsertAsync(new VisitNotificationSettings { TenantId = Tenant, OverdueReminders = true }, default));
            Assert.False(await settings.InsertAsync(new VisitNotificationSettings { TenantId = Tenant }, default));
            Assert.True(await settings.InsertAsync(new VisitNotificationSettings { TenantId = OtherTenant, OverdueReminders = false }, default));
            Assert.Equal(new[] { Tenant }, await settings.ListTenantsWithOverdueRemindersAsync(default));
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    // ═══ helpers ═════════════════════════════════════════════════════════════════════════════════════════

    private VisitNotificationWorker Worker() => _n.Worker(_contacts, _accounts, _reports, _plans);

    private PlanAtom Plan(DateOnly date, string status = PlannedVisitStatus.Planned)
    {
        var plan = new PlanAtom
        {
            Id = Guid.NewGuid(), TenantId = Tenant, VisitCode = "W5-" + _plans.Items.Count, TargetType = PlannedVisitTargetType.Contact,
            TargetId = Guid.NewGuid(), PlannedDate = date, PlanStatus = status,
            Resource = new PlannedVisitResourceRef { ResourceId = Rep, DisplayName = "Okan Tekin" }
        };
        _plans.Items.Add(plan);
        return plan;
    }

    private static VisitNotification NewRow(string rep, string eventCode) => new()
    {
        Id = Guid.NewGuid(), TenantId = Tenant, EventCode = eventCode, SubjectId = Guid.NewGuid(), PlannedVisitId = Guid.NewGuid(), ResourceId = rep,
        VisitDate = Today, Recipients = { new VisitNotificationRecipient { Role = VisitNotificationRoles.To, Kind = VisitNotificationRecipientKinds.LineManager, UserId = rep } },
        Variables = new Dictionary<string, string> { ["repName"] = "Okan Tekin" }
    };

    private VisitNotification Stored(string rep, string eventCode, Guid? tenant = null)
    {
        var row = NewRow(rep, eventCode);
        row.TenantId = tenant ?? Tenant;
        row.Status = VisitNotificationStatuses.Sent;
        row.CreatedTicks = Now.UtcTicks + _n.Rows.Items.Count;
        _n.Rows.Items.Add(row);
        return row;
    }

    private static VisitNotification Row(Guid tenant, string eventCode, Guid subject, string rep) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant, EventCode = eventCode, SubjectId = subject, PlannedVisitId = Guid.NewGuid(), ResourceId = rep,
        VisitDate = Today, Status = VisitNotificationStatuses.Sent, CreatedTicks = Now.UtcTicks, CorrelationId = "c",
        Variables = new Dictionary<string, string> { ["repName"] = "x" }
    };

    private static PlatformNotificationRequest Request()
        => new(Tenant, VisitNotificationEvents.ReportSubmitted, "MOD-0155", [new(LineManagerOf: Rep)], [], new Dictionary<string, string>(), "c");

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static PlatformNotificationClient Client(HttpMessageHandler handler, string key)
        => new(new HttpClient(handler), Config(new() { ["PlatformService:BaseUrl"] = "http://platform.test" }),
            Options.Create(new AuthServiceOptions { InternalApiKey = key }));

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class PlatformHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Answer { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            return Answer(request);
        }
    }

    /// <summary>A territory with a manager over Rep + Rep2 (a subtree assignment on their parent node) and a stranger.</summary>
    private sealed class TeamWorld
    {
        private readonly Guid _model = Guid.NewGuid(), _root = Guid.NewGuid(), _a = Guid.NewGuid(), _b = Guid.NewGuid(), _c = Guid.NewGuid();
        public FakeTerritoryModelRepo Models { get; } = new();
        public FakeTerritoryNodeRepo Nodes { get; } = new();
        public FakeTerritoryResourceAssignmentRepo Assignments { get; } = new();

        public TeamWorld()
        {
            var past = Now.AddYears(-1);
            Models.Items.Add(new TerritoryModel { Id = _model, TenantId = Tenant, Status = "active", EffectiveFrom = past });
            TerritoryNode Node(Guid id, Guid? parent) => new() { Id = id, TenantId = Tenant, ModelId = _model, ParentTerritoryId = parent, Status = "active", EffectiveFrom = past, Name = id.ToString("N")[..4] };
            Nodes.Items.AddRange(new[] { Node(_root, null), Node(_a, _root), Node(_b, _root), Node(_c, null) });
            TerritoryResourceAssignment Assign(string r, Guid node, string scope) => new()
            {
                TenantId = Tenant, ModelId = _model, TerritoryId = node, Resource = new TerritoryResourceRef { ResourceId = r, DisplayName = r },
                CoverageScope = scope, Status = "active", IsPrimary = true, ValidFrom = past
            };
            Assignments.Items.AddRange(new[] { Assign(Rep, _a, "exact-territory"), Assign(Rep2, _b, "exact-territory"), Assign(Stranger, _c, "exact-territory"), Assign(Manager, _root, "territory-subtree") });
        }

        public VisitNotificationQueryHandlers Handlers(IVisitNotificationRepository rows, string caller, params string[] perms)
        {
            var callerScope = new TestCallerScope(caller, perms);
            var scope = new VisitEvidenceFlagScopeResolver(callerScope, new TeamScopeResolver(Assignments, Models, Nodes), Models, Assignments);
            return new VisitNotificationQueryHandlers(TenantFactory.Tenant(Tenant), callerScope, rows, scope, new W5Clock(Now));
        }
    }
}
