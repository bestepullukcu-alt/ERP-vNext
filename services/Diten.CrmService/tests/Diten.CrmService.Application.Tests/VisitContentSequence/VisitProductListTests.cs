using Diten.CrmService.Application.Features.CycleCapacity.Rules;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.VisitContentSequence;
using Diten.CrmService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitContentSequence;

/// <summary>
/// WP-VP-3C (K-7) — the visit product list on the PRODUCTION resolver: play → rep-pick (an earlier source wins a product),
/// play-less planning with its report-only fallback, the rotating rep order (n-th visit shifts by n − 1), the role limit
/// with overflow carried to the next visit, play-less content by product (one / none / several journeys), the path's
/// main branch only (E7-B1, step minutes outside the duration E7-B2), and an older stored item reading as play.
/// </summary>
public sealed class VisitProductListTests
{
    // ── 1 · play first; the rep adds; the same product stays the play's ────────────────────────────────────────────

    [Fact]
    public async Task Play_items_keep_their_role_the_rep_adds_rep_picks_and_a_product_in_both_stays_play()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A");                       // the play's promo line
        var b = Playless(kit, "B");                       // a product only the rep picked

        var result = await kit.Resolver.ResolveAsync(kit.Request() with
        {
            RepProducts = new[]
            {
                new VisitContentProductPick(b.ProductId, "B", StrategyProductLineRoles.Promo),
                new VisitContentProductPick(a.ProductId, "A", StrategyProductLineRoles.NonPromo) // the play wins A
            }
        }, default);

