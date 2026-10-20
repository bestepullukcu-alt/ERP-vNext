using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing;
using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Commands;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Queries;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 price lists on the real pipeline: the manual list (EK-1) with the SAME item checks as an
/// import, four eyes (D13: the preparer never decides, the first decision wins, a return needs its reason), ONE list in
/// force per (legal entity, country) — a newer list ends the older from its first day, planned → in force by date with no
/// scheduler —, the published list frozen (a change is a new version, with its diff), tenant isolation (404), the change
/// log, and one audit record per write command.
/// </summary>
public sealed class PriceListLifecycleTests : IDisposable
{
    private readonly PricingWorld _w = new();

    public void Dispose() => _w.Dispose();

    private static PriceListItemInput Item(string sku, decimal? pharmacist = 100m, decimal? vat = 10m, decimal? depot = null, decimal? publicPrice = null, Guid? itemId = null)
        => new(itemId, PricingWorld.Sku(sku), depot, pharmacist, publicPrice, vat);

    private async Task<PriceListDto> Draft(DateOnly validFrom, string name = "TR Resmî · Kasım", params PriceListItemInput[] items)
    {
        var created = await _w.Send(new CreatePriceListCommand(new PriceListInput(name, PricingWorld.LeTr, "try", "official", validFrom, null,
            items.Length == 0 ? new[] { Item("TUT"), Item("ALM", 50m) } : items)), PricingWorld.Marketing);
        Assert.True(created.IsSuccessful, string.Join(" | ", created.Errors ?? []));
        return created.Data!;
    }

    private async Task<PriceListDto> Publish(Guid id)
    {
        var submitted = await _w.Send(new SubmitPriceListCommand(id, null), PricingWorld.Marketing);
        Assert.True(submitted.IsSuccessful, string.Join(" | ", submitted.Errors ?? []));
        var decided = await _w.Send(new DecidePriceListCommand(id, new PriceListDecisionInput("publish", null)), PricingWorld.SalesOps);
        Assert.True(decided.IsSuccessful, string.Join(" | ", decided.Errors ?? []));
        return decided.Data!;
    }

    private static string Code<T>(Response<T> response) => response.Errors?.FirstOrDefault() ?? string.Empty;

    // ── the manual list (EK-1) ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task L1_a_manual_draft_takes_its_country_from_the_legal_entity_and_its_items_from_MDM()
    {
        var draft = await Draft(_w.Today.AddDays(10));

        Assert.Equal(("TR", "TRY", PriceListStatuses.Draft, "draft", 2), (draft.CountryCode, draft.Currency, draft.Status, draft.Badge, draft.ItemCount));
        Assert.Equal(("GM-TR", "Grand Medical TR"), (draft.LegalEntityCode, draft.LegalEntityName));
        var items = (await _w.Send(new GetPriceListItemsQuery(draft.Id, null, null, null, null))).Data!.Items;
        Assert.Equal(new[] { "LS-ALM", "LS-TUT" }, items.Select(i => i.SkuCode).OrderBy(x => x));
        Assert.Contains(_w.Log.Store, e => e.EntityType == PricingChangeEntityTypes.PriceList && e.Action == PricingChangeActions.Created);
        Assert.Equal(2, _w.Log.Store.Count(e => e.Action == PricingChangeActions.ItemAdded));
    }

    [Theory]
    [InlineData("AL5", 100, 10, PricingErrors.SkuRetired, 400)]
    [InlineData("TUT", -1, 10, PricingErrors.PriceListItemInvalid, 400)]
    [InlineData("TUT", 100, 101, PricingErrors.PriceListItemInvalid, 400)]
    [InlineData("TUT", 100.12345, 10, PricingErrors.PriceListItemInvalid, 400)]
    public async Task L2_a_manual_item_passes_the_same_checks_as_an_imported_row(string sku, double price, double vat, string code, int status)
    {
        var draft = await Draft(_w.Today.AddDays(10));

        var response = await _w.Send(new UpdatePriceListCommand(draft.Id, new PriceListInput(null, null, null, null, null, null,
            new[] { Item(sku, (decimal)price, (decimal)vat) })), PricingWorld.Marketing);

        Assert.Equal((code, status), (Code(response), response.StatusCode));
    }

