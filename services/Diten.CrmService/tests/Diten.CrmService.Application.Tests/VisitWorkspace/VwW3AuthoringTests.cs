using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using SecurityClaim = System.Security.Claims.Claim;
using Claim = Diten.CrmService.Domain.Entities.Claim;
using System.Text.Json;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;
using Diten.CrmService.Application.Features.Knowledge.Path;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Tests.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitWorkspace;

/// <summary>
/// WP-VW-W3 · W3-BE-c (Acceptance) on the PRODUCTION handlers / applier / reconciler with MOD-0023 faked at its seams:
/// the objection answer (half-written draft, submit completeness, in-review lock, decide approve / return with comment,
/// the CRM-side SoD in the outcome applier, the claims re-check, withdraw by the submitter only, the retired direct
/// approve, usages, search + paging, copy, the step link rule, the event route, reconcile-on-read); the evidence policy
/// (read OR manage, a history row per changed field, the effective rules with their sources); the time-boxed exceptions
/// (dates, overlap, the approval flow + SoD, cancel / end early, the derived phase).
/// </summary>
public sealed class VwW3AuthoringTests
{
    private static readonly Guid Tenant = Guid.Parse("97c50000-0000-0000-0000-0000000000c3");
    private static readonly Guid ProductA = Guid.Parse("a0000000-0000-0000-0000-0000000000aa");
    private static readonly Guid ProductB = Guid.Parse("b0000000-0000-0000-0000-0000000000bb");
    private const string Author = "author-1", Editor = "editor-1", Submitter = "submitter-1", Reviewer = "reviewer-1";
    private const string Rep = "rep-1", OtherRep = "rep-2";

    // ═══ 1 · the objection answer ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_half_written_draft_is_saved_and_submit_checks_completeness_claims_and_product()
    {
        var w = new World();
        var created = await w.Create(ProductA, category: null, question: null, answer: null);
        Assert.Equal(201, created.StatusCode); // K-A3: an empty draft is saved
        var id = created.Data;
        Assert.Equal((string.Empty, string.Empty), (w.Objections.Items.Single().Question, w.Objections.Items.Single().Answer));

        // incomplete → 400, nothing started
        var incomplete = await w.Approvals(Author).Handle(new SubmitObjectionResponseCommand(id), default);
        Assert.Equal((400, ObjectionResponseErrors.Incomplete), (incomplete.StatusCode, incomplete.Errors![0]));
        Assert.Empty(w.Workflow.Starts);

        // a claim of ANOTHER product → 400 objection_claim_product_mismatch
        var otherProductClaim = w.Claim(ProductB, approved: true);
        Assert.True((await w.Update(id, ProductA, "price", "Pahalı mı?", "Hayır …", otherProductClaim.Id)).IsSuccessful);
        var mismatch = await w.Approvals(Author).Handle(new SubmitObjectionResponseCommand(id), default);
        Assert.Equal((400, ObjectionResponseRules.ClaimProductMismatch), (mismatch.StatusCode, mismatch.Errors![0]));

        // a claim un-approved since the draft → 400 objection_claim_not_approved
        var claim = w.Claim(ProductA, approved: true);
        Assert.True((await w.Update(id, ProductA, "price", "Pahalı mı?\nikinci satır", "Hayır …", claim.Id)).IsSuccessful);
        claim.Status = ClaimStatuses.Draft;
        var unapproved = await w.Approvals(Author).Handle(new SubmitObjectionResponseCommand(id), default);
        Assert.Equal((400, ObjectionResponseErrors.ClaimNotApproved), (unapproved.StatusCode, unapproved.Errors![0]));
        claim.Status = ClaimStatuses.Approved;

        // complete → in_review, ONE round on CRM-OBJ-RESP with the Work Center display context
        var submitted = await w.Approvals(Submitter).Handle(new SubmitObjectionResponseCommand(id), default);
        Assert.True(submitted.IsSuccessful, string.Join(" / ", submitted.Errors ?? Array.Empty<string>()));
        Assert.Equal(ObjectionResponseStatuses.InReview, submitted.Data!.Status);
        Assert.Equal(Submitter, submitted.Data.SubmittedBy);
        var start = Assert.Single(w.Workflow.Starts);
        Assert.Equal(("CRM-OBJ-RESP", "crm.objection-response", $"crm.objection-response:{id:D}:r1"),
            (start.TemplateCode, start.ObjectType, start.IdempotencyKey));
        Assert.Equal($"/CRM/ObjectionResponses/{id:D}", start.DisplayContext.DeepLinkUrl);
        Assert.Equal("İtiraz yanıtı onayı · Fiyat", start.DisplayContext.Title); // the BRD label, not the code
        Assert.Equal("Pahalı mı?", start.DisplayContext.Subtitle);              // the question's first line
        Assert.Contains("Fiyat", start.DisplayContext.Chips);

        // in_review is locked: update / retire / a second submit → 409
        var locked = await w.Update(id, ProductA, "price", "değişti", "değişti", claim.Id);
        Assert.Equal((409, ObjectionResponseErrors.InReviewLocked), (locked.StatusCode, locked.Errors![0]));
        Assert.Equal(409, (await new RetireObjectionResponseHandler(w, new Actor(Author), w.Objections).Handle(new RetireObjectionResponseCommand(id), default)).StatusCode);
        Assert.Equal(409, (await w.Approvals(Submitter).Handle(new SubmitObjectionResponseCommand(id), default)).StatusCode);

        // MOD-0023 down → 503, nothing changes
        var w2 = new World();
        var other = (await w2.Create(ProductA, "price", "Q", "A")).Data;
        w2.Workflow.StartOutcome = ClaimWorkflowCallOutcome.Unavailable;
        Assert.Equal(503, (await w2.Approvals(Author).Handle(new SubmitObjectionResponseCommand(other), default)).StatusCode);
        Assert.Equal(ObjectionResponseStatuses.Draft, w2.Objections.Items.Single().Status);
        w2.Workflow.StartOutcome = ClaimWorkflowCallOutcome.TemplateMissing;
        var missing = await w2.Approvals(Author).Handle(new SubmitObjectionResponseCommand(other), default);
        Assert.Equal((409, WorkflowApprovalErrors.TemplateMissing), (missing.StatusCode, missing.Errors![0]));
    }

