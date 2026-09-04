using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Domain.Entities;
using MongoDB.Bson.Serialization;
using Xunit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitReport;

/// <summary>
/// MOD-0155 FU02 — VisitReport runtime. Pins down: outcome recording (completed/missed/rescheduled) + the reason-code
/// rule + the fail-closed vocabulary; the orphan-report + 1:1 guards; report submit + immutability after the edit window
/// + append-only amendment; the §4.4 loop (the ACTUAL StageIndex is recorded on the report, NOT on the plan atom); the
/// calendar join; and tenant isolation. All in-memory (no Mongo, no cross-module mutation).
/// </summary>
public sealed class VisitReportRuntimeTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeVisitReportRepository Reports { get; } = new();
        public FakePlannedVisitReadRepository Plans { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid? tenant = null) => TenantId = tenant ?? TenantA;

        public RecordVisitOutcomeHandler RecordOutcome(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Reports, Plans);

        public SubmitVisitReportHandler Submit(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Reports, Plans);

        public AmendVisitReportHandler Amend(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Reports);

        public GetVisitCalendarHandler Calendar(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), Plans, Reports);

        public GetVisitReportByIdHandler Get(Guid? tenant = null) => new(Tenant(tenant ?? TenantId), Reports);
        public ListVisitReportsHandler List(Guid? tenant = null) => new(Tenant(tenant ?? TenantId), Reports);
        public GetVisitReportContractHandler Contract() => new(Tenant(TenantId));

        public Guid SeedPlan(string resourceId = "rep-1", DateOnly? date = null, int? plannedStageIndex = null)
        {
            var id = Guid.NewGuid();
            Plans.Items.Add(new PlanAtom
            {
                Id = id,
                TenantId = TenantId,
                VisitCode = "V-" + id.ToString()[..8],
                TargetType = PlannedVisitTargetType.Contact,
                TargetId = Guid.NewGuid(),
                PlannedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date),
                PlanStatus = PlannedVisitStatus.Confirmed,
                Resource = new PlannedVisitResourceRef { ResourceId = resourceId, ResourceType = "person" },
                Content = plannedStageIndex is null
                    ? null
                    : new PlannedVisitContentRef { StageIndex = plannedStageIndex, JourneyId = Guid.NewGuid() }
            });
            return id;
        }
    }

    private static VisitReportFeedbackInput Feedback(bool followUp = false)
        => new("Good discussion", "positive", followUp, followUp ? "call in 2 weeks" : null);

    // ── outcome recording ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordOutcome_completed_creates_a_draft_report_and_never_writes_the_plan_atom()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();

        var res = await f.RecordOutcome().Handle(
            new RecordVisitOutcomeCommand(planId, VisitExecutionOutcome.Completed, null, null, null, null, null, null),
            CancellationToken.None);

        Assert.True(res.IsSuccessful);
        Assert.Equal(201, res.StatusCode);
        var report = Assert.Single(f.Reports.Items);
        Assert.Equal(VisitReportStatus.Draft, report.ReportStatus);
        Assert.Equal(VisitExecutionOutcome.Completed, report.ExecutionOutcome);
        Assert.Equal("rep-1", report.ReportedByResourceId); // defaulted from the plan's resource
        // D-EXECUTION-STATUS = A + F-EXECUTED-MARKER: FU02 never mutates the FU01 plan atom.
        Assert.Equal(0, f.Plans.ReplaceCount);
    }

    [Fact]
    public async Task RecordOutcome_missed_requires_a_reason_code()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();

        var res = await f.RecordOutcome().Handle(
            new RecordVisitOutcomeCommand(planId, VisitExecutionOutcome.Missed, null, null, null, null, null, null),
            CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.ReasonCodeRequired, res.Errors!);
    }

    [Fact]
    public async Task RecordOutcome_missed_with_reason_is_recorded()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();

        var res = await f.RecordOutcome().Handle(
            new RecordVisitOutcomeCommand(
                planId, VisitExecutionOutcome.Missed, null, VisitReportReasonCodes.DoctorUnavailable,
                null, null, null, null),
            CancellationToken.None);

        Assert.True(res.IsSuccessful);
        var report = Assert.Single(f.Reports.Items);
        Assert.Equal(VisitReportReasonCodes.DoctorUnavailable, report.ReasonCode);
    }

    [Fact]
    public async Task RecordOutcome_rescheduled_captures_the_new_intended_date()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();
        var newDay = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7).Date);

        var res = await f.RecordOutcome().Handle(
            new RecordVisitOutcomeCommand(
                planId, VisitExecutionOutcome.Rescheduled, null, VisitReportReasonCodes.RescheduledByDoctor,
                newDay.ToString("yyyy-MM-dd"), "moved to next week", null, null),
            CancellationToken.None);

        Assert.True(res.IsSuccessful);
        var report = Assert.Single(f.Reports.Items);
        Assert.Equal(newDay, report.RescheduleToDate);
    }

    [Fact]
    public async Task RecordOutcome_for_a_missing_plan_is_404()
    {
        var f = new Fixture();

        var res = await f.RecordOutcome().Handle(
            new RecordVisitOutcomeCommand(
                Guid.NewGuid(), VisitExecutionOutcome.Completed, null, null, null, null, null, null),
            CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(404, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.PlannedVisitNotFound, res.Errors!);
    }

    [Fact]
    public async Task RecordOutcome_rejects_an_out_of_set_outcome_such_as_cancelled()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();

        // cancelled stays FU01's command; it is NOT a valid FU02 execution outcome (fail-closed).
        var res = await f.RecordOutcome().Handle(
            new RecordVisitOutcomeCommand(planId, "cancelled", null, null, null, null, null, null),
            CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(400, res.StatusCode);
    }

    // ── submit + the §4.4 loop ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_records_the_actual_stage_index_on_the_report_not_on_the_plan_atom()
    {
        var f = new Fixture();
        var planId = f.SeedPlan(plannedStageIndex: 2);

        var res = await f.Submit().Handle(
            new SubmitVisitReportCommand(
                planId,
                new VisitReportContentActualsInput(Guid.NewGuid(), Guid.NewGuid(), 5, "S5", MatchedPlan: false, null, null),
                new[] { new VisitReportSampleInput("brochure", null, 3, null) },
                Feedback(),
                null, null, null),
            CancellationToken.None);

        Assert.True(res.IsSuccessful);
        var report = Assert.Single(f.Reports.Items);
        Assert.Equal(VisitReportStatus.Submitted, report.ReportStatus);
        Assert.Equal(VisitExecutionOutcome.Completed, report.ExecutionOutcome);
        Assert.Equal(5, report.ContentActuals!.StageIndex);          // the ACTUAL presented stage (§4.4)
        Assert.False(report.ContentActuals.MatchedPlan);             // diverged from the FU04-planned stage
        Assert.NotNull(report.SubmittedAt);
        var sample = Assert.Single(report.Samples);
        Assert.Equal("brochure", sample.ItemType);

        // The plan atom keeps its PLANNED StageIndex; FU02 writes no advanced cursor onto it (D-STAGE-ADVANCE = B).
        Assert.Equal(2, f.Plans.Items.Single(p => p.Id == planId).Content!.StageIndex);
        Assert.Equal(0, f.Plans.ReplaceCount);
    }

    [Fact]
    public async Task Submit_requires_an_outcome_code_reference_data_driven()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();

        var res = await f.Submit().Handle(
            new SubmitVisitReportCommand(planId, null, null, Feedback: null, null, null, null),
            CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.OutcomeCodeRequired, res.Errors!);
    }

    [Fact]
    public async Task Submit_is_1to1_per_plan_a_second_submit_updates_not_duplicates()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();

        await f.Submit().Handle(
            new SubmitVisitReportCommand(planId, null, null, Feedback(), null, null, null), CancellationToken.None);
        var second = await f.Submit().Handle(
            new SubmitVisitReportCommand(planId, null, null, Feedback(followUp: true), null, null, null),
            CancellationToken.None);

        Assert.True(second.IsSuccessful);
        var report = Assert.Single(f.Reports.Items);           // still ONE report for the visit
        Assert.True(report.Feedback!.FollowUpRequired);        // the in-window edit took effect
    }

    [Fact]
    public async Task Submit_after_the_edit_window_refuses_an_in_place_edit()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();

        await f.Submit().Handle(
            new SubmitVisitReportCommand(planId, null, null, Feedback(), null, null, null), CancellationToken.None);

        // Age the submitted report past the correction window.
        var stored = f.Reports.Items.Single();
        stored.SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-(VisitReportLimits.EditWindowMinutes + 5));

        var res = await f.Submit().Handle(
            new SubmitVisitReportCommand(planId, null, null, Feedback(followUp: true), null, null, null),
            CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(409, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.EditWindowClosed, res.Errors!);
    }

    // ── append-only amendment (D-EDIT-WINDOW) ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Amend_appends_a_correction_and_marks_the_report_amended()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();
        await f.Submit().Handle(
            new SubmitVisitReportCommand(planId, null, null, Feedback(), null, null, null), CancellationToken.None);
        var reportId = f.Reports.Items.Single().Id;

        var res = await f.Amend().Handle(
            new AmendVisitReportCommand(
                reportId, "corrected sample count", "rep-1", null,
                new[] { new VisitReportSampleInput("brochure", null, 10, null) }, null, null),
            CancellationToken.None);

        Assert.True(res.IsSuccessful);
        var report = f.Reports.Items.Single();
        Assert.Equal(VisitReportStatus.Amended, report.ReportStatus);
        var amendment = Assert.Single(report.Amendments);          // append-only trail
        Assert.Equal("corrected sample count", amendment.Reason);
        Assert.Contains("Samples", amendment.ChangedFields);
        Assert.NotNull(report.AmendedAt);
    }

    [Fact]
    public async Task Amend_on_a_draft_report_is_refused()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();
        await f.RecordOutcome().Handle(
            new RecordVisitOutcomeCommand(planId, VisitExecutionOutcome.Completed, null, null, null, null, null, null),
            CancellationToken.None);
        var reportId = f.Reports.Items.Single().Id;

        var res = await f.Amend().Handle(
            new AmendVisitReportCommand(reportId, "too soon", null, null, null, null, null), CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(409, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.NotFinalised, res.Errors!);
    }

    [Fact]
    public async Task Amend_requires_a_reason()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();
        await f.Submit().Handle(
            new SubmitVisitReportCommand(planId, null, null, Feedback(), null, null, null), CancellationToken.None);
        var reportId = f.Reports.Items.Single().Id;

        var res = await f.Amend().Handle(
            new AmendVisitReportCommand(reportId, "   ", null, null, null, null, null), CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.AmendmentReasonRequired, res.Errors!);
    }

    [Fact]
    public async Task Amend_with_a_stale_version_is_409()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();
        await f.Submit().Handle(
            new SubmitVisitReportCommand(planId, null, null, Feedback(), null, null, null), CancellationToken.None);
        var report = f.Reports.Items.Single();

        var res = await f.Amend().Handle(
            new AmendVisitReportCommand(report.Id, "late", null, null, null, null, ExpectedVersion: report.Version + 5),
            CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(409, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.ConcurrencyConflict, res.Errors!);
    }

    // ── calendar join + read paths ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Calendar_joins_plan_atoms_with_their_report_state()
    {
        var f = new Fixture();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var reportedPlan = f.SeedPlan(date: today);
        var unreportedPlan = f.SeedPlan(date: today);
        await f.Submit().Handle(
            new SubmitVisitReportCommand(reportedPlan, null, null, Feedback(), null, null, null), CancellationToken.None);

        var res = await f.Calendar().Handle(
            new GetVisitCalendarQuery(today.ToString("yyyy-MM-dd"), today.ToString("yyyy-MM-dd"), null),
            CancellationToken.None);

        Assert.True(res.IsSuccessful);
        Assert.Equal(2, res.Data!.TotalCount);
        Assert.Equal(VisitReportStatus.Submitted, res.Data.Items.Single(i => i.PlannedVisitId == reportedPlan).ReportState);
        Assert.Equal("none", res.Data.Items.Single(i => i.PlannedVisitId == unreportedPlan).ReportState);
    }

    [Fact]
    public async Task Calendar_requires_a_date_window()
    {
        var f = new Fixture();
        var res = await f.Calendar().Handle(new GetVisitCalendarQuery(null, null, null), CancellationToken.None);
        Assert.False(res.IsSuccessful);
        Assert.Equal(400, res.StatusCode);
    }

    [Fact]
    public async Task List_filters_by_report_status()
    {
        var f = new Fixture();
        var draftPlan = f.SeedPlan();
        var submittedPlan = f.SeedPlan();
        await f.RecordOutcome().Handle(
            new RecordVisitOutcomeCommand(draftPlan, VisitExecutionOutcome.Completed, null, null, null, null, null, null),
            CancellationToken.None);
        await f.Submit().Handle(
            new SubmitVisitReportCommand(submittedPlan, null, null, Feedback(), null, null, null), CancellationToken.None);

        var res = await f.List().Handle(
            new ListVisitReportsQuery(ReportStatus: VisitReportStatus.Submitted), CancellationToken.None);

        Assert.True(res.IsSuccessful);
        Assert.Equal(submittedPlan, Assert.Single(res.Data!.Items).PlannedVisitId);
    }

    [Fact]
    public async Task GetById_across_tenants_is_404()
    {
        var f = new Fixture();
        var planId = f.SeedPlan();
        await f.Submit().Handle(
            new SubmitVisitReportCommand(planId, null, null, Feedback(), null, null, null), CancellationToken.None);
        var reportId = f.Reports.Items.Single().Id;

        var res = await f.Get(TenantB).Handle(new GetVisitReportByIdQuery(reportId), CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(404, res.StatusCode);
    }

    // ── contract + class map ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Contract_publishes_the_fail_closed_vocabulary()
    {
        var f = new Fixture();
        var res = await f.Contract().Handle(new GetVisitReportContractQuery(), CancellationToken.None);

        Assert.True(res.IsSuccessful);
        Assert.Equal("MOD-0155-FU02", res.Data!.ModuleId);
        Assert.Equal(VisitExecutionOutcome.All, res.Data.Vocabularies.ExecutionOutcomes);
        Assert.Equal(VisitReportStatus.All, res.Data.Vocabularies.ReportStatuses);
        Assert.DoesNotContain("cancelled", res.Data.Vocabularies.ExecutionOutcomes);
    }

    [Fact]
    public void ClassMaps_register_the_visit_report_aggregate_and_its_embedded_types()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();

        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(VisitReportEntity)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(VisitReportContentActuals)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(VisitReportSample)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(VisitReportFeedback)));
        Assert.True(BsonClassMap.IsClassMapRegistered(typeof(VisitReportAmendment)));
    }
}
