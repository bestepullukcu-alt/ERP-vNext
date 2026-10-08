using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.VisitContentSequence;
using Diten.CrmService.Application.Features.VisitContentSequence.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitContentSequence.Queries;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitContentSequence;

/// <summary>
/// MOD-0155 FU04 — Visit Content Sequence resolver, v2 since WP-SB-3b (multi-product, weighted rotation, per-product
/// stage). Pure unit tests over the in-memory <see cref="VisitContentTestKit"/> (no Mongo). Pins down: the per-role
/// weighted rotation, the per-product JourneyProgress stage + pending projection + wrap-around (S3-7, replaces the
/// D-END-OF-JOURNEY stop), the stage path resolution (latest-published / pinned, KP-3 release rules), the coded drops
/// and the next candidate taking their place, the audience warning, the product-count duration, backward compatibility
/// of the top-level fields, the retired template-level binding (S3-2), no persistence and the preview handler.
/// </summary>
public sealed class VisitContentSequenceTests
{
    // ── first visit / per-product stage ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task First_visit_starts_every_product_at_stage_zero()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A");
        var b = kit.AddProduct("B");

        var result = await kit.ResolveAsync();

        Assert.Equal(VisitContentSequenceStatus.Resolved, result.Status);
        Assert.Equal(new[] { a.ProductId, b.ProductId }, result.Items!.Select(i => i.ProductId));
        Assert.All(result.Items!, i => Assert.Equal(0, i.StageIndex));
        Assert.Equal(a.Stages[0].StageId, result.Items![0].StageId);
        Assert.Equal("strategy", result.ContentSource);
    }

    [Fact]
    public async Task Each_product_tells_its_own_journey_stage()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A");
        var b = kit.AddProduct("B");
        kit.SetProgress(a, currentStageIndex: 2, exposure: 2);
        kit.SetProgress(b, currentStageIndex: 1, exposure: 2);

        var items = (await kit.ResolveAsync()).Items!;

        var itemA = items.Single(i => i.ProductId == a.ProductId);
        var itemB = items.Single(i => i.ProductId == b.ProductId);
        Assert.Equal((2, a.Stages[2].StageId, a.Paths[2].Id), (itemA.StageIndex, itemA.StageId, itemA.PathId));
        Assert.Equal((1, b.Stages[1].StageId, b.Paths[1].Id), (itemB.StageIndex, itemB.StageId, itemB.PathId));
    }

    [Fact]
    public async Task Resolution_is_deterministic()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("A");
        kit.AddProduct("B");
        kit.AddProduct("C");
        kit.AddProduct("D");

        var first = await kit.ResolveAsync();
        var second = await kit.ResolveAsync();

        Assert.Equal(first.Items!.Select(i => (i.ProductId, i.StageId)), second.Items!.Select(i => (i.ProductId, i.StageId)));
        Assert.Equal(first.VisitDurationMinutes, second.VisitDurationMinutes);
    }

    // ── S3-7 (updates D-END-OF-JOURNEY "stops"): past the last stage the journey WRAPS to stage 0 ────────────────

    [Fact]
    public async Task Past_last_stage_wraps_to_the_first_stage_instead_of_ending()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A");
        kit.SetProgress(a, currentStageIndex: 2, exposure: 2); // the last of 3 stages is next

        var onLast = await kit.ResolveAsync();
        var afterLast = await kit.ResolveAsync(new[] { new VisitContentPendingExposure(a.ProductId, a.JourneyId, 1) });

        Assert.Equal(2, onLast.StageIndex);
        Assert.Equal(VisitContentSequenceStatus.Resolved, afterLast.Status);       // no end-of-journey flag any more
        Assert.Equal(0, afterLast.StageIndex);
        Assert.Equal(a.Stages[0].StageId, afterLast.StageId);
        Assert.DoesNotContain(VisitContentSequenceReasonCodes.JourneyCompleted, afterLast.ReasonCodes);
    }

    [Fact]
    public async Task Pending_visits_project_the_stage_forward()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A");

        var result = await kit.ResolveAsync(new[] { new VisitContentPendingExposure(a.ProductId, a.JourneyId, 1) });

        Assert.Equal(1, result.Items!.Single().StageIndex);
    }

    // ── S3-5 weighted rotation ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Five_promo_candidates_limit_three_picks_the_largest_gaps()
    {
        var kit = new VisitContentTestKit();
        var products = new[] { "A", "B", "C", "D", "E" }.Select(c => kit.AddProduct(c)).ToArray();
        // Σ exposure = 5 → each deserves 1. Gaps: A −1, B −1, C +1, D 0, E +1.
        kit.SetProgress(products[0], 0, exposure: 2);
        kit.SetProgress(products[1], 0, exposure: 2);
        kit.SetProgress(products[3], 0, exposure: 1);

        var items = (await kit.ResolveAsync()).Items!;

        // C and E (+1, tie → SortOrder), then D (0). A and B (most told) wait.
        Assert.Equal(new[] { "C", "E", "D" }, items.Select(i => i.ProductCode));
    }

    [Fact]
    public async Task Weight_changes_which_product_is_behind()
    {
        // Same exposures (A told twice, B once), one promo slot: equal weights favour B, a 75 / 25 split favours A.
        async Task<string?> Pick(decimal? weightA, decimal? weightB)
        {
            var kit = new VisitContentTestKit();
            kit.Capacity.MaxPromoProducts = 1;
            var a = kit.AddProduct("A", weight: weightA);
            var b = kit.AddProduct("B", weight: weightB);
            kit.SetProgress(a, 0, exposure: 2);
            kit.SetProgress(b, 0, exposure: 1);
            return (await kit.ResolveAsync()).Items!.Single().ProductCode;
        }

        Assert.Equal("B", await Pick(null, null));   // deserved 1.5 / 1.5 → gaps −0.5 / +0.5
        Assert.Equal("A", await Pick(75m, 25m));     // deserved 2.25 / 0.75 → gaps +0.25 / −0.25
    }

    [Fact]
    public async Task First_visit_orders_by_weight_then_sort_order()
    {
        var kit = new VisitContentTestKit();
        kit.Capacity.MaxPromoProducts = 2;
        kit.AddProduct("LIGHT", weight: 20m, sortOrder: 10);
        kit.AddProduct("HEAVY", weight: 50m, sortOrder: 30);
        kit.AddProduct("MID", weight: 30m, sortOrder: 20);

        Assert.Equal(new[] { "HEAVY", "MID" }, (await kit.ResolveAsync()).Items!.Select(i => i.ProductCode));

        var unweighted = new VisitContentTestKit();
        unweighted.Capacity.MaxPromoProducts = 2;
        unweighted.AddProduct("THIRD", sortOrder: 30);
        unweighted.AddProduct("FIRST", sortOrder: 10);
        unweighted.AddProduct("SECOND", sortOrder: 20);

        Assert.Equal(new[] { "FIRST", "SECOND" }, (await unweighted.ResolveAsync()).Items!.Select(i => i.ProductCode));
    }

    [Fact]
    public async Task Non_promo_is_rotated_separately_within_its_own_limit_from_the_capacity()
    {
        var kit = new VisitContentTestKit();
        kit.Capacity.MaxPromoProducts = 1;
        kit.Capacity.MaxNonPromoProducts = 2;
        var p1 = kit.AddProduct("P1");
        kit.AddProduct("P2");
        kit.AddProduct("N1", role: StrategyProductLineRoles.NonPromo);
        kit.AddProduct("N2", role: StrategyProductLineRoles.NonPromo);
        kit.AddProduct("N3", role: StrategyProductLineRoles.NonPromo);
        kit.SetProgress(p1, 0, exposure: 5); // a promo exposure never moves the non-promo rotation

        var result = await kit.ResolveAsync();

        Assert.Equal(new[] { "P2" }, result.Items!.Where(i => i.Role == StrategyProductLineRoles.Promo).Select(i => i.ProductCode));
        Assert.Equal(new[] { "N1", "N2" },
            result.Items!.Where(i => i.Role == StrategyProductLineRoles.NonPromo).Select(i => i.ProductCode));
        Assert.Equal((1, 2), (result.PromoItemCount, result.NonPromoItemCount));
    }

    [Fact]
    public void Rotation_gap_is_the_deserved_share_minus_the_exposure()
    {
        Assert.Equal(1m, VisitContentRotation.Gap(totalExposure: 5, weight: 1, totalWeight: 5, exposure: 0));
        Assert.Equal(-1m, VisitContentRotation.Gap(totalExposure: 5, weight: 1, totalWeight: 5, exposure: 2));
        Assert.Equal(0.25m, VisitContentRotation.Gap(totalExposure: 3, weight: 75, totalWeight: 100, exposure: 2));
    }

    // ── stage path: latest-published / pinned (KP-3 release rules) ───────────────────────────────────────────────

    [Fact]
    public async Task Latest_published_stage_tells_the_new_release_after_a_release()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A", stageCount: 1);
        var v1 = a.Paths[0];
        // KP-3 release: v2 of the same code / country / language is published, v1 becomes inactive.
        var v2 = kit.AddPath(v1.PathCode, "2.0", publishedAt: VisitContentTestKit.Past.AddDays(10));
        v1.PathStatus = KnowledgePathStatuses.Inactive;
        // Another language's release of the same code is a different identity — never picked.
        kit.AddPath(v1.PathCode, "9.0", language: "en", publishedAt: VisitContentTestKit.Past.AddDays(20));

        var item = (await kit.ResolveAsync()).Items!.Single();

        Assert.Equal((v2.Id, "2.0"), (item.PathId, item.PathVersion));
    }

    [Fact]
    public async Task Pinned_stage_with_an_inactive_path_drops_the_product_and_the_next_candidate_enters()
    {
        var kit = new VisitContentTestKit();
        kit.Capacity.MaxPromoProducts = 1;
        var a = kit.AddProduct("A", pin: ContentEngagementJourneyPathPin.Pinned);
        kit.AddProduct("B", pin: ContentEngagementJourneyPathPin.Pinned);
        a.Paths[0].PathStatus = KnowledgePathStatuses.Inactive; // a later release superseded the pinned version
        kit.AddPath(a.Paths[0].PathCode, "2.0", publishedAt: VisitContentTestKit.Past.AddDays(1));

        var result = await kit.ResolveAsync();

        Assert.Equal(new[] { "B" }, result.Items!.Select(i => i.ProductCode)); // no silent drift to A's v2
        Assert.Contains(VisitContentSequenceReasonCodes.StagePathUnpublished, result.ReasonCodes);
    }

    [Fact]
    public async Task Steps_are_the_active_steps_in_order_and_claims_come_from_the_path()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A", stageCount: 1);

        var item = (await kit.ResolveAsync()).Items!.Single();

        Assert.Equal(new[] { "C-first", "C-second" }, item.Steps.Select(s => s.ContentCode)); // archived step excluded
        Assert.Equal(new int?[] { 3, 4 }, item.Steps.Select(s => s.Minutes));
        Assert.Equal(a.Paths[0].Claims.Select(c => (c.ClaimId, c.ClaimCode)), item.Claims.Select(c => (c.ClaimId, c.ClaimCode)));
        Assert.Equal(("A-J", a.Paths[0].PathCode), (item.JourneyCode, item.PathCode));
    }

    // ── coded drops ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_line_without_a_journey_drops_with_product_has_no_journey()
    {
        var kit = new VisitContentTestKit();
        kit.Capacity.MaxPromoProducts = 1;
        kit.AddProduct("LEGACY", withJourney: false, sortOrder: 10); // a pre-SB-3a line
        kit.AddProduct("B", sortOrder: 20);

        var result = await kit.ResolveAsync();

        Assert.Equal(new[] { "B" }, result.Items!.Select(i => i.ProductCode));
        Assert.Contains(VisitContentSequenceReasonCodes.ProductHasNoJourney, result.ReasonCodes);

        var onlyLegacy = new VisitContentTestKit();
        onlyLegacy.AddProduct("LEGACY", withJourney: false);
        var none = await onlyLegacy.ResolveAsync();
        Assert.Equal(VisitContentSequenceStatus.NoJourney, none.Status);
        // WP-VP-3C (K-7a) — a visit left without any product says so (no_products).
        Assert.Equal(
            new[] { VisitContentSequenceReasonCodes.ProductHasNoJourney, VisitContentSequenceReasonCodes.NoProducts },
            none.ReasonCodes);
        Assert.Empty(none.Items!);
    }

    [Fact]
    public async Task An_unpublished_journey_drops_with_journey_unpublished()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("A", publishJourney: false);

        var result = await kit.ResolveAsync();

        Assert.Equal(VisitContentSequenceStatus.NoJourney, result.Status);
        Assert.Contains(VisitContentSequenceReasonCodes.JourneyUnpublished, result.ReasonCodes);
        Assert.Null(result.StageId);
    }

    [Fact]
    public async Task A_stored_index_beyond_the_journey_reads_as_stage_zero_with_stage_index_reset()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A");
        kit.SetProgress(a, currentStageIndex: 7, exposure: 7); // the journey lost stages since

        var result = await kit.ResolveAsync();

        Assert.Equal(0, result.Items!.Single().StageIndex);
        Assert.Contains(VisitContentSequenceReasonCodes.StageIndexReset, result.ReasonCodes);
        Assert.Contains(VisitContentSequenceReasonCodes.StageIndexReset, result.Items!.Single().Warnings);
    }

    // ── §5.3 audience mismatch = warning (CT default) ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Audience_mismatch_is_a_warning_and_the_product_stays()
    {
        var kit = new VisitContentTestKit();
        var nephrology = Guid.NewGuid();
        kit.Sources.Audiences[nephrology] = new AudienceProfile
        {
            Id = nephrology, Dimensions = { new AudienceDimensionAssignment { AxisCode = "specialty", Values = { "NEPH" } } }
        };
        kit.AddProduct("A", audienceProfileId: nephrology);
        kit.Sources.Specialties[kit.DoctorId] = "CARD";

        var mismatch = await kit.ResolveAsync();
        Assert.Equal(VisitContentSequenceStatus.Resolved, mismatch.Status);
        Assert.Contains(VisitContentSequenceReasonCodes.JourneyAudienceMismatch, mismatch.Items!.Single().Warnings);

        kit.Sources.Specialties[kit.DoctorId] = "neph";
        Assert.Empty((await kit.ResolveAsync()).Items!.Single().Warnings);

        kit.Sources.Specialties[kit.DoctorId] = null; // cannot be decided → nothing is said
        Assert.Empty((await kit.ResolveAsync()).Items!.Single().Warnings);
    }

    // ── duration = product counts (FU06B) ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Duration_is_the_product_count_times_the_product_time_plus_the_report()
    {
        var kit = new VisitContentTestKit(); // Promo=5, NonPromo=3, Report=3
        kit.AddProduct("P1");
        kit.AddProduct("P2");
        kit.AddProduct("N1", role: StrategyProductLineRoles.NonPromo);

        var result = await kit.ResolveAsync();

        Assert.Equal((2, 1), (result.PromoItemCount, result.NonPromoItemCount));
        Assert.Equal(2 * 5 + 1 * 3 + 3, result.VisitDurationMinutes);
    }

    [Fact]
    public async Task Missing_capacity_is_coded_duration_is_zero_and_the_limits_default_to_three()
    {
        var kit = new VisitContentTestKit();
        kit.Capacities.Rows.Clear();
        for (var i = 0; i < 5; i++)
        {
            kit.AddProduct($"P{i}");
        }

        var result = await kit.ResolveAsync();

        Assert.Contains(VisitContentSequenceReasonCodes.CapacityNotFound, result.ReasonCodes);
        Assert.Equal(0, result.VisitDurationMinutes);
        Assert.Equal(CycleCapacityLimits.DefaultMaxProductsPerVisit, result.Items!.Count);
    }

    [Fact]
    public async Task No_product_lines_is_coded_not_invented()
    {
        var kit = new VisitContentTestKit();

        var result = await kit.ResolveAsync();

        Assert.Equal(VisitContentSequenceStatus.NoJourney, result.Status);
        Assert.Contains(VisitContentSequenceReasonCodes.ContentSplitUnresolved, result.ReasonCodes);
        // WP-VP-3C (K-7a) — no product: planned with the report time only (the kit's 3 minutes), said with no_products.
        Assert.Contains(VisitContentSequenceReasonCodes.NoProducts, result.ReasonCodes);
        Assert.Equal(kit.Capacity.ReportDuration, result.VisitDurationMinutes);
        Assert.Empty(result.Items!);
    }

    // ── backward compatibility + S3-2 + ARCH GATE ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Top_level_fields_come_from_the_first_promo_item()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("N1", role: StrategyProductLineRoles.NonPromo, sortOrder: 5);
        var p = kit.AddProduct("P1", sortOrder: 10);
        kit.SetProgress(p, currentStageIndex: 1, exposure: 1);

        var result = await kit.ResolveAsync();
        var lead = result.Items!.First(i => i.Role == StrategyProductLineRoles.Promo);

        Assert.Equal(p.ProductId, lead.ProductId);
        Assert.Equal((lead.JourneyId, lead.StageId, lead.StageIndex, lead.StageCode, lead.StageName),
            (result.JourneyId!.Value, result.StageId!.Value, result.StageIndex!.Value, result.StageCode, result.StageDisplayName));
        Assert.Equal(kit.StrategyId, result.StrategyTemplateId); // the existing top-level field is untouched

        var onlyNonPromo = new VisitContentTestKit();
        var n = onlyNonPromo.AddProduct("N1", role: StrategyProductLineRoles.NonPromo);
        Assert.Equal(n.JourneyId, (await onlyNonPromo.ResolveAsync()).JourneyId); // no promo → the first item
    }

    [Fact]
    public async Task A_template_level_journey_binding_is_no_longer_read()
    {
        var kit = new VisitContentTestKit();
        var bound = kit.AddProduct("BOUND"); // a published journey with stages ...
        kit.Lines.Clear();                   // ... bound ONLY at template level (the retired S3-2 way)
        kit.TemplateBindings.Add(new StrategyTemplateContentReference(
            StrategyContentRefTypes.ContentEngagementJourney, bound.JourneyId, 0));

        var result = await kit.ResolveAsync();

        Assert.NotEqual(VisitContentSequenceStatus.Resolved, result.Status);
        Assert.Null(result.JourneyId);

        var a = kit.AddProduct("A");
        Assert.Equal(new[] { a.JourneyId }, (await kit.ResolveAsync()).Items!.Select(i => i.JourneyId));
    }

    [Fact]
    public void Items_carry_no_play_or_campaign_identity()
    {
        foreach (var type in new[] { typeof(VisitContentItem), typeof(VisitContentStep), typeof(VisitContentClaim),
                     typeof(PlannedVisitContentItem), typeof(PlannedVisitContentStep), typeof(PlannedVisitContentClaim),
                     typeof(Features.PlannedVisit.PlannedVisitContentItemDto) })
        {
            Assert.DoesNotContain(type.GetProperties(), p =>
                p.Name.Contains("Strategy", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Campaign", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Play", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Segment", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ── play resolution (unchanged) ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_strategy_is_coded_not_invented()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("A");
        var result = await kit.Resolver.ResolveAsync(kit.Request(strategyTemplateId: Guid.NewGuid()), default);

        Assert.Equal(VisitContentSequenceStatus.NoStrategy, result.Status);
        Assert.Contains(VisitContentSequenceReasonCodes.StrategyNotFound, result.ReasonCodes);
        Assert.Null(result.StageId);
    }

    [Fact]
    public async Task Non_member_of_segment_has_no_play()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("A");
        kit.Segments.Member = false;

        var result = await kit.Resolver.ResolveAsync(kit.Request(useSegment: true), default);

        Assert.Equal(VisitContentSequenceStatus.NoStrategy, result.Status);
        Assert.Contains(VisitContentSequenceReasonCodes.StrategyNotFound, result.ReasonCodes);
    }

    [Fact]
    public async Task Member_of_segment_resolves_the_segments_play()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("A");

        var result = await kit.Resolver.ResolveAsync(kit.Request(useSegment: true), default);

        Assert.Equal(VisitContentSequenceStatus.Resolved, result.Status);
        Assert.Equal(kit.StrategyId, result.StrategyTemplateId);
    }

    // ── AC-BND-1 no persistence / AC-EP preview handler ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolver_persists_nothing()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A");
        var progress = kit.SetProgress(a, 1, 1);

        await kit.ResolveAsync();

        Assert.Equal(0, kit.Capacities.InsertCalls);
        Assert.Equal(0, kit.Capacities.ReplaceCalls);
        Assert.Equal((1, 1, 0), (progress.CurrentStageIndex, progress.ExposureCount, progress.Cycle)); // read, never advanced
    }

    [Fact]
    public async Task Preview_handler_returns_200_with_resolver_parity()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("A");
        var handler = new PreviewVisitContentHandler(kit.Resolver);
        var request = kit.Request();

        var response = await handler.Handle(new PreviewVisitContentQuery(request), default);
        var direct = await kit.Resolver.ResolveAsync(request, default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(direct.StageId, response.Data!.StageId);
        Assert.Equal(direct.Items!.Select(i => i.PathId), response.Data.Items!.Select(i => i.PathId));
        Assert.Equal(direct.VisitDurationMinutes, response.Data.VisitDurationMinutes);
        Assert.Equal(0, kit.Capacities.InsertCalls);
    }

    [Fact]
    public async Task Preview_handler_rejects_malformed_request_with_400()
    {
        var kit = new VisitContentTestKit();
        var handler = new PreviewVisitContentHandler(kit.Resolver);
        var bad = new VisitContentSequenceRequest(
            SubjectType: "", SubjectId: Guid.Empty, SegmentId: null, StrategyTemplateId: null,
            CyclePeriodId: null, PriorStageIndex: null, EffectiveAt: VisitContentTestKit.Now);

        var response = await handler.Handle(new PreviewVisitContentQuery(bad), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }
}