    [Fact]
    public async Task The_decision_needs_a_comment_to_return_and_the_author_editor_or_submitter_never_decides()
    {
        var w = new World();
        var id = await w.SubmittedAsync();
        var round = w.Objections.Items.Single().ReviewRounds.Single();
        w.Decisions.Mine.Add(OpenTask(round.WorkflowInstanceId));

        var noComment = await w.Approvals(Reviewer).Handle(new DecideObjectionResponseCommand(id, "return", "  "), default);
        Assert.Equal((400, RegulatoryTextErrors.RejectionCommentRequired), (noComment.StatusCode, noComment.Errors![0]));
        Assert.Equal(400, (await w.Approvals(Reviewer).Handle(new DecideObjectionResponseCommand(id, "maybe", null), default)).StatusCode);

        foreach (var own in new[] { Author, Editor, Submitter.ToUpperInvariant() })
        {
            var refused = await w.Approvals(own).Handle(new DecideObjectionResponseCommand(id, "approve", null), default);
            Assert.Equal((403, ObjectionResponseErrors.SeparationOfDuties), (refused.StatusCode, refused.Errors![0]));
        }

        Assert.Empty(w.Decisions.Calls);

        var returned = await w.Approvals(Reviewer).Handle(new DecideObjectionResponseCommand(id, "return", "Kaynak eksik"), default);
        Assert.True(returned.IsSuccessful);
        var call = Assert.Single(w.Decisions.Calls);
        Assert.Equal((false, Reviewer, "Kaynak eksik"), (call.Approve, call.Actor, call.Comment));
        Assert.Equal(("return", "Kaynak eksik"), (w.Objections.Items.Single().Decisions.Single().Outcome, w.Objections.Items.Single().Decisions.Single().Comment));
        // the state change arrives as the outcome (the event): rejected → draft, the decision kept
        await w.Applier.ApplyAsync(Tenant, "crm.objection-response", id, round.WorkflowInstanceId, ClaimReviewOutcomes.Rejected,
            Reviewer, "X", DateTimeOffset.UtcNow, default);
        Assert.Equal(ObjectionResponseStatuses.Draft, w.Objections.Items.Single().Status);
        Assert.Equal("Kaynak eksik", w.Objections.Items.Single().Decisions.Single().Comment);
    }

    [Theory]
    [InlineData(Author)]
    [InlineData(Editor)]
    [InlineData(Submitter)]
    public async Task The_outcome_applier_refuses_an_approval_by_the_author_the_last_editor_or_the_submitter(string approver)
    {
        var w = new World();
        var id = await w.SubmittedAsync();
        var round = w.Objections.Items.Single().ReviewRounds.Single();

        var result = await w.Applier.ApplyAsync(Tenant, "crm.objection-response", id, round.WorkflowInstanceId,
            ClaimReviewOutcomes.Approved, approver.ToUpperInvariant(), null, DateTimeOffset.UtcNow, default);

        Assert.Equal(ClaimReviewApplyResult.Applied, result);
        var record = w.Objections.Items.Single();
        Assert.Equal(ObjectionResponseStatuses.Draft, record.Status);                    // never approved
        Assert.Equal(ObjectionResponseErrors.SeparationOfDuties, record.ReviewRounds.Single().ReasonCode);
        Assert.Null(record.ApprovedBy);
    }

    [Fact]
    public async Task An_approval_by_someone_else_approves_unless_a_claim_was_withdrawn_meanwhile()
    {
        var w = new World();
        var id = await w.SubmittedAsync();
        var round = w.Objections.Items.Single().ReviewRounds.Single();
        Assert.Equal(ClaimReviewApplyResult.Applied, await w.Applier.ApplyAsync(Tenant, "crm.objection-response", id,
            round.WorkflowInstanceId, ClaimReviewOutcomes.Approved, Reviewer, null, DateTimeOffset.UtcNow, default));
        Assert.Equal((ObjectionResponseStatuses.Approved, Reviewer), (w.Objections.Items.Single().Status, w.Objections.Items.Single().ApprovedBy));
        Assert.Equal("approve", w.Objections.Items.Single().Decisions.Single().Outcome);

        // claims re-checked at approval
        var w2 = new World();
        var id2 = await w2.SubmittedAsync();
        w2.Claims.Items.Single().Status = ClaimStatuses.Draft;
        var round2 = w2.Objections.Items.Single().ReviewRounds.Single();
        await w2.Applier.ApplyAsync(Tenant, "crm.objection-response", id2, round2.WorkflowInstanceId, ClaimReviewOutcomes.Approved,
            Reviewer, null, DateTimeOffset.UtcNow, default);
        Assert.Equal((ObjectionResponseStatuses.Draft, ObjectionResponseErrors.ClaimNotApproved),
            (w2.Objections.Items.Single().Status, w2.Objections.Items.Single().ReviewRounds.Single().ReasonCode));

        // a forged instance never lands; a timed-out round → draft
        Assert.Equal(ClaimReviewApplyResult.NoMatchingOpenRound, await w2.Applier.ApplyAsync(Tenant, "crm.objection-response", id2,
            Guid.NewGuid(), ClaimReviewOutcomes.Approved, Reviewer, null, DateTimeOffset.UtcNow, default));
    }

    [Fact]
    public async Task Only_the_submitter_withdraws_and_the_old_direct_approve_is_409()
    {
        var w = new World();
        var id = await w.SubmittedAsync();
        var other = await w.Approvals(Reviewer).Handle(new WithdrawObjectionResponseCommand(id), default);
        Assert.Equal((403, ClaimErrorCodes.ApprovalForbidden), (other.StatusCode, other.Errors![0]));
        Assert.Empty(w.Workflow.Cancels);

        var mine = await w.Approvals(Submitter).Handle(new WithdrawObjectionResponseCommand(id), default);
        Assert.True(mine.IsSuccessful);
        Assert.Single(w.Workflow.Cancels);
        Assert.Equal(ObjectionResponseStatuses.Draft, mine.Data!.Status);
        Assert.Equal(ClaimReviewOutcomes.Cancelled, w.Objections.Items.Single().ReviewRounds.Single().Outcome);

        var direct = await new ApproveObjectionResponseHandler().Handle(new ApproveObjectionResponseCommand(id), default);
        Assert.Equal((409, ObjectionResponseErrors.ApprovalViaWorkflowOnly), (direct.StatusCode, direct.Errors![0]));
    }

