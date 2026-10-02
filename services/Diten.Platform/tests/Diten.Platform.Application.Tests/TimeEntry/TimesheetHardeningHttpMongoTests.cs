using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.WorkingCalendar.Provider;
using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// WP-TIMESHEET-HARDENING-01 — the robustness items deferred at acceptance, each measured on the production classes
/// against the disposable mongod (the host's client reports every command, so a "read" is a round trip):
/// <list type="bullet">
/// <item>K1 (BL-479) — a backlog of more than 200 undecided weeks does not keep a newer approved week from its totals.</item>
/// <item>K2 (BL-483/2) — a finalization failure the puller swallows is a failure, never a change.</item>
/// <item>K3 (BL-483/1) — a withdrawal is "too late" only when somebody decided; anything else is a retryable conflict
/// or simply goes through.</item>
/// <item>K4 (BL-484) — the working calendar's inputs and the approval engine are read once for a page / a queue, and
/// the batched form answers exactly what the one-by-one form answers.</item>
/// <item>K5 (BL-488/1) — reminder recipients are resolved in groups of 100; a deactivated person is still no recipient.</item>
/// </list>
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetHardeningHttpMongoTests : TimerScenario
{
    private static readonly DateOnly PreviousMonday = Monday.AddDays(-7);          // 2026-09-28, W40
    private const string PreviousWeek = "2026-W40";

    /// <summary>Monday 2026-10-12 09:00 in Istanbul: the reminder for W41 is due.</summary>
    private static readonly DateTimeOffset MondayNine = new(2026, 10, 12, 6, 0, 0, TimeSpan.Zero);

    private readonly MongoReadCounter _reads = new();
    private readonly CapturedLogs _logs = new();
    private readonly CancelSwitch _cancel = new();
    private readonly ScopeEcho _scopeEcho = new();
    private readonly IPlatformDbContext _observed;
    private readonly ITestOutputHelper _output;

    public TimesheetHardeningHttpMongoTests(TimeEntryMongoFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _observed = fixture.ObservedDbContext(_reads.Record);
        _output = output;
    }

    protected override IPlatformDbContext HostDbContext => _observed;

    protected override void ConfigureHost(IServiceCollection services)
    {
        services.AddSingleton<ILoggerProvider>(_logs);

        // The REAL approval service; only its cancel can be told to answer like a MOD-0023 that refused (K3).
        services.AddScoped<TimesheetApprovalService>();
        services.AddScoped<ITimesheetApprovalService>(sp =>
            new SwitchedApprovals(sp.GetRequiredService<TimesheetApprovalService>(), _cancel));

        // The host's calendar; when a test asks, every Tuesday becomes a holiday NAMED after the scope it was asked for,
        // so the scope a person resolved to is visible in the provider's answer (K4a).
        services.AddSingleton<IWorkingCalendarProvider>(_ => new ScopeEchoCalendar(Host.Calendar, _scopeEcho));
    }

    /// <summary>The jobs walk ACTIVE tenants only (the registry's own filter).</summary>
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await Collection<Tenant>(PlatformCollections.Tenants).UpdateOneAsync(
            t => t.Id == Tenant, Builders<Tenant>.Update.Set(t => t.Status, TenantStatus.Active));
    }

    // ── K1 (BL-479) — the sweep is not starved ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task K1_more_than_200_older_undecided_weeks_do_not_keep_a_newer_approved_week_from_its_totals_in_the_same_sweep()
    {
        // An approved week whose totals never landed (the totals write failed once — F12).
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA));
        Ok(await DecideAsync(Manager, weekId, approve: true));
        Host.Probes.BeforeTaskTotalWrite = _ => throw new InvalidOperationException("store down while writing totals");
        await GetWeekAsync();
        Host.Probes.BeforeTaskTotalWrite = null;
        var approved = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Approved, approved.Status);
        Assert.Null(approved.TotalsAppliedAtUtc);
        Assert.Null(await ApprovedMinutesAsync(TaskA));

        // 205 weeks submitted BEFORE it, every one still waiting for its approver (an open MOD-0023 instance).
        var backlog = await SeedUndecidedBacklogAsync(205, before: approved.SubmittedAtUtcTicks ?? Wednesday.UtcTicks);

        await RunSweepAsync(maxWeeksPerTenant: 200);

        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));
        Assert.NotNull((await StoredWeekAsync(weekId)).TotalsAppliedAtUtc);
        // The backlog was looked at, not decided: every one of them is still submitted.
        Assert.Equal(205, await Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).CountDocumentsAsync(
            w => w.TenantId == Tenant && backlog.Contains(w.Id) && w.Status == TimesheetWeekStatus.Submitted));
    }

    // ── K2 (BL-483/2) — a swallowed failure is not a change ──────────────────────────────────────────────────────

    [Fact]
    public async Task K2_a_swallowed_finalization_failure_is_counted_as_a_failure_never_as_a_change()
    {
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA));
        Ok(await DecideAsync(Manager, weekId, approve: true));
        // The failure's own message names the person — exactly what must not reach the log LINE (the line is what a
        // search counts and an alert quotes). The exception itself travels beside it, for whoever has to diagnose it.
        Host.Probes.BeforeTaskTotalWrite = _ => throw new InvalidOperationException($"store down for user {Person}");

        // The puller: the failure is swallowed (F14) and reported as a failure. The read still re-reads.
        var failed = await PullAsync(weekId);
        Assert.Equal(new TimesheetPullResult(Applied: 0, Failed: 1, Attempted: 1), failed);
        Assert.True(failed.ShouldReread);

        // The sweep's counters. One run first, so anything other tenants of this database left behind is settled.
        await RunSweepAsync();
        _logs.Clear();
        await RunSweepAsync();

        var sweep = Assert.Single(_logs.Starting("time-entry.decision.sweep.completed"));
        Assert.Equal(0, sweep.Int("Finalized"));
        Assert.Equal(0, sweep.Int("AppliedWeeks"));
        Assert.True(sweep.Int("FailedWeeks") >= 1, sweep.Message);

        var failure = Assert.Single(_logs.Starting("time-entry.decision.pull.failed"), l => Equals(l.Values["WeekId"], (Guid?)weekId));
        Assert.Equal(TimesheetDecisionPuller.ThrewReasonCode, failure.Values["ReasonCode"]);
        Assert.Equal(nameof(InvalidOperationException), failure.Values["ExceptionType"]);
        // CT acceptance — without the exception every finalizer failure is the same line ("InvalidOperationException").
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.DoesNotContain(Person.ToString(), failure.Message, StringComparison.OrdinalIgnoreCase);

        // When the totals do land, THAT is a change — and a pull with nothing to do is neither.
        Host.Probes.BeforeTaskTotalWrite = null;
        Assert.Equal(new TimesheetPullResult(Applied: 1, Failed: 0, Attempted: 1), await PullAsync(weekId));
        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));
        var nothing = await PullAsync(weekId);
        Assert.Equal(TimesheetPullResult.Nothing, nothing);
        Assert.False(nothing.ShouldReread);
    }

    // CT acceptance — a finalization that RAN and found nothing left to do is an attempt: not a change, not a failure.
    // (The reader's copy of the week is older than the store's — somebody else's pull got there first.)
    [Fact]
    public async Task K2_a_pull_that_finds_the_week_already_finalized_counts_an_attempt_and_no_change()
    {
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA));
        Ok(await DecideAsync(Manager, weekId, approve: true));
        var stale = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Submitted, stale.Status);

        Assert.Equal(new TimesheetPullResult(Applied: 1, Failed: 0, Attempted: 1), await PullAsync(weekId));

        var late = await InTenantAsync(sp =>
            sp.GetRequiredService<ITimesheetDecisionPuller>().PullAsync([stale], Guid.NewGuid().ToString()));

        Assert.Equal(new TimesheetPullResult(Applied: 0, Failed: 0, Attempted: 1), late);
        Assert.True(late.ShouldReread);
        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));
    }

    // CT acceptance — the ONE shared MOD-0023 read (BL-484) is also one shared point of failure. The sweep takes the
    // same oldest weeks every run, so a single instance that cannot be read must cost its own week and nothing else.
    [Fact]
    public async Task K4c_an_instance_that_cannot_be_read_costs_only_its_own_week_not_the_rest_of_the_queue()
    {
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA));
        Ok(await DecideAsync(Manager, weekId, approve: true));
        var decided = await StoredWeekAsync(weekId);

        var unreadable = await SeedWeekAsync(TimesheetWeekStatus.Submitted, WorkflowInstanceStatus.Active);
        var instances = Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances);
        try
        {
            // A stored status no build of this code can read: every read that returns this document throws.
            await instances.UpdateOneAsync(
                i => i.Id == unreadable.WorkflowInstanceId,
                new BsonDocumentUpdateDefinition<WorkflowInstance>(
                    new BsonDocument("$set", new BsonDocument(nameof(WorkflowInstance.Status), "NoSuchStatus"))));
            _logs.Clear();

            var result = await InTenantAsync(sp => sp.GetRequiredService<ITimesheetDecisionPuller>()
                .PullAsync([unreadable, decided], Guid.NewGuid().ToString()));

            Assert.Equal(new TimesheetPullResult(Applied: 1, Failed: 1, Attempted: 1), result);
            Assert.Equal(TimesheetWeekStatus.Approved, (await StoredWeekAsync(weekId)).Status);
            Assert.Equal(120, await ApprovedMinutesAsync(TaskA));
            Assert.Equal(TimesheetWeekStatus.Submitted, (await StoredWeekAsync(unreadable.Id)).Status);

            // Said twice, and told apart: the shared read failed (no week), then THIS week's own read failed.
            var failures = _logs.Starting("time-entry.decision.pull.failed");
            Assert.Contains(failures, l => l.Values["WeekId"] is null
                && Equals(l.Values["ReasonCode"], TimesheetDecisionPuller.OutcomeReadFailedReasonCode));
            var own = Assert.Single(failures, l => Equals(l.Values["WeekId"], (Guid?)unreadable.Id));
            Assert.Equal(TimesheetDecisionPuller.OutcomeReadFailedReasonCode, own.Values["ReasonCode"]);
            Assert.NotNull(own.Exception);
            Assert.DoesNotContain(failures, l => Equals(l.Values["WeekId"], (Guid?)weekId));
        }
        finally
        {
            // Nothing this test made unreadable stays behind for the sweeps the other tests of this database run.
            await instances.DeleteOneAsync(i => i.Id == unreadable.WorkflowInstanceId);
            await Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).DeleteOneAsync(w => w.Id == unreadable.Id);
        }
    }

    // ── K3 (BL-483/1) — "too late" only when somebody decided ────────────────────────────────────────────────────

    [Fact]
    public async Task K3_a_cancel_the_approval_engine_refuses_while_nobody_decided_is_a_retryable_conflict_not_too_late()
    {
        var weekId = await SubmittedWeekAsync();
        var submitted = await StoredWeekAsync(weekId);
        var instanceId = submitted.WorkflowInstanceId!.Value;
        var instanceStatus = (await StoredInstanceAsync(instanceId)).Status;
        _cancel.Next = CancelAnswer.RefuseWithoutCancelling; // MOD-0023 lost a concurrent write: nothing changed

        var refused = await WithdrawAsync();

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.ConcurrencyConflict, refused.ReasonCode);
        var untouched = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Submitted, untouched.Status);
        Assert.Equal(instanceId, untouched.WorkflowInstanceId);
        Assert.Equal(submitted.Version, untouched.Version);
        Assert.Null(untouched.WithdrawnAtUtc);
        Assert.Equal(instanceStatus, (await StoredInstanceAsync(instanceId)).Status);

        // Retryable: the same request again simply withdraws.
        var retried = await WithdrawAsync();
        Assert.Equal(HttpStatusCode.OK, retried.Status);
        var withdrawn = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Draft, withdrawn.Status);
        Assert.NotNull(withdrawn.WithdrawnAtUtc);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await StoredInstanceAsync(instanceId)).Status);
    }

    [Fact]
    public async Task K3_a_partial_cancel_is_a_conflict_not_too_late_and_the_retry_withdraws()
    {
        var weekId = await SubmittedWeekAsync();
        var submitted = await StoredWeekAsync(weekId);
        var instanceId = submitted.WorkflowInstanceId!.Value;
        _cancel.Next = CancelAnswer.CancelThenRefuse; // MOD-0023 cancelled what it could and refused the rest

        var refused = await WithdrawAsync();

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.ConcurrencyConflict, refused.ReasonCode);
        Assert.Equal(TimesheetWeekStatus.Submitted, (await StoredWeekAsync(weekId)).Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await StoredInstanceAsync(instanceId)).Status);

        // The version comes from the store: a read would return the week to Draft through the finalizer first.
        var retried = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/withdraw", PersonToken(),
            new { expectedVersion = submitted.Version });

        Assert.Equal(HttpStatusCode.OK, retried.Status);
        var withdrawn = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Draft, withdrawn.Status);
        Assert.Null(withdrawn.WorkflowInstanceId);
        Assert.NotNull(withdrawn.WithdrawnAtUtc);
    }

    [Fact]
    public async Task K3_an_approval_cancelled_by_somebody_else_is_not_a_decision_and_the_withdraw_goes_through()
    {
        var weekId = await SubmittedWeekAsync();
        var submitted = await StoredWeekAsync(weekId);
        var approvalTask = await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.WorkflowInstanceId == submitted.WorkflowInstanceId && t.Status == ApprovalTaskStatus.WaitingApproval)
            .SingleAsync();
        var cancelled = await InTenantAsync(sp => sp.GetRequiredService<IMediator>().Send(new CancelWorkflowTaskCommand(
            approvalTask.Id,
            new CancelWorkflowTaskRequest(Manager.ToString(), "CALLED_OFF", Guid.NewGuid().ToString("N"), null),
            Guid.NewGuid().ToString())));
        Assert.True(cancelled.IsSuccessful, cancelled.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await StoredInstanceAsync(submitted.WorkflowInstanceId!.Value)).Status);

        var withdrawn = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/withdraw", PersonToken(),
            new { expectedVersion = submitted.Version });

        Assert.True(withdrawn.Status == HttpStatusCode.OK, withdrawn.ToString());
        var week = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Draft, week.Status);
        Assert.NotNull(week.WithdrawnAtUtc);
    }

    // ── K4 (BL-484) — reads per page / per queue ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task K4_the_approvals_page_reads_the_calendar_inputs_and_the_approval_engine_once_whether_the_queue_holds_two_weeks_or_six()
    {
        // The queue grows from one person's two weeks to three people's six; the page shows all of it.
        await SubmitWeekAsync(Person, PreviousWeek, Row(PreviousMonday, 120, category: Category));
        await SubmitWeekAsync(Person, CurrentWeek, Row(Monday, 90, category: Category));
        var two = await MeasureApprovalsAsync();

        foreach (var person in new[] { SecondPerson, Guid.NewGuid() })
        {
            await SubmitWeekAsync(person, PreviousWeek, Row(PreviousMonday, 120, category: Category));
            await SubmitWeekAsync(person, CurrentWeek, Row(Monday, 90, category: Category));
        }

        var six = await MeasureApprovalsAsync();

        _output.WriteLine($"queue of 2: {Describe(two.Reads)}");
        _output.WriteLine($"queue of 6: {Describe(six.Reads)}");
        Assert.Equal(2, two.Items);
        Assert.Equal(6, six.Items);

        // (a) the working calendar's inputs — the tenant, the seats, the home positions, their units;
        // (b) the approval engine's instances — "is an outcome waiting?" for the whole queue.
        foreach (var collection in new[]
                 {
                     PlatformCollections.Tenants, PlatformCollections.PositionAssignments, PlatformCollections.Positions,
                     PlatformCollections.OrganizationUnits, PlatformCollections.WorkflowInstances
                 })
        {
            Assert.True(two.Reads.GetValueOrDefault(collection) > 0, $"{collection} was not read at all — the page no longer measures it");
            Assert.True(
                six.Reads.GetValueOrDefault(collection) == two.Reads.GetValueOrDefault(collection),
                $"{collection}: {two.Reads.GetValueOrDefault(collection)} reads for a queue of 2, {six.Reads.GetValueOrDefault(collection)} for a queue of 6");
        }
    }

    [Fact]
    public async Task K4a_the_batched_working_calendar_answers_exactly_what_the_single_question_answers_for_every_scope_leg()
    {
        _scopeEcho.On = true;
        var otherLegalEntity = Guid.NewGuid();
        var otherUnit = Guid.NewGuid();
        var ghostUnit = Guid.NewGuid();                    // a position points at it; the unit itself does not exist
        await Collection<OrganizationUnit>(PlatformCollections.OrganizationUnits).InsertOneAsync(new OrganizationUnit
        {
            Id = otherUnit, TenantId = Tenant, Code = "U" + otherUnit.ToString("N")[..6], Name = "Sales", LegalEntityId = otherLegalEntity
        });
        var otherSeat = await SeedPositionInAsync(otherUnit);
        var ghostSeat = await SeedPositionInAsync(ghostUnit);

        var primaryFirst = Person;                         // scenario: primary seat in Unit (LegalEntity)
        var secondaryStoredFirst = Guid.NewGuid();         // a secondary seat stored BEFORE the primary one
        await SeatAsync(secondaryStoredFirst, otherSeat, type: AssignmentType.Secondary);
        await SeatAsync(secondaryStoredFirst, PersonSeat);
        var noSeat = Guid.NewGuid();
        var seatWithoutPosition = Guid.NewGuid();
        await SeatAsync(seatWithoutPosition, Guid.NewGuid());
        var positionWithoutUnit = Guid.NewGuid();
        await SeatAsync(positionWithoutUnit, ghostSeat);
        var otherEntity = Guid.NewGuid();
        await SeatAsync(otherEntity, otherSeat);

        var expectedScope = new Dictionary<Guid, string>
        {
            [primaryFirst] = $"{Unit}|{LegalEntity}",
            [secondaryStoredFirst] = $"{Unit}|{LegalEntity}",      // the PRIMARY seat decides, whatever was stored first
            [noSeat] = "|",                                         // the country only
            [seatWithoutPosition] = "|",
            [positionWithoutUnit] = $"{ghostUnit}|",                // the unit id, no legal entity
            [otherEntity] = $"{otherUnit}|{otherLegalEntity}"
        };

        var requests = expectedScope.Keys
            .SelectMany(person => new[]
            {
                new WorkingHoursRequest(person, Monday, Monday.AddDays(6)),
                new WorkingHoursRequest(person, PreviousMonday, PreviousMonday.AddDays(6))
            })
            .Append(new WorkingHoursRequest(primaryFirst, Monday, Monday.AddDays(-1)))      // inverted: no days
            .Append(new WorkingHoursRequest(otherEntity, Monday, Monday.AddDays(400)))      // too long: no days
            .ToList();

        var (batched, reads) = await _reads.MeasureAsync(() => InTenantAsync(sp =>
            sp.GetRequiredService<IWorkingHoursProvider>().GetWorkingWindowsForManyAsync(requests)));
        _output.WriteLine($"batched, {expectedScope.Count} people / {requests.Count} requests: {Describe(reads)}");

        Assert.Equal(requests.Count, batched.Count);
        foreach (var request in requests)
        {
            var single = await InTenantAsync(sp => sp.GetRequiredService<IWorkingHoursProvider>()
                .GetWorkingWindowsAsync(request.UserId, request.From, request.To));
            Assert.Equal(Render(single), Render(batched[request]));
        }

        // And the single answer is the stated one for each leg — the comparison is not two wrongs agreeing.
        foreach (var (person, scope) in expectedScope)
        {
            var tuesday = batched[new WorkingHoursRequest(person, Monday, Monday.AddDays(6))].Days.Single(d => d.Date == Monday.AddDays(1));
            Assert.Equal(WorkingDayKinds.Holiday, tuesday.DayKind);
            Assert.Equal(scope, tuesday.HolidayName);
        }

        Assert.Empty(batched[requests[^2]].Days);
        Assert.Empty(batched[requests[^1]].Days);

        // One read each, for six people.
        foreach (var collection in new[]
                 {
                     PlatformCollections.Tenants, PlatformCollections.PositionAssignments, PlatformCollections.Positions,
                     PlatformCollections.OrganizationUnits
                 })
        {
            Assert.True(reads.GetValueOrDefault(collection) == 1, $"{collection}: {reads.GetValueOrDefault(collection)} reads");
        }
    }

    [Fact]
    public async Task K4b_the_batched_outcome_check_answers_exactly_what_the_per_week_check_answers_for_every_outcome()
    {
        var weeks = new Dictionary<string, TimesheetWeek>
        {
            ["undecided"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, WorkflowInstanceStatus.Active),
            ["escalated"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, WorkflowInstanceStatus.Escalated),
            ["timed out"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, WorkflowInstanceStatus.TimedOut),
            ["approved in MOD-0023"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, WorkflowInstanceStatus.Completed),
            ["approved (status)"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, WorkflowInstanceStatus.Approved),
            ["rejected in MOD-0023"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, WorkflowInstanceStatus.Rejected),
            ["cancelled in MOD-0023"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, WorkflowInstanceStatus.Cancelled),
            ["instance missing"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, instance: null, danglingInstance: true),
            // UNDECIDED over there: read across tenants it would say "still waiting"; read in this tenant it does not exist.
            ["instance of another tenant"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, WorkflowInstanceStatus.Active, instanceTenant: OtherTenant),
            ["submitted, no instance"] = await SeedWeekAsync(TimesheetWeekStatus.Submitted, instance: null),
            ["approved, totals outstanding"] = await SeedWeekAsync(TimesheetWeekStatus.Approved, instance: null),
            ["approved, totals applied"] = await SeedWeekAsync(TimesheetWeekStatus.Approved, instance: null, totalsApplied: true),
            ["draft with a stale decided instance"] = await SeedWeekAsync(TimesheetWeekStatus.Draft, WorkflowInstanceStatus.Completed)
        };
        var all = weeks.Values.ToList();

        var (batched, reads) = await _reads.MeasureAsync(() => InTenantAsync(sp =>
            sp.GetRequiredService<ITimesheetFinalizer>().PendingOutcomeWeekIdsAsync(all)));
        _output.WriteLine($"batched, {all.Count} weeks: {Describe(reads)}");

        var oneByOne = new HashSet<Guid>();
        foreach (var week in all)
        {
            if (await InTenantAsync(sp => sp.GetRequiredService<ITimesheetFinalizer>().HasPendingOutcomeAsync(week)))
            {
                oneByOne.Add(week.Id);
            }
        }

        string[] Names(IEnumerable<Guid> ids) => weeks.Where(w => ids.Contains(w.Value.Id)).Select(w => w.Key).Order().ToArray();
        Assert.Equal(Names(oneByOne), Names(batched));
        Assert.Equal(
            new[]
            {
                "approved (status)", "approved in MOD-0023", "approved, totals outstanding", "cancelled in MOD-0023",
                "instance missing", "instance of another tenant", "rejected in MOD-0023"
            },
            Names(batched));
        Assert.Equal(1, reads.GetValueOrDefault(PlatformCollections.WorkflowInstances));
    }

    // ── K5 (BL-488/1) — reminder recipients in groups ────────────────────────────────────────────────────────────

    [Fact]
    public async Task K5_reminder_recipients_are_resolved_in_groups_of_100_and_a_deactivated_person_is_never_a_recipient()
    {
        var people = Enumerable.Range(0, 123).Select(_ => Guid.NewGuid()).OrderBy(id => id).ToList();
        await Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).InsertManyAsync(people.Select(DraftWeek));
        // Deactivated: AuthService leaves them out of its answer. One in the first group, one on its edge, one in the second.
        var deactivated = new[] { people[4], people[99], people[110] };
        foreach (var person in deactivated)
        {
            Host.Recipients.Omitted.Add(person);
        }

        await SwitchReminderOnAsync();
        var callsBefore = Host.Recipients.Calls;

        await RunReminderAsync(MondayNine);

        Assert.Equal(2, Host.Recipients.Calls - callsBefore);                    // 100 + 23, not 123 questions
        Assert.True(Host.Recipients.LargestCall <= ITimeEntryNotifier.ReminderGroupSize, $"a question about {Host.Recipients.LargestCall} people");

        var reminded = Reminders().Select(r => r.To.Single().Email).ToList();
        Assert.Equal(120, reminded.Count);
        Assert.Equal(
            people.Except(deactivated).Select(TestRecipients.Address).OrderBy(a => a),
            reminded.OrderBy(a => a));
        Assert.DoesNotContain(reminded, address => deactivated.Select(TestRecipients.Address).Contains(address));

        // Nobody who was left out was claimed either, and nobody is reminded twice by a second run.
        var marks = await ReminderMarkKeysAsync();
        Assert.Equal(120, marks.Count);
        Assert.DoesNotContain(marks, key => deactivated.Any(person => key.Contains(person.ToString("N"))));
        await RunReminderAsync(MondayNine.AddHours(1));
        Assert.Equal(120, Reminders().Count);
    }

    [Fact]
    public async Task K5_one_persons_failed_send_does_not_cost_the_rest_of_the_group_their_reminder()
    {
        var people = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).OrderBy(id => id).ToList();
        await Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).InsertManyAsync(people.Select(DraftWeek));
        Host.Dispatch.ThrowFor.Add(TestRecipients.Address(people[0]));           // the FIRST of the group fails
        await SwitchReminderOnAsync();

        await RunReminderAsync(MondayNine);

        // All three were attempted (the double records the request before it throws); the two after the failure got theirs.
        Assert.Equal(
            people.Select(TestRecipients.Address).OrderBy(a => a, StringComparer.Ordinal),
            Reminders().Select(r => r.To.Single().Email).OrderBy(a => a, StringComparer.Ordinal));
        Assert.Equal(3, (await ReminderMarkKeysAsync()).Count);
    }

    // CT acceptance — a send that fails before it reaches anybody still says what it was about. (The group form gave
    // the log line "(none)" for every notification of this module, not only for reminders.)
    [Fact]
    public async Task K5_a_group_whose_recipients_cannot_be_resolved_is_logged_under_its_week_and_claims_nobody()
    {
        var people = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        await Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).InsertManyAsync(people.Select(DraftWeek));
        await SwitchReminderOnAsync();
        Host.Recipients.Unreachable = true;
        try
        {
            _logs.Clear();
            await RunReminderAsync(MondayNine);
        }
        finally
        {
            Host.Recipients.Unreachable = false;
        }

        Assert.Empty(Reminders());
        Assert.Empty(await ReminderMarkKeysAsync());
        var failure = Assert.Single(_logs.Starting(TimeEntryNotificationEvents.WeekReminder), l => l.Exception is not null);
        Assert.Equal(CurrentWeek, failure.Values["Key"]);

        // Nobody was claimed, so the next run reminds all three.
        await RunReminderAsync(MondayNine.AddHours(1));
        Assert.Equal(3, Reminders().Count);
    }

    // ── World building ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Saves the rows into <paramref name="weekKey"/> as <paramref name="person"/> and submits. Somebody with no
    /// seat yet is seated in the person's seat first, so the manager is their approver.</summary>
    private async Task<Guid> SubmitWeekAsync(Guid person, string weekKey, params object[] rows)
    {
        if (!await Collection<PositionAssignment>(PlatformCollections.PositionAssignments)
                .Find(a => a.TenantId == Tenant && a.UserId == person).AnyAsync())
        {
            await SeatAsync(person, PersonSeat);
        }

        var token = PersonToken(person: person);
        var saved = await SaveAsync(await VersionAsync(weekKey, token), weekKey, token, rows);
        Assert.True(saved.Status == HttpStatusCode.OK, saved.ToString());
        var submitted = await SubmitAsync(weekKey, token);
        Assert.True(submitted.Status == HttpStatusCode.OK, submitted.ToString());
        return submitted.Data.GetProperty("weekId").GetGuid();
    }

    private async Task<Guid> SeedPositionInAsync(Guid unit)
    {
        var id = Guid.NewGuid();
        await Collection<Position>(PlatformCollections.Positions).InsertOneAsync(new Position
        {
            Id = id, TenantId = Tenant, Code = "P" + id.ToString("N")[..8], Name = "Seat " + id.ToString("N")[..4],
            OrganizationUnitId = unit
        });
        return id;
    }

    /// <summary>Submitted weeks of people nobody sees, each waiting on an OPEN MOD-0023 instance, all submitted before
    /// <paramref name="before"/>. Returns their ids.</summary>
    private async Task<HashSet<Guid>> SeedUndecidedBacklogAsync(int count, long before)
    {
        var weeks = new List<TimesheetWeek>(count);
        var instances = new List<WorkflowInstance>(count);
        for (var i = 0; i < count; i++)
        {
            var week = NewWeek(TimesheetWeekStatus.Submitted);
            week.SubmittedAtUtcTicks = before - TimeSpan.TicksPerDay + i;
            var instance = NewInstance(week.Id, WorkflowInstanceStatus.Active, Tenant);
            week.WorkflowInstanceId = instance.Id;
            weeks.Add(week);
            instances.Add(instance);
        }

        await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertManyAsync(instances);
        await Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).InsertManyAsync(weeks);
        return weeks.Select(w => w.Id).ToHashSet();
    }

    /// <summary>One stored week in <paramref name="status"/>, optionally with a MOD-0023 instance in
    /// <paramref name="instance"/> (in <paramref name="instanceTenant"/>, this tenant by default) or pointing at an
    /// instance that does not exist.</summary>
    private async Task<TimesheetWeek> SeedWeekAsync(
        TimesheetWeekStatus status, WorkflowInstanceStatus? instance, bool danglingInstance = false,
        Guid? instanceTenant = null, bool totalsApplied = false)
    {
        var week = NewWeek(status);
        week.TotalsAppliedAtUtc = totalsApplied ? Wednesday : null;
        if (instance is { } instanceStatus)
        {
            var stored = NewInstance(week.Id, instanceStatus, instanceTenant ?? Tenant);
            await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertOneAsync(stored);
            week.WorkflowInstanceId = stored.Id;
        }
        else if (danglingInstance)
        {
            week.WorkflowInstanceId = Guid.NewGuid();
        }

        await Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).InsertOneAsync(week);
        return week;
    }

    private TimesheetWeek NewWeek(TimesheetWeekStatus status) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Tenant,
        UserId = Guid.NewGuid(),
        WeekKey = CurrentWeek,
        WeekStartDate = Monday,
        TimeZoneId = Zone,
        RevisionNumber = 1,
        Status = status,
        IsOpen = status is TimesheetWeekStatus.Draft or TimesheetWeekStatus.Submitted,
        InForce = status == TimesheetWeekStatus.Approved,
        SubmittedAtUtcTicks = Wednesday.UtcTicks
    };

    private TimesheetWeek DraftWeek(Guid person)
    {
        var week = NewWeek(TimesheetWeekStatus.Draft);
        week.UserId = person;
        week.SubmittedAtUtcTicks = null;
        return week;
    }

    private static WorkflowInstance NewInstance(Guid weekId, WorkflowInstanceStatus status, Guid tenant)
    {
        var id = Guid.NewGuid();
        return new WorkflowInstance
        {
            Id = id,
            TenantId = tenant,
            TemplateId = Guid.NewGuid(),
            WorkflowTemplateId = Guid.NewGuid(),
            ObjectType = TimeEntryModule.ApprovalObjectType,
            ObjectId = weekId.ToString(),
            ObjectRef = TimesheetApprovalService.ObjectRef(weekId),
            IdempotencyKey = "hardening-test:" + id.ToString("N"),
            Status = status
        };
    }

    // ── Running things ───────────────────────────────────────────────────────────────────────────────────────────

    private async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
        {
            return await action(scope.ServiceProvider);
        }
    }

    private Task<TimesheetPullResult> PullAsync(Guid weekId)
        => InTenantAsync(async sp =>
        {
            var week = await sp.GetRequiredService<ITimesheetWeekRepository>().GetByIdAsync(weekId);
            return await sp.GetRequiredService<ITimesheetDecisionPuller>().PullAsync([week!], Guid.NewGuid().ToString());
        });

    private async Task RunSweepAsync(int maxWeeksPerTenant = 200)
    {
        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TimesheetDecisionSweepJob>().HandleAsync(
            new TimesheetDecisionSweepJobArgs(maxWeeksPerTenant), new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Recurring));
    }

    private async Task RunReminderAsync(DateTimeOffset nowUtc)
    {
        Host.Clock.UtcNow = nowUtc;
        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TimesheetReminderJob>().HandleAsync(
            new TimesheetReminderJobArgs(2000), new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Recurring));
    }

    private async Task SwitchReminderOnAsync()
    {
        var version = (await Host.GetAsync("/api/v1/time-entry/settings", AdminToken())).Data.GetProperty("version").GetInt32();
        Ok(await Host.PutAsync("/api/v1/time-entry/settings", AdminToken(),
            new { expectedVersion = version, timeAdminPoolPositionId = (Guid?)null, weeklyReminderEnabled = true }));
    }

    private IReadOnlyList<Diten.Platform.Application.Features.Notifications.Services.NotificationEventDispatchRequest> Reminders()
        => Host.Dispatch.For(TimeEntryNotificationEvents.WeekReminder).Where(r => r.TenantId == Tenant).ToList();

    private async Task<List<string>> ReminderMarkKeysAsync()
        => (await Collection<TimeEntryNotificationMark>(PlatformCollections.TimeEntryNotificationMarks)
            .Find(m => m.TenantId == Tenant && m.Kind == TimeEntryNotificationEvents.WeekReminder).ToListAsync())
            .Select(m => m.Key).ToList();

    private sealed record MeasuredPage(int Items, IReadOnlyDictionary<string, int> Reads);

    private async Task<MeasuredPage> MeasureApprovalsAsync()
    {
        var token = ApproverToken(Manager);
        var (result, reads) = await _reads.MeasureAsync(() => Host.GetAsync("/api/v1/time-entry/approvals?start=0&length=10", token));
        Assert.True(result.Status == HttpStatusCode.OK, result.ToString());
        return new MeasuredPage(result.Data.GetProperty("items").GetArrayLength(), reads);
    }

    private static string Describe(IReadOnlyDictionary<string, int> reads)
        => string.Join(", ", reads.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => $"{r.Key}={r.Value}"));

    /// <summary>The whole answer as one comparable string: the zone and every field of every day.</summary>
    private static string Render(WorkingHoursResult result)
        => JsonSerializer.Serialize(new { zone = result.TimeZone.Id, days = result.Days });
}

