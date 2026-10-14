using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Application.Features.CompetitorProducts;
using Diten.MdmService.Domain.Entities;
using Xunit;
using static Diten.MdmService.Application.Tests.CompetitorProducts.CompetitorHarness;

namespace Diten.MdmService.Application.Tests.CompetitorProducts;

/// <summary>
/// WP-VW-W4 (W4-BE-b2) — "W4-BE-b2" Acceptance on the REAL service + the REAL b1 approval flow / applier / consumer:
/// uniqueness (normalized), a duplicate market → 400, a price change → history, a proposal visible only to its proposer
/// (includeProposed=mine), complete → active, merge → MergedIntoId + alias + market move, reject, SoD, merged read → the
/// survivor, an inactive record is never selectable, a direct edit / merge needs no approval, tenant isolation.
/// </summary>
public sealed class CompetitorProductAcceptanceTests
{
    private const string RepA = "u-rep-a";
    private const string RepB = "u-rep-b";
    private const string Reviewer = "u-reviewer";

    // ── uniqueness / shape ──

    [Fact]
    public async Task Company_and_name_are_unique_after_Turkish_folding_and_spacing()
    {
        var h = new CompetitorHarness();
        await h.CreateAsync("Vastarel MR", "Servier İlaç");

        var dup = await h.Service.CreateAsync(Write("  vastarel  mr ", "SERVİER ILAÇ"), default);

        Assert.False(dup.IsSuccessful);
        Assert.Equal(409, dup.StatusCode);
        Assert.Equal(CompetitorErrors.Duplicate, dup.Errors[0]);
        Assert.Equal(CompetitorNames.Key("Servier İlaç", "Vastarel MR"), CompetitorNames.Key("SERVIER ilac", "vastarel-mr"));
        Assert.Single(h.Products.Raw);
    }

    [Fact]
    public async Task A_market_twice_is_400_and_nothing_is_saved()
    {
        var h = new CompetitorHarness();

        var result = await h.Service.CreateAsync(Write("Vastarel MR", "Servier", Price("TR", 100), Price("tr", 120)), default);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(CompetitorErrors.MarketDuplicate, result.Errors[0]);
        Assert.Empty(h.Products.Raw);
    }

    [Fact]
    public async Task An_unknown_market_or_reimbursement_is_400_and_an_unreadable_set_is_503()
    {
        var h = new CompetitorHarness();

        var market = await h.Service.CreateAsync(Write("A", "B", Price("XX", 1)), default);
        var reimbursement = await h.Service.CreateAsync(Write("A", "B", Price("TR", 1, reimbursement: "free")), default);
        h.ReferenceSets.Down = true;
        var down = await h.Service.CreateAsync(Write("A", "B", Price("TR", 1)), default);

        Assert.Equal(400, market.StatusCode);
        Assert.Equal(CompetitorErrors.MarketInvalid, market.Errors[0]);
        Assert.Equal(400, reimbursement.StatusCode);
        Assert.Equal(CompetitorErrors.ReimbursementInvalid, reimbursement.Errors[0]);
        Assert.Equal(503, down.StatusCode);
        Assert.Empty(h.Products.Raw);
    }

    // ── direct edit: price history, no approval ──

    [Fact]
    public async Task A_price_change_is_a_new_period_and_the_old_one_goes_to_history_without_any_approval()
    {
        var h = new CompetitorHarness();
        var id = await h.CreateAsync("Vastarel MR", "Servier", Price("TR", 100, from: "2026-01-01"), Price("AE", 50, "AED"));

        var updated = await h.Service.UpdateAsync(id, Write("Vastarel MR", "Servier",
            Price("TR", 130, reimbursement: "reimbursed_conditional", from: "2026-07-01")) with { ExpectedVersion = 0 }, default);

        Assert.True(updated.IsSuccessful, string.Join(";", updated.Errors));
        var row = Assert.Single(updated.Data!.CountryPrices);
        Assert.Equal(130m, row.BoxPrice);
        var changed = Assert.Single(updated.Data.CountryPriceHistory, x => x.MarketCode == "TR");
        Assert.Equal(100m, changed.BoxPrice);
        Assert.Equal("reimbursed", changed.ReimbursementStatusCode);
        Assert.Equal(new DateTime(2026, 7, 1), changed.ValidTo);
        Assert.Equal("changed", changed.Reason);
        Assert.Equal("removed", Assert.Single(updated.Data.CountryPriceHistory, x => x.MarketCode == "AE").Reason);
        Assert.Contains(updated.Data.ChangeLog, c => c.Field == "countryPrices.TR");
        Assert.Empty(h.Workflow.Starts);
    }