    [Fact]
    public async Task The_library_reads_usages_search_pages_copies_and_names_its_product()
    {
        var w = new World();
        var used = (await w.Create(ProductA, "price", "Fiyat yüksek değil mi?", "Yanıt")).Data;
        await w.Create(ProductA, "price", "Yan etkiler?", "Yanıt");
        await w.Create(ProductB, "price", "Fiyat ve geri ödeme?", "Yanıt");
        var path = new KnowledgePath { TenantId = Tenant, PathName = "Tutukon yolu", PathVersion = "1.0", PathStatus = KnowledgePathStatuses.Published };
        path.Steps.Add(new KnowledgePathStep { StepOrder = 10, StepTitle = "Açılış", Presentation = new KnowledgePathStepPresentation { ObjectionResponseIds = { used } } });
        path.Steps.Add(new KnowledgePathStep { StepOrder = 20, StepTitle = "Kanıt", Presentation = new KnowledgePathStepPresentation { ObjectionResponseIds = { used } } });
        path.Steps.Add(new KnowledgePathStep { StepOrder = 30, StepTitle = "Eski", ArchivedAt = DateTimeOffset.UtcNow, Presentation = new KnowledgePathStepPresentation { ObjectionResponseIds = { used } } });
        w.Paths.Items.Add(path);

        var usages = (await w.Queries().Handle(new GetObjectionResponseUsagesQuery(used), default)).Data!;
        Assert.Equal(new[] { ("Açılış", 10), ("Kanıt", 20) }, usages.Select(u => (u.StepTitle, u.StepOrder))); // an archived step is not a use
        Assert.All(usages, u => Assert.Equal(("Tutukon yolu", "1.0", KnowledgePathStatuses.Published), (u.PathName, u.PathVersion, u.PathStatus)));

        var search = (await w.Queries().Handle(new ListObjectionResponsesQuery(Q: "fiyat"), default)).Data!;
        Assert.Equal(2, search.Total);
        var first = search.Items.Single(i => i.Id == used);
        Assert.Equal((2, "Tutukon"), (first.UsageCount, first.ProductName));
        Assert.Null(first.ProductForm); // the MDM selector carries no form ⇒ null, still 200
        Assert.True(first.Abilities!.CanEdit && first.Abilities.CanSubmit && first.Abilities.CanCopy && !first.Abilities.CanDecide);

        var page2 = (await w.Queries().Handle(new ListObjectionResponsesQuery(Page: 2, PageSize: 2), default)).Data!;
        Assert.Equal((3, 2, 2), (page2.Total, page2.Page, page2.PageSize));
        Assert.Single(page2.Items);
        Assert.Single((await w.Queries().Handle(new ListObjectionResponsesQuery(ProductId: ProductB), default)).Data!.Items);

        var copy = await w.Approvals(Author).Handle(new CopyObjectionResponseCommand(used), default);
        Assert.Equal(201, copy.StatusCode);
        var copied = w.Objections.Items.Single(o => o.Id == copy.Data);
        Assert.Equal((used, ObjectionResponseStatuses.Draft, "Fiyat yüksek değil mi?"), (copied.CopiedFromId!.Value, copied.Status, copied.Question));
    }

    [Fact]
    public async Task A_step_links_only_an_approved_answer_of_the_paths_product()
    {
        var w = new World();
        var approvedA = w.Answer(ProductA, ObjectionResponseStatuses.Approved);
        var draftA = w.Answer(ProductA, ObjectionResponseStatuses.Draft);
        var approvedB = w.Answer(ProductB, ObjectionResponseStatuses.Approved);
        var path = new KnowledgePath { TenantId = Tenant };
        var ofA = new FixedPathProduct(ProductA);

        Assert.Null(await Link(approvedA.Id, ofA));
        Assert.Equal(KnowledgePathPresentationLimits.ObjectionInvalid, (await Link(draftA.Id, ofA))![0]);
        Assert.Equal(KnowledgePathPresentationLimits.ObjectionInvalid, (await Link(approvedB.Id, ofA))![0]);
        Assert.Equal(KnowledgePathPresentationLimits.ObjectionInvalid, (await Link(approvedA.Id, new FixedPathProduct(null)))![0]); // no product
        Assert.Equal(KnowledgePathPresentationLimits.ObjectionNotFound, (await Link(Guid.NewGuid(), ofA))![0]);
        Assert.Null(await KnowledgePathPresentation.ValidateAsync(new KnowledgePathStepPresentationInput("mesaj"), Tenant, w.Objections, default, path, ofA));

        Task<string[]?> Link(Guid id, IKnowledgePathProductResolver products)
            => KnowledgePathPresentation.ValidateAsync(new KnowledgePathStepPresentationInput(ObjectionResponseIds: new[] { id }),
                Tenant, w.Objections, default, path, products);
    }

    [Fact]
    public async Task The_completion_event_routes_crm_objection_response_and_an_old_round_is_reconciled_with_its_approver()
    {
        var w = new World();
        var id = await w.SubmittedAsync();
        var round = w.Objections.Items.Single().ReviewRounds.Single();
        var consumer = new ClaimWorkflowOutcomeConsumer(new ClaimReviewOutcomeApplier(w.Claims, new EmptyCountryVersionRepo()),
            new Inbox(), NullLogger<ClaimWorkflowOutcomeConsumer>.Instance, authoringApplier: w.Applier);
        await consumer.ConsumeAsync(Completed("crm.objection-response", id, round.WorkflowInstanceId, ClaimReviewOutcomes.Approved, Reviewer));
        Assert.Equal(ObjectionResponseStatuses.Approved, w.Objections.Items.Single().Status);

        // reconcile-on-read: an open round older than 120 s, MOD-0023 says approved; the approver comes from the task
        var w2 = new World();
        var id2 = await w2.SubmittedAsync();
        var r2 = w2.Objections.Items.Single().ReviewRounds.Single();
        r2.SubmittedAt = DateTimeOffset.UtcNow.AddHours(-1);
        w2.Workflow.Complete(r2.WorkflowInstanceId, ClaimReviewOutcomes.Approved);
        w2.Workflow.Tasks.Clear();
        w2.Workflow.Tasks.Add(new ClaimWorkflowTaskState(Guid.NewGuid(), r2.WorkflowInstanceId, "s", "medical", "Approved", "x", Author, null, null, DateTimeOffset.UtcNow));
        var read = (await w2.Queries().Handle(new GetObjectionResponseQuery(id2), default)).Data!;
        Assert.Equal(ObjectionResponseStatuses.Draft, read.Status);            // the AUTHOR approved in the Work Center → SoD
        Assert.Equal(ObjectionResponseErrors.SeparationOfDuties, read.ReviewRounds!.Single().ReasonCode);

        // an approver the reconcile cannot read → not applied (the event, which names him, applies it)
        var w3 = new World();
        var id3 = await w3.SubmittedAsync();
        var r3 = w3.Objections.Items.Single().ReviewRounds.Single();
        r3.SubmittedAt = DateTimeOffset.UtcNow.AddHours(-1);
        w3.Workflow.Complete(r3.WorkflowInstanceId, ClaimReviewOutcomes.Approved);
        Assert.Equal(ObjectionResponseStatuses.InReview, (await w3.Queries().Handle(new GetObjectionResponseQuery(id3), default)).Data!.Status);
    }

    // ═══ 2 · the evidence policy ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void The_policy_reads_need_read_or_manage()
    {
        static ClaimsPrincipal User(params string[] keys) => new(new ClaimsIdentity(keys.Select(k => new SecurityClaim("permission", k)), "test"));
        Assert.True(VisitEvidenceSettingsController.MayReadPolicy(User("crm.visit-evidence-policy.read")));
        Assert.True(VisitEvidenceSettingsController.MayReadPolicy(User("crm.visit-evidence-policy.manage")));
        Assert.False(VisitEvidenceSettingsController.MayReadPolicy(User("crm.visit-execution.read", "crm.visit-evidence-exception.manage")));
    }

