using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.DoctorInsights;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Application.Features.VisitReport.V2;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.StrategyTemplate;
using Diten.CrmService.Application.Tests.VisitExecution;
using Diten.CrmService.Application.Tests.VisitReport;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;
using ExecutionEntity = Diten.CrmService.Domain.Entities.VisitExecution;
using ReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W4 · W4-BE-a (Acceptance) on the PRODUCTION rules, service and handlers with the stores faked at their seams:
/// pre-fill from the W3 execution (marks / timings / steps / sample signature; personal notes never), previous values,
/// <c>isDue</c>; submit completeness with <c>missing[]</c> per field and the "none" answers; the pharmacy kind; the adverse
/// event question; the journey move (mapped / unmapped / unplanned); the recorder call; the competitor proposal to MDM and
/// MDM down; the 60-minute rule; old / new values in an amendment (v2 and v1); a v1 report still reads; competitor usage
/// counts (&lt;5, merged survivor); the journey stage's adoption code.
/// </summary>
public sealed class VwW4ReportV2Tests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000a4");
    private static readonly Guid ProductA = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid ProductB = Guid.Parse("b0000000-0000-0000-0000-00000000000b");
    private static readonly Guid StepPlanned = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private const string Rep = "rep-1";

    // ═══ 1 · pre-fill ═════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_prefill_takes_kind_minutes_steps_interest_objection_request_and_the_sample_signature_from_the_execution()
    {
        var w = new World();
        var plan = w.DoctorPlan();
        w.Executions.Items.Add(new ExecutionEntity
        {
            TenantId = Tenant, PlannedVisitId = plan.Id, ResourceId = Rep, Status = VisitExecutionStatus.Finished, Kind = VisitExecutionKinds.Remote,
            StartedAt = w.Clock.GetUtcNow().AddHours(-1), FinishedAt = w.Clock.GetUtcNow(), CoVisitorUserIds = { "co-1" },
            ProductTimings = { new VisitExecutionProductTiming { ProductId = ProductA, Seconds = 150 }, new VisitExecutionProductTiming { ProductId = ProductB, Seconds = 30 } },
            SlideTimings = { new VisitExecutionSlideTiming { ProductId = ProductA, StepId = StepPlanned, Seconds = 90 } },
            Marks = { new VisitExecutionMark { ProductId = ProductA, Interest = "interested", Flags = { VisitMarkFlags.Objection, VisitMarkFlags.SampleRequest }, ObjectionCategory = "price" } },
            PersonalNotes = { new VisitExecutionPersonalNote { ProductId = ProductA, Text = "PRIVATE NOTE" } },
            Signature = new VisitExecutionSignature { Confirmed = true, Context = VisitSignatureContexts.Sample }
        });

        var dto = (await w.Queries().Handle(new GetVisitReportV2PrefillQuery(plan.Id), default)).Data!;
        Assert.Equal((VisitReportKinds.Doctor, true), (dto.ReportKind, dto.SampleSigned));
        Assert.Equal((VisitExecutionKinds.Remote, plan.ContactId), (dto.Body.General!.VisitKindCode, dto.Body.General.MetContactId));
        Assert.Equal(new[] { "co-1" }, dto.Body.General.CoVisitorUserIds);

        var a = dto.Body.ProductSections!.Single(s => s.ProductId == ProductA);
        Assert.Equal((false, 3, "interested", true), (a.Unplanned, a.Minutes, a.InterestCode, a.MatchedPlan));
        Assert.Equal(StepPlanned, a.PresentedSteps.Single().StepId);
        Assert.Equal("price", a.Objections.Single().CategoryCode);
        Assert.Equal("sample", a.Requests.Single().TypeCode);
        var b = dto.Body.ProductSections!.Single(s => s.ProductId == ProductB);
        Assert.Equal((true, 1), (b.Unplanned, b.Minutes));                                   // presented without a plan line
        Assert.DoesNotContain("PRIVATE NOTE", System.Text.Json.JsonSerializer.Serialize(dto)); // personal notes never reach the report

        Assert.True(dto.Profile!.TotalIsDue);                                 // never asked ⇒ due
        Assert.True(dto.Potentials.Single(p => p.ProductId == ProductA).IsDue);
        Assert.True(dto.CompetitorsUnavailable);                              // MDM not wired ⇒ unavailable, the report goes on
    }

    [Fact]
    public async Task The_prefill_shows_the_previous_report_values_and_due_follows_the_three_month_rule()
    {
        var w = new World();
        var earlier = w.DoctorPlan(daysAgo: 20);
        var plan = w.DoctorPlan(contactId: earlier.ContactId);
        w.Reports.Items.Add(new ReportEntity
        {
            TenantId = Tenant, PlannedVisitId = earlier.Id, SchemaVersion = 2, ReportKind = VisitReportKinds.Doctor, ReportStatus = VisitReportStatus.Submitted,
            ExecutionOutcome = VisitExecutionOutcome.Completed, ExecutedAt = w.Clock.GetUtcNow().AddDays(-20), SubmittedAt = w.Clock.GetUtcNow().AddDays(-20),
            ProductSections = { new VisitReportProductSection { ProductId = ProductA, AdoptionStageCode = "aware", ShareBucketCode = "half" } }
        });
        w.Potentials.Add(earlier.ContactId!.Value, ProductA, asOf: w.Clock.GetUtcNow().AddDays(-30));

        var dto = (await w.Queries().Handle(new GetVisitReportV2PrefillQuery(plan.Id), default)).Data!;
        Assert.Equal(("aware", "half"), (dto.Previous!.ProductSections.Single().AdoptionStageCode, dto.Previous.ProductSections.Single().ShareBucketCode));
        Assert.False(dto.Potentials.Single(p => p.ProductId == ProductA).IsDue);  // 30 days old ⇒ the last value is shown, not asked
        Assert.Null(dto.Body.ProductSections!.Single().AdoptionStageCode);         // the previous value is offered, never pre-set
    }

    [Fact]
    public async Task A_pharmacy_visit_is_a_pharmacy_report_and_a_foreign_plan_is_404()
    {
        var w = new World();
        var pharmacy = w.PharmacyPlan();
        var dto = (await w.Queries().Handle(new GetVisitReportV2PrefillQuery(pharmacy.Id), default)).Data!;
        Assert.Equal(VisitReportKinds.Pharmacy, dto.ReportKind);
        Assert.NotNull(dto.Body.Pharmacy);
        Assert.Empty(dto.Body.ProductSections!);

        var foreign = w.DoctorPlan(resource: "rep-2");
        Assert.Equal(404, (await w.Queries().Handle(new GetVisitReportV2PrefillQuery(foreign.Id), default)).StatusCode);
    }

    // ═══ 2 · submit completeness ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task An_empty_doctor_report_lists_every_missing_answer_and_the_adverse_event_question_is_required()
    {
        var w = new World();
        var plan = w.DoctorPlan();
        var refused = await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, new VisitReportV2Body(), null), default);
        Assert.Equal(400, refused.StatusCode);
        Assert.Contains(VisitReportV2Errors.Incomplete, refused.Errors!);
        var missing = refused.Data!.Missing.Select(m => (m.Section, m.ProductId, m.Field)).ToList();
        Assert.Contains(("safety", (Guid?)null, "adverseEvent"), missing);
        Assert.Contains(("safety", (Guid?)null, "offLabelRequest"), missing);
        Assert.Contains(("general", (Guid?)null, "visitKindCode"), missing);
        Assert.Contains(("doctor", (Guid?)null, "totalMonthlyPatients"), missing);   // the total is due
        Assert.Contains(("product", (Guid?)ProductA, "section"), missing);           // a planned product without a section
        Assert.Empty(w.Reports.Items);

        // every product field is named
        var body = w.Complete(plan) with { };
        body.ProductSections![0].VisitSuccessCode = null;
        body.ProductSections[0].NoSample = false;
        body.Safety!.AdverseEvent = true;
        body.Safety.AdverseEventNote = null;
        var second = (await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).Data!.Missing.Select(m => m.Field).ToList();
        Assert.Equal(new[] { "adverseEventNote", "visitSuccessCode", "sample" }, second);
    }

    [Fact]
    public async Task The_none_answers_complete_a_report_and_the_potential_is_required_only_when_due_or_updated()
    {
        var w = new World();
        var plan = w.DoctorPlan();
        w.Potentials.Add(plan.ContactId!.Value, ProductA, asOf: w.Clock.GetUtcNow().AddDays(-10)); // not due
        w.Profiles.Items.Add(new DoctorProfile { TenantId = Tenant, ContactId = plan.ContactId.Value, TotalMonthlyPatients = 80, TotalPatientsAsOf = w.Clock.GetUtcNow().AddDays(-5), Version = 1 });

        var body = w.Complete(plan);
        body.ProductSections![0].Potential = null;  // not due ⇒ not asked
        body.Doctor!.TotalMonthlyPatients = null;   // not due ⇒ not asked
        var ok = await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default);
        Assert.True(ok.IsSuccessful, string.Join(" | ", ok.Errors ?? Array.Empty<string>()) + string.Join(",", ok.Data?.Missing.Select(m => m.Field) ?? Array.Empty<string>()));

        // the rep pressed "update" ⇒ the numbers are required again
        var w2 = new World();
        var plan2 = w2.DoctorPlan();
        w2.Potentials.Add(plan2.ContactId!.Value, ProductA, asOf: w2.Clock.GetUtcNow().AddDays(-10));
        var body2 = w2.Complete(plan2);
        body2.ProductSections![0].Potential = new VisitReportPotential { Updated = true };
        var missing = (await w2.Handlers().Handle(new SubmitVisitReportV2Command(plan2.Id, body2, null), default)).Data!.Missing;
        Assert.Equal(("product", "potential"), (missing.Single().Section, missing.Single().Field));
    }

    [Fact]
    public async Task A_pharmacy_report_asks_role_trend_and_adverse_event_but_never_off_label()
    {
        var w = new World();
        var plan = w.PharmacyPlan();
        var body = new VisitReportV2Body(new VisitReportGeneral { VisitKindCode = "in_person" }, null, null,
            new VisitReportPharmacy { Sales = { new VisitReportSalesRow { GskuId = Guid.NewGuid() } } }, new VisitReportSafety(), new VisitReportFollowUp());
        var missing = (await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).Data!.Missing.Select(m => m.Field).ToList();
        Assert.Equal(new[] { "adverseEvent", "metRoleCode", "sales.trendCode" }, missing);

        body.General!.MetRoleCode = "pharmacist";
        body.Pharmacy!.Sales[0].TrendCode = "up";
        body.Safety!.AdverseEvent = false;
        body.Safety.OffLabelRequest = true; // ignored at a pharmacy
        var ok = await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default);
        Assert.True(ok.IsSuccessful);
        Assert.Null(w.Reports.Items.Single().Safety!.OffLabelRequest);
        Assert.Equal(VisitReportKinds.Pharmacy, w.Reports.Items.Single().ReportKind);
    }

    [Fact]
    public async Task A_code_outside_its_set_is_400_and_an_unreadable_set_503()
    {
        var w = new World();
        var plan = w.DoctorPlan();
        var body = w.Complete(plan);
        body.ProductSections![0].VisitSuccessCode = "great";
        var bad = await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default);
        Assert.Equal(400, bad.StatusCode);
        Assert.Contains(VisitReportV2Errors.CodeInvalid, bad.Errors!);

        w.Codes.Missing = true;
        body.ProductSections[0].VisitSuccessCode = "reached";
        Assert.Equal(503, (await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).StatusCode);
    }

    // ═══ 3 · submit side effects ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Submit_records_the_doctor_insights_with_the_report_as_source()
    {
        var w = new World();
        var plan = w.DoctorPlan();
        var body = w.Complete(plan);
        body.Doctor!.KolLevelCode = "local";
        body.Doctor.TotalMonthlyPatients = 90;
        body.ProductSections![0].Potential = new VisitReportPotential { NoPatientsInIndications = true };

        var ok = await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default);
        Assert.True(ok.IsSuccessful);
        var call = Assert.Single(w.Recorder.Records);
        Assert.Equal((plan.ContactId!.Value, ok.Data!.VisitReportId!.Value, 90, "local"), (call.ContactId, call.ReportId, call.Total!.Value, call.Profile!.KolLevelCode));
        var product = call.Products.Single();
        Assert.Equal((ProductA, true, "half"), (product.ProductId, product.NoPatients!.Value, product.ShareBucket));

        // the recorder's check refuses (e.g. the total rule) ⇒ nothing is saved
        var w2 = new World();
        var plan2 = w2.DoctorPlan();
        w2.Recorder.Refuse = DoctorInsightsResult.Fail(DoctorInsightsErrors.PotentialExceedsTotal, "too many");
        var refused = await w2.Handlers().Handle(new SubmitVisitReportV2Command(plan2.Id, w2.Complete(plan2), null), default);
        Assert.Contains(DoctorInsightsErrors.PotentialExceedsTotal, refused.Errors!);
        Assert.Empty(w2.Reports.Items);
    }

    [Fact]
    public async Task The_journey_moves_to_the_first_stage_mapped_after_the_reported_adoption_stage()
    {
        var w = new World();
        var plan = w.DoctorPlan();
        w.Journey(("aware", 0), ("evaluating", 1), ("trying", 2));
        var body = w.Complete(plan);
        body.ProductSections![0].AdoptionStageCode = "aware";
        Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).IsSuccessful);
        var progress = w.Progress.Items.Single();
        Assert.Equal((1, 0, 1), (progress.CurrentStageIndex, progress.LastCompletedStageIndex!.Value, progress.ExposureCount));

        // a re-submit inside the window never moves it twice
        Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).IsSuccessful);
        Assert.Equal((1, 1), (w.Progress.Items.Single().CurrentStageIndex, w.Progress.Items.Single().ExposureCount));
    }

    [Fact]
    public async Task An_unmapped_journey_advances_from_the_presented_stage_and_an_unplanned_product_never_moves_one()
    {
        var w = new World();
        var plan = w.DoctorPlan(stageIndex: 1);
        w.Journey((null, 0), (null, 1), (null, 2));
        var body = w.Complete(plan);
        body.ProductSections![0].AdoptionStageCode = "advocate";
        var unplanned = Section(ProductB);
        unplanned.Unplanned = true;
        body = body with { ProductSections = body.ProductSections.Append(unplanned).ToList() };
        Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).IsSuccessful);

        var progress = Assert.Single(w.Progress.Items);      // ProductB (unplanned) has none
        Assert.Equal((ProductA, 2), (progress.ProductId, progress.CurrentStageIndex));
    }

    [Fact]
    public void The_journey_target_rule_mapped_after_stay_and_none()
    {
        static ContentEngagementJourneyStage S(string? code) => new() { AdoptionStageCode = code };
        var stages = new[] { S("aware"), S(null), S("trying"), S("regular") };
        Assert.Equal(2, VisitReportV2Rules.JourneyTarget(stages, "aware"));      // the first mapped AFTER aware
        Assert.Equal(2, VisitReportV2Rules.JourneyTarget(stages, "evaluating")); // an unmapped report stage still finds the next
        Assert.Equal(-1, VisitReportV2Rules.JourneyTarget(stages, "advocate"));  // nothing after ⇒ stay
        Assert.Null(VisitReportV2Rules.JourneyTarget(new[] { S(null), S(null) }, "aware")); // no mapping ⇒ Advance
        Assert.Null(VisitReportV2Rules.JourneyTarget(stages, null));
        Assert.Equal("commitment:will_try", VisitReportV2Rules.Suggestion("will_try", "aware"));
        Assert.Equal("stage:evaluating", VisitReportV2Rules.Suggestion("none", "aware"));
        Assert.Null(VisitReportV2Rules.Suggestion(null, "advocate"));
    }

    [Fact]
    public async Task A_proposed_competitor_goes_to_mdm_on_submit_and_mdm_down_keeps_it_pending()
    {
        var w = new World();
        w.Mdm.Available = true;
        var plan = w.DoctorPlan();
        var body = w.Complete(plan);
        body.ProductSections![0].NoCompetitor = false;
        var missing = (await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default)).Data!.Missing;
        Assert.Equal("competitors", missing.Single().Field);   // MDM answered ⇒ "uses none" or a competitor is required

        body.ProductSections[0].Competitors.Add(new VisitReportCompetitor { NameSnapshot = "Rakip X", Company = "Firma", Proposed = true });
        var ok = await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, body, null), default);
        Assert.True(ok.IsSuccessful);
        var proposal = Assert.Single(w.Mdm.Proposals);
        Assert.Equal(("Rakip X", "Firma", ProductA, ok.Data!.VisitReportId), (proposal.Name, proposal.Company, proposal.ProductId, proposal.ReportId));
        var stored = w.Reports.Items.Single().ProductSections[0].Competitors.Single();
        Assert.Equal((w.Mdm.NextId, true), (stored.CompetitorProductId!.Value, stored.ProposalForwarded));

        // MDM down: no competitor answer is required, a proposal stays pending with a warning
        var w2 = new World();
        var plan2 = w2.DoctorPlan();
        var body2 = w2.Complete(plan2);
        body2.ProductSections![0].NoCompetitor = false;
        body2.ProductSections[0].Competitors.Add(new VisitReportCompetitor { NameSnapshot = "Rakip Y", Company = "Firma", Proposed = true });
        var down = await w2.Handlers().Handle(new SubmitVisitReportV2Command(plan2.Id, body2, null), default);
        Assert.True(down.IsSuccessful);
        Assert.Contains("competitor_proposal_pending:Rakip Y", down.Data!.Warnings);
        Assert.False(w2.Reports.Items.Single().ProductSections[0].Competitors.Single().ProposalForwarded);
    }

    [Fact]
    public async Task The_sixty_minute_rule_holds_for_v2_and_a_draft_is_free_then_locked_after_submit()
    {
        var w = new World();
        var plan = w.DoctorPlan();
        var draft = await w.Handlers().Handle(new SaveVisitReportV2DraftCommand(plan.Id, new VisitReportV2Body(General: new VisitReportGeneral { GeneralNote = "half way" }), null), default);
        Assert.Equal((201, VisitReportStatus.Draft), (draft.StatusCode, draft.Data!.ReportStatus));
        Assert.Equal(409, (await w.Handlers().Handle(new SaveVisitReportV2DraftCommand(plan.Id, new VisitReportV2Body(), 7), default)).StatusCode); // If-Match

        Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, w.Complete(plan), null), default)).IsSuccessful);
        Assert.Equal(409, (await w.Handlers().Handle(new SaveVisitReportV2DraftCommand(plan.Id, new VisitReportV2Body(), null), default)).StatusCode);
        w.Clock.Advance(TimeSpan.FromMinutes(59));
        Assert.True((await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, w.Complete(plan), null), default)).IsSuccessful);
        w.Clock.Advance(TimeSpan.FromMinutes(2));
        var late = await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, w.Complete(plan), null), default);
        Assert.Equal(409, late.StatusCode);
    }

    // ═══ 4 · amendments ════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_v2_amendment_keeps_every_changed_value_with_its_old_and_new_value()
    {
        var w = new World();
        var plan = w.DoctorPlan();
        var id = (await w.Handlers().Handle(new SubmitVisitReportV2Command(plan.Id, w.Complete(plan), null), default)).Data!.VisitReportId!.Value;

        var corrected = w.Complete(plan);
        corrected.ProductSections![0].ShareBucketCode = "mostly";
        corrected.Safety!.AdverseEvent = true;
        corrected.Safety.AdverseEventNote = "Baş ağrısı";
        Assert.Equal(400, (await w.Handlers().Handle(new AmendVisitReportV2Command(id, " ", corrected, null), default)).StatusCode); // reason required
        var ok = await w.Handlers().Handle(new AmendVisitReportV2Command(id, "Pay yanlış girildi", corrected, null), default);
        Assert.True(ok.IsSuccessful);

        var amendment = w.Reports.Items.Single().Amendments.Single();
        var share = amendment.Changes.Single(c => c.Path == $"productSections[{ProductA:D}].shareBucketCode");
        Assert.Equal(("\"half\"", "\"mostly\""), (share.Old, share.New));
        Assert.Contains(amendment.Changes, c => c.Path == "safety.adverseEventNote" && c.Old is null && c.New == "\"Baş ağrısı\"");
        Assert.Equal(new[] { "productSections[" + ProductA.ToString("D") + "]", "safety" }, amendment.ChangedFields);
        Assert.Equal(VisitReportStatus.Amended, w.Reports.Items.Single().ReportStatus);
    }

    [Fact]
    public async Task A_v1_amendment_now_keeps_the_old_and_new_value_too_and_a_v1_report_still_reads()
    {
        var w = new World();
        var plan = w.DoctorPlan();
        var report = new ReportEntity
        {
            TenantId = Tenant, PlannedVisitId = plan.Id, ExecutionOutcome = VisitExecutionOutcome.Completed, ReportStatus = VisitReportStatus.Submitted,
            ReportedByResourceId = Rep, ExecutedAt = w.Clock.GetUtcNow(), SubmittedAt = w.Clock.GetUtcNow().AddDays(-3),
            Feedback = new VisitReportFeedback { OutcomeCode = "positive", DoctorFeedback = "ok" }
        };
        w.Reports.Items.Add(report);
        var amend = new AmendVisitReportHandler(w.Ctx(), new Actor(Rep), w.Reports, w.Plans, w.Caller());
        Assert.True((await amend.Handle(new AmendVisitReportCommand(report.Id, "Yanlış sonuç", null, null, null,
            new Features.VisitReport.VisitReportFeedbackInput("ok", "negative", false, null), null), default)).IsSuccessful);
        var change = w.Reports.Items.Single().Amendments.Single().Changes.Single(c => c.Path == "feedback.outcomeCode");
        Assert.Equal(("\"positive\"", "\"negative\""), (change.Old, change.New));

        var read = (await new GetVisitReportByIdHandler(w.Ctx(), w.Reports, w.Plans, w.Caller()).Handle(new GetVisitReportByIdQuery(report.Id), default)).Data!;
        Assert.Equal((1, (string?)null), (read.SchemaVersion, read.ReportKind));
        Assert.Null(read.ProductSections);
        Assert.Equal("feedback.outcomeCode", read.Amendments.Single().Changes!.Single(c => c.Path == "feedback.outcomeCode").Path);
        Assert.Equal(409, (await w.Handlers().Handle(new AmendVisitReportV2Command(report.Id, "x", new VisitReportV2Body(), null), default)).StatusCode); // v1 ⇒ the v1 body
    }

    // ═══ 5 · competitor usage ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Competitor_usage_counts_without_identity_hides_cells_under_five_and_counts_a_merged_record_as_its_survivor()
    {
        var w = new World();
        Guid survivor = Guid.NewGuid(), merged = Guid.NewGuid();
        w.Mdm.Available = true;
        w.Mdm.MergedInto[merged] = survivor;
        for (var i = 0; i < 6; i++)
        {
            var plan = w.DoctorPlan(daysAgo: i);
            w.Reports.Items.Add(new ReportEntity
            {
                TenantId = Tenant, PlannedVisitId = plan.Id, SchemaVersion = 2, ReportKind = VisitReportKinds.Doctor, ReportStatus = VisitReportStatus.Submitted,
                ExecutionOutcome = VisitExecutionOutcome.Completed, ExecutedAt = w.Clock.GetUtcNow().AddDays(-i),
                ProductSections = { new VisitReportProductSection { ProductId = i < 3 ? ProductA : ProductB,
                    Competitors = { new VisitReportCompetitor { CompetitorProductId = i % 2 == 0 ? survivor : merged, NameSnapshot = "X" } } } }
            });
        }

        var usage = (await w.Queries().Handle(new GetCompetitorUsageQuery(new[] { merged }, 3), default)).Data!;
        var item = usage.Items.Single();
        Assert.Equal((survivor, "6", "6", true), (item.CompetitorProductId, item.DoctorCount, item.ReportCount, usage.MergeResolved));
        Assert.Equal(new[] { "<5", "<5" }, item.ByProduct.Select(c => c.Count));
        Assert.Equal("6", item.ByCountry.Single(c => c.Country == "TR").Count);
        Assert.DoesNotContain("Doctor ", System.Text.Json.JsonSerializer.Serialize(usage));
        Assert.Equal(400, (await w.Queries().Handle(new GetCompetitorUsageQuery(Array.Empty<Guid>(), 3), default)).StatusCode);
    }

    // ═══ 6 · the journey stage's adoption code ═════════════════════════════════════════════════════════

    [Fact]
    public void A_journey_stage_takes_a_known_adoption_code_once()
    {
        var journey = new ContentEngagementJourney { Stages = { new ContentEngagementJourneyStage { StageId = Guid.NewGuid(), AdoptionStageCode = "aware" } } };
        Assert.Null(ContentEngagementJourneyValidation.ValidateAdoptionStage(journey, null, null));
        Assert.Null(ContentEngagementJourneyValidation.ValidateAdoptionStage(journey, "Trying", null));
        Assert.NotNull(ContentEngagementJourneyValidation.ValidateAdoptionStage(journey, "visionary", null));
        Assert.NotNull(ContentEngagementJourneyValidation.ValidateAdoptionStage(journey, "aware", null));                      // once per journey
        Assert.Null(ContentEngagementJourneyValidation.ValidateAdoptionStage(journey, "aware", journey.Stages[0].StageId)); // the stage itself
    }

    // ═══ 7 · persistence ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void The_v2_sections_store_every_guid_as_a_string_and_a_pre_w4_report_reads_as_v1()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var report = new ReportEntity
        {
            SchemaVersion = 2, General = new VisitReportGeneral { MetContactId = Guid.NewGuid() },
            ProductSections = { new VisitReportProductSection { ProductId = ProductA, PresentedSteps = { new VisitReportPresentedStep { StepId = StepPlanned } },
                Potential = new VisitReportPotential { Rows = { new VisitReportPotentialRow { IndicationNodeId = Guid.NewGuid() } } },
                Competitors = { new VisitReportCompetitor { CompetitorProductId = Guid.NewGuid() } },
                Requests = { new VisitReportRequest { DueDate = new DateOnly(2026, 11, 2) } },
                Sample = new VisitReportV2Sample { GskuId = Guid.NewGuid(), Quantity = 2 } } },
            Pharmacy = new VisitReportPharmacy { Recommendation = new VisitReportRecommendation { LinkedContactIds = { Guid.NewGuid() } } }
        };
        var doc = MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(report);
        var section = doc["ProductSections"].AsBsonArray[0].AsBsonDocument;
        Assert.Equal(ProductA.ToString(), section["ProductId"].AsString);
        Assert.Equal(MongoDB.Bson.BsonType.String, section["PresentedSteps"].AsBsonArray[0]["StepId"].BsonType);
        Assert.Equal(MongoDB.Bson.BsonType.String, section["Potential"]["Rows"].AsBsonArray[0]["IndicationNodeId"].BsonType);
        Assert.Equal(MongoDB.Bson.BsonType.String, section["Competitors"].AsBsonArray[0]["CompetitorProductId"].BsonType);
        Assert.Equal("2026-11-02", section["Requests"].AsBsonArray[0]["DueDate"].AsString);
        Assert.Equal(MongoDB.Bson.BsonType.String, section["Sample"]["GskuId"].BsonType);
        Assert.Equal(MongoDB.Bson.BsonType.String, doc["General"]["MetContactId"].BsonType);
        Assert.Equal(MongoDB.Bson.BsonType.String, doc["Pharmacy"]["Recommendation"]["LinkedContactIds"].AsBsonArray[0].BsonType);

        doc.Remove("SchemaVersion");
        Assert.Equal(1, MongoDB.Bson.Serialization.BsonSerializer.Deserialize<ReportEntity>(doc).SchemaVersion);
    }

    // ═══ helpers ═════════════════════════════════════════════════════════════════════════════════════════

    private static VisitReportProductSection Section(Guid product) => new()
    {
        ProductId = product, Minutes = 5, VisitSuccessCode = "reached", CommitmentCode = "none", InterestCode = "neutral",
        AdoptionStageCode = "aware", ShareBucketCode = "half", NoCompetitor = true, NoObjection = true, NoRequest = true, NoSample = true,
        NextMessage = "Kanıtı göster", Potential = new VisitReportPotential { NoPatientsInIndications = true }
    };

    private sealed class World
    {
        public TestClock Clock { get; } = new(new DateTimeOffset(2026, 10, 13, 15, 0, 0, TimeSpan.Zero));
        public FakePlannedVisitRepository Plans { get; } = new();
        public FakeVisitReportRepository Reports { get; } = new();
        public FakeVisitExecutionRepository Executions { get; } = new();
        public FakeContentEngagementJourneyRepository Journeys { get; } = new();
        public ProgressStore Progress { get; } = new();
        public InMemoryContacts Contacts { get; } = new();
        public FakeAccountRepository Accounts { get; } = new();
        public ProfileStore Profiles { get; } = new();
        public PotentialStore Potentials { get; } = new();
        public CodeCheck Codes { get; } = new();
        public RecorderSpy Recorder { get; } = new();
        public MdmFake Mdm { get; } = new();
        private Guid _journeyId = Guid.NewGuid();

        public TenantContext Ctx()
        {
            var ctx = new TenantContext();
            ctx.SetTenant(Tenant);
            return ctx;
        }

        public TestCallerScope Caller() => new(Rep);

        public VisitReportV2QueryHandlers Queries() => new(Service());

        public VisitReportV2Handlers Handlers() => new(Service());

        private VisitReportV2Service Service()
        {
            var indications = new ProductIndicationProfileReader(new NoTypes(), new NoNodes(), new NoRelationships(), null, Clock);
            return new VisitReportV2Service(Ctx(), new Actor(Rep), Caller(), Reports, Plans, Executions, Codes,
                Profiles, Potentials, new NoThresholds(), indications, Recorder, Progress, Journeys, Contacts, Accounts, Mdm.Available ? Mdm : Mdm.Down, null, Clock);
        }

        public PlanAtom DoctorPlan(int daysAgo = 0, Guid? contactId = null, string resource = Rep, int stageIndex = 0)
        {
            var contact = contactId ?? Guid.NewGuid();
            if (Contacts.Items.All(c => c.Id != contact)) Contacts.Items.Add(new Contact { Id = contact, TenantId = Tenant, DisplayName = "Doctor " + contact.ToString("N")[..4], CountryRef = "TR" });
            var plan = new PlanAtom
            {
                TenantId = Tenant, VisitCode = "PV-" + Guid.NewGuid().ToString("N")[..6], TargetType = PlannedVisitTargetType.Contact, TargetId = contact, ContactId = contact,
                PlannedDate = DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime).AddDays(-daysAgo), Resource = new PlannedVisitResourceRef { ResourceId = resource },
                PlanStatus = PlannedVisitStatus.Planned,
                ContentItems = { new PlannedVisitContentItem { ProductId = ProductA, JourneyId = _journeyId, StageIndex = stageIndex, Order = 1, Steps = { new PlannedVisitContentStep { StepId = StepPlanned } } } }
            };
            Plans.Items.Add(plan);
            return plan;
        }

        public PlanAtom PharmacyPlan()
        {
            var account = Guid.NewGuid();
            Accounts.Items.Add(new Account { Id = account, TenantId = Tenant, CountryRef = "TR" });
            var plan = new PlanAtom
            {
                TenantId = Tenant, VisitCode = "PH-" + Guid.NewGuid().ToString("N")[..6], TargetType = PlannedVisitTargetType.Pharmacy, TargetId = account, AccountId = account,
                PlannedDate = DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime), Resource = new PlannedVisitResourceRef { ResourceId = Rep }, PlanStatus = PlannedVisitStatus.Planned
            };
            Plans.Items.Add(plan);
            return plan;
        }

        public void Journey(params (string? Adoption, int Order)[] stages)
        {
            var journey = new ContentEngagementJourney { Id = _journeyId, TenantId = Tenant, JourneyCode = "J" };
            foreach (var (adoption, order) in stages)
            {
                journey.Stages.Add(new ContentEngagementJourneyStage { StageOrder = order, StageCode = "S" + order, AdoptionStageCode = adoption });
            }

            Journeys.Rows.Add(journey);
        }

        /// <summary>A complete doctor report for <paramref name="plan"/>.</summary>
        public VisitReportV2Body Complete(PlanAtom plan) => new(
            new VisitReportGeneral { VisitKindCode = "in_person", MetContactId = plan.ContactId },
            new List<VisitReportProductSection> { Section(ProductA) },
            new VisitReportDoctorLevel { TotalMonthlyPatients = 100 },
            null,
            new VisitReportSafety { AdverseEvent = false, OffLabelRequest = false },
            new VisitReportFollowUp());
    }

    private sealed class Actor(string? name) : IActorContext
    {
        public string? ActorName { get; } = name;
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>Every report / profile set accepts its seed codes; <see cref="Missing"/> = the set cannot be read.</summary>
    private sealed class CodeCheck : IReferenceDataValidator
    {
        private static readonly Dictionary<string, string[]> Sets = new()
        {
            ["crm-visit-success"] = ["reached", "partial", "not_reached"],
            ["crm-doctor-commitment"] = ["will_try", "will_prescribe", "wants_info", "none"],
            ["crm-visit-interest"] = ["interested", "neutral", "not_interested"],
            ["crm-adoption-stage"] = ["unaware", "aware", "evaluating", "trying", "regular", "advocate"],
            ["crm-share-bucket"] = ["none", "occasional", "half", "mostly", "always"],
            ["crm-visit-request-type"] = ["literature", "sample", "patient_material", "training_invite", "msl_meeting", "other"],
            ["crm-pharmacy-met-role"] = ["pharmacist", "technician", "owner", "other"],
            ["crm-sales-trend"] = ["up", "flat", "down"],
            ["crm-kol-level"] = ["none", "local", "regional", "national", "international"],
            ["visit-objection-category"] = ["price", "other"]
        };

        public bool Missing { get; set; }

        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(new ReferenceValidationResult(
                Missing ? ReferenceValidationStatus.SetMissing
                : Sets.TryGetValue(setCode, out var v) && v.Contains(value) ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue,
                setCode, value));
    }

    private sealed class RecorderSpy : IDoctorInsightsRecorder
    {
        public List<(Guid ContactId, Guid ReportId, DoctorProfileSuggestion? Profile, int? Total, IReadOnlyList<DoctorProductInsightInput> Products)> Records { get; } = new();
        public DoctorInsightsResult Refuse { get; set; } = DoctorInsightsResult.Success;

        public Task<DoctorInsightsResult> CheckFromReportAsync(Guid tenantId, Guid contactId, DoctorProfileSuggestion? profileSuggestion, int? totalPatients,
            IReadOnlyList<DoctorProductInsightInput> perProduct, CancellationToken ct) => Task.FromResult(Refuse);

        public Task<DoctorInsightsResult> RecordFromReportAsync(Guid tenantId, Guid contactId, Guid visitReportId, string? actor,
            DoctorProfileSuggestion? profileSuggestion, int? totalPatients, IReadOnlyList<DoctorProductInsightInput> perProduct, CancellationToken ct)
        {
            Records.Add((contactId, visitReportId, profileSuggestion, totalPatients, perProduct));
            return Task.FromResult(DoctorInsightsResult.Success);
        }
    }

    private sealed class MdmFake : IMdmCompetitorClient
    {
        public bool Available { get; set; }
        public Guid NextId { get; } = Guid.NewGuid();
        public List<(string Name, string Company, Guid ProductId, Guid? ReportId)> Proposals { get; } = new();
        public Dictionary<Guid, Guid> MergedInto { get; } = new();
        public IMdmCompetitorClient Down { get; } = new UnavailableMdmCompetitorClient();

        public Task<CompetitorLookupResult> LookupAsync(Guid productId, Guid? indicationNodeId, string? market, string? q, CancellationToken ct)
            => Task.FromResult(new CompetitorLookupResult(true, new[] { new CompetitorOptionDto(Guid.NewGuid(), "Rakip", "Firma", Array.Empty<string>(), null, null, "active", null) }));

        public Task<CompetitorProposalResult> ProposeAsync(string name, string company, Guid productId, Guid? reportId, CancellationToken ct)
        {
            Proposals.Add((name, company, productId, reportId));
            return Task.FromResult(new CompetitorProposalResult(true, NextId, null));
        }

        public Task<IReadOnlyDictionary<Guid, Guid>?> ResolveSurvivorsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<Guid, Guid>?>(ids.ToDictionary(id => id, id => MergedInto.TryGetValue(id, out var s) ? s : id));
    }

    private sealed class ProgressStore : IJourneyProgressRepository
    {
        public List<JourneyProgress> Items { get; } = new();

        public Task<JourneyProgress?> GetByKeyAsync(Guid tenantId, Guid contactId, Guid productId, Guid journeyId, CancellationToken cancellationToken)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == tenantId && p.ContactId == contactId && p.ProductId == productId && p.JourneyId == journeyId));

        public Task<IReadOnlyList<JourneyProgress>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<JourneyProgress>>(Items.Where(p => p.TenantId == tenantId && p.ContactId == contactId).ToList());

        public Task<bool> UpsertAsync(JourneyProgress entity, int expectedVersion, CancellationToken cancellationToken)
        {
            var existing = Items.FirstOrDefault(p => p.Id == entity.Id);
            if (expectedVersion == 0 && existing is null) { entity.Version = 1; Items.Add(entity); return Task.FromResult(true); }
            if (existing is null || existing.Version != expectedVersion) return Task.FromResult(false);
            entity.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class ProfileStore : IDoctorProfileRepository
    {
        public List<DoctorProfile> Items { get; } = new();
        public Task<DoctorProfile?> GetByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == tenantId && p.ContactId == contactId));
        public Task<IReadOnlyList<DoctorProfile>> ListByContactsAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(DoctorProfile entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ReplaceAsync(DoctorProfile entity, int expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class PotentialStore : IDoctorProductPotentialRepository
    {
        public List<DoctorProductPotential> Items { get; } = new();
        public void Add(Guid contact, Guid product, DateTimeOffset asOf) => Items.Add(new DoctorProductPotential { TenantId = Tenant, ContactId = contact, GlobalProductId = product, AsOf = asOf, NoPatientsInIndications = true, Version = 1 });
        public Task<DoctorProductPotential?> GetAsync(Guid tenantId, Guid contactId, Guid productId, CancellationToken cancellationToken) => Task.FromResult(Items.FirstOrDefault(p => p.ContactId == contactId && p.GlobalProductId == productId));
        public Task<IReadOnlyList<DoctorProductPotential>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DoctorProductPotential>>(Items.Where(p => p.TenantId == tenantId && p.ContactId == contactId).ToList());
        public Task<IReadOnlyList<DoctorProductPotential>> ListByContactsAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, Guid? productId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DoctorProductPotential>> ListAsync(Guid tenantId, Guid? productId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(DoctorProductPotential entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ReplaceAsync(DoctorProductPotential entity, int expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoThresholds : IProductPotentialThresholdRepository
    {
        public Task<IReadOnlyList<ProductPotentialThreshold>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductPotentialThreshold>>(Array.Empty<ProductPotentialThreshold>());
        public Task<ProductPotentialThreshold?> GetAsync(Guid tenantId, Guid? productId, CancellationToken cancellationToken) => Task.FromResult<ProductPotentialThreshold?>(null);
        public Task InsertAsync(ProductPotentialThreshold entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ReplaceAsync(ProductPotentialThreshold entity, int expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoTypes : IConceptTypeRepository
    {
        public Task<ConceptType?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult<ConceptType?>(null);
        public Task<IReadOnlyList<ConceptType>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ConceptType>>(Array.Empty<ConceptType>());
        public Task<IReadOnlyList<ConceptType>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConceptType?> GetActiveByCodeAsync(Guid tenantId, Guid subjectId, string conceptTypeCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(ConceptType entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(ConceptType entity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoNodes : IConceptNodeRepository
    {
        public Task<ConceptNode?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult<ConceptNode?>(null);
        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ConceptNode>>(Array.Empty<ConceptNode>());
        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConceptNode?> GetActiveByCodeAsync(Guid tenantId, Guid subjectId, Guid conceptTypeId, string conceptNodeCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(ConceptNode entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(ConceptNode entity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoRelationships : IConceptRelationshipRepository
    {
        public Task<ConceptRelationship?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken) => Task.FromResult<ConceptRelationship?>(null);
        public Task<IReadOnlyList<ConceptRelationship>> ListAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ConceptRelationship>>(Array.Empty<ConceptRelationship>());
        public Task<IReadOnlyList<ConceptRelationship>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InsertAsync(ConceptRelationship entity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(ConceptRelationship entity, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