/// <summary>What the next <c>CancelAsync</c> answers (K3). One shot: it resets itself.</summary>
internal enum CancelAnswer
{
    Real = 0,

    /// <summary>MOD-0023 refused and changed nothing — it lost a concurrent write.</summary>
    RefuseWithoutCancelling = 1,

    /// <summary>MOD-0023 cancelled what it could (the instance is closed) and refused the rest — a partial cancel.</summary>
    CancelThenRefuse = 2
}

internal sealed class CancelSwitch
{
    public CancelAnswer Next { get; set; }
}

/// <summary>The real approval service; only the next cancel can be made to answer like a MOD-0023 that refused.</summary>
internal sealed class SwitchedApprovals(TimesheetApprovalService inner, CancelSwitch cancel) : ITimesheetApprovalService
{
    public Task<TimesheetApprovalStart> StartAsync(
        TimesheetWeek week, IReadOnlyList<Guid> candidateUserIds, int submissionNumber, CancellationToken ct = default)
        => inner.StartAsync(week, candidateUserIds, submissionNumber, ct);

    public async Task<bool> CancelAsync(Guid workflowInstanceId, Guid actorUserId, CancellationToken ct = default)
    {
        var answer = cancel.Next;
        cancel.Next = CancelAnswer.Real;
        switch (answer)
        {
            case CancelAnswer.RefuseWithoutCancelling:
                return false;
            case CancelAnswer.CancelThenRefuse:
                await inner.CancelAsync(workflowInstanceId, actorUserId, ct);
                return false;
            default:
                return await inner.CancelAsync(workflowInstanceId, actorUserId, ct);
        }
    }

