using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Persistence.Recall;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-FIX-R3T item 1 — the TENANT boundary of the recall exit block (pack §8.5, CT-R7 is tenant-WIDE, never cross-tenant) and of the
/// FEFO "under recall?" read (CT-R12), measured on the REAL <see cref="RecallPostingGate"/> through the REAL posting endpoint
/// (<c>POST /api/inventory/movements</c>) and <c>getFefoOrder</c>, on the suite's own Mongo. Tenant T2 holds an ACTIVE block on the SAME lot id /
/// the same SKU + lot number as tenant T1's lot (written straight into <c>recall_lot_blocks</c> — whether that can happen in real life is not
/// the point; the filter is). T2's block must not refuse T1 — at the pre-check (<c>ByLotAsync</c> / <c>ByTypedLotAsync</c> without a session),
/// at the in-transaction re-check (the same reads WITH the posting session; the race seam lands the block between the two) and in the FEFO
/// order (<c>UnderRecallAsync</c>) — and must leave no <c>ExitRefused</c> / <c>recall.exit.refused</c> row. Control (d): the same block in T1
/// refuses all three, so a test cannot pass because nothing is refused.
/// CT sabotage that found the gap: <c>ByLotAsync</c> without its <c>TenantId</c> filter kept the full suite green (1153/1153).
/// </summary>
public sealed class RecallTenantBoundaryTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallTenantBoundaryTests(RecallApiFactory factory) => _factory = factory;

    private static string Str(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    /// <summary>Another tenant (T2) with its own company; its rows are removed with the factory (tracked).</summary>
    private (Guid Tenant, Guid Company) OtherTenant()
    {
        var tenant = Guid.NewGuid();
        _factory.TrackRecallTenant(tenant);
        return (tenant, Guid.NewGuid());
    }

    /// <summary>An ACTIVE block of a fake case in <paramref name="tenant"/> on the scenario's SKU (the R3 <c>InsertBlock</c> shape, tenant chosen).</summary>
    private static void Block(RecallScenario s, Guid tenant, Guid company, string lotId, string? lotNumber, string caseNumber)
        => s.Factory.Database.GetCollection<RecallLotBlock>(RecallCollections.LotBlocks).InsertOne(new RecallLotBlock
        {
            Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow, TenantId = tenant, LegalEntityId = company, LotId = lotId, LotNumber = lotNumber,
            SkuId = s.Ledger.Sku, SkuLevel = SkuLevel.Lsku, CaseId = Guid.NewGuid(), CaseNumber = caseNumber, Active = true, ActivatedAtUtc = DateTimeOffset.UtcNow
        });

    /// <summary>(e) what a refusal writes, by tenant: the case timeline row (<c>ExitRefused</c>) and the audit row (<c>recall.exit.refused</c>).</summary>
    private static (int Events, int Audits) Refusals(RecallScenario s, Guid tenant)
    {
        var tenantValue = tenant.ToString();
        var events = s.Factory.Database.GetCollection<BsonDocument>(RecallCollections.CaseEvents)
            .CountDocuments(new BsonDocument { ["TenantId"] = tenantValue, ["Kind"] = "ExitRefused" });
        var audits = s.Factory.Database.GetCollection<BsonDocument>("inventory_audit")
            .CountDocuments(new BsonDocument { ["TenantId"] = tenantValue, ["Action"] = "recall.exit.refused" });
        return ((int)events, (int)audits);
    }

    private static async Task<(int Status, JsonElement Body)> SalesIssueAsync(RecallScenario s, string lotText)
        => await RecallR3.PostAsync(s.Lots, RecallR3.WithLot(s.Ledger.Issue("1"), lotText, "MOD-0183"));

    private static void AssertRefused((int Status, JsonElement Body) answer, string caseNumber, string because)
    {
        Assert.True(answer.Status == 422 && RecallR3.Code(answer.Body) == "LOT_UNDER_RECALL", $"{because}: {answer.Status} {answer.Body}");
        Assert.Equal(caseNumber, Str(answer.Body.GetProperty("error").GetProperty("details"), "recallCaseNumber"));
    }

    [Fact]
    public async Task a_ByLotAsync_another_tenants_block_on_the_same_lot_id_refuses_neither_the_pre_check_nor_the_re_check()
    {
        var s = new RecallScenario(_factory);
        var (t2, c2) = OtherTenant();
        var lot = await s.LotAsync();
        var raced = await s.LotAsync();
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", lot, movementType: "GOODS_RECEIPT_PO"));
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", raced, movementType: "GOODS_RECEIPT_PO"));
        var before = s.Persisted("inventory_transactions").Count;

        // pre-check AND re-check read T2's block (it is there before the posting): neither may refuse
        Block(s, t2, c2, lot, null, "RC-T2-LOT");
        var preChecked = await SalesIssueAsync(s, lot);
        Assert.True(preChecked.Status == 201, $"T2's block must not refuse T1 (pre-check): {preChecked.Status} {preChecked.Body}");

        // re-check alone: T2's block lands between the passed pre-check and the posting transaction (the R3 race seam)
        _factory.AfterPassedPreCheck[raced] = () => { Block(s, t2, c2, raced, null, "RC-T2-RACE"); return Task.CompletedTask; };
        var reChecked = await SalesIssueAsync(s, raced);
        Assert.True(reChecked.Status == 201, $"T2's block must not refuse T1 (re-check): {reChecked.Status} {reChecked.Body}");
        Assert.Equal(before + 2, s.Persisted("inventory_transactions").Count);

        // (e) nothing recorded for the refusal that did not happen — in T2 (the block's case) nor in T1
        Assert.Equal((0, 0), Refusals(s, t2));
        Assert.Equal((0, 0), Refusals(s, s.Tenant));

        // (d) control: the same block in T1 refuses — at the pre-check, and at the re-check through the race seam
        Block(s, s.Tenant, s.Ledger.LegalEntity, lot, null, "RC-T1-LOT");
        AssertRefused(await SalesIssueAsync(s, lot), "RC-T1-LOT", "T1's own block (pre-check)");
        _factory.AfterPassedPreCheck[raced] = () => { Block(s, s.Tenant, s.Ledger.LegalEntity, raced, null, "RC-T1-RACE"); return Task.CompletedTask; };
        AssertRefused(await SalesIssueAsync(s, raced), "RC-T1-RACE", "T1's own block (re-check)");
        Assert.Contains(s.Audit("recall.exit.refused"), a => a["Detail"]["stage"] == "pre-check");
        Assert.Contains(s.Audit("recall.exit.refused"), a => a["Detail"]["stage"] == "re-check");
        Assert.Equal((0, 0), Refusals(s, t2)); // still nothing in T2: T1's refusals are written in T1
    }

    [Fact]
    public async Task b_ByTypedLotAsync_another_tenants_block_on_the_same_sku_and_lot_number_does_not_refuse_a_typed_lot()
    {
        var s = new RecallScenario(_factory);
        var (t2, c2) = OtherTenant();
        var typed = "R3T-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var raced = "R3T-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", typed, movementType: "GOODS_RECEIPT_PO"));
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", raced, movementType: "GOODS_RECEIPT_PO"));

        Block(s, t2, c2, Guid.NewGuid().ToString("D"), typed, "RC-T2-TYPED");
        var preChecked = await SalesIssueAsync(s, typed);
        Assert.True(preChecked.Status == 201, $"T2's block must not refuse T1's typed lot (pre-check): {preChecked.Status} {preChecked.Body}");

        _factory.AfterPassedPreCheck[raced] = () => { Block(s, t2, c2, Guid.NewGuid().ToString("D"), raced, "RC-T2-TYPED-RACE"); return Task.CompletedTask; };
        var reChecked = await SalesIssueAsync(s, raced);
        Assert.True(reChecked.Status == 201, $"T2's block must not refuse T1's typed lot (re-check): {reChecked.Status} {reChecked.Body}");
        Assert.Equal((0, 0), Refusals(s, t2));
        Assert.Equal((0, 0), Refusals(s, s.Tenant));

        // (d) control: the same SKU + number in T1 refuses the typed text (both stages)
        Block(s, s.Tenant, s.Ledger.LegalEntity, Guid.NewGuid().ToString("D"), typed, "RC-T1-TYPED");
        AssertRefused(await SalesIssueAsync(s, typed), "RC-T1-TYPED", "T1's own typed block (pre-check)");
        _factory.AfterPassedPreCheck[raced] = () => { Block(s, s.Tenant, s.Ledger.LegalEntity, Guid.NewGuid().ToString("D"), raced, "RC-T1-TYPED-RACE"); return Task.CompletedTask; };
        AssertRefused(await SalesIssueAsync(s, raced), "RC-T1-TYPED-RACE", "T1's own typed block (re-check)");
        Assert.Equal((0, 0), Refusals(s, t2));
    }

    [Fact]
    public async Task c_UnderRecallAsync_another_tenants_block_does_not_exclude_the_lot_from_the_FEFO_order()
    {
        var s = new RecallScenario(_factory);
        var (t2, c2) = OtherTenant();
        var lot = await s.Lots.CreateLotAsync(expiryDate: "2027-03-31");
        await s.Lots.ReleaseAsync(lot);
        await s.Lots.PostOkAsync(s.Lots.Receipt("10", lot, movementType: "GOODS_RECEIPT_PO"));
        var reader = s.Mint(s.Qa1, ["expiry.fefo-order.read"]);
        var path = $"/api/expiry/fefo-order?warehouseId={s.Ledger.Warehouse}&itemId={s.Ledger.Item}&skuId={s.Ledger.Sku}&skuLevel=Lsku";

        Block(s, t2, c2, lot, null, "RC-T2-FEFO");
        var order = await s.ExpectAsync(HttpMethod.Get, path, reader, null, 200);
        Assert.Contains(order.GetProperty("eligible").EnumerateArray(), e => Str(e, "lotId") == lot);
        Assert.DoesNotContain(order.GetProperty("excluded").EnumerateArray(), e => Str(e, "lotId") == lot);

        // (d) control: T1's own block excludes it as RECALLED
        Block(s, s.Tenant, s.Ledger.LegalEntity, lot, null, "RC-T1-FEFO");
        var recalled = await s.ExpectAsync(HttpMethod.Get, path, reader, null, 200);
        Assert.DoesNotContain(recalled.GetProperty("eligible").EnumerateArray(), e => Str(e, "lotId") == lot);
        Assert.Equal("RECALLED", Str(Assert.Single(recalled.GetProperty("excluded").EnumerateArray(), e => Str(e, "lotId") == lot), "reason"));
    }
}