    [Fact]
    public async Task L2b_no_tier_or_the_same_SKU_twice_is_refused_and_a_removed_SKU_can_come_back()
    {
        var draft = await Draft(_w.Today.AddDays(10));

        var noTier = await _w.Send(new UpdatePriceListCommand(draft.Id, new PriceListInput(null, null, null, null, null, null,
            new[] { Item("URO", pharmacist: null) })), PricingWorld.Marketing);
        var twice = await _w.Send(new UpdatePriceListCommand(draft.Id, new PriceListInput(null, null, null, null, null, null,
            new[] { Item("TUT") })), PricingWorld.Marketing);
        Assert.Equal(PricingErrors.PriceListItemInvalid, Code(noTier));
        Assert.Equal((PricingErrors.PriceListItemDuplicate, 409), (Code(twice), twice.StatusCode));

        var tut = (await _w.Send(new GetPriceListItemsQuery(draft.Id, "TUT", null, null, null))).Data!.Items.Single();
        var removed = await _w.Send(new UpdatePriceListCommand(draft.Id, new PriceListInput(null, null, null, null, null, null, null, new[] { tut.Id })),
            PricingWorld.Marketing);
        Assert.Equal(1, removed.Data!.ItemCount);
        var back = await _w.Send(new UpdatePriceListCommand(draft.Id, new PriceListInput(null, null, null, null, null, null, new[] { Item("TUT", 101m) })),
            PricingWorld.Marketing);
        Assert.True(back.IsSuccessful, string.Join(" | ", back.Errors ?? []));
        Assert.Equal(2, back.Data!.ItemCount);
    }

    [Fact]
    public async Task L2c_an_unknown_legal_entity_is_400_and_MDM_down_is_503_with_nothing_written()
    {
        var unknown = await _w.Send(new CreatePriceListCommand(new PriceListInput("x", Guid.NewGuid(), "TRY", "company", _w.Today, null)), PricingWorld.Marketing);
        _w.LegalEntities.Down = true;
        var down = await _w.Send(new CreatePriceListCommand(new PriceListInput("x", PricingWorld.LeTr, "TRY", "company", _w.Today, null)), PricingWorld.Marketing);

        Assert.Equal((PricingErrors.LegalEntityInvalid, 400), (Code(unknown), unknown.StatusCode));
        Assert.Equal((PricingErrors.MdmUnavailable, 503), (Code(down), down.StatusCode));
        Assert.Empty(_w.Lists.Store);
    }

    // ── four eyes (D13) ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task L3_submit_starts_the_publication_round_and_the_preparer_cannot_decide()
    {
        var draft = await Draft(_w.Today.AddDays(10));

        var submitted = await _w.Send(new SubmitPriceListCommand(draft.Id, null), PricingWorld.Marketing);
        Assert.Equal((PriceListStatuses.InReview, "in_review"), (submitted.Data!.Status, submitted.Data.Badge));
        var start = Assert.Single(_w.Workflow.Starts);
        Assert.Equal((PricingApprovalDefaults.TemplateCode, PricingApprovalKinds.PriceList.ObjectType, draft.Id.ToString("D")),
            (start.TemplateCode, start.ObjectType, start.ObjectId));
        Assert.Equal($"/Commercial/Pricing/PriceLists/Details/{draft.Id:D}", start.DisplayContext.DeepLinkUrl);

        var own = await _w.Send(new DecidePriceListCommand(draft.Id, new PriceListDecisionInput("publish", null)), PricingWorld.Marketing);
        Assert.Equal((PricingErrors.PriceListSod, 403), (Code(own), own.StatusCode));
        Assert.Empty(_w.Decisions.Calls);                          // refused before MOD-0023 is asked
        Assert.Equal(PriceListStatuses.InReview, _w.Lists.Store.Single().Status);
    }

    [Fact]
    public async Task L3b_the_preparer_is_refused_for_separation_of_duties_before_any_overlap_is_reported()
    {
        var day = _w.Today.AddDays(3);
        var mine = await Draft(day, "A");
        await _w.Send(new SubmitPriceListCommand(mine.Id, null), PricingWorld.Marketing);
        _w.SeedPublishedList(PricingWorld.LeTr, "TR", "TRY", day, PricingWorld.TrPrices, 10m);   // the overlap appears meanwhile

        var own = await _w.Send(new DecidePriceListCommand(mine.Id, new PriceListDecisionInput("publish", null)), PricingWorld.Marketing);

        Assert.Equal((PricingErrors.PriceListSod, 403), (Code(own), own.StatusCode));       // 403 first: the preparer learns nothing more
    }