    [Fact]
    public async Task A_put_writes_one_history_row_per_changed_field_and_the_get_says_who_and_how_many_reps()
    {
        var policies = new FakeVisitEvidencePolicyRepository();
        var changes = new ChangeRepo();
        var put = new UpdateVisitEvidencePolicyHandler(Ctx(), new Actor("admin-1"), policies, changes);

        // the first document: "created" + one row per field that differs from the defaults
        Assert.True((await put.Handle(new UpdateVisitEvidencePolicyCommand(new VisitEvidencePolicyInput(
            "info", 400, null, null, null, null, null, null, null)), default)).IsSuccessful);
        Assert.Equal(new[] { ("created", (string?)null, (string?)null, (string?)null), ("set", "locationMode", "required", "info"), ("set", "radiusMeters", "300", "400") },
            changes.Rows.Select(r => (r.Kind, r.Field, r.OldValue, r.NewValue)));
        Assert.All(changes.Rows, r => Assert.Equal(("tenant", "admin-1"), (r.Scope, r.By)));

        // no change → no row; one change → one row
        changes.Rows.Clear();
        await put.Handle(new UpdateVisitEvidencePolicyCommand(new VisitEvidencePolicyInput("info", 400, null, null, null, null, null, null, null)), default);
        Assert.Empty(changes.Rows);
        await put.Handle(new UpdateVisitEvidencePolicyCommand(new VisitEvidencePolicyInput(null, null, null, "required", null, null, null, null, null)), default);
        Assert.Equal(("photoMode", "optional", "required"), (changes.Rows.Single().Field, changes.Rows.Single().OldValue, changes.Rows.Single().NewValue));

        var history = (await new VisitEvidencePolicyReadHandlers(Ctx(), changes, new VisitEvidencePolicyResolver(policies), new Roster())
            .Handle(new GetVisitEvidencePolicyHistoryQuery(), default)).Data!;
        Assert.Equal(1, history.Total);

        var get = (await new GetVisitEvidencePolicyHandler(Ctx(), policies, new Roster()).Handle(new GetVisitEvidencePolicyQuery(), default)).Data!;
        Assert.Equal(("admin-1", 2, false), (get.UpdatedBy, get.RepCount, get.IsDefault));
        Assert.NotNull(get.UpdatedAt);
    }

    [Fact]
    public async Task The_effective_rules_show_each_field_with_its_source_tenant_or_exception()
    {
        var policies = new FakeVisitEvidencePolicyRepository();
        var exceptions = new ExceptionRepo();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var active = Exc(Rep, VisitEvidenceExceptionStatuses.Approved, today.AddDays(-1), today.AddDays(1), VisitEvidenceExceptionRules.LocationInfo);
        exceptions.Items.Add(active);
        exceptions.Items.Add(Exc(Rep, VisitEvidenceExceptionStatuses.InReview, today, today, VisitEvidenceExceptionRules.PhotoOptional));
        var handler = new VisitEvidencePolicyReadHandlers(Ctx(), new ChangeRepo(), new VisitEvidencePolicyResolver(policies, exceptions), new Roster());

        var dto = (await handler.Handle(new GetVisitEvidenceEffectiveQuery(Rep), default)).Data!;
        var location = dto.Fields.Single(f => f.Field == "locationMode");
        Assert.Equal(("info", "exception", active.Id), (location.Value, location.Source, location.ExceptionId!.Value));
        var photo = dto.Fields.Single(f => f.Field == "photoMode");
        Assert.Equal(("optional", "tenant"), (photo.Value, photo.Source)); // the in-review one relaxes nothing
        Assert.Equal(8, dto.Fields.Count);
        Assert.Equal(active.Id, dto.ActiveException!.Id);
        Assert.Equal(("active", (Guid?)null, (string?)null, "unresolved"), (dto.ActiveException.Phase, dto.ScopeChain.LegalEntity, dto.ScopeChain.BusinessUnit, dto.ScopeChain.Note));

        var other = (await handler.Handle(new GetVisitEvidenceEffectiveQuery(OtherRep), default)).Data!;
        Assert.All(other.Fields, f => Assert.Equal("tenant", f.Source));
        Assert.Null(other.ActiveException);

        var reps = (await handler.Handle(new ListVisitEvidenceRepsQuery("ayş"), default)).Data!;
        Assert.Equal(Rep, reps.Single().ResourceId);
    }

    // ═══ 3 · the time-boxed exceptions ═════════════════════════════════════════════════════════════════════

    public static TheoryData<int, int?, string?, string> BadDates => new()
    {
        { -1, 0, "ok", "past start" },
        { 0, 30, "ok", "31 days" },
        { 0, null, "ok", "no end" },
        { 3, 2, "ok", "end before start" },
        { 0, 0, null, "no reason" },
        { 0, 0, "   ", "blank reason" }
    };

    [Theory]
    [MemberData(nameof(BadDates))]
    public async Task The_dates_and_the_reason_are_checked_no_past_start_at_most_30_days_both_ends_included(int start, int? end, string? reason, string why)
    {
        var w = new World();
        var today = w.Today;
        var res = await w.Exceptions(Author).Handle(new CreateVisitEvidenceExceptionCommand(new VisitEvidenceExceptionInput(
            Rep, new[] { VisitEvidenceExceptionRules.LocationInfo }, today.AddDays(start), end is { } e ? today.AddDays(e) : null, reason)), default);
        Assert.True(res.StatusCode == 400 && res.Errors![0] == VisitEvidenceExceptionErrors.Invalid, why);
        Assert.Empty(w.ExceptionsRepo.Items);
    }

    [Fact]
    public async Task Thirty_days_are_accepted_rules_are_checked_and_an_unknown_rep_is_refused()
    {
        var w = new World();
        var ok = await w.CreateException(Rep, w.Today, w.Today.AddDays(29), VisitEvidenceExceptionRules.LocationInfo);
        Assert.Equal(201, ok.StatusCode);
        Assert.Equal((30, "Ayşe Temsilci", VisitEvidenceExceptionStatuses.Draft), (ok.Data!.Days, ok.Data.RepDisplayName, ok.Data.Status));

        Assert.Equal(400, (await w.CreateException(Rep, w.Today, w.Today)).StatusCode);                                 // no rule
        Assert.Equal(400, (await w.CreateException(Rep, w.Today, w.Today, "location_off")).StatusCode);                  // unknown rule
        var unknown = await w.CreateException("nobody", w.Today, w.Today, VisitEvidenceExceptionRules.PhotoOptional);
        Assert.Equal((400, VisitEvidenceExceptionErrors.RepUnknown), (unknown.StatusCode, unknown.Errors![0]));
    }

