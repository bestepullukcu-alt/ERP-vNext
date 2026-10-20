using System.Text.Json;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Commands;
using Diten.CrmService.Application.Features.Commercial.PreOrders.Queries;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 §4.4 / §4.5 / §4.8 / §21: the one-time signed-in link (hash only, single use, TTL, dies with
/// its submission), the Work Center outcome (event + reconcile-on-read, the SoD never skipped), visibility (own / team /
/// approver / read-all, everything else 404), the tenant settings and one audit record per write command.
/// </summary>
public sealed class PreOrderApprovalTests : PreOrderTestBase
{
    // ── the link (D3, D4) ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task K1_the_token_is_stored_only_as_its_hash_and_resolves_for_its_signed_in_approver_only()
    {
        var pending = await Pending();
        var token = W.Notifier.LastToken!;
        var link = Assert.Single(W.Links.Store);

        Assert.Equal(PreOrderApprovalService.HashToken(token), link.TokenHash);
        Assert.Matches("^[0-9a-f]{64}$", link.TokenHash);
        Assert.DoesNotContain(token, JsonSerializer.Serialize(link));
        Assert.Equal(W.Clock.Now.AddHours(48), link.ExpiresAt);

        var mine = Ok(await W.Send(new ResolvePreOrderApprovalLinkQuery(token), PreOrderWorld.Manager2));
        var notMine = await W.Send(new ResolvePreOrderApprovalLinkQuery(token), PreOrderWorld.Stranger, PreOrderPermissions.Approve);
        var unknown = await W.Send(new ResolvePreOrderApprovalLinkQuery("not-a-token"), PreOrderWorld.Manager);

        Assert.Equal((PreOrderApprovalLinkStates.Valid, pending.Id), (mine.State, mine.PreOrder!.Id));
        Assert.True(mine.PreOrder.Can.Decide);
        Assert.Equal(404, notMine.StatusCode);
        Assert.Equal(404, unknown.StatusCode);
    }

    [Fact]
    public async Task K2_a_link_decides_once_its_second_use_is_refused_and_it_then_reads_as_consumed()
    {
        var pending = await Pending();
        var token = W.Notifier.LastToken!;

        var approved = Ok(await Decide(pending.Id, PreOrderDecisions.Approve, token: token));
        var again = await Decide(pending.Id, PreOrderDecisions.Approve, by: PreOrderWorld.Manager2, token: token);
        var state = Ok(await W.Send(new ResolvePreOrderApprovalLinkQuery(token), PreOrderWorld.Manager));

        Assert.Equal((PreOrderApprovalChannels.EmailLink, PreOrderStatuses.Transmitted), (approved.Approval!.Channel, approved.Status));
        Assert.Equal(409, again.StatusCode);
        Assert.Equal((PreOrderApprovalLinkStates.Consumed, (PreOrderDto?)null), (state.State, state.PreOrder));
        Assert.Equal(PreOrderWorld.Manager, W.Links.Store.Single().ConsumedByUserId);
    }

    [Fact]
    public async Task K2b_two_approvers_racing_on_one_link_have_exactly_one_winner()
    {
        var pending = await Pending();
        var token = W.Notifier.LastToken!;
        var link = W.Links.Store.Single();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            W.Links.TryConsumeAsync(PreOrderWorld.Tenant, link.Id, i % 2 == 0 ? PreOrderWorld.Manager : PreOrderWorld.Manager2, W.Clock.Now, default)));

        Assert.Single(results, r => r);
        var refused = await Decide(pending.Id, PreOrderDecisions.Approve, token: token);
        Assert.Equal((PreOrderErrors.LinkUnusable, 409), (Code(refused), refused.StatusCode));
    }

    [Fact]
    public async Task K3_an_expired_link_shows_its_state_and_writes_no_decision()
    {
        var pending = await Pending();
        var token = W.Notifier.LastToken!;
        W.Clock.Now = W.Clock.Now.AddHours(49);

        var state = Ok(await W.Send(new ResolvePreOrderApprovalLinkQuery(token), PreOrderWorld.Manager));
        var decision = await Decide(pending.Id, PreOrderDecisions.Approve, token: token);

        Assert.Equal(PreOrderApprovalLinkStates.Expired, state.State);
        Assert.Equal((PreOrderErrors.LinkUnusable, 409), (Code(decision), decision.StatusCode));
        Assert.Equal(PreOrderStatuses.PendingApproval, W.Stored(pending.Id).Status);
        Assert.Null(W.Links.Store.Single().ConsumedAt);
    }

    [Fact]
    public async Task K4_a_change_request_voids_the_link_and_the_next_submission_has_a_new_one()
    {
        var pending = await Pending();
        var first = W.Notifier.LastToken!;
        await Decide(pending.Id, PreOrderDecisions.RequestChanges, "adet yanlış");
        await Sign(pending.Id);
        await Submit(pending.Id);
        var second = W.Notifier.LastToken!;

        Assert.Equal(PreOrderApprovalLinkStates.Superseded, Ok(await W.Send(new ResolvePreOrderApprovalLinkQuery(first), PreOrderWorld.Manager)).State);
        Assert.Equal(PreOrderApprovalLinkStates.Valid, Ok(await W.Send(new ResolvePreOrderApprovalLinkQuery(second), PreOrderWorld.Manager)).State);
        Assert.Equal(409, (await Decide(pending.Id, PreOrderDecisions.Approve, token: first)).StatusCode);
    }

    [Fact]
    public async Task K5_the_link_lifetime_is_the_tenant_setting()
    {
        W.Settings.Store.Single().ApprovalLinkTtlHours = 12;
        await Pending();

        Assert.Equal(W.Clock.Now.AddHours(12), W.Links.Store.Single().ExpiresAt);
    }

    // ── the Work Center outcome (event / reconcile) ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task E1_a_Work_Center_approval_lands_once_an_approval_by_the_rep_or_by_nobody_known_is_not_applied()
    {
        var a = await Pending();
        var b = await Pending();
        var c = await Pending();
        var approval = W.Get<PreOrderApprovalService>();

        var applied = await approval.ApplyOutcomeAsync(PreOrderWorld.Tenant, a.Id, a.Approval!.WorkflowInstanceId!.Value, ClaimReviewOutcomes.Approved,
            PreOrderWorld.Manager2.ToString("D"), null, W.Clock.Now, default);
        var replay = await approval.ApplyOutcomeAsync(PreOrderWorld.Tenant, a.Id, a.Approval.WorkflowInstanceId.Value, ClaimReviewOutcomes.Rejected,
            PreOrderWorld.Manager.ToString("D"), null, W.Clock.Now, default);
        var byRep = await approval.ApplyOutcomeAsync(PreOrderWorld.Tenant, b.Id, b.Approval!.WorkflowInstanceId!.Value, ClaimReviewOutcomes.Approved,
            PreOrderWorld.Rep.ToString("D"), null, W.Clock.Now, default);
        var wrongInstance = await approval.ApplyOutcomeAsync(PreOrderWorld.Tenant, c.Id, Guid.NewGuid(), ClaimReviewOutcomes.Rejected, null, null,
            W.Clock.Now, default);

        Assert.Equal((ClaimReviewApplyResult.Applied, PreOrderStatuses.Transmitted), (applied, W.Stored(a.Id).Status));
        Assert.Equal(ClaimReviewApplyResult.NoMatchingOpenRound, replay);
        Assert.Equal((ClaimReviewApplyResult.NoMatchingOpenRound, PreOrderStatuses.PendingApproval), (byRep, W.Stored(b.Id).Status));
        Assert.Equal((ClaimReviewApplyResult.NotFound, PreOrderStatuses.PendingApproval), (wrongInstance, W.Stored(c.Id).Status));
    }

    [Fact]
    public async Task E2_a_Work_Center_rejection_with_the_change_request_code_is_a_change_request_and_a_plain_one_rejects()
    {
        var a = await Pending();
        var b = await Pending();
        var approval = W.Get<PreOrderApprovalService>();

        await approval.ApplyOutcomeAsync(PreOrderWorld.Tenant, a.Id, a.Approval!.WorkflowInstanceId!.Value, ClaimReviewOutcomes.Rejected,
            PreOrderWorld.Manager.ToString("D"), PreOrderApprovalService.ChangesRequestedReason, W.Clock.Now, default);
        await approval.ApplyOutcomeAsync(PreOrderWorld.Tenant, b.Id, b.Approval!.WorkflowInstanceId!.Value, ClaimReviewOutcomes.Rejected,
            PreOrderWorld.Manager.ToString("D"), "COMMERCIAL_PRE_ORDER_REJECTED", W.Clock.Now, default);

        Assert.Equal((PreOrderStatuses.Draft, (PreOrderSignature?)null), (W.Stored(a.Id).Status, W.Stored(a.Id).Signature));
        Assert.Equal(PreOrderStatuses.Rejected, W.Stored(b.Id).Status);
    }

    [Fact]
    public async Task E3_with_the_event_bus_off_a_read_after_the_cut_off_takes_MOD_0023_s_outcome()
    {
        var pending = await Pending();
        var instance = pending.Approval!.WorkflowInstanceId!.Value;
        var task = W.Workflow.Tasks.Single(t => t.WorkflowInstanceId == instance);
        W.Workflow.Tasks.Remove(task);
        W.Workflow.Tasks.Add(task with { Status = "Approved", ActionedBy = PreOrderWorld.Manager2.ToString("D"), CompletedAt = W.Clock.Now });
        W.Workflow.Complete(instance, ClaimReviewOutcomes.Approved);

        var early = Ok(await W.Send(new GetPreOrderByIdQuery(pending.Id), PreOrderWorld.Rep));
        W.Clock.Now = W.Clock.Now.AddMinutes(5);
        var late = Ok(await W.Send(new GetPreOrderByIdQuery(pending.Id), PreOrderWorld.Rep));

        Assert.Equal(PreOrderStatuses.PendingApproval, early.Status);              // inside the cut-off: no MOD-0023 call
        Assert.Equal((PreOrderStatuses.Transmitted, PreOrderWorld.Manager2), (late.Status, late.Approval!.DecidedByUserId!.Value));
    }

    // ── visibility (§4.8) ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task V1_the_rep_sees_their_own_the_team_lead_and_the_approver_see_it_read_all_sees_every_one_and_the_rest_get_404()
    {
        var mine = await Pending();
        var draftOfOther = await Draft(PreOrderWorld.OtherRep);

        Assert.True((await W.Send(new GetPreOrderByIdQuery(mine.Id), PreOrderWorld.Rep)).IsSuccessful);
        Assert.True((await W.Send(new GetPreOrderByIdQuery(mine.Id), PreOrderWorld.Manager)).IsSuccessful);      // team lead (and approver)
        Assert.True((await W.Send(new GetPreOrderByIdQuery(mine.Id), PreOrderWorld.Manager2)).IsSuccessful);     // approver only
        Assert.True((await W.Send(new GetPreOrderByIdQuery(draftOfOther.Id), PreOrderWorld.Ops)).IsSuccessful);
        Assert.Equal(404, (await W.Send(new GetPreOrderByIdQuery(mine.Id), PreOrderWorld.OtherRep)).StatusCode);
        Assert.Equal(404, (await W.Send(new GetPreOrderByIdQuery(draftOfOther.Id), PreOrderWorld.Manager)).StatusCode);

        var repList = Ok(await W.Send(new GetPreOrderListQuery(null, null, null, null, null, null, null, null, null, false, null, null, null), PreOrderWorld.Rep));
        var opsList = Ok(await W.Send(new GetPreOrderListQuery(null, null, null, null, null, null, null, null, null, false, null, null, null), PreOrderWorld.Ops));
        var awaiting = Ok(await W.Send(new GetPreOrderListQuery(null, null, null, null, null, null, null, null, null, true, null, null, null), PreOrderWorld.Manager2));
        Assert.Equal(new[] { mine.Id }, repList.Items.Select(i => i.Id));
        Assert.Equal(2, opsList.Total);
        Assert.Equal((mine.Id, true), (Assert.Single(awaiting.Items).Id, awaiting.Items.Single().AwaitingMe));
    }

    [Fact]
    public async Task V2_another_tenant_s_order_is_404_for_every_read_and_write()
    {
        var pending = await Pending();
        W.Orders.Store.Single().TenantId = PreOrderWorld.OtherTenant;

        Assert.Equal(404, (await W.Send(new GetPreOrderByIdQuery(pending.Id), PreOrderWorld.Ops)).StatusCode);
        Assert.Equal(404, (await Decide(pending.Id, PreOrderDecisions.Approve)).StatusCode);
        Assert.Equal(404, (await W.Send(new CancelPreOrderCommand(pending.Id, new PreOrderCancelInput("x")), PreOrderWorld.Rep)).StatusCode);
        Assert.Equal(404, (await W.Send(new CopyPreOrderCommand(pending.Id), PreOrderWorld.Rep)).StatusCode);
        Assert.Equal(404, (await W.Send(new GetPharmacyOrderHistoryQuery(pending.Id), PreOrderWorld.Ops)).StatusCode);
        Assert.Empty(Ok(await W.Send(new GetPreOrderListQuery(null, null, null, null, null, null, null, null, null, false, null, null, null), PreOrderWorld.Ops)).Items);
    }

    [Fact]
    public async Task V3_the_pharmacy_history_is_for_the_order_s_approver_or_read_all_and_counts_only_submitted_orders()
    {
        var pending = await Pending();
        await Draft();                                                               // a draft is not history

        var forApprover = Ok(await W.Send(new GetPharmacyOrderHistoryQuery(pending.Id), PreOrderWorld.Manager2));
        var forRep = await W.Send(new GetPharmacyOrderHistoryQuery(pending.Id), PreOrderWorld.Rep);

        Assert.Equal(pending.Id, Assert.Single(forApprover.Items).Id);
        Assert.Equal(404, forRep.StatusCode);
    }

    // ── settings ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("mode")]
    [InlineData("ttl")]
    [InlineData("retries")]
    [InlineData("tier")]
    [InlineData("prefix")]
    [InlineData("prefix-twice")]
    public async Task S1_bad_settings_are_400(string what)
    {
        var input = what switch
        {
            "mode" => new PreOrderSettingsInput("sometimes", null, null, null, null),
            "ttl" => new PreOrderSettingsInput(null, 11, null, null, null),
            "retries" => new PreOrderSettingsInput(null, null, null, 11, null),
            "tier" => new PreOrderSettingsInput(null, null, [new PreOrderCountryTierInput("TR", "wholesale")], null, null),
            "prefix" => new PreOrderSettingsInput(null, null, null, null, [new PreOrderCompanyPrefixInput(PreOrderWorld.LeTr, "mg1")]),
            _ => new PreOrderSettingsInput(null, null, null, null,
                [new PreOrderCompanyPrefixInput(PreOrderWorld.LeTr, "MG"), new PreOrderCompanyPrefixInput(PreOrderWorld.LeAe, "MG")])
        };

        var response = await W.Send(new UpdatePreOrderSettingsCommand(input), PreOrderWorld.Ops);
        Assert.Equal((PreOrderErrors.SettingsInvalid, 400), (Code(response), response.StatusCode));
    }

    [Fact]
    public async Task S2_a_prefix_that_numbered_an_order_is_locked_and_the_country_tier_drives_the_price()
    {
        await Draft();                                                               // MG numbered MG-ORD-000001

        var change = await W.Send(new UpdatePreOrderSettingsCommand(new PreOrderSettingsInput(null, null, null, null,
            [new PreOrderCompanyPrefixInput(PreOrderWorld.LeTr, "GMTR"), new PreOrderCompanyPrefixInput(PreOrderWorld.LeAe, "GMAE"),
                new PreOrderCompanyPrefixInput(PreOrderWorld.LeKg, "GMKG")])), PreOrderWorld.Ops);
        var tier = Ok(await W.Send(new UpdatePreOrderSettingsCommand(new PreOrderSettingsInput("auto-within-limits", 24,
            [new PreOrderCountryTierInput("tr", "depot")], 3, null, 1)), PreOrderWorld.Ops));
        var stale = await W.Send(new UpdatePreOrderSettingsCommand(new PreOrderSettingsInput(null, 30, null, null, null, 1)), PreOrderWorld.Ops);
        var draft = await Draft(input: Input(lines: Line("TUT", 20)));
        await Sign(draft.Id);
        var priced = Ok(await Submit(draft.Id));                                  // within limits + auto mode → straight through

        Assert.Equal((PreOrderErrors.PrefixLocked, 409), (Code(change), change.StatusCode));
        Assert.Equal(("auto-within-limits", 24, "TR", "depot", 2), (tier.ApprovalMode, tier.ApprovalLinkTtlHours, tier.PriceTierByCountry.Single().CountryCode,
            tier.PriceTierByCountry.Single().Tier, tier.Version));
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal((356.20m, PriceTiers.Depot, PreOrderStatuses.Transmitted), (priced.Lines.Single().UnitPrice, priced.Price!.Tier, priced.Status));
    }

    [Fact]
    public async Task S3_without_a_stored_document_the_defaults_answer()
    {
        W.Settings.Store.Clear();
        var settings = Ok(await W.Send(new GetPreOrderSettingsQuery(), PreOrderWorld.Ops));

        Assert.Equal(("always", 48, 5, true, 0), (settings.ApprovalMode, settings.ApprovalLinkTtlHours, settings.TransmissionRetryMax, settings.IsDefault,
            settings.CompanyPrefixes.Count));
    }

    // ── audit (§21) ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A1_every_write_command_is_audited_once_reads_never_and_no_token_signature_or_e_mail_reaches_the_record()
    {
        var draft = await Draft();                                                                         // 1 create
        await W.Send(new UpdatePreOrderCommand(draft.Id, Input()));                                       // 2 update
        await Sign(draft.Id);                                                                              // 3 signature
        await W.Send(new GetPreOrderByIdQuery(draft.Id));                                                  // read
        await Submit(draft.Id);                                                                            // 4 submit
        var token = W.Notifier.LastToken!;
        await W.Send(new ResolvePreOrderApprovalLinkQuery(token), PreOrderWorld.Manager);                  // read
        await Decide(draft.Id, PreOrderDecisions.Reject, "hayır", token: token);                           // 5 decide
        var copy = Ok(await W.Send(new CopyPreOrderCommand(draft.Id), PreOrderWorld.Rep));                // 6 copy
        await W.Send(new DiscardPreOrderCommand(copy.Id, new PreOrderVersionInput()));                     // 7 discard
        await W.Send(new UpdatePreOrderSettingsCommand(new PreOrderSettingsInput(null, 24, null, null, null)), PreOrderWorld.Ops); // 8 settings

        var types = W.Audit.Bodies.Select(b => JsonDocument.Parse(b).RootElement.GetProperty("requestType").GetString()).ToList();
        Assert.Equal(new[] { "CreatePreOrderCommand", "UpdatePreOrderCommand", "CapturePreOrderSignatureCommand", "SubmitPreOrderCommand",
            "DecidePreOrderCommand", "CopyPreOrderCommand", "DiscardPreOrderCommand", "UpdatePreOrderSettingsCommand" }, types);
        Assert.All(W.Audit.Bodies, b =>
        {
            Assert.Contains("MOD-0168", b);
            Assert.DoesNotContain(token, b);
            Assert.DoesNotContain(PreOrderApprovalService.HashToken(token), b);
            Assert.DoesNotContain(Png[..20], b);
            Assert.DoesNotContain("siparis@selcuk.test", b);
        });
    }
}