    [Fact]
    public async Task A_stale_version_is_409()
    {
        var h = new CompetitorHarness();
        var id = await h.CreateAsync("Vastarel MR", "Servier");

        var stale = await h.Service.UpdateAsync(id, Write("Vastarel", "Servier") with { ExpectedVersion = 7 }, default);

        Assert.Equal(409, stale.StatusCode);
        Assert.Equal(CompetitorErrors.ConcurrencyConflict, stale.Errors[0]);
    }

    // ── proposals: visible only to the proposer ──

    [Fact]
    public async Task A_proposal_is_visible_only_to_its_proposer_with_includeProposed_mine()
    {
        var h = new CompetitorHarness();
        await h.CreateAsync("Vastarel MR", "Servier");
        h.As(RepA, CompetitorPermissions.Read, CompetitorPermissions.Propose);
        var proposed = await h.Service.ProposeAsync(new CompetitorProposalRequest("Kardiyomet", "Abdi İbrahim", Tutukon, Guid.NewGuid()), default);
        Assert.Equal(201, proposed.StatusCode);
        Assert.False(proposed.Data!.Existing);
        var id = proposed.Data.Id;

        var mineList = await h.Service.ListAsync(null, null, null, null, null, null, null, "mine", null, null, default);
        var mineLookup = await h.Service.LookupAsync(Tutukon, null, null, null, default);
        var mineGet = await h.Service.GetAsync(id, default);
        var plainList = await h.Service.ListAsync(null, null, null, null, null, null, null, null, null, null, default);

        h.As(RepB, CompetitorPermissions.Read, CompetitorPermissions.Propose);
        var otherList = await h.Service.ListAsync(null, null, null, null, null, null, null, "mine", null, null, default);
        var otherLookup = await h.Service.LookupAsync(Tutukon, null, null, null, default);
        var otherGet = await h.Service.GetAsync(id, default);
        var askedProposedStatus = await h.Service.ListAsync(null, null, null, null, null, "proposed,active", null, null, null, null, default);

        Assert.Contains(mineList.Data!.Items, r => r.Id == id);
        Assert.Contains(mineLookup.Data!, r => r.Id == id && r.IsOwnProposal);
        Assert.True(mineGet.IsSuccessful);
        Assert.DoesNotContain(plainList.Data!.Items, r => r.Id == id);
        Assert.DoesNotContain(otherList.Data!.Items, r => r.Id == id);
        Assert.DoesNotContain(otherLookup.Data!, r => r.Id == id);
        Assert.DoesNotContain(askedProposedStatus.Data!.Items, r => r.Id == id);
        Assert.Equal(404, otherGet.StatusCode);
        Assert.Single(otherList.Data.Items);
    }

    [Fact]
    public async Task Proposing_an_existing_name_returns_that_record_and_creates_nothing()
    {
        var h = new CompetitorHarness();
        var existing = await h.CreateAsync("Vastarel MR", "Servier");
        h.As(RepA, CompetitorPermissions.Read, CompetitorPermissions.Propose);

        var again = await h.Service.ProposeAsync(new CompetitorProposalRequest("VASTAREL mr", "servier", Tutukon, null), default);

        Assert.True(again.Data!.Existing);
        Assert.Equal(existing, again.Data.Id);
        Assert.Single(h.Products.Raw);
    }

    // ── the decision via the workflow ──

