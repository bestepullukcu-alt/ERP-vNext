using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Rules;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// WP-0174-P4 — the pure K-D rules of the typed-lot migration (pack §11.5, §17 "ManualLotMatcher (exact / case /
/// dash-space / row chain ≤ 3)"): the suggestion of each group and the resolution of the page's decisions.
/// </summary>
public sealed class ManualLotMatcherTests
{
    private static readonly Guid Item = Guid.NewGuid();
    private static readonly Guid Sku = Guid.NewGuid();
    private static readonly Guid OtherSku = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static TypedLotGroup G(string text, int minute = 0, Guid? sku = null)
        => new(ManualLotMatcher.GroupIdOf(sku ?? Sku, SkuLevel.Lsku, text), Item, sku ?? Sku, SkuLevel.Lsku, text, 1, T0.AddMinutes(minute));

    private static RegisteredLotRef Lot(string number, Guid? sku = null) => new(Guid.NewGuid(), sku ?? Sku, SkuLevel.Lsku, number);

    [Fact]
    public void Same_number_is_exact_case_or_spaces_is_case_dash_space_is_similar_else_new()
    {
        var lots = new[] { Lot("L-001"), Lot("ABC") };
        var groups = new[] { G("L-001", 1), G(" abc ", 2), G("L001", 3), G("NEW-9", 4), G("bad lot!", 5) };

        var s = ManualLotMatcher.Suggest(groups, lots);

        Assert.Equal((LotMatchKind.ExactLot, lots[0].LotId, "exact"), (s[groups[0].GroupId].Kind, s[groups[0].GroupId].LotId, s[groups[0].GroupId].Note));
        Assert.Equal((LotMatchKind.ExactLot, lots[1].LotId, "case"), (s[groups[1].GroupId].Kind, s[groups[1].GroupId].LotId, s[groups[1].GroupId].Note));
        Assert.Equal((LotMatchKind.SimilarLot, lots[0].LotId, "dash-space"), (s[groups[2].GroupId].Kind, s[groups[2].GroupId].LotId, s[groups[2].GroupId].Note));
        Assert.Equal((LotMatchKind.None, "new"), (s[groups[3].GroupId].Kind, s[groups[3].GroupId].Note));
        Assert.Equal((LotMatchKind.None, "new-invalid"), (s[groups[4].GroupId].Kind, s[groups[4].GroupId].Note));
    }

    [Fact]
    public void A_typo_of_an_earlier_group_points_at_the_chain_head_and_never_crosses_SKUs()
    {
        var first = G("P-7 00", 1);
        var second = G("P700", 2);
        var third = G("P-700", 3);
        var otherSku = G("P700", 0, OtherSku);

        var s = ManualLotMatcher.Suggest([third, otherSku, second, first], []);

        Assert.Equal(LotMatchKind.None, s[first.GroupId].Kind);
        Assert.Equal((LotMatchKind.SimilarGroup, first.GroupId), (s[second.GroupId].Kind, s[second.GroupId].GroupId));
        Assert.Equal((LotMatchKind.SimilarGroup, first.GroupId), (s[third.GroupId].Kind, s[third.GroupId].GroupId));
        Assert.Equal(LotMatchKind.None, s[otherSku.GroupId].Kind);
    }

    [Fact]
    public void Group_ids_are_deterministic_per_SKU_and_exact_text()
    {
        Assert.Equal(ManualLotMatcher.GroupIdOf(Sku, SkuLevel.Lsku, "L-1"), ManualLotMatcher.GroupIdOf(Sku, SkuLevel.Lsku, "L-1"));
        Assert.NotEqual(ManualLotMatcher.GroupIdOf(Sku, SkuLevel.Lsku, "L-1"), ManualLotMatcher.GroupIdOf(Sku, SkuLevel.Lsku, "l-1"));
        Assert.NotEqual(ManualLotMatcher.GroupIdOf(Sku, SkuLevel.Lsku, "L-1"), ManualLotMatcher.GroupIdOf(OtherSku, SkuLevel.Lsku, "L-1"));
        Assert.StartsWith("mlg-", ManualLotMatcher.GroupIdOf(Sku, SkuLevel.Gsku, "x"));
    }