    [Fact]
    public async Task L4_the_second_person_publishes_and_the_first_decision_wins()
    {
        var draft = await Draft(_w.Today.AddDays(-1));

        var published = await Publish(draft.Id);
        Assert.Equal((PriceListStatuses.Published, PriceListPhases.InForce, PricingWorld.SalesOps), (published.Status, published.Phase, published.PublishedBy));
        Assert.Equal("approve", Assert.Single(published.Decisions).Outcome);

        var second = await _w.Send(new DecidePriceListCommand(draft.Id, new PriceListDecisionInput("return", "late")), PricingWorld.SalesOps);
        Assert.Equal((ClaimErrorCodes.NoOpenReview, 409), (Code(second), second.StatusCode));
        Assert.Contains(_w.Log.Store, e => e.Action == PricingChangeActions.Published && e.ByUserId == PricingWorld.SalesOps);
    }

    [Fact]
    public async Task L5_a_return_needs_its_reason_and_sends_the_list_back_to_draft()
    {
        var draft = await Draft(_w.Today.AddDays(5));
        await _w.Send(new SubmitPriceListCommand(draft.Id, null), PricingWorld.Marketing);

        var bare = await _w.Send(new DecidePriceListCommand(draft.Id, new PriceListDecisionInput("return", " ")), PricingWorld.SalesOps);
        Assert.Equal(400, bare.StatusCode);

        var returned = await _w.Send(new DecidePriceListCommand(draft.Id, new PriceListDecisionInput("return", "ALM fiyatı eksik")), PricingWorld.SalesOps);
        Assert.Equal(PriceListStatuses.Draft, returned.Data!.Status);
        Assert.Contains(_w.Decisions.Calls, c => !c.Approve && c.Comment == "ALM fiyatı eksik");
    }

    [Fact]
    public async Task L5b_an_empty_list_cannot_be_submitted()
    {
        var created = await _w.Send(new CreatePriceListCommand(new PriceListInput("empty", PricingWorld.LeTr, "TRY", "company", _w.Today, null)), PricingWorld.Marketing);
        var submitted = await _w.Send(new SubmitPriceListCommand(created.Data!.Id, null), PricingWorld.Marketing);
        Assert.Equal((PricingErrors.PriceListEmpty, 409), (Code(submitted), submitted.StatusCode));
        Assert.Empty(_w.Workflow.Starts);
    }

    // ── one list in force (AÇIK-1) ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task L6_a_newer_list_ends_the_older_one_from_its_first_day_and_a_planned_list_comes_into_force_with_no_scheduler()
    {
        var october = await Publish((await Draft(_w.Today.AddDays(-19), "Ekim", Item("TUT", 412.60m))).Id);
        var november = await Publish((await Draft(_w.Today.AddDays(12), "Kasım", Item("TUT", 430.00m))).Id);

        var before = (await _w.Send(new GetPriceListByIdQuery(october.Id))).Data!;
        Assert.Equal((PriceListPhases.InForce, (DateOnly?)_w.Today.AddDays(11)), (before.Phase, before.EffectiveValidTo));
        Assert.Equal(PriceListPhases.Planned, (await _w.Send(new GetPriceListByIdQuery(november.Id))).Data!.Phase);
        var resolver = _w.Get<IPriceResolver>();
        Assert.Equal(412.60m, (await resolver.ResolveAsync(PricingWorld.Tenant, PricingWorld.LeTr, "TR", _w.Today, PriceTiers.PharmacistPurchase,
            [PricingWorld.Sku("TUT")])).Items.Single().UnitPrice);

        _w.Clock.Now = _w.Clock.Now.AddDays(12);                  // the day November begins: nothing ran, the reads change

        Assert.Equal(PriceListPhases.Past, (await _w.Send(new GetPriceListByIdQuery(october.Id))).Data!.Phase);
        Assert.Equal(PriceListPhases.InForce, (await _w.Send(new GetPriceListByIdQuery(november.Id))).Data!.Phase);
        var resolved = await resolver.ResolveAsync(PricingWorld.Tenant, PricingWorld.LeTr, "TR", _w.Today, PriceTiers.PharmacistPurchase, [PricingWorld.Sku("TUT")]);
        Assert.Equal((november.Id, 430.00m), (resolved.PriceList!.Id, resolved.Items.Single().UnitPrice));
        var inForce = (await _w.Send(new GetPriceListListQuery(null, null, null, null, "in-force", null, null, null))).Data!.Items;
        Assert.Equal(november.Id, Assert.Single(inForce).Id);
    }