        var items = result.Items!;
        Assert.Equal(2, items.Count);
        var playItem = items.Single(i => i.ProductId == a.ProductId);
        Assert.Equal((PlannedVisitContentItemSources.Play, StrategyProductLineRoles.Promo), (playItem.Source, playItem.Role));
        var picked = items.Single(i => i.ProductId == b.ProductId);
        Assert.Equal(PlannedVisitContentItemSources.RepPick, picked.Source);
        Assert.Equal(new[] { 1, 2 }, items.Select(i => i.Order));
    }

    // ── 2 · no play: the rep's pick is the list; nothing picked = report time + no_products ────────────────────────

    [Fact]
    public async Task Without_a_play_the_pick_is_the_list_and_without_a_pick_only_the_report_time_counts()
    {
        var kit = new VisitContentTestKit();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var picked = await kit.Resolver.ResolveAsync(NoPlay(kit) with
        {
            RepProducts = new[]
            {
                new VisitContentProductPick(a, "A", StrategyProductLineRoles.Promo),
                new VisitContentProductPick(b, "B", StrategyProductLineRoles.NonPromo)
            }
        }, default);

        Assert.All(picked.Items!, i => Assert.Equal(PlannedVisitContentItemSources.RepPick, i.Source));
        Assert.Equal((1, 1), (picked.PromoItemCount, picked.NonPromoItemCount));
        Assert.Equal(ActivityTimeBudgetCalculator.VisitDuration(kit.Capacity, 1, 1), picked.VisitDurationMinutes);

        var none = await kit.Resolver.ResolveAsync(NoPlay(kit), default);
        Assert.Empty(none.Items!);
        Assert.Contains(VisitContentSequenceReasonCodes.NoProducts, none.ReasonCodes);
        Assert.Equal(ActivityTimeBudgetCalculator.VisitDuration(kit.Capacity, 0, 0), none.VisitDurationMinutes);
        Assert.Equal(kit.Capacity.ReportDuration, none.VisitDurationMinutes);
    }

    // ── 3 · the rep's set rotates one place per visit ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_rep_pick_rotates_one_place_on_each_visit_of_the_period()
    {
        var kit = new VisitContentTestKit();
        var picks = new[] { "A", "B", "C" }
            .Select(code => new VisitContentProductPick(Guid.NewGuid(), code, StrategyProductLineRoles.Promo))
            .ToList();

        async Task<string> OrderOn(int visit)
            => string.Join(",", (await kit.Resolver.ResolveAsync(
                    NoPlay(kit) with { RepProducts = picks, VisitOrdinal = visit }, default))
                .Items!.OrderBy(i => i.Order).Select(i => i.ProductCode));

        Assert.Equal("A,B,C", await OrderOn(1));
        Assert.Equal("B,C,A", await OrderOn(2));
        Assert.Equal("C,A,B", await OrderOn(3));
    }

    // ── 4 · the role limit; what overflows leads the next visit ───────────────────────────────────────────────────

    [Fact]
    public async Task Above_the_promo_limit_the_rest_overflows_and_leads_the_next_visit()
    {
        var kit = new VisitContentTestKit();
        kit.Capacity.MaxPromoProducts = 3;
        var picks = new[] { "A", "B", "C", "D" }
            .Select(code => new VisitContentProductPick(Guid.NewGuid(), code, null)) // no role ⇒ promo (K-7d)
            .ToList();

        var first = await kit.Resolver.ResolveAsync(NoPlay(kit) with { RepProducts = picks, VisitOrdinal = 1 }, default);
        Assert.Equal("A,B,C", Codes(first));
        var overflow = Assert.Single(first.OverflowProducts!);
        Assert.Equal(("D", VisitContentSequenceReasonCodes.MaxPromo), (overflow.ProductCode, overflow.Reason));

        var second = await kit.Resolver.ResolveAsync(NoPlay(kit) with
        {
            RepProducts = picks, VisitOrdinal = 2, CarryOverProductIds = new[] { overflow.ProductId }
        }, default);
        Assert.Equal("D,B,C", Codes(second)); // B, C, D, A rotated — D carried to the front
        Assert.Equal("A", Assert.Single(second.OverflowProducts!).ProductCode);
    }

    // ── 5 · play-less content: one journey / none / several ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_picked_product_tells_its_single_journey_none_is_no_approved_content_two_is_ambiguous()
    {
        var kit = new VisitContentTestKit();
        var one = Playless(kit, "ONE");
        var none = Guid.NewGuid();
        var two = Playless(kit, "TWO", journeys: 2);

        var result = await kit.Resolver.ResolveAsync(NoPlay(kit) with
        {
            RepProducts = new[]
            {
                new VisitContentProductPick(one.ProductId, "ONE", null),
                new VisitContentProductPick(none, "NONE", null),
                new VisitContentProductPick(two.ProductId, "TWO", null)
            }
        }, default);

        var told = result.Items!.Single(i => i.ProductId == one.ProductId);
        Assert.Equal(one.JourneyId, told.JourneyId);
        Assert.Equal(one.Stages[0].StageId, told.StageId);
        Assert.NotEmpty(told.Steps);
        Assert.Equal(one.JourneyId, result.JourneyId); // the lead item with content

        var bare = result.Items!.Single(i => i.ProductId == none);
        Assert.Equal((Guid.Empty, VisitContentSequenceReasonCodes.NoApprovedContent), (bare.JourneyId, bare.Warnings.Single()));
        var ambiguous = result.Items!.Single(i => i.ProductId == two.ProductId);
        Assert.Equal((Guid.Empty, VisitContentSequenceReasonCodes.AmbiguousJourney), (ambiguous.JourneyId, ambiguous.Warnings.Single()));
        Assert.Equal(1, kit.Journeys.ProductJourneyReads); // one read for every picked product
    }

    [Fact]
    public async Task A_picked_product_advances_on_its_journey_like_a_play_item()
    {
        // The same progress + pending on the same journey shape: as a play line, then as a rep pick.
        var asPlay = new VisitContentTestKit();
        var playProduct = asPlay.AddProduct("ONE");
        asPlay.SetProgress(playProduct, currentStageIndex: 1, exposure: 1);
        var pendingPlay = new[] { new VisitContentPendingExposure(playProduct.ProductId, playProduct.JourneyId, 1) };
        var play = Assert.Single((await asPlay.ResolveAsync(pendingPlay)).Items!);

        var asPick = new VisitContentTestKit();
        var picked = Playless(asPick, "ONE");
        asPick.SetProgress(picked, currentStageIndex: 1, exposure: 1);
        var result = await asPick.Resolver.ResolveAsync(NoPlay(asPick) with
        {
            RepProducts = new[] { new VisitContentProductPick(picked.ProductId, "ONE", null) },
            PendingExposures = new[] { new VisitContentPendingExposure(picked.ProductId, picked.JourneyId, 1) }
        }, default);

        var item = Assert.Single(result.Items!);
        Assert.Equal(play.StageIndex, item.StageIndex);
        Assert.Equal(picked.Stages[item.StageIndex].StageId, item.StageId);
    }

    // ── 6 · E7-B1 the main branch only; E7-B2 step minutes are not the duration ───────────────────────────────────

    [Fact]
    public async Task Only_the_main_branch_steps_are_told_and_step_minutes_never_change_the_duration()
    {
        var kit = new VisitContentTestKit();
        var product = kit.AddProduct("TUTUKON", stageCount: 1);
        var path = product.Paths[0];
        var chainId = Guid.NewGuid();
        path.ChainTemplate = new KnowledgePathChainRef { ConceptChainTemplateId = chainId, ChainVersion = "1" };
        path.Steps = new List<KnowledgePathStep>
        {
            BranchStep(10, "SHORT", "s1", 30), BranchStep(20, "MAIN", "m1", 40), BranchStep(30, "MAIN", "m2", 50),
            BranchStep(40, "SHORT", "s2", 60), BranchStep(50, "MAIN", "m3", 70)
        };
        kit.Sources.ChainTemplates[chainId] = new ConceptChainTemplate
        {
            Id = chainId,
            Branches =
            {
                new ConceptChainBranch { BranchCode = "SHORT", SortOrder = 2 },
                new ConceptChainBranch { BranchCode = "MAIN", SortOrder = 1 }
            }
        };

        var result = await kit.ResolveAsync();

        var item = Assert.Single(result.Items!);
        Assert.Equal(new[] { "Step m1", "Step m2", "Step m3" }, item.Steps.Select(s => s.Title));
        Assert.Equal(ActivityTimeBudgetCalculator.VisitDuration(kit.Capacity, 1, 0), result.VisitDurationMinutes);

        // Without a readable template the main branch is the branch of the path's first step.
        Assert.Equal(new[] { "s1", "s2" }, VisitContentMainBranch.Steps(path, null).Select(s => s.StepCode));
    }

    // ── 9 · an older stored item (no Source) reads as play ────────────────────────────────────────────────────────

    [Fact]
    public void An_item_stored_before_3C_reads_as_play()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var item = new PlannedVisitContentItem
        {
            ProductId = Guid.NewGuid(), ProductCode = "OLD", Role = StrategyProductLineRoles.Promo,
            Source = PlannedVisitContentItemSources.RepPick, Order = 2
        };
        var doc = item.ToBsonDocument();
        doc.Remove("Source");
        doc.Remove("Order");

        var back = BsonSerializer.Deserialize<PlannedVisitContentItem>(doc);

        Assert.Null(back.Source);
        Assert.Equal(PlannedVisitContentItemSources.Play, back.EffectiveSource());
        Assert.Equal(PlannedVisitContentItemSources.Play, PlannedVisitMapper.ToContentItems(new[] { back }).Single().Source);
        Assert.Equal(0, back.Order);

        // A 3C item round-trips its source and order.
        var current = BsonSerializer.Deserialize<PlannedVisitContentItem>(item.ToBsonDocument());
        Assert.Equal((PlannedVisitContentItemSources.RepPick, 2), (current.Source, current.Order));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The kit's doctor with NO play (no strategy, no segment).</summary>
    private static VisitContentSequenceRequest NoPlay(VisitContentTestKit kit)
        => kit.Request() with { StrategyTemplateId = null, SegmentId = null };

    private static string Codes(VisitContentSequenceResult result)
        => string.Join(",", result.Items!.OrderBy(i => i.Order).Select(i => i.ProductCode));

    /// <summary>A product with <paramref name="journeys"/> published journeys of its own and NO play line.</summary>
    private static VisitContentTestKit.Product Playless(VisitContentTestKit kit, string code, int journeys = 1)
    {
        var product = kit.AddProduct(code);
        kit.Lines.RemoveAt(kit.Lines.Count - 1);
        var rows = new List<ContentEngagementJourneyDto> { kit.Journeys.Published.Last() };
        for (var i = 1; i < journeys; i++)
        {
            kit.AddProduct($"{code}-{i}");
            kit.Lines.RemoveAt(kit.Lines.Count - 1);
            rows.Add(kit.Journeys.Published.Last());
        }

        kit.Journeys.ProductJourneys[product.ProductId] = rows;
        return product;
    }

    private static KnowledgePathStep BranchStep(int order, string branch, string code, int minutes) => new()
    {
        StepId = Guid.NewGuid(), StepOrder = order, StepCode = code, StepTitle = $"Step {code}", StepType = "detail",
        ContentId = Guid.NewGuid(), ContentCode = $"C-{code}", EstimatedDurationMinutes = minutes,
        Arrangement = new KnowledgePathArrangement { BranchCode = branch, ChainStepId = Guid.NewGuid(), Position = 1 }
    };
}