    private static async Task<(Guid ProposalId, Guid DecisionId, Guid InstanceId)> ProposeAndSubmitAsync(CompetitorHarness h,
        CompetitorDecisionRequest decision, string name = "Kardiyomet", string company = "Abdi İbrahim")
    {
        h.As(RepA, CompetitorPermissions.Read, CompetitorPermissions.Propose);
        var proposal = (await h.Service.ProposeAsync(new CompetitorProposalRequest(name, company, Tutukon, Guid.NewGuid()), default)).Data!.Id;
        h.As("u-manager", [.. CompetitorPermissions.All]);
        var saved = await h.Service.SaveDecisionAsync(proposal, decision, default);
        Assert.True(saved.IsSuccessful, string.Join(";", saved.Errors));
        var instance = Guid.NewGuid();
        h.Workflow.NextInstanceId = instance;
        var submitted = await h.Service.SubmitDecisionAsync(proposal, default);
        Assert.True(submitted.IsSuccessful, string.Join(";", submitted.Errors));
        Assert.Equal(CompetitorDecisionStatuses.InReview, submitted.Data!.Status);
        return (proposal, saved.Data!.Id, instance);
    }

    [Fact]
    public async Task Complete_turns_the_proposal_active_with_the_completed_fields_after_the_approval_event()
    {
        var h = new CompetitorHarness();
        var (proposal, decision, instance) = await ProposeAndSubmitAsync(h, new CompetitorDecisionRequest("complete",
            Write("Kardiyomet", "Abdi İbrahim", Price("TR", 88)), null, null, null));

        Assert.Equal(CompetitorProductStatuses.Proposed, (await h.Products.GetAsync(Tenant, proposal, default))!.Status);
        var start = Assert.Single(h.Workflow.Starts);
        Assert.Equal("mdm.competitor-proposal", start.ObjectType);
        Assert.Equal("MDM-COMP-PROPOSAL", start.TemplateCode);

        await h.OutcomeAsync(decision, instance, WorkflowApprovalOutcomes.Approved, Reviewer);

        var record = (await h.Products.GetAsync(Tenant, proposal, default))!;
        Assert.Equal(CompetitorProductStatuses.Active, record.Status);
        Assert.Equal("TR", Assert.Single(record.CountryPrices).MarketCode);
        Assert.Equal(["trimetazidine"], record.ActiveIngredients);
        Assert.Equal(CompetitorDecisionStatuses.Applied, (await h.Decisions.GetAsync(Tenant, decision, default))!.Status);
        Assert.True(record.IsSelectable);
    }

    [Fact]
    public async Task FixD_F7_the_decision_carries_when_and_by_whom_it_was_prepared_not_when_it_was_applied()
    {
        // WP-VW-W4 (W4-FIX-d, F-7): "Hazırlayan" showed the APPLY time (11:04) instead of the preparation (10:53)
        var h = new CompetitorHarness();
        var (proposal, decision, instance) = await ProposeAndSubmitAsync(h, new CompetitorDecisionRequest("reject", null, null, null, "Kopya"));
        var prepared = new DateTimeOffset(2026, 10, 14, 7, 53, 0, TimeSpan.Zero);
        h.Decisions.Backdate(decision, prepared);
        await h.OutcomeAsync(decision, instance, WorkflowApprovalOutcomes.Approved, Reviewer);

        h.As("u-manager", [.. CompetitorPermissions.All]);
        var dto = (await h.Service.ProposalAsync(proposal, default)).Data!.Decision!;
        Assert.Equal(CompetitorDecisionStatuses.Applied, dto.Status);
        Assert.Equal(prepared, dto.CreatedAt);
        Assert.Equal("u-manager", dto.CreatedBy);
        Assert.NotEqual(dto.CreatedAt, dto.AppliedAt);

        // the wire name the Web reads (preparedOf: createdAt first)
        var json = System.Text.Json.JsonSerializer.Serialize(dto, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"createdAt\":\"2026-10-14T07:53:00+00:00\"", json);
        Assert.Contains("\"createdBy\":\"u-manager\"", json);
    }

