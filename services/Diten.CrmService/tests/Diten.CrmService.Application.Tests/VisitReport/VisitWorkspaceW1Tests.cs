using System.Reflection;
using System.Security.Claims;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.PlannedVisit.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.PlannedVisit.Queries;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Claim = System.Security.Claims.Claim;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Tests.VisitReport;

/// <summary>
/// WP-VW-W1 (WP-VP-4K folded in) — on the PRODUCTION handlers / controller / policy pipeline:
/// 4K-1 calendar names + cancellation reason · 4K-2 T-1 link target named by the doctor · 4K-3 reporter = caller ·
/// 4K-4 a cancelled visit is locked · 4K-5 the 48 h report deadline · 4K-6 the calendar range code · 4K-7 the canonical
/// crm.visit-report.* keys · 8a the derived work status table + priority clashes · 8b the deadline boundary, where the
/// status and the write rule read the SAME instant · 8c the read surfaces (calendar, detail, filter, contract).
/// The clock is fixed (<see cref="TimeProvider"/>); "today" is the UTC day.
/// </summary>
public sealed class VisitWorkspaceW1Tests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const string Rep = "rep-1";
    private const string OtherRep = "rep-2";

    // A Thursday visit: open until Saturday 23:59:59Z, locked from Sunday 00:00:00Z.
    private static readonly DateOnly Thursday = new(2026, 10, 15);
    private static readonly DateTimeOffset FridayMorning = new(2026, 10, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SaturdayLastSecond = new(2026, 10, 17, 23, 59, 59, TimeSpan.Zero);
    private static readonly DateTimeOffset SundayMidnight = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CountingProductNames(IReadOnlyDictionary<Guid, string> names) : IProductNameReader
    {
        public int Calls { get; private set; }
        public List<IReadOnlyCollection<Guid>> Asked { get; } = new();

        public Task<IReadOnlyDictionary<Guid, string>> ReadNamesAsync(
            IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
        {
            Calls++;
            Asked.Add(productIds);
            return Task.FromResult(names);
        }
    }

    private readonly FakeVisitReportRepository _reports = new();
    private readonly FakePlannedVisitReadRepository _plans = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeContactRepository _contacts = new();

    private static TenantContext TenantCtx()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(Tenant);
        return ctx;
    }

    private static ICallerScope RepCaller(string resource = Rep) => new TestCallerScope(resource);
    private static ICallerScope Manager() => TestCallerScope.Unrestricted("manager-1");

    private RecordVisitOutcomeHandler Outcome(ICallerScope caller, DateTimeOffset now)
        => new(TenantCtx(), new NullActorContext(), _reports, _plans, caller, new FixedClock(now),
            FakeVisitReasonSet.Permissive().Validator()); // WP-VW-W2 — reasons are reference data

    private SubmitVisitReportHandler Submit(ICallerScope caller, DateTimeOffset now)
        => new(TenantCtx(), new NullActorContext(), _reports, _plans, caller, new FixedClock(now));

    private AmendVisitReportHandler Amend(ICallerScope caller)
        => new(TenantCtx(), new NullActorContext(), _reports, _plans, caller);

    private VisitTargetNameReader Names() => new(_accounts, _contacts);

    private GetVisitCalendarHandler Calendar(DateTimeOffset now, ICallerScope? caller = null, IProductNameReader? productNames = null)
        => new(TenantCtx(), _plans, _reports, caller ?? Manager(), Names(), productNames, new FixedClock(now));

    private GetPlannedVisitByIdHandler Detail(DateTimeOffset now)
        => new(TenantCtx(), _plans, Manager(), Names(), null, _reports, new FixedClock(now));

    private PlanAtom Seed(
        DateOnly date, string status = PlannedVisitStatus.Planned, string resource = Rep,
        List<PlannedVisitContentItem>? items = null, string? cancellationReason = null)
    {
        var atom = new PlanAtom
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            VisitCode = "VW-W1-" + (_plans.Items.Count + 1).ToString("0000"),
            TargetType = PlannedVisitTargetType.Contact,
            TargetId = Guid.NewGuid(),
            PlannedDate = date,
            PlanStatus = status,
            CancellationReason = cancellationReason,
            Resource = new PlannedVisitResourceRef { ResourceId = resource, ResourceType = "user" },
            ContentItems = items ?? new List<PlannedVisitContentItem>()
        };
        _plans.Items.Add(atom);
        return atom;
    }

    private VisitReportEntity SeedReport(PlanAtom plan, string outcome, string status)
    {
        var report = new VisitReportEntity
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            PlannedVisitId = plan.Id,
            ExecutionOutcome = outcome,
            ReportStatus = status,
            ReportedByResourceId = plan.Resource.ResourceId,
            ExecutedAt = new DateTimeOffset(plan.PlannedDate.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero),
            SubmittedAt = status == VisitReportStatus.Draft ? null : new DateTimeOffset(plan.PlannedDate.ToDateTime(new TimeOnly(11, 0)), TimeSpan.Zero),
            Feedback = new VisitReportFeedback { OutcomeCode = "detaylama-tamamlandi" }
        };
        _reports.Items.Add(report);
        return report;
    }

    private static RecordVisitOutcomeCommand Missed(Guid planId, string? reporter = null)
        => new(planId, VisitExecutionOutcome.Missed, null, VisitReportReasonCodes.DoctorUnavailable, null, null, reporter, null);

    private static RecordVisitOutcomeCommand Completed(Guid planId, string? reporter = null)
        => new(planId, VisitExecutionOutcome.Completed, null, null, null, null, reporter, null);

    private static RecordVisitOutcomeCommand Rescheduled(Guid planId)
        => new(planId, VisitExecutionOutcome.Rescheduled, null, VisitReportReasonCodes.RescheduledByDoctor,
            "2026-10-22", null, null, null);

    private static SubmitVisitReportCommand SubmitCmd(Guid planId, string? reporter = null)
        => new(planId, null, null, new VisitReportFeedbackInput("ok", "detaylama-tamamlandi", false, null), null, reporter, null);

    private static AmendVisitReportCommand AmendCmd(Guid reportId, string? reporter = null)
        => new(reportId, "typo in feedback", reporter, null, null,
            new VisitReportFeedbackInput("corrected", "detaylama-tamamlandi", false, null), null);

    // ═══ 4K-1 · calendar: cancellation reason + product names ═══════════════════════════════════════════════════

    [Fact]
    public async Task K1_calendar_carries_the_cancellation_reason_week_reopened_and_manual()
    {
        var reopened = Seed(Thursday, PlannedVisitStatus.Cancelled, cancellationReason: "week_reopened");
        var manual = Seed(Thursday, PlannedVisitStatus.Cancelled, cancellationReason: "doctor on leave");
        var open = Seed(Thursday);

        var res = await Calendar(FridayMorning).Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default);

        Assert.True(res.IsSuccessful);
        var items = res.Data!.Items.ToDictionary(i => i.PlannedVisitId);
        Assert.Equal("week_reopened", items[reopened.Id].CancellationReason);
        Assert.Equal("doctor on leave", items[manual.Id].CancellationReason);
        Assert.Null(items[open.Id].CancellationReason);
    }

    [Fact]
    public async Task K1_product_name_prefers_the_snapshot_then_ONE_bulk_mdm_read()
    {
        var snapshotted = Guid.NewGuid();
        var unnamedA = Guid.NewGuid();
        var unnamedB = Guid.NewGuid();
        Seed(Thursday, items: new List<PlannedVisitContentItem>
        {
            Item(snapshotted, "TUTUKON", "Tutukon 10 mg"),
            Item(unnamedA, "ALFA", null)
        });
        Seed(Thursday, items: new List<PlannedVisitContentItem> { Item(unnamedB, "BETA", " "), Item(unnamedA, "ALFA", null) });
        var mdm = new CountingProductNames(new Dictionary<Guid, string>
        {
            [snapshotted] = "MDM must not win over the snapshot",
            [unnamedA] = "Alfa Forte",
            [unnamedB] = "Beta Plus"
        });

        var res = await Calendar(FridayMorning, productNames: mdm)
            .Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default);

        var content = res.Data!.Items.SelectMany(i => i.PlannedContent!).ToList();
        Assert.Equal("Tutukon 10 mg", content.Single(c => c.ProductId == snapshotted).ProductName);
        Assert.All(content.Where(c => c.ProductId == unnamedA), c => Assert.Equal("Alfa Forte", c.ProductName));
        Assert.Equal("Beta Plus", content.Single(c => c.ProductId == unnamedB).ProductName);
        Assert.Equal(1, mdm.Calls); // one call per request, for the whole window
        Assert.Equal(new[] { unnamedA, unnamedB }.OrderBy(x => x), mdm.Asked.Single().OrderBy(x => x));
    }

    [Fact]
    public async Task K1_mdm_down_gives_null_names_and_200_and_a_fully_snapshotted_window_reads_nothing()
    {
        var unnamed = Guid.NewGuid();
        Seed(Thursday, items: new List<PlannedVisitContentItem> { Item(unnamed, "ALFA", null) });
        var down = new CountingProductNames(new Dictionary<Guid, string>()); // the MDM reader is fail-open → no entries

        var res = await Calendar(FridayMorning, productNames: down)
            .Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default);

        Assert.True(res.IsSuccessful);
        Assert.Equal(200, res.StatusCode);
        Assert.Null(Assert.Single(Assert.Single(res.Data!.Items).PlannedContent!).ProductName);

        _plans.Items.Clear();
        Seed(Thursday, items: new List<PlannedVisitContentItem> { Item(Guid.NewGuid(), "T", "Named") });
        var idle = new CountingProductNames(new Dictionary<Guid, string>());
        await Calendar(FridayMorning, productNames: idle).Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default);
        Assert.Equal(0, idle.Calls);
    }

    // ═══ 4K-2 · T-1: the account-contact-link target is named by the doctor ══════════════════════════════════════

    [Fact]
    public async Task K2_link_target_is_named_by_the_doctor_on_calendar_list_and_detail()
    {
        var accountId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        _accounts.Items.Add(new Account { Id = accountId, TenantId = Tenant, AccountName = "Şehir Hastanesi", AccountCode = "A1", AccountType = "hospital", Status = "active" });
        _contacts.Items.Add(new Contact { Id = contactId, TenantId = Tenant, DisplayName = "Dr. Ayşe Kaya", ContactType = "hcp", Status = "passive" });
        var plan = Seed(Thursday);
        plan.TargetType = PlannedVisitTargetType.AccountContactLink;
        plan.TargetId = Guid.NewGuid(); // the LINK id — names nothing
        plan.AccountId = accountId;
        plan.ContactId = contactId;

        var calendar = Assert.Single((await Calendar(FridayMorning)
            .Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default)).Data!.Items);
        Assert.Equal("Dr. Ayşe Kaya", calendar.TargetDisplayName);
        Assert.True(calendar.TargetInactive); // passivity comes from the doctor

        var list = Assert.Single((await new ListPlannedVisitsHandler(TenantCtx(), _plans, Manager(), Names())
            .Handle(new ListPlannedVisitsQuery(), default)).Data!.Items);
        Assert.Equal("Dr. Ayşe Kaya", list.TargetDisplayName);
        Assert.Equal("Şehir Hastanesi", list.AccountDisplayName);

        var detail = (await Detail(FridayMorning).Handle(new GetPlannedVisitByIdQuery(plan.Id), default)).Data!;
        Assert.Equal("Dr. Ayşe Kaya", detail.TargetDisplayName);
        Assert.Equal("Şehir Hastanesi", detail.AccountDisplayName);
        Assert.Equal("Dr. Ayşe Kaya", detail.ContactDisplayName);
    }

    // ═══ 4K-3 · the reporter is the caller ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task K3_a_rep_cannot_record_submit_or_amend_in_another_resources_name()
    {
        var plan = Seed(Thursday);

        var outcome = await Outcome(RepCaller(), FridayMorning).Handle(Missed(plan.Id, OtherRep), default);
        Assert.Equal(403, outcome.StatusCode);
        Assert.Contains(VisitOwnership.ResourceNotCaller, outcome.Errors!);

        var submit = await Submit(RepCaller(), FridayMorning).Handle(SubmitCmd(plan.Id, OtherRep), default);
        Assert.Equal(403, submit.StatusCode);
        Assert.Contains(VisitOwnership.ResourceNotCaller, submit.Errors!);
        Assert.Empty(_reports.Items);

        var report = SeedReport(plan, VisitExecutionOutcome.Completed, VisitReportStatus.Submitted);
        var amend = await Amend(RepCaller()).Handle(AmendCmd(report.Id, OtherRep), default);
        Assert.Equal(403, amend.StatusCode);
        Assert.Contains(VisitOwnership.ResourceNotCaller, amend.Errors!);
        Assert.Empty(report.Amendments);
    }

    [Fact]
    public async Task K3_no_field_or_the_own_id_means_the_caller_and_read_all_may_name_another()
    {
        var mine = Seed(Thursday);
        var res = await Submit(RepCaller(), FridayMorning).Handle(SubmitCmd(mine.Id), default);
        Assert.True(res.IsSuccessful);
        Assert.Equal(Rep, _reports.Items.Single(r => r.PlannedVisitId == mine.Id).ReportedByResourceId);

        var same = Seed(Thursday);
        Assert.True((await Outcome(RepCaller(), FridayMorning).Handle(Missed(same.Id, Rep), default)).IsSuccessful);

        var managed = Seed(Thursday);
        var byManager = await Outcome(Manager(), FridayMorning).Handle(Missed(managed.Id, OtherRep), default);
        Assert.True(byManager.IsSuccessful);
        Assert.Equal(OtherRep, _reports.Items.Single(r => r.PlannedVisitId == managed.Id).ReportedByResourceId);

        var report = _reports.Items.Single(r => r.PlannedVisitId == mine.Id);
        Assert.True((await Amend(RepCaller()).Handle(AmendCmd(report.Id), default)).IsSuccessful);
        Assert.Equal(Rep, report.Amendments.Single().ByResourceId);
    }

    // ═══ 4K-4 · a cancelled visit is locked ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task K4_outcome_and_submit_on_a_cancelled_plan_are_409_plan_cancelled_for_every_outcome()
    {
        var plan = Seed(Thursday, PlannedVisitStatus.Cancelled, cancellationReason: "week_reopened");

        foreach (var cmd in new[] { Completed(plan.Id), Missed(plan.Id), Rescheduled(plan.Id) })
        {
            var r = await Outcome(RepCaller(), FridayMorning).Handle(cmd, default);
            Assert.Equal(409, r.StatusCode);
            Assert.Contains(VisitReportErrorCodes.PlanCancelled, r.Errors!);
        }

        var submit = await Submit(Manager(), FridayMorning).Handle(SubmitCmd(plan.Id), default);
        Assert.Equal(409, submit.StatusCode);
        Assert.Contains(VisitReportErrorCodes.PlanCancelled, submit.Errors!);
        Assert.Empty(_reports.Items);
    }

    [Fact]
    public async Task K4_a_report_submitted_before_the_cancellation_can_still_be_amended()
    {
        var plan = Seed(Thursday);
        var report = SeedReport(plan, VisitExecutionOutcome.Completed, VisitReportStatus.Submitted);
        plan.PlanStatus = PlannedVisitStatus.Cancelled;
        plan.CancellationReason = "doctor retired";

        var r = await Amend(RepCaller()).Handle(AmendCmd(report.Id), default);

        Assert.True(r.IsSuccessful);
        Assert.Equal(VisitReportStatus.Amended, report.ReportStatus);
    }

    // ═══ 4K-5 · the report deadline (48 h after the end of the planned day, UTC) ═════════════════════════════════

    [Fact]
    public void K5_the_deadline_constant_is_48_hours_and_the_thursday_deadline_is_saturday_23_59_59()
    {
        Assert.Equal(48, VisitReportLimits.ReportDeadlineHours);
        Assert.Equal(SaturdayLastSecond, VisitReportDeadline.For(Thursday));
        Assert.False(VisitReportDeadline.IsPassed(Thursday, SaturdayLastSecond));
        Assert.True(VisitReportDeadline.IsPassed(Thursday, SundayMidnight));
    }

    [Fact]
    public async Task K5_every_outcome_is_accepted_until_saturday_23_59_59_and_refused_from_sunday_00_00()
    {
        foreach (var make in new Func<Guid, RecordVisitOutcomeCommand>[] { id => Completed(id), id => Missed(id), Rescheduled })
        {
            var inTime = Seed(Thursday);
            Assert.True((await Outcome(RepCaller(), SaturdayLastSecond).Handle(make(inTime.Id), default)).IsSuccessful);

            var late = Seed(Thursday);
            var r = await Outcome(RepCaller(), SundayMidnight).Handle(make(late.Id), default);
            Assert.Equal(409, r.StatusCode);
            Assert.Contains(VisitReportErrorCodes.DeadlinePassed, r.Errors!);
            Assert.DoesNotContain(_reports.Items, x => x.PlannedVisitId == late.Id);
        }
    }

    [Fact]
    public async Task K5_a_first_submit_and_a_draft_submit_are_refused_after_the_deadline()
    {
        var none = Seed(Thursday);
        var inTime = await Submit(RepCaller(), SaturdayLastSecond).Handle(SubmitCmd(Seed(Thursday).Id), default);
        Assert.True(inTime.IsSuccessful);

        var late = await Submit(RepCaller(), SundayMidnight).Handle(SubmitCmd(none.Id), default);
        Assert.Equal(409, late.StatusCode);
        Assert.Contains(VisitReportErrorCodes.DeadlinePassed, late.Errors!);

        var drafted = Seed(Thursday);
        var draft = SeedReport(drafted, VisitExecutionOutcome.Completed, VisitReportStatus.Draft);
        var lateDraft = await Submit(RepCaller(), SundayMidnight).Handle(SubmitCmd(drafted.Id), default);
        Assert.Equal(409, lateDraft.StatusCode);
        Assert.Contains(VisitReportErrorCodes.DeadlinePassed, lateDraft.Errors!);
        Assert.Equal(VisitReportStatus.Draft, draft.ReportStatus);
    }

    [Fact]
    public async Task K5_the_read_all_holder_is_exempt_and_amendment_is_unlimited()
    {
        var plan = Seed(Thursday);
        var aMonthLater = SundayMidnight.AddDays(30);
        Assert.True((await Outcome(Manager(), aMonthLater).Handle(Missed(plan.Id, Rep), default)).IsSuccessful);

        var submitted = Seed(Thursday);
        Assert.True((await Submit(Manager(), aMonthLater).Handle(SubmitCmd(submitted.Id, Rep), default)).IsSuccessful);

        var old = Seed(new DateOnly(2026, 1, 8));
        var report = SeedReport(old, VisitExecutionOutcome.Completed, VisitReportStatus.Submitted);
        Assert.True((await Amend(RepCaller()).Handle(AmendCmd(report.Id), default)).IsSuccessful);
    }

    [Fact]
    public async Task K5_contract_publishes_report_deadline_hours_and_the_calendar_publishes_report_deadline()
    {
        var contract = (await new GetVisitReportContractHandler(TenantCtx())
            .Handle(new GetVisitReportContractQuery(), default)).Data!;
        Assert.Equal(48, contract.ReportDeadlineHours);
        Assert.Contains(VisitReportErrorCodes.DeadlinePassed, contract.ErrorCodes);
        Assert.Contains(VisitReportErrorCodes.PlanCancelled, contract.ErrorCodes);

        Seed(Thursday);
        var item = Assert.Single((await Calendar(FridayMorning)
            .Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default)).Data!.Items);
        Assert.Equal(SaturdayLastSecond, item.ReportDeadline);
    }

    // ═══ 4K-6 · the calendar range code ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(null, "2026-10-15")]
    [InlineData("2026-10-15", null)]
    [InlineData("not-a-date", "2026-10-15")]
    public async Task K6_a_missing_or_bad_window_is_400_calendar_range_invalid(string? from, string? to)
    {
        var res = await Calendar(FridayMorning).Handle(new GetVisitCalendarQuery(from, to), default);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.CalendarRangeInvalid, res.Errors!);
        Assert.DoesNotContain(VisitReportErrorCodes.RescheduleDateInvalid, res.Errors!);
    }

    // ═══ 4K-7 · the endpoints require crm.visit-report.* (the territory fallback opens nothing) ══════════════════

    public static TheoryData<string, string[]> ActionKeys => new()
    {
        { nameof(VisitReportController.Contract), new[] { VisitReportPermissions.Read } },
        { nameof(VisitReportController.Calendar), new[] { VisitReportPermissions.Read } },
        { nameof(VisitReportController.List), new[] { VisitReportPermissions.Read } },
        { nameof(VisitReportController.Get), new[] { VisitReportPermissions.Read } },
        { nameof(VisitReportController.RecordOutcome), new[] { VisitReportPermissions.Record, VisitReportPermissions.PlannedVisitManage } },
        { nameof(VisitReportController.Submit), new[] { VisitReportPermissions.Record, VisitReportPermissions.PlannedVisitManage } },
        { nameof(VisitReportController.Amend), new[] { VisitReportPermissions.Amend } },
    };

    [Theory]
    [MemberData(nameof(ActionKeys))]
    public async Task K7_each_endpoint_requires_its_canonical_keys_and_the_old_fallback_is_403(string action, string[] keys)
    {
        Assert.Equal(keys.OrderBy(k => k), ControllerAction(action).GetCustomAttributes<HasPermissionAttribute>()
            .Select(a => a.Permission).OrderBy(k => k));

        Assert.True((await Evaluate(action, User(keys))).Succeeded);
        Assert.True((await Evaluate(action, User("crm.territory.read", "crm.territory.model.manage"))).Forbidden);
        Assert.True((await Evaluate(action, Anonymous())).Challenged);
    }

    [Fact]
    public async Task K7_read_does_not_record_or_amend_and_record_needs_planned_visit_manage_too()
    {
        var reader = User(VisitReportPermissions.Read);
        Assert.True((await Evaluate(nameof(VisitReportController.Calendar), reader)).Succeeded);
        Assert.True((await Evaluate(nameof(VisitReportController.RecordOutcome), reader)).Forbidden);
        Assert.True((await Evaluate(nameof(VisitReportController.Amend), reader)).Forbidden);

        Assert.True((await Evaluate(nameof(VisitReportController.RecordOutcome), User(VisitReportPermissions.Record))).Forbidden);
        Assert.True((await Evaluate(nameof(VisitReportController.Amend), User(VisitReportPermissions.Record, VisitReportPermissions.PlannedVisitManage))).Forbidden);
    }

    [Fact]
    public void K7_no_visit_report_action_carries_a_territory_key()
    {
        var actions = typeof(VisitReportController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(ActionKeys.Count(), actions.Length);
        Assert.All(actions.SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>()),
            a => Assert.DoesNotContain("crm.territory", a.Permission, StringComparison.Ordinal));
        Assert.Null(typeof(VisitReportPermissions).GetField("ReadFallback"));
        Assert.Null(typeof(VisitReportPermissions).GetField("ManageFallback"));
    }

    // ═══ 8a · the derived work status: one example per row + the priority clashes ═══════════════════════════════

    [Fact]
    public void W8a_every_row_of_the_status_table()
    {
        var friday = FridayMorning;
        Assert.Equal(VisitWorkStatus.Cancelled, Derive(Seed(Thursday, PlannedVisitStatus.Cancelled), null, friday));
        Assert.Equal(VisitWorkStatus.NotDone, Derive(Seed(Thursday), VisitExecutionOutcome.Missed, friday, VisitReportStatus.Submitted));
        Assert.Equal(VisitWorkStatus.Rescheduled, Derive(Seed(Thursday), VisitExecutionOutcome.Rescheduled, friday, VisitReportStatus.Amended));
        Assert.Equal(VisitWorkStatus.Reported, Derive(Seed(Thursday), VisitExecutionOutcome.Completed, friday, VisitReportStatus.Submitted));
        Assert.Equal(VisitWorkStatus.Expired, Derive(Seed(Thursday), null, SundayMidnight));
        Assert.Equal(VisitWorkStatus.ReportMissing, Derive(Seed(Thursday), VisitExecutionOutcome.Completed, friday, VisitReportStatus.Draft));
        Assert.Equal(VisitWorkStatus.Missed, Derive(Seed(Thursday), null, friday));
        Assert.Equal(VisitWorkStatus.Today, Derive(Seed(new DateOnly(2026, 10, 16)), null, friday));
        Assert.Equal(VisitWorkStatus.Planned, Derive(Seed(new DateOnly(2026, 10, 17)), null, friday));
    }

    [Fact]
    public void W8a_priority_clashes()
    {
        // cancelled + reported → cancelled
        Assert.Equal(VisitWorkStatus.Cancelled,
            Derive(Seed(Thursday, PlannedVisitStatus.Cancelled), VisitExecutionOutcome.Completed, FridayMorning, VisitReportStatus.Submitted));
        // deadline passed + a completed DRAFT → expired (the lock wins over "report missing")
        Assert.Equal(VisitWorkStatus.Expired,
            Derive(Seed(Thursday), VisitExecutionOutcome.Completed, SundayMidnight, VisitReportStatus.Draft));
        // a SUBMITTED report + deadline passed → reported (the lock applies only without a report)
        Assert.Equal(VisitWorkStatus.Reported,
            Derive(Seed(Thursday), VisitExecutionOutcome.Completed, SundayMidnight.AddDays(10), VisitReportStatus.Submitted));
        // a missed DRAFT → missed, then expired after the deadline
        var drafted = Seed(Thursday);
        var draft = SeedReport(drafted, VisitExecutionOutcome.Missed, VisitReportStatus.Draft);
        Assert.Equal(VisitWorkStatus.Missed, VisitWorkStatus.Derive(drafted, draft, FridayMorning));
        Assert.Equal(VisitWorkStatus.Expired, VisitWorkStatus.Derive(drafted, draft, SundayMidnight));
    }

    [Fact]
    public void W8a_the_codes_are_published_in_priority_order_and_manager_attention_is_expired_only()
    {
        Assert.Equal(
            new[] { "cancelled", "not_done", "rescheduled", "reported", "expired", "report_missing", "missed", "today", "planned" },
            VisitWorkStatus.All);
        Assert.DoesNotContain("in_progress", VisitWorkStatus.All); // W3
        Assert.All(VisitWorkStatus.All, s => Assert.Equal(s == VisitWorkStatus.Expired, VisitWorkStatus.NeedsManagerAttention(s)));
    }

    // ═══ 8b · the boundary: status and rule read the SAME deadline ═══════════════════════════════════════════════

    [Fact]
    public async Task W8b_thursday_visit_friday_and_saturday_last_second_missed_sunday_midnight_expired_and_locked()
    {
        var plan = Seed(Thursday);

        Assert.Equal(VisitWorkStatus.Missed, VisitWorkStatus.Derive(plan, null, FridayMorning));
        Assert.Equal(VisitWorkStatus.Missed, VisitWorkStatus.Derive(plan, null, SaturdayLastSecond));
        Assert.Equal(VisitWorkStatus.Expired, VisitWorkStatus.Derive(plan, null, SundayMidnight));

        var atSunday = Assert.Single((await Calendar(SundayMidnight)
            .Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default)).Data!.Items);
        Assert.Equal(VisitWorkStatus.Expired, atSunday.WorkStatus);
        Assert.True(atSunday.ManagerAttention);

        // the same instant: the rule refuses
        var r = await Outcome(RepCaller(), SundayMidnight).Handle(Missed(plan.Id), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(VisitReportErrorCodes.DeadlinePassed, r.Errors!);

        // one second earlier: the status is still open and the rule accepts
        var atSaturday = Assert.Single((await Calendar(SaturdayLastSecond)
            .Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15"), default)).Data!.Items);
        Assert.Equal(VisitWorkStatus.Missed, atSaturday.WorkStatus);
        Assert.False(atSaturday.ManagerAttention);
        Assert.True((await Outcome(RepCaller(), SaturdayLastSecond).Handle(Missed(plan.Id), default)).IsSuccessful);
    }

    // ═══ 8c · read surfaces ═══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task W8c_calendar_and_detail_carry_work_status_deadline_and_manager_attention()
    {
        var expired = Seed(new DateOnly(2026, 10, 12));
        var reported = Seed(new DateOnly(2026, 10, 12));
        SeedReport(reported, VisitExecutionOutcome.Completed, VisitReportStatus.Submitted);

        var items = (await Calendar(FridayMorning)
            .Handle(new GetVisitCalendarQuery("2026-10-12", "2026-10-12"), default)).Data!.Items.ToDictionary(i => i.PlannedVisitId);
        Assert.Equal(VisitWorkStatus.Expired, items[expired.Id].WorkStatus);
        Assert.True(items[expired.Id].ManagerAttention);
        Assert.Equal(VisitWorkStatus.Reported, items[reported.Id].WorkStatus);
        Assert.False(items[reported.Id].ManagerAttention);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 23, 59, 59, TimeSpan.Zero), items[expired.Id].ReportDeadline);

        var detail = (await Detail(FridayMorning).Handle(new GetPlannedVisitByIdQuery(expired.Id), default)).Data!;
        Assert.Equal(VisitWorkStatus.Expired, detail.WorkStatus);
        Assert.True(detail.ManagerAttention);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 23, 59, 59, TimeSpan.Zero), detail.ReportDeadline);

        var reportedDetail = (await Detail(FridayMorning).Handle(new GetPlannedVisitByIdQuery(reported.Id), default)).Data!;
        Assert.Equal(VisitWorkStatus.Reported, reportedDetail.WorkStatus);
    }

    [Fact]
    public async Task W8c_work_status_filter_returns_only_the_asked_codes_and_absent_keeps_the_old_behaviour()
    {
        var missed = Seed(Thursday);
        var today = Seed(new DateOnly(2026, 10, 16));
        var expired = Seed(new DateOnly(2026, 10, 12));

        var onlyMissed = (await Calendar(FridayMorning)
            .Handle(new GetVisitCalendarQuery("2026-10-12", "2026-10-18", null, "missed"), default)).Data!;
        Assert.Equal(new[] { missed.Id }, onlyMissed.Items.Select(i => i.PlannedVisitId));
        Assert.Equal(1, onlyMissed.TotalCount);

        var two = (await Calendar(FridayMorning)
            .Handle(new GetVisitCalendarQuery("2026-10-12", "2026-10-18", null, " Missed , expired "), default)).Data!;
        Assert.Equal(new[] { expired.Id, missed.Id }.OrderBy(x => x), two.Items.Select(i => i.PlannedVisitId).OrderBy(x => x));

        var all = (await Calendar(FridayMorning)
            .Handle(new GetVisitCalendarQuery("2026-10-12", "2026-10-18"), default)).Data!;
        Assert.Equal(3, all.TotalCount);
        Assert.Contains(all.Items, i => i.PlannedVisitId == today.Id && i.WorkStatus == VisitWorkStatus.Today);
    }

    [Theory]
    [InlineData("in_progress")]
    [InlineData("missed,bogus")]
    [InlineData(",")]
    public async Task W8c_a_bad_work_status_is_400_work_status_invalid(string raw)
    {
        Seed(Thursday);
        var res = await Calendar(FridayMorning).Handle(new GetVisitCalendarQuery("2026-10-15", "2026-10-15", null, raw), default);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.WorkStatusInvalid, res.Errors!);
    }

    [Fact]
    public async Task W8c_contract_publishes_work_statuses_and_the_filter()
    {
        var contract = (await new GetVisitReportContractHandler(TenantCtx())
            .Handle(new GetVisitReportContractQuery(), default)).Data!;
        Assert.Equal(VisitWorkStatus.All, contract.WorkStatuses);
        Assert.Contains("workStatus", contract.SupportedFilters.List);
        Assert.Contains(VisitReportErrorCodes.WorkStatusInvalid, contract.ErrorCodes);
        Assert.Contains(VisitReportErrorCodes.CalendarRangeInvalid, contract.ErrorCodes);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    private string Derive(PlanAtom plan, string? outcome, DateTimeOffset now, string status = VisitReportStatus.Submitted)
        => VisitWorkStatus.Derive(plan, outcome is null ? null : SeedReport(plan, outcome, status), now);

    private static PlannedVisitContentItem Item(Guid productId, string code, string? name) => new()
    {
        ProductId = productId, ProductCode = code, ProductName = name, Role = "promo",
        JourneyId = Guid.NewGuid(), StageId = Guid.NewGuid(), StageIndex = 0
    };

    private static MethodInfo ControllerAction(string name)
        => typeof(VisitReportController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal User(params string[] permissions)
        => new(new ClaimsIdentity(
            permissions.Select(p => new Claim("permission", p)).Append(new Claim("sub", Rep)), "Bearer"));

    private static async Task<PolicyAuthorizationResult> Evaluate(string action, ClaimsPrincipal user)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IPolicyEvaluator, PolicyEvaluator>();
        await using var provider = services.BuildServiceProvider();

        // Class-level [Authorize] + every action-level [HasPermission], combined exactly as the endpoint would be.
        var authorizeData = typeof(VisitReportController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(ControllerAction(action).GetCustomAttributes<AuthorizeAttribute>())
            .Cast<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(), authorizeData);

        var httpContext = new DefaultHttpContext { User = user, RequestServices = provider };
        var authenticate = user.Identity?.IsAuthenticated == true
            ? AuthenticateResult.Success(new AuthenticationTicket(user, "Bearer"))
            : AuthenticateResult.NoResult();

        return await provider.GetRequiredService<IPolicyEvaluator>()
            .AuthorizeAsync(policy!, authenticate, httpContext, resource: null);
    }
}
