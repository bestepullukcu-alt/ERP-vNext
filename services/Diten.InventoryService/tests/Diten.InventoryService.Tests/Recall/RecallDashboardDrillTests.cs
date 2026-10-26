using System.Text.Json;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-0177-R6 — the drill KPIs of getDashboard (pack §8.9) under the SERVER clock (<see cref="DrillClock"/>): "Son tatbikat süresi" = the newest
/// COMPLETED run vs the CURRENT DrillTargetHours (Hedefte when minutes ≤ hours × 60), the chart = the last 12 completed runs oldest first, the
/// frequency band = days since the last completed run STRICTLY more than DrillFrequencyWarningDays (R6 sabotage (c): <c>&gt;</c> → <c>≥</c>).
/// </summary>
public sealed class RecallDashboardDrillTests : IClassFixture<RecallDrillApiFactory>
{
    private readonly RecallDrillApiFactory _factory;

    public RecallDashboardDrillTests(RecallDrillApiFactory factory) => _factory = factory;

    private static Task<JsonElement> PanelAsync(DrillWorld w, Guid? company = null)
        => w.S.ExpectAsync(HttpMethod.Get, "/api/recall/dashboard", w.S.Mint(w.S.Management, ["recall.dashboard.read"], company), null, 200);

    /// <summary>A run of exactly <paramref name="minutes"/> on the frozen clock, completed unsigned (the policy does not require it).</summary>
    private async Task<JsonElement> CompletedRunAsync(DrillWorld w, string planId, int minutes)
    {
        var run = await w.StartOkAsync(planId);
        run = await w.StepsAsync(run, () => _factory.Clock.Advance(TimeSpan.FromMinutes(minutes)));
        var prepared = await w.PrepareOkAsync(run);
        var (status, completed, _) = await w.ResultAsync(DrillWorld.RunId(run), RecallScenario.ETag(prepared), null);
        Assert.True(status == 200, completed.ToString());
        Assert.Equal(minutes, completed.GetProperty("durationMinutes").GetInt32());
        return completed;
    }

    [Fact]
    public async Task Last_drill_and_chart_are_the_completed_runs_against_the_current_target()
    {
        var w = await DrillWorld.CreateAsync(_factory, resultRequired: false);
        _factory.Clock.Freeze();
        var planId = DrillWorld.PlanId(await w.PlanAsync());

        var empty = await PanelAsync(w);
        Assert.False(empty.TryGetProperty("lastDrill", out _));
        Assert.Empty(empty.GetProperty("drillChart").GetProperty("runs").EnumerateArray());
        Assert.True(empty.GetProperty("frequencyWarning").GetProperty("warning").GetBoolean()); // no completed run at all (R5 rule)

        var fast = await CompletedRunAsync(w, planId, 100);
        _factory.Clock.Advance(TimeSpan.FromHours(1));
        var cancelled = await w.StartOkAsync(planId);
        Assert.Equal(200, (await w.CancelAsync(cancelled)).Status); // a cancelled run is not a drill result
        _factory.Clock.Advance(TimeSpan.FromHours(1));
        var slow = await CompletedRunAsync(w, planId, 300);

        var panel = await PanelAsync(w);
        var last = panel.GetProperty("lastDrill");
        Assert.Equal((DrillWorld.RunId(slow), 300, 4, false), (last.GetProperty("runId").GetString(), last.GetProperty("durationMinutes").GetInt32(),
            last.GetProperty("targetHours").GetInt32(), last.GetProperty("onTarget").GetBoolean()));
        var chart = panel.GetProperty("drillChart");
        Assert.Equal(4, chart.GetProperty("targetHours").GetInt32());
        Assert.Equal([(DrillWorld.RunId(fast), 100, true), (DrillWorld.RunId(slow), 300, false)],
            chart.GetProperty("runs").EnumerateArray().Select(r => (r.GetProperty("runId").GetString(), r.GetProperty("durationMinutes").GetInt32(), r.GetProperty("onTarget").GetBoolean())).ToList());
        Assert.False(panel.GetProperty("frequencyWarning").GetProperty("warning").GetBoolean());

        // the boundary of "Hedefte": exactly the target (240 minutes) is on target
        Assert.True(Diten.InventoryService.Domain.Recall.Rules.RecallDashboardRules.OnTarget(240, 4));
        Assert.False(Diten.InventoryService.Domain.Recall.Rules.RecallDashboardRules.OnTarget(241, 4));

        // another company has no drill
        var other = await PanelAsync(w, Guid.NewGuid());
        Assert.False(other.TryGetProperty("lastDrill", out _));
        Assert.Empty(other.GetProperty("drillChart").GetProperty("runs").EnumerateArray());
    }

    [Fact]
    public async Task The_chart_holds_the_last_twelve_completed_runs_oldest_first()
    {
        var w = await DrillWorld.CreateAsync(_factory, resultRequired: false);
        _factory.Clock.Freeze();
        var planId = DrillWorld.PlanId(await w.PlanAsync());
        var runs = new List<string>();
        for (var i = 1; i <= 13; i++)
        {
            runs.Add(DrillWorld.RunId(await CompletedRunAsync(w, planId, i)));
            _factory.Clock.Advance(TimeSpan.FromMinutes(5));
        }

        var chart = (await PanelAsync(w)).GetProperty("drillChart").GetProperty("runs").EnumerateArray().Select(r => r.GetProperty("runId").GetString()).ToList();
        Assert.Equal(runs.Skip(1), chart);
    }

    [Fact]
    public async Task The_frequency_band_shows_only_after_MORE_than_the_warning_days()
    {
        var w = await DrillWorld.CreateAsync(_factory, resultRequired: false);
        _factory.Clock.Freeze();
        await CompletedRunAsync(w, DrillWorld.PlanId(await w.PlanAsync()), 30);

        _factory.Clock.Advance(TimeSpan.FromDays(180)); // exactly the default 180 days: no band
        var at = (await PanelAsync(w)).GetProperty("frequencyWarning");
        Assert.Equal((180, false, 180), (at.GetProperty("daysSinceLastCompleted").GetInt32(), at.GetProperty("warning").GetBoolean(), at.GetProperty("warningDays").GetInt32()));

        _factory.Clock.Advance(TimeSpan.FromDays(1));
        var after = (await PanelAsync(w)).GetProperty("frequencyWarning");
        Assert.Equal((181, true), (after.GetProperty("daysSinceLastCompleted").GetInt32(), after.GetProperty("warning").GetBoolean()));
    }
}
