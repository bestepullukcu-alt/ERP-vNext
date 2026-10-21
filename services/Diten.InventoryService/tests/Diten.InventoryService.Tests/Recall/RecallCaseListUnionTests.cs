using System.Text.Json;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R6 — listCases is the UNION read of Vakalar (RECALL 1.5.0, CT decision A): real cases in <c>items</c>, drill runs in
/// <c>drillRows</c> (no class, no level — D-25), ONE page cut on the server over both, every row with sortAt + position, the same page asked
/// twice gives the same rows. Filters: type Real / Drill / absent; status → cases, drillStatus → drills (one kind's status excludes the other
/// kind); class excludes the drill rows (R6 sabotage (g)); q / skuId / openedFrom apply to both. Every answer is checked against 1.5.0.
/// </summary>
public sealed class RecallCaseListUnionTests : IClassFixture<RecallDrillApiFactory>
{
    private readonly RecallDrillApiFactory _factory;

    public RecallCaseListUnionTests(RecallDrillApiFactory factory) => _factory = factory;

    private sealed record World(DrillWorld W, string Token, string A, string B, string Completed, string Cancelled, string CompletedNumber);

    /// <summary>On the frozen clock: case A (class 2) at T, drill R1 (completed) at T+1 h, case B (class 1) at T+2 h, drill R2 (cancelled) at T+3 h.</summary>
    private async Task<World> WorldAsync()
    {
        var w = await DrillWorld.CreateAsync(_factory, resultRequired: false);
        _factory.Clock.Freeze();
        var planId = DrillWorld.PlanId(await w.PlanAsync());
        var a = RecallScenario.Id(await w.S.OpenAsync(await w.S.LotAsync(), @class: 2));
        _factory.Clock.Advance(TimeSpan.FromHours(1));
        var r1 = await w.StepsAsync(await w.StartOkAsync(planId));
        var prepared = await w.PrepareOkAsync(r1);
        var (sc, completed, _) = await w.ResultAsync(DrillWorld.RunId(r1), RecallScenario.ETag(prepared), null);
        Assert.True(sc == 200, completed.ToString());
        _factory.Clock.Advance(TimeSpan.FromHours(1));
        var b = RecallScenario.Id(await w.S.OpenAsync(await w.S.LotAsync(), @class: 1));
        _factory.Clock.Advance(TimeSpan.FromHours(1));
        var r2 = await w.StartOkAsync(planId);
        Assert.Equal(200, (await w.CancelAsync(r2)).Status);
        var token = w.S.Mint(w.S.Management, ["recall.cases.read"]);
        return new World(w, token, a, b, DrillWorld.RunId(r1), DrillWorld.RunId(r2), completed.GetProperty("number").GetString()!);
    }

    private static Task<JsonElement> ListAsync(World x, string query = "") => x.W.S.ExpectAsync(HttpMethod.Get, "/api/recall/cases" + query, x.Token, null, 200);

    /// <summary>The merged page as (kind, id, position), in position order.</summary>
    private static List<(string Kind, string Id, int Position)> Rows(JsonElement page)
    {
        var rows = page.GetProperty("items").EnumerateArray().Select(c => ("Case", c.GetProperty("caseId").GetString()!, c.GetProperty("position").GetInt32())).ToList();
        if (page.TryGetProperty("drillRows", out var drills))
            rows.AddRange(drills.EnumerateArray().Select(d => ("Drill", d.GetProperty("runId").GetString()!, d.GetProperty("position").GetInt32())));
        return rows.OrderBy(r => r.Item3).ToList();
    }

    [Fact]
    public async Task One_page_over_cases_and_drill_runs_newest_first_with_sortAt_and_position()
    {
        var x = await WorldAsync();
        var page = await ListAsync(x);
        Assert.Equal(4, page.GetProperty("total").GetInt32());
        Assert.Equal([("Drill", x.Cancelled, 0), ("Case", x.B, 1), ("Drill", x.Completed, 2), ("Case", x.A, 3)], Rows(page));
        Assert.All(page.GetProperty("items").EnumerateArray(), c => Assert.Equal("Real", c.GetProperty("type").GetString()));

        var drill = Assert.Single(page.GetProperty("drillRows").EnumerateArray(), d => d.GetProperty("runId").GetString() == x.Completed);
        Assert.Equal(("Drill", "Completed", x.CompletedNumber), (drill.GetProperty("type").GetString(), drill.GetProperty("status").GetString(), drill.GetProperty("number").GetString()));
        Assert.False(drill.TryGetProperty("class", out _));   // D-25: a drill row carries no class …
        Assert.False(drill.TryGetProperty("level", out _));   // … and no level
        Assert.True(drill.GetProperty("passed").GetBoolean());
        Assert.Equal(drill.GetProperty("startedAtUtc").GetString(), drill.GetProperty("sortAt").GetString());
        Assert.Equal(("1", "0", "1"), (page.GetProperty("drillStatusCounts").GetProperty("Completed").ToString(),
            page.GetProperty("drillStatusCounts").GetProperty("Running").ToString(), page.GetProperty("drillStatusCounts").GetProperty("Cancelled").ToString()));
    }