    [Fact]
    public async Task L6b_a_second_list_of_the_same_company_and_country_beginning_the_same_day_is_409_at_submit_and_at_decision()
    {
        var day = _w.Today.AddDays(3);
        var first = await Draft(day, "A");
        var second = await Draft(day, "B");
        await _w.Send(new SubmitPriceListCommand(second.Id, null), PricingWorld.Marketing);   // both in review at once
        await Publish(first.Id);

        var lateSubmit = await _w.Send(new SubmitPriceListCommand((await Draft(day, "C")).Id, null), PricingWorld.Marketing);
        var lateDecision = await _w.Send(new DecidePriceListCommand(second.Id, new PriceListDecisionInput("publish", null)), PricingWorld.SalesOps);

        Assert.Equal((PricingErrors.PriceListOverlap, 409), (Code(lateSubmit), lateSubmit.StatusCode));
        Assert.Equal((PricingErrors.PriceListOverlap, 409), (Code(lateDecision), lateDecision.StatusCode));
        Assert.Equal(1, _w.Lists.Store.Count(l => l.IsPublished()));
    }

    [Fact]
    public async Task L6c_the_applier_refuses_an_overlapping_approval_that_arrives_from_the_Work_Center()
    {
        var day = _w.Today.AddDays(3);
        var first = await Draft(day, "A");
        var second = await Draft(day, "B");
        await _w.Send(new SubmitPriceListCommand(second.Id, null), PricingWorld.Marketing);
        await Publish(first.Id);
        var round = _w.Lists.Store.Single(l => l.Id == second.Id).ReviewRounds.Single();

        // the approval decided in the Work Center (the event): the overlap is checked again when it lands
        var result = await _w.Get<PricingApprovalOutcomeApplier>().ApplyAsync(PricingWorld.Tenant, PricingApprovalKinds.PriceList.ObjectType, second.Id,
            round.WorkflowInstanceId, ClaimReviewOutcomes.Approved, PricingWorld.SalesOps, null, _w.Clock.Now, default);

        Assert.Equal(ClaimReviewApplyResult.Applied, result);
        var after = _w.Lists.Store.Single(l => l.Id == second.Id);
        Assert.Equal((PriceListStatuses.Draft, PricingErrors.PriceListOverlap), (after.Status, after.ReviewRounds.Single().ReasonCode));
    }

    // ── frozen once published; a change is a new version ───────────────────────────────────────────────────────

    [Fact]
    public async Task L7_a_published_list_is_never_edited_a_new_version_copies_it_and_shows_its_diff()
    {
        var v1 = await Publish((await Draft(_w.Today.AddDays(-5), "Ekim", Item("TUT", 412.60m), Item("ALM", 186.40m), Item("GLU", 98.75m))).Id);

        var edit = await _w.Send(new UpdatePriceListCommand(v1.Id, new PriceListInput("x", null, null, null, null, null)), PricingWorld.Marketing);
        Assert.Equal((PricingErrors.PriceListNotEditable, 409), (Code(edit), edit.StatusCode));

        var v2 = (await _w.Send(new CreatePriceListVersionCommand(v1.Id, new PriceListVersionInput(_w.Today.AddDays(20), null, "Kasım")), PricingWorld.Marketing)).Data!;
        Assert.Equal((2, v1.Id, v1.FamilyId, PriceListStatuses.Draft, 3), (v2.ListVersion, v2.PreviousPriceListId, v2.FamilyId, v2.Status, v2.ItemCount));
        var items = (await _w.Send(new GetPriceListItemsQuery(v2.Id, null, null, null, null))).Data!.Items;
        var tut = items.Single(i => i.SkuCode == "LS-TUT");
        var glu = items.Single(i => i.SkuCode == "LS-GLU");
        await _w.Send(new UpdatePriceListCommand(v2.Id, new PriceListInput(null, null, null, null, null, null,
            new[] { new PriceListItemInput(tut.Id, null, null, 430m, null, 10m), Item("URO", 254.30m) }, new[] { glu.Id })), PricingWorld.Marketing);

        var diff = (await _w.Send(new GetPriceListItemsQuery(v2.Id, null, "previous", null, null))).Data!;
        Assert.Equal(new[] { ("LS-ALM", "unchanged"), ("LS-TUT", "changed"), ("LS-URO", "added") },
            diff.Items.Select(i => (i.SkuCode!, i.Diff!.Kind)).OrderBy(x => x.Item1));
        Assert.Equal(412.60m, diff.Items.Single(i => i.SkuCode == "LS-TUT").Diff!.PreviousPharmacistPurchasePrice);
        Assert.Equal("LS-GLU", Assert.Single(diff.Removed!).SkuCode);
        Assert.Equal(3, _w.Items.Store.Count(i => i.PriceListId == v1.Id && !i.IsDeleted));     // the source never changed
    }