    [Fact]
    public async Task Merge_points_the_proposal_to_the_target_adds_the_alias_and_moves_the_markets()
    {
        var h = new CompetitorHarness();
        var target = await h.CreateAsync("Vastarel MR", "Servier", Price("TR", 100));
        var (proposal, decision, instance) = await ProposeAndSubmitAsync(h,
            new CompetitorDecisionRequest("merge", null, target, null, null), name: "Vastrel", company: "Servier");

        await h.OutcomeAsync(decision, instance, WorkflowApprovalOutcomes.Approved, Reviewer);

        var source = (await h.Products.GetAsync(Tenant, proposal, default))!;
        var survivor = (await h.Products.GetAsync(Tenant, target, default))!;
        Assert.Equal(CompetitorProductStatuses.Merged, source.Status);
        Assert.Equal(target, source.MergedIntoId);
        Assert.Contains("Vastrel", survivor.Aliases);
        Assert.Contains(survivor.MergedFrom, m => m.SourceId == proposal);
        Assert.Equal(CompetitorDecisionStatuses.Applied, (await h.Decisions.GetAsync(Tenant, decision, default))!.Status);

        // the merged read names the survivor; a new proposal of the merged name lands on the survivor
        var read = await h.Service.GetAsync(proposal, default);
        Assert.Equal(target, read.Data!.Survivor!.Id);
        h.As(RepB, CompetitorPermissions.Read, CompetitorPermissions.Propose);
        var again = await h.Service.ProposeAsync(new CompetitorProposalRequest("vastrel", "SERVIER", Tutukon, null), default);
        Assert.Equal(target, again.Data!.Id);
        Assert.True(again.Data.Existing);
    }

    [Fact]
    public async Task A_direct_merge_of_two_active_records_moves_the_chosen_market_adds_aliases_and_needs_no_approval()
    {
        var h = new CompetitorHarness();
        var source = await h.CreateAsync("Vastarel", "Servier", Price("AE", 40, "AED"), Price("TR", 90));
        var target = await h.CreateAsync("Vastarel MR", "Servier", Price("TR", 100));

        var unconfirmed = await h.Service.MergeAsync(source, new CompetitorMergeRequest(target, ["AE"], Confirm: false), default);
        var merged = await h.Service.MergeAsync(source, new CompetitorMergeRequest(target, ["AE", "TR"], Confirm: true), default);

        Assert.Equal(400, unconfirmed.StatusCode);
        Assert.Equal(CompetitorErrors.MergeConfirmRequired, unconfirmed.Errors[0]);
        Assert.True(merged.IsSuccessful, string.Join(";", merged.Errors));
        Assert.Contains("Vastarel", merged.Data!.Aliases);
        Assert.Equal(40m, Assert.Single(merged.Data.CountryPrices, p => p.MarketCode == "AE").BoxPrice);
        Assert.Equal(100m, Assert.Single(merged.Data.CountryPrices, p => p.MarketCode == "TR").BoxPrice); // the survivor keeps its own row
        var gone = (await h.Products.GetAsync(Tenant, source, default))!;
        Assert.Equal(CompetitorProductStatuses.Merged, gone.Status);
        Assert.Equal(target, gone.MergedIntoId);
        Assert.Empty(h.Workflow.Starts);
        Assert.Equal(target, (await h.Service.GetAsync(source, default)).Data!.Survivor!.Id);
    }

    [Fact]
    public async Task Reject_marks_the_proposal_rejected_with_the_reason()
    {
        var h = new CompetitorHarness();
        var (proposal, decision, instance) = await ProposeAndSubmitAsync(h,
            new CompetitorDecisionRequest("reject", null, null, null, "Not a competitor in TR"));

        await h.OutcomeAsync(decision, instance, WorkflowApprovalOutcomes.Approved, Reviewer);

        var record = (await h.Products.GetAsync(Tenant, proposal, default))!;
        Assert.Equal(CompetitorProductStatuses.Rejected, record.Status);
        Assert.Equal("Not a competitor in TR", record.RejectedReason);
        Assert.False(record.IsSelectable);
    }