    [Fact]
    public async Task Paging_cuts_the_merged_order_and_the_same_page_gives_the_same_rows()
    {
        var x = await WorldAsync();
        var first = await ListAsync(x, "?page=1&pageSize=2");
        var second = await ListAsync(x, "?page=2&pageSize=2");
        Assert.Equal([("Drill", x.Cancelled, 0), ("Case", x.B, 1)], Rows(first));
        Assert.Equal([("Drill", x.Completed, 0), ("Case", x.A, 1)], Rows(second));
        Assert.Equal(Rows(second), Rows(await ListAsync(x, "?page=2&pageSize=2")));
        Assert.Equal(4, second.GetProperty("total").GetInt32());

        // number order: RC-yyyy-NNNN (cases) sort before RC-yyyy-DNNN (drills), ascending
        var byNumber = Rows(await ListAsync(x, "?sort=number"));
        Assert.Equal(["Case", "Case", "Drill", "Drill"], byNumber.Select(r => r.Kind));
    }

    [Fact]
    public async Task Type_and_status_filters_pick_their_kind()
    {
        var x = await WorldAsync();
        var real = await ListAsync(x, "?type=Real");
        Assert.Equal([("Case", x.B, 0), ("Case", x.A, 1)], Rows(real));
        Assert.False(real.TryGetProperty("drillRows", out _));
        Assert.Equal(2, real.GetProperty("total").GetInt32());

        var drills = await ListAsync(x, "?type=Drill");
        Assert.Equal([("Drill", x.Cancelled, 0), ("Drill", x.Completed, 1)], Rows(drills));
        Assert.Empty(drills.GetProperty("items").EnumerateArray());

        Assert.Equal([("Case", x.B, 0), ("Case", x.A, 1)], Rows(await ListAsync(x, "?status=Draft")));               // a case status only → no drills
        Assert.Equal([("Drill", x.Completed, 0)], Rows(await ListAsync(x, "?drillStatus=Completed")));             // a drill status only → no cases
        Assert.Equal([("Drill", x.Cancelled, 0), ("Case", x.B, 1), ("Case", x.A, 2)], Rows(await ListAsync(x, "?status=Draft&drillStatus=Cancelled")));
        Assert.Equal(400, (await x.W.S.SendAsync(HttpMethod.Get, "/api/recall/cases?drillStatus=Closed", x.Token)).Status);
    }

    [Fact]
    public async Task A_class_filter_leaves_the_drill_rows_out_because_a_drill_has_no_class()
    {
        var x = await WorldAsync();
        var page = await ListAsync(x, "?class=1");
        Assert.Equal([("Case", x.B, 0)], Rows(page));
        Assert.False(page.TryGetProperty("drillRows", out _));
        Assert.Equal(1, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Search_sku_and_date_apply_to_both_kinds()
    {
        var x = await WorldAsync();
        Assert.Equal([("Drill", x.Completed, 0)], Rows(await ListAsync(x, "?q=" + x.CompletedNumber)));
        Assert.Empty(Rows(await ListAsync(x, "?skuId=" + Guid.NewGuid().ToString("D"))));
        var tomorrow = _factory.Clock.GetUtcNow().AddDays(1).ToString("yyyy-MM-dd");
        Assert.Empty(Rows(await ListAsync(x, "?openedFrom=" + tomorrow)));
        var drillSku = (await ListAsync(x, "?type=Drill")).GetProperty("drillRows")[0].GetProperty("item").GetProperty("skuId").GetString();
        Assert.Contains(Rows(await ListAsync(x, "?skuId=" + drillSku)), r => r.Kind == "Drill");
    }

    [Fact]
    public async Task Another_company_lists_neither_the_cases_nor_the_drill_runs()
    {
        var x = await WorldAsync();
        var other = await x.W.S.ExpectAsync(HttpMethod.Get, "/api/recall/cases", x.W.S.Mint(x.W.S.Management, ["recall.cases.read"], Guid.NewGuid()), null, 200);
        Assert.Equal(0, other.GetProperty("total").GetInt32());
        Assert.Empty(Rows(other));
    }
}
