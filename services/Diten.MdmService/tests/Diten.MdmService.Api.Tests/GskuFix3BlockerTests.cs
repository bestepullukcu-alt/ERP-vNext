using System.Net;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX3, the addendum's blockers through the real API. E1: an identity approval closed without
/// a decision sends the pair back to draft. E2: after the engine started the approval, the submitter's repeat of the
/// same submit (and the requester's repeat of the same withdrawal) reads the engine again and puts the operation back
/// where the pair stands — or says, truthfully, that a person has to reconcile it.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    // FIX5 item 1 (BL-547 merged): Platform now names an approval cancelled or timed out without a decision. The identity
    // flow's branch that FIX3 wrote and FIX4 kept dormant is live: the pair goes back to draft, the maker sees why.
    [Fact]
    public async Task E1_An_identity_approval_closed_without_a_decision_sends_the_pair_back_to_draft()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        host.Engine.CloseWithoutDecision(host.Engine.Open("gsku", gsku.ToString("D")));

        var draft = await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.Draft);

        Assert.Equal("FIRST_GSKU_IDENTITY_CLOSED_WITHOUT_DECISION", draft.NotAppliedReason);
        Assert.Contains("EDIT", draft.Actions);
        Assert.Contains("SUBMIT", draft.Actions);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable, (await IdentityOperationOf(gsku)).Checkpoint);
    }

    // Evidence that contradicts itself is not "closed": quarantined, the pair kept pending, and the submitter's repeat
    // says truthfully that a person has to reconcile it.
    [Fact]
    public async Task E1_Inconsistent_identity_evidence_is_quarantined_and_the_repeat_says_a_person_has_to_reconcile()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted, (await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit",
            new { expectedVersion = version }, MakerToken(), key)).StatusCode);
        host.Engine.Open("gsku", gsku.ToString("D")).EvidenceInconsistent = true;

        for (var cycle = 0; cycle < 10 && (await IdentityOperationOf(gsku)).Checkpoint
                 != FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired; cycle++)
        {
            await host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.FirstGskuIdentityWorkflowRecoveryRunner>(
                runner => runner.RunCycleAsync());
            await Task.Delay(1100);
        }
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, (await IdentityOperationOf(gsku)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await DetailAsync(gsku)).Status);

        var repeat = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = version },
            MakerToken(), key);

        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal(FirstGskuIdentityWorkflowProcessor.ManualReconciliationNeeded, Envelope.FirstError(await Envelope.ReadAsync(repeat)));
    }

    // Only the engine's own "closed without a decision" code counts as closed.
    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION", true)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT", false)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION", false)]
    public void E1_Only_the_engines_own_closed_code_counts_as_closed_without_a_decision(
        ProductIdentityWorkflowTransportOutcome outcome, string code, bool closed)
    {
        Assert.Equal(closed, Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.ProductIdentityWorkflowFaultPolicy
            .IsClosedWithoutDecision(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(outcome, code)));
    }

    /*
     * FIX5 item 1 (CT decision 3, final) on a CORRECTION and a RETIREMENT REQUEST, by Platform's own answer:
     *   closed     — WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION: the request ends, the GSKU is free (not counted);
     *   inconsistent — WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT: stuck with the lock held (counted); the requester's same
     *                command reads the engine again — still contradictory: a person (409); once the engine's facts are
     *                straight and decided: applied;
     *   escalated  — WORKFLOW_DECISION_NOT_TERMINAL (SLA-escalated, still open): waits; approved later: applied.
     */
    [Theory]
    [InlineData("correction", "closed")]
    [InlineData("correction", "inconsistent")]
    [InlineData("correction", "escalated")]
    [InlineData("retirement", "closed")]
    [InlineData("retirement", "inconsistent")]
    [InlineData("retirement", "escalated")]
    public async Task CT3_Each_terminal_answer_of_the_engine_has_its_own_outcome(string flow, string answer)
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        var correction = flow == "correction";
        var path = correction ? "correction-requests" : "retirement-requests";
        object body = correction
            ? new { expectedGskuVersion = approved.Version, packQuantity = 5m, packUomCode = "C62" }
            : new { expectedGskuVersion = approved.Version, requestReason = "PRODUCT_DISCONTINUED" };
        Task<HttpResponseMessage> Request() => SendAsync(HttpMethod.Post, $"/api/gskus/{approved.Id:D}/{path}", body, MakerToken(), key);
        Assert.True((await Request()).IsSuccessStatusCode);
        var instance = host.Engine.Latest(correction ? "GskuCorrection" : GskuRetirementRequestWorkflowStartRequestFactory.ObjectType);
        var prefix = correction ? "GSKU_CORRECTION" : "GSKU_RETIREMENT_REQUEST";
        var free = correction ? "REQUEST_CORRECTION" : "REQUEST_RETIREMENT";
        Func<Func<Detail, bool>, Task<Detail>> cycles = correction
            ? until => CorrectionCyclesUntilAsync(approved.Id, until)
            : until => RetirementCyclesUntilAsync(approved.Id, until);

        switch (answer)
        {
            case "closed":
            {
                host.Engine.CloseWithoutDecision(instance);
                var freed = await cycles(detail => detail.Actions.Contains(free));
                var (code, disposition) = await StateOf(approved.Id, correction);
                Assert.Equal($"{prefix}_CLOSED_WITHOUT_DECISION", code);
                Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, disposition); // ended, not stuck
                Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, freed.Status);
                break;
            }
            case "inconsistent":
            {
                instance.EvidenceInconsistent = true;
                var stuck = await cycles(detail => detail.LifecycleRequestState == "STUCK");
                Assert.DoesNotContain(free, stuck.Actions);
                var (code, disposition) = await StateOf(approved.Id, correction);
                Assert.Equal($"{prefix}_EVIDENCE_CONFLICT", code);
                Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, disposition);

                var still = await Request();
                Assert.Equal(HttpStatusCode.Conflict, still.StatusCode);
                Assert.Equal($"{prefix}_MANUAL_RECONCILIATION_NEEDED", Envelope.FirstError(await Envelope.ReadAsync(still)));

                instance.EvidenceInconsistent = false;
                host.Engine.Approve(instance, host.Approver);
                var settled = await Request();
                Assert.True(settled.IsSuccessStatusCode, await settled.Content.ReadAsStringAsync());
                var detail = await DetailAsync(approved.Id);
                if (correction) Assert.Equal(5m, detail.Quantity);
                else Assert.Equal(ProductIdentityLifecycleStatus.Retired, detail.Status);
                break;
            }
            default:
            {
                host.Engine.Escalate(instance);
                for (var cycle = 0; cycle < 3; cycle++)
                {
                    // The worker itself, each cycle (the helper above stops at once when its condition already holds).
                    if (correction)
                        await host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.GskuCorrectionRecoveryRunner>(
                            runner => runner.RunCycleAsync());
                    else
                        await host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.GskuRetirementRequestRecoveryRunner>(
                            runner => runner.RunCycleAsync());
                    await Task.Delay(1100);
                }
                var (code, disposition) = await StateOf(approved.Id, correction);
                Assert.Equal("WORKFLOW_DECISION_NOT_TERMINAL", code);
                Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, disposition);
                Assert.DoesNotContain(free, (await DetailAsync(approved.Id)).Actions); // still waiting, lock held

                host.Engine.Approve(instance, host.Approver);
                var applied = await cycles(detail => correction ? detail.Quantity == 5m
                    : detail.Status == ProductIdentityLifecycleStatus.Retired);
                Assert.NotNull(applied);
                break;
            }
        }
    }

    private async Task<(string? Code, ProductIdentityWorkflowRecoveryDisposition Disposition)> StateOf(Guid gsku, bool correction)
    {
        if (correction)
        {
            var operation = await CorrectionOf(gsku);
            return (operation.LastFailureCode, operation.RecoveryDisposition);
        }
        var request = await RetirementOf(gsku);
        return (request.LastFailureCode, request.RecoveryDisposition);
    }

    // E2, decided: the approval was granted while the operation sat in manual reconciliation; the repeat applies it.
    [Fact]
    public async Task E2_The_repeat_applies_a_decision_the_engine_made_during_the_quarantine()
    {
        var (gsku, key) = await QuarantinedPendingAsync("FIRST_GSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT");
        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
        var detail = await RawDetailAsync(gsku);
        Assert.Contains("SUBMIT", detail.GetProperty("availableActions").EnumerateArray().Select(x => x.GetString()));
        var submittedAt = detail.GetProperty("identitySubmitVersion").GetInt32();

        var repeat = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = submittedAt },
            MakerToken(), key);

        Assert.True(repeat.IsSuccessStatusCode, await repeat.Content.ReadAsStringAsync());
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved,
            (await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.IdentityApproved)).Status);
    }

    // E2, open: the approval is still in the Task Center — the repeat puts the operation back to waiting for it.
    [Fact]
    public async Task E2_The_repeat_puts_an_open_approval_back_to_waiting()
    {
        var (gsku, key) = await QuarantinedPendingAsync("FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT");
        var submittedAt = (await IdentityOperationOf(gsku)).ExpectedGskuVersion;

        var repeat = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = submittedAt },
            MakerToken(), key);

        Assert.Equal(HttpStatusCode.Accepted, repeat.StatusCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision, (await IdentityOperationOf(gsku)).Checkpoint);
        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
        await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.IdentityApproved);
    }

    // E2, not settleable: the engine does not know the approval — the repeat changes nothing and says a person has to
    // reconcile it (never "submit again").
    [Fact]
    public async Task E2_A_quarantine_the_repeat_cannot_settle_stays_frozen_and_says_so()
    {
        var (gsku, key) = await QuarantinedPendingAsync("FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT");
        await host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>(
                FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.GskuId == gsku, Builders<FirstGskuIdentityWorkflowOperation>.Update
                .Set(x => x.WorkflowInstanceId, Guid.NewGuid()));
        var submittedAt = (await IdentityOperationOf(gsku)).ExpectedGskuVersion;

        var repeat = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = submittedAt },
            MakerToken(), key);

        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal("FIRST_GSKU_IDENTITY_MANUAL_RECONCILIATION_NEEDED", Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, (await IdentityOperationOf(gsku)).Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await DetailAsync(gsku)).Status);
    }

    // E2, a withdrawal in manual reconciliation (e.g. its requester never came back): the requester's repeat of the
    // same withdrawal reads the engine again and finishes it.
    [Fact]
    public async Task E2_The_requesters_repeat_of_the_same_withdrawal_settles_its_quarantine()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        var pending = await DetailAsync(gsku);
        var key = Guid.NewGuid();
        host.Engine.NextCancelFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        var body = new { expectedGskuVersion = pending.Version, reasonCode = "MAKER_CORRECTION" };
        Assert.False((await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/identity-approval/withdraw", body,
            MakerToken(), key)).IsSuccessStatusCode);
        await QuarantineAsync(gsku, "FIRST_GSKU_IDENTITY_WITHDRAWAL_REQUESTER_NEVER_RETURNED");

        var same = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/identity-approval/withdraw", body,
            MakerToken(), key);

        Assert.True(same.StatusCode == HttpStatusCode.OK, await same.Content.ReadAsStringAsync());
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await DetailAsync(gsku)).Status);
    }

    // CT decision 1: a unit withdrawn from new use AFTER the revision was approved (between the second and the third
    // gate) does not void the approval — the pair is completed with the proof of the first gate, and the screen is told.
    [Fact]
    public async Task D1_A_reference_retired_after_the_approval_is_applied_with_the_submit_time_proof()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
        // Gate 1 (the decision begins) and gate 2 (the revision) read Platform; from the third read on, the unit is
        // no longer selectable.
        host.PlatformReference.NotSelectableFromCall = host.PlatformReference.Calls + 3;
        try
        {
            var approved = await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.IdentityApproved);
            Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, approved.Status);
        }
        finally
        {
            host.PlatformReference.NotSelectableFromCall = null;
        }
        var operation = await IdentityOperationOf(gsku);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.Completed, operation.Checkpoint);
        Assert.Equal("FIRST_GSKU_IDENTITY_REFERENCE_RETIRED_AFTER_APPROVAL", operation.ApprovalAppliedNote);
        Assert.Equal("FIRST_GSKU_IDENTITY_REFERENCE_RETIRED_AFTER_APPROVAL",
            (await RawDetailAsync(gsku)).GetProperty("identityApprovalNote").GetString());
    }

    // A submitted pair whose identity operation is then put in manual reconciliation after the start (as a step that
    // met the given fault would), with the key the submit was sent under.
    private async Task<(Guid Gsku, Guid Key)> QuarantinedPendingAsync(string reason)
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var key = Guid.NewGuid();
        var submit = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = version },
            MakerToken(), key);
        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);
        await QuarantineAsync(gsku, reason);
        return (gsku, key);
    }

    private Task QuarantineAsync(Guid gsku, string reason) =>
        host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>(FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.GskuId == gsku, Builders<FirstGskuIdentityWorkflowOperation>.Update
                .Set(x => x.Checkpoint, FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                .Set(x => x.LastFailureCode, reason)
                .Set(x => x.LeaseOwner, null)
                .Set(x => x.LeaseUntilUtcTicksV1, null)
                .Set(x => x.NextAttemptAtUtcTicksV1, null));

    private Task<FirstGskuIdentityWorkflowOperation> IdentityOperationOf(Guid gsku) =>
        host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>(FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .Find(x => x.GskuId == gsku).SortByDescending(x => x.CreatedAtUtcTicksV1).FirstAsync();

    private async Task<JsonElement> RawDetailAsync(Guid gsku)
    {
        var response = await host.Client(MakerToken()).GetAsync($"/api/gskus/{gsku:D}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").Clone();
    }
}
