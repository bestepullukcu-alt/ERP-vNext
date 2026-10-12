using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Xunit;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitReport;

/// <summary>
/// WP-E2E-FIX-1 — on the production handlers: (E9-B2) the calendar cell carries the atom's ContentItems as
/// <c>plannedContent</c>; (E9-B1 pre-condition) a report's actual journey / stage ids are stored as sent; (E9-B4) a
/// missing outcome code answers in the envelope's root <c>errors</c> — and the envelope's <c>data</c> of a failed
/// <c>Response&lt;Guid&gt;</c> is the empty-Guid STRING, the shape that hid the error from the Web client; (E9-B5) a
/// visit is not closed before its day (409 visit_not_yet_due), rescheduling stays free.
/// </summary>
public sealed class VisitExecutionFix1Tests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateOnly Today = new(2026, 10, 19);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private readonly FakeVisitReportRepository _reports = new();
    private readonly FakePlannedVisitReadRepository _plans = new();
    private readonly TimeProvider _clock = new FixedClock(new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.Zero));

    private static TenantContext TenantCtx()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(Tenant);
        return ctx;
    }

    private RecordVisitOutcomeHandler Outcome()
        => new(TenantCtx(), new NullActorContext(), _reports, _plans, TestCallerScope.Unrestricted(), _clock);

    private SubmitVisitReportHandler Submit()
        => new(TenantCtx(), new NullActorContext(), _reports, _plans, TestCallerScope.Unrestricted(), _clock);

    private GetVisitCalendarHandler Calendar()
        => new(TenantCtx(), _plans, _reports, TestCallerScope.Unrestricted(),
            new Diten.CrmService.Application.Features.PlannedVisit.VisitTargetNameReader(
                new Diten.CrmService.Application.Tests.PlannedVisit.FakeAccountRepository(),
                new Diten.CrmService.Application.Tests.PlannedVisit.FakeContactRepository()));

    private PlanAtom Seed(DateOnly date, List<PlannedVisitContentItem>? items = null)
    {
        var atom = new PlanAtom
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            VisitCode = "VP-E2E-TUT-0001",
            TargetType = PlannedVisitTargetType.Contact,
            TargetId = Guid.NewGuid(),
            PlannedDate = date,
            PlanStatus = PlannedVisitStatus.Planned,
            Resource = new PlannedVisitResourceRef { ResourceId = "rep-1", ResourceType = "person" },
            Content = new PlannedVisitContentRef { JourneyId = Guid.NewGuid(), StageId = Guid.NewGuid(), StageIndex = 0 },
            ContentItems = items ?? new List<PlannedVisitContentItem>()
        };
        _plans.Items.Add(atom);
        return atom;
    }

    private static VisitReportFeedbackInput Feedback(string? outcomeCode = "detaylama-tamamlandi")
        => new("ok", outcomeCode, false, null);

    // ── 1 · E9-B2 plannedContent ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Calendar_item_carries_the_atoms_content_items_as_planned_content()
    {
        var journeyId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var atom = Seed(Today, new List<PlannedVisitContentItem>
        {
            new()
            {
                ProductId = productId, ProductCode = "TUTUKON", Role = "promo",
                JourneyId = journeyId, JourneyCode = "CEJ-2026-8AD806",
                StageId = stageId, StageIndex = 0, StageCode = "E2E-TUT-S1", StageName = "Farkındalık",
                Steps = new List<PlannedVisitContentStep>
                {
                    new() { StepId = Guid.NewGuid(), ContentId = Guid.NewGuid(), Title = "Sindirim konforu", Type = "detailing" },
                    new() { StepId = Guid.NewGuid(), ContentId = Guid.NewGuid(), Title = "Doz şeması", Type = "leaflet" }
                }
            }
        });

        var res = await Calendar().Handle(
            new GetVisitCalendarQuery("2026-10-19", "2026-10-19", null), CancellationToken.None);

        var item = Assert.Single(res.Data!.Items);
        var content = Assert.Single(item.PlannedContent!);
        Assert.Equal(productId, content.ProductId);
        Assert.Equal("TUTUKON", content.ProductCode);
        Assert.Equal("promo", content.Role);
        Assert.Equal(journeyId, content.JourneyId);
        Assert.Equal("CEJ-2026-8AD806", content.JourneyCode);
        Assert.Equal(stageId, content.StageId);
        Assert.Equal(0, content.StageIndex);
        Assert.Equal("E2E-TUT-S1", content.StageCode);
        Assert.Equal("Farkındalık", content.StageName);
        Assert.Equal(new[] { "Sindirim konforu", "Doz şeması" }, content.Steps.Select(s => s.Title));
        Assert.Equal(new[] { "detailing", "leaflet" }, content.Steps.Select(s => s.Type));

        // The pre-existing (legacy single Content) fields are unchanged.
        Assert.Equal(atom.Content!.JourneyId, item.PlannedJourneyId);
        Assert.Equal(atom.Content.StageId, item.PlannedStageId);
        Assert.Equal(0, item.PlannedStageIndex);
    }

    [Fact]
    public async Task Calendar_item_without_content_items_has_an_empty_planned_content_list()
    {
        Seed(Today);

        var res = await Calendar().Handle(
            new GetVisitCalendarQuery("2026-10-19", "2026-10-19", null), CancellationToken.None);

        var item = Assert.Single(res.Data!.Items);
        Assert.NotNull(item.PlannedContent);
        Assert.Empty(item.PlannedContent!);
        Assert.Equal(0, item.PlannedStageIndex);
    }

    // ── 2 · E9-B1 pre-condition: actual journey / stage ids reach the report ─────────────────────────────────────

    [Fact]
    public async Task Submitted_report_stores_the_actual_journey_and_stage_ids()
    {
        var atom = Seed(Today);
        var journeyId = atom.Content!.JourneyId!.Value;
        var stageId = atom.Content.StageId!.Value;

        var res = await Submit().Handle(
            new SubmitVisitReportCommand(
                atom.Id,
                new VisitReportContentActualsInput(journeyId, stageId, 0, "E2E-TUT-S1", true, "E2E-TUT yolculuk", "Farkındalık"),
                null, Feedback(), null, null, null),
            CancellationToken.None);

        Assert.True(res.IsSuccessful);
        var actuals = Assert.Single(_reports.Items).ContentActuals!;
        Assert.Equal(journeyId, actuals.JourneyId);
        Assert.Equal(stageId, actuals.StageId);
        Assert.Equal(0, actuals.StageIndex);
        Assert.Equal("E2E-TUT-S1", actuals.StageCode);
        Assert.True(actuals.MatchedPlan);
        Assert.Equal("Farkındalık", actuals.StageDisplayName);
    }

    // ── 4 · E9-B4 outcome code refusal shape ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Empty_outcome_code_answers_in_the_root_errors_array_and_data_is_the_empty_guid_string()
    {
        var atom = Seed(Today);

        var res = await Submit().Handle(
            new SubmitVisitReportCommand(atom.Id, null, null, Feedback(outcomeCode: "  "), null, null, null),
            CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(400, res.StatusCode);
        Assert.Equal(VisitReportErrorCodes.OutcomeCodeRequired, res.Errors![1]); // [message, code]

        // The wire shape the Web client receives (ASP.NET web JSON defaults = camelCase): "data" is NOT null for a
        // failed Response<Guid> — it is the empty-Guid string, which a `body.data || body` reader takes as the payload
        // and so never reaches `errors` ("İşlem başarısız"). The errors live at the root.
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(res, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("data").ValueKind);
        Assert.Equal(Guid.Empty.ToString(), doc.RootElement.GetProperty("data").GetString());
        Assert.Contains(
            VisitReportErrorCodes.OutcomeCodeRequired,
            doc.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()));
    }

    // ── 5 · E9-B5 not-yet-due ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(VisitExecutionOutcome.Completed, null)]
    [InlineData(VisitExecutionOutcome.Missed, VisitReportReasonCodes.DoctorUnavailable)]
    public async Task A_future_visit_cannot_be_marked_completed_or_missed(string outcome, string? reason)
    {
        var atom = Seed(Today.AddDays(21)); // 9 Nov

        var res = await Outcome().Handle(
            new RecordVisitOutcomeCommand(atom.Id, outcome, null, reason, null, null, null, null), CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(409, res.StatusCode);
        Assert.Contains(VisitReportErrorCodes.NotYetDue, res.Errors!);
        Assert.Empty(_reports.Items);
    }

    [Fact]
    public async Task A_future_visit_can_still_be_rescheduled()
    {
        var atom = Seed(Today.AddDays(21));

        var res = await Outcome().Handle(
            new RecordVisitOutcomeCommand(
                atom.Id, VisitExecutionOutcome.Rescheduled, null, VisitReportReasonCodes.RescheduledByRep,
                "2026-11-16", null, null, null),
            CancellationToken.None);

        Assert.True(res.IsSuccessful);
        Assert.Equal(VisitExecutionOutcome.Rescheduled, Assert.Single(_reports.Items).ExecutionOutcome);
    }

    [Fact]
    public async Task A_report_on_a_future_visit_is_refused_409_not_yet_due()
    {
        var atom = Seed(Today.AddDays(1));

        var res = await Submit().Handle(
            new SubmitVisitReportCommand(atom.Id, null, null, Feedback(), null, null, null), CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(409, res.StatusCode);
        Assert.Equal(VisitReportErrorCodes.NotYetDue, res.Errors![1]);
        Assert.Empty(_reports.Items);
    }

    [Theory]
    [InlineData(0)]   // today
    [InlineData(-12)] // past
    public async Task Today_and_past_visits_take_an_outcome_and_a_report(int offsetDays)
    {
        var outcomeAtom = Seed(Today.AddDays(offsetDays));
        var reportAtom = Seed(Today.AddDays(offsetDays));

        var outcome = await Outcome().Handle(
            new RecordVisitOutcomeCommand(outcomeAtom.Id, VisitExecutionOutcome.Completed, null, null, null, null, null, null),
            CancellationToken.None);
        var report = await Submit().Handle(
            new SubmitVisitReportCommand(reportAtom.Id, null, null, Feedback(), null, null, null), CancellationToken.None);

        Assert.True(outcome.IsSuccessful);
        Assert.True(report.IsSuccessful);
    }

    [Fact]
    public void The_not_yet_due_code_is_published_in_the_contract_error_codes()
        => Assert.Contains("visit_not_yet_due", VisitReportErrorCodes.All);
}
