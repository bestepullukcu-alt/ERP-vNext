using System.Text.Json;
using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Infrastructure.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.MdmService.Application.Tests.Approvals;

/// <summary>
/// WP-VW-W4 (W4-BE-b1) — the MDM approval core on a TEST-ONLY approvable record (Acceptance): submit → the start call with
/// the <c>{type}:{id}:r{round}</c> idempotency key; the approval event → applied; a foreign tenant / a wrong instance / a
/// closed round → not applied; a repeated event → applied once (inbox); SoD (author / editor / submitter approval → draft +
/// <c>…_sod</c>); reconcile 120 s later on read; a CAS conflict → 409.
/// </summary>
public sealed class WorkflowApprovalCoreTests
{
    private static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly Guid OtherTenant = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string Author = "u-author";
    private const string Editor = "u-editor";
    private const string Submitter = "u-submitter";
    private const string Approver = "u-approver";

    private static WorkflowDisplayContext Display => new("Rakip önerisi", null, MdmApprovalDefaults.SourceModule, "/MDM/x", []);

    private sealed class Harness
    {
        public InMemoryApprovalStore Store { get; } = new();
        public FakeWorkflowClient Client { get; } = new();
        public FixedActor Actor { get; } = new(Submitter);
        public InMemoryInbox Inbox { get; } = new();
        public WorkflowApprovalOutcomeApplier Applier { get; }
        public WorkflowApprovalFlow Flow { get; }
        public MdmWorkflowOutcomeConsumer Consumer { get; }
        public TestApprovable Record { get; }

        public Harness(Func<TestApprovable, string, CancellationToken, Task<string?>>? extra = null)
        {
            Applier = new WorkflowApprovalOutcomeApplier([TestApprovableKind.Target(Store, extra)]);
            Flow = new WorkflowApprovalFlow(Client, Actor, Applier);
            Consumer = new MdmWorkflowOutcomeConsumer(Applier, Inbox, NullLogger<MdmWorkflowOutcomeConsumer>.Instance);
            Record = new TestApprovable { TenantId = Tenant, CreatedBy = Author, UpdatedBy = Editor, Version = 4 };
            Store.Seed(Record);
        }

        public TestApprovable Current => Store.Peek(Tenant, Record.Id)!;

        public async Task<Guid> SubmitAsync()
        {
            var record = (await Store.GetAsync(Tenant, Record.Id, default))!;
            var result = await Flow.SubmitAsync<TestApprovable, bool>(Store, record, TestApprovableKind.Kind, "MDM-COMP-PROPOSAL",
                Display, TestApprovableStatuses.InReview, default);
            Assert.Null(result);
            return Client.NextInstanceId;
        }

        public Task<WorkflowApplyResult?> EventAsync(Guid instanceId, string outcome, string? completedBy, Guid? payloadTenant = null,
            Guid? envelopeTenant = null, Guid? eventId = null, string objectType = "mdm.test-approvable", Guid? objectId = null)
        {
            var payload = JsonSerializer.Serialize(new
            {
                tenantId = payloadTenant ?? Tenant,
                workflowInstanceId = instanceId,
                objectType,
                objectId = (objectId ?? Record.Id).ToString("D"),
                outcome,
                completedAt = DateTimeOffset.Parse("2026-10-13T10:00:00Z"),
                completedBy,
                reasonCode = "WORKCENTER_APPROVE"
            });
            return Consumer.ConsumeAsync(new EventTransportMessage(eventId ?? Guid.NewGuid(), MdmWorkflowOutcomeConsumer.CompletedEventName, 1,
                Guid.NewGuid(), null, envelopeTenant ?? payloadTenant ?? Tenant, "platform", DateTimeOffset.UtcNow, payload));
        }
    }

    // ── submit ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_starts_the_workflow_with_the_round_idempotency_key_and_moves_the_record_in_review()
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();

