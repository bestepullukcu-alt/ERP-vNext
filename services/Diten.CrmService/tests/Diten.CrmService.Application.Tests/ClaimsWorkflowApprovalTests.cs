using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Authorization;
using Diten.CrmService.Infrastructure.Eventing;
using Diten.CrmService.Infrastructure.Workflow;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-CL-BE-4 — claims approved ONLY through MOD-0023: submit / withdraw, the completion-event consumer (inbox,
/// binding to the open round), the single outcome applier (+ the moved core propagation), reconcile-on-read, review
/// history, the removed direct approve, the in-review lock and tenant isolation. MOD-0023 is faked at the
/// <see cref="IClaimWorkflowClient"/> seam; the Gateway client itself is tested over a stub HTTP handler.
/// </summary>
public sealed class ClaimsWorkflowApprovalTests
{
    private static readonly Guid TenantA = Guid.Parse("a4a4a4a4-0000-0000-0000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("b4b4b4b4-0000-0000-0000-00000000000b");
    private static readonly Guid Product = Guid.Parse("0000aaaa-0000-0000-0000-0000000000a1");

    // ============================================================ fixture

    private sealed class Store
    {
        public List<Claim> Claims { get; } = new();
        public List<ClaimCountryVersion> Versions { get; } = new();
    }

    private sealed class Fx
    {
        public Store Store { get; }
        public Guid TenantId { get; }
        public ClaimRepo Claims { get; }
        public VersionRepo Versions { get; }
        public FakeClaimWorkflowClient Workflow { get; } = new();
        public Audit Audit { get; } = new();
        public Languages Refs { get; } = new();
        public Clock Clock { get; } = new();

        public Fx(Guid tenant, Store? shared = null)
        {
            TenantId = tenant;
            Store = shared ?? new Store();
            Claims = new ClaimRepo(Store);
            Versions = new VersionRepo(Store);
        }

        public TenantContext Ctx()
        {
            var ctx = new TenantContext();
            ctx.SetTenant(TenantId);
            return ctx;
        }

        public ClaimReviewOutcomeApplier Applier() => new(Claims, Versions, Audit);

        public ClaimReviewReconciler Reconciler(int afterSeconds = 120) =>
            new(Workflow, Applier(), new Settings(afterSeconds), Clock);

        public SubmitClaimReviewHandler Submit() => new(Ctx(), new Actor("alice"), Claims, Workflow, new Settings(), Audit);

        public SubmitClaimCountryVersionReviewHandler SubmitVersion() =>
            new(Ctx(), new Actor("alice"), Claims, Versions, Workflow, Refs, new Settings(), Audit);

        public WithdrawClaimReviewHandler Withdraw() => new(Ctx(), new Actor("alice"), Claims, Workflow, Applier(), Audit);

        public WithdrawClaimCountryVersionReviewHandler WithdrawVersion() =>
            new(Ctx(), new Actor("alice"), Versions, Workflow, Applier(), Audit);

        public ClaimWorkflowOutcomeConsumer Consumer(Inbox inbox) =>
            new(Applier(), inbox, NullLogger<ClaimWorkflowOutcomeConsumer>.Instance);

        public Claim SeedClaim(string code = "CL-1", string status = ClaimStatuses.Draft, string kind = ClaimKinds.Core,
            string? localCountry = null, string version = "1.0", Guid? product = null, Guid? supersedes = null)
        {
            var claim = new Claim
            {
                TenantId = TenantId, ClaimCode = code, ClaimName = "Name " + code, ClaimText = "Reduces symptoms",
                ClaimVersion = version, Status = status, Kind = kind, LocalCountryCode = localCountry,
                ProductId = product ?? Product, SupersedesClaimId = supersedes, EffectiveFrom = DateTimeOffset.UtcNow
            };
            if (status == ClaimStatuses.Approved)
            {
                claim.ApprovedAt = DateTimeOffset.UtcNow.AddDays(-1);
            }

            Store.Claims.Add(claim);
            return claim;
        }

        public ClaimCountryVersion SeedVersion(Claim claim, string country, string status = ClaimStatuses.Draft,
            params string[] languages)
        {
            var version = new ClaimCountryVersion
            {
                TenantId = TenantId, ClaimCode = claim.ClaimCode, ClaimId = claim.Id, BoundCoreVersion = claim.ClaimVersion,
                CountryCode = country, CountryVersion = "1.0", Status = status, AdaptationTypeCode = "verbatim",
                ValidFrom = DateTimeOffset.UtcNow.Date,
                Texts = (languages.Length == 0 ? Refs.Of(country) : languages)
                    .Select(l => new ClaimLocalizedText { LanguageCode = l, Text = "t-" + l }).ToList()
            };
            Store.Versions.Add(version);
            return version;
        }
    }

    private static EventTransportMessage Completed(Guid tenant, string objectType, Guid objectId, Guid instanceId,
        string outcome, Guid? eventId = null, string eventName = ClaimWorkflowOutcomeConsumer.CompletedEventName,
        string completedBy = "approver-7", string reasonCode = "APPROVED")
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(), tenantId = tenant, workflowInstanceId = instanceId, templateCode = "CLAIM-CORE-MLR",
            objectType, objectId = objectId.ToString("D"), objectRef = "x", outcome,
            completedAt = DateTimeOffset.UtcNow, completedBy, finalStageCode = "stage-1", finalStepCode = "step-1", reasonCode
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new EventTransportMessage(eventId ?? Guid.NewGuid(), eventName, 1, Guid.NewGuid(), null, tenant,
            "Diten.Platform", DateTimeOffset.UtcNow, payload);
    }

    // ============================================================ submit

    [Fact]
    public async Task Submit_core_starts_the_round_with_display_context_and_idempotency_key()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim("CL-SUB", version: "2.0");

        var r = await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);

        Assert.Equal(201, r.StatusCode);
        var start = Assert.Single(fx.Workflow.Starts);
        Assert.Equal("CLAIM-CORE-MLR", start.TemplateCode);
        Assert.Equal("crm.claim", start.ObjectType);
        Assert.Equal(claim.Id.ToString("D"), start.ObjectId);
        Assert.Equal($"crm/claim/{claim.Id:D}", start.ObjectRef);
        Assert.Equal($"crm:crm.claim:{claim.Id:D}:r1", start.IdempotencyKey);
        Assert.Equal("İddia onayı · CL-SUB v2.0", start.DisplayContext.Title);
        Assert.Equal("Name CL-SUB", start.DisplayContext.Subtitle);
        Assert.Equal("crm", start.DisplayContext.SourceModule);
        Assert.Equal($"/CRM/Claims/Details/{claim.Id:D}", start.DisplayContext.DeepLinkUrl);
        Assert.Equal(["core", "v2.0"], start.DisplayContext.Chips);

        Assert.Equal(ClaimStatuses.InReview, claim.Status);
        var round = Assert.Single(claim.ReviewRounds);
        Assert.Equal(1, round.RoundNo);
        Assert.Equal(fx.Workflow.LastInstanceId, round.WorkflowInstanceId);
        Assert.Equal("CLAIM-CORE-MLR", round.TemplateCode);
        Assert.Equal("alice", round.SubmittedBy);
        Assert.True(round.IsOpen());
        Assert.Contains(fx.Audit.Events, e => e.Event == ClaimReasonCodes.ReviewSubmitted && e.Detail == "CL-SUB|r1");
    }

    [Fact]
    public async Task Local_claim_uses_its_country_template()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim("CL-LOC", kind: ClaimKinds.Local, localCountry: "UZ");
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);
        Assert.Equal("CLAIM-LOCAL-MLR-UZ", fx.Workflow.Starts.Single().TemplateCode);
    }

    [Theory]
    [InlineData(ClaimWorkflowCallOutcome.TemplateMissing, 409, ClaimErrorCodes.ApprovalTemplateMissing)]
    [InlineData(ClaimWorkflowCallOutcome.Forbidden, 403, ClaimErrorCodes.ApprovalForbidden)]
    [InlineData(ClaimWorkflowCallOutcome.Unavailable, 503, ClaimErrorCodes.WorkflowUnavailable)]
    [InlineData(ClaimWorkflowCallOutcome.Rejected, 400, ClaimErrorCodes.WorkflowRequestRejected)]
    public async Task A_refused_start_writes_nothing(ClaimWorkflowCallOutcome outcome, int status, string code)
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim();
        fx.Workflow.StartOutcome = outcome;

        var r = await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);

        Assert.Equal(status, r.StatusCode);
        Assert.Equal(code, r.Errors![0]);
        Assert.Equal(ClaimStatuses.Draft, claim.Status);
        Assert.Empty(claim.ReviewRounds);
        Assert.Equal(0, fx.Claims.Updates);
        Assert.Empty(fx.Audit.Events);
    }

    [Fact]
    public async Task Only_a_draft_with_a_product_is_submitted()
    {
        var fx = new Fx(TenantA);
        var approved = fx.SeedClaim("CL-AP", ClaimStatuses.Approved);
        Assert.Equal(ClaimErrorCodes.InvalidStatus,
            (await fx.Submit().Handle(new SubmitClaimReviewCommand(approved.Id), default)).Errors![0]);

        var noProduct = fx.SeedClaim("CL-NP");
        noProduct.ProductId = null;
        Assert.Equal(ClaimErrorCodes.ProductRequired,
            (await fx.Submit().Handle(new SubmitClaimReviewCommand(noProduct.Id), default)).Errors![0]);
        Assert.Empty(fx.Workflow.Starts);
    }

    [Fact]
    public async Task Country_version_submit_checks_core_languages_and_closure_then_starts_the_country_template()
    {
        var fx = new Fx(TenantA);
        var draftCore = fx.SeedClaim("CL-D");
        var onDraftCore = fx.SeedVersion(draftCore, "TR");
        Assert.Equal(ClaimErrorCodes.CoreNotApproved,
            (await fx.SubmitVersion().Handle(new SubmitClaimCountryVersionReviewCommand(onDraftCore.Id), default)).Errors![0]);

        var core = fx.SeedClaim("CL-C", ClaimStatuses.Approved);
        var partial = fx.SeedVersion(core, "UZ", ClaimStatuses.Draft, "uz");
        var incomplete = await fx.SubmitVersion().Handle(new SubmitClaimCountryVersionReviewCommand(partial.Id), default);
        Assert.Equal(400, incomplete.StatusCode);
        Assert.Equal(ClaimErrorCodes.LanguagesIncomplete, incomplete.Errors![0]);

        var closed = fx.SeedVersion(core, "GE");
        core.CountryClosures.Add(new ClaimCountryClosure { CountryCode = "GE", ReasonCode = "no-license", ClosedAt = DateTimeOffset.UtcNow });
        Assert.Equal(ClaimErrorCodes.CountryClosed,
            (await fx.SubmitVersion().Handle(new SubmitClaimCountryVersionReviewCommand(closed.Id), default)).Errors![0]);
        Assert.Empty(fx.Workflow.Starts);

        var ok = fx.SeedVersion(core, "BY");
        var r = await fx.SubmitVersion().Handle(new SubmitClaimCountryVersionReviewCommand(ok.Id), default);
        Assert.Equal(201, r.StatusCode);
        var start = fx.Workflow.Starts.Single();
        Assert.Equal("CLAIM-LOCAL-MLR-BY", start.TemplateCode);
        Assert.Equal("crm.claim-country-version", start.ObjectType);
        Assert.Equal($"crm/claim-country-version/{ok.Id:D}", start.ObjectRef);
        Assert.Equal("İddia ülke onayı · CL-C · BY v1.0", start.DisplayContext.Title);
        Assert.Equal($"/CRM/Claims/Details/{core.Id:D}?country=BY", start.DisplayContext.DeepLinkUrl);
        Assert.Equal(["BY", "v1.0"], start.DisplayContext.Chips);
        Assert.Equal(ClaimStatuses.InReview, ok.Status);
    }

    // ============================================================ consumer + applier

    [Theory]
    [InlineData(ClaimReviewOutcomes.Approved, ClaimStatuses.Approved)]
    [InlineData(ClaimReviewOutcomes.Rejected, ClaimStatuses.Draft)]
    [InlineData(ClaimReviewOutcomes.Cancelled, ClaimStatuses.Draft)]
    [InlineData(ClaimReviewOutcomes.TimedOut, ClaimStatuses.Draft)]
    public async Task Consumer_applies_each_outcome_to_the_open_round(string outcome, string expectedStatus)
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim();
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);
        var instance = claim.ReviewRounds.Single().WorkflowInstanceId;

        await fx.Consumer(new Inbox()).ConsumeAsync(
            Completed(TenantA, "crm.claim", claim.Id, instance, outcome, reasonCode: "R-1"));

        Assert.Equal(expectedStatus, claim.Status);
        var round = claim.ReviewRounds.Single();
        Assert.Equal(outcome, round.Outcome);
        Assert.Equal("approver-7", round.CompletedBy);
        Assert.Equal("R-1", round.ReasonCode);
        Assert.NotNull(round.ClosedAt);
        if (outcome == ClaimReviewOutcomes.Approved)
        {
            Assert.Equal("approver-7", claim.ApprovedBy);
        }

        Assert.Contains(fx.Audit.Events, e => e.Event == ClaimReasonCodes.ReviewOutcomeApplied
            && e.Detail == $"CL-1|r1|{outcome}");
    }

    [Fact]
    public async Task Consumer_ignores_foreign_events_duplicates_unbound_instances_and_tenant_mismatch()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim();
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);
        var instance = claim.ReviewRounds.Single().WorkflowInstanceId;
        var inbox = new Inbox();
        var consumer = fx.Consumer(inbox);

        await consumer.ConsumeAsync(Completed(TenantA, "crm.claim", claim.Id, instance, "approved",
            eventName: "tenant.entitlement.enabled.v1"));
        await consumer.ConsumeAsync(Completed(TenantA, "tasks.task-review", claim.Id, instance, "approved"));
        await consumer.ConsumeAsync(Completed(TenantA, "crm.claim", claim.Id, Guid.NewGuid(), "approved")); // not the open round
        await consumer.ConsumeAsync(Completed(TenantB, "crm.claim", claim.Id, instance, "approved")); // other tenant: not found
        Assert.Equal(ClaimStatuses.InReview, claim.Status);

        var eventId = Guid.NewGuid();
        await consumer.ConsumeAsync(Completed(TenantA, "crm.claim", claim.Id, instance, "rejected", eventId));
        Assert.Equal(ClaimStatuses.Draft, claim.Status);

        // Resubmit, then REDELIVER the old rejected event: the inbox refuses it before it can touch round 2.
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);
        await consumer.ConsumeAsync(Completed(TenantA, "crm.claim", claim.Id, instance, "rejected", eventId));
        Assert.Equal(ClaimStatuses.InReview, claim.Status);
        Assert.Equal(2, claim.ReviewRounds.Count);
        Assert.Equal($"crm:crm.claim:{claim.Id:D}:r2", fx.Workflow.Starts[1].IdempotencyKey);
    }

    [Fact]
    public async Task Core_approval_propagates_old_core_inactive_and_country_versions_review_required()
    {
        var fx = new Fx(TenantA);
        var v1 = fx.SeedClaim("CL-P", ClaimStatuses.Approved, version: "1.0");
        var tr = fx.SeedVersion(v1, "TR", ClaimStatuses.Approved);
        var trApprovedAt = tr.ApprovedAt = DateTimeOffset.UtcNow.AddDays(-3);
        var v2 = fx.SeedClaim("CL-P", version: "2.0", supersedes: v1.Id);
        await fx.Submit().Handle(new SubmitClaimReviewCommand(v2.Id), default);

        await fx.Consumer(new Inbox()).ConsumeAsync(
            Completed(TenantA, "crm.claim", v2.Id, v2.ReviewRounds.Single().WorkflowInstanceId, "approved"));

        Assert.Equal(ClaimStatuses.Approved, v2.Status);
        Assert.Equal(ClaimStatuses.Inactive, v1.Status);
        Assert.Equal(ClaimStatuses.ReviewRequired, tr.Status);
        Assert.Equal(trApprovedAt, tr.ApprovedAt);
        Assert.Contains(fx.Audit.Events, e => e.Event == ClaimReasonCodes.CountryVersionsReviewRequired);
    }

    [Fact]
    public async Task Country_version_approval_inactivates_the_previous_approved_version()
    {
        var fx = new Fx(TenantA);
        var core = fx.SeedClaim("CL-CV", ClaimStatuses.Approved);
        var previous = fx.SeedVersion(core, "TR", ClaimStatuses.ReviewRequired);
        var next = fx.SeedVersion(core, "TR");
        await fx.SubmitVersion().Handle(new SubmitClaimCountryVersionReviewCommand(next.Id), default);

        await fx.Consumer(new Inbox()).ConsumeAsync(Completed(TenantA, "crm.claim-country-version", next.Id,
            next.ReviewRounds.Single().WorkflowInstanceId, "approved"));

        Assert.Equal(ClaimStatuses.Approved, next.Status);
        Assert.Equal(ClaimStatuses.Inactive, previous.Status);
        Assert.Contains(fx.Audit.Events, e => e.Event == ClaimReasonCodes.CountryVersionApproved);
    }

    // ============================================================ reconcile-on-read

    [Fact]
    public async Task Reconcile_on_read_closes_an_overdue_round_through_by_objects()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim("CL-R");
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);
        var instance = claim.ReviewRounds.Single().WorkflowInstanceId;
        fx.Workflow.Complete(instance, ClaimReviewOutcomes.Approved);

        // Not overdue yet → MOD-0023 is not asked.
        var list = new ListClaimsHandler(fx.Ctx(), fx.Claims, fx.Versions, fx.Reconciler());
        await list.Handle(new ListClaimsQuery(), default);
        Assert.Empty(fx.Workflow.ByObjectsCalls);
        Assert.Equal(ClaimStatuses.InReview, claim.Status);

        fx.Clock.Now = DateTimeOffset.UtcNow.AddSeconds(121);
        var dto = (await list.Handle(new ListClaimsQuery(), default)).Data!.Items.Single();
        Assert.Equal(ClaimStatuses.Approved, dto.Status);
        var call = fx.Workflow.ByObjectsCalls.Single();
        Assert.Equal("crm.claim", call.ObjectType);
        Assert.Equal([claim.Id.ToString("D")], call.Ids);
        Assert.Equal(ClaimReviewOutcomes.Approved, claim.ReviewRounds.Single().Outcome);
    }

    [Fact]
    public async Task Reconcile_on_read_covers_detail_and_country_versions_too()
    {
        var fx = new Fx(TenantA);
        var core = fx.SeedClaim("CL-RD", ClaimStatuses.Approved);
        var version = fx.SeedVersion(core, "AZ");
        await fx.SubmitVersion().Handle(new SubmitClaimCountryVersionReviewCommand(version.Id), default);
        fx.Workflow.Complete(version.ReviewRounds.Single().WorkflowInstanceId, ClaimReviewOutcomes.Rejected);
        fx.Clock.Now = DateTimeOffset.UtcNow.AddMinutes(5);

        var dto = (await new GetClaimCountryVersionHandler(fx.Ctx(), fx.Versions, null, fx.Reconciler())
            .Handle(new GetClaimCountryVersionQuery(version.Id), default)).Data!;
        Assert.Equal(ClaimStatuses.Draft, dto.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Platform_unreachable_never_breaks_the_read(bool throws)
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim("CL-U");
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);
        fx.Workflow.Unreachable = !throws;
        fx.Workflow.Throws = throws;
        fx.Clock.Now = DateTimeOffset.UtcNow.AddHours(1);

        var r = await new GetClaimHandler(fx.Ctx(), fx.Claims, fx.Versions, fx.Reconciler())
            .Handle(new GetClaimQuery(claim.Id), default);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal(ClaimStatuses.InReview, r.Data!.Status);
    }

    // ============================================================ withdraw

    [Fact]
    public async Task Withdraw_cancels_the_waiting_task_and_returns_the_claim_to_draft()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim("CL-W");
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);
        var instance = claim.ReviewRounds.Single().WorkflowInstanceId;
        var task = fx.Workflow.Tasks.Single(t => t.WorkflowInstanceId == instance);

        var r = await fx.Withdraw().Handle(new WithdrawClaimReviewCommand(claim.Id), default);

        Assert.True(r.IsSuccessful, string.Join(",", r.Errors ?? []));
        var cancel = Assert.Single(fx.Workflow.Cancels);
        Assert.Equal(task.TaskId, cancel.TaskId);
        Assert.Equal("alice", cancel.Actor);
        Assert.Equal($"crm:crm.claim:{claim.Id:D}:r1:withdraw", cancel.Key);
        Assert.Equal(ClaimStatuses.Draft, claim.Status);
        Assert.Equal(ClaimReviewOutcomes.Cancelled, claim.ReviewRounds.Single().Outcome);
        Assert.Contains(fx.Audit.Events, e => e.Event == ClaimReasonCodes.ReviewWithdrawn);

        // MOD-0023's own "cancelled" completion event arrives later: nothing left to apply.
        await fx.Consumer(new Inbox()).ConsumeAsync(Completed(TenantA, "crm.claim", claim.Id, instance, "cancelled"));
        Assert.Equal(ClaimStatuses.Draft, claim.Status);
    }

    [Fact]
    public async Task Withdraw_fails_closed_when_platform_is_down_or_nothing_is_open()
    {
        var fx = new Fx(TenantA);
        var draft = fx.SeedClaim("CL-WD");
        Assert.Equal(ClaimErrorCodes.NoOpenReview,
            (await fx.Withdraw().Handle(new WithdrawClaimReviewCommand(draft.Id), default)).Errors![0]);

        var core = fx.SeedClaim("CL-WV", ClaimStatuses.Approved);
        var version = fx.SeedVersion(core, "TR");
        await fx.SubmitVersion().Handle(new SubmitClaimCountryVersionReviewCommand(version.Id), default);
        fx.Workflow.Unreachable = true;
        var r = await fx.WithdrawVersion().Handle(new WithdrawClaimCountryVersionReviewCommand(version.Id), default);
        Assert.Equal(503, r.StatusCode);
        Assert.Equal(ClaimStatuses.InReview, version.Status);
        Assert.True(version.ReviewRounds.Single().IsOpen());
    }

    // ============================================================ history, lock, direct approve

    [Fact]
    public async Task Review_history_lists_rounds_newest_first_with_step_rows_or_rounds_only_when_platform_is_down()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim("CL-H");
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);
        await fx.Consumer(new Inbox()).ConsumeAsync(Completed(TenantA, "crm.claim", claim.Id,
            claim.ReviewRounds[0].WorkflowInstanceId, "rejected"));
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);

        var history = (await new GetClaimReviewHistoryHandler(fx.Ctx(), fx.Claims, fx.Workflow)
            .Handle(new GetClaimReviewHistoryQuery(claim.Id), default)).Data!;
        Assert.Equal([2, 1], history.Select(h => h.RoundNo));
        Assert.True(history[0].IsOpen);
        Assert.Equal(ClaimReviewOutcomes.Rejected, history[1].Outcome);
        Assert.True(history[0].StepsAvailable);
        Assert.Equal("step-1", history[0].Steps!.Single().StepCode);

        fx.Workflow.Unreachable = true;
        var roundsOnly = (await new GetClaimReviewHistoryHandler(fx.Ctx(), fx.Claims, fx.Workflow)
            .Handle(new GetClaimReviewHistoryQuery(claim.Id), default)).Data!;
        Assert.Equal(2, roundsOnly.Count);
        Assert.All(roundsOnly, h => Assert.False(h.StepsAvailable));
    }

    [Fact]
    public async Task In_review_records_are_locked_for_update_new_version_and_archive()
    {
        var fx = new Fx(TenantA);
        var claim = fx.SeedClaim("CL-L");
        await fx.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);

        var update = await new UpdateClaimHandler(fx.Ctx(), new Actor("alice"), fx.Claims)
            .Handle(new UpdateClaimCommand(claim.Id, "x", "y", DateTimeOffset.UtcNow), default);
        Assert.Equal(ClaimErrorCodes.InReviewLocked, update.Errors![0]);
        var archive = await new ArchiveClaimHandler(fx.Ctx(), new Actor("alice"), fx.Claims)
            .Handle(new ArchiveClaimCommand(claim.Id), default);
        Assert.Equal(ClaimErrorCodes.InReviewLocked, archive.Errors![0]);
        var newVersion = await new CreateClaimNewVersionHandler(fx.Ctx(), new Actor("alice"), fx.Claims)
            .Handle(new CreateClaimNewVersionCommand(claim.Id), default);
        Assert.Equal(ClaimErrorCodes.InReviewLocked, newVersion.Errors![0]);

        var core = fx.SeedClaim("CL-LV", ClaimStatuses.Approved);
        var version = fx.SeedVersion(core, "TR");
        await fx.SubmitVersion().Handle(new SubmitClaimCountryVersionReviewCommand(version.Id), default);
        var archiveVersion = await new ArchiveClaimCountryVersionHandler(fx.Ctx(), new Actor("alice"), fx.Versions)
            .Handle(new ArchiveClaimCountryVersionCommand(version.Id), default);
        Assert.Equal(ClaimErrorCodes.InReviewLocked, archiveVersion.Errors![0]);
        Assert.Equal(ClaimStatuses.InReview, version.Status);
    }

    [Fact]
    public void Direct_approve_endpoints_answer_approval_via_workflow_only()
    {
        var controller = new ClaimsController(null!);
        foreach (var result in new[] { controller.Approve(Guid.NewGuid()), controller.ApproveCountryVersion(Guid.NewGuid()) })
        {
            var obj = Assert.IsAssignableFrom<ObjectResult>(result);
            Assert.Equal(409, obj.StatusCode);
            var body = Assert.IsType<Response<bool>>(obj.Value);
            Assert.Equal(ClaimErrorCodes.ApprovalViaWorkflowOnly, body.Errors![0]);
        }

        // The approval commands are gone from production code.
        var application = typeof(ClaimReviewOutcomeApplier).Assembly;
        Assert.Null(application.GetType("Diten.CrmService.Application.Features.ContentComposition.Claims.ApproveClaimCommand"));
        Assert.Null(application.GetType(
            "Diten.CrmService.Application.Features.ContentComposition.Claims.ApproveClaimCountryVersionCommand"));
    }

    [Theory]
    [InlineData(nameof(ClaimsController.SubmitReview), "POST", "claims/{claimId:guid}/submit-review", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.WithdrawReview), "POST", "claims/{claimId:guid}/withdraw-review", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.ReviewHistory), "GET", "claims/{claimId:guid}/review-history", ClaimPermissions.Read)]
    [InlineData(nameof(ClaimsController.SubmitCountryVersionReview), "POST", "claims/country-versions/{countryVersionId:guid}/submit-review", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.WithdrawCountryVersionReview), "POST", "claims/country-versions/{countryVersionId:guid}/withdraw-review", ClaimPermissions.Manage)]
    [InlineData(nameof(ClaimsController.CountryVersionReviewHistory), "GET", "claims/country-versions/{countryVersionId:guid}/review-history", ClaimPermissions.Read)]
    public void Review_endpoints_use_existing_claim_permissions(string action, string verb, string route, string permission)
    {
        var method = typeof(ClaimsController).GetMethod(action)!;
        var http = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        Assert.Contains(verb, http.HttpMethods);
        Assert.Equal("api/crm/content-composition/" + route, http.Template);
        Assert.Equal(permission, method.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
    }

    // ============================================================ tenant isolation + persistence

    [Fact]
    public async Task Tenant_isolation_submit_withdraw_and_history_do_not_see_another_tenant()
    {
        var a = new Fx(TenantA);
        var claim = a.SeedClaim("CL-T");
        await a.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default);

        var b = new Fx(TenantB, a.Store);
        Assert.Equal(404, (await b.Submit().Handle(new SubmitClaimReviewCommand(claim.Id), default)).StatusCode);
        Assert.Equal(404, (await b.Withdraw().Handle(new WithdrawClaimReviewCommand(claim.Id), default)).StatusCode);
        Assert.Equal(404, (await new GetClaimReviewHistoryHandler(b.Ctx(), b.Claims, b.Workflow)
            .Handle(new GetClaimReviewHistoryQuery(claim.Id), default)).StatusCode);
        Assert.Equal(ClaimReviewApplyResult.NotFound, await b.Applier().ApplyAsync(TenantB, "crm.claim", claim.Id,
            claim.ReviewRounds.Single().WorkflowInstanceId, "approved", null, null, DateTimeOffset.UtcNow, default));
        Assert.Equal(ClaimStatuses.InReview, claim.Status);
    }

    [Fact]
    public void Review_rounds_on_a_claim_store_the_workflow_instance_id_as_a_string()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var instance = Guid.NewGuid();
        var doc = new Claim
        {
            TenantId = TenantA, ReviewRounds = { new ClaimReviewRound { WorkflowInstanceId = instance, RoundNo = 1 } }
        }.ToBsonDocument();
        var round = doc["ReviewRounds"].AsBsonArray[0];
        Assert.Equal(BsonType.String, round["WorkflowInstanceId"].BsonType);
        Assert.Equal(instance.ToString(), round["WorkflowInstanceId"].AsString);
        Assert.Equal(1, round["RoundNo"].AsInt32);
    }

    // ============================================================ Gateway client (stub HTTP)

    [Fact]
    public async Task Gateway_client_forwards_the_callers_token_and_tenant_and_sends_no_candidates()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            "{\"data\":{\"workflowInstanceId\":\"11111111-2222-3333-4444-555555555555\"},\"isSuccessful\":true}");
        var client = GatewayClient(handler, "Bearer user-token", TenantA);

        var r = await client.StartAsync(new ClaimWorkflowStartRequest("CLAIM-CORE-MLR", "crm.claim", "id-1",
            "crm/claim/id-1", "crm:crm.claim:id-1:r1",
            new ClaimWorkflowDisplayContext("T", "S", "crm", "/CRM/Claims/Details/id-1", ["core", "v1.0"])), default);

        Assert.Equal(ClaimWorkflowCallOutcome.Ok, r.Outcome);
        Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), r.WorkflowInstanceId);
        var sent = handler.Requests.Single();
        Assert.Equal("/api/v1/workflow/instances", sent.Path);
        Assert.Equal("Bearer user-token", sent.Authorization);
        Assert.Equal(TenantA.ToString(), sent.Tenant);
        using var body = JsonDocument.Parse(sent.Body!);
        Assert.Equal(0, body.RootElement.GetProperty("candidatePrincipalIds").GetArrayLength());
        Assert.Equal("CLAIM-CORE-MLR", body.RootElement.GetProperty("templateCode").GetString());
        Assert.Equal("crm:crm.claim:id-1:r1", body.RootElement.GetProperty("idempotencyKey").GetString());
        Assert.Equal("/CRM/Claims/Details/id-1",
            body.RootElement.GetProperty("displayContext").GetProperty("deepLinkUrl").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "{\"reason_code\":\"NOT_FOUND_NON_LEAKAGE\"}", ClaimWorkflowCallOutcome.TemplateMissing)]
    [InlineData(HttpStatusCode.Conflict, "{\"reason_code\":\"WORKFLOW_TEMPLATE_NO_ACTIVE_VERSION\"}", ClaimWorkflowCallOutcome.TemplateMissing)]
    [InlineData(HttpStatusCode.Forbidden, "", ClaimWorkflowCallOutcome.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized, "", ClaimWorkflowCallOutcome.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError, "", ClaimWorkflowCallOutcome.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", ClaimWorkflowCallOutcome.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, "{\"reason_code\":\"VALIDATION_FAILED\",\"errors\":[\"bad\"]}", ClaimWorkflowCallOutcome.Rejected)]
    public async Task Gateway_client_maps_platform_answers(HttpStatusCode status, string body, ClaimWorkflowCallOutcome expected)
    {
        var client = GatewayClient(new StubHandler(status, body), "Bearer t", TenantA);
        var r = await client.StartAsync(new ClaimWorkflowStartRequest("T", "crm.claim", "1", "r", "k",
            new ClaimWorkflowDisplayContext("t", null, "crm", "/x", [])), default);
        Assert.Equal(expected, r.Outcome);
    }

    [Fact]
    public async Task Gateway_client_network_failure_is_unavailable_and_reads_return_null()
    {
        var client = GatewayClient(new StubHandler(throws: true), "Bearer t", TenantA);
        Assert.Equal(ClaimWorkflowCallOutcome.Unavailable, (await client.StartAsync(new ClaimWorkflowStartRequest("T",
            "crm.claim", "1", "r", "k", new ClaimWorkflowDisplayContext("t", null, "crm", "/x", [])), default)).Outcome);
        Assert.Null(await client.GetInstancesByObjectsAsync("crm.claim", ["1"], default));
        Assert.Null(await client.GetTasksAsync([Guid.NewGuid()], default));
        Assert.Equal(ClaimWorkflowCallOutcome.Unavailable,
            await client.CancelTaskAsync(Guid.NewGuid(), "a", "r", "k", default));
    }

    [Fact]
    public async Task Gateway_client_reads_by_objects_and_filters_tasks_by_instance()
    {
        var instance = Guid.NewGuid();
        var byObjects = new StubHandler(HttpStatusCode.OK, "{\"data\":[{\"objectId\":\"o1\",\"instances\":[{\"workflowInstanceId\":\""
            + instance + "\",\"status\":\"Completed\",\"outcome\":\"approved\",\"completedAt\":\"2026-10-01T10:00:00+00:00\"}]}]}");
        var states = await GatewayClient(byObjects, "Bearer t", TenantA)
            .GetInstancesByObjectsAsync("crm.claim", ["o1", "o2"], default);
        Assert.Equal("approved", states!["o1"].Single().Outcome);
        Assert.Contains("objectIds=o1%2Co2", byObjects.Requests.Single().PathAndQuery);

        var other = Guid.NewGuid();
        var tasks = new StubHandler(HttpStatusCode.OK, "{\"data\":["
            + $"{{\"id\":\"{Guid.NewGuid()}\",\"workflowInstanceId\":\"{instance}\",\"stageCode\":\"s\",\"stepCode\":\"p\",\"status\":\"WaitingApproval\"}},"
            + $"{{\"id\":\"{Guid.NewGuid()}\",\"workflowInstanceId\":\"{other}\",\"stageCode\":\"s\",\"stepCode\":\"p\",\"status\":\"WaitingApproval\"}}]}}");
        var rows = await GatewayClient(tasks, "Bearer t", TenantA).GetTasksAsync([instance], default);
        Assert.True(Assert.Single(rows!).IsOpen);
    }

    private static GatewayClaimWorkflowClient GatewayClient(StubHandler handler, string authorization, Guid tenant)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = authorization;
        context.Request.Headers["X-Tenant-Id"] = tenant.ToString();
        return new GatewayClaimWorkflowClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test") },
            new ConfigurationBuilder().Build(),
            new HttpContextAccessor { HttpContext = context },
            NullLogger<GatewayClaimWorkflowClient>.Instance);
    }

    // ============================================================ fakes

    private sealed class StubHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "", bool throws = false)
        : HttpMessageHandler
    {
        public List<(string Path, string PathAndQuery, string? Authorization, string? Tenant, string? Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.RequestUri!.AbsolutePath, request.RequestUri.PathAndQuery,
                request.Headers.TryGetValues("Authorization", out var a) ? a.Single() : null,
                request.Headers.TryGetValues("X-Tenant-Id", out var t) ? t.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            if (throws)
            {
                throw new HttpRequestException("connection refused");
            }

            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Actor(string name) : IActorContext
    {
        public string? ActorName => name;
    }

    private sealed class Settings(int reconcileAfterSeconds = 120) : IClaimWorkflowSettings
    {
        public string CoreTemplateCode => "CLAIM-CORE-MLR";
        public string LocalTemplateCodeFormat => "CLAIM-LOCAL-MLR-{0}";
        public int ReconcileAfterSeconds => reconcileAfterSeconds;
    }

    private sealed class Clock : TimeProvider
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

    private sealed class Languages : IReferenceMetadataReader
    {
        private static readonly Dictionary<string, string> Map = new()
        {
            ["TR"] = "tr", ["BY"] = "ru,be", ["UZ"] = "uz,ru", ["TM"] = "tk,ru", ["GE"] = "ka", ["AZ"] = "az"
        };

        public string[] Of(string country) => Map[country].Split(',');

        public Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, string>?>(Map.TryGetValue(value, out var l)
                ? new Dictionary<string, string> { ["Languages"] = l }
                : null);
    }

    private sealed class Audit : IContentCompositionAuditPublisher
    {
        public List<(string Event, string EntityType, Guid EntityId, string? Detail)> Events { get; } = new();

        public Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version,
            string? detail, CancellationToken cancellationToken)
        {
            Events.Add((eventName, entityType, entityId, detail));
            return Task.CompletedTask;
        }
    }

    private sealed class ClaimRepo(Store s) : IClaimRepository
    {
        public int Updates { get; private set; }
        private IEnumerable<Claim> Of(Guid t) => s.Claims.Where(x => x.TenantId == t && !x.IsDeleted);
        public Task<Claim?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Of(t).FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<Claim>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult<IReadOnlyList<Claim>>(Of(t).ToList());
        public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid t, string code, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Claim>>(Of(t).Where(x => x.ClaimCode == code).ToList());
        public Task<Claim?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) =>
            Task.FromResult(Of(t).FirstOrDefault(x => x.ClaimCode == code && !x.IsArchived()));
        public Task InsertAsync(Claim e, CancellationToken ct) { s.Claims.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(Claim e, CancellationToken ct) { Updates++; return Task.CompletedTask; }
    }

    private sealed class VersionRepo(Store s) : IClaimCountryVersionRepository
    {
        private IEnumerable<ClaimCountryVersion> Of(Guid t) => s.Versions.Where(x => x.TenantId == t && !x.IsDeleted);
        public Task<ClaimCountryVersion?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Of(t).FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimCodeAsync(Guid t, string code, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClaimCountryVersion>>(Of(t).Where(x => x.ClaimCode == code).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListByClaimIdAsync(Guid t, Guid claimId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClaimCountryVersion>>(Of(t).Where(x => x.ClaimId == claimId).ToList());
        public Task<IReadOnlyList<ClaimCountryVersion>> ListAsync(Guid t, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClaimCountryVersion>>(Of(t).ToList());
        public Task InsertAsync(ClaimCountryVersion e, CancellationToken ct) { s.Versions.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(ClaimCountryVersion e, CancellationToken ct) => Task.CompletedTask;
    }
}