    [Fact]
    public async Task The_preparer_approving_their_own_decision_is_SoD_returned_and_nothing_is_applied()
    {
        var h = new CompetitorHarness();
        var (proposal, decision, instance) = await ProposeAndSubmitAsync(h, new CompetitorDecisionRequest("complete",
            Write("Kardiyomet", "Abdi İbrahim"), null, null, null));

        await h.OutcomeAsync(decision, instance, WorkflowApprovalOutcomes.Approved, "u-manager");

        Assert.Equal(CompetitorProductStatuses.Proposed, (await h.Products.GetAsync(Tenant, proposal, default))!.Status);
        var stored = (await h.Decisions.GetAsync(Tenant, decision, default))!;
        Assert.Equal(CompetitorDecisionStatuses.Returned, stored.Status);
        Assert.Equal(CompetitorErrors.Sod, Assert.Single(stored.ReviewRounds).ReasonCode);
        Assert.True(CompetitorDecisionStatuses.IsEditable(stored.Status));
    }

    [Fact]
    public async Task A_decision_in_review_is_locked()
    {
        var h = new CompetitorHarness();
        var (proposal, _, _) = await ProposeAndSubmitAsync(h, new CompetitorDecisionRequest("reject", null, null, null, "x"));

        var edit = await h.Service.SaveDecisionAsync(proposal, new CompetitorDecisionRequest("reject", null, null, null, "y"), default);

        Assert.Equal(409, edit.StatusCode);
        Assert.Equal(CompetitorErrors.DecisionLocked, edit.Errors[0]);
    }

    // ── inactive is never selectable ──

    [Fact]
    public async Task An_inactive_record_is_not_in_lookup_and_is_not_a_merge_target()
    {
        var h = new CompetitorHarness();
        var inactive = await h.CreateAsync("Vastarel MR", "Servier");
        var other = await h.CreateAsync("Vastarel", "Servier");
        Assert.True((await h.Service.DeactivateAsync(inactive, null, default)).IsSuccessful);

        var lookup = await h.Service.LookupAsync(Tutukon, null, null, null, default);
        var candidates = await h.Service.MergeCandidatesAsync(other, null, default);
        var direct = await h.Service.MergeAsync(other, new CompetitorMergeRequest(inactive, null, true), default);
        h.As(RepA, CompetitorPermissions.Read, CompetitorPermissions.Propose);
        var proposal = (await h.Service.ProposeAsync(new CompetitorProposalRequest("Kardiyomet", "Abdi", Tutukon, null), default)).Data!.Id;
        h.As("u-manager", [.. CompetitorPermissions.All]);
        var viaDecision = await h.Service.SaveDecisionAsync(proposal, new CompetitorDecisionRequest("merge", null, inactive, null, null), default);

        Assert.DoesNotContain(lookup.Data!, r => r.Id == inactive);
        Assert.Contains(lookup.Data!, r => r.Id == other);
        Assert.DoesNotContain(candidates.Data!, r => r.Id == inactive);
        Assert.Equal(409, direct.StatusCode);
        Assert.Equal(CompetitorErrors.NotSelectable, direct.Errors[0]);
        Assert.Equal(409, viaDecision.StatusCode);
        Assert.Equal(CompetitorErrors.NotSelectable, viaDecision.Errors[0]);
        // still listed for the manager (inactive is a default list status), still readable
        Assert.Contains((await h.Service.ListAsync(null, null, null, null, null, null, null, null, null, null, default)).Data!.Items, r => r.Id == inactive);
    }