    [Fact]
    public async Task L8_a_draft_is_discarded_and_a_discarded_list_cannot_be_submitted()
    {
        var draft = await Draft(_w.Today.AddDays(2));
        var discarded = await _w.Send(new DiscardPriceListCommand(draft.Id, draft.Version), PricingWorld.Marketing);
        var submit = await _w.Send(new SubmitPriceListCommand(draft.Id, null), PricingWorld.Marketing);

        Assert.Equal(PriceListStatuses.Discarded, discarded.Data!.Status);
        Assert.Equal(409, submit.StatusCode);
    }

    [Fact]
    public async Task L8b_a_stale_version_is_409()
    {
        var draft = await Draft(_w.Today.AddDays(2));
        var stale = await _w.Send(new UpdatePriceListCommand(draft.Id, new PriceListInput("new name", null, null, null, null, null, ExpectedVersion: draft.Version - 1)),
            PricingWorld.Marketing);
        Assert.Equal((PricingErrors.Stale, 409), (Code(stale), stale.StatusCode));
    }

    // ── tenant isolation, export, audit ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task L9_another_tenants_list_is_404_for_every_read_and_write()
    {
        var foreign = _w.SeedPublishedList(PricingWorld.LeTr, "TR", "TRY", _w.Today, PricingWorld.TrPrices, 10m, tenant: PricingWorld.OtherTenant);

        Assert.Equal(404, (await _w.Send(new GetPriceListByIdQuery(foreign.Id))).StatusCode);
        Assert.Equal(404, (await _w.Send(new GetPriceListItemsQuery(foreign.Id, null, null, null, null))).StatusCode);
        Assert.Equal(404, (await _w.Send(new CreatePriceListVersionCommand(foreign.Id, new PriceListVersionInput(null, null)), PricingWorld.Marketing)).StatusCode);
        Assert.Equal(404, (await _w.Send(new SubmitPriceListCommand(foreign.Id, null), PricingWorld.Marketing)).StatusCode);
        Assert.Equal(404, (await _w.Send(new DecidePriceListCommand(foreign.Id, new PriceListDecisionInput("publish", null)), PricingWorld.SalesOps)).StatusCode);
        Assert.Empty((await _w.Send(new GetPriceListListQuery(null, null, null, null, null, null, null, null))).Data!.Items);
    }

    [Fact]
    public async Task L10_the_export_is_an_xlsx_of_the_list_items()
    {
        var list = _w.SeedPublishedList(PricingWorld.LeTr, "TR", "TRY", _w.Today, PricingWorld.TrPrices, 10m);
        var export = (await _w.Send(new GetPriceListExportQuery(list.Id, null, null, null, null, null))).Data!;

        Assert.EndsWith(".xlsx", export.FileName);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(export.Content));
        Assert.Equal(5, workbook.Worksheet(1).RangeUsed()!.RowCount());          // header + four products
    }

    [Fact]
    public async Task L11_every_write_command_is_audited_once_and_a_read_not_at_all()
    {
        var draft = await Draft(_w.Today.AddDays(2));                                                    // 1 create
        await _w.Send(new UpdatePriceListCommand(draft.Id, new PriceListInput("renamed", null, null, null, null, null)), PricingWorld.Marketing); // 2
        await _w.Send(new GetPriceListByIdQuery(draft.Id));                                              // a read: none
        await _w.Send(new SubmitPriceListCommand(draft.Id, null), PricingWorld.Marketing);              // 3
        await _w.Send(new DecidePriceListCommand(draft.Id, new PriceListDecisionInput("publish", null)), PricingWorld.Marketing); // 4 (refused: still audited)
        await _w.Send(new DecidePriceListCommand(draft.Id, new PriceListDecisionInput("publish", null)), PricingWorld.SalesOps);  // 5

        Assert.Equal(5, _w.Audit.Bodies.Count);
        Assert.Equal(new[] { "CreatePriceListCommand", "UpdatePriceListCommand", "SubmitPriceListCommand", "DecidePriceListCommand", "DecidePriceListCommand" },
            _w.Audit.Bodies.Select(b => System.Text.Json.JsonDocument.Parse(b).RootElement.GetProperty("requestType").GetString()));
        Assert.All(_w.Audit.Bodies, b => Assert.Contains("MOD-0156", b));
    }
}
