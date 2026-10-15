using System.Net;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX2 over HTTP, against the real Api, the real repositories (a throwaway mongod) and — for the
/// approval-time reference check — the real resolver client. One principle: no state without a way out. Each test
/// drives a draft or a lock into a state and then takes its way out.
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    // Item 1 (a): the engine never received the submit → the same submit again goes through.
    [Fact]
    public async Task F1_A_submit_the_engine_never_received_is_submitted_again_with_the_same_submit()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var key = Guid.NewGuid();
        host.Engine.NextStartFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        var first = await SubmitWithAsync(gsku, version, key, MakerToken());
        Assert.False(first.IsSuccessStatusCode);

        var draft = await DetailAsync(gsku);
        Assert.Equal("RESUBMIT_REQUIRED", draft.SubmitState);
        Assert.Contains("SUBMIT", draft.Actions);
        Assert.Contains("EDIT", draft.Actions);

        var again = await SubmitWithAsync(gsku, version, key, MakerToken());
        Assert.Equal(HttpStatusCode.Accepted, again.StatusCode);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await DetailAsync(gsku)).Status);
    }

    // Item 1 (a): … or the draft is edited: the edit sets the unreceived submit aside, and a new one goes through.
    [Fact]
    public async Task F1_An_edit_sets_aside_a_submit_the_engine_never_received()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        host.Engine.NextStartFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        Assert.False((await SubmitAsync(gsku, version)).IsSuccessStatusCode);

        var edit = await SendAsync(HttpMethod.Put, $"/api/gskus/{gsku:D}",
            new { expectedVersion = version, packQuantity = 6m, packUomCode = "C62" }, MakerToken());
        Assert.True(edit.StatusCode == HttpStatusCode.OK, await edit.Content.ReadAsStringAsync());

        var setAside = await host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>(
                "mdm_first_gsku_identity_workflow_operations")
            .Find(x => x.GskuId == gsku && x.ExpectedGskuVersion == version).SingleAsync();
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, setAside.Checkpoint);
        Assert.Equal("FIRST_GSKU_IDENTITY_SUBMIT_REPLACED_BY_EDIT", setAside.LastFailureCode);
        var edited = await DetailAsync(gsku);
        Assert.Null(edited.SubmitState);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, edited.Version)).StatusCode);
    }

    // Item 1 (b) + CT decision C(A): manual reconciliation; the engine holds nothing → the submitter's repeat ends the
    // submit, the draft opens, an edited draft is submitted. Nobody else can settle it; the edit is refused until then.
    [Fact]
    public async Task F1_Manual_reconciliation_is_settled_by_the_submitters_repeat_when_the_engine_holds_nothing()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_START_IDEMPOTENCY_CONFLICT", false);
        Assert.False((await SubmitWithAsync(gsku, version, key, MakerToken())).IsSuccessStatusCode);

        var manual = await DetailAsync(gsku);
        Assert.Equal("RECONCILIATION_REQUIRED", manual.SubmitState);
        Assert.Contains("SUBMIT", manual.Actions);
        Assert.DoesNotContain("EDIT", manual.Actions);
        var edit = await SendAsync(HttpMethod.Put, $"/api/gskus/{gsku:D}",
            new { expectedVersion = version, packQuantity = 6m, packUomCode = "C62" }, MakerToken());
        Assert.Equal("GSKU_IDENTITY_RECONCILIATION_REQUIRED", Envelope.FirstError(await Envelope.ReadAsync(edit)));
        var other = await SubmitWithAsync(gsku, version, key,
            host.Token(_tenant, Guid.NewGuid(), MakerPermissions));
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);

        var settled = await SubmitWithAsync(gsku, version, key, MakerToken());

        Assert.Equal("FIRST_GSKU_IDENTITY_SUBMISSION_ENDED", Envelope.FirstError(await Envelope.ReadAsync(settled)));
        var ended = await DetailAsync(gsku);
        Assert.Equal("SUBMISSION_ENDED", ended.SubmitState);
        Assert.Contains("EDIT", ended.Actions);
        Assert.DoesNotContain("SUBMIT", ended.Actions);
        var reEdit = await SendAsync(HttpMethod.Put, $"/api/gskus/{gsku:D}",
            new { expectedVersion = version, packQuantity = 6m, packUomCode = "C62" }, MakerToken());
        Assert.Equal(HttpStatusCode.OK, reEdit.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version + 1)).StatusCode);
    }

    // Item 1 (b) + C(A): manual reconciliation; the engine DOES hold the start → the repeat carries the submit on.
    [Fact]
    public async Task F1_Manual_reconciliation_goes_on_when_the_engine_holds_the_start()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_START_IDEMPOTENCY_CONFLICT", true);
        Assert.False((await SubmitWithAsync(gsku, version, key, MakerToken())).IsSuccessStatusCode);
        Assert.Equal("RECONCILIATION_REQUIRED", (await DetailAsync(gsku)).SubmitState);

        var settled = await SubmitWithAsync(gsku, version, key, MakerToken());

        Assert.Equal(HttpStatusCode.Accepted, settled.StatusCode);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await DetailAsync(gsku)).Status);
        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
        await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.IdentityApproved);
    }

    // Item 1 (b): the engine refused the start before processing it → not reconciliation: the submit ends, the draft
    // opens for an edit (the same submit cannot be repeated at this version).
    [Fact]
    public async Task F1_A_start_the_engine_refused_before_processing_ends_and_opens_the_draft()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Invalid, "VALIDATION_FAILED", false);

        var submit = await SubmitAsync(gsku, version);

        Assert.Equal("FIRST_GSKU_IDENTITY_SUBMISSION_ENDED", Envelope.FirstError(await Envelope.ReadAsync(submit)));
        var ended = await DetailAsync(gsku);
        Assert.Equal("SUBMISSION_ENDED", ended.SubmitState);
        Assert.Contains("EDIT", ended.Actions);
    }

    // Item 2, through the REAL resolver client: the unit was retired between submit and decision → the approval is not
    // applicable and the pair is a draft again with the reason (FIX1 quarantined it in production).
    [Fact]
    public async Task F2_An_approval_whose_unit_was_retired_meanwhile_returns_the_pair_to_draft()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        host.PlatformReference.Retired["C62"] = true;
        try
        {
            host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
            var draft = await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.Draft);

            Assert.Equal("REFERENCE_NOT_SELECTABLE", draft.NotAppliedReason);
            Assert.Contains("EDIT", draft.Actions);
        }
        finally
        {
            host.PlatformReference.Retired.TryRemove("C62", out _);
        }
    }

    // Item 3: the maker decided their own approval → not applicable; the pair is a draft again with the reason.
    [Fact]
    public async Task F3_A_decision_by_the_maker_returns_the_pair_to_draft()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), _maker);

        var draft = await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.Draft);

        Assert.Equal("FIRST_GSKU_IDENTITY_SELF_APPROVAL_IGNORED", draft.NotAppliedReason);
        Assert.Contains("SUBMIT", draft.Actions);
    }

    // Item 3 on a correction: the maker's own decision ends it unapplied and frees the GSKU.
    [Fact]
    public async Task F3_A_correction_decided_by_its_maker_frees_the_gsku()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 3m)).IsSuccessStatusCode);
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), _maker);

        var freed = await CorrectionCyclesUntilAsync(approved.Id, detail => detail.Actions.Contains("REQUEST_CORRECTION"));

        Assert.Equal(1m, freed.Quantity);
    }

    // Item 4: the engine's start answer could not be read — it may hold an approval: the lock stays, the worker finds
    // the start, and the decision is applied (FIX1 released the lock and the approval was orphaned).
    [Fact]
    public async Task F4_An_unreadable_start_answer_keeps_the_lock_and_the_decision_still_applies()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Invalid,
            ProductIdentityWorkflowFaultPolicy.ResponseUnreadable, true);
        Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        var locked = await DetailAsync(approved.Id);
        Assert.DoesNotContain("REQUEST_CORRECTION", locked.Actions);
        Assert.DoesNotContain("REQUEST_RETIREMENT", locked.Actions);

        await CorrectionCyclesUntilAsync(approved.Id, _ => host.Engine.Instances("GskuCorrection")
            .Any(x => x.ApprovedBy is null && !x.Cancelled && !x.ClosedWithoutDecision));
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);
        var applied = await CorrectionCyclesUntilAsync(approved.Id, detail => detail.Quantity == 4m);

        Assert.Contains("REQUEST_CORRECTION", applied.Actions);
    }

    // Item 5 (a): an administrator closes the correction's approval without a decision → the GSKU is freed.
    [Fact]
    public async Task F5_A_correction_closed_without_a_decision_frees_the_gsku()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 5m)).IsSuccessStatusCode);
        host.Engine.CloseWithoutDecision(host.Engine.Latest("GskuCorrection"));

        var freed = await CorrectionCyclesUntilAsync(approved.Id, detail => detail.Actions.Contains("REQUEST_CORRECTION"));

        Assert.Equal(1m, freed.Quantity);
    }

    // Item 5 (b): no approval definition → nothing started; the GSKU is freed at once and the requester told why.
    [Fact]
    public async Task F5_A_correction_whose_definition_is_missing_frees_the_gsku_and_says_so()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.NotFound, "NOT_FOUND_NON_LEAKAGE", false);

        var refused = await RequestCorrectionAsync(approved.Id, approved.Version, 5m);

        Assert.Equal(ProductIdentityWorkflowMakerCodes.DefinitionNotReady, Envelope.FirstError(await Envelope.ReadAsync(refused)));
        Assert.Contains("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);
    }

    // Item 8: a withdrawal whose cancellation timed out finishes with the same request repeated; another request
    // (another text, another command) is refused and told the command the first one stands under.
    [Fact]
    public async Task F8_A_withdrawal_that_timed_out_finishes_on_the_same_request_and_another_is_told_the_pending_one()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        var pending = await DetailAsync(gsku);
        var key = Guid.NewGuid();
        host.Engine.NextCancelFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        var body = new { expectedGskuVersion = pending.Version, reasonCode = "MAKER_CORRECTION" };
        Assert.False((await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/identity-approval/withdraw", body,
            MakerToken(), key)).IsSuccessStatusCode);

        var other = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/identity-approval/withdraw",
            new { expectedGskuVersion = pending.Version, reasonCode = "OTHER_REASON" }, MakerToken());
        var otherBody = await Envelope.ReadAsync(other);
        Assert.Equal("FIRST_GSKU_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT", Envelope.FirstError(otherBody));
        Assert.Equal(key, otherBody.GetProperty("data").GetProperty("pendingWithdrawalCommandId").GetGuid());

        var same = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/identity-approval/withdraw", body,
            MakerToken(), key);
        Assert.True(same.StatusCode == HttpStatusCode.OK, await same.Content.ReadAsStringAsync());
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await DetailAsync(gsku)).Status);
    }

    // Item 13: direct retirement is closed for every caller — a non-human token is refused like a person (FIX1 left a
    // "system actor passes" branch that could never be reached and was never tested).
    [Theory]
    [InlineData("tenant_user")]
    [InlineData("service_client")]
    [InlineData("system")]
    public async Task F13_Direct_retirement_is_refused_to_every_caller(string actorType)
    {
        var approved = await ApprovedGskuAsync();

        var response = await SendAsync(HttpMethod.Post, $"/api/gskus/{approved.Id:D}/retire",
            new { expectedVersion = approved.Version, reasonCode = "X" },
            host.TokenOf(actorType, _tenant, Guid.NewGuid(), "mdm.gskus.read", "mdm.gskus.retire"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("GSKU_DIRECT_RETIREMENT_SYSTEM_ONLY", Envelope.FirstError(await Envelope.ReadAsync(response)));
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await DetailAsync(approved.Id)).Status);
    }

    private Task<HttpResponseMessage> SubmitWithAsync(Guid gsku, int version, Guid key, string token) =>
        SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = version }, token, key);

    private Task<HttpResponseMessage> RequestCorrectionAsync(Guid gsku, int version, decimal quantity) =>
        PostAsync($"/api/gskus/{gsku:D}/correction-requests",
            new { expectedGskuVersion = version, packQuantity = quantity, packUomCode = "C62" });

    private Task<Detail> CorrectionCyclesUntilAsync(Guid gsku, Func<Detail, bool> done) =>
        CyclesUntilAsync(gsku, done,
            () => host.RunAsync<GskuCorrectionRecoveryRunner>(runner => runner.RunCycleAsync()));
}