    [Fact]
    public async Task An_in_review_or_approved_exception_of_the_same_rep_and_rule_on_overlapping_days_is_409()
    {
        var w = new World();
        w.ExceptionsRepo.Items.Add(Exc(Rep, VisitEvidenceExceptionStatuses.Approved, w.Today.AddDays(2), w.Today.AddDays(5), VisitEvidenceExceptionRules.LocationInfo));
        w.ExceptionsRepo.Items.Add(Exc(Rep, VisitEvidenceExceptionStatuses.Cancelled, w.Today, w.Today.AddDays(20), VisitEvidenceExceptionRules.PhotoOptional));

        var overlap = await w.CreateException(Rep, w.Today.AddDays(5), w.Today.AddDays(8), VisitEvidenceExceptionRules.LocationInfo, VisitEvidenceExceptionRules.PhotoOptional);
        Assert.Equal((409, VisitEvidenceExceptionErrors.Overlap), (overlap.StatusCode, overlap.Errors![0]));
        Assert.Equal(201, (await w.CreateException(Rep, w.Today.AddDays(6), w.Today.AddDays(8), VisitEvidenceExceptionRules.LocationInfo)).StatusCode); // next day
        Assert.Equal(201, (await w.CreateException(Rep, w.Today.AddDays(2), w.Today.AddDays(5), VisitEvidenceExceptionRules.PhotoOptional)).StatusCode); // other rule (the cancelled one does not hold)
        Assert.Equal(201, (await w.CreateException(OtherRep, w.Today.AddDays(2), w.Today.AddDays(5), VisitEvidenceExceptionRules.LocationInfo)).StatusCode); // other rep
    }

    [Fact]
    public async Task An_exception_is_submitted_to_its_template_and_its_creator_or_submitter_never_approves_it()
    {
        var w = new World();
        var id = (await w.CreateException(Rep, w.Today, w.Today.AddDays(3), VisitEvidenceExceptionRules.LocationInfo)).Data!.Id;
        var submitted = await w.Exceptions(Submitter).Handle(new SubmitVisitEvidenceExceptionCommand(id), default);
        Assert.Equal(VisitEvidenceExceptionStatuses.InReview, submitted.Data!.Status);
        var start = Assert.Single(w.Workflow.Starts);
        Assert.Equal(("CRM-VISIT-EVIDENCE-EXC", "crm.visit-evidence-exception"), (start.TemplateCode, start.ObjectType));
        Assert.Equal($"/CRM/VisitEvidenceSettings?tab=exceptions&id={id:D}", start.DisplayContext.DeepLinkUrl);
        Assert.Equal(409, (await w.Exceptions(Author).Handle(new UpdateVisitEvidenceExceptionCommand(id, new VisitEvidenceExceptionInput(
            Rep, new[] { VisitEvidenceExceptionRules.LocationInfo }, w.Today, w.Today, "x")), default)).StatusCode); // locked

        var round = w.ExceptionsRepo.Items.Single().ReviewRounds.Single();
        foreach (var own in new[] { Author, Submitter })
        {
            var record = w.ExceptionsRepo.Items.Single();
            record.Status = VisitEvidenceExceptionStatuses.InReview;
            round.Outcome = null;
            round.ClosedAt = null;
            await w.Applier.ApplyAsync(Tenant, "crm.visit-evidence-exception", id, round.WorkflowInstanceId, ClaimReviewOutcomes.Approved,
                own, null, DateTimeOffset.UtcNow, default);
            Assert.Equal((VisitEvidenceExceptionStatuses.Draft, VisitEvidenceExceptionErrors.SeparationOfDuties), (record.Status, round.ReasonCode));
        }

        w.ExceptionsRepo.Items.Single().Status = VisitEvidenceExceptionStatuses.InReview;
        round.Outcome = null;
        round.ClosedAt = null;
        await w.Applier.ApplyAsync(Tenant, "crm.visit-evidence-exception", id, round.WorkflowInstanceId, ClaimReviewOutcomes.Approved,
            "sales-ops-1", null, DateTimeOffset.UtcNow, default);
        Assert.Equal((VisitEvidenceExceptionStatuses.Approved, "sales-ops-1"), (w.ExceptionsRepo.Items.Single().Status, w.ExceptionsRepo.Items.Single().ApprovedBy));
        Assert.Equal(VisitEvidenceExceptionPhases.Active, (await w.ExceptionQueries().Handle(new GetVisitEvidenceExceptionQuery(id), default)).Data!.Phase);
    }

    [Fact]
    public async Task A_planned_one_is_cancelled_an_active_one_ends_early_today_and_the_list_counts_every_status_and_phase()
    {
        var w = new World();
        var planned = Exc(Rep, VisitEvidenceExceptionStatuses.Approved, w.Today.AddDays(3), w.Today.AddDays(5), VisitEvidenceExceptionRules.LocationInfo);
        var active = Exc(OtherRep, VisitEvidenceExceptionStatuses.Approved, w.Today.AddDays(-2), w.Today.AddDays(5), VisitEvidenceExceptionRules.PhotoOptional);
        var expired = Exc(Rep, VisitEvidenceExceptionStatuses.Approved, w.Today.AddDays(-9), w.Today.AddDays(-5), VisitEvidenceExceptionRules.PhotoOptional);
        w.ExceptionsRepo.Items.AddRange(new[] { planned, active, expired });

        var list = (await w.ExceptionQueries().Handle(new ListVisitEvidenceExceptionsQuery(), default)).Data!;
        Assert.Equal((1, 1, 1, 3), (list.PhaseCounts["planned"], list.PhaseCounts["active"], list.PhaseCounts["expired"], list.StatusCounts["approved"]));
        Assert.Single((await w.ExceptionQueries().Handle(new ListVisitEvidenceExceptionsQuery(Phase: "active"), default)).Data!.Items);

        Assert.Equal(400, (await w.Exceptions(Author).Handle(new CancelVisitEvidenceExceptionCommand(planned.Id, " "), default)).StatusCode); // reason required
        Assert.Equal(409, (await w.Exceptions(Author).Handle(new CancelVisitEvidenceExceptionCommand(active.Id, "x"), default)).StatusCode);   // not planned
        Assert.Equal(409, (await w.Exceptions(Author).Handle(new EndVisitEvidenceExceptionEarlyCommand(planned.Id, "x"), default)).StatusCode); // not active

        var cancelled = (await w.Exceptions(Author).Handle(new CancelVisitEvidenceExceptionCommand(planned.Id, "Gerek kalmadı"), default)).Data!;
        Assert.Equal((VisitEvidenceExceptionStatuses.Cancelled, "Gerek kalmadı", Author), (cancelled.Status, cancelled.CancelReason, cancelled.CancelledBy));
        var ended = (await w.Exceptions(Author).Handle(new EndVisitEvidenceExceptionEarlyCommand(active.Id, "Konum düzeldi"), default)).Data!;
        Assert.Equal((VisitEvidenceExceptionStatuses.EndedEarly, w.Today, (string?)null), (ended.Status, ended.EndDate, ended.Phase));
    }

    // ═══ 4 · W3-FIX (F3 machine reason codes, F5 display names) ═══════════════════════════════════════════