    public Task RetireSubmissionAsync(TimesheetWeek week, int submissionNumber, Guid actorUserId, CancellationToken ct = default)
        => inner.RetireSubmissionAsync(week, submissionNumber, actorUserId, ct);

    public Task<TimesheetDecision> ReadDecisionAsync(Guid workflowInstanceId, CancellationToken ct = default)
        => inner.ReadDecisionAsync(workflowInstanceId, ct);

    public Task<IReadOnlyDictionary<Guid, TimesheetDecisionOutcome>> ReadOutcomesAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct = default)
        => inner.ReadOutcomesAsync(workflowInstanceIds, ct);
}

internal sealed class ScopeEcho
{
    public bool On { get; set; }
}

/// <summary>
/// The host's stub calendar, except that — when switched on — every Tuesday is a full holiday whose NAME is the scope the
/// question carried (<c>{unit}|{legal entity}</c>). The provider copies the name into its answer, so which scope a person
/// resolved to can be read off the answer itself.
/// </summary>
internal sealed class ScopeEchoCalendar(IWorkingCalendarProvider inner, ScopeEcho echo) : IWorkingCalendarProvider
{
    public Task<WorkingDayResult> IsWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
    {
        if (!echo.On || date.DayOfWeek != DayOfWeek.Tuesday)
        {
            return inner.IsWorkingDayAsync(date, scope, ct);
        }

        var holiday = new HolidayInfo(
            Guid.NewGuid(), "ECHO", $"{scope.OrganizationUnitId}|{scope.LegalEntityId}", date, date, "PublicHoliday", false, false);
        return Task.FromResult(new WorkingDayResult(
            WorkingCalendarResolution.Resolved, false, date, scope.CountryCode, Guid.NewGuid(), null, holiday, "stub", []));
    }