    [Fact]
    public async Task Lookup_carries_the_market_card_as_country_for_the_report()
    {
        var h = new CompetitorHarness();
        var id = await h.CreateAsync("Vastarel MR", "Servier", Price("TR", 100, reimbursement: "reimbursed_conditional"), Price("AE", 40, "AED"));

        var row = Assert.Single((await h.Service.LookupAsync(Tutukon, null, "tr", "vastarel", default)).Data!);
        var wire = System.Text.Json.JsonSerializer.SerializeToElement(row, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Equal(id, row.Id);
        Assert.Equal(("TR", 100m, "reimbursed_conditional"), (row.Country!.MarketCode, row.Country.BoxPrice, row.Country.ReimbursementStatusCode));
        Assert.Equal("TR", wire.GetProperty("country").GetProperty("marketCode").GetString()); // the W4-BE-a client reads "country"
        Assert.Empty((await h.Service.LookupAsync(Uroflex, null, "TR", null, default)).Data!);
    }

    // ── similarity ──

    [Fact]
    public async Task Similarity_folds_Turkish_and_includes_aliases_and_proposals_show_similar_records()
    {
        Assert.Equal(100, CompetitorNames.Similarity("Tamsulin", "TAMSULİN"));
        Assert.Equal(100, CompetitorNames.Similarity("Ürofleks", "urofleks"));
        Assert.InRange(CompetitorNames.Similarity("Koronex", "Koronex 100"), 60, 99);
        Assert.InRange(CompetitorNames.Similarity("Koronex", "Parol"), 0, 20);

        var h = new CompetitorHarness();
        var withAlias = await h.CreateAsync("Vastarel MR", "Servier");
        var row = (await h.Products.GetAsync(Tenant, withAlias, default))!;
        row.Aliases = ["Kardiyomet"];
        Assert.True(await h.Products.ReplaceAsync(row, row.Version, default));
        Assert.Equal(100, CompetitorNames.BestSimilarity("kardiyomet", (await h.Products.GetAsync(Tenant, withAlias, default))!));

        h.As(RepA, CompetitorPermissions.Read, CompetitorPermissions.Propose);
        await h.Service.ProposeAsync(new CompetitorProposalRequest("Kardiyomet", "Abdi İbrahim", Tutukon, null), default);
        h.As("u-manager", [.. CompetitorPermissions.All]);
        var proposals = await h.Service.ProposalsAsync(default);

        var similar = Assert.Single(Assert.Single(proposals.Data!).Similar);
        Assert.Equal(withAlias, similar.Id);
        Assert.Equal(100, similar.Score);
    }

    // ── tenant isolation ──

    [Fact]
    public async Task Another_tenant_never_sees_or_changes_a_record()
    {
        var h = new CompetitorHarness();
        var id = await h.CreateAsync("Vastarel MR", "Servier");
        h.TenantContext.SetTenant(OtherTenant);

        var get = await h.Service.GetAsync(id, default);
        var list = await h.Service.ListAsync(null, null, null, null, null, null, null, null, null, null, default);
        var update = await h.Service.UpdateAsync(id, Write("X", "Y"), default);
        var deactivate = await h.Service.DeactivateAsync(id, null, default);
        var sameName = await h.Service.CreateAsync(Write("Vastarel MR", "Servier"), default);

        Assert.Equal(404, get.StatusCode);
        Assert.DoesNotContain(list.Data!.Items, r => r.Id == id);
        Assert.Equal(404, update.StatusCode);
        Assert.Equal(404, deactivate.StatusCode);
        Assert.True(sameName.IsSuccessful); // uniqueness is per tenant
        Assert.Equal("Vastarel MR", h.Products.Raw.Single(r => r.Id == id).Name);
        Assert.Equal(Tenant, h.Products.Raw.Single(r => r.Id == id).TenantId);
    }

    // ── usage placeholder / menu ──

    [Fact]
    public async Task Usage_is_zero_and_not_available_without_a_CRM_call()
    {
        var h = new CompetitorHarness();
        var id = await h.CreateAsync("Vastarel MR", "Servier");

        var usage = await h.Service.UsageAsync(id, default);

        Assert.Equal(0, usage.Data!.ReportCount);
        Assert.False(usage.Data.Available);
        Assert.Equal("crm_usage_pending", usage.Data.Note);
    }

    [Fact]
    public void The_manifest_declares_the_competitor_products_page_with_the_three_keys()
    {
        var manifest = new ProductItemSkuMasterManifestProvider().GetManifest();

        var page = Assert.Single(manifest.Pages, p => p.PageCode == "COMPETITOR_PRODUCTS");
        Assert.Equal("/MasterDataManagement/CompetitorProducts", page.RoutePath);
        Assert.Equal(CompetitorPermissions.Read, page.RequiredPermission);
        Assert.True(page.IsNavigationVisible);
        Assert.Equal(new[] { CompetitorPermissions.Manage, CompetitorPermissions.Propose, CompetitorPermissions.Read }.Order(),
            page.Actions.Select(a => a.PermissionKey).Distinct().Order());
    }
}