    [Fact]
    public async Task F3_a_work_center_machine_code_is_not_stored_as_a_reason_nor_shown_as_a_note_but_a_real_comment_and_a_refusal_stay()
    {
        Assert.True(WorkflowApprovalNotes.IsMachineCode("WORKCENTER_APPROVE"));
        Assert.True(WorkflowApprovalNotes.IsMachineCode(" WORKCENTER_REQUEST_INFO "));
        Assert.False(WorkflowApprovalNotes.IsMachineCode("workcenter_approve"));
        Assert.False(WorkflowApprovalNotes.IsMachineCode("WORKCENTER_ approve"));
        Assert.False(WorkflowApprovalNotes.IsMachineCode(ObjectionResponseErrors.SeparationOfDuties));
        Assert.False(WorkflowApprovalNotes.IsMachineCode("Kaynak belge eksik."));

        // the outcome applier: an approval by someone else, reason WORKCENTER_APPROVE → nothing stored
        var w = new World();
        var id = (await w.CreateException(Rep, w.Today, w.Today.AddDays(2), VisitEvidenceExceptionRules.LocationInfo)).Data!.Id;
        await w.Exceptions(Submitter).Handle(new SubmitVisitEvidenceExceptionCommand(id), default);
        var round = w.ExceptionsRepo.Items.Single().ReviewRounds.Single();
        await w.Applier.ApplyAsync(Tenant, "crm.visit-evidence-exception", id, round.WorkflowInstanceId, ClaimReviewOutcomes.Approved,
            "sales-ops-1", "WORKCENTER_APPROVE", DateTimeOffset.UtcNow, default);
        Assert.Equal(VisitEvidenceExceptionStatuses.Approved, w.ExceptionsRepo.Items.Single().Status);
        Assert.Null(round.ReasonCode);

        // the CRM's own refusal code is still stored (SoD) even when the Work Center sent its machine code
        var o = new World();
        var oid = await o.SubmittedAsync();
        var oround = o.Objections.Items.Single().ReviewRounds.Single();
        await o.Applier.ApplyAsync(Tenant, "crm.objection-response", oid, oround.WorkflowInstanceId, ClaimReviewOutcomes.Approved,
            Author, "WORKCENTER_APPROVE", DateTimeOffset.UtcNow, default);
        Assert.Equal(ObjectionResponseErrors.SeparationOfDuties, oround.ReasonCode);

        // a record written before W3-FIX: the read hides the stored machine code, keeps a real comment
        var record = o.Objections.Items.Single();
        oround.ReasonCode = "WORKCENTER_APPROVE";
        record.Decisions.Clear();
        record.Decisions.Add(new RegulatoryTextDecision { RoundNo = 1, By = Reviewer, Outcome = WorkflowApprovalDecisions.Approve, Comment = "WORKCENTER_APPROVE", At = DateTimeOffset.UtcNow });
        record.Decisions.Add(new RegulatoryTextDecision { RoundNo = 2, By = Reviewer, Outcome = WorkflowApprovalDecisions.Return, Comment = "Kaynak belge eksik.", At = DateTimeOffset.UtcNow });
        var dto = ObjectionResponseDto.From(record);
        Assert.Null(dto.ReviewRounds!.Single().ReasonCode);
        Assert.Equal(new[] { (string?)null, "Kaynak belge eksik." }, dto.Decisions!.Select(d => d.Comment));
    }

    [Fact]
    public async Task F3_a_work_center_return_takes_the_reviewers_comment_from_the_history_never_the_machine_code()
    {
        var w = new World();
        var id = (await w.CreateException(Rep, w.Today, w.Today.AddDays(2), VisitEvidenceExceptionRules.LocationInfo)).Data!.Id;
        await w.Exceptions(Submitter).Handle(new SubmitVisitEvidenceExceptionCommand(id), default);
        var round = w.ExceptionsRepo.Items.Single().ReviewRounds.Single();
        await w.Applier.ApplyAsync(Tenant, "crm.visit-evidence-exception", id, round.WorkflowInstanceId, ClaimReviewOutcomes.Rejected,
            "sales-ops-1", "WORKCENTER_REJECT", DateTimeOffset.UtcNow, default);
        Assert.Null(round.ReasonCode);

        var at = DateTimeOffset.UtcNow;
        w.Decisions.History.Add(new WorkflowHistoryEntry(1, "task_rejected", "sales-ops-1", null, "sales_ops", null, "Tarih aralığı fazla uzun.", "WORKCENTER_REJECT", at));
        w.Decisions.History.Add(new WorkflowHistoryEntry(2, "task_rejected", "sales-ops-1", null, "sales_ops", null, "WORKCENTER_REJECT", "WORKCENTER_REJECT", at.AddSeconds(1)));
        var read = (await w.ExceptionQueries().Handle(new GetVisitEvidenceExceptionQuery(id), default)).Data!;
        Assert.Equal(VisitEvidenceExceptionStatuses.Draft, read.Status);
        Assert.Equal("Tarih aralığı fazla uzun.", read.Decisions.Single().Comment);
        Assert.Null(read.ReviewRounds.Single().ReasonCode);
    }

    [Fact]
    public async Task F5_the_exception_list_detail_history_and_policy_carry_display_names_and_an_unknown_id_stays_out()
    {
        Guid creator = Guid.NewGuid(), approver = Guid.NewGuid(), stranger = Guid.NewGuid(), admin = Guid.NewGuid();
        var names = new PeopleNames(new Dictionary<Guid, string> { [creator] = "Ali Yönetici", [approver] = "Sema Onaylayan", [admin] = "Admin User" });

        var w = new World();
        var e = Exc(Rep, VisitEvidenceExceptionStatuses.Approved, w.Today, w.Today.AddDays(2), VisitEvidenceExceptionRules.LocationInfo);
        e.CreatedBy = creator.ToString("D");
        e.ApprovedBy = approver.ToString("D");
        e.UpdatedBy = stranger.ToString("D");
        w.ExceptionsRepo.Items.Add(e);

        var list = (await w.ExceptionQueries(names).Handle(new ListVisitEvidenceExceptionsQuery(), default)).Data!;
        Assert.Equal("Ali Yönetici", list.DisplayNames![creator.ToString("D")]);
        Assert.Equal("Sema Onaylayan", list.DisplayNames[approver.ToString("D")]);
        Assert.False(list.DisplayNames.ContainsKey(stranger.ToString("D")));    // unresolved → absent, the screen shows a short id
        Assert.Single(names.Calls);                                            // ONE bulk call for the page

        var detail = (await w.ExceptionQueries(names).Handle(new GetVisitEvidenceExceptionQuery(e.Id), default)).Data!;
        Assert.Equal("Sema Onaylayan", detail.DisplayNames![approver.ToString("D")]);
        Assert.Null((await w.ExceptionQueries().Handle(new GetVisitEvidenceExceptionQuery(e.Id), default)).Data!.DisplayNames); // no resolver → null

        var policies = new FakeVisitEvidencePolicyRepository();
        var changes = new ChangeRepo();
        await new UpdateVisitEvidencePolicyHandler(Ctx(), new Actor(admin.ToString("D")), policies, changes)
            .Handle(new UpdateVisitEvidencePolicyCommand(new VisitEvidencePolicyInput("info", null, null, null, null, null, null, null, null)), default);
        var history = (await new VisitEvidencePolicyReadHandlers(Ctx(), changes, new VisitEvidencePolicyResolver(policies), new Roster(), names: names)
            .Handle(new GetVisitEvidencePolicyHistoryQuery(), default)).Data!;
        Assert.Equal("Admin User", history.DisplayNames![admin.ToString("D")]);
        var get = (await new GetVisitEvidencePolicyHandler(Ctx(), policies, new Roster(), names).Handle(new GetVisitEvidencePolicyQuery(), default)).Data!;
        Assert.Equal("Admin User", get.DisplayNames![get.UpdatedBy!]);
    }

