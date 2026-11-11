using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-4 AC-40 — the dashboard KPIs are counted at read time from what the company's counts and lines say (no stored total), and the manual
/// <c>COUNT_*</c> number is the Variance Trace's own §2.4 classification over the selected warehouse. Compared with the data the test wrote.
/// </summary>
public sealed class CycleCountDashboardTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    [Fact]
    public async Task AC40_the_dashboard_counts_open_counts_awaiting_and_refused_lines_accuracy_and_manual_count_movements_at_read_time()
    {
        var s = new CycleCountScenario(factory);
        var empty = await Expect(s.GetAsync("/api/warehouse/cycle-counts/dashboard"), 200);
        Assert.Equal(0, empty.GetProperty("openCounts").GetInt64());
        Assert.False(empty.TryGetProperty("accuracyPercent", out _));
        Assert.False(empty.TryGetProperty("manualCountMovements", out _));

        // (1) a correction the ledger refuses (a reservation would be uncovered) — Submitted, the line Approved + Refused
        await s.ReceiveAsync(10, s.BinA);
        await s.Inventory.ReserveExpectingAsync(s.Inventory.Reservation("8"), 201);
        var (refusedCount, refusedLine) = await s.SubmittedLineAsync(0, 1);
        Assert.Equal("Refused", (await Expect(s.ApproveAsync(refusedLine), 200)).GetProperty("adjustment").GetProperty("status").GetString());
        // (2) a variance awaiting a decision — Submitted
        await s.SubmittedLineAsync(10, 8, bin: s.Node());
        // (3) an exact count — NoVariance, the count closes
        var (exactCount, _) = await s.SubmittedLineAsync(5, 5, bin: s.Node());
        Assert.Equal("Closed", (await s.CurrentAsync(exactCount)).GetProperty("status").GetString());
        // (4) a released count not counted yet; (5) a Draft — not open
        var bin4 = s.Node();
        await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [bin4])));
        await s.CreatedAsync(s.Form(scope: [s.Node()]));
        // (6) a manual COUNT_LOSS on the warehouse's bin (MOD-0173's own endpoint)
        var manual = s.Inventory.Movement("COUNT_LOSS", "1");
        manual["fromWarehouseId"] = s.Inventory.Warehouse;
        manual["fromLocationId"] = s.BinA.ToString("D");
        manual["sourceModule"] = "MOD-0173";
        manual["sourceType"] = "MANUAL_MOVEMENT";
        await s.Inventory.PostExpectingAsync(manual, 201);

        var all = await Expect(s.GetAsync("/api/warehouse/cycle-counts/dashboard"), 200);
        Assert.Equal(3, all.GetProperty("openCounts").GetInt64());
        Assert.Equal(1, all.GetProperty("awaitingApprovalLines").GetInt64());
        Assert.Equal(1, all.GetProperty("refusedAdjustments").GetInt64());
        Assert.Equal(3, all.GetProperty("countedLines").GetInt64());
        Assert.Equal(1, all.GetProperty("exactLines").GetInt64());
        Assert.Equal("33.3", all.GetProperty("accuracyPercent").GetString());
        Assert.Equal(31, all.GetProperty("periodDays").GetInt32());
        Assert.False(all.TryGetProperty("manualCountMovements", out _)); // the manual read is warehouse-bound (§2.4)

        var warehouse = await Expect(s.GetAsync($"/api/warehouse/cycle-counts/dashboard?warehouseId={s.Warehouse:D}"), 200);
        Assert.Equal(1, warehouse.GetProperty("manualCountMovements").GetInt32());
        Assert.Equal(3, warehouse.GetProperty("openCounts").GetInt64());
        var other = await Expect(s.GetAsync($"/api/warehouse/cycle-counts/dashboard?warehouseId={s.Inventory.Warehouse2}"), 200);
        Assert.Equal(0, other.GetProperty("openCounts").GetInt64());
        Assert.Equal(0, other.GetProperty("manualCountMovements").GetInt32());

        // read time, no stored total: a decision changes the cards at once
        await Expect(s.RejectAsync(await s.LineAsync(refusedCount, LineId(refusedLine))), 200);
        var after = await Expect(s.GetAsync("/api/warehouse/cycle-counts/dashboard"), 200);
        Assert.Equal(0, after.GetProperty("refusedAdjustments").GetInt64());
        Assert.Equal(2, after.GetProperty("openCounts").GetInt64());

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync("/api/warehouse/cycle-counts/dashboard?warehouseId=not-a-uuid"), 400));
        Assert.Equal(403, (int)(await s.GetAsync("/api/warehouse/cycle-counts/dashboard", s.TokenFor(s.User, VariancesRead))).StatusCode);
        var otherCompany = Guid.NewGuid();
        factory.LegalEntities.RegisterName(otherCompany, "OTH", "Other company");
        var foreign = await Expect(s.GetAsync("/api/warehouse/cycle-counts/dashboard", TestTokens.Mint(s.Tenant, otherCompany, s.User, [Read])), 200);
        Assert.Equal(0, foreign.GetProperty("openCounts").GetInt64());
        Assert.Equal(0, foreign.GetProperty("countedLines").GetInt64());
    }
}