        var start = Assert.Single(h.Client.Starts);
        Assert.Equal("MDM-COMP-PROPOSAL", start.TemplateCode);
        Assert.Equal("mdm.test-approvable", start.ObjectType);
        Assert.Equal(h.Record.Id.ToString("D"), start.ObjectId);
        Assert.Equal($"mdm/test-approvable/{h.Record.Id:D}", start.ObjectRef);
        Assert.Equal($"mdm.test-approvable:{h.Record.Id:D}:r1", start.IdempotencyKey);
        Assert.Equal("MDM_TEST_APPROVABLE_SUBMITTED", start.ReasonCode);

        var stored = h.Current;
        Assert.Equal(TestApprovableStatuses.InReview, stored.Status);
        Assert.Equal(5, stored.Version);
        var round = Assert.Single(stored.ReviewRounds);
        Assert.Equal((instance, 1, Submitter, "MDM-COMP-PROPOSAL"), (round.WorkflowInstanceId, round.RoundNo, round.SubmittedBy, round.TemplateCode));

        // after the round closes (returned), the next submit is round 2
        await h.EventAsync(instance, WorkflowApprovalOutcomes.Rejected, Approver);
        h.Client.NextInstanceId = Guid.NewGuid();
        await h.SubmitAsync();
        Assert.Equal($"mdm.test-approvable:{h.Record.Id:D}:r2", h.Client.Starts[1].IdempotencyKey);
    }

    [Theory]
    [InlineData(WorkflowCallOutcome.Unavailable, 503, WorkflowApprovalErrors.WorkflowUnavailable)]
    [InlineData(WorkflowCallOutcome.TemplateMissing, 409, WorkflowApprovalErrors.TemplateMissing)]
    [InlineData(WorkflowCallOutcome.Forbidden, 403, WorkflowApprovalErrors.ApprovalForbidden)]
    public async Task A_refused_start_changes_nothing(WorkflowCallOutcome outcome, int status, string code)
    {
        var h = new Harness();
        h.Client.StartOutcome = outcome;
        var record = (await h.Store.GetAsync(Tenant, h.Record.Id, default))!;
        var result = await h.Flow.SubmitAsync<TestApprovable, bool>(h.Store, record, TestApprovableKind.Kind, "T", Display,
            TestApprovableStatuses.InReview, default);
        Assert.Equal(status, result!.StatusCode);
        Assert.Equal(code, result.Errors[0]);
        Assert.Equal(0, h.Store.Replaces);
        Assert.Equal(TestApprovableStatuses.Draft, h.Current.Status);
    }

    // ── the outcome event ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_approval_event_is_applied_and_a_machine_reason_code_is_not_stored()
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();

        Assert.Equal(WorkflowApplyResult.Applied, await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, Approver));

        var stored = h.Current;
        Assert.Equal(TestApprovableStatuses.Applied, stored.Status);
        Assert.Equal(Approver, stored.ApprovedBy);
        var round = Assert.Single(stored.ReviewRounds);
        Assert.Equal(WorkflowApprovalOutcomes.Approved, round.Outcome);
        Assert.Null(round.ReasonCode); // WORKCENTER_APPROVE is a machine code, not a note
        var decision = Assert.Single(stored.Decisions);
        Assert.Equal((WorkflowApprovalDecisions.Approve, Approver), (decision.Outcome, decision.By));
    }

    [Fact]
    public async Task A_foreign_tenant_a_wrong_instance_a_closed_round_or_a_tenant_mismatch_is_not_applied()
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();

        Assert.Equal(WorkflowApplyResult.NotFound, await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, Approver, payloadTenant: OtherTenant));
        Assert.Null(await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, Approver, payloadTenant: Tenant, envelopeTenant: OtherTenant));
        Assert.Equal(WorkflowApplyResult.NoMatchingOpenRound, await h.EventAsync(Guid.NewGuid(), WorkflowApprovalOutcomes.Approved, Approver));
        Assert.Equal(TestApprovableStatuses.InReview, h.Current.Status);

        // the round closes (returned) — a late approval of the same instance is not applied
        Assert.Equal(WorkflowApplyResult.Applied, await h.EventAsync(instance, WorkflowApprovalOutcomes.Rejected, Approver));
        Assert.Equal(WorkflowApplyResult.NoMatchingOpenRound, await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, Approver));
        var stored = h.Current;
        Assert.Equal(TestApprovableStatuses.Draft, stored.Status);
        Assert.Null(stored.ApprovedBy);
        Assert.Equal(WorkflowApprovalDecisions.Return, Assert.Single(stored.Decisions).Outcome);
    }

    [Fact]
    public async Task Another_modules_workflow_and_an_unknown_outcome_are_ignored()
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();
        Assert.Null(await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, Approver, objectType: "crm.objection-response"));
        Assert.Null(await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, Approver, objectType: "mdm.unknown-kind"));
        Assert.Null(await h.EventAsync(instance, "maybe", Approver));
        Assert.True(WorkflowApprovalOutcomeApplier.Handles("mdm.competitor-proposal"));
        Assert.False(WorkflowApprovalOutcomeApplier.Handles("crm.claim"));
        Assert.Equal(TestApprovableStatuses.InReview, h.Current.Status);
    }

    [Fact]
    public async Task A_repeated_event_is_applied_once_through_the_inbox()
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();
        var eventId = Guid.NewGuid();
        var replacesBefore = h.Store.Replaces;

        Assert.Equal(WorkflowApplyResult.Applied, await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, Approver, eventId: eventId));
        Assert.Null(await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, Approver, eventId: eventId));
        Assert.Equal(replacesBefore + 1, h.Store.Replaces);
    }

    // ── separation of duties ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Author)]
    [InlineData(Editor)]
    [InlineData(Submitter)]
    [InlineData("  U-SUBMITTER ")] // the same person, written differently
    public async Task An_approval_by_the_author_the_editor_or_the_submitter_goes_back_to_draft_with_the_sod_code(string approver)
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();

        Assert.Equal(WorkflowApplyResult.Applied, await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, approver));

        var stored = h.Current;
        Assert.Equal(TestApprovableStatuses.Draft, stored.Status);
        Assert.Null(stored.ApprovedBy);
        Assert.Equal(TestApprovableKind.SodCode, Assert.Single(stored.ReviewRounds).ReasonCode);
    }

    [Fact]
    public async Task The_owner_cannot_decide_and_another_reviewer_decides_on_the_workflow_task()
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();
        var record = (await h.Store.GetAsync(Tenant, h.Record.Id, default))!;

        h.Actor.ActorName = Editor;
        var own = await h.Flow.DecideAsync<TestApprovable, bool>(h.Store, record, TestApprovableKind.Kind, "approve", null, TestApprovableKind.SodCode, default);
        Assert.Equal((403, TestApprovableKind.SodCode), (own!.StatusCode, own.Errors[0]));
        Assert.Empty(h.Client.Decisions);

        h.Actor.ActorName = Approver;
        var noNote = await h.Flow.DecideAsync<TestApprovable, bool>(h.Store, record, TestApprovableKind.Kind, "return", "  ", TestApprovableKind.SodCode, default);
        Assert.Equal(WorkflowApprovalErrors.RejectionCommentRequired, noNote!.Errors[0]);

        var task = FakeWorkflowClient.OpenTask(instance);
        h.Client.MyTasks.Add(task);
        Assert.Null(await h.Flow.DecideAsync<TestApprovable, bool>(h.Store, record, TestApprovableKind.Kind, "return", " Kaynak eksik ", TestApprovableKind.SodCode, default));
        var decided = Assert.Single(h.Client.Decisions);
        Assert.Equal((task.TaskId, false, Approver, "Kaynak eksik"), (decided.TaskId, decided.Approve, decided.Actor, decided.Comment));
        Assert.Equal($"mdm.test-approvable:{h.Record.Id:D}:r1:{task.TaskId:D}:return", decided.Key);
        Assert.Equal("Kaynak eksik", Assert.Single(h.Current.Decisions).Comment);
    }

    [Fact]
    public async Task Only_the_submitter_withdraws_and_the_record_returns_to_draft()
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();
        h.Client.Tasks.Add(FakeWorkflowClient.OpenTask(instance));

        h.Actor.ActorName = Approver;
        var foreign = await h.Flow.WithdrawAsync<TestApprovable, bool>(h.Current, TestApprovableKind.Kind, default);
        Assert.Equal(403, foreign!.StatusCode);
        Assert.Empty(h.Client.Cancels);

        h.Actor.ActorName = Submitter;
        Assert.Null(await h.Flow.WithdrawAsync<TestApprovable, bool>(h.Current, TestApprovableKind.Kind, default));
        Assert.Equal("MDM_TEST_APPROVABLE_WITHDRAWN", Assert.Single(h.Client.Cancels).Reason);
        var stored = h.Current;
        Assert.Equal(TestApprovableStatuses.Draft, stored.Status);
        Assert.Equal(WorkflowApprovalOutcomes.Cancelled, Assert.Single(stored.ReviewRounds).Outcome);
        Assert.Empty(stored.Decisions); // a cancelled round leaves no decision
    }

    [Fact]
    public async Task The_kinds_own_refusal_also_sends_an_approval_back_to_draft()
    {
        var h = new Harness((r, by, ct) => Task.FromResult<string?>("test_target_gone"));
        var instance = await h.SubmitAsync();
        await h.EventAsync(instance, WorkflowApprovalOutcomes.Approved, Approver);
        Assert.Equal((TestApprovableStatuses.Draft, "test_target_gone"), (h.Current.Status, h.Current.ReviewRounds[0].ReasonCode));
    }

    // ── reconcile on read ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reconcile_reads_a_round_older_than_120_seconds_from_the_workflow_and_applies_it_with_the_actioning_approver()
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();
        var submittedAt = h.Current.ReviewRounds[0].SubmittedAt;
        var clock = new ManualClock(submittedAt.AddSeconds(119));
        var reconciler = new WorkflowApprovalReconciler(h.Client, h.Applier, settings: null, clock: clock);
        h.Client.Instances[h.Record.Id.ToString("D")] = [new WorkflowInstanceState(instance, "Completed", WorkflowApprovalOutcomes.Approved, submittedAt.AddSeconds(60))];
        h.Client.Tasks.Add(FakeWorkflowClient.OpenTask(instance, status: "Approved", actionedBy: Approver, completedAt: submittedAt.AddSeconds(60)));

        Assert.Equal(0, await reconciler.ReconcileAsync(Tenant, TestApprovableKind.Kind, [h.Current], default));
        Assert.Equal(0, h.Client.ByObjectsCalls); // not due yet: MOD-0023 is not even asked

        clock.Now = submittedAt.AddSeconds(121);
        Assert.Equal(1, await reconciler.ReconcileAsync(Tenant, TestApprovableKind.Kind, [h.Current], default));
        Assert.Equal((TestApprovableStatuses.Applied, Approver), (h.Current.Status, h.Current.ApprovedBy));
    }

    [Fact]
    public async Task Reconcile_does_not_apply_an_approval_whose_approver_is_unknown()
    {
        var h = new Harness();
        var instance = await h.SubmitAsync();
        var submittedAt = h.Current.ReviewRounds[0].SubmittedAt;
        var reconciler = new WorkflowApprovalReconciler(h.Client, h.Applier, clock: new ManualClock(submittedAt.AddMinutes(5)));
        h.Client.Instances[h.Record.Id.ToString("D")] = [new WorkflowInstanceState(instance, "Completed", WorkflowApprovalOutcomes.Approved, null)];

        Assert.Equal(0, await reconciler.ReconcileAsync(Tenant, TestApprovableKind.Kind, [h.Current], default));
        Assert.Equal(TestApprovableStatuses.InReview, h.Current.Status);
    }

    // ── CAS ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_lost_compare_and_swap_answers_409()
    {
        var h = new Harness();
        h.Store.AlwaysConflict = true;
        var record = (await h.Store.GetAsync(Tenant, h.Record.Id, default))!;
        var result = await h.Flow.SubmitAsync<TestApprovable, bool>(h.Store, record, TestApprovableKind.Kind, "T", Display,
            TestApprovableStatuses.InReview, default);
        Assert.Equal((409, WorkflowApprovalErrors.ConcurrencyConflict), (result!.StatusCode, result.Errors[0]));
    }
}