    // ═══ helpers ═════════════════════════════════════════════════════════════════════════════════════════

    private static TenantContext Ctx()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(Tenant);
        return ctx;
    }

    private static ClaimWorkflowTaskState OpenTask(Guid instance)
        => new(Guid.NewGuid(), instance, "s", "medical", "WaitingApproval", Reviewer, null, null, null, null);

    private static VisitEvidenceException Exc(string rep, string status, DateOnly start, DateOnly end, params string[] rules) => new()
    {
        TenantId = Tenant, ResourceId = rep, RepDisplayName = rep, Status = status, StartDate = start, EndDate = end,
        Rules = rules.ToList(), Reason = "r", CreatedBy = Author, Version = 1
    };

    private static EventTransportMessage Completed(string objectType, Guid objectId, Guid instanceId, string outcome, string by)
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(), tenantId = Tenant, workflowInstanceId = instanceId, templateCode = "CRM-OBJ-RESP", objectType,
            objectId = objectId.ToString("D"), objectRef = "x", outcome, completedAt = DateTimeOffset.UtcNow, completedBy = by, reasonCode = "X"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new EventTransportMessage(Guid.NewGuid(), ClaimWorkflowOutcomeConsumer.CompletedEventName, 1, Guid.NewGuid(),
            null, Tenant, "Diten.Platform", DateTimeOffset.UtcNow, payload);
    }

    private sealed class World : ITenantContext, IReferenceDataValidator, IReferenceDataCatalogReader
    {
        public Guid? TenantId => Tenant;
        public bool HasTenant => true;
        public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
        public ObjectionRepo Objections { get; } = new();
        public ExceptionRepo ExceptionsRepo { get; } = new();
        public ClaimRepo Claims { get; } = new();
        public PathRepo Paths { get; } = new();
        public FakeClaimWorkflowClient Workflow { get; } = new();
        public DecisionClient Decisions { get; } = new();
        public WorkflowApprovalOutcomeApplier Applier { get; }

        public World() => Applier = new WorkflowApprovalOutcomeApplier(Objections, ExceptionsRepo, Claims);

        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult(new ReferenceValidationResult(
                value is "price" or "side_effect" ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue, setCode, value));

        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult(new ReferenceSetSnapshot(setCode, true, new[]
            {
                new ReferenceValueSnapshot("price", "Price", null, true, false, new Dictionary<string, string> { ["label_tr"] = "Fiyat" })
            }));

        private WorkflowApprovalFlow Flow(string actor) => new(Workflow, Decisions, new Actor(actor), Applier);

        private WorkflowApprovalReconciler Reconciler => new(Workflow, Applier);

        public ObjectionResponseReader Reader(string actor) => new(this, new Actor(actor), Objections, Paths,
            new Names(), Flow(actor), Reconciler);

        public ObjectionResponseApprovalHandlers Approvals(string actor)
            => new(Reader(actor), new ObjectionResponseRules(this, Claims), Flow(actor), Applier, catalog: this);

        public ObjectionResponseQueryHandlers Queries() => new(Reader(Reviewer));

        public Task<Common.Models.Response<Guid>> Create(Guid product, string? category, string? question, string? answer, params Guid[] claims)
            => new CreateObjectionResponseHandler(this, new Actor(Author), Objections, new ObjectionResponseRules(this, Claims))
                .Handle(new CreateObjectionResponseCommand(product, category, question, answer, claims), default);

        public Task<Common.Models.Response<bool>> Update(Guid id, Guid product, string? category, string? question, string? answer, params Guid[] claims)
            => new UpdateObjectionResponseHandler(this, new Actor(Editor), Objections, new ObjectionResponseRules(this, Claims))
                .Handle(new UpdateObjectionResponseCommand(id, product, category, question, answer, claims), default);

        /// <summary>A complete answer (author → edited by editor → submitted by submitter), in review.</summary>
        public async Task<Guid> SubmittedAsync()
        {
            var claim = Claim(ProductA, approved: true);
            var id = (await Create(ProductA, "price", "Q", "A", claim.Id)).Data;
            Assert.True((await Update(id, ProductA, "price", "Pahalı değil mi?", "Uzun vadede …", claim.Id)).IsSuccessful);
            Assert.True((await Approvals(Submitter).Handle(new SubmitObjectionResponseCommand(id), default)).IsSuccessful);
            return id;
        }

        public Claim Claim(Guid product, bool approved)
        {
            var claim = new Claim
            {
                TenantId = Tenant, ClaimCode = "C-" + Guid.NewGuid().ToString("N")[..6], ProductId = product,
                Status = approved ? ClaimStatuses.Approved : ClaimStatuses.Draft
            };
            Claims.Items.Add(claim);
            return claim;
        }

        public ObjectionResponse Answer(Guid product, string status)
        {
            var o = new ObjectionResponse { TenantId = Tenant, ProductId = product, CategoryCode = "price", Question = "Q", Answer = "A", Status = status, Version = 1 };
            Objections.Items.Add(o);
            return o;
        }

        private VisitEvidenceExceptionService Service(string actor, IUserDisplayNameResolver? names = null)
            => new(this, new Actor(actor), ExceptionsRepo, Flow(actor), Reconciler, names: names);

        public VisitEvidenceExceptionCommandHandlers Exceptions(string actor) => new(Service(actor), new Roster(), Applier);

        public VisitEvidenceExceptionQueryHandlers ExceptionQueries(IUserDisplayNameResolver? names = null) => new(Service(Reviewer, names));

        public Task<Common.Models.Response<VisitEvidenceExceptionDto>> CreateException(string rep, DateOnly start, DateOnly end, params string[] rules)
            => Exceptions(Author).Handle(new CreateVisitEvidenceExceptionCommand(new VisitEvidenceExceptionInput(rep, rules, start, end, "Konum servisi arızalı")), default);
    }

    private sealed class Actor(string? name) : IActorContext
    {
        public string? ActorName { get; } = name;
    }

    private sealed class FixedPathProduct(Guid? product) : IKnowledgePathProductResolver
    {
        public Task<Guid?> ProductOfAsync(Guid tenantId, KnowledgePath path, CancellationToken cancellationToken) => System.Threading.Tasks.Task.FromResult(product);
    }

    private sealed class Names : IProductNameReader
    {
        public Task<IReadOnlyDictionary<Guid, string>> ReadNamesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
            => System.Threading.Tasks.Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string> { [ProductA] = "Tutukon" });
    }

    private sealed class Roster : IVisitEvidenceRepRoster
    {
        public Task<IReadOnlyList<EvidenceRepDto>> ListAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<IReadOnlyList<EvidenceRepDto>>(new[] { new EvidenceRepDto(Rep, "Ayşe Temsilci"), new EvidenceRepDto(OtherRep, "Mehmet Temsilci") });
    }

    /// <summary>Resolves the ids it knows; the others are absent (the production contract — never fabricated).</summary>
    private sealed class PeopleNames(Dictionary<Guid, string> known) : IUserDisplayNameResolver
    {
        public List<IReadOnlyCollection<Guid>> Calls { get; } = new();

        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
        {
            Calls.Add(userIds);
            return System.Threading.Tasks.Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                userIds.Where(known.ContainsKey).ToDictionary(id => id, id => known[id]));
        }
    }

    private sealed class Inbox : ICrmEventInboxRepository
    {
        private readonly HashSet<Guid> _seen = new();

        public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
            => System.Threading.Tasks.Task.FromResult(_seen.Add(eventId));
    }

    private sealed class DecisionClient : IWorkflowDecisionClient
    {
        public List<ClaimWorkflowTaskState> Mine { get; } = new();
        public List<(Guid TaskId, bool Approve, string Actor, string? Comment)> Calls { get; } = new();

        public Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetMyTasksAsync(CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<IReadOnlyList<ClaimWorkflowTaskState>?>(Mine.ToList());

        public Task<WorkflowDecisionResult> DecideTaskAsync(Guid taskId, bool approve, string actorId, string reasonCode,
            string idempotencyKey, string? comment, CancellationToken ct)
        {
            Calls.Add((taskId, approve, actorId, comment));
            return System.Threading.Tasks.Task.FromResult(new WorkflowDecisionResult(ClaimWorkflowCallOutcome.Ok, null));
        }

        public List<WorkflowHistoryEntry> History { get; } = new();

        public Task<IReadOnlyList<WorkflowHistoryEntry>?> GetInstanceHistoryAsync(Guid workflowInstanceId, CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<IReadOnlyList<WorkflowHistoryEntry>?>(History.ToList());
    }

    private sealed class ObjectionRepo : IObjectionResponseRepository
    {
        public List<ObjectionResponse> Items { get; } = new();
        public Task<ObjectionResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => System.Threading.Tasks.Task.FromResult(Items.FirstOrDefault(o => o.TenantId == tenantId && o.Id == id));
        public Task<IReadOnlyList<ObjectionResponse>> ListAsync(Guid tenantId, CancellationToken ct) => System.Threading.Tasks.Task.FromResult<IReadOnlyList<ObjectionResponse>>(Items.Where(o => o.TenantId == tenantId).ToList());
        public Task<IReadOnlyList<ObjectionResponse>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<IReadOnlyList<ObjectionResponse>>(Items.Where(o => o.TenantId == tenantId && ids.Contains(o.Id)).ToList());
        public Task InsertAsync(ObjectionResponse entity, CancellationToken ct) { Items.Add(entity); return System.Threading.Tasks.Task.CompletedTask; }
        public Task<bool> ReplaceAsync(ObjectionResponse entity, int expectedVersion, CancellationToken ct) { entity.Version = expectedVersion + 1; return System.Threading.Tasks.Task.FromResult(true); }
    }

    private sealed class ExceptionRepo : IVisitEvidenceExceptionRepository
    {
        public List<VisitEvidenceException> Items { get; } = new();
        public Task<VisitEvidenceException?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => System.Threading.Tasks.Task.FromResult(Items.FirstOrDefault(o => o.TenantId == tenantId && o.Id == id));
        public Task<IReadOnlyList<VisitEvidenceException>> ListAsync(Guid tenantId, CancellationToken ct) => System.Threading.Tasks.Task.FromResult<IReadOnlyList<VisitEvidenceException>>(Items.Where(o => o.TenantId == tenantId).ToList());
        public Task<IReadOnlyList<VisitEvidenceException>> ListByResourceAsync(Guid tenantId, string resourceId, CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<IReadOnlyList<VisitEvidenceException>>(Items.Where(o => o.TenantId == tenantId && o.ResourceId == resourceId).ToList());
        public Task InsertAsync(VisitEvidenceException entity, CancellationToken ct) { Items.Add(entity); return System.Threading.Tasks.Task.CompletedTask; }
        public Task<bool> ReplaceAsync(VisitEvidenceException entity, int expectedVersion, CancellationToken ct) { entity.Version = expectedVersion + 1; return System.Threading.Tasks.Task.FromResult(true); }
    }

    private sealed class ChangeRepo : IVisitEvidencePolicyChangeRepository
    {
        public List<VisitEvidencePolicyChange> Rows { get; } = new();
        public Task InsertManyAsync(IReadOnlyCollection<VisitEvidencePolicyChange> rows, CancellationToken ct) { Rows.AddRange(rows); return System.Threading.Tasks.Task.CompletedTask; }
        public Task<(IReadOnlyList<VisitEvidencePolicyChange> Items, long Total)> ListAsync(Guid tenantId, int skip, int take, CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<(IReadOnlyList<VisitEvidencePolicyChange>, long)>((Rows.Skip(skip).Take(take).ToList(), Rows.Count));
    }

    private sealed class ClaimRepo : IClaimRepository
    {
        public List<Claim> Items { get; } = new();
        public Task<Claim?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => System.Threading.Tasks.Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));
        public Task<IReadOnlyList<Claim>> ListAsync(Guid tenantId, CancellationToken ct) => System.Threading.Tasks.Task.FromResult<IReadOnlyList<Claim>>(Items.ToList());
        public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid tenantId, string claimCode, CancellationToken ct) => System.Threading.Tasks.Task.FromResult<IReadOnlyList<Claim>>(Items.Where(c => c.ClaimCode == claimCode).ToList());
        public Task<Claim?> GetActiveByCodeAsync(Guid tenantId, string claimCode, CancellationToken ct) => System.Threading.Tasks.Task.FromResult(Items.FirstOrDefault(c => c.ClaimCode == claimCode));
        public Task InsertAsync(Claim entity, CancellationToken ct) { Items.Add(entity); return System.Threading.Tasks.Task.CompletedTask; }
        public Task UpdateAsync(Claim entity, CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
    }

    private sealed class PathRepo : IKnowledgePathRepository
    {
        public List<KnowledgePath> Items { get; } = new();
        public Task<KnowledgePath?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => System.Threading.Tasks.Task.FromResult(Items.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));
        public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid tenantId, CancellationToken ct) => System.Threading.Tasks.Task.FromResult<IReadOnlyList<KnowledgePath>>(Items.Where(p => p.TenantId == tenantId).ToList());
        public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid tenantId, string pathCode, CancellationToken ct) => System.Threading.Tasks.Task.FromResult<IReadOnlyList<KnowledgePath>>(Items.Where(p => p.PathCode == pathCode).ToList());
        public Task InsertAsync(KnowledgePath entity, CancellationToken ct) { Items.Add(entity); return System.Threading.Tasks.Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePath entity, int expectedVersion, CancellationToken ct) => System.Threading.Tasks.Task.FromResult(true);
    }
}