    public Task<HolidayLookupResult> GetHolidayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
        => inner.GetHolidayAsync(date, scope, ct);

    public Task<WorkingDateResult> NextWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
        => inner.NextWorkingDayAsync(date, scope, ct);

    public Task<WorkingDateResult> AddWorkingDaysAsync(DateOnly start, int days, WorkingCalendarScope scope, CancellationToken ct = default)
        => inner.AddWorkingDaysAsync(start, days, scope, ct);

    public Task<WorkingDayCountResult> WorkingDaysBetweenAsync(DateOnly from, DateOnly to, WorkingCalendarScope scope, CancellationToken ct = default)
        => inner.WorkingDaysBetweenAsync(from, to, scope, ct);
}

/// <summary>One captured log line: the rendered message, the template's values by name, and the exception handed over.</summary>
internal sealed record CapturedLog(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Values, Exception? Exception)
{
    public int Int(string name) => Convert.ToInt32(Values[name], System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Every log line the host writes, kept — how a test reads a job's counters, which exist nowhere else.</summary>
internal sealed class CapturedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public ILogger CreateLogger(string categoryName) => new Sink(categoryName, _entries);

    public void Clear() => _entries.Clear();

    public IReadOnlyList<CapturedLog> Starting(string prefix)
        => _entries.Where(e => e.Message.StartsWith(prefix, StringComparison.Ordinal)).ToList();

    public void Dispose()
    {
    }

    private sealed class Sink(string category, ConcurrentQueue<CapturedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var pair in pairs)
                {
                    values[pair.Key] = pair.Value;
                }
            }

            entries.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), values, exception));
        }
    }
}
