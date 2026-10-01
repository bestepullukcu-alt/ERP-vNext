using System.Net;
using System.Text;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Application.Features.Knowledge.Path;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Application.Features.Knowledge.Path.Handlers;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Eventing;
using Diten.CrmService.Infrastructure.Workflow;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-KP-2 — knowledge path revision + MLR approval: the submit gate (every code), the frozen snapshot and numbering,
/// the MOD-0023 start (template, idempotency, DisplayContext), withdraw, the one-channel decision (comment, person SoD,
/// task lookup, Platform 403 passthrough), outcome routing on the shared consumer, apply + note carry, reconcile-on-read,
/// notes, the change summary, the direct-publish gate (legacy kept), the Gateway decision calls (caller's token +
/// comment) and the class map. MOD-0023 is faked at the client seams.
/// </summary>
public sealed class KnowledgePathReviewTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ProductX = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Author = "user-author";
    private const string Reviewer = "user-reviewer";

    // ================================================================ submit gate

    [Fact]
    public async Task Submit_refuses_a_legacy_path_with_chain_template_required()
    {
        var fx = new Fixture();
        var legacy = await fx.LegacyPathAsync();
        Assert.Equal((409, KnowledgePathStudioErrors.ChainTemplateRequired), Code(await fx.Submit(legacy)));
        Assert.Empty(fx.Workflow.Starts);
    }

    [Fact]
    public async Task Submit_refuses_a_path_that_is_not_a_draft_with_invalid_status()
    {
        var fx = new Fixture();
        var id = await fx.ReadyPathAsync();
        fx.Paths.Items.Single(p => p.Id == id).PathStatus = KnowledgePathStatuses.Approved;
        Assert.Equal((409, ClaimErrorCodes.InvalidStatus), Code(await fx.Submit(id)));
    }

    [Fact]
    public async Task Submit_refuses_an_unfilled_chain_with_chain_conformance_failed()
    {
        var fx = new Fixture();
        var id = await fx.ChainedPathAsync();      // no content on the required step
        Assert.Equal((409, KnowledgePathReviewErrors.ChainConformanceFailed), Code(await fx.Submit(id)));
        Assert.Empty(fx.Workflow.Starts);
    }

    [Fact]
    public async Task Submit_refuses_unpublished_or_foreign_language_content()
    {
        var fx = new Fixture();
        var id = await fx.ReadyPathAsync();
        fx.ContentTr.ContentStatus = KnowledgeContentStatuses.Draft;
        Assert.Equal((409, KnowledgePathReviewErrors.ComponentNotPublished), Code(await fx.Submit(id)));

        fx.ContentTr.ContentStatus = KnowledgeContentStatuses.Published;
        fx.ContentTr.LanguageCode = "en";
        Assert.Equal((409, ChainContextErrors.ComponentLanguageMismatch), Code(await fx.Submit(id)));
        Assert.Empty(fx.Revisions.Items);
    }

    [Fact]
    public async Task Submit_refuses_a_claim_without_a_country_version_but_not_an_unapproved_one()
    {
        var fx = new Fixture();
        var id = await fx.ReadyPathAsync();
        await fx.PlaceClaim(id, fx.ClaimDraftTr);   // TR version exists but is a draft — NOT a gate (KP-3 gates)
        Assert.Equal(201, (await fx.Submit(id)).StatusCode);

        var other = await fx.ReadyPathAsync("KP-B");
        await fx.PlaceClaim(other, fx.ClaimNoTr);   // no TR version at all
        Assert.Equal((409, KnowledgePathReviewErrors.ClaimNoCountryVersion), Code(await fx.Submit(other)));
    }

    // ================================================================ submit

    [Fact]
    public async Task Submit_freezes_a_revision_starts_kp_mlr_and_moves_the_path_to_review()
    {
        var fx = new Fixture();
        var id = await fx.ReadyPathAsync();
        await fx.PlaceClaim(id, fx.ClaimOk);

        var r = await fx.Submit(id);
        Assert.Equal(201, r.StatusCode);
        var revision = fx.Revisions.Items.Single();
        Assert.Equal((1, KnowledgePathRevisionStatuses.InReview, Author), (revision.RevisionNumber, revision.Status, revision.CreatedBy));

        var snapshot = revision.Snapshot;
        Assert.Equal((fx.Template.Id, "1.0", "TR", "tr", ProductX),
            (snapshot.ConceptChainTemplateId, snapshot.ChainVersion, snapshot.CountryCode, snapshot.LanguageCode, snapshot.ProductId));
        var step = Assert.Single(snapshot.Steps);
        Assert.Equal((fx.ContentTr.Id, "3.1", "BR1"), (step.ContentId, step.ContentVersion, step.Arrangement!.BranchCode));
        var claim = Assert.Single(snapshot.Claims);
        Assert.Equal(("CLM-OK", "2.0", fx.VersionOk.Id, "1.0"), (claim.ClaimCode, claim.ClaimVersion, claim.CountryVersionId, claim.CountryVersion));
        Assert.All(snapshot.Conformance, c => Assert.Equal(KnowledgePathConformanceStatuses.Ok, c.Status));
        Assert.Empty(snapshot.Pages);
        Assert.Null(revision.ChangeSummary.ComparedToRevision);

        var start = Assert.Single(fx.Workflow.Starts);
        Assert.Equal("KP-MLR-TR", start.TemplateCode);
        Assert.Equal(KnowledgePathReviewRules.ObjectType, start.ObjectType);
        Assert.Equal(revision.Id.ToString("D"), start.ObjectId);
        Assert.Equal($"crm:knowledge-path:{id:D}:r1", start.IdempotencyKey);
        Assert.Equal($"Bilgi yolu onayı · KP-A v1.0 · Rev 1", start.DisplayContext.Title);
        Assert.Equal($"/CRM/KnowledgePaths/{id:D}/Review/{revision.Id:D}", start.DisplayContext.DeepLinkUrl);
        Assert.Equal(["TR", "tr", "v1.0"], start.DisplayContext.Chips);
        Assert.Equal((fx.Workflow.LastInstanceId, "KP-MLR-TR", Author),
            (revision.ReviewRound.WorkflowInstanceId, revision.ReviewRound.TemplateCode, revision.ReviewRound.SubmittedBy));

        var path = fx.Paths.Items.Single(p => p.Id == id);
        Assert.Equal(KnowledgePathStatuses.Review, path.PathStatus);
        // The draft stays editable while the round runs (the revision is the frozen copy).
        Assert.Equal(201, (await fx.AddStep(id, fx.ContentTr2.Id, "S-2")).StatusCode);
    }

    [Fact]
    public async Task A_second_submit_while_a_round_is_open_is_review_round_open()
    {
        var fx = new Fixture();
        var id = await fx.ReadyPathAsync();
        Assert.Equal(201, (await fx.Submit(id)).StatusCode);
        fx.Paths.Items.Single(p => p.Id == id).PathStatus = KnowledgePathStatuses.Draft;  // even if the status was reset
        Assert.Equal((409, KnowledgePathReviewErrors.ReviewRoundOpen), Code(await fx.Submit(id)));
        Assert.Single(fx.Workflow.Starts);
    }

    [Theory]
    [InlineData(ClaimWorkflowCallOutcome.TemplateMissing, 409, "approval_template_missing")]
    [InlineData(ClaimWorkflowCallOutcome.Unavailable, 503, "workflow_unavailable")]
    public async Task A_failed_start_writes_nothing(ClaimWorkflowCallOutcome outcome, int status, string code)
    {
        var fx = new Fixture();
        var id = await fx.ReadyPathAsync();
        fx.Workflow.StartOutcome = outcome;
        Assert.Equal((status, code), Code(await fx.Submit(id)));
        Assert.Empty(fx.Revisions.Items);
        Assert.Equal(KnowledgePathStatuses.Draft, fx.Paths.Items.Single(p => p.Id == id).PathStatus);
    }

    [Fact]
    public async Task Resubmitting_after_a_rejection_numbers_carries_open_notes_and_summarises_changes()
    {
        var fx = new Fixture();
        var id = await fx.ReadyPathAsync();
        await fx.PlaceClaim(id, fx.ClaimOk);
        await fx.Submit(id);
        var first = fx.Revisions.Items.Single();
        var keep = (await fx.AddNote(id, first.Id, "Ayrıntı satırı KÜB 4.2 ile tutarlı mı?", Reviewer)).Data!;
        var done = (await fx.AddNote(id, first.Id, "Başlık uygun.", Reviewer)).Data!;
        Assert.True((await fx.ResolveNote(id, first.Id, done.NoteId, Reviewer)).IsSuccessful);
        await fx.Applier().ApplyAsync(TenantA, first.Id, first.ReviewRound.WorkflowInstanceId, ClaimReviewOutcomes.Rejected,
            "approver-7", "REJECTED", DateTimeOffset.UtcNow, default);
        Assert.Equal(KnowledgePathStatuses.Draft, fx.Paths.Items.Single(p => p.Id == id).PathStatus);

        // Changes: a second content, the claim removed, the content's version moved on.
        await fx.AddStep(id, fx.ContentTr2.Id, "S-2");
        await fx.RemoveClaim(id, fx.ClaimOk.Id);
        fx.ContentTr.ContentVersion = "3.2";

        Assert.Equal(201, (await fx.Submit(id)).StatusCode);
        var second = fx.Revisions.Items.Single(r => r.RevisionNumber == 2);
        Assert.Equal($"crm:knowledge-path:{id:D}:r2", fx.Workflow.Starts[1].IdempotencyKey);
        var carried = Assert.Single(second.Notes);
        Assert.Equal((keep.Text, 1, false), (carried.Text, carried.CarriedFromRevision, carried.IsResolved()));

        var kinds = second.ChangeSummary.Items.Select(i => i.Kind).OrderBy(k => k).ToList();
        Assert.Equal(1, second.ChangeSummary.ComparedToRevision);
        Assert.Equal([KnowledgePathChangeKinds.ClaimRemoved, KnowledgePathChangeKinds.ContentVersionChanged, KnowledgePathChangeKinds.StepAdded], kinds);
        Assert.Contains(second.ChangeSummary.Items, i => i.Kind == KnowledgePathChangeKinds.ContentVersionChanged && i.From == "3.1" && i.To == "3.2");
    }

    // ================================================================ withdraw

    [Fact]
    public async Task Withdraw_cancels_the_waiting_task_and_returns_the_path_to_draft()
    {
        var fx = new Fixture();
        var id = await fx.ReadyPathAsync();
        await fx.Submit(id);
        var r = await fx.Withdraw(id);
        Assert.True(r.IsSuccessful);
        var cancel = Assert.Single(fx.Workflow.Cancels);
        Assert.Equal(KnowledgePathReviewRules.WithdrawReasonCode, cancel.Reason);
        Assert.EndsWith(":r1:withdraw", cancel.Key);
        Assert.Equal(KnowledgePathRevisionStatuses.Withdrawn, fx.Revisions.Items.Single().Status);
        Assert.Equal(KnowledgePathStatuses.Draft, fx.Paths.Items.Single(p => p.Id == id).PathStatus);

        Assert.Equal((409, ClaimErrorCodes.NoOpenReview), Code(await fx.Withdraw(id)));
    }

    // ================================================================ decision (one channel)

    [Fact]
    public async Task A_reviewer_approves_on_their_own_task_with_the_comment()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        var task = fx.Workflow.Tasks.Single();
        fx.Decisions.Mine.Add(task);

        fx.Actor.Name = Reviewer;
        var r = await fx.Decide(id, revision.Id, "approve", "Mekanizma cümlesi uygun.");
        Assert.True(r.IsSuccessful);
        var call = Assert.Single(fx.Decisions.Calls);
        Assert.Equal((task.TaskId, true, Reviewer, "Mekanizma cümlesi uygun.", KnowledgePathReviewRules.ApproveReasonCode),
            (call.TaskId, call.Approve, call.Actor, call.Comment, call.Reason));
        Assert.Equal($"crm:knowledge-path:{id:D}:r1:{task.TaskId:D}:approve", call.Key);
    }

    [Fact]
    public async Task A_rejection_needs_a_comment()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        fx.Decisions.Mine.Add(fx.Workflow.Tasks.Single());
        fx.Actor.Name = Reviewer;

        Assert.Equal((400, KnowledgePathReviewErrors.CommentRequired), Code(await fx.Decide(id, revision.Id, "reject", "  ")));
        Assert.Empty(fx.Decisions.Calls);
        Assert.True((await fx.Decide(id, revision.Id, "reject", "Güvenlilik sayfası eksik.")).IsSuccessful);
        Assert.False(Assert.Single(fx.Decisions.Calls).Approve);
        Assert.Equal((400, KnowledgePathReviewErrors.DecisionInvalid), Code(await fx.Decide(id, revision.Id, "maybe", null)));
    }

    [Fact]
    public async Task The_submitter_can_never_decide_person_based_sod()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        fx.Decisions.Mine.Add(fx.Workflow.Tasks.Single()); // the submitter even holds the reviewer position

        fx.Actor.Name = Author.ToUpperInvariant();
        Assert.Equal((403, KnowledgePathReviewErrors.SodSubmitterCannotDecide), Code(await fx.Decide(id, revision.Id, "approve", null)));
        Assert.Empty(fx.Decisions.Calls);
    }

    [Fact]
    public async Task Not_a_candidate_gets_platforms_403_and_no_waiting_step_is_409()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        fx.Actor.Name = Reviewer;
        fx.Decisions.Outcome = new WorkflowDecisionResult(ClaimWorkflowCallOutcome.Forbidden, "Actor is not a candidate.");

        // Not in tasks/mine: the instance's open task is still called so MOD-0023 answers for itself.
        var forbidden = await fx.Decide(id, revision.Id, "approve", null);
        Assert.Equal((403, ClaimErrorCodes.ApprovalForbidden), Code(forbidden));
        Assert.Equal("Actor is not a candidate.", forbidden.Errors![1]);
        Assert.Equal(fx.Workflow.Tasks.Single().TaskId, Assert.Single(fx.Decisions.Calls).TaskId);

        fx.Workflow.Tasks.Clear();
        Assert.Equal((409, ClaimErrorCodes.NoOpenReview), Code(await fx.Decide(id, revision.Id, "approve", null)));

        fx.Decisions.Unreachable = true;
        Assert.Equal((503, ClaimErrorCodes.WorkflowUnavailable), Code(await fx.Decide(id, revision.Id, "approve", null)));
    }

    // ================================================================ outcome: routing, apply, reconcile

    [Fact]
    public async Task The_shared_consumer_routes_a_path_revision_outcome_to_the_path_applier()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        var consumer = new ClaimWorkflowOutcomeConsumer(
            new ClaimReviewOutcomeApplier(fx.Claims, fx.Versions), new Inbox(),
            NullLogger<ClaimWorkflowOutcomeConsumer>.Instance, fx.Applier());

        await consumer.ConsumeAsync(Completed(KnowledgePathReviewRules.ObjectType, revision.Id,
            revision.ReviewRound.WorkflowInstanceId, ClaimReviewOutcomes.Approved));

        var stored = fx.Revisions.Items.Single();
        Assert.Equal(KnowledgePathRevisionStatuses.Approved, stored.Status);
        Assert.Equal(("approved", "approver-7"), (stored.ReviewRound.Outcome, stored.ReviewRound.CompletedBy));
        Assert.Equal(KnowledgePathStatuses.Approved, fx.Paths.Items.Single(p => p.Id == id).PathStatus);

        // A claim event on the same consumer still reaches the claim applier (its own round), never the path.
        var claim = fx.ClaimOk;
        var claimInstance = Guid.NewGuid();
        claim.Status = ClaimStatuses.InReview;
        claim.ReviewRounds.Add(new ClaimReviewRound { WorkflowInstanceId = claimInstance, RoundNo = 1, SubmittedAt = DateTimeOffset.UtcNow });
        await consumer.ConsumeAsync(Completed(ClaimReviewRules.ClaimObjectType, claim.Id, claimInstance, ClaimReviewOutcomes.Approved));
        Assert.Equal(ClaimStatuses.Approved, claim.Status);
    }

    [Fact]
    public async Task A_rejection_returns_the_path_to_draft_and_a_replay_or_foreign_instance_is_ignored()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        var instance = revision.ReviewRound.WorkflowInstanceId;

        Assert.Equal(ClaimReviewApplyResult.NoMatchingOpenRound, await fx.Applier().ApplyAsync(TenantA, revision.Id,
            Guid.NewGuid(), ClaimReviewOutcomes.Approved, "x", null, DateTimeOffset.UtcNow, default));
        Assert.Equal(ClaimReviewApplyResult.Applied, await fx.Applier().ApplyAsync(TenantA, revision.Id, instance,
            ClaimReviewOutcomes.Rejected, "approver-7", "REJECTED", DateTimeOffset.UtcNow, default));
        Assert.Equal(KnowledgePathRevisionStatuses.Rejected, fx.Revisions.Items.Single().Status);
        Assert.Equal(KnowledgePathStatuses.Draft, fx.Paths.Items.Single(p => p.Id == id).PathStatus);
        Assert.Equal(ClaimReviewApplyResult.NoMatchingOpenRound, await fx.Applier().ApplyAsync(TenantA, revision.Id, instance,
            ClaimReviewOutcomes.Approved, "approver-7", null, DateTimeOffset.UtcNow, default));
        Assert.Equal(KnowledgePathStatuses.Draft, fx.Paths.Items.Single(p => p.Id == id).PathStatus);
    }

    [Fact]
    public async Task A_stale_open_round_is_reconciled_on_read_after_the_window_only()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        fx.Workflow.Complete(revision.ReviewRound.WorkflowInstanceId, ClaimReviewOutcomes.Approved);

        var fresh = await fx.GetRevision(id, revision.Id);           // inside the 120 s window: not asked
        Assert.Equal(KnowledgePathRevisionStatuses.InReview, fresh.Data!.Status);
        Assert.Empty(fx.Workflow.ByObjectsCalls);

        fx.Clock.Now = DateTimeOffset.UtcNow.AddSeconds(121);
        var later = await fx.GetRevision(id, revision.Id);
        Assert.Equal(KnowledgePathRevisionStatuses.Approved, later.Data!.Status);
        Assert.Equal(KnowledgePathReviewRules.ObjectType, Assert.Single(fx.Workflow.ByObjectsCalls).ObjectType);
        Assert.Equal(KnowledgePathStatuses.Approved, fx.Paths.Items.Single(p => p.Id == id).PathStatus);
    }

    // ================================================================ notes + reads

    [Fact]
    public async Task Notes_are_pinned_and_resolved_by_their_author_or_a_manager()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        Assert.Equal((400, KnowledgePathReviewErrors.NoteTextRequired), Code(await fx.AddNote(id, revision.Id, " ", Reviewer)));

        var note = (await fx.AddNote(id, revision.Id, "Grafik arka planı paletin dışında.", Reviewer, page: "p-3", block: "b-7", x: 0.4, y: 0.6)).Data!;
        Assert.Equal(("p-3", "b-7", 0.4, Reviewer), (note.PageRef, note.BlockRef, note.X, note.Author));

        Assert.Equal((403, ClaimErrorCodes.ApprovalForbidden), Code(await fx.ResolveNote(id, revision.Id, note.NoteId, "someone-else")));
        Assert.True((await fx.ResolveNote(id, revision.Id, note.NoteId, "manager-1", canManage: true)).IsSuccessful);
        Assert.Equal("manager-1", fx.Revisions.Items.Single().Notes.Single().ResolvedBy);
        Assert.Equal((404, KnowledgePathReviewErrors.NoteNotFound), Code(await fx.ResolveNote(id, revision.Id, Guid.NewGuid(), Reviewer)));
    }

    [Fact]
    public async Task Reads_list_revisions_detail_and_the_platform_history_with_step_names_and_comments()
    {
        var fx = new Fixture();
        var (id, revision) = await fx.SubmittedAsync();
        fx.Decisions.History[revision.ReviewRound.WorkflowInstanceId] =
        [
            new WorkflowHistoryEntry(2, "approve", "u-med", "Dr. Ayşe Kaya", "medical", "Medikal inceleme", "Mekanizma cümlesi uygun.", "APPROVED", Jan1.AddDays(1)),
            new WorkflowHistoryEntry(1, "start", Author, null, null, null, null, "CRM_KNOWLEDGE_PATH_SUBMITTED", Jan1)
        ];

        var list = (await new ListKnowledgePathRevisionsHandler(fx.Tenant, fx.Paths, fx.Revisions, fx.Reconciler())
            .Handle(new ListKnowledgePathRevisionsQuery(id), default)).Data!;
        Assert.Equal((1, KnowledgePathRevisionStatuses.InReview, true), (list.Single().RevisionNumber, list.Single().Status, list.Single().Round.IsOpen));

        var history = (await new GetKnowledgePathReviewHistoryHandler(fx.Tenant, fx.Paths, fx.Revisions, fx.Decisions)
            .Handle(new GetKnowledgePathReviewHistoryQuery(id), default)).Data!.Single();
        Assert.True(history.Available);
        Assert.Equal(["start", "approve"], history.Entries.Select(e => e.Action));
        Assert.Equal(("Medikal inceleme", "Mekanizma cümlesi uygun.", "Dr. Ayşe Kaya"),
            (history.Entries[1].StepName, history.Entries[1].Comment, history.Entries[1].ActorDisplay));
    }

    // ================================================================ direct publish gate

    [Fact]
    public async Task A_chain_bound_path_is_never_published_directly_a_legacy_path_still_is()
    {
        var fx = new Fixture();
        var chained = await fx.ReadyPathAsync();
        Assert.Equal((409, ClaimErrorCodes.ApprovalViaWorkflowOnly),
            Code(await fx.Publish().Handle(new PublishKnowledgePathCommand(chained), default)));

        var legacy = await fx.LegacyPathAsync();
        await fx.AddLegacyStep(legacy);
        Assert.True((await fx.Publish().Handle(new PublishKnowledgePathCommand(legacy), default)).IsSuccessful);
    }

    [Fact]
    public async Task Update_never_writes_approved_and_a_chain_bound_status_follows_the_round()
    {
        var fx = new Fixture();
        var chained = await fx.ReadyPathAsync();
        Assert.Equal((409, ClaimErrorCodes.ApprovalViaWorkflowOnly), Code(await fx.UpdateStatus(chained, KnowledgePathStatuses.Approved)));
        Assert.Equal((409, ClaimErrorCodes.ApprovalViaWorkflowOnly), Code(await fx.UpdateStatus(chained, KnowledgePathStatuses.Review)));
        await fx.Submit(chained);
        Assert.Equal((409, ClaimErrorCodes.ApprovalViaWorkflowOnly), Code(await fx.UpdateStatus(chained, KnowledgePathStatuses.Draft)));
        Assert.True((await fx.UpdateStatus(chained, null)).IsSuccessful);             // a plain edit keeps "review"
        Assert.Equal(KnowledgePathStatuses.Review, fx.Paths.Items.Single(p => p.Id == chained).PathStatus);

        var legacy = await fx.LegacyPathAsync();
        Assert.Equal((409, ClaimErrorCodes.ApprovalViaWorkflowOnly), Code(await fx.UpdateStatus(legacy, KnowledgePathStatuses.Approved)));
        Assert.True((await fx.UpdateStatus(legacy, KnowledgePathStatuses.Review)).IsSuccessful); // legacy behaviour kept
    }

    // ================================================================ Gateway decision calls + class map

    [Fact]
    public async Task The_gateway_client_uses_the_callers_token_for_my_tasks_decisions_with_comment_and_history()
    {
        var handler = new RecordingHandler();
        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = "Bearer user-token";
        http.Request.Headers["X-Tenant-Id"] = TenantA.ToString();
        var client = new GatewayClaimWorkflowClient(new HttpClient(handler) { BaseAddress = new Uri("http://gw.test") },
            new ConfigurationBuilder().Build(), new HttpContextAccessor { HttpContext = http },
            NullLogger<GatewayClaimWorkflowClient>.Instance);

        var mine = await client.GetMyTasksAsync(default);
        Assert.Equal(RecordingHandler.InstanceId, Assert.Single(mine!).WorkflowInstanceId);

        var result = await client.DecideTaskAsync(RecordingHandler.TaskId, approve: false, "u-legal", "R", "k-1", "Eksik kaynak.", default);
        Assert.Equal(ClaimWorkflowCallOutcome.Ok, result.Outcome);
        var post = handler.Requests.Single(r => r.Method == "POST");
        Assert.EndsWith($"/api/v1/workflow/tasks/{RecordingHandler.TaskId:D}/reject", post.Url);
        using (var body = JsonDocument.Parse(post.Body))
        {
            Assert.Equal(("Eksik kaynak.", "u-legal", "k-1"), (body.RootElement.GetProperty("comment").GetString(),
                body.RootElement.GetProperty("actorId").GetString(), body.RootElement.GetProperty("idempotencyKey").GetString()));
        }

        var history = await client.GetInstanceHistoryAsync(RecordingHandler.InstanceId, default);
        Assert.Equal(("Hukuk", "Eksik kaynak."), (history![0].StepName, history[0].Comment));
        Assert.All(handler.Requests, r => Assert.Equal("Bearer user-token", r.Authorization));
        Assert.Contains(handler.Requests, r => r.Url.EndsWith("/api/v1/workflow/tasks/mine"));
    }

    [Fact]
    public void The_revision_round_trips_with_string_guids()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var revision = new KnowledgePathRevision
        {
            TenantId = TenantA, PathId = Guid.NewGuid(), PathCode = "KP-A", PathVersion = "1.0", RevisionNumber = 2,
            Snapshot = new KnowledgePathRevisionSnapshot
            {
                ConceptChainTemplateId = Guid.NewGuid(), ProductId = ProductX, AudienceProfileIds = { Guid.NewGuid() },
                Steps = { new KnowledgePathRevisionStep { StepId = Guid.NewGuid(), ContentId = Guid.NewGuid(),
                    Arrangement = new KnowledgePathArrangement { BranchCode = "BR1", ChainStepId = Guid.NewGuid() } } },
                Claims = { new KnowledgePathRevisionClaim { ClaimId = Guid.NewGuid(), CountryVersionId = Guid.NewGuid() } },
                Conformance = { new KnowledgePathRevisionConformance { ChainStepId = Guid.NewGuid(), Status = "ok" } }
            },
            ReviewRound = new ClaimReviewRound { WorkflowInstanceId = Guid.NewGuid(), RoundNo = 2 },
            Notes = { new KnowledgePathRevisionNote { Text = "n", CarriedFromRevision = 1, X = 0.5 } },
            ChangeSummary = new KnowledgePathChangeSummary { ComparedToRevision = 1, Items = { new KnowledgePathChange { Kind = "step-added", Ref = "S-2" } } }
        };

        var doc = revision.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["PathId"].BsonType);
        Assert.Equal(BsonType.String, doc["Snapshot"]["ConceptChainTemplateId"].BsonType);
        Assert.Equal(BsonType.String, doc["Snapshot"]["AudienceProfileIds"][0].BsonType);
        Assert.Equal(BsonType.String, doc["Snapshot"]["Steps"][0]["ContentId"].BsonType);
        Assert.Equal(BsonType.String, doc["Snapshot"]["Claims"][0]["CountryVersionId"].BsonType);
        Assert.Equal(BsonType.String, doc["ReviewRound"]["WorkflowInstanceId"].BsonType);
        Assert.Equal(BsonType.String, doc["Notes"][0]["NoteId"].BsonType);

        var back = BsonSerializer.Deserialize<KnowledgePathRevision>(doc);
        Assert.Equal((revision.PathId, 2, ProductX, 1, "step-added"),
            (back.PathId, back.RevisionNumber, back.Snapshot.ProductId, back.Notes.Single().CarriedFromRevision, back.ChangeSummary.Items.Single().Kind));
        Assert.Empty(back.RenderedArtifacts);
        Assert.Null(back.ReleaseState);
    }

    // ================================================================ fixture

    private static (int, string) Code<T>(Response<T> r) => (r.StatusCode, r.Errors?.FirstOrDefault() ?? string.Empty);

    private static EventTransportMessage Completed(string objectType, Guid objectId, Guid instanceId, string outcome)
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(), tenantId = TenantA, workflowInstanceId = instanceId, templateCode = "KP-MLR-TR",
            objectType, objectId = objectId.ToString("D"), objectRef = "x", outcome, completedAt = DateTimeOffset.UtcNow,
            completedBy = "approver-7", finalStageCode = "regulatory", finalStepCode = "step-3", reasonCode = "APPROVED"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new EventTransportMessage(Guid.NewGuid(), ClaimWorkflowOutcomeConsumer.CompletedEventName, 1, Guid.NewGuid(),
            null, TenantA, "Diten.Platform", DateTimeOffset.UtcNow, payload);
    }

    private sealed class Fixture
    {
        public TenantContext Tenant { get; } = new();
        public ActorBox Actor { get; } = new() { Name = Author };
        public PathRepo Paths { get; } = new();
        public RevisionRepo Revisions { get; } = new();
        public ContentSetTestSubjects Subjects { get; } = new();
        public ContentSetTestProfiles Profiles { get; } = new();
        public ContentSetTestTemplates Templates { get; } = new();
        public ContentSetTestContents Contents { get; } = new();
        public ContentSetTestClaims Claims { get; } = new();
        public VersionRepo Versions { get; } = new();
        public ContentSetTestCatalog Catalog { get; } = new();
        public NodeRepo Nodes { get; } = new();
        public TopicRepo Topics { get; } = new();
        public FakeClaimWorkflowClient Workflow { get; } = new();
        public FakeDecisions Decisions { get; } = new();
        public TestClock Clock { get; } = new();
        public Guid T1 { get; } = Guid.NewGuid();
        public Guid T2 { get; } = Guid.NewGuid();
        public Subject Subject { get; }
        public ConceptChainTemplate Template { get; }
        public KnowledgeContent ContentTr { get; }
        public KnowledgeContent ContentTr2 { get; }
        public Claim ClaimOk { get; }
        public Claim ClaimDraftTr { get; }
        public Claim ClaimNoTr { get; }
        public ClaimCountryVersion VersionOk { get; }

        public Fixture()
        {
            Tenant.SetTenant(TenantA);
            Subject = new Subject
            {
                TenantId = TenantA, SubjectCode = "ALM", SubjectName = "Almiba",
                ExternalReferences = { new KnowledgeExternalReference { SourceSystem = "global-product", ExternalId = ProductX.ToString(), ExternalCode = "ALMIBA", IsPrimary = true } }
            };
            Subjects.Items.Add(Subject);
            Template = new ConceptChainTemplate
            {
                TenantId = TenantA, ChainCode = "TPL-ALM", ChainName = "Almiba", SubjectId = Subject.Id, ChainVersion = "1.0",
                Status = ConceptChainStatuses.Published, EffectiveFrom = Jan1, OrderedConceptTypes = { T1, T2 },
                Branches =
                {
                    new ConceptChainBranch { BranchCode = "BR1", SortOrder = 0, Steps = { new ConceptChainStep { ConceptTypeId = T1, MinSelection = 1, MaxSelection = 2 } } },
                    new ConceptChainBranch { BranchCode = "BR2", SortOrder = 1, Steps = { new ConceptChainStep { ConceptTypeId = T2, MinSelection = 0 } } }
                }
            };
            Templates.Items.Add(Template);
            ContentTr = Content("KC-1", "3.1");
            ContentTr2 = Content("KC-2", "1.0");
            ClaimOk = Claim("CLM-OK");
            ClaimDraftTr = Claim("CLM-DRAFT");
            ClaimNoTr = Claim("CLM-NOTR");
            VersionOk = Version(ClaimOk, "TR", ClaimStatuses.Approved);
            Version(ClaimDraftTr, "TR", ClaimStatuses.Draft);
            Version(ClaimNoTr, "UZ", ClaimStatuses.Approved);
        }

        private KnowledgeContent Content(string code, string version)
        {
            var c = new KnowledgeContent
            {
                TenantId = TenantA, ContentCode = code, ContentTitle = code, ContentType = KnowledgeContentTypes.Presentation,
                ContentStatus = KnowledgeContentStatuses.Published, SubjectId = Subject.Id, LanguageCode = "tr",
                ContentVersion = version, EffectiveFrom = Jan1, Url = "https://x"
            };
            Contents.Items.Add(c);
            return c;
        }

        private Claim Claim(string code)
        {
            var c = new Claim { TenantId = TenantA, ClaimCode = code, ClaimName = code, ClaimText = "t", ClaimVersion = "2.0", ProductId = ProductX, Status = ClaimStatuses.Approved };
            Claims.Items.Add(c);
            return c;
        }

        private ClaimCountryVersion Version(Claim claim, string country, string status)
        {
            var v = new ClaimCountryVersion
            {
                TenantId = TenantA, ClaimCode = claim.ClaimCode, ClaimId = claim.Id, CountryCode = country, CountryVersion = "1.0",
                Status = status, Texts = { new ClaimLocalizedText { LanguageCode = country == "TR" ? "tr" : "uz", Text = "metin" } }
            };
            Versions.Items.Add(v);
            return v;
        }

        private ChainContextResolver Resolver() => new(Templates, Subjects, Profiles);
        private KnowledgePathStudioReader Studio() => new(Templates, Resolver(), Claims, Versions);

        public KnowledgePathRevisionOutcomeApplier Applier() => new(Revisions, Paths);
        public KnowledgePathReviewReconciler Reconciler() => new(Workflow, Applier(), clock: Clock);

        public PublishKnowledgePathHandler Publish() => new(Tenant, Actor, Paths);

        public Task<Response<KnowledgePathRevisionDto>> Submit(Guid id)
            => new SubmitKnowledgePathReviewHandler(Tenant, Actor, Paths, Revisions, Contents, Claims, Studio(), Workflow, Reconciler())
                .Handle(new SubmitKnowledgePathReviewCommand(id), default);

        public Task<Response<KnowledgePathRevisionDto>> Withdraw(Guid id)
            => new WithdrawKnowledgePathReviewHandler(Tenant, Actor, Paths, Revisions, Workflow, Applier())
                .Handle(new WithdrawKnowledgePathReviewCommand(id), default);

        public Task<Response<KnowledgePathDecisionDto>> Decide(Guid id, Guid revisionId, string decision, string? comment)
            => new DecideKnowledgePathRevisionHandler(Tenant, Actor, Paths, Revisions, Workflow, Decisions)
                .Handle(new DecideKnowledgePathRevisionCommand(id, revisionId, decision, comment), default);

        public Task<Response<KnowledgePathRevisionDto>> GetRevision(Guid id, Guid revisionId)
            => new GetKnowledgePathRevisionHandler(Tenant, Paths, Revisions, Reconciler())
                .Handle(new GetKnowledgePathRevisionQuery(id, revisionId), default);

        public Task<Response<KnowledgePathRevisionNoteDto>> AddNote(Guid id, Guid revisionId, string text, string author,
            string? page = null, string? block = null, double? x = null, double? y = null)
        {
            var actor = new ActorBox { Name = author };
            return new AddKnowledgePathRevisionNoteHandler(Tenant, actor, Paths, Revisions)
                .Handle(new AddKnowledgePathRevisionNoteCommand(id, revisionId, page, block, null, x, y, text), default);
        }

        public Task<Response<KnowledgePathRevisionNoteDto>> ResolveNote(Guid id, Guid revisionId, Guid noteId, string who, bool canManage = false)
            => new ResolveKnowledgePathRevisionNoteHandler(Tenant, new ActorBox { Name = who }, Paths, Revisions)
                .Handle(new ResolveKnowledgePathRevisionNoteCommand(id, revisionId, noteId, canManage), default);

        public async Task<Guid> ChainedPathAsync(string code = "KP-A")
        {
            var r = await new CreateKnowledgePathHandler(Tenant, Actor, Paths, Subjects, Topics, Profiles, Templates, Catalog, Resolver())
                .Handle(new CreateKnowledgePathCommand(code, "Almiba detay", Guid.Empty, "Objective", "1.0", Jan1,
                    LanguageCode: "tr", ChainTemplateId: Template.Id, CountryCode: "TR"), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }

        public async Task<Guid> ReadyPathAsync(string code = "KP-A")
        {
            var id = await ChainedPathAsync(code);
            Assert.Equal(201, (await AddStep(id, ContentTr.Id, "S-1")).StatusCode);
            return id;
        }

        public async Task<(Guid PathId, KnowledgePathRevision Revision)> SubmittedAsync()
        {
            var id = await ReadyPathAsync();
            Assert.Equal(201, (await Submit(id)).StatusCode);
            return (id, Revisions.Items.Single());
        }

        public Task<Response<Guid>> AddStep(Guid id, Guid contentId, string code)
            => new AddKnowledgePathStepHandler(Tenant, Actor, Paths, Contents, Nodes, Templates)
                .Handle(new AddKnowledgePathStepCommand(id, 10, code, "Step", "core-message", contentId, true,
                    Arrangement: new KnowledgePathArrangementInput(T1, "BR1", 0)), default);

        public async Task PlaceClaim(Guid id, Claim claim)
            => Assert.True((await new AddKnowledgePathClaimHandler(Tenant, Actor, Paths, Templates, Subjects, Claims)
                .Handle(new AddKnowledgePathClaimCommand(id, claim.Id, new KnowledgePathArrangementInput(T1, "BR1", 1)), default)).IsSuccessful);

        public async Task RemoveClaim(Guid id, Guid claimId)
            => Assert.True((await new RemoveKnowledgePathClaimHandler(Tenant, Actor, Paths)
                .Handle(new RemoveKnowledgePathClaimCommand(id, claimId), default)).IsSuccessful);

        public async Task<Guid> LegacyPathAsync()
        {
            var r = await new CreateKnowledgePathHandler(Tenant, Actor, Paths, Subjects, Topics, Profiles)
                .Handle(new CreateKnowledgePathCommand("KP-OLD", "Eski", Subject.Id, "Objective", "1.0", Jan1, LanguageCode: "tr"), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }

        public async Task AddLegacyStep(Guid id)
            => Assert.Equal(201, (await new AddKnowledgePathStepHandler(Tenant, Actor, Paths, Contents, Nodes, Templates)
                .Handle(new AddKnowledgePathStepCommand(id, 10, "S10", "Step", "core-message", ContentTr.Id, true), default)).StatusCode);

        public Task<Response<bool>> UpdateStatus(Guid id, string? status)
        {
            var p = Paths.Items.Single(x => x.Id == id);
            return new UpdateKnowledgePathHandler(Tenant, Actor, Paths, Subjects, Topics, Profiles)
                .Handle(new UpdateKnowledgePathCommand(id, p.PathName, p.SubjectId, p.Objective, p.PathVersion, p.EffectiveFrom,
                    LanguageCode: p.LanguageCode, PathStatus: status), default);
        }
    }

    private sealed class ActorBox : IActorContext
    {
        public string? Name { get; set; }
        public string? ActorName => Name;
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset? Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now ?? DateTimeOffset.UtcNow;
    }

    private sealed class Inbox : ICrmEventInboxRepository
    {
        private readonly HashSet<Guid> _seen = new();
        public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(_seen.Add(eventId));
    }

    private sealed class FakeDecisions : IWorkflowDecisionClient
    {
        public List<ClaimWorkflowTaskState> Mine { get; } = new();
        public bool Unreachable { get; set; }
        public WorkflowDecisionResult Outcome { get; set; } = new(ClaimWorkflowCallOutcome.Ok, null);
        public List<(Guid TaskId, bool Approve, string Actor, string Reason, string Key, string? Comment)> Calls { get; } = new();
        public Dictionary<Guid, IReadOnlyList<WorkflowHistoryEntry>> History { get; } = new();

        public Task<IReadOnlyList<ClaimWorkflowTaskState>?> GetMyTasksAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ClaimWorkflowTaskState>?>(Unreachable ? null : Mine.ToList());

        public Task<WorkflowDecisionResult> DecideTaskAsync(Guid taskId, bool approve, string actorId, string reasonCode,
            string idempotencyKey, string? comment, CancellationToken ct)
        {
            Calls.Add((taskId, approve, actorId, reasonCode, idempotencyKey, comment));
            return Task.FromResult(Outcome);
        }

        public Task<IReadOnlyList<WorkflowHistoryEntry>?> GetInstanceHistoryAsync(Guid workflowInstanceId, CancellationToken ct)
            => Task.FromResult(History.TryGetValue(workflowInstanceId, out var h) ? h : null);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public static readonly Guid InstanceId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        public static readonly Guid TaskId = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        public List<(string Method, string Url, string Body, string? Authorization)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add((request.Method.Method, url, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct),
                request.Headers.TryGetValues("Authorization", out var a) ? a.Single() : null));
            object data = url.EndsWith("/tasks/mine")
                ? new[] { new { id = TaskId, workflowInstanceId = InstanceId, stageCode = "legal", stepCode = "step-2", status = "WaitingApproval" } }
                : url.EndsWith("/history")
                    ? new[] { new { sequenceNo = 3, action = "reject", actorId = "u-legal", fromStepCode = "step-2", stepName = "Hukuk", comment = "Eksik kaynak.", occurredAt = Jan1 } }
                    : new { workflowInstanceId = InstanceId };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { data }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class PathRepo : IKnowledgePathRepository
    {
        public List<KnowledgePath> Items { get; } = new();
        public Task<KnowledgePath?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<KnowledgePath>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<KnowledgePath>> ListByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult((IReadOnlyList<KnowledgePath>)Items.Where(x => x.TenantId == t && x.PathCode == code).ToList());
        public Task InsertAsync(KnowledgePath e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task<bool> ReplaceAsync(KnowledgePath e, int expectedVersion, CancellationToken ct)
        {
            var stored = Items.FirstOrDefault(x => x.Id == e.Id);
            if (stored is null || stored.Version != expectedVersion) return Task.FromResult(false);
            e.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class RevisionRepo : IKnowledgePathRevisionRepository
    {
        public List<KnowledgePathRevision> Items { get; } = new();
        public Task<KnowledgePathRevision?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<KnowledgePathRevision>> ListByPathAsync(Guid t, Guid pathId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<KnowledgePathRevision>)Items.Where(x => x.TenantId == t && x.PathId == pathId).OrderBy(x => x.RevisionNumber).ToList());
        public Task InsertAsync(KnowledgePathRevision e, CancellationToken ct)
        {
            Assert.DoesNotContain(Items, x => x.PathId == e.PathId && x.RevisionNumber == e.RevisionNumber); // the unique index
            Items.Add(e);
            return Task.CompletedTask;
        }
        public Task<bool> ReplaceAsync(KnowledgePathRevision e, int expectedVersion, CancellationToken ct)
        {
            var stored = Items.FirstOrDefault(x => x.Id == e.Id);
            if (stored is null || stored.Version != expectedVersion) return Task.FromResult(false);
            e.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class VersionRepo : IClaimCountryVersionRepository
    {
        public List<ClaimCountryVersion> Items { get; } = new();
        public Task<ClaimCountryVersion?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(x => x.TenantId == t && x.ClaimCode == code).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(Guid t, Guid claimId, CancellationToken ct) => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(x => x.TenantId == t && x.ClaimId == claimId).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<ClaimCountryVersion>)Items.Where(x => x.TenantId == t).ToList());
        public Task InsertAsync(ClaimCountryVersion e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ClaimCountryVersion e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NodeRepo : IConceptNodeRepository
    {
        public Task<ConceptNode?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<ConceptNode?>(null);
        public Task<IReadOnlyList<ConceptNode>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<ConceptNode>)Array.Empty<ConceptNode>());
        public Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct) => Task.FromResult((IReadOnlyList<ConceptNode>)Array.Empty<ConceptNode>());
        public Task<ConceptNode?> GetActiveByCodeAsync(Guid t, Guid s, Guid ty, string code, CancellationToken ct) => Task.FromResult<ConceptNode?>(null);
        public Task InsertAsync(ConceptNode e, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(ConceptNode e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TopicRepo : ITopicRepository
    {
        public Task<Topic?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<Topic?>(null);
        public Task<IReadOnlyList<Topic>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<Topic>)Array.Empty<Topic>());
        public Task<IReadOnlyList<Topic>> ListBySubjectAsync(Guid t, Guid s, CancellationToken ct) => Task.FromResult((IReadOnlyList<Topic>)Array.Empty<Topic>());
        public Task<Topic?> GetActiveByCodeAsync(Guid t, Guid s, string code, CancellationToken ct) => Task.FromResult<Topic?>(null);
        public Task InsertAsync(Topic e, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Topic e, CancellationToken ct) => Task.CompletedTask;
    }
}
