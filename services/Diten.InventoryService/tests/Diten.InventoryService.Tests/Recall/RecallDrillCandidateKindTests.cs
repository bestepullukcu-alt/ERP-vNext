using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall.Drills;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-MVP1-FU-CLEAN1 — O-R5-5: a lot reached through BOTH candidate paths (received by PO and by production, parent of a genealogy link and
/// sold) is ONE candidate of ONE kind — a finished good (pack §8.2: the company received it from production) — fixed in the candidate read
/// itself, not hidden on the screen. A plain raw material stays a raw-material candidate.
/// </summary>
public sealed class RecallDrillCandidateKindTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallDrillCandidateKindTests(RecallApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_lot_reached_by_both_paths_is_one_finished_good_candidate_and_no_raw_material_candidate()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory); // Rm: PO receipt + genealogy parent; Fg: PROD receipt + sold
        var both = await w.S.LotAsync();
        var order = "MO-" + Guid.NewGuid().ToString("N")[..8];
        await w.PostAsync(w.Lots.Receipt("20", both, movementType: "GOODS_RECEIPT_PO"), "MOD-0172", "PO-" + order);   // received by PO …
        await w.PostAsync(w.Lots.Receipt("5", both, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", order);          // … and by production
        var child = await w.S.LotAsync();
        await w.PostAsync(w.Lots.Receipt("3", child, movementType: "GOODS_RECEIPT_PROD"), "MOD-0194", order);
        await w.LinkAsync(both, child, order);                                                                        // a genealogy parent
        await w.SellAsync(both, "2");                                                                                  // and sold

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(w.S.Tenant, w.S.Ledger.LegalEntity, w.S.Qa1);
        var candidates = scope.ServiceProvider.GetRequiredService<IRecallDrillCandidates>();
        var finished = await candidates.FinishedGoodLotsAsync(DateTimeOffset.UtcNow.AddDays(-365));
        var raw = await candidates.RawMaterialLotsAsync();

        Assert.Single(finished, l => l.LotId == both);                // once, as a finished good
        Assert.DoesNotContain(raw, l => l.LotId == both);             // not a raw material too
        Assert.Contains(raw, l => l.LotId == w.Rm);                   // a plain raw material is still one
        Assert.Contains(finished, l => l.LotId == w.Fg);
        Assert.Empty(finished.Select(l => l.LotId).Intersect(raw.Select(l => l.LotId)));  // the two kinds never share a lot
        Assert.Equal(finished.Count, finished.Select(l => l.LotId).Distinct().Count());   // no lot twice in a list
    }
}