    [Fact]
    public void Decisions_resolve_link_new_and_a_typo_chain_of_three()
    {
        var lot = Guid.NewGuid();
        var (a, b, c, d, e) = (G("A"), G("B"), G("C"), G("D"), G("E"));
        var plan = ManualLotMigrationPlan.Resolve([a, b, c, d, e],
        [
            new(a.GroupId, MigrationDecisionKind.LinkExisting, lot, null),
            new(b.GroupId, MigrationDecisionKind.NewLot, null, null),
            new(c.GroupId, MigrationDecisionKind.Typo, null, d.GroupId),
            new(d.GroupId, MigrationDecisionKind.Typo, null, e.GroupId),
            new(e.GroupId, MigrationDecisionKind.Typo, null, b.GroupId)
        ]);

        Assert.True(plan.IsValid);
        var t = plan.Targets.ToDictionary(x => x.GroupId);
        Assert.Equal(lot, t[a.GroupId].ExistingLotId);
        Assert.Equal(b.GroupId, t[b.GroupId].NewLotOwnerGroupId);
        Assert.Equal(b.GroupId, t[c.GroupId].NewLotOwnerGroupId); // c → d → e → b: three typo steps
    }

    [Fact]
    public void A_chain_longer_than_three_a_cycle_another_SKU_or_a_self_link_is_unresolved()
    {
        var (a, b, c, d, e) = (G("A"), G("B"), G("C"), G("D"), G("E"));
        var x = G("X", 0, OtherSku);
        MigrationDecisionInput Typo(TypedLotGroup from, TypedLotGroup to) => new(from.GroupId, MigrationDecisionKind.Typo, null, to.GroupId);
        MigrationDecisionInput New(TypedLotGroup g) => new(g.GroupId, MigrationDecisionKind.NewLot, null, null);

        var tooLong = ManualLotMigrationPlan.Resolve([a, b, c, d, e], [Typo(a, b), Typo(b, c), Typo(c, d), Typo(d, e), New(e)]);
        Assert.Equal(MigrationPlanError.TypoUnresolved, tooLong.Error);
        Assert.Equal([a.GroupId], tooLong.Groups); // b → c → d → e is three steps: fine

        var cycle = ManualLotMigrationPlan.Resolve([a, b], [Typo(a, b), Typo(b, a)]);
        Assert.Equal(MigrationPlanError.TypoUnresolved, cycle.Error);

        var crossSku = ManualLotMigrationPlan.Resolve([a, x], [Typo(a, x), New(x)]);
        Assert.Equal((MigrationPlanError.TypoUnresolved, a.GroupId), (crossSku.Error, crossSku.Groups.Single()));

        var self = ManualLotMigrationPlan.Resolve([a], [Typo(a, a)]);
        Assert.Equal(MigrationPlanError.TypoUnresolved, self.Error);
    }

    [Fact]
    public void Unknown_groups_are_stale_repeats_are_duplicates_missing_or_partial_decisions_are_incomplete()
    {
        var (a, b) = (G("A"), G("B"));
        Assert.Equal(MigrationPlanError.Stale,
            ManualLotMigrationPlan.Resolve([a], [new("mlg-gone", MigrationDecisionKind.NewLot, null, null), new(a.GroupId, MigrationDecisionKind.NewLot, null, null)]).Error);
        Assert.Equal(MigrationPlanError.Duplicate,
            ManualLotMigrationPlan.Resolve([a], [new(a.GroupId, MigrationDecisionKind.NewLot, null, null), new(a.GroupId, MigrationDecisionKind.NewLot, null, null)]).Error);
        var missing = ManualLotMigrationPlan.Resolve([a, b], [new(a.GroupId, MigrationDecisionKind.NewLot, null, null)]);
        Assert.Equal((MigrationPlanError.Incomplete, b.GroupId), (missing.Error, missing.Groups.Single()));
        Assert.Equal(MigrationPlanError.Incomplete,
            ManualLotMigrationPlan.Resolve([a], [new(a.GroupId, MigrationDecisionKind.LinkExisting, null, null)]).Error);
    }
}
